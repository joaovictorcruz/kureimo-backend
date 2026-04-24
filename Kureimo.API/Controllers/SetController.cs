using Kureimo.API.Extensions;
using Kureimo.Application.DTOs;
using Kureimo.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kureimo.API.Controllers
{
    [ApiController]
    [Route("sets")]
    [Authorize]
    public class SetController : ControllerBase
    {
        private readonly SetService _setService;

        public SetController(SetService setService)
        {
            _setService = setService;
        }

        // ── Endpoints de leitura ──────────────────────────────────────────────────

        /// <summary>
        /// Retorna o set completo (com photocards e claims) via link de acesso.
        /// Usado pelo front quando o usuário abre o link recebido do GON.
        /// Sets em Draft não são acessíveis por aqui.
        /// </summary>
        /// <response code="200">Detalhes do set.</response>
        /// <response code="404">Set não encontrado ou ainda em Draft.</response>
        [HttpGet("{accessToken}")]
        [ProducesResponseType(typeof(SetDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByAccessToken(
            [FromRoute] string accessToken,
            CancellationToken ct)
        {
            var result = await _setService.GetByAccessTokenAsync(accessToken, ct);
            return Ok(result);
        }

        /// <summary>
        /// Lista todos os sets criados pelo GON autenticado.
        /// </summary>
        /// <response code="200">Lista de sets do GON.</response>
        [HttpGet("mine")]
        [Authorize(Roles = "Gon,Admin")]
        [ProducesResponseType(typeof(IEnumerable<SetDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMySets(CancellationToken ct)
        {
            var gonId = User.GetUserId();
            var result = await _setService.GetMySetssAsync(gonId, ct);
            return Ok(result);
        }

        // ── Endpoints de criação e edição (GON) ───────────────────────────────────

        /// <summary>
        /// Cria um novo set em status Draft.
        /// </summary>
        /// <response code="201">Set criado.</response>
        /// <response code="403">Usuário não é GON.</response>
        [HttpPost]
        [Authorize(Roles = "Gon,Admin")]
        [ProducesResponseType(typeof(SetDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create(
            [FromBody] CreateSetDto dto,
            CancellationToken ct)
        {
            var gonId = User.GetUserId();
            var result = await _setService.CreateAsync(dto, gonId, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Atualiza título, descrição ou horário de abertura de um set (somente Draft/Published).
        /// </summary>
        /// <response code="204">Atualizado com sucesso.</response>
        /// <response code="403">Não é o dono do set.</response>
        /// <response code="404">Set não encontrado.</response>
        [HttpPut("{accessToken}")]
        [Authorize(Roles = "Gon,Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            [FromRoute] string accessToken,
            [FromBody] UpdateSetDto dto,
            CancellationToken ct)
        {
            var gonId = User.GetUserId();
            await _setService.UpdateAsync(accessToken, dto, gonId, ct);
            return NoContent();
        }

        /// <summary>
        /// Adiciona um photocard ao set. Só é possível antes de fechar o set.
        /// </summary>
        /// <response code="201">Photocard adicionado.</response>
        /// <response code="403">Não é o dono do set.</response>
        /// <response code="404">Set não encontrado.</response>
        [HttpPost("{accessToken}/photocards")]
        [Authorize(Roles = "Gon,Admin")]
        [ProducesResponseType(typeof(PhotocardDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddPhotocard(
            [FromRoute] string accessToken,
            [FromBody] AddPhotocardDto dto,
            CancellationToken ct)
        {
            var gonId = User.GetUserId();
            var result = await _setService.AddPhotocardAsync(accessToken, dto, gonId, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        // ── Endpoints de ciclo de vida do set (GON) ───────────────────────────────

        /// <summary>
        /// Publica o set: o link pode ser compartilhado, mas claims ainda não estão abertos.
        /// Transição: Draft → Published.
        /// </summary>
        /// <response code="204">Set publicado.</response>
        [HttpPost("{accessToken}/publish")]
        [Authorize(Roles = "Gon,Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Publish(
            [FromRoute] string accessToken,
            CancellationToken ct)
        {
            var gonId = User.GetUserId();
            await _setService.PublishAsync(accessToken, gonId, ct);
            return NoContent();
        }

        /// <summary>
        /// Abre o set para claims manualmente.
        /// Transição: Published → Open.
        /// </summary>
        /// <response code="204">Set aberto para claims.</response>
        [HttpPost("{accessToken}/open")]
        [Authorize(Roles = "Gon,Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Open(
            [FromRoute] string accessToken,
            CancellationToken ct)
        {
            var gonId = User.GetUserId();
            await _setService.OpenAsync(accessToken, gonId, ct);
            return NoContent();
        }

        /// <summary>
        /// Encerra o set: não aceita mais claims.
        /// Transição: qualquer status → Closed.
        /// </summary>
        /// <response code="204">Set encerrado.</response>
        [HttpPost("{accessToken}/close")]
        [Authorize(Roles = "Gon,Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Close(
            [FromRoute] string accessToken,
            CancellationToken ct)
        {
            var gonId = User.GetUserId();
            await _setService.CloseAsync(accessToken, gonId, ct);
            return NoContent();
        }
    }
}
