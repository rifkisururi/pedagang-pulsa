using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Domain.Entities;

public class ReferralLog
{
    public Guid Id { get; set; }
    public Guid ReferrerId { get; set; }
    public Guid RefereeId { get; set; }
    public decimal? BonusAmount { get; set; }
    public ReferralBonusStatus BonusStatus { get; set; } = ReferralBonusStatus.Pending;
    public string? Notes { get; set; }
    public Guid? PaidBy { get; set; }
    public DateTime? PaidAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User Referrer { get; set; } = null!;
    public User Referee { get; set; } = null!;
}
