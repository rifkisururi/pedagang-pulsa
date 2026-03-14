namespace PedagangPulsa.Domain.Entities;

public class SupplierCallback
{
    public long Id { get; set; }
    public int SupplierId { get; set; }
    public string? RawHeaders { get; set; }
    public string RawPayload { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public bool HmacValid { get; set; }
    public long? AttemptId { get; set; }
    public bool IsProcessed { get; set; } = false;
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Supplier Supplier { get; set; } = null!;
    public TransactionAttempt? Attempt { get; set; }
}
