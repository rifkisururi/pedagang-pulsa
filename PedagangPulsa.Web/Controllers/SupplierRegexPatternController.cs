using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class SupplierRegexPatternController : Controller
{
    private readonly SupplierRegexPatternService _service;
    private readonly SupplierService _supplierService;
    private readonly ILogger<SupplierRegexPatternController> _logger;

    public SupplierRegexPatternController(
        SupplierRegexPatternService service,
        SupplierService supplierService,
        ILogger<SupplierRegexPatternController> logger)
    {
        _service = service;
        _supplierService = supplierService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> GetData(
        [FromForm] int draw,
        [FromForm] int start,
        [FromForm] int length,
        [FromForm] int? supplierId = null,
        [FromForm] string? search = null,
        [FromForm] string? isTrxSukses = null)
    {
        var page = (start / length) + 1;
        var pageSize = length;

        bool? trxSuksesFilter = isTrxSukses switch
        {
            "true" => true,
            "false" => false,
            _ => null
        };

        var (patterns, totalFiltered, totalRecords) = await _service.GetPagedAsync(
            page, pageSize, supplierId, search, trxSuksesFilter);

        var data = patterns.Select(p => new
        {
            p.Id,
            SupplierName = p.Supplier.Name,
            p.SeqNo,
            p.IsTrxSukses,
            p.Label,
            RegexTruncated = p.Regex.Length > 80 ? p.Regex[..80] + "..." : p.Regex,
            SampleTruncated = p.SampleMessage != null && p.SampleMessage.Length > 60
                ? p.SampleMessage[..60] + "..."
                : p.SampleMessage,
            p.IsActive
        });

        return Json(new
        {
            draw,
            recordsTotal = totalRecords,
            recordsFiltered = totalFiltered,
            data
        });
    }

    public async Task<IActionResult> Create(int? supplierId)
    {
        var model = new SupplierRegexPatternViewModel();
        if (supplierId.HasValue)
        {
            model.SupplierId = supplierId.Value;
            var supplier = await _supplierService.GetSupplierByIdAsync(supplierId.Value);
            model.SupplierName = supplier?.Name;
        }
        await PopulateSuppliersAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create(SupplierRegexPatternViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSuppliersAsync();
            return View(model);
        }

        var pattern = new SupplierRegexPattern
        {
            SupplierId = model.SupplierId,
            SeqNo = model.SeqNo,
            IsTrxSukses = model.IsTrxSukses,
            Label = model.Label,
            Regex = model.Regex,
            SampleMessage = model.SampleMessage,
            IsActive = model.IsActive
        };

        var result = await _service.CreateAsync(pattern);
        if (result == null)
        {
            ModelState.AddModelError("SeqNo", "SeqNo sudah digunakan untuk supplier ini.");
            await PopulateSuppliersAsync();
            return View(model);
        }

        TempData["Success"] = "Regex pattern berhasil ditambahkan.";
        return RedirectToAction(nameof(Index), new { supplierId = model.SupplierId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var pattern = await _service.GetByIdAsync(id);
        if (pattern == null) return NotFound();

        var model = new SupplierRegexPatternViewModel
        {
            Id = pattern.Id,
            SupplierId = pattern.SupplierId,
            SupplierName = pattern.Supplier.Name,
            SeqNo = pattern.SeqNo,
            IsTrxSukses = pattern.IsTrxSukses,
            Label = pattern.Label,
            Regex = pattern.Regex,
            SampleMessage = pattern.SampleMessage,
            IsActive = pattern.IsActive
        };

        await PopulateSuppliersAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Edit(SupplierRegexPatternViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSuppliersAsync();
            return View(model);
        }

        var pattern = new SupplierRegexPattern
        {
            Id = model.Id!.Value,
            SupplierId = model.SupplierId,
            SeqNo = model.SeqNo,
            IsTrxSukses = model.IsTrxSukses,
            Label = model.Label,
            Regex = model.Regex,
            SampleMessage = model.SampleMessage,
            IsActive = model.IsActive
        };

        var result = await _service.UpdateAsync(pattern);
        if (result == null)
        {
            ModelState.AddModelError("SeqNo", "SeqNo sudah digunakan untuk supplier ini.");
            await PopulateSuppliersAsync();
            return View(model);
        }

        TempData["Success"] = "Regex pattern berhasil diperbarui.";
        return RedirectToAction(nameof(Index), new { supplierId = model.SupplierId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var pattern = await _service.GetByIdAsync(id);
        if (pattern == null) return NotFound();

        await _service.DeleteAsync(id);
        TempData["Success"] = "Regex pattern berhasil dihapus.";
        return RedirectToAction(nameof(Index), new { supplierId = pattern.SupplierId });
    }

    [HttpPost]
    public IActionResult TestRegex([FromBody] TestRegexRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Regex) || string.IsNullOrWhiteSpace(request.TestMessage))
        {
            return Json(new { success = false, message = "Regex dan Test Message wajib diisi." });
        }

        var result = _service.TestRegex(request.Regex, request.TestMessage);
        return Json(new
        {
            success = result.IsMatch,
            message = result.Message,
            groups = result.Groups.Select(g => new { name = g.Name, value = g.Value })
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetSuppliersSelect()
    {
        var suppliers = await _supplierService.GetAllActiveSuppliersAsync();
        var selectList = suppliers.Select(s => new { value = s.Id.ToString(), text = s.Name }).ToList();
        return Json(selectList);
    }

    private async Task PopulateSuppliersAsync()
    {
        var suppliers = await _supplierService.GetAllActiveSuppliersAsync();
        ViewData["Suppliers"] = suppliers
            .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(s.Name, s.Id.ToString()))
            .ToList();
    }
}

public class TestRegexRequest
{
    public string Regex { get; set; } = string.Empty;
    public string TestMessage { get; set; } = string.Empty;
}
