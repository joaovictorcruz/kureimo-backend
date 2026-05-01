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
        DateTimeOffset ClaimOpensAt,
        string BackgroundColor,
        string FontColor,
        string FontStyle
    );

    public record UpdateSetDto(
        string? Title,
        string? Description,
        DateTimeOffset? ClaimOpensAt,
        string? ImageUrl,
        string? BackgroundColor,
        string? FontColor,
        string? FontStyle
    );

    public record AddPhotocardDto(
        string ArtistName,
        string Version
    );

    // Resposta resumida — usada na listagem dos sets do GON
    public record SetDto(
        Guid Id,
        string Title,
        string? Description,
        string AccessToken,
        string Status,
        string ImageUrl,
        string BackgroundColor,
        string FontColor,
        string FontStyle,
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
        string ImageUrl,
        string BackgroundColor,
        string FontColor,
        string FontStyle,
        DateTimeOffset ClaimOpensAt,
         GonInfoDto Gon,
        IEnumerable<PhotocardDetailDto> Photocards
    );

    public record PhotocardDto(
        Guid Id,
        string ArtistName,
        string Version,
        int TotalClaims
    );

    public record PhotocardDetailDto(
        Guid Id,
        string ArtistName,
        string Version,
        IEnumerable<ClaimDto> Claims
    );

    public record GonInfoDto(
        Guid Id,
        string Username,
        string? ProfilePicUrl
    );
}
