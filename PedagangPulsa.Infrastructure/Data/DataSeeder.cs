using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Entities;

namespace PedagangPulsa.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Seed Admin User
        await SeedAdminUserAsync(context);

        // Seed Product Categories
        await SeedProductCategoriesAsync(context);

        // Seed User Levels
        await SeedUserLevelsAsync(context);
    }

    private static async Task SeedAdminUserAsync(AppDbContext context)
    {
        // Check if admin user already exists
        var existingAdmin = await context.AdminUsers.FirstOrDefaultAsync(a => a.Username == "admin");
        if (existingAdmin != null)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("Admin user already exists in admin_users table.");
            Console.WriteLine("Username: admin");
            Console.WriteLine("===========================================");
            return;
        }

        // Create default admin user
        var adminUser = new AdminUser
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@pedagangpulsa.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = Domain.Enums.AdminRole.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.AdminUsers.Add(adminUser);
        await context.SaveChangesAsync();

        Console.WriteLine("===========================================");
        Console.WriteLine("ADMIN_USERS: Default admin user created.");
        Console.WriteLine("Username: admin");
        Console.WriteLine("Password: Admin@123");
        Console.WriteLine("Role: SuperAdmin");
        Console.WriteLine("Email: admin@pedagangpulsa.com");
        Console.WriteLine("Please change the password after first login!");
        Console.WriteLine("===========================================");
    }

    private static async Task SeedProductCategoriesAsync(AppDbContext context)
    {
        // Check if categories already exist
        if (await context.ProductCategories.AnyAsync())
        {
            Console.WriteLine("Product categories already exist.");
            return;
        }

        var categories = new List<ProductCategory>
        {
            new ProductCategory
            {
                Name = "Pulsa Reguler",
                Code = "PULSA",
                SortOrder = 1,
                IsActive = true
            },
            new ProductCategory
            {
                Name = "Pulsa Transfer",
                Code = "PULSA_TF",
                SortOrder = 2,
                IsActive = true
            },
            new ProductCategory
            {
                Name = "Data Package",
                Code = "DATA",
                SortOrder = 3,
                IsActive = true
            },
            new ProductCategory
            {
                Name = "E-Wallet",
                Code = "EWALLET",
                SortOrder = 4,
                IsActive = true
            },
            new ProductCategory
            {
                Name = "PLN Token",
                Code = "PLN",
                SortOrder = 5,
                IsActive = true
            },
            new ProductCategory
            {
                Name = "PDAM",
                Code = "PDAM",
                SortOrder = 6,
                IsActive = true
            },
            new ProductCategory
            {
                Name = "BPJS",
                Code = "BPJS",
                SortOrder = 7,
                IsActive = true
            },
            new ProductCategory
            {
                Name = "Game Voucher",
                Code = "GAME",
                SortOrder = 8,
                IsActive = true
            }
        };

        await context.ProductCategories.AddRangeAsync(categories);
        await context.SaveChangesAsync();

        Console.WriteLine("===========================================");
        Console.WriteLine("PRODUCT_CATEGORIES: Default categories created.");
        Console.WriteLine($"Total categories: {categories.Count}");
        Console.WriteLine("===========================================");
    }

    private static async Task SeedUserLevelsAsync(AppDbContext context)
    {
        // Check if levels already exist
        if (await context.UserLevels.AnyAsync())
        {
            Console.WriteLine("User levels already exist.");
            return;
        }

        var levels = new List<UserLevel>
        {
            new UserLevel
            {
                Name = "Bronze",
                Description = "Level reseller pemula",
                MarkupType = Domain.Enums.MarkupType.Percentage,
                MarkupValue = 0,
                CanTransfer = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new UserLevel
            {
                Name = "Silver",
                Description = "Level reseller menengah",
                MarkupType = Domain.Enums.MarkupType.Percentage,
                MarkupValue = 0,
                CanTransfer = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new UserLevel
            {
                Name = "Gold",
                Description = "Level reseller tingkat lanjut",
                MarkupType = Domain.Enums.MarkupType.Percentage,
                MarkupValue = 0,
                CanTransfer = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new UserLevel
            {
                Name = "Platinum",
                Description = "Level reseller premium",
                MarkupType = Domain.Enums.MarkupType.Percentage,
                MarkupValue = 0,
                CanTransfer = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new UserLevel
            {
                Name = "Diamond",
                Description = "Level reseller tertinggi",
                MarkupType = Domain.Enums.MarkupType.Percentage,
                MarkupValue = 0,
                CanTransfer = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await context.UserLevels.AddRangeAsync(levels);
        await context.SaveChangesAsync();

        Console.WriteLine("===========================================");
        Console.WriteLine("USER_LEVELS: Default user levels created.");
        Console.WriteLine($"Total levels: {levels.Count}");
        Console.WriteLine("===========================================");
    }
}
