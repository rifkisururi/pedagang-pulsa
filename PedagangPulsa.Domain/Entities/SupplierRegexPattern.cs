namespace PedagangPulsa.Domain.Entities;

public class SupplierRegexPattern
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public int SeqNo { get; set; }
    public bool IsTrxSukses { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Regex { get; set; } = string.Empty;
    public string? SampleMessage { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Supplier Supplier { get; set; } = null!;
}
