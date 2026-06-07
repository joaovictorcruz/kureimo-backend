using Kureimo.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Infra.Cache
{
    public class SetCacheService : ISetCacheService
    {
        private readonly IDistributedCache _cache;
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(45);

        public SetCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        private static string Key(string accessToken) => $"set:{accessToken}";

        public async Task<string?> GetAsync(string accessToken, CancellationToken ct = default)
            => await _cache.GetStringAsync(Key(accessToken), ct);

        public async Task SetAsync(string accessToken, string json, CancellationToken ct = default)
            => await _cache.SetStringAsync(Key(accessToken), json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Ttl
            }, ct);

        public async Task InvalidateAsync(string accessToken, CancellationToken ct = default)
            => await _cache.RemoveAsync(Key(accessToken), ct);
    }
}
