using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PedagangPulsa.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext context, IServiceProvider serviceProvider)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Seed Superadmin User
        await SeedSuperAdminUserAsync(serviceProvider);
    }

    private static async Task SeedSuperAdminUserAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Create SuperAdmin role if it doesn't exist
        if (!await roleManager.RoleExistsAsync("SuperAdmin"))
        {
            await roleManager.CreateAsync(new IdentityRole("SuperAdmin"));
        }

        // Create superadmin user if it doesn't exist
        var superAdmin = await userManager.FindByNameAsync("admin");
        if (superAdmin == null)
        {
            superAdmin = new IdentityUser
            {
                UserName = "admin",
                Email = "admin@pedagangpulsa.com",
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var result = await userManager.CreateAsync(superAdmin, "Admin@123");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
                Console.WriteLine("Superadmin user created successfully.");
                Console.WriteLine("Username: admin");
                Console.WriteLine("Password: Admin@123");
                Console.WriteLine("Please change the password after first login.");
            }
            else
            {
                Console.WriteLine("Failed to create superadmin user:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"- {error.Description}");
                }
            }
        }
    }
}
