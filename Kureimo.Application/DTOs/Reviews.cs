using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Application.DTOs
{
    public record UserProfileDto(
            Guid Id,
            string Username,
            string Role,
            string? ProfilePicUrl,
            double AverageRating,
            int ReviewCount,
            int PublishedSetsCount
        );

    public record ReviewDto(
        Guid Id,
        Guid AuthorUserId,
        string AuthorUsername,
        string? AuthorProfilePicUrl,
        int Rating,
        string Comment,
        DateTimeOffset CreatedAt
    );

    public record CreateReviewDto(int Rating, string Comment);
}
