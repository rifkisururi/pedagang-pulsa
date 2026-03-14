using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public string Destination { get; set; } = string.Empty;
    public decimal SellPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public short CurrentSeq { get; set; } = 1;
    public DateTime PinVerifiedAt { get; set; }
    public string? Sn { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public Supplier? Supplier { get; set; }
    public ICollection<TransactionAttempt> Attempts { get; set; } = new List<TransactionAttempt>();
    public int? SupplierId { get; set; }
    public string? SupplierTrxId { get; set; }
}
