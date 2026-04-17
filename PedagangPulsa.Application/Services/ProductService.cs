using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Domain.Entities;

namespace PedagangPulsa.Application.Services;

public class ProductService
{
    private readonly IAppDbContext _context;

    public ProductService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<(List<Product> Products, int TotalFiltered, int TotalRecords)> GetProductsPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        int? categoryId = null,
        bool? isActive = null,
        string? sortColumn = null,
        string? sortDirection = null)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchLower) ||
                p.Code.ToLower().Contains(searchLower) ||
                (p.Description != null && p.Description.ToLower().Contains(searchLower)));
        }

        // Apply category filter
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        // Apply active status filter
        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        // Get total records
        var totalRecords = await _context.Products.CountAsync();
        var totalFiltered = await query.CountAsync();

        // Apply sorting
        if (!string.IsNullOrWhiteSpace(sortColumn))
        {
            var sortDir = sortDirection?.ToLower() == "desc";
            var sortCol = sortColumn.ToLower();

            if (sortDir)
            {
                query = sortCol switch
                {
                    "code" => query.OrderByDescending(p => p.Code),
                    "name" => query.OrderByDescending(p => p.Name),
                    "category" => query.OrderByDescending(p => p.Category.Name),
                    "denomination" => query.OrderByDescending(p => p.Denomination),
                    "isactive" => query.OrderByDescending(p => p.IsActive),
                    "createdat" => query.OrderByDescending(p => p.CreatedAt),
                    _ => query.OrderByDescending(p => p.CreatedAt)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "code" => query.OrderBy(p => p.Code),
                    "name" => query.OrderBy(p => p.Name),
                    "category" => query.OrderBy(p => p.Category.Name),
                    "denomination" => query.OrderBy(p => p.Denomination),
                    "isactive" => query.OrderBy(p => p.IsActive),
                    "createdat" => query.OrderBy(p => p.CreatedAt),
                    _ => query.OrderBy(p => p.CreatedAt)
                };
            }
        }
        else
        {
            query = query.OrderBy(p => p.Name);
        }

        // Apply pagination
        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (products, totalFiltered, totalRecords);
    }

    public async Task<Product?> GetProductByIdAsync(Guid id)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.ProductLevelPrices)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<ProductCategory>> GetCategoriesAsync()
    {
        return await _context.ProductCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
    }

    public async Task<List<UserLevel>> GetLevelsAsync()
    {
        return await _context.UserLevels
            .OrderBy(l => l.Id)
            .ToListAsync();
    }

     public async Task<Product?> CreateProductAsync(Product product, List<ProductLevelPrice>? levelPrices)
     {
         var existingProduct = await _context.Products
             .FirstOrDefaultAsync(p => p.Code == product.Code);

         if (existingProduct != null)
         {
             return null;
         }

         _context.Products.Add(product);

         if (levelPrices != null && levelPrices.Any())
         {
             foreach (var price in levelPrices)
             {
                 price.Product = product;
             }
             _context.ProductLevelPrices.AddRange(levelPrices);
         }

         await _context.SaveChangesAsync();
         return product;
     }

    public async Task<Product?> UpdateProductAsync(Product product, List<ProductLevelPrice>? levelPrices)
    {
        var existing = await _context.Products
            .Include(p => p.ProductLevelPrices)
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        if (existing == null) return null;

        var duplicateCode = await _context.Products
            .AnyAsync(p => p.Code == product.Code && p.Id != product.Id);

        if (duplicateCode)
        {
            return null;
        }

        existing.Name = product.Name;
        existing.Code = product.Code;
        existing.CategoryId = product.CategoryId;
        existing.Denomination = product.Denomination;
        existing.Operator = product.Operator;
        existing.Description = product.Description;
        existing.IsActive = product.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        // Update level prices
        if (levelPrices != null)
        {
            var existingPrices = existing.ProductLevelPrices.ToList();
            foreach (var newPrice in levelPrices)
            {
                var existingPrice = existingPrices.FirstOrDefault(lp => lp.LevelId == newPrice.LevelId);
                if (existingPrice != null)
                {
                    existingPrice.Margin = newPrice.Margin;
                    existingPrice.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    newPrice.ProductId = existing.Id;
                    _context.ProductLevelPrices.Add(newPrice);
                }
            }
        }

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteProductAsync(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Product>> GetActiveProductsAsync()
    {
        return await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<bool> UpdateProductPriceAsync(Guid productId, int levelId, decimal margin, string? updatedBy = null)
    {
        var price = await _context.ProductLevelPrices
            .FirstOrDefaultAsync(p => p.ProductId == productId && p.LevelId == levelId);

        if (price == null)
        {
            price = new ProductLevelPrice
            {
                ProductId = productId,
                LevelId = levelId,
                Margin = margin,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ProductLevelPrices.Add(price);
        }
        else
        {
            price.Margin = margin;
            price.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ProductLevelPrice>> GetProductPricesAsync(Guid productId)
    {
        return await _context.ProductLevelPrices
            .Where(p => p.ProductId == productId)
            .Include(p => p.Level)
            .OrderBy(p => p.LevelId)
            .ToListAsync();
    }
}
