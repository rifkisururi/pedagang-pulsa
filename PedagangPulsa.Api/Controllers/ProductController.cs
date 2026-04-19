using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Infrastructure.Data;
using System.Security.Claims;
using System.Text.Json;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductController> _logger;
    private readonly IRedisService _redis;
    private readonly IProductCacheService _productCache;
    private readonly IConfiguration _configuration;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProductController(AppDbContext context, ILogger<ProductController> logger, IRedisService redis, IProductCacheService productCache, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _redis = redis;
        _productCache = productCache;
        _configuration = configuration;
    }

    [HttpGet("categories")]
    [Authorize]
    public async Task<IActionResult> GetCategories()
    {
        const string cacheKey = "product:categories";
        var cached = await _redis.GetAsync(cacheKey);
        if (cached != null)
        {
            var cachedResult = JsonSerializer.Deserialize<CategoryListResponse>(cached, _jsonOpts)!;
            return Ok(cachedResult);
        }

        var categories = await _context.ProductCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        var operators = await _context.Products
            .Where(p => p.IsActive && p.Operator != null)
            .Select(p => new { p.CategoryId, Operator = p.Operator!.ToLower() })
            .Distinct()
            .ToListAsync();

        var subCategories = operators
            .GroupBy(o => o.CategoryId)
            .ToDictionary(g => g.Key, g => g.Select(o => o.Operator).OrderBy(x => x).ToList());

        var result = categories.Select(c => new CategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            Code = c.Code,
            Icon = c.IconUrl,
            SubCategories = subCategories.GetValueOrDefault(c.Id, new())
        }).ToList();

        var response = new CategoryListResponse { Success = true, Data = result };
        await _redis.SetAsync(cacheKey, JsonSerializer.Serialize(response, _jsonOpts), TimeSpan.FromMinutes(10));

        return Ok(response);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int? categoryId = null,
        [FromQuery] string? operatorParam = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token format",
                ErrorCode = "INVALID_TOKEN_FORMAT"
            });
        }

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "User not found",
                ErrorCode = "USER_NOT_FOUND"
            });
        }

        // Check cache
        var cacheKey = $"products:{categoryId ?? 0}:{operatorParam ?? "_"}:{user.LevelId}:{page}:{pageSize}";
        var cached = await _redis.GetAsync(cacheKey);
        if (cached != null)
        {
            return Ok(JsonSerializer.Deserialize<ProductListResponse>(cached, _jsonOpts)!);
        }

        var query = _context.Products.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive);

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(operatorParam))
        {
            query = query.Where(p => p.Operator == operatorParam);
        }

        var totalRecords = await query.CountAsync();

        var productIds = await query
            .Select(p => p.Id)
            .ToListAsync();

        var costPrices = await _context.SupplierProducts
            .Where(sp => productIds.Contains(sp.ProductId) && sp.IsActive)
            .GroupBy(sp => sp.ProductId)
            .Select(g => new { ProductId = g.Key, CostPrice = g.OrderBy(sp => sp.Seq).First().CostPrice })
            .ToDictionaryAsync(x => x.ProductId, x => x.CostPrice);

        var pagedIds = await query
            .OrderBy(p => p.Category!.Name)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => p.Id)
            .ToListAsync();

        var products = await query
            .OrderBy(p => p.Category!.Name)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListDto
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                CategoryName = p.Category!.Name,
                Operator = p.Operator,
                Denomination = p.Denomination,
                Description = p.Description
            })
            .ToListAsync();

        var margins = await _context.ProductLevelPrices
            .Where(plp => pagedIds.Contains(plp.ProductId) && plp.LevelId == user.LevelId && plp.IsActive)
            .GroupBy(plp => plp.ProductId)
            .Select(g => new { ProductId = g.Key, Margin = g.OrderByDescending(plp => plp.UpdatedAt).First().Margin })
            .ToDictionaryAsync(x => x.ProductId, x => x.Margin);

        foreach (var dto in products)
        {
            var hasCost = costPrices.TryGetValue(dto.Id, out var cost) && cost > 0;
            var hasMargin = margins.TryGetValue(dto.Id, out var margin) && margin > 0;

            dto.Price = (hasCost && hasMargin) ? cost + margin : (decimal?)null;
            dto.Available = hasCost && hasMargin;
        }

        var productResponse = new ProductListResponse
        {
            Success = true,
            Data = products,
            TotalRecords = totalRecords,
            Page = page,
            PageSize = pageSize
        };
        await _redis.SetAsync(cacheKey, JsonSerializer.Serialize(productResponse, _jsonOpts), TimeSpan.FromMinutes(5));

        return Ok(productResponse);
    }

    [HttpGet("{id}/price")]
    [Authorize]
    public async Task<IActionResult> GetPrice(Guid id)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token format",
                ErrorCode = "INVALID_TOKEN_FORMAT"
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "User not found",
                ErrorCode = "USER_NOT_FOUND"
            });
        }

        var product = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.Id == id && p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Code,
                CategoryName = p.Category!.Name,
                p.Operator,
                p.Denomination,
                p.Description
            })
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "Product not found",
                ErrorCode = "PRODUCT_NOT_FOUND"
            });
        }

        var levelPrice = await _context.ProductLevelPrices
            .Where(plp => plp.ProductId == id && plp.LevelId == user.LevelId && plp.IsActive)
            .Select(plp => plp.Margin)
            .FirstOrDefaultAsync();

        var costPrice = await _context.SupplierProducts
            .Where(sp => sp.ProductId == id && sp.IsActive)
            .OrderBy(sp => sp.Seq)
            .Select(sp => sp.CostPrice)
            .FirstOrDefaultAsync();

        decimal computedSellPrice = 0;
        if (levelPrice > 0 && costPrice > 0)
        {
            computedSellPrice = costPrice + levelPrice;
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                id = product.Id,
                name = product.Name,
                code = product.Code,
                categoryName = product.CategoryName,
                @operator = product.Operator,
                denomination = product.Denomination,
                description = product.Description,
                price = computedSellPrice > 0 ? computedSellPrice : (decimal?)null,
                levelId = user.LevelId,
                message = computedSellPrice > 0 ? "Price available for your level" : "Price not available for your level"
            }
        });
    }

    [HttpGet("{id}/suppliers")]
    [Authorize]
    public async Task<IActionResult> GetProductSuppliers(Guid id)
    {
        var suppliers = await _context.SupplierProducts
            .Include(sp => sp.Supplier)
            .Where(sp => sp.ProductId == id && sp.IsActive && sp.Supplier.IsActive)
            .OrderBy(sp => sp.Seq)
            .Select(sp => new
            {
                supplierId = sp.SupplierId,
                supplierName = sp.Supplier!.Name,
                supplierCode = sp.Supplier.Code,
                productCode = sp.SupplierProductCode,
                productName = sp.SupplierProductName,
                costPrice = sp.CostPrice,
                sequence = sp.Seq
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = suppliers
        });
    }

    /// <summary>
    /// Invalidate product cache. Requires X-Api-Key header for authentication.
    /// </summary>
    [HttpPost("cache/invalidate")]
    public async Task<IActionResult> InvalidateCache()
    {
        var apiKey = _configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return StatusCode(500, new ErrorResponse
            {
                Message = "Internal API key not configured",
                ErrorCode = "CONFIG_ERROR"
            });
        }

        var requestKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(requestKey) || requestKey != apiKey)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid or missing API key",
                ErrorCode = "INVALID_API_KEY"
            });
        }

        await _productCache.InvalidateProductCacheAsync();
        _logger.LogInformation("Product cache invalidated via API");

        return Ok(new { success = true, message = "Product cache invalidated" });
    }
}
