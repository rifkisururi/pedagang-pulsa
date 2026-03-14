using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Application.Services;

public class SupplierProductService
{
    private readonly AppDbContext _context;

    public SupplierProductService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SupplierProduct>> GetSupplierProductsByProductIdAsync(Guid productId)
    {
        return await _context.SupplierProducts
            .Include(sp => sp.Supplier)
            .Where(sp => sp.ProductId == productId)
            .OrderBy(sp => sp.Seq)
            .ToListAsync();
    }

    public async Task<SupplierProduct?> GetSupplierProductAsync(Guid productId, int supplierId)
    {
        return await _context.SupplierProducts
            .Include(sp => sp.Supplier)
            .Include(sp => sp.Product)
            .FirstOrDefaultAsync(sp => sp.ProductId == productId && sp.SupplierId == supplierId);
    }

    public async Task<List<Supplier>> GetAvailableSuppliersForProductAsync(Guid productId)
    {
        var existingSupplierIds = await _context.SupplierProducts
            .Where(sp => sp.ProductId == productId)
            .Select(sp => sp.SupplierId)
            .ToListAsync();

        return await _context.Suppliers
            .Where(s => s.IsActive && !existingSupplierIds.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<SupplierProduct?> AddSupplierProductAsync(SupplierProduct supplierProduct)
    {
        // Check if mapping already exists
        var exists = await _context.SupplierProducts
            .AnyAsync(sp => sp.ProductId == supplierProduct.ProductId && sp.SupplierId == supplierProduct.SupplierId);

        if (exists)
        {
            return null;
        }

        supplierProduct.UpdatedAt = DateTime.UtcNow;

        _context.SupplierProducts.Add(supplierProduct);
        await _context.SaveChangesAsync();
        return supplierProduct;
    }

    public async Task<SupplierProduct?> UpdateSupplierProductAsync(SupplierProduct supplierProduct)
    {
        var existing = await _context.SupplierProducts
            .FirstOrDefaultAsync(sp => sp.ProductId == supplierProduct.ProductId && sp.SupplierId == supplierProduct.SupplierId);

        if (existing == null) return null;

        existing.SupplierProductCode = supplierProduct.SupplierProductCode;
        existing.CostPrice = supplierProduct.CostPrice;
        existing.Seq = supplierProduct.Seq;
        existing.IsActive = supplierProduct.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteSupplierProductAsync(Guid productId, int supplierId)
    {
        var supplierProduct = await _context.SupplierProducts
            .FirstOrDefaultAsync(sp => sp.ProductId == productId && sp.SupplierId == supplierId);

        if (supplierProduct == null) return false;

        _context.SupplierProducts.Remove(supplierProduct);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReorderSupplierProductsAsync(Guid productId, List<SupplierProductReorder> reorderList)
    {
        var supplierProducts = await _context.SupplierProducts
            .Where(sp => sp.ProductId == productId)
            .ToListAsync();

        foreach (var reorder in reorderList)
        {
            var sp = supplierProducts.FirstOrDefault(s => s.SupplierId == reorder.SupplierId);
            if (sp != null)
            {
                sp.Seq = (short)reorder.NewSequence;
                sp.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Product?> GetProductWithSuppliersAsync(Guid productId)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.SupplierProducts)
                .ThenInclude(sp => sp.Supplier)
            .FirstOrDefaultAsync(p => p.Id == productId);
    }
}

public class SupplierProductReorder
{
    public int SupplierId { get; set; }
    public int NewSequence { get; set; }
}
