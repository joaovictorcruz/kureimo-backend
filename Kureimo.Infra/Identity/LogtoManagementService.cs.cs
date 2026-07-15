using Kureimo.Domain.Exceptions;
using Kureimo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Kureimo.Infra.Identity
{
    public class LogtoManagementSettings
    {
        public const string SectionName = "Logto:ManagementApi";
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }

    /// <summary>
    /// Fala com a Management API do Logto (server-to-server, via M2M) —
    /// usado hoje só pra setar o primaryEmail depois do onboarding,
    /// já que o "esqueci senha por email" nativo do Logto só olha esse campo.
    /// </summary>
    public class LogtoManagementService : ILogtoManagementService
    {
        private readonly HttpClient _httpClient;
        private readonly string _authority;
        private readonly LogtoManagementSettings _settings;
        private readonly ILogger<LogtoManagementService> _logger;

        private string? _cachedToken;
        private DateTimeOffset _cachedTokenExpiresAt;

        public LogtoManagementService(
            HttpClient httpClient,
            IConfiguration configuration,
            IOptions<LogtoManagementSettings> settings,
            ILogger<LogtoManagementService> logger)
        {
            _httpClient = httpClient;
            _authority = configuration["Logto:Authority"]
                ?? throw new InvalidOperationException("Logto:Authority não configurada.");
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SetPrimaryEmailAsync(string logtoUserId, string email, CancellationToken ct = default)
        {
            var token = await GetManagementTokenAsync(ct);

            using var request = new HttpRequestMessage(HttpMethod.Patch, $"{_authority}/api/users/{logtoUserId}")
            {
                Content = JsonContent.Create(new { primaryEmail = email })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Falha ao atualizar email no Logto para {LogtoUserId}: {Status} — {Body}",
                    logtoUserId, response.StatusCode, body);
                throw new DomainException("Não foi possível confirmar o email. Tente novamente.");
            }
        }

        private async Task<string> GetManagementTokenAsync(CancellationToken ct)
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _cachedTokenExpiresAt)
                return _cachedToken;

            var basicAuth = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_authority}/oidc/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["resource"] = "https://default.logto.app/api",
                    ["scope"] = "all"
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Falha ao obter token M2M do Logto: {Status} — {Body}", response.StatusCode, body);
                throw new DomainException("Não foi possível autenticar com o Logto (M2M).");
            }

            var payload = await response.Content.ReadFromJsonAsync<LogtoTokenResponse>(cancellationToken: ct)
                ?? throw new DomainException("Resposta inválida do Logto ao obter token M2M.");

            _cachedToken = payload.AccessToken;
            _cachedTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn - 60);

            return _cachedToken;
        }

        public async Task DeleteUserAsync(string logtoUserId, CancellationToken ct = default)
        {
            var token = await GetManagementTokenAsync(ct);

            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{_authority}/api/users/{logtoUserId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Usuário {LogtoUserId} já não existia no Logto ao tentar excluir.", logtoUserId);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Falha ao excluir usuário no Logto {LogtoUserId}: {Status} — {Body}", logtoUserId, response.StatusCode, body);
                throw new DomainException("Não foi possível excluir a conta. Tente novamente.");
            }
        }

        private record LogtoTokenResponse(
            [property: JsonPropertyName("access_token")] string AccessToken,
            [property: JsonPropertyName("expires_in")] int ExpiresIn);
    }
}
