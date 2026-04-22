using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;
using DashboardViewModel = PedagangPulsa.Web.Areas.Admin.ViewModels.DashboardViewModel;
using DailyRevenueItem = PedagangPulsa.Web.Areas.Admin.ViewModels.DailyRevenueItem;
using SupplierStatusViewModel = PedagangPulsa.Web.Areas.Admin.ViewModels.SupplierStatusViewModel;

namespace PedagangPulsa.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly DashboardService _dashboardService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(DashboardService dashboardService, ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var summary = await _dashboardService.GetSummaryAsync();
        var hourly = await _dashboardService.GetHourlyTransactionsAsync();
        var revenue = await _dashboardService.GetDailyRevenueAsync(7);
        var suppliers = await _dashboardService.GetSupplierStatusAsync();

        var model = new DashboardViewModel
        {
            TotalTransactionsToday = summary.TotalTransactionsToday,
            RevenueToday = summary.RevenueToday,
            ProfitToday = summary.ProfitToday,
            ProfitMargin = summary.ProfitMargin,
            FailedTransactions = summary.FailedTransactions,
            FailedRate = summary.FailedRate,
            ActiveUsers = summary.ActiveUsers,
            NewUsersToday = summary.NewUsersToday,
            NewUsersThisWeek = summary.NewUsersThisWeek,
            PendingTopupCount = summary.PendingTopupCount,
            PendingTopupAmount = summary.PendingTopupAmount,
            TotalUserBalance = summary.TotalUserBalance,
            RevenueVsYesterday = summary.RevenueVsYesterday,
            TransactionsVsYesterday = summary.TransactionsVsYesterday,
            ProfitVsYesterday = summary.ProfitVsYesterday,
            NewUsersVsYesterday = summary.NewUsersVsYesterday,
            HourlyTransactionsToday = hourly.Today,
            HourlyTransactionsYesterday = hourly.Yesterday,
            DailyRevenueItems = revenue.Items.Select(r => new DailyRevenueItem
            {
                Label = r.Label,
                FullDate = r.FullDate,
                Revenue = r.Revenue
            }).ToList(),
            SupplierStatuses = suppliers.Select(s => new SupplierStatusViewModel
            {
                SupplierId = s.SupplierId,
                Name = s.Name,
                ActiveBalance = s.ActiveBalance,
                IsActive = s.IsActive,
            }).ToList(),
        };

        return View(model);
    }
}
