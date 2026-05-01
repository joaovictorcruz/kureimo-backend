using Kureimo.Application.DTOs;
using Kureimo.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kureimo.API.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Registra um novo usuário (Collector ou GON).
        /// </summary>
        /// <response code="201">Usuário criado com token JWT.</response>
        /// <response code="409">Email ou username já em uso.</response>
        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequestDto dto,
            CancellationToken ct)
        {
            var result = await _authService.RegisterAsync(dto, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Autentica um usuário e retorna um token JWT.
        /// </summary>
        /// <response code="200">Login realizado com sucesso.</response>
        /// <response code="401">Credenciais inválidas.</response>
        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequestDto dto,
            CancellationToken ct)
        {
            var result = await _authService.LoginAsync(dto, ct);
            return Ok(result);
        }
    }
}
