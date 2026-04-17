using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Caching;
using System.Collections.Concurrent;

namespace PedagangPulsa.Infrastructure.Caching;

public class RedisService : IRedisService
{
    private sealed class CacheEntry
    {
        public required string Value { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }
    }

    private static readonly ConcurrentDictionary<string, CacheEntry> Store = new();
    private readonly ILogger<RedisService> _logger;

    public RedisService(ILogger<RedisService> logger)
    {
        _logger = logger;
    }

    public Task SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        Store[key] = new CacheEntry
        {
            Value = value,
            ExpiresAt = expiry.HasValue ? DateTimeOffset.UtcNow.Add(expiry.Value) : null
        };

        _logger.LogDebug("Cache set for {Key}", key);
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key)
    {
        if (!TryGetValidEntry(key, out var entry))
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(entry!.Value);
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(TryGetValidEntry(key, out _));
    }

    public Task RemoveAsync(string key)
    {
        Store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<string?> GetAndRemoveAsync(string key)
    {
        if (!TryGetValidEntry(key, out var entry))
        {
            return Task.FromResult<string?>(null);
        }

        Store.TryRemove(key, out _);
        return Task.FromResult<string?>(entry!.Value);
    }

    public Task<long> TtlAsync(string key)
    {
        if (!TryGetValidEntry(key, out var entry) || entry?.ExpiresAt == null)
        {
            return Task.FromResult(0L);
        }

        var ttl = (long)Math.Ceiling((entry.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalSeconds);
        return Task.FromResult(Math.Max(0L, ttl));
    }

    private static bool TryGetValidEntry(string key, out CacheEntry? entry)
    {
        entry = null;
        if (!Store.TryGetValue(key, out var current))
        {
            return false;
        }

        if (current.ExpiresAt.HasValue && current.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            Store.TryRemove(key, out _);
            return false;
        }

        entry = current;
        return true;
    }
}
