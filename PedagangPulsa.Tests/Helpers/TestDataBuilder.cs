using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Tests.Helpers;

/// <summary>
/// Builder pattern for creating test data
/// </summary>
public class TestDataBuilder
{
    /// <summary>
    /// Create a test user with default values
    /// </summary>
    public static User CreateUser(
        string? username = null,
        string? email = null,
        string? phone = null,
        int levelId = 1,
        string? referralCode = null,
        Guid? referredBy = null,
        UserStatus status = UserStatus.Active,
        string pin = "123456")
    {
        var guid = Guid.NewGuid().ToString("N");
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username ?? $"testuser_{guid}",
            FullName = $"Test User {username}",
            Email = email ?? $"test_{guid}@test.com",
            Phone = phone ?? $"0812{guid.Substring(0, 8)}",
            PinHash = BCrypt.Net.BCrypt.HashPassword(pin),
            PinFailedAttempts = 0,
            PinLockedAt = null,
            LevelId = levelId,
            CanTransferOverride = null,
            ReferralCode = referralCode ?? GenerateReferralCode(),
            ReferredBy = referredBy,
            Status = status,
            EmailVerifiedAt = DateTime.UtcNow,
            PhoneVerifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Create a test user balance
    /// </summary>
    public static UserBalance CreateUserBalance(
        Guid userId,
        decimal activeBalance = 1000000,
        decimal heldBalance = 0)
    {
        return new UserBalance
        {
            UserId = userId,
            ActiveBalance = activeBalance,
            HeldBalance = heldBalance,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Create a test transaction
    /// </summary>
    public static Transaction CreateTransaction(
        Guid userId,
        Guid productId,
        string destination,
        decimal sellPrice,
        TransactionStatus status = TransactionStatus.Pending)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid().ToString(),
            UserId = userId,
            ProductId = productId,
            Destination = destination,
            SellPrice = sellPrice,
            Status = status,
            CurrentSeq = 1,
            PinVerifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Create a test topup request
    /// </summary>
    public static TopupRequest CreateTopupRequest(
        Guid userId,
        decimal amount,
        TopupStatus status = TopupStatus.Pending)
    {
        return new TopupRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BankAccountId = null,
            Amount = amount,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Create a test balance ledger entry
    /// </summary>
    public static BalanceLedger CreateBalanceLedger(
        Guid userId,
        BalanceTransactionType type,
        decimal amount,
        decimal activeBefore,
        decimal activeAfter,
        string? notes = null)
    {
        return new BalanceLedger
        {
            UserId = userId,
            Type = type,
            Amount = amount,
            ActiveBefore = activeBefore,
            ActiveAfter = activeAfter,
            HeldBefore = 0,
            HeldAfter = 0,
            Notes = notes,
            RefType = "Test",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Create a test product
    /// </summary>
    public static Product CreateProduct(
        int categoryId,
        string name,
        string code,
        decimal denomination,
        string? @operator = null,
        bool isActive = true)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            Name = name,
            Code = code,
            Denomination = denomination,
            Operator = @operator ?? "TestOperator",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Create a test supplier
    /// </summary>
    public static Supplier CreateSupplier(
        string name,
        string code,
        string apiBaseUrl,
        bool isActive = true)
    {
        var random = new Random();
        return new Supplier
        {
            Id = random.Next(1, 1000),
            Name = name,
            Code = code,
            ApiBaseUrl = apiBaseUrl,
            ApiKeyEnc = "encrypted_test_key",
            CallbackSecret = "test_secret",
            IsActive = isActive,
            TimeoutSeconds = 30
        };
    }

    /// <summary>
    /// Create a test supplier product mapping
    /// </summary>
    public static SupplierProduct CreateSupplierProduct(
        Guid productId,
        int supplierId,
        string supplierProductCode,
        decimal costPrice,
        short seq = 1,
        bool isActive = true)
    {
        var random = new Random();
        return new SupplierProduct
        {
            Id = random.Next(1, 1000),
            ProductId = productId,
            SupplierId = supplierId,
            SupplierProductCode = supplierProductCode,
            SupplierProductName = $"Test Product {supplierProductCode}",
            CostPrice = costPrice,
            Seq = seq,
            IsActive = isActive
        };
    }

    /// <summary>
    /// Create a test transaction attempt
    /// </summary>
    public static TransactionAttempt CreateTransactionAttempt(
        Guid transactionId,
        int supplierId,
        int supplierProductId,
        short seq,
        AttemptStatus status = AttemptStatus.Pending)
    {
        return new TransactionAttempt
        {
            TransactionId = transactionId,
            SupplierId = supplierId,
            SupplierProductId = supplierProductId,
            Seq = seq,
            Status = status,
            AttemptedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Create a test product category
    /// </summary>
    public static ProductCategory CreateProductCategory(
        string name,
        string code,
        bool isActive = true)
    {
        var random = new Random();
        return new ProductCategory
        {
            Id = random.Next(1, 1000),
            Name = name,
            Code = code,
            IsActive = isActive
        };
    }

    /// <summary>
    /// Create a test user level
    /// </summary>
    public static UserLevel CreateUserLevel(
        string name,
        decimal markupValue,
        MarkupType markupType = MarkupType.Percentage,
        bool canTransfer = false)
    {
        return new UserLevel
        {
            Name = name,
            Description = $"Test level {name}",
            MarkupValue = markupValue,
            MarkupType = markupType,
            CanTransfer = canTransfer,
            IsActive = true
        };
    }

    /// <summary>
    /// Generate a random referral code
    /// </summary>
    public static string GenerateReferralCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// Create a test refresh token
    /// </summary>
    public static RefreshToken CreateRefreshToken(
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        return new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Create a test referral log
    /// </summary>
    public static ReferralLog CreateReferralLog(
        Guid referrerId,
        Guid refereeId,
        ReferralBonusStatus bonusStatus = ReferralBonusStatus.Pending)
    {
        return new ReferralLog
        {
            ReferrerId = referrerId,
            RefereeId = refereeId,
            BonusStatus = bonusStatus,
            CreatedAt = DateTime.UtcNow
        };
    }
}
