using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Tests.Helpers;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Application.Services;

public class UserLevelServiceTests : IAsyncLifetime
{
    private TestDbContext _context = null!;
    private UserLevelService _service = null!;

    public async Task InitializeAsync()
    {
        _context = new TestDbContext();
        await _context.CleanupBeforeSeedAsync();
        await _context.SeedAsync();
        _service = new UserLevelService(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.CleanupBeforeSeedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetAllLevelsAsync_ReturnsAllLevels()
    {
        // Act
        var result = await _service.GetAllLevelsAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().BeInAscendingOrder(l => l.Id);
    }

    [Fact]
    public async Task GetLevelByIdAsync_WithValidId_ReturnsLevel()
    {
        // Arrange
        var level = await _context.UserLevels.FirstAsync();

        // Act
        var result = await _service.GetLevelByIdAsync(level.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(level.Id);
        result.Name.Should().Be(level.Name);
    }

    [Fact]
    public async Task GetLevelByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _service.GetLevelByIdAsync(99999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateLevelAsync_WithValidData_ReturnsCreatedLevel()
    {
        // Arrange
        var newLevel = new UserLevel
        {
            Name = "TestLevel",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 5.0m,
            Description = "Test level description",
            IsActive = true
        };

        // Act
        var result = await _service.CreateLevelAsync(newLevel);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("TestLevel");

        var savedLevel = await _context.UserLevels.FindAsync(result.Id);
        savedLevel.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateLevelAsync_WithDuplicateName_ReturnsNull()
    {
        // Arrange
        var existingLevel = await _context.UserLevels.FirstAsync();
        var duplicateLevel = new UserLevel
        {
            Name = existingLevel.Name,
            MarkupType = MarkupType.Percentage,
            MarkupValue = 5.0m
        };

        // Act
        var result = await _service.CreateLevelAsync(duplicateLevel);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateLevelAsync_WithValidData_ReturnsUpdatedLevel()
    {
        // Arrange
        var level = await _context.UserLevels.FirstAsync();
        level.Name = "UpdatedLevel";
        level.MarkupValue = 10.0m;

        // Act
        var result = await _service.UpdateLevelAsync(level);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("UpdatedLevel");
        result.MarkupValue.Should().Be(10.0m);
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateLevelAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var nonExistentLevel = new UserLevel
        {
            Id = 99999,
            Name = "NonExistent",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 5.0m
        };

        // Act
        var result = await _service.UpdateLevelAsync(nonExistentLevel);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateLevelAsync_WithDuplicateName_ReturnsNull()
    {
        // Arrange
        var levels = await _context.UserLevels.Take(2).ToListAsync();
        var firstLevel = levels[0];
        var secondLevelName = levels[1].Name;

        firstLevel.Name = secondLevelName;

        // Act
        var result = await _service.UpdateLevelAsync(firstLevel);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteLevelAsync_WithValidId_ReturnsTrue()
    {
        // Arrange
        var newLevel = new UserLevel
        {
            Name = "ToDelete",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 5.0m,
            IsActive = true
        };
        _context.UserLevels.Add(newLevel);
        await _context.SaveChangesAsync();
        var levelId = newLevel.Id;

        // Act
        var result = await _service.DeleteLevelAsync(levelId);

        // Assert
        result.Should().BeTrue();

        var deletedLevel = await _context.UserLevels.FindAsync(levelId);
        deletedLevel.Should().BeNull();
    }

    [Fact]
    public async Task DeleteLevelAsync_WithInvalidId_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteLevelAsync(99999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteLevelAsync_WithUsersUsingLevel_ReturnsFalse()
    {
        // Arrange - Use a level that has users (from seeded data)
        var levelWithUsers = await _context.UserLevels
            .Include(l => l.Users)
            .FirstAsync(l => l.Users.Any());

        // Act
        var result = await _service.DeleteLevelAsync(levelWithUsers.Id);

        // Assert
        result.Should().BeFalse();

        // Verify level still exists
        var level = await _context.UserLevels.FindAsync(levelWithUsers.Id);
        level.Should().NotBeNull();
    }

    [Fact]
    public async Task GetLevelsPagedAsync_ReturnsPagedResults()
    {
        // Act
        var (levels, totalFiltered, totalRecords) = await _service.GetLevelsPagedAsync(1, 10);

        // Assert
        levels.Should().NotBeEmpty();
        totalRecords.Should().BeGreaterThan(0);
        totalFiltered.Should().Be(totalRecords);
        levels.Count.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task GetLevelsPagedAsync_WithSearchFilter_ReturnsFilteredResults()
    {
        // Arrange
        var levelToFind = await _context.UserLevels.FirstAsync();

        // Act
        var (levels, totalFiltered, totalRecords) = await _service.GetLevelsPagedAsync(
            1, 10, search: levelToFind.Name.Substring(0, 3));

        // Assert
        levels.Should().NotBeEmpty();
        totalFiltered.Should().BeGreaterThan(0);
        totalFiltered.Should().BeLessThanOrEqualTo(totalRecords);
    }

    [Fact]
    public async Task GetLevelsPagedAsync_WithIsActiveFilter_ReturnsOnlyActiveLevels()
    {
        // Act
        var (activeLevels, _, _) = await _service.GetLevelsPagedAsync(1, 10, isActive: true);
        var (inactiveLevels, _, _) = await _service.GetLevelsPagedAsync(1, 10, isActive: false);

        // Assert
        activeLevels.Should().OnlyContain(l => l.IsActive);
        if (inactiveLevels.Any())
        {
            inactiveLevels.Should().OnlyContain(l => !l.IsActive);
        }
    }

    [Fact]
    public async Task GetLevelsPagedAsync_WithPaging_SkipsCorrectRecords()
    {
        // Act
        var (page1, _, _) = await _service.GetLevelsPagedAsync(1, 2);
        var (page2, _, _) = await _service.GetLevelsPagedAsync(2, 2);

        // Assert
        page1.Count.Should().BeLessThanOrEqualTo(2);
        page2.Count.Should().BeLessThanOrEqualTo(2);

        // Verify no duplicate IDs between pages
        var page1Ids = page1.Select(l => l.Id).ToHashSet();
        var page2Ids = page2.Select(l => l.Id).ToHashSet();
        page1Ids.IntersectWith(page2Ids);
        page1Ids.Should().BeEmpty();
    }
}
