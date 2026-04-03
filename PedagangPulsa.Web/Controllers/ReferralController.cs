using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Finance")]
public class ReferralController : Controller
{
    private readonly ReferralService _referralService;
    private readonly ILogger<ReferralController> _logger;

    public ReferralController(ReferralService referralService, ILogger<ReferralController> logger)
    {
        _referralService = referralService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var topReferrers = await _referralService.GetTopReferrersAsync(10);
        ViewBag.TopReferrers = topReferrers;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> GetData(
        [FromForm] int draw,
        [FromForm] int start,
        [FromForm] int length,
        [FromForm] string? search = null,
        [FromForm] string? status = null,
        [FromForm] string? startDate = null,
        [FromForm] string? endDate = null,
        [FromForm] string? orderColumn = null,
        [FromForm] string? orderDirection = null)
    {
        var page = (start / length) + 1;
        var pageSize = length;

        DateTime? startDt = null;
        DateTime? endDt = null;

        if (!string.IsNullOrWhiteSpace(startDate) && DateTime.TryParse(startDate, out var parsedStart))
        {
            startDt = parsedStart;
        }

        if (!string.IsNullOrWhiteSpace(endDate) && DateTime.TryParse(endDate, out var parsedEnd))
        {
            endDt = parsedEnd.AddDays(1).AddTicks(-1);
        }

        var (logs, totalFiltered, totalRecords) = await _referralService.GetReferralLogsPagedAsync(
            page,
            pageSize,
            search,
            status,
            startDt,
            endDt,
            orderColumn,
            orderDirection);

        var logData = logs.Select(l => new ReferralLogDataRow
        {
            Id = l.Id,
            CreatedAt = l.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            ReferrerUsername = l.Referrer?.UserName ?? "Unknown",
            RefereeUsername = l.Referee?.UserName ?? "Unknown",
            BonusAmount = l.BonusAmount ?? 0,
            BonusStatus = l.BonusStatus.ToString(),
            BonusPaidAt = l.PaidAt?.ToString("dd MMM yyyy HH:mm"),
            CancelledAt = l.CancelledAt?.ToString("dd MMM yyyy HH:mm")
        }).ToList();

        return Json(new
        {
            draw = draw,
            recordsTotal = totalRecords,
            recordsFiltered = totalFiltered,
            data = logData
        });
    }

    [HttpPost]
    public async Task<IActionResult> PayBonus(Guid logId)
    {
        var result = await _referralService.PayPendingBonusAsync(logId, User.Identity?.Name);

        if (result)
        {
            return Json(new { success = true, message = "Referral bonus paid successfully" });
        }

        return Json(new { success = false, message = "Failed to pay referral bonus. Log may not exist or bonus already paid." });
    }

    [HttpPost]
    public async Task<IActionResult> CancelBonus(Guid logId, string? reason = null)
    {
        var result = await _referralService.CancelReferralBonusAsync(logId, reason, User.Identity?.Name);

        if (result)
        {
            return Json(new { success = true, message = "Referral bonus cancelled successfully" });
        }

        return Json(new { success = false, message = "Failed to cancel referral bonus. Log may not exist or bonus already paid." });
    }
}
