using Kureimo.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Repositories
{
    public interface IReviewRepository
    {
        Task<Review?> GetByAuthorAndTargetAsync(Guid authorUserId, Guid targetUserId, CancellationToken ct = default);
        Task<(IEnumerable<Review> Items, int TotalCount)> GetByTargetIdAsync(Guid targetUserId, int page, int pageSize, CancellationToken ct = default);
        Task<(double AverageRating, int Count)> GetRatingSummaryAsync(Guid targetUserId, CancellationToken ct = default);
        Task AddAsync(Review review, CancellationToken ct = default);
        void Update(Review review);
    }
}
