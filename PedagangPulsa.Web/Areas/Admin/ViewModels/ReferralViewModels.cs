namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class ReferralLogDataRow
{
    public Guid Id { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string ReferrerUsername { get; set; } = string.Empty;
    public string RefereeUsername { get; set; } = string.Empty;
    public decimal BonusAmount { get; set; }
    public string BonusStatus { get; set; } = string.Empty;
    public string? BonusPaidAt { get; set; }
    public string? CancelledAt { get; set; }
}

public class ReferralSummaryViewModel
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public int TotalReferrals { get; set; }
    public decimal TotalBonusPaid { get; set; }
    public decimal PendingBonus { get; set; }
}
