namespace PedagangPulsa.Domain.Entities;

public class SupplierBalance
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public decimal ActiveBalance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Supplier Supplier { get; set; } = null!;
    public ICollection<SupplierBalanceLedger> Ledgers { get; set; } = new List<SupplierBalanceLedger>();
}

public class SupplierBalanceLedger
{
    public long Id { get; set; }
    public int SupplierId { get; set; }
    public string Type { get; set; } = string.Empty; // Deposit, Transaction, Refund, Adjustment
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Description { get; set; }
    public string? AdminNote { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Supplier Supplier { get; set; } = null!;
}
