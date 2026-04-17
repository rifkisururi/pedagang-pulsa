using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Tests.Helpers;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Application.Services;

public class ReferralServiceTests : IAsyncLifetime
{
    private TestDbContext _context = null!;
    private ReferralService _service = null!;
    private Mock<ILogger<ReferralService>> _loggerMock = null!;

    public async Task InitializeAsync()
    {
        _context = new TestDbContext();
        await _context.CleanupBeforeSeedAsync();
        await _context.SeedAsync();
        _loggerMock = MockServices.CreateLogger<ReferralService>();
        _service = new ReferralService(_context, _loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        await _context.CleanupBeforeSeedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetReferralLogsPagedAsync_ReturnsPagedResults()
    {
        // Act
        var (logs, totalFiltered, totalRecords) = await _service.GetReferralLogsPagedAsync(1, 10);

        // Assert
        logs.Should().NotBeNull();
        totalRecords.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetReferralLogsPagedAsync_WithSearchFilter_ReturnsFilteredResults()
    {
        // Arrange - Create a referral log first
        var referrer = await _context.Users.FirstAsync(u => u.UserName == "user1");
        var referee = await _context.Users.FirstAsync(u => u.UserName == "user2");

        _context.ReferralLogs.Add(new ReferralLog
        {
            ReferrerId = referrer.Id,
            RefereeId = referee.Id,
            BonusAmount = 10000,
            BonusStatus = ReferralBonusStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var (logs, totalFiltered, totalRecords) = await _service.GetReferralLogsPagedAsync(
            1, 10, search: "user1");

        // Assert
        logs.Should().NotBeEmpty();
        totalFiltered.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetReferralLogsPagedAsync_WithStatusFilter_ReturnsFilteredResults()
    {
        // Act
        var (logs, totalFiltered, totalRecords) = await _service.GetReferralLogsPagedAsync(
            1, 10, status: "Pending");

        // Assert
        logs.Should().NotBeNull();
        if (logs.Any())
        {
            logs.Should().OnlyContain(l => l.BonusStatus == ReferralBonusStatus.Pending);
        }
    }

    [Fact]
    public async Task GetReferralLogsPagedAsync_WithDateFilter_ReturnsFilteredResults()
    {
        // Arrange
        var referrer = await _context.Users.FirstAsync();
        var referee = await _context.Users.Skip(1).FirstAsync();

        var pastLog = new ReferralLog
        {
            ReferrerId = referrer.Id,
            RefereeId = referee.Id,
            BonusAmount = 10000,
            BonusStatus = ReferralBonusStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        _context.ReferralLogs.Add(pastLog);
        await _context.SaveChangesAsync();

        // Act
        var startDate = DateTime.UtcNow.AddDays(-5);
        var (logs, totalFiltered, totalRecords) = await _service.GetReferralLogsPagedAsync(
            1, 10, startDate: startDate);

        // Assert
        logs.Should().NotContain(l => l.CreatedAt < startDate);
    }

    [Fact]
    public async Task GetReferralLogsPagedAsync_WithPaging_ReturnsCorrectPage()
    {
        // Arrange - Add multiple logs
        var referrer = await _context.Users.FirstAsync();
        for (int i = 0; i < 5; i++)
        {
            var referee = new User
            {
                UserName = $"referee{i}",
                Email = $"referee{i}@test.com",
                PinHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                LevelId = 1,
                Status = UserStatus.Active,
                ReferralCode = $"REF{i}",
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(referee);
            await _context.SaveChangesAsync();

            _context.ReferralLogs.Add(new ReferralLog
            {
                ReferrerId = referrer.Id,
                RefereeId = referee.Id,
                BonusAmount = 10000,
                BonusStatus = ReferralBonusStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var (page1, _, _) = await _service.GetReferralLogsPagedAsync(1, 2);
        var (page2, _, _) = await _service.GetReferralLogsPagedAsync(2, 2);

        // Assert
        page1.Count.Should().BeLessOrEqualTo(2);
        page2.Count.Should().BeLessOrEqualTo(2);

        // Verify no duplicates
        var page1Ids = page1.Select(l => l.Id).ToHashSet();
        var page2Ids = page2.Select(l => l.Id).ToHashSet();
        page1Ids.IntersectWith(page2Ids);
        page1Ids.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTopReferrersAsync_ReturnsOrderedResults()
    {
        // Arrange
        var referrer = await _context.Users.FirstAsync(u => u.UserName == "user1");

        // Add a paid referral log
        _context.ReferralLogs.Add(new ReferralLog
        {
            ReferrerId = referrer.Id,
            RefereeId = Guid.NewGuid(),
            BonusAmount = 50000,
            BonusStatus = ReferralBonusStatus.Paid,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTopReferrersAsync(10);

        // Assert
        result.Should().NotBeNull();
        if (result.Count > 1)
        {
            result.Should().BeInDescendingOrder(r => r.TotalBonusPaid);
        }
    }

    [Fact]
    public async Task GetTopReferrersAsync_WithCountLimit_ReturnsLimitedResults()
    {
        // Arrange
        var referrers = await _context.Users.Take(5).ToListAsync();
        for (int i = 0; i < referrers.Count; i++)
        {
            _context.ReferralLogs.Add(new ReferralLog
            {
                ReferrerId = referrers[i].Id,
                RefereeId = Guid.NewGuid(),
                BonusAmount = 10000 * (i + 1),
                BonusStatus = ReferralBonusStatus.Paid,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTopReferrersAsync(3);

        // Assert
        result.Count.Should().BeLessOrEqualTo(3);
    }

    [Fact]
    public async Task PayPendingBonusAsync_WithValidLog_PaysBonus()
    {
        // Arrange
        var referrer = await _context.Users.Include(u => u.Balance).FirstAsync(u => u.UserName == "user1");
        var initialBalance = referrer.Balance!.ActiveBalance;

        var referralLog = new ReferralLog
        {
            ReferrerId = referrer.Id,
            RefereeId = Guid.NewGuid(),
            BonusAmount = 25000,
            BonusStatus = ReferralBonusStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _context.ReferralLogs.Add(referralLog);
        await _context.SaveChangesAsync();

        // Act
        var performedBy = referrer.Id.ToString();
        var result = await _service.PayPendingBonusAsync(referralLog.Id, performedBy);

        // Assert
        result.Should().BeTrue();

        // Verify balance was updated
        _context.Entry(referrer.Balance).ReloadAsync().Wait();
        referrer.Balance.ActiveBalance.Should().Be(initialBalance + 25000);

        // Verify log was updated
        _context.Entry(referralLog).ReloadAsync().Wait();
        referralLog.BonusStatus.Should().Be(ReferralBonusStatus.Paid);
        referralLog.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PayPendingBonusAsync_WithInvalidLog_ReturnsFalse()
    {
        // Act
        var result = await _service.PayPendingBonusAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task PayPendingBonusAsync_WithAlreadyPaidLog_ReturnsFalse()
    {
        // Arrange
        var referrer = await _context.Users.FirstAsync();
        var referralLog = new ReferralLog
        {
            ReferrerId = referrer.Id,
            RefereeId = Guid.NewGuid(),
            BonusAmount = 25000,
            BonusStatus = ReferralBonusStatus.Paid,
            CreatedAt = DateTime.UtcNow
        };
        _context.ReferralLogs.Add(referralLog);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PayPendingBonusAsync(referralLog.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelReferralBonusAsync_WithValidLog_CancelsBonus()
    {
        // Arrange
        var referrer = await _context.Users.FirstAsync();
        var referralLog = new ReferralLog
        {
            ReferrerId = referrer.Id,
            RefereeId = Guid.NewGuid(),
            BonusAmount = 25000,
            BonusStatus = ReferralBonusStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _context.ReferralLogs.Add(referralLog);
        await _context.SaveChangesAsync();

        // Act
        var performedBy = referrer.Id.ToString();
        var result = await _service.CancelReferralBonusAsync(referralLog.Id, "Test cancellation", performedBy);

        // Assert
        result.Should().BeTrue();

        // Verify log was updated
        _context.Entry(referralLog).ReloadAsync().Wait();
        referralLog.BonusStatus.Should().Be(ReferralBonusStatus.Cancelled);
        referralLog.CancelledAt.Should().NotBeNull();
        referralLog.CancellationReason.Should().Be("Test cancellation");
    }

    [Fact]
    public async Task CancelReferralBonusAsync_WithInvalidLog_ReturnsFalse()
    {
        // Act
        var result = await _service.CancelReferralBonusAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelReferralBonusAsync_WithAlreadyPaidLog_DoesNotCancel()
    {
        // Arrange
        var referrer = await _context.Users.FirstAsync();
        var referralLog = new ReferralLog
        {
            ReferrerId = referrer.Id,
            RefereeId = Guid.NewGuid(),
            BonusAmount = 25000,
            BonusStatus = ReferralBonusStatus.Paid,
            CreatedAt = DateTime.UtcNow
        };
        _context.ReferralLogs.Add(referralLog);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CancelReferralBonusAsync(referralLog.Id);

        // Assert - Since it's already paid, it shouldn't find it as Pending
        result.Should().BeFalse();
    }
}
