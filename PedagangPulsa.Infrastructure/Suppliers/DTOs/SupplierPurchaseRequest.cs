namespace PedagangPulsa.Infrastructure.Suppliers.DTOs;

public class SupplierPurchaseRequest
{
    public int SupplierId { get; set; }
    public string SupplierUsername { get; set; } = string.Empty;
    public string SupplierApiKey { get; set; } = string.Empty;
    public string? SupplierApiSecret { get; set; }
    public string SupplierApiUrl { get; set; } = string.Empty;
    public string SupplierProductCode { get; set; } = string.Empty;
    public string DestinationNumber { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}
