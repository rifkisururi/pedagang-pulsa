using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Application.Services;

public class ProductService
{
    private readonly AppDbContext _context;

    public ProductService(AppDbContext context)
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
            // Check if using InMemory database (for testing)
            var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

            if (isInMemory)
            {
                // Use case-insensitive Contains for InMemory
                var searchLower = search.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(searchLower) ||
                    p.Code.ToLower().Contains(searchLower) ||
                    (p.Description != null && p.Description.ToLower().Contains(searchLower)));
            }
            else
            {
                // Use ILike for PostgreSQL
                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, $"%{search}%") ||
                    EF.Functions.ILike(p.Code, $"%{search}%") ||
                    EF.Functions.ILike(p.Description ?? "", $"%{search}%"));
            }
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
                    existingPrice.SellPrice = newPrice.SellPrice;
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

    public async Task<bool> UpdateProductPriceAsync(Guid productId, int levelId, decimal sellPrice, string? updatedBy = null)
    {
        var price = await _context.ProductLevelPrices
            .FirstOrDefaultAsync(p => p.ProductId == productId && p.LevelId == levelId);

        if (price == null)
        {
            // Create new price entry
            price = new ProductLevelPrice
            {
                ProductId = productId,
                LevelId = levelId,
                SellPrice = sellPrice,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ProductLevelPrices.Add(price);
        }
        else
        {
            price.SellPrice = sellPrice;
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
