using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Domain.Entities;

public class UserLevel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MarkupType MarkupType { get; set; } = MarkupType.Percentage;
    public decimal MarkupValue { get; set; }
    public bool CanTransfer { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UserLevelConfig> Configs { get; set; } = new List<UserLevelConfig>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<ProductLevelPrice> ProductLevelPrices { get; set; } = new List<ProductLevelPrice>();
}
