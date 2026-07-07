using Kureimo.Application.DTOs;
using Kureimo.Domain.Entities;
using Kureimo.Domain.Exceptions;
using Kureimo.Domain.Interfaces;
using Kureimo.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Application.Services
{
    public class ReviewService
    {
        private readonly IReviewRepository _reviewRepository;
        private readonly IUserRepository _userRepository;
        private readonly ISetRepository _setRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ReviewService> _logger;

        public ReviewService(
            IReviewRepository reviewRepository,
            IUserRepository userRepository,
            ISetRepository setRepository,
            IUnitOfWork unitOfWork,
            ILogger<ReviewService> logger)
        {
            _reviewRepository = reviewRepository;
            _userRepository = userRepository;
            _setRepository = setRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<UserProfileDto> GetProfileAsync(Guid targetUserId, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByIdAsync(targetUserId, ct)
                ?? throw new UserNotFoundException();

            var (average, count) = await _reviewRepository.GetRatingSummaryAsync(targetUserId, ct);
            var publishedSets = await _setRepository.CountPublishedByGonIdAsync(targetUserId, ct);

            return new UserProfileDto(
                user.Id, user.Username, user.Role.ToString(), user.ProfilePicUrl,
                average, count, publishedSets);
        }

        public async Task<PagedResultDto<ReviewDto>> GetReviewsAsync(
            Guid targetUserId, int page, int pageSize, CancellationToken ct = default)
        {
            // Clamp: default 5, nunca mais que 50 por página
            pageSize = Math.Clamp(pageSize, 1, 50);
            page = Math.Max(page, 1);

            var (items, totalCount) = await _reviewRepository.GetByTargetIdAsync(targetUserId, page, pageSize, ct);
            var reviewList = items.ToList();

            var authorIds = reviewList.Select(r => r.AuthorUserId).Distinct().ToList();
            var authors = new Dictionary<Guid, User>();
            foreach (var id in authorIds)
            {
                var author = await _userRepository.GetByIdAsync(id, ct);
                if (author is not null) authors[id] = author;
            }

            var dtos = reviewList.Select(r => new ReviewDto(
                r.Id,
                r.AuthorUserId,
                authors.GetValueOrDefault(r.AuthorUserId)?.Username ?? "unknown",
                authors.GetValueOrDefault(r.AuthorUserId)?.ProfilePicUrl,
                r.Rating,
                r.Comment,
                r.CreatedAt));

            return new PagedResultDto<ReviewDto>(
                dtos, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize));
        }

        public async Task<ReviewDto> CreateOrUpdateReviewAsync(
            Guid targetUserId, Guid authorUserId, CreateReviewDto dto, CancellationToken ct = default)
        {
            _ = await _userRepository.GetByIdAsync(targetUserId, ct)
                ?? throw new UserNotFoundException();

            var author = await _userRepository.GetByIdAsync(authorUserId, ct)
                ?? throw new UserNotFoundException();

            var existing = await _reviewRepository.GetByAuthorAndTargetAsync(authorUserId, targetUserId, ct);

            Review review;
            if (existing is not null)
            {
                existing.Update(dto.Rating, dto.Comment);
                _reviewRepository.Update(existing);
                review = existing;
            }
            else
            {
                review = new Review(targetUserId, authorUserId, dto.Rating, dto.Comment);
                await _reviewRepository.AddAsync(review, ct);
            }

            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Review registrado — Alvo: {TargetId} | Autor: {AuthorId} | Nota: {Rating}",
                targetUserId, authorUserId, dto.Rating);

            return new ReviewDto(
                review.Id, review.AuthorUserId, author.Username, author.ProfilePicUrl,
                review.Rating, review.Comment, review.CreatedAt);
        }
    }
}
