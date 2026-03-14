namespace PedagangPulsa.Domain.Entities;

public class IdempotencyKey
{
    public string Key { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid? TransactionId { get; set; }
    public string? ResponseCache { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}
