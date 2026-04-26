using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Application.DTOs
{
    public record PagedResultDto<T>(
        IEnumerable<T> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages
    );

    public record PaginationDto(
        int Page = 1,
        int PageSize = 10
    );
}
