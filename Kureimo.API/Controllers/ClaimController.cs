using Kureimo.API.Extensions;
using Kureimo.API.Middleware;
using Kureimo.Application.DTOs;
using Kureimo.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kureimo.API.Controllers
{
    [ApiController]
    [Route("claims")]
    [Authorize]
    public class ClaimController : ControllerBase
    {
        private readonly ClaimService _claimService;

        public ClaimController(ClaimService claimService)
        {
            _claimService = claimService;
        }

        /// <summary>
        /// Registra o claim do usuário autenticado em um photocard.
        ///
        /// O timestamp usado é o do servidor (capturado pelo RequestTimestampMiddleware),
        /// nunca o do cliente. Isso garante a ordem justa na fila de claims.
        ///
        /// Em caso de concorrência simultânea, o mecanismo de retry com RowVersion
        /// (no ClaimService) garante que todos os claims sejam registrados corretamente.
        /// </summary>
        /// <response code="201">Claim registrado com sucesso.</response>
        /// <response code="409">Usuário já deu claim neste photocard.</response>
        /// <response code="422">Janela de claim não está aberta.</response>
        [HttpPost("{photocardId:guid}")]
        [ProducesResponseType(typeof(ClaimDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> RegisterClaim(
            [FromRoute] Guid photocardId,
            CancellationToken ct)
        {
            var userId = User.GetUserId();

            // Timestamp capturado pelo middleware no momento exato de chegada da request
            var serverTimestamp = (DateTimeOffset)HttpContext.Items[RequestTimestampMiddleware.Key]!;

            var result = await _claimService.RegisterClaimAsync(photocardId, userId, serverTimestamp, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Retorna a lista de claims de um photocard ordenada por posição na fila.
        /// Acessível por qualquer usuário autenticado — collector, GON ou admin.
        /// </summary>
        /// <response code="200">Lista de claims ordenada por QueuePosition.</response>
        /// <response code="404">Photocard não encontrado.</response>
        [HttpGet("photocard/{photocardId:guid}")]
        [ProducesResponseType(typeof(IEnumerable<ClaimDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClaimsByPhotocard(
            [FromRoute] Guid photocardId,
            CancellationToken ct)
        {
            var result = await _claimService.GetClaimsByPhotocardAsync(photocardId, ct);
            return Ok(result);
        }
    }
}
