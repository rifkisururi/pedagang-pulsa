namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class DashboardViewModel
{
    // KPI Card Data
    public int TotalTransactionsToday { get; set; }
    public decimal RevenueToday { get; set; }
    public decimal ProfitToday { get; set; }
    public decimal ProfitMargin { get; set; }
    public int FailedTransactions { get; set; }
    public double FailedRate { get; set; }
    public int ActiveUsers { get; set; }
    public int NewUsersToday { get; set; }
    public int NewUsersThisWeek { get; set; }
    public int PendingTopupCount { get; set; }
    public decimal PendingTopupAmount { get; set; }
    public decimal TotalUserBalance { get; set; }

    // vs Yesterday
    public decimal RevenueVsYesterday { get; set; }
    public double TransactionsVsYesterday { get; set; }
    public double ProfitVsYesterday { get; set; }
    public double NewUsersVsYesterday { get; set; }

    // Chart Data
    public List<int> HourlyTransactionsToday { get; set; } = new();
    public List<int> HourlyTransactionsYesterday { get; set; } = new();
    public List<DailyRevenueItem> DailyRevenueItems { get; set; } = new();

    // Supplier Status
    public List<SupplierStatusViewModel> SupplierStatuses { get; set; } = new();
}

public class DailyRevenueItem
{
    public string Label { get; set; } = string.Empty;
    public string FullDate { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class SupplierStatusViewModel
{
    public int SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal ActiveBalance { get; set; }
    public bool IsActive { get; set; }
}
