namespace PedagangPulsa.Domain.Entities;

public class UserLevelConfig
{
    public int Id { get; set; }
    public int LevelId { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation properties
    public UserLevel Level { get; set; } = null!;
}
