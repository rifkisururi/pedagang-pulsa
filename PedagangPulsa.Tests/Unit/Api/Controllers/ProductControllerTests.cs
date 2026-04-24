using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PedagangPulsa.Api.Controllers;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Domain.Configuration;
using PedagangPulsa.Infrastructure.Data;
using PedagangPulsa.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Api.Controllers;

public class ProductControllerTests : IAsyncDisposable
{
    private readonly TestDbContext _context;
    private readonly Mock<ILogger<ProductController>> _loggerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IProductCacheService> _productCacheMock;
    private readonly IConfiguration _configuration;
    private readonly ProductController _controller;

    public ProductControllerTests()
    {
        _context = new TestDbContext();
        _context.SeedAsync().Wait();

        _loggerMock = MockServices.CreateLogger<ProductController>();
        _redisServiceMock = new Mock<IRedisService>();
        _redisServiceMock.Setup(r => r.GetAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        _productCacheMock = new Mock<IProductCacheService>();
        _configuration = new ConfigurationBuilder().Build();

        _controller = new ProductController(
            _context,
            _loggerMock.Object,
            _redisServiceMock.Object,
            _productCacheMock.Object,
            _configuration,
            Options.Create(new PricingConfig())
        );
    }

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // ===== Existing endpoint tests =====

    [Fact]
    public async Task GetCategories_ReturnsAllCategories()
    {
        // Arrange & Act
        var result = await _controller.GetCategories();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CategoryListResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeEmpty();
        response.Data.First().Name.Should().Be("Pulsa");
    }

    [Fact]
    public async Task GetProducts_WithAuthentication_ReturnsUserLevelPricing()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Act
        var result = await _controller.GetProducts();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProductListResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeEmpty();
        response.Data.First().Price.Should().BeGreaterThan(0);
        response.TotalRecords.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetProducts_WithCategoryIdFilter_ReturnsFilteredProducts()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var category = await _context.ProductCategories.FirstAsync();

        // Act
        var result = await _controller.GetProducts(categoryId: category.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProductListResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().OnlyContain(p => p.CategoryName == "Pulsa");
    }

    [Fact]
    public async Task GetProducts_WithOperatorFilter_ReturnsFilteredProducts()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Act
        var result = await _controller.GetProducts(operatorParam: "Indosat");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProductListResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().OnlyContain(p => p.Operator == "Indosat");
    }

    [Fact]
    public async Task GetProducts_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Act
        var result = await _controller.GetProducts(page: 1, pageSize: 1);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProductListResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(1);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(1);
    }


    [Fact]
    public async Task GetPrice_WithValidProduct_ReturnsUserLevelPrice()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var product = await _context.Products.FirstAsync();

        // Act
        var result = await _controller.GetPrice(product.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.SerializeToElement(okResult.Value);
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("data").GetProperty("price").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task GetPrice_WithInvalidProduct_ReturnsNotFound()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var nonExistentProductId = Guid.NewGuid();

        // Act
        var result = await _controller.GetPrice(nonExistentProductId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task GetProductSuppliers_WithValidProduct_ReturnsSuppliers()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var product = await _context.Products.FirstAsync();

        // Act
        var result = await _controller.GetProductSuppliers(product.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.SerializeToElement(okResult.Value);

        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("data").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetProductSuppliers_WithInvalidProduct_ReturnsEmptyList()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var nonExistentProductId = Guid.NewGuid();

        // Act
        var result = await _controller.GetProductSuppliers(nonExistentProductId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.SerializeToElement(okResult.Value);

        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    // ===== Catalog endpoint tests =====

    [Fact]
    public async Task GetCatalogCategories_ReturnsActiveCategories()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Act
        var result = await _controller.GetCatalogCategories();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CatalogCategoryListResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeEmpty();
        response.Data.Should().OnlyContain(c => c.Id > 0 && !string.IsNullOrEmpty(c.Name) && !string.IsNullOrEmpty(c.Code));
    }

    [Fact]
    public async Task GetCatalogCategories_ReturnsCachedResponse_WhenAvailable()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var cachedResponse = new CatalogCategoryListResponse
        {
            Success = true,
            Data = new List<CatalogCategoryDto> { new() { Id = 99, Name = "Cached", Code = "CACHED" } }
        };
        var camelOpts = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        var cachedJson = System.Text.Json.JsonSerializer.Serialize(cachedResponse, camelOpts);
        _redisServiceMock.Setup(r => r.GetAsync("catalog:categories")).ReturnsAsync(cachedJson);

        // Act
        var result = await _controller.GetCatalogCategories();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CatalogCategoryListResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(1);
        response.Data[0].Name.Should().Be("Cached");
    }

    [Fact]
    public async Task GetCatalogByCategory_ReturnsGroupedProducts()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var category = await _context.ProductCategories.FirstAsync();

        // Act
        var result = await _controller.GetCatalogByCategory(category.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CatalogByCategoryResponse>().Subject;

        response.Success.Should().BeTrue();
        response.CategoryId.Should().Be(category.Id);
        response.CategoryName.Should().Be(category.Name);
        response.Data.Should().NotBeEmpty();

        // Verify products within groups have price info
        var groupWithProducts = response.Data.FirstOrDefault(g => g.Products.Count > 0);
        if (groupWithProducts != null)
        {
            groupWithProducts.Products.Should().OnlyContain(p => p.Id != Guid.Empty);
            groupWithProducts.Products.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Name));
        }
    }

    [Fact]
    public async Task GetCatalogByCategory_ReturnsCachedResponse_WhenAvailable()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var cachedResponse = new CatalogByCategoryResponse
        {
            Success = true,
            CategoryId = 1,
            CategoryName = "Cached Category",
            Data = new List<CatalogGroupDto>
            {
                new() { Id = 1, Name = "Cached Group", Products = new List<CatalogProductDto>() }
            }
        };
        var camelOpts = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        var cachedJson = System.Text.Json.JsonSerializer.Serialize(cachedResponse, camelOpts);
        _redisServiceMock.Setup(r => r.GetAsync(It.Is<string>(k => k.StartsWith("catalog:")))).ReturnsAsync(cachedJson);

        // Act
        var result = await _controller.GetCatalogByCategory(1);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CatalogByCategoryResponse>().Subject;

        response.CategoryName.Should().Be("Cached Category");
    }

    [Fact]
    public async Task GetCatalogByCategory_WithInvalidCategory_ReturnsNotFound()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Act
        var result = await _controller.GetCatalogByCategory(99999);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("CATEGORY_NOT_FOUND");
    }

    [Fact]
    public async Task GetCatalogByCategory_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - user without NameIdentifier claim
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        // Act
        var result = await _controller.GetCatalogByCategory(1);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.ErrorCode.Should().Be("INVALID_TOKEN");
    }

    [Fact]
    public async Task GetCatalogByCategory_ComputesPriceBasedOnUserLevel()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        var user2 = await _context.Users.FirstAsync(u => u.UserName == "user2");

        var category = await _context.ProductCategories.FirstAsync();

        // Act - user1 (member1)
        SetupAuthenticatedUser(user1.Id);
        var result1 = await _controller.GetCatalogByCategory(category.Id);
        var okResult1 = result1.Should().BeOfType<OkObjectResult>().Subject;
        var response1 = okResult1.Value.Should().BeOfType<CatalogByCategoryResponse>().Subject;
        var user1Prices = response1.Data
            .SelectMany(g => g.Products)
            .Where(p => p.Price.HasValue)
            .ToDictionary(p => p.Id, p => p.Price!.Value);

        // Act - user2 (member2)
        SetupAuthenticatedUser(user2.Id);
        _redisServiceMock.Setup(r => r.GetAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        var result2 = await _controller.GetCatalogByCategory(category.Id);
        var okResult2 = result2.Should().BeOfType<OkObjectResult>().Subject;
        var response2 = okResult2.Value.Should().BeOfType<CatalogByCategoryResponse>().Subject;
        var user2Prices = response2.Data
            .SelectMany(g => g.Products)
            .Where(p => p.Price.HasValue)
            .ToDictionary(p => p.Id, p => p.Price!.Value);

        // Assert - different levels should produce different prices (margin differs per level)
        foreach (var productId in user1Prices.Keys.Intersect(user2Prices.Keys))
        {
            user1Prices[productId].Should().NotBe(user2Prices[productId],
                $"prices for product {productId} should differ between user levels");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _context.CleanAsync();
        await _context.DisposeAsync();
    }

    public class ApiResponse<T>
    {
        public bool success { get; set; }
        public T data { get; set; }
    }
}
