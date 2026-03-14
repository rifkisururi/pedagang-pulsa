namespace PedagangPulsa.Domain.Entities;

public class ProductLevelPrice
{
    public int Id { get; set; }
    public Guid ProductId { get; set; }
    public int LevelId { get; set; }
    public decimal SellPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Product Product { get; set; } = null!;
    public UserLevel Level { get; set; } = null!;
}
