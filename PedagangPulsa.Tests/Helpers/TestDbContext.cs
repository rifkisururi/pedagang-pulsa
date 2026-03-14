using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Tests.Helpers;

/// <summary>
/// In-memory database context for testing
/// </summary>
public class TestDbContext : AppDbContext
{
    private static int _categoryIdCounter = 1;
    private static int _supplierProductIdCounter = 1;
    private static int _supplierBalanceIdCounter = 1;
    private static int _productLevelPriceIdCounter = 1;

    public bool IsInMemory => Database.IsInMemory();

    public TestDbContext() : base(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options)
    {
    }

    /// <summary>
    /// Seed database with test data
    /// </summary>
    public async Task SeedAsync()
    {
        // Add user levels
        var member1Level = new UserLevel
        {
            Id = 1,
            Name = "Member1",
            Description = "Basic member level",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 5.0m,
            CanTransfer = false,
            IsActive = true
        };

        var member2Level = new UserLevel
        {
            Id = 2,
            Name = "Member2",
            Description = "Advanced member level",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 3.0m,
            CanTransfer = true,
            IsActive = true
        };

        UserLevels.AddRange(member1Level, member2Level);
        await SaveChangesAsync();

        // Add admin user
        var adminUser = new AdminUser
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@test.com",
            PasswordHash = "hashed_password",
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        AdminUsers.Add(adminUser);
        await SaveChangesAsync();

        // Add test users
        var user1 = CreateUser(
            username: "user1",
            email: "user1@test.com",
            phone: "08123456789",
            levelId: 1,
            referralCode: "USER1ABC"
        );

        var user2 = CreateUser(
            username: "user2",
            email: "user2@test.com",
            phone: "08123456790",
            levelId: 2,
            referralCode: "USER2XYZ",
            referredBy: user1.Id
        );

        Users.AddRange(user1, user2);
        await SaveChangesAsync();

        // Add user balances
        var balance1 = new UserBalance
        {
            UserId = user1.Id,
            ActiveBalance = 1000000,
            HeldBalance = 0,
            UpdatedAt = DateTime.UtcNow
        };

        var balance2 = new UserBalance
        {
            UserId = user2.Id,
            ActiveBalance = 500000,
            HeldBalance = 0,
            UpdatedAt = DateTime.UtcNow
        };

        UserBalances.AddRange(balance1, balance2);
        await SaveChangesAsync();

        // Add product category
        var category = new ProductCategory
        {
            Id = _categoryIdCounter++,
            Name = "Pulsa",
            Code = "PULSA",
            IsActive = true
        };

        ProductCategories.Add(category);
        await SaveChangesAsync();

        // Add products
        var product1 = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "Pulsa Indosat 5.000",
            Code = "IS5",
            Denomination = 5000,
            Operator = "Indosat",
            IsActive = true
        };

        var product2 = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "Pulsa Telkomsel 10.000",
            Code = "TS10",
            Denomination = 10000,
            Operator = "Telkomsel",
            IsActive = true
        };

        Products.AddRange(product1, product2);
        await SaveChangesAsync();

        // Add product level prices
        ProductLevelPrices.AddRange(
            new ProductLevelPrice
            {
                Id = _productLevelPriceIdCounter++,
                ProductId = product1.Id,
                LevelId = 1,
                SellPrice = 5500,
                IsActive = true
            },
            new ProductLevelPrice
            {
                Id = _productLevelPriceIdCounter++,
                ProductId = product1.Id,
                LevelId = 2,
                SellPrice = 5300,
                IsActive = true
            },
            new ProductLevelPrice
            {
                Id = _productLevelPriceIdCounter++,
                ProductId = product2.Id,
                LevelId = 1,
                SellPrice = 10500,
                IsActive = true
            },
            new ProductLevelPrice
            {
                Id = _productLevelPriceIdCounter++,
                ProductId = product2.Id,
                LevelId = 2,
                SellPrice = 10300,
                IsActive = true
            }
        );
        await SaveChangesAsync();

        // Add suppliers
        var supplier1 = new Supplier
        {
            Name = "Digiflazz",
            Code = "DIGIFLAZZ",
            ApiBaseUrl = "https://api.digiflazz.com",
            ApiKeyEnc = "encrypted_key",
            CallbackSecret = "secret123",
            IsActive = true,
            TimeoutSeconds = 30
        };

        var supplier2 = new Supplier
        {
            Name = "VIPReseller",
            Code = "VIPRESELLER",
            ApiBaseUrl = "https://api.vipreseller.com",
            ApiKeyEnc = "encrypted_key",
            CallbackSecret = "secret456",
            IsActive = true,
            TimeoutSeconds = 30
        };

        Suppliers.AddRange(supplier1, supplier2);
        await SaveChangesAsync();

        // Add supplier balances
        SupplierBalances.AddRange(
            new SupplierBalance
            {
                Id = _supplierBalanceIdCounter++,
                SupplierId = 1,
                ActiveBalance = 5000000,
                UpdatedAt = DateTime.UtcNow
            },
            new SupplierBalance
            {
                Id = _supplierBalanceIdCounter++,
                SupplierId = 2,
                ActiveBalance = 3000000,
                UpdatedAt = DateTime.UtcNow
            }
        );
        await SaveChangesAsync();

        // Add supplier products
        SupplierProducts.AddRange(
            new SupplierProduct
            {
                Id = _supplierProductIdCounter++,
                ProductId = product1.Id,
                SupplierId = 1,
                SupplierProductCode = "IS5",
                SupplierProductName = "Indosat 5.000",
                CostPrice = 5200,
                Seq = 1,
                IsActive = true
            },
            new SupplierProduct
            {
                Id = _supplierProductIdCounter++,
                ProductId = product1.Id,
                SupplierId = 2,
                SupplierProductCode = "IS5",
                SupplierProductName = "Indosat 5.000",
                CostPrice = 5250,
                Seq = 2,
                IsActive = true
            }
        );

        await SaveChangesAsync();
    }

    private User CreateUser(
        string username,
        string email,
        string phone,
        int levelId,
        string referralCode,
        Guid? referredBy = null)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = $"Test {username}",
            Email = email,
            Phone = phone,
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            PinFailedAttempts = 0,
            PinLockedAt = null,
            LevelId = levelId,
            CanTransferOverride = null,
            ReferralCode = referralCode,
            ReferredBy = referredBy,
            Status = UserStatus.Active,
            EmailVerifiedAt = DateTime.UtcNow,
            PhoneVerifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Clean up database
    /// </summary>
    public async Task CleanAsync()
    {
        // Remove all data
        TransactionAttempts.RemoveRange(TransactionAttempts);
        SupplierCallbacks.RemoveRange(SupplierCallbacks);
        Transactions.RemoveRange(Transactions);
        IdempotencyKeys.RemoveRange(IdempotencyKeys);
        SupplierProducts.RemoveRange(SupplierProducts);
        Products.RemoveRange(Products);
        ProductCategories.RemoveRange(ProductCategories);
        BalanceLedgers.RemoveRange(BalanceLedgers);
        UserBalances.RemoveRange(UserBalances);
        RefreshTokens.RemoveRange(RefreshTokens);
        Users.RemoveRange(Users);
        UserLevelConfigs.RemoveRange(UserLevelConfigs);
        UserLevels.RemoveRange(UserLevels);
        SupplierBalanceLedgers.RemoveRange(SupplierBalanceLedgers);
        SupplierBalances.RemoveRange(SupplierBalances);
        Suppliers.RemoveRange(Suppliers);

        await SaveChangesAsync();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Don't configure again, already configured in constructor
    }
}
