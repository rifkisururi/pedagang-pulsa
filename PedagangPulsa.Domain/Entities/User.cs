using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public string PinHash { get; set; } = string.Empty;
    public short PinFailedAttempts { get; set; }
    public DateTime? PinLockedAt { get; set; }
    public int LevelId { get; set; }
    public bool? CanTransferOverride { get; set; }
    public string ReferralCode { get; set; } = string.Empty;
    public Guid? ReferredBy { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime? EmailVerifiedAt { get; set; }
    public DateTime? PhoneVerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public UserLevel Level { get; set; } = null!;
    public User? Referrer { get; set; }
    public ICollection<User> Referees { get; set; } = new List<User>();
    public UserBalance? Balance { get; set; }
    public ICollection<BalanceLedger> BalanceLedgers { get; set; } = new List<BalanceLedger>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<TopupRequest> TopupRequests { get; set; } = new List<TopupRequest>();
    public ICollection<PeerTransfer> SentTransfers { get; set; } = new List<PeerTransfer>();
    public ICollection<PeerTransfer> ReceivedTransfers { get; set; } = new List<PeerTransfer>();
    public ICollection<ReferralLog> ReferralLogsAsReferrer { get; set; } = new List<ReferralLog>();
    public ICollection<ReferralLog> ReferralLogsAsReferee { get; set; } = new List<ReferralLog>();
}
