namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class AdjustBalanceViewModel
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public decimal CurrentBalance { get; set; }

    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public class BalanceLedgerDataRow
{
    public long Id { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
}

public class TopBalanceHolderViewModel
{
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public decimal Balance { get; set; }
}
