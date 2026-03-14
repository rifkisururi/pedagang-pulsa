using Microsoft.Extensions.Logging;
using PedagangPulsa.Infrastructure.Suppliers.DTOs;

namespace PedagangPulsa.Infrastructure.Suppliers;

public abstract class SupplierAdapterBase : ISupplierAdapter
{
    protected readonly ILogger _logger;
    protected readonly HttpClient _httpClient;

    protected SupplierAdapterBase(ILogger logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public abstract string SupplierName { get; }
    public abstract string SupplierCode { get; }

    public abstract Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request);
    public abstract Task<SupplierBalanceResult> CheckBalanceAsync(SupplierBalanceRequest request);
    public abstract Task<SupplierPingResult> PingAsync();

    protected async Task<SupplierPurchaseResult> HandleExceptionAsync(Exception ex, string operation)
    {
        _logger.LogError(ex, "Error during {Operation} with {Supplier}", operation, SupplierName);

        return new SupplierPurchaseResult
        {
            Success = false,
            ErrorCode = "SYSTEM_ERROR",
            Message = ex.Message,
            Timestamp = DateTime.UtcNow
        };
    }

    protected async Task<SupplierPingResult> PingAsync(string url)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            stopwatch.Stop();

            return new SupplierPingResult
            {
                Success = true,
                Message = "Connected",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ping failed for {Supplier}", SupplierName);

            return new SupplierPingResult
            {
                Success = false,
                Message = ex.Message,
                ResponseTimeMs = -1,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
