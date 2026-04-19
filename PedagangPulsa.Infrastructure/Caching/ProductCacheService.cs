using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Caching;

namespace PedagangPulsa.Infrastructure.Caching;

public class ProductCacheService : IProductCacheService
{
    private readonly IRedisService _redis;
    private readonly ILogger<ProductCacheService> _logger;

    public ProductCacheService(IRedisService redis, ILogger<ProductCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task InvalidateProductCacheAsync()
    {
        // product:categories
        await _redis.RemoveAsync("product:categories");

        // products:* (semua kombinasi categoryId, operator, levelId, page, pageSize)
        await _redis.RemoveByPatternAsync("products:*");

        _logger.LogInformation("Product cache invalidated");
    }
}
