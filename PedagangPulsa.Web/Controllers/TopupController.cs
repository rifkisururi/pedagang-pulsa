using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Finance")]
public class TopupController : Controller
{
    private readonly TopupService _topupService;
    private readonly ILogger<TopupController> _logger;

    public TopupController(TopupService topupService, ILogger<TopupController> logger)
    {
        _topupService = topupService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var topup = await _topupService.GetTopupRequestByIdAsync(id);
        if (topup == null)
        {
            return NotFound();
        }

        var model = new TopupDetailViewModel
        {
            Id = topup.Id,
            Username = topup.User?.UserName ?? "Unknown",
            FullName = topup.User?.FullName ?? string.Empty,
            Amount = topup.Amount,
            BankName = topup.BankAccount?.BankName ?? "-",
            AccountNumber = topup.BankAccount?.AccountNumber ?? "-",
            AccountName = topup.BankAccount?.AccountName ?? "-",
            TransferProof = topup.TransferProofUrl,
            Status = topup.Status.ToString(),
            CreatedAt = topup.CreatedAt,
            UpdatedAt = topup.UpdatedAt,
            Notes = topup.Notes,
            FinalAmount = topup.Amount,
            RejectReason = topup.RejectReason,
            ApprovedBy = topup.ApprovedBy?.ToString(),
            ApprovedAt = topup.ApprovedAt,
            RejectedBy = topup.RejectedBy?.ToString(),
            RejectedAt = topup.RejectedAt,
            ApprovedNotes = ""
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Approve(Guid id)
    {
        var topup = await _topupService.GetTopupRequestByIdAsync(id);
        if (topup == null)
        {
            return NotFound();
        }

        var currentBalance = topup.User?.Balance?.ActiveBalance ?? 0;

        var model = new ApproveTopupViewModel
        {
            Id = topup.Id,
            Username = topup.User?.UserName ?? "Unknown",
            FullName = topup.User?.FullName ?? string.Empty,
            Amount = topup.Amount,
            FinalAmount = topup.Amount,
            BankName = topup.BankAccount?.BankName ?? "-",
            TransferProof = topup.TransferProofUrl,
            CurrentBalance = currentBalance
        };

        return PartialView("_ApproveModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ApproveTopupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Where(x => x.Value?.Errors.Count > 0)
                .Select(x => $"{x.Key}: {string.Join(", ", x.Value!.Errors.Select(e => e.ErrorMessage))}")
                .ToList();
            return Json(new { success = false, message = "Invalid data: " + string.Join("; ", errors) });
        }

        var result = await _topupService.ApproveTopupAsync(
            model.Id,
            model.FinalAmount,
            model.Notes,
            User.Identity?.Name);

        if (result)
        {
            return Json(new { success = true, message = "Topup approved successfully" });
        }

        return Json(new { success = false, message = "Failed to approve topup" });
    }

    [HttpGet]
    public async Task<IActionResult> Reject(Guid id)
    {
        var topup = await _topupService.GetTopupRequestByIdAsync(id);
        if (topup == null)
        {
            return NotFound();
        }

        var model = new RejectTopupViewModel
        {
            Id = topup.Id,
            Username = topup.User?.UserName ?? "Unknown",
            FullName = topup.User?.FullName ?? string.Empty,
            Amount = topup.Amount,
            BankName = topup.BankAccount?.BankName ?? "-"
        };

        return PartialView("_RejectModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(RejectTopupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Invalid data" });
        }

        if (string.IsNullOrWhiteSpace(model.RejectReason))
        {
            return Json(new { success = false, message = "Reject reason is required" });
        }

        var result = await _topupService.RejectTopupAsync(
            model.Id,
            model.RejectReason,
            User.Identity?.Name);

        if (result)
        {
            return Json(new { success = true, message = "Topup rejected successfully" });
        }

        return Json(new { success = false, message = "Failed to reject topup" });
    }

    #region AJAX Endpoints

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

        var (topups, totalFiltered, totalRecords) = await _topupService.GetTopupRequestsPagedAsync(
            page,
            pageSize,
            search,
            status,
            startDt,
            endDt,
            orderColumn,
            orderDirection);

        var topupData = topups.Select(t => new TopupListViewModel.TopupDataRow
        {
            Id = t.Id,
            CreatedAt = t.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            Username = t.User?.UserName ?? "Unknown",
            Amount = t.Amount,
            BankName = t.BankAccount?.BankName ?? "-",
            TransferProof = t.TransferProofUrl,
            Status = t.Status.ToString()
        }).ToList();

        return Json(new
        {
            draw = draw,
            recordsTotal = totalRecords,
            recordsFiltered = totalFiltered,
            data = topupData
        });
    }

    #endregion
}
