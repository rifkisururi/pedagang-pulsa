using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Application.Abstractions.Fcm;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Application.Abstractions.Suppliers;
using PedagangPulsa.Infrastructure.Caching;
using PedagangPulsa.Infrastructure.Data;
using PedagangPulsa.Infrastructure.Fcm;
using PedagangPulsa.Infrastructure.Suppliers;

namespace PedagangPulsa.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        bool ignorePendingModelChangesWarning = false)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);

            if (ignorePendingModelChangesWarning)
            {
                options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
            }
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ISupplierAdapterFactory, SupplierAdapterFactory>();
        services.AddScoped<IRedisService, RedisService>();
        services.AddHttpClient();
        services.AddHttpClient("Fcm", client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddHttpClient("GoogleAuth", client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IFcmClient, FcmClient>();

        return services;
    }
}

public static class DatabaseInitializationExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(this IHost app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        try
        {
            var context = services.GetRequiredService<AppDbContext>();
            await context.Database.MigrateAsync();
            await DataSeeder.SeedAsync(context);
        }
        catch (Exception exception)
        {
            var logger = services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("DatabaseInitialization");
            logger.LogError(exception, "An error occurred migrating or seeding the database.");
        }
    }
}
