using Microsoft.Extensions.Logging;
using PedagangPulsa.Infrastructure.Suppliers.Digiflazz;
using PedagangPulsa.Infrastructure.Suppliers.VIPReseller;

namespace PedagangPulsa.Infrastructure.Suppliers;

public interface ISupplierAdapterFactory
{
    ISupplierAdapter? CreateAdapter(string supplierCode, ILoggerFactory loggerFactory);
}

public class SupplierAdapterFactory : ISupplierAdapterFactory
{
    private readonly ILogger<SupplierAdapterFactory> _logger;

    public SupplierAdapterFactory(ILogger<SupplierAdapterFactory> logger)
    {
        _logger = logger;
    }

    public ISupplierAdapter? CreateAdapter(string supplierCode, ILoggerFactory loggerFactory)
    {
        try
        {
            return supplierCode.ToUpperInvariant() switch
            {
                "DIGIFLAZZ" => new DigiflazzAdapter(
                    loggerFactory.CreateLogger<DigiflazzAdapter>(),
                    new HttpClient()
                ),
                "VIPRESELLER" => new VIPResellerAdapter(
                    loggerFactory.CreateLogger<VIPResellerAdapter>(),
                    new HttpClient()
                ),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create adapter for supplier code: {SupplierCode}", supplierCode);
            return null;
        }
    }
}
