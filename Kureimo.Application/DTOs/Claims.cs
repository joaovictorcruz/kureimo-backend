using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Application.DTOs
{
    public record ClaimDto(
        Guid Id,
        Guid PhotocardId,
        Guid UserId,
        string Username,
        string? PhoneNumber,
        DateTimeOffset ClaimedAt,
        int QueuePosition
    );
}
