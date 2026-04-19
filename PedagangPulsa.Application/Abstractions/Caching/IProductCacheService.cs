namespace PedagangPulsa.Application.Abstractions.Caching;

public interface IProductCacheService
{
    Task InvalidateProductCacheAsync();
}
