using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Api.Controllers;
using PedagangPulsa.Api.DTOs;
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
    private readonly ProductController _controller;

    public ProductControllerTests()
    {
        _context = new TestDbContext();
        _context.SeedAsync().Wait();

        _loggerMock = MockServices.CreateLogger<ProductController>();
        _controller = new ProductController(_context, _loggerMock.Object);
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
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
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
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
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
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
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
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
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
    public async Task GetProducts_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No authenticated user set up

        // Act
        var result = await _controller.GetProducts();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INVALID_TOKEN");
    }

    [Fact]
    public async Task GetPrice_WithValidProduct_ReturnsUserLevelPrice()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        var product = await _context.Products.FirstAsync();

        // Act
        var result = await _controller.GetPrice(product.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic response = okResult.Value;

        response.success.Should().Be(true);
        response.data.price.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPrice_WithInvalidProduct_ReturnsNotFound()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
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
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        var product = await _context.Products.FirstAsync();

        // Act
        var result = await _controller.GetProductSuppliers(product.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic response = okResult.Value;

        response.success.Should().Be(true);
        response.data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProductSuppliers_WithInvalidProduct_ReturnsEmptyList()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        var nonExistentProductId = Guid.NewGuid();

        // Act
        var result = await _controller.GetProductSuppliers(nonExistentProductId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic response = okResult.Value;

        response.success.Should().Be(true);
        response.data.Should().NotBeNull();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.CleanAsync();
        await _context.DisposeAsync();
    }
}
