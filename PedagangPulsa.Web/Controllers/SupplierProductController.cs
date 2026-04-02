using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class SupplierProductController : Controller
{
    private readonly SupplierProductService _supplierProductService;
    private readonly ProductService _productService;
    private readonly SupplierService _supplierService;
    private readonly AppDbContext _context;
    private readonly ILogger<SupplierProductController> _logger;

    public SupplierProductController(
        SupplierProductService supplierProductService,
        ProductService productService,
        SupplierService supplierService,
        AppDbContext context,
        ILogger<SupplierProductController> logger)
    {
        _supplierProductService = supplierProductService;
        _productService = productService;
        _supplierService = supplierService;
        _context = context;
        _logger = logger;
    }

    // Index page for all supplier products (menu navigation)
    [HttpGet]
    public IActionResult Index(Guid? productId)
    {
        ViewBag.SelectedProductId = productId;
        return View();
    }

    // Index page for specific product (from Product Details)
    [HttpGet]
    public async Task<IActionResult> ByProduct(Guid productId)
    {
        return RedirectToAction(nameof(Index), new { productId = productId });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Add(Guid productId)
    {
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null)
        {
            return NotFound();
        }

        var availableSuppliers = await _supplierProductService.GetAvailableSuppliersForProductAsync(productId);
        var allSuppliers = await _supplierService.GetAllActiveSuppliersAsync();

        var model = new SupplierProductViewModel
        {
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductName = product.Name,
            AvailableSuppliers = allSuppliers.Select(s => new SupplierProductViewModel.SupplierItem
            {
                Id = s.Id,
                Name = s.Name
            }).ToList()
        };

        // Get next sequence number
        var existingCount = (await _supplierProductService.GetSupplierProductsByProductIdAsync(productId)).Count;
        model.Seq = (short)(existingCount + 1);

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(SupplierProductViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var product = await _productService.GetProductByIdAsync(model.ProductId);
            var allSuppliers = await _supplierService.GetAllActiveSuppliersAsync();

            model.ProductCode = product?.Code ?? "";
            model.ProductName = product?.Name ?? "";
            model.AvailableSuppliers = allSuppliers.Select(s => new SupplierProductViewModel.SupplierItem
            {
                Id = s.Id,
                Name = s.Name
            }).ToList();

            return View(model);
        }

        var supplierProduct = new SupplierProduct
        {
            ProductId = model.ProductId,
            SupplierId = model.SupplierId,
            CostPrice = model.CostPrice,
            SupplierProductCode = model.SupplierProductCode,
            SupplierProductName = model.SupplierProductName,
            Seq = model.Seq,
            IsActive = model.IsActive
        };

        var result = await _supplierProductService.AddSupplierProductAsync(supplierProduct);
        if (result == null)
        {
            ModelState.AddModelError("", "Failed to add supplier. This supplier may already be mapped to this product.");
            return View(model);
        }

        TempData["Success"] = "Supplier added successfully.";
        return RedirectToAction(nameof(Index), new { productId = model.ProductId });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Edit(Guid productId, int supplierId)
    {
        var supplierProduct = await _supplierProductService.GetSupplierProductAsync(productId, supplierId);
        if (supplierProduct == null)
        {
            return NotFound();
        }

        var model = new SupplierProductViewModel
        {
            ProductId = supplierProduct.ProductId,
            SupplierId = supplierProduct.SupplierId,
            CostPrice = supplierProduct.CostPrice,
            SupplierProductCode = supplierProduct.SupplierProductCode,
            SupplierProductName = supplierProduct.SupplierProductName,
            Seq = supplierProduct.Seq,
            IsActive = supplierProduct.IsActive,
            ProductCode = supplierProduct.Product?.Code ?? "",
            ProductName = supplierProduct.Product?.Name ?? "",
            SupplierName = supplierProduct.Supplier?.Name ?? ""
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SupplierProductViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var supplierProduct = new SupplierProduct
        {
            ProductId = model.ProductId,
            SupplierId = model.SupplierId,
            CostPrice = model.CostPrice,
            SupplierProductCode = model.SupplierProductCode,
            SupplierProductName = model.SupplierProductName,
            Seq = model.Seq,
            IsActive = model.IsActive
        };

        var result = await _supplierProductService.UpdateSupplierProductAsync(supplierProduct);
        if (result == null)
        {
            ModelState.AddModelError("", "Failed to update supplier mapping.");
            return View(model);
        }

        TempData["Success"] = "Supplier mapping updated successfully.";
        return RedirectToAction(nameof(Index), new { productId = model.ProductId });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Delete(Guid productId, int supplierId)
    {
        var supplierProduct = await _supplierProductService.GetSupplierProductAsync(productId, supplierId);
        if (supplierProduct == null)
        {
            return NotFound();
        }

        var model = new SupplierProductDeleteViewModel
        {
            ProductId = productId,
            SupplierId = supplierId,
            ProductName = supplierProduct.Product?.Name ?? "",
            SupplierName = supplierProduct.Supplier?.Name ?? ""
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(SupplierProductDeleteViewModel model)
    {
        var result = await _supplierProductService.DeleteSupplierProductAsync(model.ProductId, model.SupplierId);
        if (!result)
        {
            ModelState.AddModelError("", "Failed to delete supplier mapping.");
            return View(model);
        }

        TempData["Success"] = "Supplier mapping deleted successfully.";
        return RedirectToAction(nameof(Index), new { productId = model.ProductId });
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Reorder([FromBody] SupplierProductReorderViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Invalid data" });
        }

        var reorderList = model.Suppliers.Select(s => new SupplierProductReorder
        {
            SupplierId = s.SupplierId,
            NewSequence = s.Seq
        }).ToList();

        var result = await _supplierProductService.ReorderSupplierProductsAsync(model.ProductId, reorderList);

        if (result)
        {
            return Json(new { success = true, message = "Order updated successfully" });
        }

        return Json(new { success = false, message = "Failed to update order" });
    }

    #region AJAX Endpoints

    [HttpGet]
    public async Task<IActionResult> GetMapping(Guid productId, int supplierId)
    {
        var sp = await _supplierProductService.GetSupplierProductAsync(productId, supplierId);
        if (sp == null) return NotFound();

        return Json(new
        {
            productId = sp.ProductId,
            supplierId = sp.SupplierId,
            costPrice = sp.CostPrice,
            supplierProductCode = sp.SupplierProductCode,
            supplierProductName = sp.SupplierProductName,
            seq = sp.Seq,
            isActive = sp.IsActive
        });
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromForm] SupplierProductViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Invalid data", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        var supplierProduct = new SupplierProduct
        {
            ProductId = model.ProductId,
            SupplierId = model.SupplierId,
            CostPrice = model.CostPrice,
            SupplierProductCode = model.SupplierProductCode,
            SupplierProductName = model.SupplierProductName,
            Seq = model.Seq,
            IsActive = model.IsActive
        };

        // Check if it's an update or new
        var existing = await _supplierProductService.GetSupplierProductAsync(model.ProductId, model.SupplierId);
        
        SupplierProduct? result;
        if (existing != null)
        {
            result = await _supplierProductService.UpdateSupplierProductAsync(supplierProduct);
        }
        else
        {
            result = await _supplierProductService.AddSupplierProductAsync(supplierProduct);
        }

        if (result == null)
        {
            return Json(new { success = false, message = "Failed to save mapping. It might already exist or data is invalid." });
        }

        return Json(new { success = true, message = "Mapping saved successfully." });
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAjax(Guid productId, int supplierId)
    {
        var result = await _supplierProductService.DeleteSupplierProductAsync(productId, supplierId);
        if (result)
        {
            return Json(new { success = true, message = "Mapping deleted successfully." });
        }
        return Json(new { success = false, message = "Failed to delete mapping." });
    }

    [HttpPost]
    public async Task<IActionResult> GetData(
        [FromForm] int draw,
        [FromForm] int start = 1,
        [FromForm] int length = 10,
        [FromForm] string? search = null,
        [FromForm] string? productId = null,
        [FromForm] string? supplierId = null,
        [FromForm] string? isActive = null)
    {
        var page = (start / length) + 1;
        var pageSize = length;

        var query = _context.SupplierProducts
            .Include(sp => sp.Supplier)
            .Include(sp => sp.Product)
            .ThenInclude(p => p.Category)
            .AsQueryable();

        // Apply product filter
        if (!string.IsNullOrWhiteSpace(productId) && Guid.TryParse(productId, out var prodId))
        {
            query = query.Where(sp => sp.ProductId == prodId);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

            if (isInMemory)
            {
                var searchLower = search.ToLower();
                query = query.Where(sp =>
                    sp.Product!.Name.ToLower().Contains(searchLower) ||
                    sp.Product.Code.ToLower().Contains(searchLower) ||
                    sp.Supplier!.Name.ToLower().Contains(searchLower) ||
                    (sp.SupplierProductCode != null && sp.SupplierProductCode.ToLower().Contains(searchLower)));
            }
            else
            {
                query = query.Where(sp =>
                    EF.Functions.ILike(sp.Product!.Name, $"%{search}%") ||
                    EF.Functions.ILike(sp.Product.Code, $"%{search}%") ||
                    EF.Functions.ILike(sp.Supplier!.Name, $"%{search}%") ||
                    EF.Functions.ILike(sp.SupplierProductCode ?? "", $"%{search}%"));
            }
        }

        // Apply supplier filter
        if (!string.IsNullOrWhiteSpace(supplierId) && int.TryParse(supplierId, out var suppId))
        {
            query = query.Where(sp => sp.SupplierId == suppId);
        }

        // Apply active filter
        if (!string.IsNullOrWhiteSpace(isActive) && bool.TryParse(isActive, out var activeBool))
        {
            query = query.Where(sp => sp.IsActive == activeBool);
        }

        var totalRecords = await _context.SupplierProducts.CountAsync();
        var totalFiltered = await query.CountAsync();

        var supplierProducts = await query
            .OrderBy(sp => sp.Product.Name)
            .ThenBy(sp => sp.Seq)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = supplierProducts.Select(sp => new
        {
            id = sp.ProductId,
            supplierId = sp.SupplierId,
            productName = sp.Product?.Name ?? "Unknown",
            productCode = sp.Product?.Code ?? "Unknown",
            productCategory = sp.Product?.Category?.Name ?? "Unknown",
            supplierName = sp.Supplier?.Name ?? "Unknown",
            supplierProductCode = sp.SupplierProductCode,
            supplierProductName = sp.SupplierProductName,
            costPrice = sp.CostPrice,
            seq = sp.Seq,
            isActive = sp.IsActive ? "Active" : "Inactive"
        }).ToList();

        return Json(new
        {
            draw = draw,
            recordsTotal = totalRecords,
            recordsFiltered = totalFiltered,
            data = data
        });
    }

    #endregion
}
