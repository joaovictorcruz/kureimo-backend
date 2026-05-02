using Kureimo.API.Middleware;
using Kureimo.Infra;
using Kureimo.Infra.Realtime;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<InternalApiKeyFilter>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Kureimo API",
        Version = "v1",
        Description = "API para gerenciamento de claims de photocards"
    });

    // Habilita autenticação JWT diretamente no Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT. Exemplo: Bearer {seu_token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    // Política global — 60 requests por minuto por IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Política mais restrita para auth — evita brute force
    options.AddFixedWindowLimiter("auth", authOptions =>
    {
        authOptions.PermitLimit = 10;
        authOptions.Window = TimeSpan.FromMinutes(1);
        authOptions.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── CORS ──────────────────────────────────────────────────────────────────────
// AllowCredentials() é obrigatório para o SignalR funcionar corretamente.
// Ajuste a origin conforme o ambiente (dev = Vite, prod = domínio do frontend).
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",  // Vite dev server padrão
                "http://localhost:3000"   // Alternativa React
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Necessário para SignalR (WebSocket com cookie/token)
    });
});

builder.Services.AddHttpContextAccessor();

// ── Infraestrutura (DB, JWT, Repositórios, SignalR, Services) ─────────────────
// Tudo registrado em Kureimo.Infra/DependencyInjection.cs
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5 * 1024 * 1024; // MAX 5MB
});

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Pipeline ──────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseCors("FrontendPolicy");

// Mapeia exceções de domínio para respostas HTTP antes de qualquer controller
app.UseMiddleware<ExceptionHandlerMiddleware>();

// Captura o timestamp exato da request — lido pelo ClaimController
app.UseMiddleware<RequestTimestampMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Hub SignalR — o cliente se conecta em /hubs/set e passa o JWT via query string:
// /hubs/set?access_token=<jwt>
app.MapHub<SetHub>("/hubs/set");

app.Run();
