using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Suppliers;
using PedagangPulsa.Infrastructure.Suppliers.DTOs;
using PedagangPulsa.Tests.Helpers;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Application.Services;

public class TransactionServiceTests : IAsyncLifetime
{
    private TestDbContext _context = null!;
    private TransactionService _transactionService = null!;
    private Mock<ILoggerFactory> _loggerFactoryMock = null!;
    private Mock<ISupplierAdapterFactory> _adapterFactoryMock = null!;
    private Mock<ISupplierAdapter> _supplierAdapterMock = null!;

    public async Task InitializeAsync()
    {
        _context = new TestDbContext();

        // Clean up database before seeding to avoid primary key conflicts
        await _context.CleanupBeforeSeedAsync();

        await _context.SeedAsync();

        _loggerFactoryMock = MockServices.CreateLoggerFactory();
        _adapterFactoryMock = MockServices.CreateSupplierAdapterFactory();
        _supplierAdapterMock = new Mock<ISupplierAdapter>();

        _transactionService = new TransactionService(
            _context,
            _adapterFactoryMock.Object,
            _loggerFactoryMock.Object
        );
    }

    public async Task DisposeAsync()
    {
        // Clean up test data after all tests
        await _context.CleanupBeforeSeedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CreateTransactionAsync_WithSufficientBalance_CreatesPendingTransaction()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var product = await _context.Products.FirstAsync(p => p.Code == "IS5");
        var destination = "08123456789";
        var amount = 5500m; // user1's price for IS5

        // Act
        var result = await _transactionService.CreateTransactionAsync(
            user.Id,
            product.Id,
            destination,
            amount
        );

        // Assert
        result.Transaction.Should().NotBeNull();
        result.ErrorMessage.Should().BeEmpty();
        result.Transaction!.Status.Should().Be(TransactionStatus.Pending);
        result.Transaction.UserId.Should().Be(user.Id);
        result.Transaction.ProductId.Should().Be(product.Id);
        result.Transaction.Destination.Should().Be(destination);
        result.Transaction.SellPrice.Should().Be(amount);

        // Verify balance was held
        _context.Entry(user.Balance!).ReloadAsync().Wait();
        user.Balance!.ActiveBalance.Should().BeLessThan(1000000m); // Initial was 1,000,000
        user.Balance.HeldBalance.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateTransactionAsync_WithInsufficientBalance_ReturnsError()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var product = await _context.Products.FirstAsync(p => p.Code == "IS5");
        var destination = "08123456789";
        var amount = user.Balance!.ActiveBalance + 1000000m; // More than available

        // Act
        var result = await _transactionService.CreateTransactionAsync(
            user.Id,
            product.Id,
            destination,
            amount
        );

        // Assert
        result.Transaction.Should().BeNull();
        result.ErrorMessage.Should().Be("Insufficient balance");
    }

    [Fact]
    public async Task CreateTransactionAsync_WithInvalidUser_ReturnsError()
    {
        // Arrange
        var invalidUserId = Guid.NewGuid();
        var product = await _context.Products.FirstAsync(p => p.Code == "IS5");

        // Act
        var result = await _transactionService.CreateTransactionAsync(
            invalidUserId,
            product.Id,
            "08123456789",
            5500m
        );

        // Assert
        result.Transaction.Should().BeNull();
        result.ErrorMessage.Should().Be("User not found");
    }

    [Fact]
    public async Task CreateTransactionAsync_HoldsBalanceBeforeCreatingTransaction()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var product = await _context.Products.FirstAsync(p => p.Code == "IS5");
        var initialActive = user.Balance!.ActiveBalance;
        var initialHeld = user.Balance.HeldBalance;
        var amount = 5500m;

        // Act
        var result = await _transactionService.CreateTransactionAsync(
            user.Id,
            product.Id,
            "08123456789",
            amount
        );

        // Assert
        result.Transaction.Should().NotBeNull();

        // Refresh from database
        _context.Entry(user.Balance).ReloadAsync().Wait();

        user.Balance.ActiveBalance.Should().Be(initialActive - amount);
        user.Balance.HeldBalance.Should().Be(initialHeld + amount);
    }

    [Fact]
    public async Task CreateTransactionAsync_CreatesLedgerEntry()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var product = await _context.Products.FirstAsync(p => p.Code == "IS5");
        var amount = 5500m;

        // Act
        var result = await _transactionService.CreateTransactionAsync(
            user.Id,
            product.Id,
            "08123456789",
            amount
        );

        // Assert
        var ledger = await _context.BalanceLedgers
            .Where(bl => bl.UserId == user.Id)
            .OrderByDescending(bl => bl.CreatedAt)
            .FirstOrDefaultAsync();

        ledger.Should().NotBeNull();
        ledger!.Type.Should().Be(BalanceTransactionType.PurchaseHold);
        ledger.Amount.Should().Be(amount);
    }

    [Fact]
    public async Task ProcessTransactionAsync_WithSuccessfulSupplier_UpdatesTransactionToSuccess()
    {
        // Arrange
        // Setup mock supplier
        _adapterFactoryMock
            .Setup(x => x.CreateAdapter("DIGIFLAZZ", It.IsAny<ILoggerFactory>()))
            .Returns(_supplierAdapterMock.Object);

        _supplierAdapterMock
            .Setup(x => x.PurchaseAsync(It.IsAny<SupplierPurchaseRequest>()))
            .ReturnsAsync(new SupplierPurchaseResult
            {
                Success = true,
                SerialNumber = "SN12345",
                SupplierTransactionId = "TRX123",
                Message = "Success"
            });

        // Create transaction
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var product = await _context.Products.FirstAsync(p => p.Code == "IS5");
        var createResult = await _transactionService.CreateTransactionAsync(
            user.Id,
            product.Id,
            "08123456789",
            5500m
        );

        _context.Entry(user.Balance!).ReloadAsync().Wait();
        var initialHeld = user.Balance!.HeldBalance;
        var initialActive = user.Balance.ActiveBalance;

        // Act
        var processResult = await _transactionService.ProcessTransactionAsync(createResult.Transaction!.Id);

        // Assert
        processResult.Should().BeTrue();

        // Refresh transaction
        _context.Entry(createResult.Transaction).ReloadAsync().Wait();
        createResult.Transaction.Status.Should().Be(TransactionStatus.Success);
        createResult.Transaction.SerialNumber.Should().Be("SN12345");
        createResult.Transaction.SupplierTrxId.Should().Be("TRX123");
        createResult.Transaction.CompletedAt.Should().NotBeNull();

        // Verify held balance was debited
        _context.Entry(user.Balance).ReloadAsync().Wait();
        user.Balance.HeldBalance.Should().BeLessThan(initialHeld);
        user.Balance.ActiveBalance.Should().Be(initialActive); // Should not change on success
    }

    [Fact]
    public async Task ProcessTransactionAsync_WithAllSuppliersFailed_ReleasesHeldBalance()
    {
        // Arrange
        // Setup mock supplier to fail
        _adapterFactoryMock
            .Setup(x => x.CreateAdapter("DIGIFLAZZ", It.IsAny<ILoggerFactory>()))
            .Returns(_supplierAdapterMock.Object);

        _supplierAdapterMock
            .Setup(x => x.PurchaseAsync(It.IsAny<SupplierPurchaseRequest>()))
            .ReturnsAsync(new SupplierPurchaseResult
            {
                Success = false,
                ErrorCode = "SUPPLIER_ERROR",
                Message = "Supplier error"
            });

        // Create transaction
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var product = await _context.Products.FirstAsync(p => p.Code == "IS5");
        var createResult = await _transactionService.CreateTransactionAsync(
            user.Id,
            product.Id,
            "08123456789",
            5500m
        );

        _context.Entry(user.Balance!).ReloadAsync().Wait();
        var initialHeld = user.Balance!.HeldBalance;
        var initialActive = user.Balance.ActiveBalance;

        // Act
        var processResult = await _transactionService.ProcessTransactionAsync(createResult.Transaction!.Id);

        // Assert
        processResult.Should().BeFalse();

        // Refresh transaction - fetch from database instead of ReloadAsync
        var updatedTransaction = await _context.Transactions.FindAsync(createResult.Transaction.Id);
        updatedTransaction.Should().NotBeNull();
        updatedTransaction!.Status.Should().Be(TransactionStatus.Failed);
        updatedTransaction.ErrorMessage.Should().Be("All suppliers failed");
        updatedTransaction.CompletedAt.Should().NotBeNull();

        // Verify held balance was released
        _context.Entry(user.Balance).ReloadAsync().Wait();
        user.Balance.HeldBalance.Should().BeLessThan(initialHeld);
        user.Balance.ActiveBalance.Should().BeGreaterThan(initialActive);
    }

    [Fact]
    public async Task ProcessTransactionAsync_CreatesTransactionAttemptsForEachSupplier()
    {
        // Arrange
        // Setup mock supplier
        _adapterFactoryMock
            .Setup(x => x.CreateAdapter("DIGIFLAZZ", It.IsAny<ILoggerFactory>()))
            .Returns(_supplierAdapterMock.Object);

        _supplierAdapterMock
            .Setup(x => x.PurchaseAsync(It.IsAny<SupplierPurchaseRequest>()))
            .ReturnsAsync(new SupplierPurchaseResult
            {
                Success = false,
                ErrorCode = "SUPPLIER_ERROR",
                Message = "Supplier error"
            });

        // Create transaction
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var product = await _context.Products.FirstAsync(p => p.Code == "IS5");
        var createResult = await _transactionService.CreateTransactionAsync(
            user.Id,
            product.Id,
            "08123456789",
            5500m
        );

        // Act
        await _transactionService.ProcessTransactionAsync(createResult.Transaction!.Id);

        // Assert
        var attempts = await _context.TransactionAttempts
            .Where(ta => ta.TransactionId == createResult.Transaction.Id)
            .ToListAsync();

        attempts.Should().HaveCountGreaterThanOrEqualTo(1);
        attempts.Should().OnlyContain(ta => ta.Status == AttemptStatus.Failed);
    }

    [Fact]
    public async Task ProcessTransactionAsync_WithPendingTransaction_ChangesStatus()
    {
        // Arrange
        _adapterFactoryMock
            .Setup(x => x.CreateAdapter("DIGIFLAZZ", It.IsAny<ILoggerFactory>()))
            .Returns(_supplierAdapterMock.Object);

        _supplierAdapterMock
            .Setup(x => x.PurchaseAsync(It.IsAny<SupplierPurchaseRequest>()))
            .ReturnsAsync(new SupplierPurchaseResult
            {
                Success = true,
                SerialNumber = "SN12345",
                SupplierTransactionId = "TRX123",
                Message = "Success"
            });

        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var product = await _context.Products.FirstAsync(p => p.Code == "IS5");
        var createResult = await _transactionService.CreateTransactionAsync(
            user.Id,
            product.Id,
            "08123456789",
            5500m
        );

        var initialStatus = createResult.Transaction!.Status;

        // Act
        await _transactionService.ProcessTransactionAsync(createResult.Transaction.Id);

        // Assert
        // Fetch from database instead of ReloadAsync
        var updatedTransaction = await _context.Transactions.FindAsync(createResult.Transaction.Id);
        updatedTransaction.Should().NotBeNull();
        initialStatus.Should().Be(TransactionStatus.Pending);
        updatedTransaction!.Status.Should().Be(TransactionStatus.Success);
    }

    [Fact]
    public async Task ProcessTransactionAsync_WithInvalidTransactionId_ReturnsFalse()
    {
        // Arrange
        var invalidTransactionId = Guid.NewGuid();

        // Act
        var result = await _transactionService.ProcessTransactionAsync(invalidTransactionId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DebitHeldBalanceAsync_AfterSuccess_DebitsHeldBalance()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var holdAmount = 5500m;
        await _transactionService.HoldBalanceAsync(user.Id, holdAmount, "TestHold", Guid.NewGuid());

        _context.Entry(user.Balance!).ReloadAsync().Wait();
        var initialHeld = user.Balance!.HeldBalance;

        // Act
        var result = await _transactionService.DebitHeldBalanceAsync(user.Id, holdAmount, "Success debit");

        // Assert
        result.Should().BeTrue();

        // Refresh from database
        _context.Entry(user.Balance).ReloadAsync().Wait();

        user.Balance.HeldBalance.Should().Be(initialHeld - holdAmount);
        user.Balance.ActiveBalance.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ReleaseHeldBalanceAsync_AfterFailure_ReleasesBalanceToActive()
    {
        // Arrange
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstAsync(u => u.Username == "user1");
        var holdAmount = 5500m;
        await _transactionService.HoldBalanceAsync(user.Id, holdAmount, "TestHold", Guid.NewGuid());

        _context.Entry(user.Balance!).ReloadAsync().Wait();
        var initialActive = user.Balance!.ActiveBalance;
        var initialHeld = user.Balance.HeldBalance;

        // Act
        var result = await _transactionService.ReleaseHeldBalanceAsync(user.Id, holdAmount, "Failed release");

        // Assert
        result.Should().BeTrue();

        // Refresh from database
        _context.Entry(user.Balance).ReloadAsync().Wait();

        user.Balance.ActiveBalance.Should().Be(initialActive + holdAmount);
        user.Balance.HeldBalance.Should().Be(initialHeld - holdAmount);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
