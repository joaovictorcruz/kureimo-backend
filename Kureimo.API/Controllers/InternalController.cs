using Kureimo.API.Middleware;
using Kureimo.Application.Services;
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

        public InternalController(SetService setService)
        {
            _setService = setService;
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
    }

    public record NotifySetOpenDto(string AccessToken);
}
