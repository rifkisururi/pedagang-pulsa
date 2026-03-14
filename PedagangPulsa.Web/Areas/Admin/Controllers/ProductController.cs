using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class ProductController : Controller
{
    private readonly ProductService _productService;
    private readonly ILogger<ProductController> _logger;

    public ProductController(ProductService productService, ILogger<ProductController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        var levels = await _productService.GetLevelsAsync();

        var model = new ProductDetailViewModel
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            Description = product.Description,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? "Unknown",
            Denomination = product.Denomination,
            IsActive = product.IsActive,
            LevelPrices = product.ProductLevelPrices.Select(lp => new ProductDetailViewModel.PriceItem
            {
                LevelId = lp.LevelId,
                LevelName = lp.Level?.Name ?? "Unknown",
                SellPrice = lp.SellPrice,
                Margin = 0,
                MarginPercent = 0
            }).ToList()
        };

        // Fill in missing levels
        foreach (var level in levels)
        {
            if (!model.LevelPrices.Any(lp => lp.LevelId == level.Id))
            {
                model.LevelPrices.Add(new ProductDetailViewModel.PriceItem
                {
                    LevelId = level.Id,
                    LevelName = level.Name,
                    SellPrice = 0,
                    Margin = 0,
                    MarginPercent = 0
                });
            }
        }

        model.LevelPrices = model.LevelPrices.OrderBy(lp => lp.LevelId).ToList();

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Create()
    {
        var categories = await _productService.GetCategoriesAsync();
        var levels = await _productService.GetLevelsAsync();

        var model = new ProductViewModel
        {
            AvailableCategories = categories.Select(c => new ProductViewModel.CategoryItem
            {
                Id = c.Id,
                Name = c.Name
            }).ToList(),
            AvailableLevels = levels.Select(l => new ProductViewModel.LevelItem
            {
                Id = l.Id,
                Name = l.Name
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var categories = await _productService.GetCategoriesAsync();
            var levels = await _productService.GetLevelsAsync();
            model.AvailableCategories = categories.Select(c => new ProductViewModel.CategoryItem
            {
                Id = c.Id,
                Name = c.Name
            }).ToList();
            model.AvailableLevels = levels.Select(l => new ProductViewModel.LevelItem
            {
                Id = l.Id,
                Name = l.Name
            }).ToList();
            return View(model);
        }

        var product = new Product
        {
            Code = model.Code,
            Name = model.Name,
            CategoryId = model.CategoryId,
            Description = model.Description,
            Denomination = model.Denomination,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var levelPrices = new List<ProductLevelPrice>();
        if (model.LevelPrices != null)
        {
            foreach (var lp in model.LevelPrices)
            {
                levelPrices.Add(new ProductLevelPrice
                {
                    LevelId = lp.LevelId,
                    SellPrice = lp.SellPrice,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        var result = await _productService.CreateProductAsync(product, levelPrices);
        if (result == null)
        {
            ModelState.AddModelError("", "Failed to create product.");
            return View(model);
        }

        TempData["Success"] = "Product created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        var categories = await _productService.GetCategoriesAsync();
        var levels = await _productService.GetLevelsAsync();

        var model = new ProductViewModel
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            CategoryId = product.CategoryId,
            Denomination = product.Denomination,
            Description = product.Description,
            IsActive = product.IsActive,
            AvailableCategories = categories.Select(c => new ProductViewModel.CategoryItem
            {
                Id = c.Id,
                Name = c.Name
            }).ToList(),
            AvailableLevels = levels.Select(l => new ProductViewModel.LevelItem
            {
                Id = l.Id,
                Name = l.Name
            }).ToList(),
            LevelPrices = levels.Select(l => new ProductViewModel.LevelPriceItem
            {
                LevelId = l.Id,
                SellPrice = product.ProductLevelPrices.FirstOrDefault(lp => lp.LevelId == l.Id)?.SellPrice ?? 0
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var categories = await _productService.GetCategoriesAsync();
            var levels = await _productService.GetLevelsAsync();
            model.AvailableCategories = categories.Select(c => new ProductViewModel.CategoryItem
            {
                Id = c.Id,
                Name = c.Name
            }).ToList();
            model.AvailableLevels = levels.Select(l => new ProductViewModel.LevelItem
            {
                Id = l.Id,
                Name = l.Name
            }).ToList();
            return View(model);
        }

        var product = new Product
        {
            Id = model.Id.Value,
            Code = model.Code,
            Name = model.Name,
            CategoryId = model.CategoryId,
            Denomination = model.Denomination,
            Description = model.Description,
            IsActive = model.IsActive,
            UpdatedAt = DateTime.UtcNow
        };

        var levelPrices = new List<ProductLevelPrice>();
        if (model.LevelPrices != null)
        {
            foreach (var lp in model.LevelPrices)
            {
                levelPrices.Add(new ProductLevelPrice
                {
                    LevelId = lp.LevelId,
                    SellPrice = lp.SellPrice
                });
            }
        }

        var result = await _productService.UpdateProductAsync(product, levelPrices);
        if (result == null)
        {
            ModelState.AddModelError("", "Failed to update product. Product not found.");
            return View(model);
        }

        TempData["Success"] = "Product updated successfully.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        var model = new ProductDeleteViewModel
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            CategoryName = product.Category?.Name ?? "Unknown"
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(ProductDeleteViewModel model)
    {
        var result = await _productService.DeleteProductAsync(model.Id);
        if (!result)
        {
            ModelState.AddModelError("", "Failed to delete product. Product not found.");
            return View(model);
        }

        TempData["Success"] = "Product deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    #region AJAX Endpoints

    [HttpPost]
    public async Task<IActionResult> GetData(
        [FromForm] int draw,
        [FromForm] int start,
        [FromForm] int length,
        [FromForm] string? search = null,
        [FromForm] int? categoryId = null,
        [FromForm] string? isActive = null,
        [FromForm] string? orderColumn = null,
        [FromForm] string? orderDirection = null)
    {
        var page = (start / length) + 1;
        var pageSize = length;

        bool? activeFilter = null;
        if (!string.IsNullOrWhiteSpace(isActive) && bool.TryParse(isActive, out var activeBool))
        {
            activeFilter = activeBool;
        }

        var (products, totalFiltered, totalRecords) = await _productService.GetProductsPagedAsync(
            page,
            pageSize,
            search,
            categoryId,
            activeFilter,
            orderColumn,
            orderDirection);

        var productData = products.Select(p => new ProductListViewModel.ProductDataRow
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Category = p.Category?.Name ?? "Unknown",
            Denomination = p.Denomination,
            IsActive = p.IsActive ? "Active" : "Inactive"
        }).ToList();

        return Json(new
        {
            draw = draw,
            recordsTotal = totalRecords,
            recordsFiltered = totalFiltered,
            data = productData
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _productService.GetCategoriesAsync();
        var result = categories.Select(c => new
        {
            id = c.Id,
            name = c.Name
        });
        return Json(result);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> UpdatePrice([FromBody] UpdatePriceViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Invalid data" });
        }

        var result = await _productService.UpdateProductPriceAsync(
            model.ProductId,
            model.LevelId,
            model.SellPrice,
            User.Identity?.Name);

        if (result)
        {
            return Json(new { success = true, message = "Price updated successfully" });
        }

        return Json(new { success = false, message = "Failed to update price" });
    }

    #endregion
}
