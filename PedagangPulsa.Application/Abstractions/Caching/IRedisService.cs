namespace PedagangPulsa.Application.Abstractions.Caching;

public interface IRedisService
{
    Task SetAsync(string key, string value, TimeSpan? expiry = null);
    Task<string?> GetAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task RemoveAsync(string key);
    Task<string?> GetAndRemoveAsync(string key);
    Task<long> TtlAsync(string key);
    Task RemoveByPatternAsync(string pattern);
    Task<long> IncrementAsync(string key, TimeSpan? expiry = null);
}
