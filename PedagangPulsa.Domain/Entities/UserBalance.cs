namespace PedagangPulsa.Domain.Entities;

public class UserBalance
{
    public Guid UserId { get; set; }
    public decimal ActiveBalance { get; set; }
    public decimal HeldBalance { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
}
