using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Tests.Helpers;

namespace PedagangPulsa.Tests.Unit.Application.Services;

public class ProductServiceTests : IAsyncLifetime
{
    private TestDbContext _context = null!;
    private ProductService _service = null!;

    public async Task InitializeAsync()
    {
        _context = new TestDbContext();
        _service = new ProductService(_context);

        // Clean up database before seeding to avoid primary key conflicts
        await _context.CleanupBeforeSeedAsync();
        await SeedDataAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean up test data after all tests
        await _context.CleanupBeforeSeedAsync();
        await _context.DisposeAsync();
    }

    private async Task SeedDataAsync()
    {
        // Add categories
        var pulsaCategory = new ProductCategory
        {
            Id = 1,
            Name = "Pulsa",
            Code = "PULSA",
            SortOrder = 1,
            IsActive = true
        };

        var dataCategory = new ProductCategory
        {
            Id = 2,
            Name = "Data",
            Code = "DATA",
            SortOrder = 2,
            IsActive = true
        };

        _context.ProductCategories.AddRange(pulsaCategory, dataCategory);
        await _context.SaveChangesAsync();

        // Add user levels
        var bronzeLevel = new UserLevel
        {
            Id = 1,
            Name = "Bronze",
            Description = "Bronze Level",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 2,
            CanTransfer = true,
            IsActive = true
        };

        var silverLevel = new UserLevel
        {
            Id = 2,
            Name = "Silver",
            Description = "Silver Level",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 1.5m,
            CanTransfer = true,
            IsActive = true
        };

        var goldLevel = new UserLevel
        {
            Id = 3,
            Name = "Gold",
            Description = "Gold Level",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 1,
            CanTransfer = true,
            IsActive = true
        };

        _context.UserLevels.AddRange(bronzeLevel, silverLevel, goldLevel);
        await _context.SaveChangesAsync();
    }

    #region Product CRUD Tests

    [Fact]
    public async Task CreateProductAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Pulsa Telkomsel 5.000",
            Code = "TSEL5",
            Denomination = 5000,
            Operator = "Telkomsel",
            Description = "Pulsa Telkomsel 5.000",
            IsActive = true
        };

        // Act
        var result = await _service.CreateProductAsync(product, null);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Name.Should().Be("Pulsa Telkomsel 5.000");
        result.Code.Should().Be("TSEL5");

        var savedProduct = await _context.Products.FindAsync(product.Id);
        savedProduct.Should().NotBeNull();
        savedProduct!.Code.Should().Be("TSEL5");
    }

    [Fact]
    public async Task CreateProductAsync_WithDuplicateCode_ReturnsNull()
    {
        // Arrange
        var existingProduct = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Existing Product",
            Code = "TSEL5",
            Denomination = 5000,
            Operator = "Telkomsel",
            IsActive = true
        };

        await _service.CreateProductAsync(existingProduct, null);

        var newProduct = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "New Product",
            Code = "TSEL5", // Duplicate code
            Denomination = 10000,
            Operator = "Telkomsel",
            IsActive = true
        };

        // Act
        var result = await _service.CreateProductAsync(newProduct, null);

        // Assert
        result.Should().BeNull();

        // Verify only one product with this code exists
        var productsWithSameCode = await _context.Products
            .Where(p => p.Code == "TSEL5")
            .ToListAsync();

        productsWithSameCode.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateProductAsync_WithLevelPrices_CreatesPrices()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Pulsa Telkomsel 5.000",
            Code = "TSEL5",
            Denomination = 5000,
            Operator = "Telkomsel",
            IsActive = true
        };

        var levelPrices = new List<ProductLevelPrice>
        {
            new() { ProductId = product.Id, LevelId = 1, Margin = 5600 },
            new() { ProductId = product.Id, LevelId = 2, Margin = 5550 },
            new() { ProductId = product.Id, LevelId = 3, Margin = 5500 }
        };

        // Act
        var result = await _service.CreateProductAsync(product, levelPrices);

        // Assert
        var prices = await _context.ProductLevelPrices
            .Where(p => p.ProductId == product.Id)
            .ToListAsync();

        prices.Should().HaveCount(3);
        prices.Should().Contain(p => p.LevelId == 1 && p.Margin == 5600);
        prices.Should().Contain(p => p.LevelId == 2 && p.Margin == 5550);
        prices.Should().Contain(p => p.LevelId == 3 && p.Margin == 5500);
    }

    [Fact]
    public async Task UpdateProductAsync_WithValidData_UpdatesProduct()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Original Name",
            Code = "TSEL5",
            Denomination = 5000,
            Operator = "Telkomsel",
            IsActive = true
        };

        await _service.CreateProductAsync(product, null);

        var updatedProduct = new Product
        {
            Id = product.Id,
            CategoryId = 1,
            Name = "Updated Name",
            Code = "TSEL5-UPDATED",
            Denomination = 10000,
            Operator = "Telkomsel Updated",
            Description = "Updated description",
            IsActive = false
        };

        // Act
        var result = await _service.UpdateProductAsync(updatedProduct, null);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Code.Should().Be("TSEL5-UPDATED");
        result.Denomination.Should().Be(10000);
        result.Operator.Should().Be("Telkomsel Updated");
        result.Description.Should().Be("Updated description");
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProductAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var nonExistentProduct = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Non-existent",
            Code = "NONE"
        };

        // Act
        var result = await _service.UpdateProductAsync(nonExistentProduct, null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProductAsync_WithLevelPrices_UpdatesPrices()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Pulsa Telkomsel 5.000",
            Code = "TSEL5",
            Denomination = 5000,
            Operator = "Telkomsel",
            IsActive = true
        };

        var originalPrices = new List<ProductLevelPrice>
        {
            new() { ProductId = product.Id, LevelId = 1, Margin = 5600 },
            new() { ProductId = product.Id, LevelId = 2, Margin = 5550 }
        };

        await _service.CreateProductAsync(product, originalPrices);

        var updatedPrices = new List<ProductLevelPrice>
        {
            new() { ProductId = product.Id, LevelId = 1, Margin = 5700 }, // Updated
            new() { ProductId = product.Id, LevelId = 2, Margin = 5550 }, // Same
            new() { ProductId = product.Id, LevelId = 3, Margin = 5500 }  // New
        };

        // Act
        var result = await _service.UpdateProductAsync(product, updatedPrices);

        // Assert
        var prices = await _service.GetProductPricesAsync(product.Id);
        prices.Should().HaveCount(3);
        prices.Should().Contain(p => p.LevelId == 1 && p.Margin == 5700);
        prices.Should().Contain(p => p.LevelId == 2 && p.Margin == 5550);
        prices.Should().Contain(p => p.LevelId == 3 && p.Margin == 5500);
    }

    [Fact]
    public async Task DeleteProductAsync_WithValidId_ReturnsTrue()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "To Delete",
            Code = "DEL"
        };

        await _service.CreateProductAsync(product, null);

        // Act
        var result = await _service.DeleteProductAsync(product.Id);

        // Assert
        result.Should().BeTrue();
        var deletedProduct = await _context.Products.FindAsync(product.Id);
        deletedProduct.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProductAsync_WithNonExistentId_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteProductAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetProductByIdAsync_WithValidId_ReturnsProduct()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TEST"
        };

        await _service.CreateProductAsync(product, null);

        // Act
        var result = await _service.GetProductByIdAsync(product.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Name.Should().Be("Test Product");
        result.Category.Should().NotBeNull();
        result.Category.Name.Should().Be("Pulsa");
    }

    [Fact]
    public async Task GetProductByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _service.GetProductByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Product Pricing Tests

    [Fact]
    public async Task UpdateProductPriceAsync_WithNewPrice_CreatesPriceEntry()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TEST"
        };

        await _service.CreateProductAsync(product, null);

        // Act
        var result = await _service.UpdateProductPriceAsync(product.Id, 1, 5600);

        // Assert
        result.Should().BeTrue();
        var price = await _context.ProductLevelPrices
            .FirstOrDefaultAsync(p => p.ProductId == product.Id && p.LevelId == 1);
        price.Should().NotBeNull();
        price!.Margin.Should().Be(5600);
    }

    [Fact]
    public async Task UpdateProductPriceAsync_WithExistingPrice_UpdatesPrice()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TEST"
        };

        var levelPrices = new List<ProductLevelPrice>
        {
            new() { ProductId = product.Id, LevelId = 1, Margin = 5600 }
        };

        await _service.CreateProductAsync(product, levelPrices);

        // Act
        var result = await _service.UpdateProductPriceAsync(product.Id, 1, 5700);

        // Assert
        result.Should().BeTrue();
        var price = await _context.ProductLevelPrices
            .FirstOrDefaultAsync(p => p.ProductId == product.Id && p.LevelId == 1);
        price.Should().NotBeNull();
        price!.Margin.Should().Be(5700);
    }

    [Fact]
    public async Task GetProductPricesAsync_ReturnsAllLevelPrices()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TEST"
        };

        var levelPrices = new List<ProductLevelPrice>
        {
            new() { ProductId = product.Id, LevelId = 1, Margin = 5600 },
            new() { ProductId = product.Id, LevelId = 2, Margin = 5550 },
            new() { ProductId = product.Id, LevelId = 3, Margin = 5500 }
        };

        await _service.CreateProductAsync(product, levelPrices);

        // Act
        var result = await _service.GetProductPricesAsync(product.Id);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(p => p.LevelId);
        result.Should().OnlyContain(p => p.Level != null);
    }

    #endregion

    #region Product Listing Tests

    [Fact]
    public async Task GetProductsPagedAsync_ReturnsPagedResults()
    {
        // Arrange
        for (int i = 1; i <= 25; i++)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = 1,
                Name = $"Product {i}",
                Code = $"PROD{i}",
                Denomination = i * 1000,
                IsActive = true
            };

            await _service.CreateProductAsync(product, null);
        }

        // Act
        var (products, totalFiltered, totalRecords) = await _service.GetProductsPagedAsync(
            page: 1,
            pageSize: 10
        );

        // Assert
        products.Should().HaveCount(10);
        totalFiltered.Should().Be(25);
        totalRecords.Should().Be(25);
    }

    [Fact]
    public async Task GetProductsPagedAsync_WithSearch_FiltersResults()
    {
        // Arrange
        var products = new List<Product>
        {
            new() { Id = Guid.NewGuid(), CategoryId = 1, Name = "Pulsa Telkomsel", Code = "TSEL" },
            new() { Id = Guid.NewGuid(), CategoryId = 1, Name = "Pulsa Indosat", Code = "ISAT" },
            new() { Id = Guid.NewGuid(), CategoryId = 2, Name = "Data Telkomsel", Code = "DTSEL" }
        };

        foreach (var product in products)
        {
            await _service.CreateProductAsync(product, null);
        }

        // Act
        var (result, totalFiltered, _) = await _service.GetProductsPagedAsync(
            page: 1,
            pageSize: 10,
            search: "Telkomsel"
        );

        // Assert
        result.Should().HaveCount(2);
        totalFiltered.Should().Be(2);
        result.Should().OnlyContain(p => p.Name.Contains("Telkomsel") || p.Code.Contains("Telkomsel"));
    }

    [Fact]
    public async Task GetProductsPagedAsync_WithCategoryFilter_FiltersByCategory()
    {
        // Arrange
        var pulsaProduct = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Pulsa Product",
            Code = "PULSA"
        };

        var dataProduct = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 2,
            Name = "Data Product",
            Code = "DATA"
        };

        await _service.CreateProductAsync(pulsaProduct, null);
        await _service.CreateProductAsync(dataProduct, null);

        // Act
        var (result, totalFiltered, _) = await _service.GetProductsPagedAsync(
            page: 1,
            pageSize: 10,
            categoryId: 1
        );

        // Assert
        result.Should().HaveCount(1);
        totalFiltered.Should().Be(1);
        result[0].CategoryId.Should().Be(1);
    }

    [Fact]
    public async Task GetProductsPagedAsync_WithIsActiveFilter_FiltersByStatus()
    {
        // Arrange
        var activeProduct = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Active Product",
            Code = "ACTIVE",
            IsActive = true
        };

        var inactiveProduct = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Inactive Product",
            Code = "INACTIVE",
            IsActive = false
        };

        await _service.CreateProductAsync(activeProduct, null);
        await _service.CreateProductAsync(inactiveProduct, null);

        // Act
        var (result, totalFiltered, _) = await _service.GetProductsPagedAsync(
            page: 1,
            pageSize: 10,
            isActive: true
        );

        // Assert
        result.Should().HaveCount(1);
        totalFiltered.Should().Be(1);
        result[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetProductsPagedAsync_WithSortColumn_SortsResults()
    {
        // Arrange
        var products = new List<Product>
        {
            new() { Id = Guid.NewGuid(), CategoryId = 1, Name = "Charlie", Code = "C" },
            new() { Id = Guid.NewGuid(), CategoryId = 1, Name = "Alpha", Code = "A" },
            new() { Id = Guid.NewGuid(), CategoryId = 1, Name = "Bravo", Code = "B" }
        };

        foreach (var product in products)
        {
            await _service.CreateProductAsync(product, null);
        }

        // Act - Ascending
        var (resultAsc, _, _) = await _service.GetProductsPagedAsync(
            page: 1,
            pageSize: 10,
            sortColumn: "name",
            sortDirection: "asc"
        );

        // Assert
        resultAsc[0].Name.Should().Be("Alpha");
        resultAsc[1].Name.Should().Be("Bravo");
        resultAsc[2].Name.Should().Be("Charlie");

        // Act - Descending
        var (resultDesc, _, _) = await _service.GetProductsPagedAsync(
            page: 1,
            pageSize: 10,
            sortColumn: "name",
            sortDirection: "desc"
        );

        // Assert
        resultDesc[0].Name.Should().Be("Charlie");
        resultDesc[1].Name.Should().Be("Bravo");
        resultDesc[2].Name.Should().Be("Alpha");
    }

    [Fact]
    public async Task GetProductsPagedAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 1; i <= 25; i++)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = 1,
                Name = $"Product {i:D2}",
                Code = $"PROD{i:D2}"
            };

            await _service.CreateProductAsync(product, null);
        }

        // Act - Page 2
        var (page2, _, _) = await _service.GetProductsPagedAsync(
            page: 2,
            pageSize: 10
        );

        // Assert
        page2.Should().HaveCount(10);
        page2[0].Name.Should().Be("Product 11");
        page2[9].Name.Should().Be("Product 20");

        // Act - Page 3
        var (page3, _, _) = await _service.GetProductsPagedAsync(
            page: 3,
            pageSize: 10
        );

        // Assert
        page3.Should().HaveCount(5);
        page3[0].Name.Should().Be("Product 21");
        page3[4].Name.Should().Be("Product 25");
    }

    #endregion

    #region Helper Methods Tests

    [Fact]
    public async Task GetCategoriesAsync_ReturnsActiveCategoriesOrdered()
    {
        // Arrange - Add inactive category
        var inactiveCategory = new ProductCategory
        {
            Id = 3,
            Name = "Inactive",
            Code = "INACTIVE",
            SortOrder = 3,
            IsActive = false
        };

        _context.ProductCategories.Add(inactiveCategory);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCategoriesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(c => c.IsActive);
        result.Should().BeInAscendingOrder(c => c.SortOrder);
    }

    [Fact]
    public async Task GetLevelsAsync_ReturnsAllLevelsOrdered()
    {
        // Act
        var result = await _service.GetLevelsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(l => l.Id);
        result.Should().Contain(l => l.Name == "Bronze");
        result.Should().Contain(l => l.Name == "Silver");
        result.Should().Contain(l => l.Name == "Gold");
    }

    #endregion
}
