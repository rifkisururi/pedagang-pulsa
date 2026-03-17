namespace PedagangPulsa.Domain.Entities;

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? ApiBaseUrl { get; set; }
    public string? MemberId { get; set; }
    public string? Pin { get; set; }
    public string? Password { get; set; }
    public short TimeoutSeconds { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public SupplierBalance? Balance { get; set; }
    public ICollection<SupplierProduct> SupplierProducts { get; set; } = new List<SupplierProduct>();
    public ICollection<TransactionAttempt> TransactionAttempts { get; set; } = new List<TransactionAttempt>();
    public ICollection<SupplierCallback> SupplierCallbacks { get; set; } = new List<SupplierCallback>();
    public ICollection<SupplierBalanceLedger> Ledgers { get; set; } = new List<SupplierBalanceLedger>();
}
