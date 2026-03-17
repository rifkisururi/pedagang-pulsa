using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Tests.Helpers;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Application.Services;

public class SupplierServiceTests : IAsyncLifetime
{
    private TestDbContext _context = null!;
    private SupplierService _supplierService = null!;
    private SupplierProductService _supplierProductService = null!;
    private SupplierBalanceService _supplierBalanceService = null!;

    public async Task InitializeAsync()
    {
        _context = new TestDbContext();
        _supplierService = new SupplierService(_context);
        _supplierProductService = new SupplierProductService(_context);
        _supplierBalanceService = new SupplierBalanceService(_context, new Microsoft.Extensions.Logging.Abstractions.NullLogger<SupplierBalanceService>());

        // Clean up database before seeding to avoid primary key conflicts
        await _context.CleanupBeforeSeedAsync();

        // Seed ProductCategory (required for Product entity)
        var category = new ProductCategory
        {
            Id = 1,
            Name = "Pulsa",
            Code = "PULSA",
            SortOrder = 1,
            IsActive = true
        };
        _context.ProductCategories.Add(category);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean up test data after all tests
        await _context.CleanupBeforeSeedAsync();
        await _context.DisposeAsync();
    }

    #region Supplier CRUD Tests

    [Fact]
    public async Task CreateSupplierAsync_WithValidData_ReturnsSupplier()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Digiflazz",
            Code = "DIGI",
            ApiBaseUrl = "https://api.digiflazz.com",
            MemberId = "test_member_id",
            Pin = "test_pin",
            Password = "test_password",
            TimeoutSeconds = 30,
            IsActive = true
        };

        // Act
        var result = await _supplierService.CreateSupplierAsync(supplier);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Digiflazz");
        result.Code.Should().Be("DIGI");
        result.Id.Should().BeGreaterThan(0);

        var savedSupplier = await _context.Suppliers.FindAsync(result.Id);
        savedSupplier.Should().NotBeNull();
        savedSupplier!.Name.Should().Be("Digiflazz");
    }

    [Fact]
    public async Task CreateSupplierAsync_WithDuplicateName_ReturnsNull()
    {
        // Arrange
        var existingSupplier = new Supplier
        {
            Name = "Digiflazz",
            Code = "DIGI",
            ApiBaseUrl = "https://api.digiflazz.com"
        };

        await _supplierService.CreateSupplierAsync(existingSupplier);

        var newSupplier = new Supplier
        {
            Name = "Digiflazz", // Duplicate name
            Code = "DIGI2",
            ApiBaseUrl = "https://api.digiflazz.com"
        };

        // Act
        var result = await _supplierService.CreateSupplierAsync(newSupplier);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSupplierAsync_WithValidData_UpdatesSupplier()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Digiflazz",
            Code = "DIGI",
            ApiBaseUrl = "https://api.digiflazz.com",
            MemberId = "old_member_id",
            Pin = "old_pin",
            Password = "old_password",
            TimeoutSeconds = 30
        };

        var created = await _supplierService.CreateSupplierAsync(supplier);

        created.ApiBaseUrl = "https://api.new-digiflazz.com";
        created.MemberId = "new_member_id";
        created.Pin = "new_pin";
        created.Password = "new_password";
        created.TimeoutSeconds = 60;
        created.IsActive = false;

        // Act
        var result = await _supplierService.UpdateSupplierAsync(created);

        // Assert
        result.Should().NotBeNull();
        result.ApiBaseUrl.Should().Be("https://api.new-digiflazz.com");
        result.MemberId.Should().Be("new_member_id");
        result.TimeoutSeconds.Should().Be(60);
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSupplierAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var supplier = new Supplier
        {
            Id = 9999,
            Name = "Non-existent"
        };

        // Act
        var result = await _supplierService.UpdateSupplierAsync(supplier);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSupplierAsync_WithValidId_ReturnsTrue()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "To Delete",
            Code = "DEL"
        };

        var created = await _supplierService.CreateSupplierAsync(supplier);

        // Act
        var result = await _supplierService.DeleteSupplierAsync(created.Id);

        // Assert
        result.Should().BeTrue();

        var deletedSupplier = await _context.Suppliers.FindAsync(created.Id);
        deletedSupplier.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSupplierAsync_WithNonExistentId_ReturnsFalse()
    {
        // Act
        var result = await _supplierService.DeleteSupplierAsync(9999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSupplierAsync_WithAssociatedProducts_ReturnsFalse()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "With Products",
            Code = "WP"
        };

        var createdSupplier = await _supplierService.CreateSupplierAsync(supplier);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TEST"
        };

        _context.Products.Add(product);

        var supplierProduct = new SupplierProduct
        {
            ProductId = product.Id,
            SupplierId = createdSupplier.Id,
            SupplierProductCode = "SP1",
            CostPrice = 5000,
            Seq = 1
        };

        _context.SupplierProducts.Add(supplierProduct);
        await _context.SaveChangesAsync();

        // Act
        var result = await _supplierService.DeleteSupplierAsync(createdSupplier.Id);

        // Assert
        result.Should().BeFalse();

        var existingSupplier = await _context.Suppliers.FindAsync(createdSupplier.Id);
        existingSupplier.Should().NotBeNull(); // Should still exist
    }

    [Fact]
    public async Task GetSupplierByIdAsync_WithValidId_ReturnsSupplier()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Code = "TEST"
        };

        var created = await _supplierService.CreateSupplierAsync(supplier);

        // Act
        var result = await _supplierService.GetSupplierByIdAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Supplier");
    }

    [Fact]
    public async Task GetAllActiveSuppliersAsync_ReturnsOnlyActiveSuppliers()
    {
        // Arrange
        var active1 = new Supplier { Name = "Active 1", Code = "A1", IsActive = true };
        var active2 = new Supplier { Name = "Active 2", Code = "A2", IsActive = true };
        var inactive = new Supplier { Name = "Inactive", Code = "I1", IsActive = false };

        await _supplierService.CreateSupplierAsync(active1);
        await _supplierService.CreateSupplierAsync(active2);
        await _supplierService.CreateSupplierAsync(inactive);

        // Act
        var result = await _supplierService.GetAllActiveSuppliersAsync();

        // Assert
        result.Should().HaveCount(2);
        result.All(s => s.IsActive).Should().BeTrue();
    }

    #endregion

    #region Supplier-Product Mapping Tests

    [Fact]
    public async Task AddSupplierProductAsync_WithValidData_ReturnsSupplierProduct()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Code = "TS"
        };

        var createdSupplier = await _supplierService.CreateSupplierAsync(supplier);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TP"
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var supplierProduct = new SupplierProduct
        {
            ProductId = product.Id,
            SupplierId = createdSupplier.Id,
            SupplierProductCode = "SP1",
            CostPrice = 5500,
            Seq = 1,
            IsActive = true
        };

        // Act
        var result = await _supplierProductService.AddSupplierProductAsync(supplierProduct);

        // Assert
        result.Should().NotBeNull();
        result.SupplierProductCode.Should().Be("SP1");
        result.CostPrice.Should().Be(5500);
    }

    [Fact]
    public async Task AddSupplierProductAsync_WithDuplicateMapping_ReturnsNull()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        var createdSupplier = await _supplierService.CreateSupplierAsync(supplier);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TP"
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var supplierProduct1 = new SupplierProduct
        {
            ProductId = product.Id,
            SupplierId = createdSupplier.Id,
            SupplierProductCode = "SP1",
            CostPrice = 5500,
            Seq = 1
        };

        await _supplierProductService.AddSupplierProductAsync(supplierProduct1);

        var supplierProduct2 = new SupplierProduct
        {
            ProductId = product.Id,
            SupplierId = createdSupplier.Id, // Same combination
            SupplierProductCode = "SP2",
            CostPrice = 5600,
            Seq = 2
        };

        // Act
        var result = await _supplierProductService.AddSupplierProductAsync(supplierProduct2);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSupplierProductsByProductIdAsync_ReturnsOrderedBySeq()
    {
        // Arrange
        var supplier1 = new Supplier { Name = "Supplier 1", Code = "S1" };
        var supplier2 = new Supplier { Name = "Supplier 2", Code = "S2" };
        var supplier3 = new Supplier { Name = "Supplier 3", Code = "S3" };

        await _supplierService.CreateSupplierAsync(supplier1);
        await _supplierService.CreateSupplierAsync(supplier2);
        await _supplierService.CreateSupplierAsync(supplier3);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TP"
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var sp1 = new SupplierProduct { ProductId = product.Id, SupplierId = supplier1.Id, SupplierProductCode = "SP1", CostPrice = 5500, Seq = 2 };
        var sp2 = new SupplierProduct { ProductId = product.Id, SupplierId = supplier2.Id, SupplierProductCode = "SP2", CostPrice = 5600, Seq = 1 };
        var sp3 = new SupplierProduct { ProductId = product.Id, SupplierId = supplier3.Id, SupplierProductCode = "SP3", CostPrice = 5700, Seq = 3 };

        await _supplierProductService.AddSupplierProductAsync(sp1);
        await _supplierProductService.AddSupplierProductAsync(sp2);
        await _supplierProductService.AddSupplierProductAsync(sp3);

        // Act
        var result = await _supplierProductService.GetSupplierProductsByProductIdAsync(product.Id);

        // Assert
        result.Should().HaveCount(3);
        result[0].Seq.Should().Be(1); // supplier2
        result[1].Seq.Should().Be(2); // supplier1
        result[2].Seq.Should().Be(3); // supplier3
    }

    [Fact]
    public async Task UpdateSupplierProductAsync_WithValidData_UpdatesMapping()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        var createdSupplier = await _supplierService.CreateSupplierAsync(supplier);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TP"
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var supplierProduct = new SupplierProduct
        {
            ProductId = product.Id,
            SupplierId = createdSupplier.Id,
            SupplierProductCode = "SP1",
            CostPrice = 5500,
            Seq = 1
        };

        await _supplierProductService.AddSupplierProductAsync(supplierProduct);

        // Update
        supplierProduct.SupplierProductCode = "SP1-UPDATED";
        supplierProduct.CostPrice = 5800;
        supplierProduct.Seq = 2;
        supplierProduct.IsActive = false;

        // Act
        var result = await _supplierProductService.UpdateSupplierProductAsync(supplierProduct);

        // Assert
        result.Should().NotBeNull();
        result.SupplierProductCode.Should().Be("SP1-UPDATED");
        result.CostPrice.Should().Be(5800);
        result.Seq.Should().Be(2);
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSupplierProductAsync_WithValidIds_ReturnsTrue()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        var createdSupplier = await _supplierService.CreateSupplierAsync(supplier);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TP"
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var supplierProduct = new SupplierProduct
        {
            ProductId = product.Id,
            SupplierId = createdSupplier.Id,
            SupplierProductCode = "SP1",
            CostPrice = 5500,
            Seq = 1
        };

        await _supplierProductService.AddSupplierProductAsync(supplierProduct);

        // Act
        var result = await _supplierProductService.DeleteSupplierProductAsync(product.Id, createdSupplier.Id);

        // Assert
        result.Should().BeTrue();

        var deleted = await _context.SupplierProducts
            .FirstOrDefaultAsync(sp => sp.ProductId == product.Id && sp.SupplierId == createdSupplier.Id);

        deleted.Should().BeNull();
    }

    [Fact]
    public async Task ReorderSupplierProductsAsync_WithValidList_ReordersCorrectly()
    {
        // Arrange
        var supplier1 = new Supplier { Name = "Supplier 1", Code = "S1" };
        var supplier2 = new Supplier { Name = "Supplier 2", Code = "S2" };
        var supplier3 = new Supplier { Name = "Supplier 3", Code = "S3" };

        await _supplierService.CreateSupplierAsync(supplier1);
        await _supplierService.CreateSupplierAsync(supplier2);
        await _supplierService.CreateSupplierAsync(supplier3);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TP"
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var sp1 = new SupplierProduct { ProductId = product.Id, SupplierId = supplier1.Id, SupplierProductCode = "SP1", CostPrice = 5500, Seq = 1 };
        var sp2 = new SupplierProduct { ProductId = product.Id, SupplierId = supplier2.Id, SupplierProductCode = "SP2", CostPrice = 5600, Seq = 2 };
        var sp3 = new SupplierProduct { ProductId = product.Id, SupplierId = supplier3.Id, SupplierProductCode = "SP3", CostPrice = 5700, Seq = 3 };

        await _supplierProductService.AddSupplierProductAsync(sp1);
        await _supplierProductService.AddSupplierProductAsync(sp2);
        await _supplierProductService.AddSupplierProductAsync(sp3);

        var reorderList = new List<SupplierProductReorder>
        {
            new() { SupplierId = supplier3.Id, NewSequence = 1 },
            new() { SupplierId = supplier1.Id, NewSequence = 2 },
            new() { SupplierId = supplier2.Id, NewSequence = 3 }
        };

        // Act
        await _supplierProductService.ReorderSupplierProductsAsync(product.Id, reorderList);

        // Assert
        var result = await _supplierProductService.GetSupplierProductsByProductIdAsync(product.Id);

        result.Should().HaveCount(3);
        result[0].SupplierId.Should().Be(supplier3.Id);
        result[0].Seq.Should().Be(1);
        result[1].SupplierId.Should().Be(supplier1.Id);
        result[1].Seq.Should().Be(2);
        result[2].SupplierId.Should().Be(supplier2.Id);
        result[2].Seq.Should().Be(3);
    }

    [Fact]
    public async Task GetAvailableSuppliersForProductAsync_ReturnsOnlySuppliersNotMapped()
    {
        // Arrange
        var supplier1 = new Supplier { Name = "Supplier 1", Code = "S1", IsActive = true };
        var supplier2 = new Supplier { Name = "Supplier 2", Code = "S2", IsActive = true };
        var supplier3 = new Supplier { Name = "Supplier 3", Code = "S3", IsActive = false }; // Inactive

        await _supplierService.CreateSupplierAsync(supplier1);
        await _supplierService.CreateSupplierAsync(supplier2);
        await _supplierService.CreateSupplierAsync(supplier3);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            Name = "Test Product",
            Code = "TP"
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Map supplier1 to product
        var sp1 = new SupplierProduct
        {
            ProductId = product.Id,
            SupplierId = supplier1.Id,
            SupplierProductCode = "SP1",
            CostPrice = 5500,
            Seq = 1
        };

        await _supplierProductService.AddSupplierProductAsync(sp1);

        // Act
        var result = await _supplierProductService.GetAvailableSuppliersForProductAsync(product.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(supplier2.Id);
        result[0].IsActive.Should().BeTrue();
    }

    #endregion

    #region Supplier Balance Tests

    [Fact]
    public async Task UpdateSupplierBalanceAsync_WithValidData_UpdatesBalance()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Code = "TS"
        };

        var created = await _supplierService.CreateSupplierAsync(supplier);

        // Act
        var result = await _supplierService.UpdateSupplierBalanceAsync(created.Id, 100000);

        // Assert
        result.Should().BeTrue();

        var updatedSupplier = await _context.Suppliers
            .Include(s => s.Balance)
            .FirstOrDefaultAsync(s => s.Id == created.Id);

        updatedSupplier.Should().NotBeNull();
        updatedSupplier!.Balance.Should().NotBeNull();
        updatedSupplier.Balance.ActiveBalance.Should().Be(100000);
    }

    [Fact]
    public async Task UpdateSupplierBalanceAsync_WithNonExistentId_ReturnsFalse()
    {
        // Act
        var result = await _supplierService.UpdateSupplierBalanceAsync(9999, 100000);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DepositToSupplierAsync_WithValidAmount_CreditsBalance()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Code = "TS"
        };

        var created = await _supplierService.CreateSupplierAsync(supplier);

        // Act
        var result = await _supplierBalanceService.DepositToSupplierAsync(
            created.Id,
            50000,
            "Initial deposit",
            "Test deposit",
            "admin");

        // Assert
        result.Should().BeTrue();

        var supplierWithBalance = await _supplierBalanceService.GetSupplierWithBalanceAsync(created.Id);
        supplierWithBalance.Should().NotBeNull();
        supplierWithBalance!.Balance.Should().NotBeNull();
        supplierWithBalance.Balance.ActiveBalance.Should().Be(50000);

        var ledger = await _context.SupplierBalanceLedgers
            .FirstOrDefaultAsync(l => l.SupplierId == created.Id);

        ledger.Should().NotBeNull();
        ledger!.Type.Should().Be("Deposit");
        ledger.Amount.Should().Be(50000);
        ledger.BalanceBefore.Should().Be(0);
        ledger.BalanceAfter.Should().Be(50000);
    }

    [Fact]
    public async Task DebitFromSupplierAsync_WithSufficientBalance_DebitsSuccessfully()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        var created = await _supplierService.CreateSupplierAsync(supplier);

        // First deposit
        await _supplierBalanceService.DepositToSupplierAsync(created.Id, 100000, "Initial deposit");

        // Act
        var result = await _supplierBalanceService.DebitFromSupplierAsync(
            created.Id,
            5000,
            "Transaction deduction");

        // Assert
        result.Should().BeTrue();

        var supplierWithBalance = await _supplierBalanceService.GetSupplierWithBalanceAsync(created.Id);
        supplierWithBalance!.Balance.ActiveBalance.Should().Be(95000);

        var ledgers = await _context.SupplierBalanceLedgers
            .Where(l => l.SupplierId == created.Id)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        ledgers.Should().HaveCount(2);
        ledgers[0].Type.Should().Be("Transaction");
        ledgers[0].Amount.Should().Be(-5000);
        ledgers[0].BalanceBefore.Should().Be(100000);
        ledgers[0].BalanceAfter.Should().Be(95000);
    }

    [Fact]
    public async Task DebitFromSupplierAsync_WithInsufficientBalance_ReturnsFalse()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        var created = await _supplierService.CreateSupplierAsync(supplier);

        await _supplierBalanceService.DepositToSupplierAsync(created.Id, 10000, "Initial deposit");

        // Act - Try to debit more than available
        var result = await _supplierBalanceService.DebitFromSupplierAsync(
            created.Id,
            15000, // More than balance
            "Transaction deduction");

        // Assert
        result.Should().BeFalse();

        var supplierWithBalance = await _supplierBalanceService.GetSupplierWithBalanceAsync(created.Id);
        supplierWithBalance!.Balance.ActiveBalance.Should().Be(10000); // Unchanged
    }

    [Fact]
    public async Task GetAllSuppliersWithBalanceAsync_ReturnsSuppliersWithBalances()
    {
        // Arrange
        var supplier1 = new Supplier { Name = "Supplier 1", Code = "S1" };
        var supplier2 = new Supplier { Name = "Supplier 2", Code = "S2" };

        await _supplierService.CreateSupplierAsync(supplier1);
        await _supplierService.CreateSupplierAsync(supplier2);

        await _supplierBalanceService.DepositToSupplierAsync(supplier1.Id, 100000, "Deposit 1");
        await _supplierBalanceService.DepositToSupplierAsync(supplier2.Id, 50000, "Deposit 2");

        // Act
        var result = await _supplierBalanceService.GetAllSuppliersWithBalanceAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Balance.Should().NotBeNull();
        result[1].Balance.Should().NotBeNull();
    }

    #endregion

    #region Supplier Paging Tests

    [Fact]
    public async Task GetSuppliersPagedAsync_ReturnsPagedResults()
    {
        // Arrange
        for (int i = 1; i <= 25; i++)
        {
            var supplier = new Supplier
            {
                Name = $"Supplier {i}",
                Code = $"S{i}",
                IsActive = true
            };

            await _supplierService.CreateSupplierAsync(supplier);
        }

        // Act
        var (suppliers, totalFiltered, totalRecords) = await _supplierService.GetSuppliersPagedAsync(1, 10);

        // Assert
        suppliers.Should().HaveCount(10);
        totalFiltered.Should().Be(25);
        totalRecords.Should().Be(25);
    }

    [Fact]
    public async Task GetSuppliersPagedAsync_WithSearch_FiltersResults()
    {
        // Arrange
        var supplier1 = new Supplier { Name = "Digiflazz", Code = "Digi", IsActive = true };
        var supplier2 = new Supplier { Name = "Apigames", Code = "API", IsActive = true };
        var supplier3 = new Supplier { Name = "Mobile Pulsa", Code = "MP", IsActive = true };

        await _supplierService.CreateSupplierAsync(supplier1);
        await _supplierService.CreateSupplierAsync(supplier2);
        await _supplierService.CreateSupplierAsync(supplier3);

        // Act
        var (suppliers, totalFiltered, _) = await _supplierService.GetSuppliersPagedAsync(1, 10, search: "Digi");

        // Assert
        suppliers.Should().HaveCount(1);
        totalFiltered.Should().Be(1);
        suppliers[0].Name.Should().Contain("Digi");
    }

    [Fact]
    public async Task GetSuppliersPagedAsync_WithIsActiveFilter_FiltersByStatus()
    {
        // Arrange
        var active1 = new Supplier { Name = "Active 1", Code = "A1", IsActive = true };
        var active2 = new Supplier { Name = "Active 2", Code = "A2", IsActive = true };
        var inactive = new Supplier { Name = "Inactive", Code = "I1", IsActive = false };

        await _supplierService.CreateSupplierAsync(active1);
        await _supplierService.CreateSupplierAsync(active2);
        await _supplierService.CreateSupplierAsync(inactive);

        // Act
        var (suppliers, totalFiltered, _) = await _supplierService.GetSuppliersPagedAsync(1, 10, isActive: false);

        // Assert
        suppliers.Should().HaveCount(1);
        totalFiltered.Should().Be(1);
        suppliers[0].IsActive.Should().BeFalse();
    }

    #endregion
}
