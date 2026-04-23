using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Application.DTOs
{
    public record CreateSetDto(
        string Title,
        string? Description,
        DateTimeOffset ClaimOpensAt
    );

    public record UpdateSetDto(
        string? Title,
        string? Description,
        DateTimeOffset? ClaimOpensAt
    );

    public record AddPhotocardDto(
        string ArtistName,
        string Version,
        string ImageUrl
    );

    // Resposta resumida — usada na listagem dos sets do GON
    public record SetDto(
        Guid Id,
        string Title,
        string? Description,
        string AccessToken,
        string Status,
        DateTimeOffset ClaimOpensAt,
        int TotalPhotocards,
        DateTimeOffset CreatedAt
    );

    // Resposta detalhada — usada na página do set acessada via link
    public record SetDetailDto(
        Guid Id,
        string Title,
        string? Description,
        string AccessToken,
        string Status,
        DateTimeOffset ClaimOpensAt,
        IEnumerable<PhotocardDetailDto> Photocards
    );

    public record PhotocardDto(
        Guid Id,
        string ArtistName,
        string Version,
        string ImageUrl,
        int TotalClaims
    );

    public record PhotocardDetailDto(
        Guid Id,
        string ArtistName,
        string Version,
        string ImageUrl,
        IEnumerable<ClaimDto> Claims
    );
}
