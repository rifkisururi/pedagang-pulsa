using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Domain.Entities;

public class TopupRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int? BankAccountId { get; set; }
    public decimal Amount { get; set; }
    public string? TransferProofUrl { get; set; }
    public TopupStatus Status { get; set; } = TopupStatus.Pending;
    public string? RejectReason { get; set; }
    public string? Notes { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
    public BankAccount? BankAccount { get; set; }
}
