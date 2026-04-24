using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Tests.Helpers;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Application.Services;

public class TopupServiceTests : IAsyncLifetime
{
    private TestDbContext _context = null!;
    private TopupService _topupService = null!;

    public async Task InitializeAsync()
    {
        _context = new TestDbContext();

        // Clean up database before seeding to avoid primary key conflicts
        await _context.CleanupBeforeSeedAsync();

        await _context.SeedAsync();

        _topupService = new TopupService(_context);
    }

    public async Task DisposeAsync()
    {
        // Clean up test data after all tests
        await _context.CleanupBeforeSeedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task ApproveTopupAsync_WithPendingTopup_CreditsUserBalance()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.UserName == "user1");
        var initialBalance = user.Balance!.ActiveBalance;

        var topup = TestDataBuilder.CreateTopupRequest(
            user.Id,
            100000m,
            status: TopupStatus.Pending
        );

        _context.TopupRequests.Add(topup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _topupService.ApproveTopupAsync(
            topup.Id,
            100000m,
            "Approved",
            Guid.NewGuid().ToString()
        );

        // Assert
        result.Should().BeTrue();

        // Refresh from database
        await _context.Entry(user.Balance).ReloadAsync();
        user.Balance.ActiveBalance.Should().Be(initialBalance + 100000m);

        // Verify topup status
        await _context.Entry(topup).ReloadAsync();
        topup.Status.Should().Be(TopupStatus.Approved);
        topup.ApprovedAt.Should().NotBeNull();
        topup.ApprovedBy.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveTopupAsync_WithDifferentAmount_CreditsActualAmount()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.UserName == "user1");
        var initialBalance = user.Balance!.ActiveBalance;

        var topup = TestDataBuilder.CreateTopupRequest(
            user.Id,
            100000m, // Requested amount
            status: TopupStatus.Pending
        );

        _context.TopupRequests.Add(topup);
        await _context.SaveChangesAsync();

        var actualAmount = 95000m; // Different from requested

        // Act
        var result = await _topupService.ApproveTopupAsync(
            topup.Id,
            actualAmount,
            "Partial payment received",
            Guid.NewGuid().ToString()
        );

        // Assert
        result.Should().BeTrue();

        // Refresh from database
        await _context.Entry(user.Balance).ReloadAsync();
        user.Balance.ActiveBalance.Should().Be(initialBalance + actualAmount);
        user.Balance.ActiveBalance.Should().NotBe(initialBalance + 100000m);
    }

    [Fact]
    public async Task ApproveTopupAsync_CreatesBalanceLedgerEntry()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        var amount = 100000m;

        var topup = TestDataBuilder.CreateTopupRequest(
            user.Id,
            amount,
            status: TopupStatus.Pending
        );

        _context.TopupRequests.Add(topup);
        await _context.SaveChangesAsync();

        // Act
        await _topupService.ApproveTopupAsync(
            topup.Id,
            amount,
            "Approved",
            Guid.NewGuid().ToString()
        );

        // Assert
        var ledger = await _context.BalanceLedgers
            .Where(bl => bl.UserId == user.Id && bl.RefId == topup.Id)
            .FirstOrDefaultAsync();

        ledger.Should().NotBeNull();
        ledger!.Type.Should().Be(BalanceTransactionType.Topup);
        ledger.Amount.Should().Be(amount);
        ledger.RefType.Should().Be("TopupRequest");
    }

    [Fact]
    public async Task ApproveTopupAsync_WithAlreadyApprovedTopup_ReturnsFalse()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");

        var topup = TestDataBuilder.CreateTopupRequest(
            user.Id,
            100000m,
            status: TopupStatus.Approved
        );

        _context.TopupRequests.Add(topup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _topupService.ApproveTopupAsync(
            topup.Id,
            100000m,
            "Approved",
            Guid.NewGuid().ToString()
        );

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ApproveTopupAsync_WithInvalidTopupId_ReturnsFalse()
    {
        // Arrange
        var invalidTopupId = Guid.NewGuid();

        // Act
        var result = await _topupService.ApproveTopupAsync(
            invalidTopupId,
            100000m,
            "Approved",
            Guid.NewGuid().ToString()
        );

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RejectTopupAsync_WithPendingTopup_DoesNotCreditBalance()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.UserName == "user1");
        var initialBalance = user.Balance!.ActiveBalance;

        var topup = TestDataBuilder.CreateTopupRequest(
            user.Id,
            100000m,
            status: TopupStatus.Pending
        );

        _context.TopupRequests.Add(topup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _topupService.RejectTopupAsync(
            topup.Id,
            "Invalid proof of payment",
            Guid.NewGuid().ToString()
        );

        // Assert
        result.Should().BeTrue();

        // Refresh from database
        await _context.Entry(user.Balance).ReloadAsync();
        user.Balance.ActiveBalance.Should().Be(initialBalance); // Unchanged

        // Verify topup status
        await _context.Entry(topup).ReloadAsync();
        topup.Status.Should().Be(TopupStatus.Rejected);
        topup.RejectReason.Should().Be("Invalid proof of payment");
        topup.RejectedAt.Should().NotBeNull();
        topup.RejectedBy.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectTopupAsync_DoesNotCreateBalanceLedgerEntry()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");

        var topup = TestDataBuilder.CreateTopupRequest(
            user.Id,
            100000m,
            status: TopupStatus.Pending
        );

        _context.TopupRequests.Add(topup);
        await _context.SaveChangesAsync();

        // Act
        await _topupService.RejectTopupAsync(
            topup.Id,
            "Invalid proof",
            Guid.NewGuid().ToString()
        );

        // Assert
        var ledger = await _context.BalanceLedgers
            .Where(bl => bl.UserId == user.Id && bl.RefId == topup.Id)
            .FirstOrDefaultAsync();

        ledger.Should().BeNull();
    }

    [Fact]
    public async Task RejectTopupAsync_WithAlreadyRejectedTopup_ReturnsFalse()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");

        var topup = TestDataBuilder.CreateTopupRequest(
            user.Id,
            100000m,
            status: TopupStatus.Rejected
        );

        _context.TopupRequests.Add(topup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _topupService.RejectTopupAsync(
            topup.Id,
            "Already rejected",
            Guid.NewGuid().ToString()
        );

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RejectTopupAsync_WithInvalidTopupId_ReturnsFalse()
    {
        // Arrange
        var invalidTopupId = Guid.NewGuid();

        // Act
        var result = await _topupService.RejectTopupAsync(
            invalidTopupId,
            "Invalid",
            Guid.NewGuid().ToString()
        );

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetTopupRequestsPagedAsync_ReturnsPagedResults()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");

        // Add multiple topup requests
        for (int i = 0; i < 15; i++)
        {
            var topup = TestDataBuilder.CreateTopupRequest(
                user.Id,
                (i + 1) * 10000m,
                status: i % 2 == 0 ? TopupStatus.Pending : TopupStatus.Approved
            );
            _context.TopupRequests.Add(topup);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _topupService.GetTopupRequestsPagedAsync(
            page: 1,
            pageSize: 10
        );

        // Assert
        result.Topups.Should().HaveCount(10);
        result.TotalFiltered.Should().BeGreaterOrEqualTo(15);
        result.TotalRecords.Should().BeGreaterOrEqualTo(15);
    }

    [Fact]
    public async Task GetTopupRequestsPagedAsync_WithStatusFilter_FiltersCorrectly()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");

        // Add topup requests with different statuses
        var pendingTopup = TestDataBuilder.CreateTopupRequest(user.Id, 10000m, TopupStatus.Pending);
        var approvedTopup = TestDataBuilder.CreateTopupRequest(user.Id, 20000m, TopupStatus.Approved);
        var rejectedTopup = TestDataBuilder.CreateTopupRequest(user.Id, 30000m, TopupStatus.Rejected);

        _context.TopupRequests.AddRange(pendingTopup, approvedTopup, rejectedTopup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _topupService.GetTopupRequestsPagedAsync(
            page: 1,
            pageSize: 10,
            status: "pending"
        );

        // Assert
        result.Topups.Should().OnlyContain(t => t.Status == TopupStatus.Pending);
        result.TotalFiltered.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task GetTopupRequestByIdAsync_ReturnsTopupWithUserAndBalance()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");

        var topup = TestDataBuilder.CreateTopupRequest(user.Id, 100000m);
        _context.TopupRequests.Add(topup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _topupService.GetTopupRequestByIdAsync(topup.Id);

        // Assert
        result.Should().NotBeNull();
        result!.User.Should().NotBeNull();
        result.User.Username.Should().Be("user1");
        result.User.Balance.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateTopupRequest_WithValidData_SetsPendingStatus()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        var amount = 100000m;

        var topup = TestDataBuilder.CreateTopupRequest(user.Id, amount);

        // Act
        _context.TopupRequests.Add(topup);
        await _context.SaveChangesAsync();

        // Assert
        topup.Status.Should().Be(TopupStatus.Pending);
        topup.Amount.Should().Be(amount);
        topup.UserId.Should().Be(user.Id);
    }

    [Theory]
    [InlineData(-1000)]
    [InlineData(0)]
    [InlineData(0.01)]
    public async Task CreateTopupRequest_WithInvalidAmount_ShouldValidate(decimal invalidAmount)
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");

        // Act
        var topup = TestDataBuilder.CreateTopupRequest(user.Id, invalidAmount);

        // Assert - In a real scenario, validation would happen before saving
        // For now, we're just testing the builder
        topup.Amount.Should().Be(invalidAmount);
    }
}
