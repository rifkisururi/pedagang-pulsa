namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class UserDetailViewModel
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ActiveBalance { get; set; }
    public decimal HeldBalance { get; set; }
    public decimal TotalBalance => ActiveBalance + HeldBalance;
    public string CreatedAt { get; set; } = string.Empty;
    public string? ReferralCode { get; set; }
    public string? ReferredBy { get; set; }

    // For balance ledger
    public class BalanceLedgerItem
    {
        public DateTime CreatedAt { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal ActiveBefore { get; set; }
        public decimal ActiveAfter { get; set; }
        public string? Notes { get; set; }
    }
    public List<BalanceLedgerItem> RecentTransactions { get; set; } = new();
}
