using HostelSystem.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace HostelSystem.Infrastructure.Services;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        if (_cache.TryGetValue<T>(key, out var value))
            return Task.FromResult<T?>(value);

        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        if (expiry.HasValue)
            _cache.Set(key, value, expiry.Value);
        else
            _cache.Set(key, value);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        // IMemoryCache has no native pattern removal (Redis would use SCAN + DEL).
        // In-memory caching relies on explicit key removal and TTL expiry instead.
        return Task.CompletedTask;
    }
}
