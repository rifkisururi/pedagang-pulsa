using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Tests.Helpers;

public class TestDbContext : AppDbContext
{
    private static int _categoryIdCounter = 1;
    private static int _supplierProductIdCounter = 1;
    private static int _supplierBalanceIdCounter = 1;
    private static int _productLevelPriceIdCounter = 1;

    public bool IsInMemory => true;

    public TestDbContext()
        : base(CreateOptions())
    {
        Database.EnsureCreated();
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;
    }

    public async Task CleanupBeforeSeedAsync()
    {
        await Database.EnsureDeletedAsync();
        await Database.EnsureCreatedAsync();
        ResetCounters();
    }

    public async Task SeedAsync()
    {
        await CleanupBeforeSeedAsync();

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

        AdminUsers.Add(new AdminUser
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 12),
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var user1 = CreateUser(
            username: "user1",
            email: "user1@test.com",
            phone: "08123456789",
            levelId: member1Level.Id,
            referralCode: "USER1ABC");

        var user2 = CreateUser(
            username: "user2",
            email: "user2@test.com",
            phone: "08123456790",
            levelId: member2Level.Id,
            referralCode: "USER2XYZ",
            referredBy: user1.Id);

        Users.AddRange(user1, user2);

        UserBalances.AddRange(
            new UserBalance
            {
                UserId = user1.Id,
                ActiveBalance = 1000000,
                HeldBalance = 0,
                UpdatedAt = DateTime.UtcNow
            },
            new UserBalance
            {
                UserId = user2.Id,
                ActiveBalance = 500000,
                HeldBalance = 0,
                UpdatedAt = DateTime.UtcNow
            });

        await SaveChangesAsync();

        var category = new ProductCategory
        {
            Id = _categoryIdCounter++,
            Name = "Pulsa",
            Code = "PULSA",
            SortOrder = 1,
            IsActive = true
        };

        ProductCategories.Add(category);

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

        ProductLevelPrices.AddRange(
            new ProductLevelPrice
            {
                Id = _productLevelPriceIdCounter++,
                ProductId = product1.Id,
                LevelId = member1Level.Id,
                Margin = 300,
                IsActive = true
            },
            new ProductLevelPrice
            {
                Id = _productLevelPriceCounter(),
                ProductId = product1.Id,
                LevelId = member2Level.Id,
                Margin = 100,
                IsActive = true
            },
            new ProductLevelPrice
            {
                Id = _productLevelPriceCounter(),
                ProductId = product2.Id,
                LevelId = member1Level.Id,
                Margin = 400,
                IsActive = true
            },
            new ProductLevelPrice
            {
                Id = _productLevelPriceCounter(),
                ProductId = product2.Id,
                LevelId = member2Level.Id,
                Margin = 200,
                IsActive = true
            });

        Suppliers.AddRange(
            new Supplier
            {
                Id = 1,
                Name = "Digiflazz",
                Code = "DIGIFLAZZ",
                ApiBaseUrl = "https://api.digiflazz.com",
                MemberId = "test_member_id",
                Pin = "test_pin",
                Password = "test_password",
                IsActive = true,
                TimeoutSeconds = 30
            },
            new Supplier
            {
                Id = 2,
                Name = "VIPReseller",
                Code = "VIPRESELLER",
                ApiBaseUrl = "https://api.vipreseller.com",
                MemberId = "test_member_id",
                Pin = "test_pin",
                Password = "test_password",
                IsActive = true,
                TimeoutSeconds = 30
            });

        await SaveChangesAsync();

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
            });

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
            },
            new SupplierProduct
            {
                Id = _supplierProductIdCounter++,
                ProductId = product2.Id,
                SupplierId = 1,
                SupplierProductCode = "TS10",
                SupplierProductName = "Telkomsel 10.000",
                CostPrice = 10100,
                Seq = 1,
                IsActive = true
            });

        BankAccounts.Add(new BankAccount
        {
            Id = 1,
            BankName = "BCA",
            AccountName = "Pedagang Pulsa",
            AccountNumber = "1234567890",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await SaveChangesAsync();
    }

    public async Task CleanAsync()
    {
        await CleanupBeforeSeedAsync();
    }

    private static void ResetCounters()
    {
        _categoryIdCounter = 1;
        _supplierProductIdCounter = 1;
        _supplierBalanceIdCounter = 1;
        _productLevelPriceIdCounter = 1;
    }

    private static int _productLevelPriceCounter() => _productLevelPriceIdCounter++;

    private static User CreateUser(
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
            UserName = username,
            FullName = $"Test {username}",
            Email = email,
            Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 12),
            PinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 12),
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
}
