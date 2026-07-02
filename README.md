# Kureimo — Backend

Kureimo é uma plataforma para a comunidade brasileira de colecionadores de photocards de K-pop. O sistema conecta **GOMs** (Group Order Managers), que organizam pedidos em grupo com fornecedores, e **collectors**, que reservam ("dão claim") os photocards que desejam dentro de um set.

Este repositório contém o backend da aplicação, escrito em **.NET 8** seguindo os princípios de **Clean Architecture**.

---

## Sumário

- [Visão geral do domínio](#visão-geral-do-domínio)
- [Arquitetura](#arquitetura)
- [Stack técnica](#stack-técnica)
- [Estrutura do repositório](#estrutura-do-repositório)
- [Fluxo principal](#fluxo-principal)
- [Ciclo de vida de um Set](#ciclo-de-vida-de-um-set)
- [Concorrência em Claims](#concorrência-em-claims)
- [Cache](#cache)
- [Realtime](#realtime)
- [Worker](#worker)
- [Endpoints](#endpoints)
- [Rodando localmente](#rodando-localmente)
- [Variáveis de ambiente](#variáveis-de-ambiente)
- [Deploy](#deploy)
- [Roadmap conhecido](#roadmap-conhecido)

---

## Visão geral do domínio

| Conceito | Descrição |
|---|---|
| **User** | Usuário da plataforma. Possui uma `Role`: `Collector`, `Gon` ou `Admin`. |
| **Set** | Uma "coleção" de photocards organizada por um GON, com link de acesso público (`AccessToken`), horário de abertura de claims, imagem e customização visual (cores/fonte). |
| **Photocard** | Item individual dentro de um Set (artista + versão), pelo qual os collectors competem. |
| **Claim** | O registro de que um collector reservou um Photocard, com posição na fila e timestamp de servidor. |

A regra de negócio central do produto é que **um claim é um compromisso social** dentro da comunidade — por isso existem controles cuidadosos de concorrência (para garantir fila justa), uma janela curta de arrependimento, e nenhuma tolerância para timestamps vindos do cliente.

---

## Arquitetura

O projeto segue **Clean Architecture** em 4 camadas, mais um serviço worker separado:

```
Kureimo.Domain        → Entidades, regras de negócio, interfaces (sem dependências externas)
Kureimo.Application   → Casos de uso (Services) e DTOs
Kureimo.Infra         → EF Core, Redis, SignalR, Cloudinary, Resend, JWT (implementações)
Kureimo.API           → Controllers, middlewares, pipeline HTTP
Kureimo.Worker        → BackgroundService independente para jobs agendados
```

Regras principais dessa separação:
- `Domain` não depende de nada — toda regra de negócio relevante vive nas entidades (`Set`, `Photocard`, `Claim`, `User`), não nos services.
- `Application` orquestra casos de uso, mas delega validação de regra de negócio ao domínio.
- `Infra` é o único lugar que conhece Postgres, Redis, Cloudinary etc.
- `API` e `Worker` são as duas aplicações executáveis do monorepo — API atende requisições HTTP/SignalR, Worker roda jobs em background e fala com a API via um endpoint interno autenticado por API Key.

---

## Stack técnica

- **.NET 8** / ASP.NET Core Web API
- **PostgreSQL** via EF Core (Npgsql), com retry automático em falhas transientes
- **Concorrência otimista** via `RowVersion` (`xmin`) no `Photocard`
- **Redis** (StackExchange.Redis) para cache de leitura de Sets — com fallback para cache em memória se `Redis` não estiver configurado
- **SignalR** para notificações em tempo real (claims, abertura de set)
- **Cloudinary** para armazenamento de imagens (sets e fotos de perfil)
- **Resend** para envio de e-mails transacionais
- **JWT Bearer** para autenticação (⚠️ ver [Roadmap conhecido](#roadmap-conhecido) — em processo de migração)
- **Rate Limiting** nativo do ASP.NET Core (global + política restrita para auth)
- **Docker Compose** para ambiente local; deploy via Railway

---

## Estrutura do repositório

```
Kureimo.API/
  Controllers/        → AuthController, ClaimController, InternalController, SetController, UserController
  Middleware/          → ExceptionHandlerMiddleware, InternalApiKeyMiddleware, RequestTimestampMiddleware
  Program.cs           → Composição do pipeline HTTP

Kureimo.Application/
  DTOs/
  Interfaces/
  Services/            → AuthService, ClaimService, SetService, UserService

Kureimo.Domain/
  Entities/            → BaseEntity, Set, Photocard, Claim, User, PasswordResetToken
  Enums/                → SetStatus, UserRole
  Exceptions/           → Exceções de domínio (mapeadas para HTTP no ExceptionHandlerMiddleware)
  Interfaces/ Repositories/

Kureimo.Infra/
  Cache/                → SetCacheService (Redis, cache-aside)
  Email/                → ResendEmailService
  Persistence/          → AppDbContext, UnitOfWork, Repositórios, Migrations
  Realtime/             → SetHub, SignalRNotificationService
  Security/             → JwtService, PasswordHasher
  Storage/               → CloudinaryService
  DependencyInjection.cs → Registro central de toda a infraestrutura

Kureimo.Worker/
  Jobs/                 → AutoOpenSetsJob
```

---

## Fluxo principal

1. Um **GON** cria um `Set` em status `Draft`, com título, imagem, cores/fonte customizáveis e um `ClaimOpensAt` (mínimo de alguns minutos no futuro).
2. Adiciona um ou mais `Photocard`s ao set.
3. Publica o set (`Draft → Published`) — a partir daí o link (`/set/{AccessToken}`) pode ser compartilhado, mas os claims ainda não estão liberados.
4. Quando chega a hora marcada, o set abre automaticamente para claims (`Published → Open`) — ver [Worker](#worker).
5. Collectors autenticados dão claim nos photocards desejados. A ordem de chegada define a `QueuePosition`.
6. Dentro de uma janela curta após o claim, o collector pode desistir (unclaim). Depois disso o claim é definitivo.
7. O GON encerra o set (`Open → Closed`) manualmente, quando o pedido em grupo está fechado.
8. Sets encerrados podem ser removidos do histórico do GON (soft delete), individualmente ou em lote.

---

## Ciclo de vida de um Set

```
Draft ──publish──▶ Published ──open (manual ou automático)──▶ Open ──close──▶ Closed
  │                    │                                          
  └──────────cancel─────────────────────────────────────────────▶ (Cancelado, fora de Closed)
```

Regras notáveis aplicadas no domínio (`Set.cs`):
- Só pode ser publicado se tiver ao menos um photocard.
- Só pode ser aberto se estiver `Published`.
- Precisa ficar aberto por pelo menos 1 minuto antes de poder ser fechado.
- Não pode ser cancelado se já estiver `Closed`.
- Só pode ser removido do histórico (soft delete) se estiver `Closed`.
- Photocards só podem ser adicionados/removidos/reordenados enquanto o set não estiver `Open` ou `Closed`.

---

## Concorrência em Claims

Quando múltiplos collectors tentam dar claim no mesmo photocard ao mesmo tempo, o sistema garante ordem justa através de:

- **Timestamp de servidor**: capturado pelo `RequestTimestampMiddleware` no momento exato em que a request chega — nunca é aceito timestamp vindo do cliente.
- **Concorrência otimista**: o `Photocard` possui `RowVersion`, então o EF Core detecta automaticamente quando duas requisições concorrentes tentam persistir claims no mesmo agregado.
- **Retry com backoff implícito**: o `ClaimService` tenta registrar o claim até 50 vezes em caso de conflito de concorrência, recalculando a posição na fila a cada tentativa.

---

## Cache

O `SetService.GetByAccessTokenAsync` usa o padrão **Cache-Aside** com Redis:

- Chave: `set:{accessToken}`
- TTL: 45 segundos
- Só sets com status `Published` ou `Open` são cacheados (dados de `Draft` não são expostos publicamente e `Closed` muda pouco/tem baixo tráfego de leitura).
- Toda operação de escrita no set (update, publish, open, close, cancel, adicionar/editar/remover/reordenar photocard, trocar imagem, soft delete) invalida o cache.
- Se a connection string do Redis não estiver configurada, o sistema cai automaticamente para cache em memória (útil em dev).

---

## Realtime

- Hub SignalR em `/hubs/set`, autenticado via JWT passado por query string (`?access_token=...`) — necessário porque WebSockets não enviam headers customizados.
- Eventos disparados: claim registrado, claim removido, mudança de status do set (ex.: `Open`).
- Todas as notificações são **fire-and-forget**: falha ao notificar via SignalR nunca impede a operação principal (o claim/mudança de status já foi persistido antes do disparo).

---

## Worker

O `Kureimo.Worker` roda como processo separado (`AutoOpenSetsJob`, um `BackgroundService`):

- A cada 30 segundos, busca sets `Published` cujo `ClaimOpensAt` já passou.
- Abre esses sets (`Open()`) e persiste em lote.
- Notifica a API via endpoint interno (`POST internal/sets/notify-open`, autenticado por API Key) para que a API dispare o SignalR — o Worker não fala diretamente com os clientes conectados.
- Decisão arquitetural: sem SignalR backplane / sem Kafka. Para a carga de pico esperada, escalonamento vertical e esse job simples de polling são suficientes; a fila de mensagens foi avaliada e considerada over-engineering neste estágio.

---

## Endpoints

Prefixos: `/auth`, `/users`, `/sets`, `/claims`, `/internal`.

> A autenticação atual é via JWT Bearer (com suporte a cookie httpOnly `kureimo_token` e query string para o SignalR). **Esse mecanismo está em processo de migração para Logto** e por isso não está detalhado aqui — será documentado à parte quando a migração for concluída.

### Sets (`/sets`) — requer autenticação
| Método | Rota | Descrição | Quem |
|---|---|---|---|
| GET | `/sets/{accessToken}` | Detalhes do set (photocards + claims) | Autenticado |
| GET | `/sets/mine` | Sets do GON autenticado (paginado) | Gon/Admin |
| POST | `/sets` | Cria set em Draft (multipart, com imagem) | Gon/Admin |
| PUT | `/sets/{accessToken}` | Atualiza título/horário/imagem/cores/fonte | Gon/Admin (dono) |
| PUT | `/sets/{accessToken}/image` | Troca imagem do set | Gon/Admin (dono) |
| POST | `/sets/{accessToken}/photocards` | Adiciona photocard | Gon/Admin (dono) |
| PUT | `/sets/{accessToken}/photocards/{id}` | Edita photocard | Gon/Admin (dono) |
| DELETE | `/sets/{accessToken}/photocards/{id}` | Remove photocard | Gon/Admin (dono) |
| PUT | `/sets/{accessToken}/photocards/reorder` | Reordena photocards | Gon/Admin (dono) |
| POST | `/sets/{accessToken}/publish` | Draft → Published | Gon/Admin (dono) |
| POST | `/sets/{accessToken}/open` | Published → Open (manual) | Gon/Admin (dono) |
| POST | `/sets/{accessToken}/close` | Open → Closed | Gon/Admin (dono) |
| DELETE | `/sets/{accessToken}` | Remove set do histórico (soft delete, requer Closed) | Gon/Admin (dono) |
| DELETE | `/sets/{accessToken}/cancel` | Cancela set (qualquer status exceto Closed) | Gon/Admin (dono) |
| DELETE | `/sets/history` | Limpa todos os sets fechados do GON | Gon/Admin |

### Claims (`/claims`) — requer autenticação
| Método | Rota | Descrição |
|---|---|---|
| POST | `/claims/{photocardId}` | Registra claim (timestamp de servidor) |
| DELETE | `/claims/{photocardId}` | Remove claim (dentro da janela de arrependimento) |
| GET | `/claims/photocard/{photocardId}` | Lista claims de um photocard, ordenados por fila |

### Users (`/users`) — requer autenticação
| Método | Rota | Descrição |
|---|---|---|
| GET | `/users/{id}` | Dados públicos do usuário |
| PUT | `/users/{id}` | Atualiza username/email (próprio usuário) |
| PUT | `/users/{id}/password` | Troca senha (exige senha atual) |
| PUT | `/users/{id}/profile-pic` | Atualiza foto de perfil |
| POST | `/users/{id}/promote-to-gon` | Promove Collector a GON (Admin) |
| DELETE | `/users/{id}` | Desativa conta (próprio usuário) |

### Internal (`/internal`)
Endpoints protegidos por API Key, usados pelo Worker para notificar a API (ex.: disparar SignalR após abrir sets).

---

## Rodando localmente

Pré-requisitos: Docker e Docker Compose.

```bash
docker compose up --build
```

Isso sobe:
- `postgres` — Postgres 16, banco `KureimoDb`
- `api` — Kureimo.API, exposta em `http://localhost:5177`
- `worker` — Kureimo.Worker, roda o `AutoOpenSetsJob` em background

O Swagger fica disponível em ambiente de desenvolvimento em `/swagger`.

> Redis não faz parte do `docker-compose.yml` atual — sem a connection string `Redis` configurada, o cache cai automaticamente para memória.

---

## Variáveis de ambiente

| Variável | Descrição |
|---|---|
| `ConnectionStrings__DefaultConnection` | String de conexão do Postgres |
| `ConnectionStrings__Redis` | String de conexão do Redis (opcional — fallback para memória) |
| `JwtSettings__Secret` / `Issuer` / `Audience` / `ExpirationMinutes` | Configuração do JWT (sujeito a mudança com a migração para Logto) |
| `Cloudinary__CloudName` / `ApiKey` / `ApiSecret` | Credenciais do Cloudinary (upload de imagens) |
| `Resend__ApiKey` | Credencial do Resend (e-mails transacionais) |
| `InternalApi__ApiKey` | Chave usada pelo Worker para chamar endpoints internos da API |
| `InternalApi__BaseUrl` | URL base da API, usada pelo Worker |
| `FrontendUrl` | URL do frontend, usada em e-mails/links |

---

## Deploy

- Deploy automatizado via GitHub Actions (`.github/workflows/deploy-prod.yml`) para **Railway**.
- API e Worker são publicados como dois serviços/containers separados (`Dockerfile.api` e `Dockerfile.worker`), compartilhando o mesmo banco Postgres.
- Escalonamento vertical é a estratégia adotada para lidar com o pico de tráfego esperado (momento de abertura de claims), em vez de arquitetura distribuída com backplane de SignalR.

---

## Roadmap conhecido

- **Autenticação**: migração do esquema atual (JWT emitido internamente) para **Logto**. A documentação de autenticação será reescrita após essa mudança.
- **Identidade da comunidade**: em vez de 2FA por SMS, a direção adotada é um sistema de reputação/feedback entre usuários, ainda a ser detalhado.
