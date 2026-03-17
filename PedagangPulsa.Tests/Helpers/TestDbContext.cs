using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Tests.Helpers;

/// <summary>
/// PostgreSQL database context for testing
/// Uses the same database as the web application
/// </summary>
public class TestDbContext : AppDbContext
{
    private static int _categoryIdCounter = 1;
    private static int _supplierProductIdCounter = 1;
    private static int _supplierBalanceIdCounter = 1;
    private static int _productLevelPriceIdCounter = 1;

    public bool IsInMemory => false;

    public TestDbContext() : base(
        CreateOptions())
    {
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        // Use the dev database connection string
        var connectionString = "Host=ep-noisy-rain-a1pqpydc-pooler.ap-southeast-1.aws.neon.tech;Username=neondb_owner;Password=npg_a1pMW8UqCKVI;Database=neondb;SSL Mode=Require;Trust Server Certificate=true";

        return new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;
    }

    /// <summary>
    /// Clean up database before seeding to avoid primary key conflicts
    /// Uses TRUNCATE with CASCADE for efficient cleanup
    /// </summary>
    public async Task CleanupBeforeSeedAsync()
    {
        // Use TRUNCATE with CASCADE for efficient cleanup and restart sequences
        // This is safer than DELETE as it handles foreign keys automatically
        var tables = new[]
        {
            "\"AdminUsers\"",
            "\"Suppliers\"",
            "\"SupplierBalances\"",
            "\"SupplierBalanceLedgers\"",
            "\"SupplierCallbacks\"",
            "\"SupplierProducts\"",
            "\"TopupRequests\"",
            "\"ReferralLogs\"",
            "\"UserLevels\"",
            "\"UserLevelConfigs\"",
            "\"Users\"",
            "\"UserBalances\"",
            "\"BalanceLedgers\"",
            "\"RefreshTokens\"",
            "\"Products\"",
            "\"ProductLevelPrices\"",
            "\"ProductCategories\"",
            "\"Transactions\"",
            "\"TransactionAttempts\"",
            "\"IdempotencyKeys\""
        };

        foreach (var table in tables)
        {
            try
            {
                await Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {table} CASCADE");
            }
            catch
            {
                // Table might not exist or already truncated, continue
            }
        }

        // Restart sequences to ensure IDs start from 1
        await RestartSequencesAsync();
    }

    /// <summary>
    /// Restart all sequences to 1
    /// </summary>
    private async Task RestartSequencesAsync()
    {
        var sequences = new[]
        {
            "\"ProductCategories_Id_seq\"",
            "\"UserLevels_Id_seq\"",
            "\"SupplierBalances_Id_seq\"",
            "\"SupplierProducts_Id_seq\"",
            "\"ProductLevelPrices_Id_seq\""
        };

        foreach (var sequence in sequences)
        {
            try
            {
                await Database.ExecuteSqlRawAsync($"ALTER SEQUENCE {sequence} RESTART WITH 1");
            }
            catch
            {
                // Sequence might not exist, ignore error
            }
        }
    }

    /// <summary>
    /// Seed database with test data
    /// </summary>
    public async Task SeedAsync()
    {
        // Clean up first to avoid conflicts
        await CleanupBeforeSeedAsync();

        // Add user levels - Let database auto-generate IDs
        var member1Level = new UserLevel
        {
            Name = "Member1",
            Description = "Basic member level",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 5.0m,
            CanTransfer = false,
            IsActive = true
        };

        var member2Level = new UserLevel
        {
            Name = "Member2",
            Description = "Advanced member level",
            MarkupType = MarkupType.Percentage,
            MarkupValue = 3.0m,
            CanTransfer = true,
            IsActive = true
        };

        UserLevels.AddRange(member1Level, member2Level);
        await SaveChangesAsync();

        // Get the generated IDs for use in creating users
        var member1LevelId = member1Level.Id;
        var member2LevelId = member2Level.Id;

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
            levelId: member1LevelId,
            referralCode: "USER1ABC"
        );

        var user2 = CreateUser(
            username: "user2",
            email: "user2@test.com",
            phone: "08123456790",
            levelId: member2LevelId,
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
                LevelId = member1LevelId,
                SellPrice = 5500,
                IsActive = true
            },
            new ProductLevelPrice
            {
                Id = _productLevelPriceIdCounter++,
                ProductId = product1.Id,
                LevelId = member2LevelId,
                SellPrice = 5300,
                IsActive = true
            },
            new ProductLevelPrice
            {
                Id = _productLevelPriceIdCounter++,
                ProductId = product2.Id,
                LevelId = member1LevelId,
                SellPrice = 10500,
                IsActive = true
            },
            new ProductLevelPrice
            {
                Id = _productLevelPriceIdCounter++,
                ProductId = product2.Id,
                LevelId = member2LevelId,
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
            MemberId = "test_member_id",
            Pin = "test_pin",
            Password = "test_password",
            IsActive = true,
            TimeoutSeconds = 30
        };

        var supplier2 = new Supplier
        {
            Name = "VIPReseller",
            Code = "VIPRESELLER",
            ApiBaseUrl = "https://api.vipreseller.com",
            MemberId = "test_member_id",
            Pin = "test_pin",
            Password = "test_password",
            IsActive = true,
            TimeoutSeconds = 30
        };

        Suppliers.AddRange(supplier1, supplier2);
        await SaveChangesAsync();

        // Reload suppliers to get their auto-generated IDs
        var supplier1Id = Suppliers.Where(s => s.Code == "DIGIFLAZZ").Select(s => s.Id).First();
        var supplier2Id = Suppliers.Where(s => s.Code == "VIPRESELLER").Select(s => s.Id).First();

        // Add supplier balances using actual supplier IDs
        SupplierBalances.AddRange(
            new SupplierBalance
            {
                Id = _supplierBalanceIdCounter++,
                SupplierId = supplier1Id,
                ActiveBalance = 5000000,
                UpdatedAt = DateTime.UtcNow
            },
            new SupplierBalance
            {
                Id = _supplierBalanceIdCounter++,
                SupplierId = supplier2Id,
                ActiveBalance = 3000000,
                UpdatedAt = DateTime.UtcNow
            }
        );
        await SaveChangesAsync();

        // Add supplier products using actual supplier IDs
        SupplierProducts.AddRange(
            new SupplierProduct
            {
                Id = _supplierProductIdCounter++,
                ProductId = product1.Id,
                SupplierId = supplier1Id,
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
                SupplierId = supplier2Id,
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
        await CleanupBeforeSeedAsync();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Don't configure again, already configured in constructor
    }
}
