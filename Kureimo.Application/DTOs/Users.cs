using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Application.DTOs
{
    public record UserDto(
        Guid Id,
        string Username,
        string Email,
        string Role,
        bool IsActive,
        DateTimeOffset CreatedAt
    );

    public record UpdateUserDto(
        string? Username,
        string? Email
    );

    public record UpdatePasswordDto(
        string CurrentPassword,
        string NewPassword
    );
}
