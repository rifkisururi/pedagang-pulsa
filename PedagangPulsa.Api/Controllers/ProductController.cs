using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Infrastructure.Data;
using System.Security.Claims;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductController> _logger;

    public ProductController(AppDbContext context, ILogger<ProductController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.ProductCategories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code,
                Icon = c.IconUrl
            })
            .ToListAsync();

        return Ok(new CategoryListResponse
        {
            Success = true,
            Data = categories
        });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int? categoryId = null,
        [FromQuery] string? operatorParam = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        var userGuid = Guid.Parse(userId);

        // Get user's level
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userGuid);

        if (user == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "User not found",
                ErrorCode = "USER_NOT_FOUND"
            });
        }

        var query = _context.Products
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
                Description = p.Description,
                Price = p.ProductLevelPrices
                    .Where(plp => plp.LevelId == user.LevelId && plp.IsActive)
                    .Select(plp => plp.SellPrice)
                    .FirstOrDefault(),
                Available = p.ProductLevelPrices
                    .Any(plp => plp.LevelId == user.LevelId && plp.IsActive && plp.SellPrice > 0)
            })
            .ToListAsync();

        return Ok(new ProductListResponse
        {
            Success = true,
            Data = products,
            TotalRecords = totalRecords,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id}/price")]
    [Authorize]
    public async Task<IActionResult> GetPrice(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        var userGuid = Guid.Parse(userId);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userGuid);

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

        // Get price for user's level
        var levelPrice = await _context.ProductLevelPrices
            .Where(plp => plp.ProductId == id && plp.LevelId == user.LevelId && plp.IsActive)
            .Select(plp => plp.SellPrice)
            .FirstOrDefaultAsync();

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
                price = levelPrice > 0 ? levelPrice : (decimal?)null,
                levelId = user.LevelId,
                message = levelPrice > 0 ? "Price available for your level" : "Price not available for your level"
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
}
