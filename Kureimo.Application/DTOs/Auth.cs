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
        Guid Id,
        string Username,
        string Email,
        string Role,
        string? PhoneNumber,    
        string? ProfilePicUrl  
    );

    public record ForgotPasswordDto(
        string Email
    );

    public record ResetPasswordDto(
        string Token, 
        string NewPassword
    );
}
