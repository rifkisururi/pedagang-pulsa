using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Suppliers;
using PedagangPulsa.Tests.Helpers;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Application.Services;

public class BalanceServiceTests : IAsyncLifetime
{
    private TestDbContext _context = null!;
    private BalanceService _balanceService = null!;
    private TransactionService _transactionService = null!;
    private Mock<ILogger<BalanceService>> _loggerMock = null!;
    private Mock<ILoggerFactory> _loggerFactoryMock = null!;
    private Mock<ISupplierAdapterFactory> _adapterFactoryMock = null!;

    public async Task InitializeAsync()
    {
        _context = new TestDbContext();

        // Clean up database before seeding to avoid primary key conflicts
        await _context.CleanupBeforeSeedAsync();

        await _context.SeedAsync();

        _loggerMock = MockServices.CreateLogger<BalanceService>();
        _balanceService = new BalanceService(_context, _loggerMock.Object);

        _loggerFactoryMock = MockServices.CreateLoggerFactory();
        _adapterFactoryMock = MockServices.CreateSupplierAdapterFactory();
        _transactionService = new TransactionService(_context, _adapterFactoryMock.Object, _loggerFactoryMock.Object);
    }

    public async Task DisposeAsync()
    {
        // Clean up test data after all tests
        await _context.CleanupBeforeSeedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task HoldSufficientBalance_ReturnsSuccessAndMovesBalanceToHeld()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var initialActive = user.Balance!.ActiveBalance;
        var initialHeld = user.Balance.HeldBalance;
        var holdAmount = 50000m;

        // Act
        var result = await _transactionService.HoldBalanceAsync(user.Id, holdAmount, "TestHold", Guid.NewGuid());

        // Assert
        result.Should().BeTrue();

        // Refresh from database
        await _context.Entry(user).ReloadAsync();
        await _context.Entry(user.Balance!).ReloadAsync();

        user.Balance.ActiveBalance.Should().Be(initialActive - holdAmount);
        user.Balance.HeldBalance.Should().Be(initialHeld + holdAmount);
    }

    [Fact]
    public async Task HoldInsufficientBalance_ReturnsFailureAndDoesNotChangeBalance()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var initialActive = user.Balance!.ActiveBalance;
        var initialHeld = user.Balance.HeldBalance;
        var holdAmount = initialActive + 1000000m; // More than available

        // Act
        var result = await _transactionService.HoldBalanceAsync(user.Id, holdAmount, "TestHold", Guid.NewGuid());

        // Assert
        result.Should().BeFalse();

        // Refresh from database
        await _context.Entry(user).ReloadAsync();
        await _context.Entry(user.Balance!).ReloadAsync();

        user.Balance.ActiveBalance.Should().Be(initialActive);
        user.Balance.HeldBalance.Should().Be(initialHeld);
    }

    [Fact]
    public async Task HoldWithZeroBalance_ReturnsFailure()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser("zerobalance", levelId: 1);
        var balance = TestDataBuilder.CreateUserBalance(user.Id, 0, 0);

        _context.Users.Add(user);
        _context.UserBalances.Add(balance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _transactionService.HoldBalanceAsync(user.Id, 1000m, "TestHold", Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HoldCreatesLedgerEntry()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var holdAmount = 50000m;
        var refId = Guid.NewGuid();

        // Act
        await _transactionService.HoldBalanceAsync(user.Id, holdAmount, "TestHold", refId);

        // Assert
        var ledger = await _context.BalanceLedgers
            .Where(bl => bl.UserId == user.Id)
            .OrderByDescending(bl => bl.CreatedAt)
            .FirstOrDefaultAsync();

        ledger.Should().NotBeNull();
        ledger!.Type.Should().Be(BalanceTransactionType.PurchaseHold);
        ledger.Amount.Should().Be(holdAmount);
        ledger.RefId.Should().Be(refId);
        ledger.RefType.Should().Be("Transaction");
    }

    [Fact]
    public async Task DebitHeldBalance_WithExactAmount_ReturnsSuccess()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var holdAmount = 50000m;
        await _transactionService.HoldBalanceAsync(user.Id, holdAmount, "TestHold", Guid.NewGuid());

        await _context.Entry(user.Balance!).ReloadAsync();
        var initialHeld = user.Balance!.HeldBalance;

        // Act
        var result = await _transactionService.DebitHeldBalanceAsync(user.Id, holdAmount, "TestDebit");

        // Assert
        result.Should().BeTrue();

        // Refresh from database
        await _context.Entry(user.Balance).ReloadAsync();

        user.Balance.HeldBalance.Should().Be(initialHeld - holdAmount);
        user.Balance.ActiveBalance.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task DebitHeldBalance_WithMoreThanHeld_ReturnsFailure()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var holdAmount = 50000m;
        await _transactionService.HoldBalanceAsync(user.Id, holdAmount, "TestHold", Guid.NewGuid());

        await _context.Entry(user.Balance!).ReloadAsync();
        var initialHeld = user.Balance!.HeldBalance;

        // Act
        var result = await _transactionService.DebitHeldBalanceAsync(user.Id, holdAmount + 10000m, "TestDebit");

        // Assert
        result.Should().BeFalse();

        // Refresh from database
        await _context.Entry(user.Balance).ReloadAsync();

        user.Balance.HeldBalance.Should().Be(initialHeld); // Unchanged
    }

    [Fact]
    public async Task DebitHeldBalance_CreatesLedgerEntry()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var holdAmount = 50000m;
        await _transactionService.HoldBalanceAsync(user.Id, holdAmount, "TestHold", Guid.NewGuid());

        // Act
        await _transactionService.DebitHeldBalanceAsync(user.Id, holdAmount, "TestDebit");

        // Assert
        var ledger = await _context.BalanceLedgers
            .Where(bl => bl.UserId == user.Id && bl.Type == BalanceTransactionType.PurchaseDebit)
            .OrderByDescending(bl => bl.CreatedAt)
            .FirstOrDefaultAsync();

        ledger.Should().NotBeNull();
        ledger!.Type.Should().Be(BalanceTransactionType.PurchaseDebit);
        ledger.Amount.Should().Be(holdAmount);
        ledger.Notes.Should().Contain("TestDebit");
    }

    [Fact]
    public async Task ReleaseHeldBalance_ReturnsToActiveBalance()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var holdAmount = 50000m;
        await _transactionService.HoldBalanceAsync(user.Id, holdAmount, "TestHold", Guid.NewGuid());

        await _context.Entry(user.Balance!).ReloadAsync();
        var initialActive = user.Balance!.ActiveBalance;
        var initialHeld = user.Balance.HeldBalance;

        // Act
        var result = await _transactionService.ReleaseHeldBalanceAsync(user.Id, holdAmount, "TestRelease");

        // Assert
        result.Should().BeTrue();

        // Refresh from database
        await _context.Entry(user.Balance).ReloadAsync();

        user.Balance.ActiveBalance.Should().Be(initialActive + holdAmount);
        user.Balance.HeldBalance.Should().Be(initialHeld - holdAmount);
    }

    [Fact]
    public async Task ReleaseHeldBalance_WithMoreThanHeld_ReturnsFailure()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var holdAmount = 50000m;
        await _transactionService.HoldBalanceAsync(user.Id, holdAmount, "TestHold", Guid.NewGuid());

        await _context.Entry(user.Balance!).ReloadAsync();
        var initialActive = user.Balance!.ActiveBalance;
        var initialHeld = user.Balance.HeldBalance;

        // Act
        var result = await _transactionService.ReleaseHeldBalanceAsync(user.Id, holdAmount + 10000m, "TestRelease");

        // Assert
        result.Should().BeFalse();

        // Refresh from database
        await _context.Entry(user.Balance).ReloadAsync();

        user.Balance.ActiveBalance.Should().Be(initialActive); // Unchanged
        user.Balance.HeldBalance.Should().Be(initialHeld); // Unchanged
    }

    [Fact]
    public async Task ReleaseHeldBalance_CreatesLedgerEntry()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var holdAmount = 50000m;
        await _transactionService.HoldBalanceAsync(user.Id, holdAmount, "TestHold", Guid.NewGuid());

        // Act
        await _transactionService.ReleaseHeldBalanceAsync(user.Id, holdAmount, "TestRelease");

        // Assert
        var ledger = await _context.BalanceLedgers
            .Where(bl => bl.UserId == user.Id && bl.Type == BalanceTransactionType.PurchaseRelease)
            .OrderByDescending(bl => bl.CreatedAt)
            .FirstOrDefaultAsync();

        ledger.Should().NotBeNull();
        ledger!.Type.Should().Be(BalanceTransactionType.PurchaseRelease);
        ledger.Amount.Should().Be(holdAmount);
        ledger.Notes.Should().Contain("TestRelease");
    }

    [Fact]
    public async Task AdjustUserBalance_CreditIncreasesBalance()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var initialBalance = user.Balance!.ActiveBalance;
        var creditAmount = 100000m;

        // Act
        var result = await _balanceService.AdjustUserBalanceAsync(
            user.Id,
            creditAmount,
            "Topup",
            "Test topup",
            "Test note",
            Guid.NewGuid().ToString()
        );

        // Assert
        result.Should().BeTrue();

        // Refresh from database
        await _context.Entry(user.Balance).ReloadAsync();

        user.Balance.ActiveBalance.Should().Be(initialBalance + creditAmount);
    }

    [Fact]
    public async Task AdjustUserBalance_DebitDecreasesBalance()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var initialBalance = user.Balance!.ActiveBalance;
        var debitAmount = -50000m;

        // Act
        var result = await _balanceService.AdjustUserBalanceAsync(
            user.Id,
            debitAmount,
            "Adjustment",
            "Test adjustment",
            "Test note",
            Guid.NewGuid().ToString()
        );

        // Assert
        result.Should().BeTrue();

        // Refresh from database
        await _context.Entry(user.Balance).ReloadAsync();

        user.Balance.ActiveBalance.Should().Be(initialBalance + debitAmount);
    }

    [Fact]
    public async Task AdjustUserBalance_WithNegativeBalance_ReturnsFailure()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var currentBalance = user.Balance!.ActiveBalance;
        var debitAmount = -(currentBalance + 1000000m); // More than available

        // Act
        var result = await _balanceService.AdjustUserBalanceAsync(
            user.Id,
            debitAmount,
            "Adjustment",
            "Test adjustment",
            "Test note",
            Guid.NewGuid().ToString()
        );

        // Assert
        result.Should().BeFalse();

        // Refresh from database
        await _context.Entry(user.Balance).ReloadAsync();

        user.Balance.ActiveBalance.Should().Be(currentBalance); // Unchanged
    }

    [Fact]
    public async Task AdjustUserBalance_CreatesLedgerEntry()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var amount = 50000m;

        // Act
        await _balanceService.AdjustUserBalanceAsync(
            user.Id,
            amount,
            "Adjustment",
            "Test adjustment",
            "Test note",
            Guid.NewGuid().ToString()
        );

        // Assert
        var ledger = await _context.BalanceLedgers
            .Where(bl => bl.UserId == user.Id && bl.Type == BalanceTransactionType.Adjustment)
            .OrderByDescending(bl => bl.CreatedAt)
            .FirstOrDefaultAsync();

        ledger.Should().NotBeNull();
        ledger!.Type.Should().Be(BalanceTransactionType.Adjustment);
        ledger.Amount.Should().Be(amount);
        ledger.Notes.Should().Contain("Test adjustment");
    }

    [Fact]
    public async Task BalanceLedgerIntegrity_AllEntriesHaveCorrectBeforeAfterValues()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");

        // Act - Perform multiple operations
        var refId1 = Guid.NewGuid();
        await _transactionService.HoldBalanceAsync(user.Id, 50000m, "Hold1", refId1);

        await _context.Entry(user.Balance!).ReloadAsync();
        await _transactionService.ReleaseHeldBalanceAsync(user.Id, 50000m, "Release1");

        await _context.Entry(user.Balance).ReloadAsync();
        await _balanceService.AdjustUserBalanceAsync(
            user.Id,
            100000m,
            "Topup",
            "Test topup",
            null,
            Guid.NewGuid().ToString()
        );

        // Assert - Verify ledger entries
        var ledgers = await _context.BalanceLedgers
            .Where(bl => bl.UserId == user.Id)
            .OrderByDescending(bl => bl.CreatedAt)
            .Take(3)
            .ToListAsync();

        ledgers.Should().HaveCount(3);

        foreach (var ledger in ledgers)
        {
            ledger.ActiveBefore.Should().BeGreaterThanOrEqualTo(0);
            ledger.ActiveAfter.Should().BeGreaterThanOrEqualTo(0);
            ledger.HeldBefore.Should().BeGreaterThanOrEqualTo(0);
            ledger.HeldAfter.Should().BeGreaterThanOrEqualTo(0);

            // Verify the math is correct
            if (ledger.Type == BalanceTransactionType.PurchaseHold)
            {
                ledger.ActiveAfter.Should().Be(ledger.ActiveBefore - ledger.Amount);
                ledger.HeldAfter.Should().Be(ledger.HeldBefore + ledger.Amount);
            }
            else if (ledger.Type == BalanceTransactionType.PurchaseRelease)
            {
                ledger.ActiveAfter.Should().Be(ledger.ActiveBefore + ledger.Amount);
                ledger.HeldAfter.Should().Be(ledger.HeldBefore - ledger.Amount);
            }
            else if (ledger.Type == BalanceTransactionType.Topup || ledger.Type == BalanceTransactionType.Adjustment)
            {
                ledger.ActiveAfter.Should().Be(ledger.ActiveBefore + ledger.Amount);
            }
        }
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
