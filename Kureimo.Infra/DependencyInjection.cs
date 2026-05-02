using Kureimo.Application.Interfaces;
using Kureimo.Application.Services;
using Kureimo.Domain.Interfaces;
using Kureimo.Domain.Repositories;
using Kureimo.Infra.Persistence;
using Kureimo.Infra.Persistence.Repositories;
using Kureimo.Infra.Realtime;
using Kureimo.Infra.Security;
using Kureimo.Infra.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Infra
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services
                .AddDatabase(configuration)
                .AddRepositories()
                .AddSecurity(configuration)
                .AddRealTime()
                .AddApplicationServices();

            return services;
        }

        // ── Banco de Dados ────────────────────────────────────────────────────────

        private static IServiceCollection AddDatabase(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    npgsqlOptions =>
                    {
                        // Retry automático em caso de falha transiente
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorCodesToAdd: null);
                    }));

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }

        // ── Repositórios ──────────────────────────────────────────────────────────

        private static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ISetRepository, SetRepository>();
            services.AddScoped<IPhotocardRepository, PhotocardRepository>();
            services.AddScoped<IClaimRepository, ClaimRepository>();

            return services;
        }

        // ── Segurança: JWT + BCrypt ───────────────────────────────────────────────

        private static IServiceCollection AddSecurity(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Lê a seção JwtSettings do appsettings.json e injeta via IOptions<JwtSettings>
            services.Configure<JwtSettings>(
                configuration.GetSection(JwtSettings.SectionName));

            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();

            // Configura autenticação JWT no pipeline do ASP.NET Core
            var jwtSettings = configuration
                .GetSection(JwtSettings.SectionName)
                .Get<JwtSettings>()!;

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    // Sem tolerância de clock — token expirado = inválido
                    ClockSkew = TimeSpan.Zero
                };

                // Necessário para que o SignalR consiga autenticar via query string
                // O cliente envia: /hubs/set?access_token=...
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Lê o token do cookie httpOnly
                        if (context.Request.Cookies.TryGetValue("kureimo_token", out var cookieToken))
                            context.Token = cookieToken;

                        // Mantém suporte ao query string para o SignalR
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;

                        return Task.CompletedTask;
                    }
                };
            });

            return services;
        }

        // ── Tempo Real: SignalR ───────────────────────────────────────────────────

        private static IServiceCollection AddRealTime(this IServiceCollection services)
        {
            services.AddSignalR();
            services.AddScoped<IRealtimeNotificationService, SignalRNotificationService>();

            return services;
        }

        // ── Application Services ──────────────────────────────────────────────────

        private static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<AuthService>();
            services.AddScoped<UserService>();
            services.AddScoped<SetService>();
            services.AddScoped<ClaimService>();
            services.AddScoped<IStorageService, CloudinaryService>();

            return services;
        }

        public static IServiceCollection AddInfrastructureForWorker(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDatabase(configuration).AddRepositories();

            return services;
        }

    }
}
