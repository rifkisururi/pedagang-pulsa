using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Finance")]
public class SupplierBalanceController : Controller
{
    private readonly SupplierBalanceService _supplierBalanceService;
    private readonly ILogger<SupplierBalanceController> _logger;

    public SupplierBalanceController(SupplierBalanceService supplierBalanceService, ILogger<SupplierBalanceController> logger)
    {
        _supplierBalanceService = supplierBalanceService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var suppliers = await _supplierBalanceService.GetAllSuppliersWithBalanceAsync();
        ViewBag.Suppliers = suppliers;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Deposit(int supplierId)
    {
        var supplier = await _supplierBalanceService.GetSupplierWithBalanceAsync(supplierId);
        if (supplier == null)
        {
            return NotFound();
        }

        var model = new DepositSupplierViewModel
        {
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            SupplierCode = supplier.Code,
            CurrentBalance = supplier.Balance?.ActiveBalance ?? 0
        };

        return PartialView("_DepositModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deposit(DepositSupplierViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Invalid data" });
        }

        if (model.Amount <= 0)
        {
            return Json(new { success = false, message = "Amount must be greater than zero" });
        }

        var result = await _supplierBalanceService.DepositToSupplierAsync(
            model.SupplierId,
            model.Amount,
            $"Deposit to {model.SupplierName}",
            model.Notes,
            User.Identity?.Name);

        if (result)
        {
            return Json(new { success = true, message = $"Deposit of {model.Amount:C} to {model.SupplierName} successful" });
        }

        return Json(new { success = false, message = "Failed to process deposit" });
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

        var (ledgers, totalFiltered, totalRecords) = await _supplierBalanceService.GetSupplierLedgersPagedAsync(
            page,
            pageSize,
            search,
            type,
            startDt,
            endDt,
            orderColumn,
            orderDirection);

        var ledgerData = ledgers.Select(l => new SupplierLedgerDataRow
        {
            Id = l.Id,
            CreatedAt = l.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            SupplierName = l.Supplier?.Name ?? "Unknown",
            Type = l.Type,
            Amount = l.Amount,
            BalanceBefore = l.BalanceBefore,
            BalanceAfter = l.BalanceAfter,
            Description = l.Description ?? "-",
            PerformedBy = l.PerformedBy ?? "-",
            AdminNote = l.AdminNote
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
