using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;

namespace PedagangPulsa.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Admin,Finance")]
public class ExportController : Controller
{
    private readonly ExportService _exportService;
    private readonly ILogger<ExportController> _logger;

    public ExportController(ExportService exportService, ILogger<ExportController> logger)
    {
        _exportService = exportService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ExportTransactions(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? status = null,
        int? userId = null)
    {
        try
        {
            var data = await _exportService.ExportTransactionsAsync(startDate, endDate, status, userId);

            var fileName = $"Transactions_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(
                data,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting transactions");
            return BadRequest(new { success = false, message = "Error exporting transactions" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ExportTopupRequests(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? status = null)
    {
        try
        {
            var data = await _exportService.ExportTopupRequestsAsync(startDate, endDate, status);

            var fileName = $"TopupRequests_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(
                data,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting topup requests");
            return BadRequest(new { success = false, message = "Error exporting topup requests" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ExportBalanceLedger(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? type = null,
        Guid? userId = null)
    {
        try
        {
            var data = await _exportService.ExportBalanceLedgerAsync(startDate, endDate, type, userId);

            var fileName = $"BalanceLedger_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(
                data,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting balance ledger");
            return BadRequest(new { success = false, message = "Error exporting balance ledger" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ExportProfitReport(
        DateTime startDate,
        DateTime endDate)
    {
        try
        {
            var data = await _exportService.ExportProfitReportAsync(startDate, endDate);

            var fileName = $"ProfitReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";

            return File(
                data,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting profit report");
            return BadRequest(new { success = false, message = "Error exporting profit report" });
        }
    }
}
