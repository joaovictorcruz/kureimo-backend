using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Application.DTOs
{
    public record RegisterRequestDto(
        string Username,
        string Email,
        string Password,
        string PhoneNumber,
        bool IsGon = false
    );

    public record LoginRequestDto(
        string Email,
        string Password
    );

    public record AuthResponseDto(
        string Username,
        string Email,
        string Role,
        string? PhoneNumber,    
        string? ProfilePicUrl  
    );
}
