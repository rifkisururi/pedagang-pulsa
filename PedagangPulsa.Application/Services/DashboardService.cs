using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Application.Services;

public class DashboardService
{
    private readonly IAppDbContext _context;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IAppDbContext context, ILogger<DashboardService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DashboardSummary> GetSummaryAsync()
    {
        var today = DateTime.UtcNow;
        var todayStart = today.Date;
        var todayEnd = today.Date.AddDays(1).AddTicks(-1);
        var yesterdayStart = todayStart.AddDays(-1);
        var yesterdayEnd = todayStart.AddTicks(-1);
        var weekAgo = todayStart.AddDays(-7);

        var todayTransactions = await _context.Transactions
            .Where(t => t.CreatedAt >= todayStart && t.CreatedAt <= todayEnd)
            .ToListAsync();

        var yesterdayTransactions = await _context.Transactions
            .Where(t => t.CreatedAt >= yesterdayStart && t.CreatedAt <= yesterdayEnd)
            .ToListAsync();

        var totalToday = todayTransactions.Count;
        var totalYesterday = yesterdayTransactions.Count;

        var successToday = todayTransactions.Where(t => t.Status == TransactionStatus.Success);
        var revenueToday = successToday.Sum(t => t.SellPrice);
        var costToday = successToday.Sum(t => t.CostPrice ?? 0);
        var profitToday = revenueToday - costToday;

        var successYesterday = yesterdayTransactions.Where(t => t.Status == TransactionStatus.Success);
        var revenueYesterday = successYesterday.Sum(t => t.SellPrice);
        var profitYesterday = revenueYesterday - successYesterday.Sum(t => t.CostPrice ?? 0);

        var failedToday = todayTransactions.Count(t => t.Status == TransactionStatus.Failed);
        var failedRate = totalToday > 0 ? (double)failedToday / totalToday * 100 : 0;

        var activeUsers = await _context.Users
            .CountAsync(u => u.Status == UserStatus.Active);

        var newUsersToday = await _context.Users
            .CountAsync(u => u.CreatedAt >= todayStart && u.CreatedAt <= todayEnd);

        var newUsersYesterday = await _context.Users
            .CountAsync(u => u.CreatedAt >= yesterdayStart && u.CreatedAt <= yesterdayEnd);

        var newUsersThisWeek = await _context.Users
            .CountAsync(u => u.CreatedAt >= weekAgo && u.CreatedAt <= todayEnd);

        var pendingTopups = await _context.TopupRequests
            .Where(t => t.Status == TopupStatus.Pending)
            .ToListAsync();

        var totalUserBalance = await _context.UserBalances
            .SumAsync(b => b.ActiveBalance);

        return new DashboardSummary
        {
            TotalTransactionsToday = totalToday,
            RevenueToday = revenueToday,
            ProfitToday = profitToday,
            ProfitMargin = revenueToday > 0 ? profitToday / revenueToday * 100 : 0,
            FailedTransactions = failedToday,
            FailedRate = failedRate,
            ActiveUsers = activeUsers,
            NewUsersToday = newUsersToday,
            NewUsersYesterday = newUsersYesterday,
            NewUsersThisWeek = newUsersThisWeek,
            PendingTopupCount = pendingTopups.Count,
            PendingTopupAmount = pendingTopups.Sum(t => t.Amount),
            TotalUserBalance = totalUserBalance,
            // vs yesterday comparisons
            RevenueVsYesterday = revenueYesterday > 0 ? (revenueToday - revenueYesterday) / revenueYesterday * 100 : 0,
            TransactionsVsYesterday = totalYesterday > 0 ? (double)(totalToday - totalYesterday) / totalYesterday * 100 : 0,
            ProfitVsYesterday = profitYesterday > 0 ? (double)((profitToday - profitYesterday) / profitYesterday * 100) : 0,
            NewUsersVsYesterday = newUsersYesterday > 0 ? (double)(newUsersToday - newUsersYesterday) / newUsersYesterday * 100 : 0,
        };
    }

    public async Task<HourlyTransactionData> GetHourlyTransactionsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var yesterday = today.AddDays(-1);

        var todayData = await _context.Transactions
            .Where(t => t.CreatedAt >= today && t.CreatedAt < tomorrow)
            .GroupBy(t => t.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .ToListAsync();

        var yesterdayData = await _context.Transactions
            .Where(t => t.CreatedAt >= yesterday && t.CreatedAt < today)
            .GroupBy(t => t.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .ToListAsync();

        var todayByHour = Enumerable.Range(0, 24)
            .ToDictionary(h => h, h => todayData.FirstOrDefault(d => d.Hour == h)?.Count ?? 0);

        var yesterdayByHour = Enumerable.Range(0, 24)
            .ToDictionary(h => h, h => yesterdayData.FirstOrDefault(d => d.Hour == h)?.Count ?? 0);

        return new HourlyTransactionData
        {
            Today = Enumerable.Range(0, 24).Select(h => todayByHour[h]).ToList(),
            Yesterday = Enumerable.Range(0, 24).Select(h => yesterdayByHour[h]).ToList(),
        };
    }

    public async Task<DailyRevenueData> GetDailyRevenueAsync(int days = 7)
    {
        var today = DateTime.UtcNow.Date;

        var data = new List<DailyRevenueItem>();

        for (var i = days - 1; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var nextDay = date.AddDays(1);

            var revenue = await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Success && t.CreatedAt >= date && t.CreatedAt < nextDay)
                .SumAsync(t => t.SellPrice);

            data.Add(new DailyRevenueItem
            {
                Label = date.ToString("ddd"),
                FullDate = date.ToString("dd MMM"),
                Revenue = revenue
            });
        }

        return new DailyRevenueData { Items = data };
    }

    public async Task<List<SupplierStatusItem>> GetSupplierStatusAsync()
    {
        return await _context.Suppliers
            .Include(s => s.Balance)
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierStatusItem
            {
                SupplierId = s.Id,
                Name = s.Name,
                ActiveBalance = s.Balance != null ? s.Balance.ActiveBalance : 0,
                IsActive = s.IsActive,
            })
            .ToListAsync();
    }
}

// DTOs

public class DashboardSummary
{
    public int TotalTransactionsToday { get; set; }
    public decimal RevenueToday { get; set; }
    public decimal ProfitToday { get; set; }
    public decimal ProfitMargin { get; set; }
    public int FailedTransactions { get; set; }
    public double FailedRate { get; set; }
    public int ActiveUsers { get; set; }
    public int NewUsersToday { get; set; }
    public int NewUsersYesterday { get; set; }
    public int NewUsersThisWeek { get; set; }
    public int PendingTopupCount { get; set; }
    public decimal PendingTopupAmount { get; set; }
    public decimal TotalUserBalance { get; set; }

    // vs yesterday
    public decimal RevenueVsYesterday { get; set; }
    public double TransactionsVsYesterday { get; set; }
    public double ProfitVsYesterday { get; set; }
    public double NewUsersVsYesterday { get; set; }
}

public class HourlyTransactionData
{
    public List<int> Today { get; set; } = new();
    public List<int> Yesterday { get; set; } = new();
}

public class DailyRevenueData
{
    public List<DailyRevenueItem> Items { get; set; } = new();
}

public class DailyRevenueItem
{
    public string Label { get; set; } = string.Empty;
    public string FullDate { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class SupplierStatusItem
{
    public int SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal ActiveBalance { get; set; }
    public bool IsActive { get; set; }
}
