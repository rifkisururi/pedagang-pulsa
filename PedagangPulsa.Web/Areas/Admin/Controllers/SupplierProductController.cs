using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class SupplierProductController : Controller
{
    private readonly SupplierProductService _supplierProductService;
    private readonly ProductService _productService;
    private readonly SupplierService _supplierService;
    private readonly ILogger<SupplierProductController> _logger;

    public SupplierProductController(
        SupplierProductService supplierProductService,
        ProductService productService,
        SupplierService supplierService,
        ILogger<SupplierProductController> logger)
    {
        _supplierProductService = supplierProductService;
        _productService = productService;
        _supplierService = supplierService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid productId)
    {
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null)
        {
            return NotFound();
        }

        var supplierProducts = await _supplierProductService.GetSupplierProductsByProductIdAsync(productId);
        var availableSuppliers = await _supplierProductService.GetAvailableSuppliersForProductAsync(productId);

        var model = new SupplierProductListViewModel
        {
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductName = product.Name,
            ProductCategory = product.Category?.Name ?? "Unknown",
            SupplierProducts = supplierProducts.Select(sp => new SupplierProductListViewModel.SupplierProductItem
            {
                SupplierId = sp.SupplierId,
                SupplierName = sp.Supplier?.Name ?? "Unknown",
                CostPrice = sp.CostPrice,
                SupplierSku = sp.SupplierProductCode,
                Seq = sp.Seq,
                IsActive = sp.IsActive
            }).ToList(),
            AvailableSuppliers = availableSuppliers.Select(s => new SupplierProductListViewModel.AvailableSupplierItem
            {
                Id = s.Id,
                Name = s.Name
            }).ToList()
        };

        return View(model);
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
            SupplierProductCode = model.SupplierSku,
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
            SupplierSku = supplierProduct.SupplierProductCode,
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
            SupplierProductCode = model.SupplierSku,
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
}
