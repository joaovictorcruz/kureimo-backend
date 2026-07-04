using Kureimo.API.Middleware;
using Kureimo.Application.Services;
using Kureimo.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Kureimo.API.Controllers
{
    /// <summary>
    /// Endpoints exclusivos para comunicação interna com o Kureimo.Worker.
    /// Protegidos por API Key — nunca expostos ao frontend.
    /// </summary>
    [ApiController]
    [Route("internal")]
    [ServiceFilter(typeof(InternalApiKeyFilter))]
    public class InternalController : ControllerBase
    {
        private readonly SetService _setService;
        private readonly IEmailService _emailService;

        public InternalController(SetService setService, IEmailService emailService)
        {
            _setService = setService;
            _emailService = emailService;   
        }

        /// <summary>
        /// Recebe a notificação do Worker de que um set foi aberto
        /// e dispara o evento SignalR para todos os collectors conectados.
        /// </summary>
        [HttpPost("sets/notify-open")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> NotifySetOpen(
            [FromBody] NotifySetOpenDto dto,
            CancellationToken ct)
        {
            await _setService.NotifySetOpenedAsync(dto.AccessToken, ct);
            return NoContent();
        }

        [HttpPost("logto/send-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SendLogtoEmail([FromBody] LogtoEmailWebhookDto dto, CancellationToken ct)
        {
            await _emailService.SendVerificationCodeAsync(dto.To, dto.Payload.Code, dto.Type, ct);
            return Ok();
        }
    }

    public record NotifySetOpenDto(string AccessToken);
    public record LogtoEmailWebhookDto(string To, string Type, LogtoEmailPayloadDto Payload);
    public record LogtoEmailPayloadDto(string Code);
}
