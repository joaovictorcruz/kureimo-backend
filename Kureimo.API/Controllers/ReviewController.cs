using Kureimo.API.Extensions;
using Kureimo.Application.DTOs;
using Kureimo.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kureimo.API.Controllers
{
    [ApiController]
    [Route("users/{id:guid}")]
    [Authorize]
    public class ReviewController : ControllerBase
    {
        private readonly ReviewService _reviewService;

        public ReviewController(ReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        /// <summary>
        /// Perfil público: média de avaliação, quantidade de reviews e sets publicados.
        /// </summary>
        [HttpGet("profile")]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProfile([FromRoute] Guid id, CancellationToken ct)
        {
            var result = await _reviewService.GetProfileAsync(id, ct);
            return Ok(result);
        }

        /// <summary>
        /// Lista paginada de reviews, mais recentes primeiro. Default: 5 por página, máximo 50.
        /// </summary>
        [HttpGet("reviews")]
        [ProducesResponseType(typeof(PagedResultDto<ReviewDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReviews(
            [FromRoute] Guid id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 5,
            CancellationToken ct = default)
        {
            var result = await _reviewService.GetReviewsAsync(id, page, pageSize, ct);
            return Ok(result);
        }

        /// <summary>
        /// Cria ou atualiza (se o autor já tiver avaliado essa pessoa antes) uma review.
        /// </summary>
        [HttpPost("reviews")]
        [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateReview(
            [FromRoute] Guid id,
            [FromBody] CreateReviewDto dto,
            CancellationToken ct)
        {
            var authorId = User.GetUserId();
            var result = await _reviewService.CreateOrUpdateReviewAsync(id, authorId, dto, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }
    }
}
