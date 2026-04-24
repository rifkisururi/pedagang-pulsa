using FluentAssertions;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Tests.Helpers;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Application.Services;

public class SupplierRegexPatternServiceTests : IAsyncLifetime
{
    private TestDbContext _context = null!;
    private SupplierRegexPatternService _service = null!;

    public async Task InitializeAsync()
    {
        _context = new TestDbContext();
        _service = new SupplierRegexPatternService(_context);
        await _context.CleanupBeforeSeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CleanupBeforeSeedAsync();
        await _context.DisposeAsync();
    }

    #region Create Tests

    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsPattern()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        var pattern = new SupplierRegexPattern
        {
            SupplierId = supplier.Id,
            SeqNo = 1,
            IsTrxSukses = true,
            Label = "Transaksi Sukses",
            Regex = @"SUKSES.*?(?<sn>\d+)",
            SampleMessage = "SUKSES ref123 sn456789"
        };

        // Act
        var result = await _service.CreateAsync(pattern);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result!.Label.Should().Be("Transaksi Sukses");
        result!.Regex.Should().Be(@"SUKSES.*?(?<sn>\d+)");
        result!.SupplierId.Should().Be(supplier.Id);
        result!.SeqNo.Should().Be(1);
        result!.IsActive.Should().BeTrue();

        var saved = await _context.SupplierRegexPatterns.FindAsync(result.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSeqNo_ReturnsNull()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        var pattern1 = new SupplierRegexPattern
        {
            SupplierId = supplier.Id,
            SeqNo = 1,
            Label = "Pattern 1",
            Regex = @"test1"
        };
        await _service.CreateAsync(pattern1);

        var pattern2 = new SupplierRegexPattern
        {
            SupplierId = supplier.Id,
            SeqNo = 1, // Duplicate SeqNo for same supplier
            Label = "Pattern 2",
            Regex = @"test2"
        };

        // Act
        var result = await _service.CreateAsync(pattern2);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_SameSeqNoDifferentSupplier_ReturnsPattern()
    {
        // Arrange
        var supplier1 = new Supplier { Name = "Supplier 1", Code = "S1" };
        var supplier2 = new Supplier { Name = "Supplier 2", Code = "S2" };
        _context.Suppliers.AddRange(supplier1, supplier2);
        await _context.SaveChangesAsync();

        var pattern1 = new SupplierRegexPattern
        {
            SupplierId = supplier1.Id,
            SeqNo = 1,
            Label = "Pattern S1",
            Regex = @"test1"
        };
        await _service.CreateAsync(pattern1);

        var pattern2 = new SupplierRegexPattern
        {
            SupplierId = supplier2.Id,
            SeqNo = 1, // Same SeqNo but different supplier - should be allowed
            Label = "Pattern S2",
            Regex = @"test2"
        };

        // Act
        var result = await _service.CreateAsync(pattern2);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Read Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsPatternWithSupplier()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        var pattern = new SupplierRegexPattern
        {
            SupplierId = supplier.Id,
            SeqNo = 1,
            Label = "Test Pattern",
            Regex = @"test"
        };
        await _service.CreateAsync(pattern);

        // Act
        var result = await _service.GetByIdAsync(pattern.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Label.Should().Be("Test Pattern");
        result.Supplier.Should().NotBeNull();
        result.Supplier.Name.Should().Be("Test Supplier");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _service.GetByIdAsync(9999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySupplierAsync_ReturnsPatternsOrderedBySeqNo()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        var pattern3 = new SupplierRegexPattern { SupplierId = supplier.Id, SeqNo = 3, Label = "Third", Regex = @"c" };
        var pattern1 = new SupplierRegexPattern { SupplierId = supplier.Id, SeqNo = 1, Label = "First", Regex = @"a" };
        var pattern2 = new SupplierRegexPattern { SupplierId = supplier.Id, SeqNo = 2, Label = "Second", Regex = @"b" };

        await _service.CreateAsync(pattern3);
        await _service.CreateAsync(pattern1);
        await _service.CreateAsync(pattern2);

        // Act
        var result = await _service.GetBySupplierAsync(supplier.Id);

        // Assert
        result.Should().HaveCount(3);
        result[0].SeqNo.Should().Be(1);
        result[1].SeqNo.Should().Be(2);
        result[2].SeqNo.Should().Be(3);
    }

    [Fact]
    public async Task GetBySupplierAsync_WithNoPatterns_ReturnsEmptyList()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetBySupplierAsync(supplier.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedResults()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        for (int i = 1; i <= 15; i++)
        {
            var pattern = new SupplierRegexPattern
            {
                SupplierId = supplier.Id,
                SeqNo = i,
                Label = $"Pattern {i}",
                Regex = $@"pattern{i}"
            };
            await _service.CreateAsync(pattern);
        }

        // Act
        var (patterns, totalFiltered, totalRecords) = await _service.GetPagedAsync(1, 10);

        // Assert
        patterns.Should().HaveCount(10);
        totalFiltered.Should().Be(15);
        totalRecords.Should().Be(15);
    }

    [Fact]
    public async Task GetPagedAsync_WithSearch_FiltersByLabelAndRegex()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        var pattern1 = new SupplierRegexPattern { SupplierId = supplier.Id, SeqNo = 1, Label = "Sukses", Regex = @"SUKSES" };
        var pattern2 = new SupplierRegexPattern { SupplierId = supplier.Id, SeqNo = 2, Label = "Gagal", Regex = @"GAGAL" };
        var pattern3 = new SupplierRegexPattern { SupplierId = supplier.Id, SeqNo = 3, Label = "Pending", Regex = @"PENDING" };

        await _service.CreateAsync(pattern1);
        await _service.CreateAsync(pattern2);
        await _service.CreateAsync(pattern3);

        // Act
        var (patterns, totalFiltered, _) = await _service.GetPagedAsync(1, 10, search: "suk");

        // Assert
        patterns.Should().HaveCount(1);
        totalFiltered.Should().Be(1);
        patterns[0].Label.Should().Be("Sukses");
    }

    [Fact]
    public async Task GetPagedAsync_WithSupplierIdFilter_FiltersBySupplier()
    {
        // Arrange
        var supplier1 = new Supplier { Name = "Supplier 1", Code = "S1" };
        var supplier2 = new Supplier { Name = "Supplier 2", Code = "S2" };
        _context.Suppliers.AddRange(supplier1, supplier2);
        await _context.SaveChangesAsync();

        await _service.CreateAsync(new SupplierRegexPattern { SupplierId = supplier1.Id, SeqNo = 1, Label = "P1", Regex = @"a" });
        await _service.CreateAsync(new SupplierRegexPattern { SupplierId = supplier1.Id, SeqNo = 2, Label = "P2", Regex = @"b" });
        await _service.CreateAsync(new SupplierRegexPattern { SupplierId = supplier2.Id, SeqNo = 1, Label = "P3", Regex = @"c" });

        // Act
        var (patterns, totalFiltered, _) = await _service.GetPagedAsync(1, 10, supplierId: supplier1.Id);

        // Assert
        patterns.Should().HaveCount(2);
        totalFiltered.Should().Be(2);
        patterns.All(p => p.SupplierId == supplier1.Id).Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_WithIsTrxSuksesFilter_FiltersByStatus()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        await _service.CreateAsync(new SupplierRegexPattern { SupplierId = supplier.Id, SeqNo = 1, Label = "Sukses", Regex = @"ok", IsTrxSukses = true });
        await _service.CreateAsync(new SupplierRegexPattern { SupplierId = supplier.Id, SeqNo = 2, Label = "Gagal", Regex = @"fail", IsTrxSukses = false });
        await _service.CreateAsync(new SupplierRegexPattern { SupplierId = supplier.Id, SeqNo = 3, Label = "Timeout", Regex = @"timeout", IsTrxSukses = false });

        // Act
        var (patterns, totalFiltered, _) = await _service.GetPagedAsync(1, 10, isTrxSukses: false);

        // Assert
        patterns.Should().HaveCount(2);
        totalFiltered.Should().Be(2);
        patterns.All(p => p.IsTrxSukses == false).Should().BeTrue();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesPattern()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        var created = await _service.CreateAsync(new SupplierRegexPattern
        {
            SupplierId = supplier.Id,
            SeqNo = 1,
            Label = "Old Label",
            Regex = @"old_regex",
            IsTrxSukses = true,
            IsActive = true
        });

        created!.Label = "New Label";
        created!.Regex = @"new_regex";
        created!.IsTrxSukses = false;
        created!.IsActive = false;
        created!.SampleMessage = "Updated sample";

        // Act
        var result = await _service.UpdateAsync(created);

        // Assert
        result.Should().NotBeNull();
        result!.Label.Should().Be("New Label");
        result!.Regex.Should().Be(@"new_regex");
        result!.IsTrxSukses.Should().BeFalse();
        result!.IsActive.Should().BeFalse();
        result!.SampleMessage.Should().Be("Updated sample");
        result!.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var pattern = new SupplierRegexPattern
        {
            Id = 9999,
            SupplierId = 1,
            SeqNo = 1,
            Label = "Non-existent",
            Regex = @"test"
        };

        // Act
        var result = await _service.UpdateAsync(pattern);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithSeqNoConflict_ReturnsNull()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        var pattern1 = await _service.CreateAsync(new SupplierRegexPattern
        {
            SupplierId = supplier.Id,
            SeqNo = 1,
            Label = "Pattern 1",
            Regex = @"test1"
        });

        var pattern2 = await _service.CreateAsync(new SupplierRegexPattern
        {
            SupplierId = supplier.Id,
            SeqNo = 2,
            Label = "Pattern 2",
            Regex = @"test2"
        });

        // Try to update pattern2 to have same SeqNo as pattern1
        pattern2!.SeqNo = 1;

        // Act
        var result = await _service.UpdateAsync(pattern2);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_WithValidId_ReturnsTrue()
    {
        // Arrange
        var supplier = new Supplier { Name = "Test Supplier", Code = "TS" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        var created = await _service.CreateAsync(new SupplierRegexPattern
        {
            SupplierId = supplier.Id,
            SeqNo = 1,
            Label = "To Delete",
            Regex = @"delete"
        });

        // Act
        var result = await _service.DeleteAsync(created!.Id);

        // Assert
        result.Should().BeTrue();
        var deleted = await _context.SupplierRegexPatterns.FindAsync(created.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteAsync(9999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region TestRegex Tests

    [Fact]
    public void TestRegex_WithMatchingPattern_ReturnsMatch()
    {
        // Act
        var result = _service.TestRegex(@"SUKSES.*?sn=(?<sn>\d+)", "Transaksi SUKSES ref123 sn=456789");

        // Assert
        result.IsMatch.Should().BeTrue();
        result.Groups.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Groups.Should().Contain(g => g.Name == "sn" && g.Value == "456789");
    }

    [Fact]
    public void TestRegex_WithNoMatch_ReturnsNoMatch()
    {
        // Act
        var result = _service.TestRegex(@"SUKSES", "Transaksi GAGAL ref123");

        // Assert
        result.IsMatch.Should().BeFalse();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void TestRegex_WithInvalidPattern_ReturnsError()
    {
        // Act - unbalanced parentheses is invalid
        var result = _service.TestRegex(@"(?<name>test", "test message");

        // Assert
        result.IsMatch.Should().BeFalse();
        result.Message.Should().Contain("tidak valid");
    }

    [Theory]
    [InlineData(@"(?<product>\w+)\s+ke\s+(?<dest>\d+)", "pulsa ke 08123456789", "product", "pulsa")]
    [InlineData(@"(?<status>SUKSES|GAGAL)\s*ref=(?<ref>\w+)", "SUKSES ref=ABC123", "ref", "ABC123")]
    [InlineData(@"sn\s*[=:]\s*(?<sn>\d+)", "sn: 9876543210", "sn", "9876543210")]
    public void TestRegex_WithNamedGroups_ExtractsCorrectValues(string pattern, string message, string expectedGroup, string expectedValue)
    {
        // Act
        var result = _service.TestRegex(pattern, message);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.Groups.Should().Contain(g => g.Name == expectedGroup && g.Value == expectedValue);
    }

    #endregion
}
