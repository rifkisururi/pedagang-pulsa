using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Domain.Entities;

public class TransactionAttempt
{
    public long Id { get; set; }
    public Guid TransactionId { get; set; }
    public int SupplierId { get; set; }
    public int SupplierProductId { get; set; }
    public short Seq { get; set; }
    public AttemptStatus Status { get; set; } = AttemptStatus.Pending;
    public string? SupplierRefId { get; set; }
    public string? SupplierTrxId { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public Transaction Transaction { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public SupplierProduct SupplierProduct { get; set; } = null!;
    public ICollection<SupplierCallback> SupplierCallbacks { get; set; } = new List<SupplierCallback>();
}
