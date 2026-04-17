namespace PedagangPulsa.Domain.Entities;

public class PeerTransfer
{
    public Guid Id { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User FromUser { get; set; } = null!;
    public User ToUser { get; set; } = null!;
}
