namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class DepositSupplierViewModel
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierCode { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }

    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public class SupplierLedgerDataRow
{
    public long Id { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
}
