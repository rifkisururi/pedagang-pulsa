using PedagangPulsa.Infrastructure.Suppliers.DTOs;

namespace PedagangPulsa.Infrastructure.Suppliers;

public interface ISupplierAdapter
{
    string SupplierName { get; }
    string SupplierCode { get; }

    /// <summary>
    /// Purchase a product from the supplier
    /// </summary>
    Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request);

    /// <summary>
    /// Check current balance with the supplier
    /// </summary>
    Task<SupplierBalanceResult> CheckBalanceAsync(SupplierBalanceRequest request);

    /// <summary>
    /// Ping the supplier API to check connectivity
    /// </summary>
    Task<SupplierPingResult> PingAsync();
}
