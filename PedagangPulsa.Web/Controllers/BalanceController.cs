using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Finance")]
public class BalanceController : Controller
{
    private readonly BalanceService _balanceService;
    private readonly ILogger<BalanceController> _logger;

    public BalanceController(BalanceService balanceService, ILogger<BalanceController> logger)
    {
        _balanceService = balanceService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> SearchUsers(string term)
    {
        var users = await _balanceService.SearchUsersAsync(term);
        var results = users.Select(u => new
        {
            id = u.Id.ToString(),
            text = $"{u.Username} ({u.Email ?? "No Email"})",
            username = u.Username,
            email = u.Email,
            fullName = u.FullName
        }).ToList();

        return Json(results);
    }

    [HttpGet]
    public async Task<IActionResult> AdjustBalance(Guid userId)
    {
        var user = await _balanceService.GetUserWithBalanceAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        var model = new AdjustBalanceViewModel
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            CurrentBalance = user.Balance?.ActiveBalance ?? 0
        };

        return PartialView("_AdjustBalanceModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustBalance(AdjustBalanceViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Invalid data" });
        }

        if (model.Amount == 0)
        {
            return Json(new { success = false, message = "Amount cannot be zero" });
        }

        var type = model.Amount > 0 ? "ManualCredit" : "ManualDebit";
        var description = model.Amount > 0
            ? $"Manual balance addition by admin"
            : $"Manual balance deduction by admin";

        var result = await _balanceService.AdjustUserBalanceAsync(
            model.UserId,
            model.Amount,
            type,
            description,
            model.Notes,
            User.Identity?.Name);

        if (result)
        {
            return Json(new
            {
                success = true,
                message = $"Balance {(model.Amount > 0 ? "added" : "deducted")} successfully"
            });
        }

        return Json(new { success = false, message = "Failed to adjust balance. Please check if user has sufficient balance for deduction." });
    }

    [HttpPost]
    public async Task<IActionResult> GetData(
        [FromForm] int draw,
        [FromForm] int start,
        [FromForm] int length,
        [FromForm] string? search = null,
        [FromForm] string? type = null,
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

        var (ledgers, totalFiltered, totalRecords) = await _balanceService.GetBalanceLedgersPagedAsync(
            page,
            pageSize,
            search,
            type,
            startDt,
            endDt,
            orderColumn,
            orderDirection);

        var ledgerData = ledgers.Select(l => new BalanceLedgerDataRow
        {
            Id = l.Id,
            CreatedAt = l.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            Username = l.User?.Username ?? "Unknown",
            Type = l.Type.ToString(),
            Amount = l.Amount,
            BalanceBefore = l.ActiveBefore,
            BalanceAfter = l.ActiveAfter,
            Description = l.Notes ?? "-",
            PerformedBy = l.CreatedBy?.ToString() ?? "System",
            AdminNote = l.Notes
        }).ToList();

        return Json(new
        {
            draw = draw,
            recordsTotal = totalRecords,
            recordsFiltered = totalFiltered,
            data = ledgerData
        });
    }
}
