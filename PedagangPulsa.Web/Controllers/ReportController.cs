using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;

namespace PedagangPulsa.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Finance")]
public class ReportController : Controller
{
    private readonly ReportService _reportService;
    private readonly ILogger<ReportController> _logger;

    public ReportController(ReportService reportService, ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Daily(DateTime date)
    {
        if (date == default)
        {
            date = DateTime.Today;
        }

        var report = await _reportService.GetDailyProfitReportAsync(date);
        return PartialView("_DailyReport", report);
    }

    [HttpPost]
    public async Task<JsonResult> DailyData(DateTime date)
    {
        if (date == default)
        {
            date = DateTime.Today;
        }

        var report = await _reportService.GetDailyProfitReportAsync(date);
        return Json(new
        {
            success = true,
            data = report
        });
    }

    [HttpPost]
    public async Task<JsonResult> SummaryData(DateTime startDate, DateTime endDate)
    {
        var summary = await _reportService.GetDailyProfitSummaryAsync(startDate, endDate);
        return Json(new
        {
            success = true,
            data = summary
        });
    }

    [HttpPost]
    public async Task<IActionResult> BySupplier(DateTime? startDate, DateTime? endDate)
    {
        var report = await _reportService.GetProfitBySupplierAsync(startDate, endDate);
        return PartialView("_BySupplierReport", report);
    }

    [HttpPost]
    public async Task<JsonResult> BySupplierData(DateTime? startDate, DateTime? endDate)
    {
        var report = await _reportService.GetProfitBySupplierAsync(startDate, endDate);
        return Json(new
        {
            success = true,
            data = report
        });
    }

    [HttpPost]
    public async Task<IActionResult> ByProduct(DateTime? startDate, DateTime? endDate)
    {
        var report = await _reportService.GetProfitByProductAsync(startDate, endDate);
        return PartialView("_ByProductReport", report);
    }

    [HttpPost]
    public async Task<JsonResult> ByProductData(DateTime? startDate, DateTime? endDate)
    {
        var report = await _reportService.GetProfitByProductAsync(startDate, endDate);
        return Json(new
        {
            success = true,
            data = report
        });
    }
}
