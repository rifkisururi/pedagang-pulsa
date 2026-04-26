using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Domain.Configuration;
using PedagangPulsa.Infrastructure.Data;
using System.Security.Claims;
using System.Text.Json;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductController> _logger;
    private readonly IRedisService _redis;
    private readonly IProductCacheService _productCache;
    private readonly IConfiguration _configuration;
    private readonly PricingConfig _pricingConfig;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProductController(AppDbContext context, ILogger<ProductController> logger, IRedisService redis, IProductCacheService productCache, IConfiguration configuration, IOptions<PricingConfig> pricingConfig)
    {
        _context = context;
        _logger = logger;
        _redis = redis;
        _productCache = productCache;
        _configuration = configuration;
        _pricingConfig = pricingConfig.Value;
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
            .Include(p => p.ProductGroup)
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
                ProductGroupId = p.ProductGroupId,
                ProductGroupName = p.ProductGroup!.Name,
                Operator = p.Operator,
                Denomination = p.Denomination,
                ValidityDays = p.ValidityDays,
                ValidityText = p.ValidityText,
                QuotaMb = p.QuotaMb,
                QuotaText = p.QuotaText,
                Description = p.Description,
                IsInquiryProduct = p.IsInquiryProduct
            })
            .ToListAsync();

        var margins = await _context.ProductLevelPrices
            .Where(plp => pagedIds.Contains(plp.ProductId) && plp.LevelId == user.LevelId && plp.IsActive)
            .GroupBy(plp => plp.ProductId)
            .Select(g => new { ProductId = g.Key, Margin = g.OrderByDescending(plp => plp.UpdatedAt).First().Margin })
            .ToDictionaryAsync(x => x.ProductId, x => x.Margin);

        foreach (var dto in products)
        {
            if (dto.IsInquiryProduct)
            {
                dto.Available = true;
                dto.Price = null;
            }
            else
            {
                var hasCost = costPrices.TryGetValue(dto.Id, out var cost) && cost > 0;
                var hasMargin = margins.TryGetValue(dto.Id, out var margin) && margin > 0;

                // Fallback to default margin from config if no margin set
                var effectiveMargin = hasMargin ? margin : _pricingConfig.GetDefaultMargin(cost);

                dto.Price = (hasCost && effectiveMargin > 0) ? cost + effectiveMargin : (decimal?)null;
                dto.Available = hasCost && effectiveMargin > 0;
            }
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
            .Include(p => p.ProductGroup)
            .Where(p => p.Id == id && p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Code,
                CategoryName = p.Category!.Name,
                ProductGroupName = p.ProductGroup != null ? p.ProductGroup.Name : null,
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
        var effectiveMargin = levelPrice > 0 ? levelPrice : _pricingConfig.GetDefaultMargin(costPrice);
        if (effectiveMargin > 0 && costPrice > 0)
        {
            computedSellPrice = costPrice + effectiveMargin;
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

    [HttpGet("catalog/categories")]
    [Authorize]
    public async Task<IActionResult> GetCatalogCategories()
    {
        const string cacheKey = "catalog:categories";
        var cached = await _redis.GetAsync(cacheKey);
        if (cached != null)
        {
            return Ok(JsonSerializer.Deserialize<CatalogCategoryListResponse>(cached, _jsonOpts)!);
        }

        var categories = await _context.ProductCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new CatalogCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code,
                Icon = c.IconUrl
            })
            .ToListAsync();

        var response = new CatalogCategoryListResponse { Success = true, Data = categories };
        await _redis.SetAsync(cacheKey, JsonSerializer.Serialize(response, _jsonOpts), TimeSpan.FromMinutes(10));

        return Ok(response);
    }

    [HttpGet("catalog/{categoryId}")]
    [Authorize]
    public async Task<IActionResult> GetCatalogByCategory(int categoryId)
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

        var cacheKey = $"catalog:{categoryId}:{user.LevelId}";
        var cached = await _redis.GetAsync(cacheKey);
        if (cached != null)
        {
            return Ok(JsonSerializer.Deserialize<CatalogByCategoryResponse>(cached, _jsonOpts)!);
        }

        var category = await _context.ProductCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.IsActive);

        if (category == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "Category not found",
                ErrorCode = "CATEGORY_NOT_FOUND"
            });
        }

        // Load groups for this category
        var groups = await _context.ProductGroups.AsNoTracking()
            .Where(g => g.CategoryId == categoryId && g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ToListAsync();

        // Load all active products for this category
        var products = await _context.Products.AsNoTracking()
            .Where(p => p.CategoryId == categoryId && p.IsActive)
            .OrderBy(p => p.ProductGroupId)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var productIds = products.Select(p => p.Id).ToList();

        // Batch load cost prices (lowest seq supplier)
        var costPrices = await _context.SupplierProducts
            .Where(sp => productIds.Contains(sp.ProductId) && sp.IsActive)
            .GroupBy(sp => sp.ProductId)
            .Select(g => new { ProductId = g.Key, CostPrice = g.OrderBy(sp => sp.Seq).First().CostPrice })
            .ToDictionaryAsync(x => x.ProductId, x => x.CostPrice);

        // Batch load margins for user's level
        var margins = await _context.ProductLevelPrices
            .Where(plp => productIds.Contains(plp.ProductId) && plp.LevelId == user.LevelId && plp.IsActive)
            .GroupBy(plp => plp.ProductId)
            .Select(g => new { ProductId = g.Key, Margin = g.OrderByDescending(plp => plp.UpdatedAt).First().Margin })
            .ToDictionaryAsync(x => x.ProductId, x => x.Margin);

        // Build product DTOs with prices, grouped by ProductGroupId
        var productsByGroup = new Dictionary<int, List<CatalogProductDto>>();
        List<CatalogProductDto>? ungrouped = null;

        foreach (var p in products)
        {
            CatalogProductDto dto;

            if (p.IsInquiryProduct)
            {
                dto = new CatalogProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    Denomination = p.Denomination,
                    ValidityDays = p.ValidityDays,
                    ValidityText = p.ValidityText,
                    QuotaMb = p.QuotaMb,
                    QuotaText = p.QuotaText,
                    Description = p.Description,
                    IsInquiryProduct = true,
                    Available = true,
                    Price = null
                };
            }
            else
            {
                var hasCost = costPrices.TryGetValue(p.Id, out var cost) && cost > 0;
                var hasMargin = margins.TryGetValue(p.Id, out var margin) && margin > 0;

                // Fallback to default margin from config if no margin set
                var effectiveMargin = hasMargin ? margin : _pricingConfig.GetDefaultMargin(cost);

                dto = new CatalogProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    Denomination = p.Denomination,
                    ValidityDays = p.ValidityDays,
                    ValidityText = p.ValidityText,
                    QuotaMb = p.QuotaMb,
                    QuotaText = p.QuotaText,
                    Description = p.Description,
                    IsInquiryProduct = false,
                    Price = (hasCost && effectiveMargin > 0) ? cost + effectiveMargin : null,
                    Available = hasCost && effectiveMargin > 0
                };
            }

            if (p.ProductGroupId.HasValue)
            {
                if (!productsByGroup.ContainsKey(p.ProductGroupId.Value))
                    productsByGroup[p.ProductGroupId.Value] = new();
                productsByGroup[p.ProductGroupId.Value].Add(dto);
            }
            else
            {
                ungrouped ??= new();
                ungrouped.Add(dto);
            }
        }

        var groupDtos = groups.Select(g => new CatalogGroupDto
        {
            Id = g.Id,
            Name = g.Name,
            Operator = g.Operator,
            Products = productsByGroup.GetValueOrDefault(g.Id, new())
        }).Where(g => g.Products.Count > 0).ToList();

        if (ungrouped is { Count: > 0 })
        {
            groupDtos.Add(new CatalogGroupDto
            {
                Id = 0,
                Name = "Lainnya",
                Operator = null,
                Products = ungrouped
            });
        }

        var response = new CatalogByCategoryResponse
        {
            Success = true,
            CategoryId = category.Id,
            CategoryName = category.Name,
            Data = groupDtos
        };

        await _redis.SetAsync(cacheKey, JsonSerializer.Serialize(response, _jsonOpts), TimeSpan.FromMinutes(5));

        return Ok(response);
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
