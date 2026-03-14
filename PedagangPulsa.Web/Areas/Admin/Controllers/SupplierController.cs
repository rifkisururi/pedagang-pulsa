using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class SupplierController : Controller
{
    private readonly SupplierService _supplierService;
    private readonly ILogger<SupplierController> _logger;

    public SupplierController(SupplierService supplierService, ILogger<SupplierController> logger)
    {
        _supplierService = supplierService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var supplier = await _supplierService.GetSupplierByIdAsync(id);
        if (supplier == null)
        {
            return NotFound();
        }

        var model = new SupplierDetailViewModel
        {
            Id = supplier.Id,
            Name = supplier.Name,
            ApiUrl = supplier.ApiBaseUrl,
            ApiKey = supplier.ApiKeyEnc,
            ApiSecret = supplier.CallbackSecret,
            TimeoutSeconds = supplier.TimeoutSeconds,
            Balance = supplier.Balance?.ActiveBalance ?? 0,
            IsActive = supplier.IsActive,
            CreatedAt = supplier.CreatedAt,
            UpdatedAt = supplier.UpdatedAt
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupplierViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var supplier = new Supplier
        {
            Name = model.Name,
            ApiBaseUrl = model.ApiUrl,
            ApiKeyEnc = model.ApiKey,
            CallbackSecret = model.ApiSecret,
            TimeoutSeconds = (short)model.TimeoutSeconds,
            IsActive = model.IsActive
        };

        var result = await _supplierService.CreateSupplierAsync(supplier);
        if (result == null)
        {
            ModelState.AddModelError("", "Failed to create supplier. Supplier with this name may already exist.");
            return View(model);
        }

        TempData["Success"] = "Supplier created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        var supplier = await _supplierService.GetSupplierByIdAsync(id);
        if (supplier == null)
        {
            return NotFound();
        }

        var model = new SupplierViewModel
        {
            Id = supplier.Id,
            Name = supplier.Name,
            ApiUrl = supplier.ApiBaseUrl,
            ApiKey = supplier.ApiKeyEnc,
            ApiSecret = supplier.CallbackSecret,
            TimeoutSeconds = supplier.TimeoutSeconds,
            InitialBalance = supplier.Balance?.ActiveBalance ?? 0,
            IsActive = supplier.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SupplierViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var supplier = new Supplier
        {
            Id = model.Id.Value,
            Name = model.Name,
            ApiBaseUrl = model.ApiUrl,
            ApiKeyEnc = model.ApiKey,
            CallbackSecret = model.ApiSecret,
            TimeoutSeconds = (short)model.TimeoutSeconds,
            IsActive = model.IsActive
        };

        var result = await _supplierService.UpdateSupplierAsync(supplier);
        if (result == null)
        {
            ModelState.AddModelError("", "Failed to update supplier. Supplier with this name may already exist.");
            return View(model);
        }

        TempData["Success"] = "Supplier updated successfully.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var supplier = await _supplierService.GetSupplierByIdAsync(id);
        if (supplier == null)
        {
            return NotFound();
        }

        var model = new SupplierDeleteViewModel
        {
            Id = supplier.Id,
            Name = supplier.Name
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(SupplierDeleteViewModel model)
    {
        var result = await _supplierService.DeleteSupplierAsync(model.Id);
        if (!result)
        {
            ModelState.AddModelError("", "Failed to delete supplier. Supplier may be in use by products.");
            return View(model);
        }

        TempData["Success"] = "Supplier deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    #region AJAX Endpoints

    [HttpPost]
    public async Task<IActionResult> GetData(
        [FromForm] int draw,
        [FromForm] int start,
        [FromForm] int length,
        [FromForm] string? search = null,
        [FromForm] string? isActive = null)
    {
        var page = (start / length) + 1;
        var pageSize = length;

        bool? activeFilter = null;
        if (!string.IsNullOrWhiteSpace(isActive) && bool.TryParse(isActive, out var activeBool))
        {
            activeFilter = activeBool;
        }

        var (suppliers, totalFiltered, totalRecords) = await _supplierService.GetSuppliersPagedAsync(
            page,
            pageSize,
            search,
            activeFilter);

        var supplierData = suppliers.Select(s => new SupplierListViewModel.SupplierDataRow
        {
            Id = s.Id,
            Name = s.Name,
            ApiUrl = s.ApiBaseUrl,
            TimeoutSeconds = s.TimeoutSeconds,
            Balance = s.Balance?.ActiveBalance ?? 0,
            IsActive = s.IsActive ? "Active" : "Inactive"
        }).ToList();

        return Json(new
        {
            draw = draw,
            recordsTotal = totalRecords,
            recordsFiltered = totalFiltered,
            data = supplierData
        });
    }

    #endregion
}
