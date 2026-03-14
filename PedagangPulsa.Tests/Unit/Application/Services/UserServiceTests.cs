using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Tests.Helpers;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Application.Services;

public class UserServiceTests : IAsyncLifetime
{
    private TestDbContext _context = null!;
    private UserService _userService = null!;
    private UserLevelService _userLevelService = null!;
    private ReferralService _referralService = null!;

    public async Task InitializeAsync()
    {
        _context = new TestDbContext();
        _userService = new UserService(_context);
        _userLevelService = new UserLevelService(_context);
        _referralService = new ReferralService(_context, new Microsoft.Extensions.Logging.Abstractions.NullLogger<ReferralService>());

        // Seed required data
        await SeedDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    private async Task SeedDataAsync()
    {
        // Add user levels
        var levels = new List<UserLevel>
        {
            new() { Id = 1, Name = "Regular", MarkupType = MarkupType.Percentage, MarkupValue = 0, IsActive = true },
            new() { Id = 2, Name = "Bronze", MarkupType = MarkupType.Percentage, MarkupValue = 2, IsActive = true },
            new() { Id = 3, Name = "Silver", MarkupType = MarkupType.Percentage, MarkupValue = 1.5m, IsActive = true },
            new() { Id = 4, Name = "Gold", MarkupType = MarkupType.Percentage, MarkupValue = 1, IsActive = true }
        };

        _context.UserLevels.AddRange(levels);
        await _context.SaveChangesAsync();
    }

    #region User CRUD Tests

    [Fact]
    public async Task GetUserByIdAsync_WithValidId_ReturnsUserWithIncludes()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            FullName = "Test User",
            Email = "test@example.com",
            Phone = "08123456789",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var balance = new UserBalance
        {
            UserId = user.Id,
            ActiveBalance = 50000,
            HeldBalance = 0
        };

        _context.Users.Add(user);
        _context.UserBalances.Add(balance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.GetUserByIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Username.Should().Be("testuser");
        result.Level.Should().NotBeNull();
        result.Balance.Should().NotBeNull();
        result.Balance.ActiveBalance.Should().Be(50000);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _userService.GetUserByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUserLevelAsync_WithValidData_UpdatesLevel()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.UpdateUserLevelAsync(user.Id, 3, null, "Level upgrade");

        // Assert
        result.Should().BeTrue();

        var updatedUser = await _context.Users.FindAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.LevelId.Should().Be(3);
    }

    [Fact]
    public async Task UpdateUserLevelAsync_WithInvalidId_ReturnsFalse()
    {
        // Act
        var result = await _userService.UpdateUserLevelAsync(Guid.NewGuid(), 2, null, "Test");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SuspendUserAsync_WithValidId_SuspendsUser()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.SuspendUserAsync(user.Id, "Violation", null);

        // Assert
        result.Should().BeTrue();

        var suspendedUser = await _context.Users.FindAsync(user.Id);
        suspendedUser.Should().NotBeNull();
        suspendedUser!.Status.Should().Be(UserStatus.Suspended);
    }

    [Fact]
    public async Task UnsuspendUserAsync_WithValidId_ActivatesUser()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Suspended
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.UnsuspendUserAsync(user.Id, null);

        // Assert
        result.Should().BeTrue();

        var activatedUser = await _context.Users.FindAsync(user.Id);
        activatedUser.Should().NotBeNull();
        activatedUser!.Status.Should().Be(UserStatus.Active);
    }

    #endregion

    #region User Level Tests

    [Fact]
    public async Task CreateLevelAsync_WithValidData_CreatesLevel()
    {
        // Arrange
        var level = new UserLevel
        {
            Name = "Platinum",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 0.5m,
            CanTransfer = true,
            IsActive = true
        };

        // Act
        var result = await _userLevelService.CreateLevelAsync(level);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Platinum");
        result.MarkupValue.Should().Be(0.5m);
    }

    [Fact]
    public async Task CreateLevelAsync_WithDuplicateName_ReturnsNull()
    {
        // Arrange
        var existingLevel = new UserLevel
        {
            Name = "Bronze",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 2,
            IsActive = true
        };

        await _userLevelService.CreateLevelAsync(existingLevel);

        var newLevel = new UserLevel
        {
            Name = "Bronze", // Duplicate
            MarkupType = MarkupType.Percentage,
            MarkupValue = 3,
            IsActive = true
        };

        // Act
        var result = await _userLevelService.CreateLevelAsync(newLevel);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateLevelAsync_WithValidData_UpdatesLevel()
    {
        // Arrange
        var level = new UserLevel
        {
            Name = "Test Level",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 5,
            IsActive = true
        };

        var created = await _userLevelService.CreateLevelAsync(level);

        created.MarkupValue = 3;
        created.CanTransfer = false;
        created.Description = "Updated";

        // Act
        var result = await _userLevelService.UpdateLevelAsync(created);

        // Assert
        result.Should().NotBeNull();
        result.MarkupValue.Should().Be(3);
        result.CanTransfer.Should().BeFalse();
        result.Description.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateLevelAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var level = new UserLevel { Id = 9999, Name = "Non-existent" };

        // Act
        var result = await _userLevelService.UpdateLevelAsync(level);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteLevelAsync_WithValidId_DeletesLevel()
    {
        // Arrange
        var level = new UserLevel
        {
            Name = "To Delete",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 5,
            IsActive = true
        };

        var created = await _userLevelService.CreateLevelAsync(level);

        // Act
        var result = await _userLevelService.DeleteLevelAsync(created.Id);

        // Assert
        result.Should().BeTrue();

        var deleted = await _context.UserLevels.FindAsync(created.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteLevelAsync_WithUsers_ReturnsFalse()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act - Try to delete level that has users
        var result = await _userLevelService.DeleteLevelAsync(1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetLevelByIdAsync_WithValidId_ReturnsLevel()
    {
        // Act
        var result = await _userLevelService.GetLevelByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Regular");
    }

    [Fact]
    public async Task GetAllLevelsAsync_ReturnsAllLevels()
    {
        // Act
        var result = await _userLevelService.GetAllLevelsAsync();

        // Assert
        result.Should().HaveCount(4);
        result.Should().BeInAscendingOrder(l => l.Id);
    }

    #endregion

    #region User Paging Tests

    [Fact]
    public async Task GetUsersPagedAsync_ReturnsPagedResults()
    {
        // Arrange
        for (int i = 1; i <= 25; i++)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = $"user{i}",
                PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
                LevelId = 1,
                Status = UserStatus.Active
            };

            var balance = new UserBalance
            {
                UserId = user.Id,
                ActiveBalance = i * 1000,
                HeldBalance = 0
            };

            _context.Users.Add(user);
            _context.UserBalances.Add(balance);
        }

        await _context.SaveChangesAsync();

        // Act
        var (users, totalFiltered, totalRecords) = await _userService.GetUsersPagedAsync(1, 10);

        // Assert
        users.Should().HaveCount(10);
        totalFiltered.Should().Be(25);
        totalRecords.Should().Be(25);
    }

    [Fact]
    public async Task GetUsersPagedAsync_WithSearch_FiltersResults()
    {
        // Arrange
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Username = "john_doe",
            FullName = "John Doe",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active
        };

        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Username = "jane_smith",
            FullName = "Jane Smith",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active
        };

        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        // Act
        var (users, totalFiltered, _) = await _userService.GetUsersPagedAsync(1, 10, search: "john");

        // Assert
        users.Should().HaveCount(1);
        users[0].Username.Should().Contain("john");
        totalFiltered.Should().Be(1);
    }

    [Fact]
    public async Task GetUsersPagedAsync_WithLevelFilter_FiltersByLevel()
    {
        // Arrange
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active
        };

        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Username = "user2",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 2,
            Status = UserStatus.Active
        };

        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        // Act
        var (users, totalFiltered, _) = await _userService.GetUsersPagedAsync(1, 10, levelId: 2);

        // Assert
        users.Should().HaveCount(1);
        users[0].LevelId.Should().Be(2);
        totalFiltered.Should().Be(1);
    }

    [Fact]
    public async Task GetUsersPagedAsync_WithStatusFilter_FiltersByStatus()
    {
        // Arrange
        var active = new User
        {
            Id = Guid.NewGuid(),
            Username = "active_user",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active
        };

        var suspended = new User
        {
            Id = Guid.NewGuid(),
            Username = "suspended_user",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Suspended
        };

        _context.Users.AddRange(active, suspended);
        await _context.SaveChangesAsync();

        // Act
        var (users, totalFiltered, _) = await _userService.GetUsersPagedAsync(1, 10, status: "suspended");

        // Assert
        users.Should().HaveCount(1);
        users[0].Status.Should().Be(UserStatus.Suspended);
        totalFiltered.Should().Be(1);
    }

    #endregion

    #region Referral System Tests

    [Fact]
    public async Task PayPendingBonusAsync_WithValidLog_CreditsBalance()
    {
        // Arrange
        var referrer = new User
        {
            Id = Guid.NewGuid(),
            Username = "referrer",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active
        };

        var referee = new User
        {
            Id = Guid.NewGuid(),
            Username = "referee",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active,
            ReferredBy = referrer.Id
        };

        var referrerBalance = new UserBalance
        {
            UserId = referrer.Id,
            ActiveBalance = 100000,
            HeldBalance = 0
        };

        var refereeBalance = new UserBalance
        {
            UserId = referee.Id,
            ActiveBalance = 50000,
            HeldBalance = 0
        };

        var referralLog = new ReferralLog
        {
            Id = Guid.NewGuid(),
            ReferrerId = referrer.Id,
            RefereeId = referee.Id,
            BonusStatus = ReferralBonusStatus.Pending,
            BonusAmount = 5000,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(referrer, referee);
        _context.UserBalances.AddRange(referrerBalance, refereeBalance);
        _context.ReferralLogs.Add(referralLog);
        await _context.SaveChangesAsync();

        // Act
        var result = await _referralService.PayPendingBonusAsync(referralLog.Id, referrer.Id.ToString());

        // Assert
        result.Should().BeTrue();

        var updatedReferrer = await _context.Users
            .Include(u => u.Balance)
            .FirstOrDefaultAsync(u => u.Id == referrer.Id);

        updatedReferrer.Should().NotBeNull();
        updatedReferrer!.Balance.ActiveBalance.Should().Be(105000);

        var updatedLog = await _context.ReferralLogs.FindAsync(referralLog.Id);
        updatedLog.Should().NotBeNull();
        updatedLog!.BonusStatus.Should().Be(ReferralBonusStatus.Paid);
        updatedLog.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelReferralBonusAsync_WithValidLog_CancelsBonus()
    {
        // Arrange
        var referrer = new User
        {
            Id = Guid.NewGuid(),
            Username = "referrer",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active
        };

        var referee = new User
        {
            Id = Guid.NewGuid(),
            Username = "referee",
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
            LevelId = 1,
            Status = UserStatus.Active,
            ReferredBy = referrer.Id
        };

        var referrerBalance = new UserBalance
        {
            UserId = referrer.Id,
            ActiveBalance = 100000,
            HeldBalance = 0
        };

        var refereeBalance = new UserBalance
        {
            UserId = referee.Id,
            ActiveBalance = 50000,
            HeldBalance = 0
        };

        var referralLog = new ReferralLog
        {
            Id = Guid.NewGuid(),
            ReferrerId = referrer.Id,
            RefereeId = referee.Id,
            BonusStatus = ReferralBonusStatus.Pending,
            BonusAmount = 5000,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(referrer, referee);
        _context.UserBalances.AddRange(referrerBalance, refereeBalance);
        _context.ReferralLogs.Add(referralLog);
        await _context.SaveChangesAsync();

        // Act
        var result = await _referralService.CancelReferralBonusAsync(referralLog.Id, "User cancelled", referrer.Id.ToString());

        // Assert
        result.Should().BeTrue();

        var updatedLog = await _context.ReferralLogs.FindAsync(referralLog.Id);
        updatedLog.Should().NotBeNull();
        updatedLog!.BonusStatus.Should().Be(ReferralBonusStatus.Cancelled);
        updatedLog.CancelledAt.Should().NotBeNull();
        updatedLog.CancellationReason.Should().Be("User cancelled");

        // Balance should be unchanged
        var referrerWithBalance = await _context.Users
            .Include(u => u.Balance)
            .FirstOrDefaultAsync(u => u.Id == referrer.Id);

        referrerWithBalance.Should().NotBeNull();
        referrerWithBalance!.Balance.ActiveBalance.Should().Be(100000); // Unchanged
    }

    #endregion
}
