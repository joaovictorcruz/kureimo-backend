# Kureimo — Backend

Kureimo é uma plataforma para a comunidade brasileira de colecionadores de photocards de K-pop. O sistema conecta **GOMs** (Group Order Managers), que organizam pedidos em grupo com fornecedores, e **collectors**, que reservam ("dão claim") os photocards que desejam dentro de um set.

Este repositório contém o backend da aplicação, escrito em **.NET 8** seguindo os princípios de **Clean Architecture**.

---

## Sumário

- [Visão geral do domínio](#visão-geral-do-domínio)
- [Arquitetura de infraestrutura](#arquitetura-de-infraestrutura)
- [Arquitetura de código](#arquitetura-de-código)
- [Stack técnica](#stack-técnica)
- [Estrutura do repositório](#estrutura-do-repositório)
- [Autenticação](#autenticação)
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

---

## Visão geral do domínio

| Conceito | Descrição |
|---|---|
| **User** | Usuário da plataforma. Possui uma `Role`: `Collector`, `Gon` ou `Admin`. Identidade (login, senha, MFA) é gerenciada pelo Logto; este banco guarda o perfil de domínio, referenciado pelo `LogtoId`. |
| **Set** | Uma "coleção" de photocards organizada por um GON, com link de acesso público (`AccessToken`), horário de abertura de claims, imagem e customização visual (cores/fonte). |
| **Photocard** | Item individual dentro de um Set (artista + versão), pelo qual os collectors competem. |
| **Claim** | O registro de que um collector reservou um Photocard, com posição na fila e timestamp de servidor. |
| **Review** | Avaliação (1 a 5 estrelas + comentário) que um usuário deixa no perfil de outro — hoje usado principalmente para avaliar GOMs. |

A regra de negócio central do produto é que **um claim é um compromisso social** dentro da comunidade — por isso existem controles cuidadosos de concorrência (para garantir fila justa), uma janela curta de arrependimento, e nenhuma tolerância para timestamps vindos do cliente.

---

## Arquitetura de infraestrutura

<img width="1541" height="754" alt="image" src="https://github.com/user-attachments/assets/50021141-518e-43a1-9c1b-0a7666e403f2" />


Visão geral de onde cada peça roda hoje:

- **Frontend**: Cloudflare Pages.
- **Identidade (Logto)**: self-hosted no Railway — deliberadamente **fora** do cluster Kubernetes (ver [Autenticação](#autenticação) para o porquê).
- **API e Worker**: containers publicados via CI/CD, rodando em um cluster **Vultr Kubernetes Engine (VKE)** na região de São Paulo — escolhido para minimizar latência no momento crítico do produto: a corrida de claim.
- **Ambientes** (`kureimo-prod` e `kureimo-homolog`) isolados por namespace dentro do mesmo cluster.
- **Postgres e Redis** de produção rodam dentro do próprio cluster (imagens oficiais, não serviço gerenciado); o ambiente de homologação ainda aponta para um projeto Supabase separado.
- Todo tráfego externo entra por um único **Ingress Controller**, evitando múltiplos Load Balancers.

Detalhes de provisionamento de infraestrutura (Terraform, manifests, CI/CD do cluster) vivem no repositório `kureimo-iac`, não aqui.

---

## Arquitetura de código

O projeto segue **Clean Architecture** em 4 camadas, mais um serviço worker separado:

```
Kureimo.Domain        → Entidades, regras de negócio, interfaces (sem dependências externas)
Kureimo.Application   → Casos de uso (Services) e DTOs
Kureimo.Infra         → EF Core, Redis, SignalR, Cloudinary, Resend, Logto (implementações)
Kureimo.API           → Controllers, middlewares, pipeline HTTP
Kureimo.Worker        → BackgroundService independente para jobs agendados
```

Regras principais dessa separação:
- `Domain` não depende de nada — toda regra de negócio relevante vive nas entidades (`Set`, `Photocard`, `Claim`, `User`, `Review`), não nos services.
- `Application` orquestra casos de uso, mas delega validação de regra de negócio ao domínio.
- `Infra` é o único lugar que conhece Postgres, Redis, Cloudinary, Logto (Management API) etc.
- `API` e `Worker` são as duas aplicações executáveis do monorepo — API atende requisições HTTP/SignalR, Worker roda jobs em background e fala com a API via um endpoint interno autenticado por API Key.

---

## Stack técnica

- **.NET 8** / ASP.NET Core Web API
- **PostgreSQL** via EF Core (Npgsql), com retry automático em falhas transientes
- **Concorrência otimista** via `RowVersion` (`xmin`) no `Photocard`
- **Redis** (StackExchange.Redis) para cache de leitura de Sets — com fallback para cache em memória se `Redis` não estiver configurado
- **SignalR** para notificações em tempo real (claims, abertura de set)
- **Cloudinary** para armazenamento de imagens (sets e fotos de perfil)
- **Resend** para envio de e-mails transacionais (domínio próprio verificado, `@kureimo.com`)
- **Logto** (self-hosted) para autenticação — OIDC/JWT Bearer, MFA por SMS (Twilio), login social (Google), Account Center para autogerenciamento de perfil
- **Rate Limiting** nativo do ASP.NET Core (global, por IP real via `ForwardedHeaders`)
- **Docker Compose** para ambiente local; deploy via Kubernetes (Vultr) para API/Worker e Railway para Logto

---

## Estrutura do repositório

```
Kureimo.API/
  Controllers/        → ClaimController, InternalController, SetController, UserController,
                         ReviewController, LogtoWebhookController
  Middleware/          → ExceptionHandlerMiddleware, InternalApiKeyMiddleware,
                         RequestTimestampMiddleware, UserProvisioningMiddleware
  Program.cs           → Composição do pipeline HTTP

Kureimo.Application/
  DTOs/
  Interfaces/
  Services/            → SetService, ClaimService, UserService, ReviewService

Kureimo.Domain/
  Entities/            → BaseEntity, Set, Photocard, Claim, User, Review
  Enums/                → SetStatus, UserRole
  Exceptions/           → Exceções de domínio (mapeadas para HTTP no ExceptionHandlerMiddleware)
  Interfaces/ Repositories/  → inclui ILogtoManagementService, IReviewRepository

Kureimo.Infra/
  Cache/                → SetCacheService (Redis, cache-aside)
  Email/                → ResendEmailService
  Identity/             → LogtoManagementService (Management API do Logto)
  Persistence/          → AppDbContext, UnitOfWork, Repositórios, Migrations
  Realtime/             → SetHub, SignalRNotificationService
  Storage/               → CloudinaryService
  DependencyInjection.cs → Registro central de toda a infraestrutura

Kureimo.Worker/
  Jobs/                 → AutoOpenSetsJob
```

---

## Autenticação

Autenticação é delegada a um **Logto self-hosted** (Railway). O backend nunca vê senha, código de SMS ou código de email — só valida tokens OIDC.

**Por que o Logto fica fora do cluster de São Paulo**: a validação de JWT no `.NET` (`JwtBearer` com `Authority`/`Audience`) não faz chamada de rede a cada request — as chaves públicas (JWKS) são buscadas uma vez e cacheadas localmente (~24h). Toda validação subsequente é verificação de assinatura local. O Logto só é acionado em momentos que não coincidem com o pico de disputa de claim: login inicial, refresh silencioso de token, e chamadas administrativas nossas via Management API (onboarding, suspensão de conta). Por isso, manter o Logto nos EUA não compromete a experiência do usuário no momento crítico do produto.

**Fluxo resumido**:
1. Front redireciona o usuário para a experiência hospedada do Logto (login com usuário/email + senha + MFA por SMS, ou Google).
2. No cadastro, telefone é obrigatório e verificado por SMS (usado tanto como identificador quanto como segundo fator).
3. Após login, o front chama `GET /users/me` com o access token — a API valida o token localmente e cria o `User` local automaticamente na primeira chamada de um `LogtoId` novo (**JIT provisioning**, feito pelo `UserProvisioningMiddleware`).
4. O usuário completa um onboarding (`POST /users/me/complete-onboarding`) informando email (usado só para recuperação de conta) e escolhendo `Gon` ou `Collector`. Até isso acontecer, `profileCompleted: false` e ações de negócio (criar set, dar claim) são bloqueadas com `403` e `code: "PROFILE_INCOMPLETE"`.
5. Alteração de email/telefone/username/senha acontece no **Account Center** do próprio Logto — não há mais endpoint de troca de senha no backend.
6. "Esqueci minha senha" por email é resolvido via um conector HTTP customizado do Logto, que chama `POST /internal/logto/send-email` (protegido por chave própria) e usa o `ResendEmailService` já existente.

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
9. A qualquer momento, um collector pode visitar o perfil de um GOM (via o `Id` exposto no set) e deixar uma avaliação (nota + comentário).

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
- Criar um set exige que o GON tenha completado o onboarding (`ProfileCompleted`).

---

## Concorrência em Claims

Quando múltiplos collectors tentam dar claim no mesmo photocard ao mesmo tempo, o sistema garante ordem justa através de:

- **Timestamp de servidor**: capturado pelo `RequestTimestampMiddleware` no momento exato em que a request chega — nunca é aceito timestamp vindo do cliente.
- **Concorrência otimista**: o `Photocard` possui `RowVersion`, então o EF Core detecta automaticamente quando duas requisições concorrentes tentam persistir claims no mesmo agregado.
- **Retry com backoff implícito**: o `ClaimService` tenta registrar o claim até 50 vezes em caso de conflito de concorrência, recalculando a posição na fila a cada tentativa.
- Dar claim exige que o collector tenha completado o onboarding (`ProfileCompleted`).

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

- Hub SignalR em `/hubs/set`, autenticado via access token do Logto passado por query string (`?access_token=...`) — necessário porque WebSockets não enviam headers customizados.
- Eventos disparados: claim registrado, claim removido, mudança de status do set (ex.: `Open`).
- Todas as notificações são **fire-and-forget**: falha ao notificar via SignalR nunca impede a operação principal (o claim/mudança de status já foi persistido antes do disparo).

---

## Worker

O `Kureimo.Worker` roda como processo separado (`AutoOpenSetsJob`, um `BackgroundService`):

- A cada 30 segundos, busca sets `Published` cujo `ClaimOpensAt` já passou.
- Abre esses sets (`Open()`) e persiste em lote.
- Notifica a API via endpoint interno (`POST internal/sets/notify-open`, autenticado por API Key) para que a API dispare o SignalR — o Worker não fala diretamente com os clientes conectados.
- Roda com **1 réplica fixa** — não foi desenhado para execução paralela.
- Decisão arquitetural: sem SignalR backplane / sem Kafka. Para a carga de pico esperada, escalonamento horizontal simples da API e esse job de polling são suficientes; fila de mensagens foi avaliada e considerada over-engineering neste estágio.

---

## Endpoints

Prefixos: `/users`, `/sets`, `/claims`, `/internal`.

> Não existe mais um `AuthController`. Login, cadastro, MFA e recuperação de senha acontecem inteiramente na experiência hospedada do Logto — ver [Autenticação](#autenticação).

### Users (`/users`) — requer autenticação (Bearer token do Logto)
| Método | Rota | Descrição |
|---|---|---|
| GET | `/users/me` | Dados do usuário autenticado (cria o usuário local via JIT provisioning na primeira chamada) |
| POST | `/users/me/complete-onboarding` | Define email e role (`Gon`/`Collector`) — obrigatório antes de ações de negócio |
| GET | `/users/{id}` | Dados públicos do usuário |
| PUT | `/users/{id}` | Atualiza username/email (próprio usuário) — ver nota abaixo |
| PUT | `/users/{id}/profile-pic` | Atualiza foto de perfil |
| POST | `/users/{id}/promote-to-gon` | Promove Collector a GON (Admin) |
| DELETE | `/users/{id}` | Desativa conta (próprio usuário) — também suspende no Logto |
| GET | `/users/{id}/profile` | Perfil público: nota média, quantidade de avaliações, sets publicados |
| GET | `/users/{id}/reviews` | Lista paginada de avaliações (`page`, `pageSize`, máx. 50) |
| POST | `/users/{id}/reviews` | Cria ou atualiza (se já existir) uma avaliação do usuário autenticado sobre `{id}` |

> Edição de email, telefone, username e senha é feita pelo usuário no **Account Center** do Logto, não mais por `PUT /users/{id}` — esse endpoint hoje só é relevante para campos que não são de identidade.

### Sets (`/sets`) — requer autenticação
| Método | Rota | Descrição | Quem |
|---|---|---|---|
| GET | `/sets/{accessToken}` | Detalhes do set (photocards + claims) | Autenticado |
| GET | `/sets/mine` | Sets do GON autenticado (paginado) | Gon/Admin |
| POST | `/sets` | Cria set em Draft (multipart, com imagem) | Gon/Admin (onboarding completo) |
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
| POST | `/claims/{photocardId}` | Registra claim (timestamp de servidor, onboarding completo) |
| DELETE | `/claims/{photocardId}` | Remove claim (dentro da janela de arrependimento) |
| GET | `/claims/photocard/{photocardId}` | Lista claims de um photocard, ordenados por fila |

### Internal (`/internal`)
| Método | Rota | Descrição | Autenticação |
|---|---|---|---|
| POST | `/internal/sets/notify-open` | Worker notifica a API para disparar SignalR | `X-Internal-Api-Key` |
| POST | `/internal/logto/send-email` | Webhook do conector HTTP Email do Logto | Header `Authorization` com chave própria (`Logto:EmailWebhookApiKey`) |

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

O Swagger fica disponível em ambiente de desenvolvimento em `/swagger`. Um endpoint `GET /health` (sem autenticação) existe para health checks de infraestrutura (probes do Kubernetes).

Para testar fluxos de autenticação localmente, é necessário um tenant Logto acessível (self-hosted ou apontando para o de homologação) — não há mais fluxo de login mockado no backend.

> Redis não faz parte do `docker-compose.yml` atual — sem a connection string `Redis` configurada, o cache cai automaticamente para memória.

---

## Variáveis de ambiente

| Variável | Descrição |
|---|---|
| `ConnectionStrings__DefaultConnection` | String de conexão do Postgres |
| `ConnectionStrings__Redis` | String de conexão do Redis (opcional — fallback para memória) |
| `Logto__Authority` | URL base do tenant Logto (ex.: `https://auth.kureimo.com`) |
| `Logto__Audience` | API Identifier cadastrado no Logto (ex.: `https://kureimo-api.com`) |
| `Logto__ManagementApi__ClientId` / `ClientSecret` | Credenciais da Application Machine-to-Machine, usadas para chamar a Management API do Logto (ex.: setar email após onboarding, suspender conta) |
| `Logto__EmailWebhookApiKey` | Chave que autentica o webhook de email chamado pelo conector HTTP do Logto |
| `Cloudinary__CloudName` / `ApiKey` / `ApiSecret` | Credenciais do Cloudinary (upload de imagens) |
| `Resend__ApiKey` | Credencial do Resend (e-mails transacionais) |
| `InternalApi__ApiKey` | Chave usada pelo Worker para chamar endpoints internos da API |
| `InternalApi__BaseUrl` | URL base da API, usada pelo Worker (dentro do cluster, aponta para o Service interno, não URL pública) |
| `FrontendUrl` | URL do frontend, usada em e-mails/links |

---

## Deploy

- **API e Worker**: imagens publicadas no Docker Hub via GitHub Actions a cada merge na `main`; o deployment no cluster Vultr (VKE, São Paulo) é aplicado via Terraform, mantido no repositório `kureimo-iac`.
- **Logto**: continua hospedado no Railway (ver [Autenticação](#autenticação) para o porquê de não migrar).
- **Frontend**: Cloudflare Pages.
- Escalonamento horizontal da API (múltiplas réplicas) é a estratégia adotada para lidar com o pico de tráfego esperado (momento de abertura de claims).
