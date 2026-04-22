using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Configuration;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Controllers;

[Authorize]
public class ProductController : Controller
{
    private readonly ProductService _productService;
    private readonly IAppDbContext _context;
    private readonly ILogger<ProductController> _logger;
    private readonly IProductCacheService _productCache;
    private readonly PricingConfig _pricingConfig;

    public ProductController(ProductService productService, IAppDbContext context, ILogger<ProductController> logger, IProductCacheService productCache, IOptions<PricingConfig> pricingConfig)
    {
        _productService = productService;
        _context = context;
        _logger = logger;
        _productCache = productCache;
        _pricingConfig = pricingConfig.Value;
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

        var costPrice = await _context.SupplierProducts
            .Where(sp => sp.ProductId == id && sp.IsActive)
            .OrderBy(sp => sp.Seq)
            .Select(sp => sp.CostPrice)
            .FirstOrDefaultAsync();

        var model = new ProductDetailViewModel
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            Description = product.Description,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? "Unknown",
            ProductGroupId = product.ProductGroupId,
            ProductGroupName = product.ProductGroup?.Name,
            Operator = product.Operator,
            Denomination = product.Denomination,
            ValidityDays = product.ValidityDays,
            ValidityText = product.ValidityText,
            QuotaMb = product.QuotaMb,
            QuotaText = product.QuotaText,
            IsActive = product.IsActive,
            LevelPrices = product.ProductLevelPrices.Select(lp => new ProductDetailViewModel.PriceItem
            {
                LevelId = lp.LevelId,
                LevelName = lp.Level?.Name ?? "Unknown",
                Margin = lp.Margin,
                CostPrice = costPrice,
                ComputedSellPrice = costPrice > 0 ? costPrice + lp.Margin : 0,
                MarginPercent = costPrice > 0 ? lp.Margin / costPrice * 100 : 0
            }).ToList(),
            SupplierProducts = product.SupplierProducts
                .OrderBy(sp => sp.Seq)
                .Select(sp => new ProductDetailViewModel.SupplierProductItem
                {
                    Id = sp.Id,
                    SupplierName = sp.Supplier?.Name ?? "Unknown",
                    SupplierProductCode = sp.SupplierProductCode,
                    SupplierProductName = sp.SupplierProductName,
                    CostPrice = sp.CostPrice,
                    Seq = sp.Seq,
                    IsActive = sp.IsActive
                }).ToList()
        };

        foreach (var level in levels)
        {
            if (!model.LevelPrices.Any(lp => lp.LevelId == level.Id))
            {
                model.LevelPrices.Add(new ProductDetailViewModel.PriceItem
                {
                    LevelId = level.Id,
                    LevelName = level.Name,
                    Margin = _pricingConfig.GetDefaultMargin(costPrice),
                    CostPrice = costPrice,
                    ComputedSellPrice = costPrice > 0 ? costPrice + _pricingConfig.GetDefaultMargin(costPrice) : 0,
                    MarginPercent = costPrice > 0 ? _pricingConfig.GetDefaultMargin(costPrice) / costPrice * 100 : 0
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
        var groups = await _productService.GetProductGroupsAsync();
        var levels = await _productService.GetLevelsAsync();

        var model = new ProductViewModel
        {
            AvailableCategories = categories.Select(c => new ProductViewModel.CategoryItem
            {
                Id = c.Id,
                Name = c.Name
            }).ToList(),
            AvailableGroups = groups.Select(g => new ProductViewModel.GroupItem
            {
                Id = g.Id,
                CategoryId = g.CategoryId,
                Name = g.Name,
                Operator = g.Operator
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
            await PopulateDropdowns(model);
            return View(model);
        }

        var product = new Product
        {
            Code = model.Code,
            Name = model.Name,
            CategoryId = model.CategoryId,
            ProductGroupId = model.ProductGroupId,
            Operator = model.Operator,
            Description = model.Description,
            Denomination = model.Denomination,
            ValidityDays = model.ValidityDays,
            ValidityText = model.ValidityText,
            QuotaMb = model.QuotaMb,
            QuotaText = model.QuotaText,
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
                    Margin = lp.Margin,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        var result = await _productService.CreateProductAsync(product, levelPrices);
        if (result == null)
        {
            ModelState.AddModelError("", "Failed to create product.");
            await PopulateDropdowns(model);
            return View(model);
        }

        TempData["Success"] = "Product created successfully.";
        await _productCache.InvalidateProductCacheAsync();
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
        var groups = await _productService.GetProductGroupsAsync();
        var levels = await _productService.GetLevelsAsync();

        var model = new ProductViewModel
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            CategoryId = product.CategoryId,
            ProductGroupId = product.ProductGroupId,
            Denomination = product.Denomination,
            ValidityDays = product.ValidityDays,
            ValidityText = product.ValidityText,
            QuotaMb = product.QuotaMb,
            QuotaText = product.QuotaText,
            Operator = product.Operator,
            Description = product.Description,
            IsActive = product.IsActive,
            AvailableCategories = categories.Select(c => new ProductViewModel.CategoryItem
            {
                Id = c.Id,
                Name = c.Name
            }).ToList(),
            AvailableGroups = groups.Select(g => new ProductViewModel.GroupItem
            {
                Id = g.Id,
                CategoryId = g.CategoryId,
                Name = g.Name,
                Operator = g.Operator
            }).ToList(),
            AvailableLevels = levels.Select(l => new ProductViewModel.LevelItem
            {
                Id = l.Id,
                Name = l.Name
            }).ToList(),
            LevelPrices = levels.Select(l => new ProductViewModel.LevelPriceItem
            {
                LevelId = l.Id,
                Margin = product.ProductLevelPrices.FirstOrDefault(lp => lp.LevelId == l.Id)?.Margin ?? _pricingConfig.GetDefaultMargin(0)
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
            await PopulateDropdowns(model);
            return View(model);
        }

        var product = new Product
        {
            Id = model.Id!.Value,
            Code = model.Code,
            Name = model.Name,
            CategoryId = model.CategoryId,
            ProductGroupId = model.ProductGroupId,
            Denomination = model.Denomination,
            ValidityDays = model.ValidityDays,
            ValidityText = model.ValidityText,
            QuotaMb = model.QuotaMb,
            QuotaText = model.QuotaText,
            Operator = model.Operator,
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
                    Margin = lp.Margin
                });
            }
        }

        var result = await _productService.UpdateProductAsync(product, levelPrices);
        if (result == null)
        {
            ModelState.AddModelError("", "Failed to update product. Product not found.");
            await PopulateDropdowns(model);
            return View(model);
        }

        TempData["Success"] = "Product updated successfully.";
        await _productCache.InvalidateProductCacheAsync();
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
        await _productCache.InvalidateProductCacheAsync();
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
        [FromForm] int? groupId = null,
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
            groupId,
            activeFilter,
            orderColumn,
            orderDirection);

        var productData = products.Select(p => new ProductListViewModel.ProductDataRow
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Operator = p.Operator,
            Category = p.Category?.Name ?? "Unknown",
            ProductGroup = p.ProductGroup?.Name,
            Denomination = p.Denomination,
            ValidityDays = p.ValidityDays,
            ValidityText = p.ValidityText,
            QuotaMb = p.QuotaMb,
            QuotaText = p.QuotaText,
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

    [HttpGet]
    public async Task<IActionResult> GetGroups(int? categoryId = null)
    {
        var groups = await _productService.GetProductGroupsAsync(categoryId);
        var result = groups.Select(g => new
        {
            id = g.Id,
            categoryId = g.CategoryId,
            name = g.Name,
            @operator = g.Operator
        });
        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetProductsSelect()
    {
        var products = await _productService.GetActiveProductsAsync();
        var selectList = products.Select(p => new
        {
            value = p.Id.ToString(),
            text = p.Code + " - " + p.Name
        }).ToList();

        return Json(selectList);
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
            model.Margin,
            User.Identity?.Name);

        if (result)
        {
            await _productCache.InvalidateProductCacheAsync();
            return Json(new { success = true, message = "Price updated successfully" });
        }

        return Json(new { success = false, message = "Failed to update price" });
    }

    #endregion

    private async Task PopulateDropdowns(ProductViewModel model)
    {
        var categories = await _productService.GetCategoriesAsync();
        var groups = await _productService.GetProductGroupsAsync();
        var levels = await _productService.GetLevelsAsync();

        model.AvailableCategories = categories.Select(c => new ProductViewModel.CategoryItem
        {
            Id = c.Id,
            Name = c.Name
        }).ToList();
        model.AvailableGroups = groups.Select(g => new ProductViewModel.GroupItem
        {
            Id = g.Id,
            CategoryId = g.CategoryId,
            Name = g.Name,
            Operator = g.Operator
        }).ToList();
        model.AvailableLevels = levels.Select(l => new ProductViewModel.LevelItem
        {
            Id = l.Id,
            Name = l.Name
        }).ToList();
    }
}
