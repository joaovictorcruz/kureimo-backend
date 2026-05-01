using Kureimo.API.Extensions;
using Kureimo.Application.DTOs;
using Kureimo.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kureimo.API.Controllers
{
    [ApiController]
    [Route("users")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController(UserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Retorna os dados públicos de um usuário pelo ID.
        /// </summary>
        /// <response code="200">Dados do usuário.</response>
        /// <response code="404">Usuário não encontrado.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(
            [FromRoute] Guid id,
            CancellationToken ct)
        {
            var result = await _userService.GetByIdAsync(id, ct);
            return Ok(result);
        }

        /// <summary>
        /// Atualiza username e/ou email do usuário autenticado.
        /// Apenas o próprio usuário pode alterar seus dados.
        /// </summary>
        /// <response code="200">Dados atualizados.</response>
        /// <response code="403">Tentativa de editar outro usuário.</response>
        /// <response code="409">Novo username ou email já em uso.</response>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(
            [FromRoute] Guid id,
            [FromBody] UpdateUserDto dto,
            CancellationToken ct)
        {
            var requestingUserId = User.GetUserId();
            var result = await _userService.UpdateAsync(id, dto, requestingUserId, ct);
            return Ok(result);
        }

        /// <summary>
        /// Altera a senha do usuário autenticado.
        /// Exige a senha atual para confirmação.
        /// </summary>
        /// <response code="204">Senha alterada.</response>
        /// <response code="401">Senha atual incorreta.</response>
        /// <response code="403">Tentativa de alterar senha de outro usuário.</response>
        [HttpPut("{id:guid}/password")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdatePassword(
            [FromRoute] Guid id,
            [FromBody] UpdatePasswordDto dto,
            CancellationToken ct)
        {
            var requestingUserId = User.GetUserId();
            await _userService.UpdatePasswordAsync(id, dto, requestingUserId, ct);
            return NoContent();
        }

        /// <summary>
        /// Atualiza a foto de perfil do usuário autenticado.
        /// Aceita jpg, jpeg, png ou webp. Tamanho máximo: 5MB.
        /// </summary>
        /// <response code="200">Foto atualizada. Retorna o UserDto com a nova URL.</response>
        /// <response code="400">Formato ou tamanho inválido.</response>
        /// <response code="403">Tentativa de alterar foto de outro usuário.</response>
        [HttpPut("{id:guid}/profile-pic")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateProfilePic(
            [FromRoute] Guid id,
            IFormFile file,
            CancellationToken ct)
        {
            var requestingUserId = User.GetUserId();

            await using var stream = file.OpenReadStream();
            var result = await _userService.UpdateProfilePicAsync(
                id, stream, file.FileName, requestingUserId, ct);

            return Ok(result);
        }

        /// <summary>
        /// Promove um Collector para a role de GON.
        /// Operação exclusiva de Admin.
        /// </summary>
        /// <response code="204">Usuário promovido a GON.</response>
        /// <response code="403">Usuário autenticado não é Admin.</response>
        /// <response code="404">Usuário alvo não encontrado.</response>
        [HttpPost("{id:guid}/promote-to-gon")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PromoteToGon(
            [FromRoute] Guid id,
            CancellationToken ct)
        {
            var requestingUserId = User.GetUserId();
            await _userService.PromoteToGonAsync(id, requestingUserId, ct);
            return NoContent();
        }

        /// <summary>
        /// Desativa a conta do usuário autenticado.
        /// Apenas o próprio usuário pode desativar sua conta.
        /// </summary>
        /// <response code="204">Conta desativada.</response>
        /// <response code="403">Tentativa de desativar conta de outro usuário.</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Deactivate(
            [FromRoute] Guid id,
            CancellationToken ct)
        {
            var requestingUserId = User.GetUserId();
            await _userService.DeactivateAsync(id, requestingUserId, ct);
            return NoContent();
        }
    }
}
