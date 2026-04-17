using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Domain.Entities;

namespace PedagangPulsa.Application.Services;

public class SupplierService
{
    private readonly IAppDbContext _context;

    public SupplierService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<(List<Supplier> Suppliers, int TotalFiltered, int TotalRecords)> GetSuppliersPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        bool? isActive = null)
    {
        var query = _context.Suppliers.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(searchLower) ||
                (s.ApiBaseUrl != null && s.ApiBaseUrl.ToLower().Contains(searchLower)));
        }

        // Apply active filter
        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        var totalRecords = await _context.Suppliers.CountAsync();
        var totalFiltered = await query.CountAsync();

        var suppliers = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (suppliers, totalFiltered, totalRecords);
    }

    public async Task<Supplier?> GetSupplierByIdAsync(int id)
    {
        return await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<Supplier>> GetAllActiveSuppliersAsync()
    {
        return await _context.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Supplier?> CreateSupplierAsync(Supplier supplier)
    {
        // Check if supplier with same name exists
        var exists = await _context.Suppliers
            .AnyAsync(s => s.Name == supplier.Name);

        if (exists)
        {
            return null;
        }

        supplier.CreatedAt = DateTime.UtcNow;
        supplier.UpdatedAt = DateTime.UtcNow;

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();
        return supplier;
    }

    public async Task<Supplier?> UpdateSupplierAsync(Supplier supplier)
    {
        var existing = await _context.Suppliers.FindAsync(supplier.Id);
        if (existing == null) return null;

        // Check if another supplier with same name exists
        var nameExists = await _context.Suppliers
            .AnyAsync(s => s.Name == supplier.Name && s.Id != supplier.Id);

        if (nameExists)
        {
            return null;
        }

        existing.Name = supplier.Name;
        existing.ApiBaseUrl = supplier.ApiBaseUrl;
        existing.MemberId = supplier.MemberId;
        existing.Pin = supplier.Pin;
        existing.Password = supplier.Password;
        existing.TimeoutSeconds = supplier.TimeoutSeconds;
        existing.Balance = supplier.Balance;
        existing.IsActive = supplier.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteSupplierAsync(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null) return false;

        // Check if there are supplier products using this supplier
        var hasProducts = await _context.SupplierProducts
            .AnyAsync(sp => sp.SupplierId == id);

        if (hasProducts)
        {
            return false;
        }

        _context.Suppliers.Remove(supplier);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateSupplierBalanceAsync(int supplierId, decimal newBalance)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.Balance)
            .FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier == null) return false;

        if (supplier.Balance == null)
        {
            supplier.Balance = new SupplierBalance
            {
                SupplierId = supplierId,
                ActiveBalance = newBalance,
                UpdatedAt = DateTime.UtcNow
            };
        }
        else
        {
            supplier.Balance.ActiveBalance = newBalance;
            supplier.Balance.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }
}
