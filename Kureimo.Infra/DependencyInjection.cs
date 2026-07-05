using Kureimo.Application.Services;
using Kureimo.Domain.Interfaces;
using Kureimo.Domain.Repositories;
using Kureimo.Infra.Cache;
using Kureimo.Infra.Email;
using Kureimo.Infra.Identity;
using Kureimo.Infra.Persistence;
using Kureimo.Infra.Persistence.Repositories;
using Kureimo.Infra.Realtime;
using Kureimo.Infra.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Resend;
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
                .AddEmail(configuration)
                .AddCache(configuration)
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
            var authority = configuration["Logto:Authority"]
                   ?? throw new InvalidOperationException("Logto:Authority não configurada.");
            var audience = configuration["Logto:Audience"]
                ?? throw new InvalidOperationException("Logto:Audience não configurada.");

            services.Configure<LogtoManagementSettings>(configuration.GetSection(LogtoManagementSettings.SectionName));
            services.AddHttpClient<ILogtoManagementService, LogtoManagementService>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                // O Logto expõe o discovery document em {ENDPOINT}/oidc/.well-known/openid-configuration
                options.Authority = $"{authority}/oidc";
                options.Audience = audience;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"{authority}/oidc",
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                // SignalR manda o token via query string, não header —
                // mesma mecânica de antes, só que agora é token do Logto.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
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
            services.AddScoped<UserService>();
            services.AddScoped<SetService>();
            services.AddScoped<ClaimService>();
            services.AddScoped<IStorageService, CloudinaryService>();
            services.AddScoped<IEmailService, ResendEmailService>();

            return services;
        }

        private static IServiceCollection AddEmail(this IServiceCollection services,IConfiguration configuration)
        {
            services.AddOptions();
            services.AddHttpClient<ResendClient>();
            services.Configure<ResendClientOptions>(o =>
            {
                o.ApiToken = configuration["Resend:ApiKey"]!;
            });
            services.AddTransient<IResend, ResendClient>();

            return services;
        }

        private static IServiceCollection AddCache(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var redisConnection = configuration.GetConnectionString("Redis");

            if (!string.IsNullOrEmpty(redisConnection))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = "kureimo:";
                });
            }
            else
            {
                // Fallback para memória em dev caso Redis não esteja configurado
                services.AddDistributedMemoryCache();
            }

            services.AddScoped<ISetCacheService, SetCacheService>();

            return services;
        }


        public static IServiceCollection AddInfrastructureForWorker(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDatabase(configuration).AddRepositories();

            return services;
        }

    }
}
