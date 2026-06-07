using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Interfaces
{
    public interface ISetCacheService
    {
        Task<string?> GetAsync(string accessToken, CancellationToken ct = default);
        Task SetAsync(string accessToken, string json, CancellationToken ct = default);
        Task InvalidateAsync(string accessToken, CancellationToken ct = default);
    }
}
