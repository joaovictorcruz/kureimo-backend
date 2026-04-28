using Kureimo.Domain.Interfaces;
using Kureimo.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kureimo.Worker.Jobs
{
    public class AutoOpenSetsJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AutoOpenSetsJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        public AutoOpenSetsJob(
            IServiceScopeFactory scopeFactory,
            IHttpClientFactory httpClientFactory,
            ILogger<AutoOpenSetsJob> logger)
        {
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoOpenSetsJob iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task ProcessAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var setRepository = scope.ServiceProvider
                    .GetRequiredService<ISetRepository>();
                var unitOfWork = scope.ServiceProvider
                    .GetRequiredService<IUnitOfWork>();

                var sets = await setRepository.GetPublishedDueForOpenAsync(ct);
                var openedTokens = new List<string>();

                foreach (var set in sets)
                {
                    set.Open();
                    openedTokens.Add(set.AccessToken);
                }

                if (openedTokens.Count == 0)
                    return;

                // Persiste todos os Opens de uma vez
                await unitOfWork.CommitAsync(ct);

                _logger.LogInformation(
                    "AutoOpenSetsJob: {Count} set(s) abertos.", openedTokens.Count);

                // Notifica a API para disparar o SignalR para cada set aberto
                var client = _httpClientFactory.CreateClient("KureimoApi");

                foreach (var accessToken in openedTokens)
                {
                    await NotifyApiAsync(client, accessToken, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no AutoOpenSetsJob.");
            }
        }

        private async Task NotifyApiAsync(
            HttpClient client,
            string accessToken,
            CancellationToken ct)
        {
            try
            {
                var payload = JsonSerializer.Serialize(new { accessToken });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    "internal/sets/notify-open", content, ct);

                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning(
                        "Falha ao notificar API para set {AccessToken}: {Status}",
                        accessToken, response.StatusCode);
            }
            catch (Exception ex)
            {
                // Falha de notificação não é crítica — o Open já foi persistido no banco
                // O collector vai ver o status correto no próximo GET
                _logger.LogError(ex,
                    "Erro ao notificar API para set {AccessToken}", accessToken);
            }
        }
    }
}
