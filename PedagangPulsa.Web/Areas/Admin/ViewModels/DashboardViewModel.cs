namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class DashboardViewModel
{
    // KPI Card Data
    public int TotalTransactionsToday { get; set; }
    public decimal RevenueToday { get; set; }
    public decimal ProfitToday { get; set; }
    public int FailedTransactions { get; set; }
    public int TotalActiveUsers { get; set; }
    public int NewUsersToday { get; set; }
    public int PendingTopupCount { get; set; }
    public decimal PendingTopupAmount { get; set; }
    public decimal TotalUserBalance { get; set; }

    // Chart Data (for future use)
    public List<int> HourlyTransactions { get; set; } = new();
    public List<decimal> DailyRevenue { get; set; } = new();
}
