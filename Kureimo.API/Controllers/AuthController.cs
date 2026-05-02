using Kureimo.API.Extensions;
using Kureimo.Application.DTOs;
using Kureimo.Application.Services;
using Kureimo.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
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
        private readonly IWebHostEnvironment _env;

        public AuthController(AuthService authService, IWebHostEnvironment env)
        {
            _authService = authService;
            _env = env;

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
            SetAuthCookie(result.token);

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
            var (result, token) = await _authService.LoginAsync(dto, ct);
            SetAuthCookie(token);

            return Ok(result);
        }

        /// <summary>
        /// Retorna os dados do usuário autenticado via cookie.
        /// Usado pelo frontend ao carregar a página para restaurar a sessão.
        /// </summary>
        /// <response code="200">Dados do usuário autenticado.</response>
        /// <response code="401">Cookie ausente ou expirado.</response>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Me(CancellationToken ct)
        {
            var userId = User.GetUserId();
            var result = await _authService.GetMeAsync(userId, ct);
            return Ok(result);
        }

        private void SetAuthCookie(string token)
        {
            var isProd = _env.IsProduction();

            Response.Cookies.Append("kureimo_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = isProd,                                          // false em dev local
                SameSite = isProd ? SameSiteMode.None : SameSiteMode.Lax,// Lax funciona em dev
                Expires = DateTimeOffset.UtcNow.AddMinutes(480)
            });
        }
    }
}
