namespace PedagangPulsa.Domain.Entities;

public class ProductGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Operator { get; set; }
    public int CategoryId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ProductCategory Category { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product
{
    public Guid Id { get; set; }
    public int CategoryId { get; set; }
    public int? ProductGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal? Denomination { get; set; }
    public int? ValidityDays { get; set; }
    public string? ValidityText { get; set; }
    public int? QuotaMb { get; set; }
    public string? QuotaText { get; set; }
    public string? Operator { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ProductCategory Category { get; set; } = null!;
    public ProductGroup? ProductGroup { get; set; }
    public ICollection<ProductLevelPrice> ProductLevelPrices { get; set; } = new List<ProductLevelPrice>();
    public ICollection<SupplierProduct> SupplierProducts { get; set; } = new List<SupplierProduct>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
