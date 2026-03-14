namespace PedagangPulsa.Domain.Entities;

public class SupplierProduct
{
    public int Id { get; set; }
    public Guid ProductId { get; set; }
    public int SupplierId { get; set; }
    public string SupplierProductCode { get; set; } = string.Empty;
    public string? SupplierProductName { get; set; }
    public decimal CostPrice { get; set; }
    public short Seq { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Product Product { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public ICollection<TransactionAttempt> TransactionAttempts { get; set; } = new List<TransactionAttempt>();
}
