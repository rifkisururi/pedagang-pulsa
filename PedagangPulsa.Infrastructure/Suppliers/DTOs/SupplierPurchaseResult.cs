namespace PedagangPulsa.Infrastructure.Suppliers.DTOs;

public class SupplierPurchaseResult
{
    public bool Success { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? SupplierTransactionId { get; set; }
    public string? SupplierMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class SupplierBalanceRequest
{
    public Guid SupplierId { get; set; }
    public string SupplierUsername { get; set; } = string.Empty;
    public string SupplierApiKey { get; set; } = string.Empty;
    public string? SupplierApiSecret { get; set; }
    public string SupplierApiUrl { get; set; } = string.Empty;
}

public class SupplierBalanceResult
{
    public bool Success { get; set; }
    public decimal Balance { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class SupplierPingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long ResponseTimeMs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
