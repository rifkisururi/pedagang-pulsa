using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class ProductGroupController : Controller
{
    private readonly IAppDbContext _context;
    private readonly ILogger<ProductGroupController> _logger;

    public ProductGroupController(IAppDbContext context, ILogger<ProductGroupController> logger)
    {
        _context = context;
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
        [FromForm] string? search = null,
        [FromForm] int? categoryId = null,
        [FromForm] string? isActive = null)
    {
        var page = (start / length) + 1;
        var pageSize = length;

        bool? activeFilter = isActive switch
        {
            "true" => true,
            "false" => false,
            _ => null
        };

        var query = _context.ProductGroups
            .Include(g => g.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(g =>
                g.Name.ToLower().Contains(s) ||
                (g.Operator != null && g.Operator.ToLower().Contains(s)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(g => g.CategoryId == categoryId.Value);
        }

        if (activeFilter.HasValue)
        {
            query = query.Where(g => g.IsActive == activeFilter.Value);
        }

        var totalRecords = await _context.ProductGroups.CountAsync();
        var totalFiltered = await query.CountAsync();

        var groups = await query
            .OrderBy(g => g.CategoryId)
            .ThenBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = groups.Select(g => new
        {
            g.Id,
            g.Name,
            g.Operator,
            CategoryName = g.Category?.Name ?? "-",
            g.SortOrder,
            ProductCount = _context.Products.Count(p => p.ProductGroupId == g.Id),
            g.IsActive
        });

        return Json(new { draw, recordsTotal = totalRecords, recordsFiltered = totalFiltered, data });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create()
    {
        var model = new ProductGroupViewModel();
        await PopulateCategoriesAsync(model);
        return PartialView("_FormModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create(ProductGroupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Data tidak valid.", errors });
        }

        var group = new ProductGroup
        {
            Name = model.Name,
            Operator = model.Operator,
            CategoryId = model.CategoryId,
            SortOrder = model.SortOrder,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ProductGroups.Add(group);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Product group berhasil ditambahkan." });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        var group = await _context.ProductGroups.FindAsync(id);
        if (group == null) return NotFound();

        var model = new ProductGroupViewModel
        {
            Id = group.Id,
            Name = group.Name,
            Operator = group.Operator,
            CategoryId = group.CategoryId,
            SortOrder = group.SortOrder,
            IsActive = group.IsActive
        };

        await PopulateCategoriesAsync(model);
        return PartialView("_FormModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Edit(ProductGroupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Data tidak valid.", errors });
        }

        var group = await _context.ProductGroups.FindAsync(model.Id);
        if (group == null)
        {
            return Json(new { success = false, message = "Product group tidak ditemukan." });
        }

        group.Name = model.Name;
        group.Operator = model.Operator;
        group.CategoryId = model.CategoryId;
        group.SortOrder = model.SortOrder;
        group.IsActive = model.IsActive;
        group.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Product group berhasil diperbarui." });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var group = await _context.ProductGroups.FindAsync(id);
        if (group == null) return NotFound();

        var model = new ProductGroupViewModel
        {
            Id = group.Id,
            Name = group.Name,
            CategoryId = group.CategoryId
        };

        return PartialView("_DeleteModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(ProductGroupViewModel model)
    {
        var group = await _context.ProductGroups.FindAsync(model.Id);
        if (group == null)
        {
            return Json(new { success = false, message = "Product group tidak ditemukan." });
        }

        var productCount = await _context.Products.CountAsync(p => p.ProductGroupId == group.Id);
        if (productCount > 0)
        {
            return Json(new { success = false, message = $"Group ini masih memiliki {productCount} produk. Hapus atau pindahkan produk terlebih dahulu." });
        }

        _context.ProductGroups.Remove(group);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Product group berhasil dihapus." });
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.ProductCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
        return Json(categories.Select(c => new { id = c.Id, name = c.Name }));
    }

    private async Task PopulateCategoriesAsync(ProductGroupViewModel model)
    {
        var categories = await _context.ProductCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        model.AvailableCategories = categories.Select(c => new ProductGroupViewModel.CategoryItem
        {
            Id = c.Id,
            Name = c.Name
        }).ToList();
    }
}
