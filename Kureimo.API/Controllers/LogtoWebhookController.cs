using Kureimo.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kureimo.API.Controllers
{
    [ApiController]
    [Route("internal/logto")]
    [AllowAnonymous]
    public class LogtoWebhookController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LogtoWebhookController> _logger;

        public LogtoWebhookController(
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<LogtoWebhookController> logger)
        {
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("send-email")]
        public async Task<IActionResult> SendEmail([FromBody] LogtoEmailWebhookDto dto, CancellationToken ct)
        {
            var expectedKey = _configuration["Logto:EmailWebhookApiKey"]
                ?? throw new InvalidOperationException("Logto:EmailWebhookApiKey não configurada.");

            if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
            {
                _logger.LogWarning("Webhook Logto recebido SEM header Authorization.");
                return Unauthorized();
            }

            var providedAuth = authHeaderValues.ToString().Trim();

            // Remove prefixo "Bearer " se o Logto enviar assim
            if (providedAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                providedAuth = providedAuth["Bearer ".Length..].Trim();

            _logger.LogInformation(
                "Webhook Logto - tamanho recebido: {Len}, tamanho esperado: {ExpLen}",
                providedAuth.Length, expectedKey.Length);

            if (string.IsNullOrEmpty(providedAuth) ||
                !string.Equals(providedAuth, expectedKey, StringComparison.Ordinal))
            {
                return Unauthorized();
            }

            await _emailService.SendVerificationCodeAsync(dto.To, dto.Payload.Code, dto.Type, ct);
            return Ok();
        }
    }
    public record LogtoEmailWebhookDto(string To, string Type, LogtoEmailPayloadDto Payload);
    public record LogtoEmailPayloadDto(string Code);
}
