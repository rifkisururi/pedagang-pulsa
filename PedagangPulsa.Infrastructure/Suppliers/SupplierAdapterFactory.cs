using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Suppliers;
using PedagangPulsa.Infrastructure.Suppliers.Digiflazz;
using PedagangPulsa.Infrastructure.Suppliers.Otomax;
using PedagangPulsa.Infrastructure.Suppliers.VIPReseller;

namespace PedagangPulsa.Infrastructure.Suppliers;

public class SupplierAdapterFactory : ISupplierAdapterFactory
{
    private readonly ILogger<SupplierAdapterFactory> _logger;
    private readonly OtomaxSettings _otomaxSettings;

    public SupplierAdapterFactory(ILogger<SupplierAdapterFactory> logger, OtomaxSettings otomaxSettings)
    {
        _logger = logger;
        _otomaxSettings = otomaxSettings;
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
                "OTOMAX" => new OtomaxAdapter(
                    loggerFactory.CreateLogger<OtomaxAdapter>(),
                    CreateOtomaxHttpClient()
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

    private HttpClient CreateOtomaxHttpClient()
    {
        var handler = _otomaxSettings.CreateHttpMessageHandler();

        if (handler != null)
        {
            _logger.LogInformation("Otomax adapter using proxy: {ProxyHost}:{ProxyPort}",
                _otomaxSettings.ProxyHost, _otomaxSettings.ProxyPort);
            return new HttpClient(handler);
        }

        return new HttpClient();
    }
}
