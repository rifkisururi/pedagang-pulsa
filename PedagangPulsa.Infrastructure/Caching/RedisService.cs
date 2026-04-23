using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Caching;
using StackExchange.Redis;

namespace PedagangPulsa.Infrastructure.Caching;

public sealed class RedisService : IRedisService, IAsyncDisposable
{
    private readonly IDatabase _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisService> _logger;

    public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        _logger.LogDebug("Cache set for {Key} with expiry {Expiry}s", key, expiry?.TotalSeconds);

        if (expiry.HasValue)
        {
            var seconds = (int)expiry.Value.TotalSeconds;
            var result = await _db.StringSetAsync(
                key, value,
                expiry: TimeSpan.FromSeconds(seconds),
                when: When.Always);
            if (!result)
            {
                _logger.LogWarning("Cache set failed for {Key}", key);
            }
        }
        else
        {
            var result = await _db.StringSetAsync(key, value);
            if (!result)
            {
                _logger.LogWarning("Cache set failed for {Key}", key);
            }
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        var value = await _db.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public Task<bool> ExistsAsync(string key)
    {
        return _db.KeyExistsAsync(key);
    }

    public Task RemoveAsync(string key)
    {
        return _db.KeyDeleteAsync(key);
    }

    public async Task<string?> GetAndRemoveAsync(string key)
    {
        var value = await _db.StringGetAsync(key);
        if (value.HasValue)
        {
            await _db.KeyDeleteAsync(key);
        }
        return value.HasValue ? value.ToString() : null;
    }

    public async Task<long> TtlAsync(string key)
    {
        var ttl = await _db.KeyTimeToLiveAsync(key);
        return ttl.HasValue ? (long)ttl.Value.TotalSeconds : 0;
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        var endpoints = _redis.GetEndPoints();
        var server = _redis.GetServer(endpoints[0]);
        var keys = server.Keys(pattern: pattern).ToArray();
        if (keys.Length > 0)
        {
            await _db.KeyDeleteAsync(keys);
            _logger.LogDebug("Cache removed for pattern {Pattern}, {Count} keys deleted", pattern, keys.Length);
        }
    }

    public async Task<long> IncrementAsync(string key, TimeSpan? expiry = null)
    {
        var value = await _db.StringIncrementAsync(key);
        if (expiry.HasValue && value == 1)
        {
            await _db.KeyExpireAsync(key, expiry.Value);
        }
        return value;
    }

    public async ValueTask DisposeAsync()
    {
        if (_redis is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _redis.Dispose();
        }
    }
}
