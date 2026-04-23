using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Auth;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Application.Abstractions.Fcm;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Application.Abstractions.Sms;
using PedagangPulsa.Application.Abstractions.Suppliers;
using PedagangPulsa.Infrastructure.Caching;
using PedagangPulsa.Infrastructure.Data;
using PedagangPulsa.Infrastructure.Fcm;
using PedagangPulsa.Infrastructure.Sms;
using PedagangPulsa.Infrastructure.Suppliers;
using StackExchange.Redis;

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
        services.AddHttpClient();
        services.AddHttpClient("Fcm", client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddHttpClient("GoogleAuth", client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IFcmClient, FcmClient>();
        services.AddHttpClient("SmsGate", client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<ISmsClient, SmsGateClient>();
        services.AddScoped<IGoogleTokenValidator, PedagangPulsa.Infrastructure.Auth.GoogleTokenValidator>();

        return services;
    }

    public static IServiceCollection AddRedisInfrastructure(
        this IServiceCollection services,
        string redisConnectionString)
    {
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();
            logger.LogInformation("Connecting to Redis...");

            // StackExchange.Redis does not support redis:// URL scheme.
            // Convert "redis://user:pass@host:port" to "host:port,password=pass,user=user"
            var config = ParseConnectionString(redisConnectionString);
            config.AbortOnConnectFail = false;
            config.ConnectTimeout = 10000;
            config.AsyncTimeout = 5000;
            config.SyncTimeout = 5000;
            config.ReconnectRetryPolicy = new ExponentialRetry(5000);
            var connection = ConnectionMultiplexer.Connect(config);
            connection.ConnectionFailed += (_, e) =>
                logger.LogWarning(e.Exception, "Redis connection failed. EndPoint: {EndPoint}", e.EndPoint);
            connection.ConnectionRestored += (_, e) =>
                logger.LogInformation("Redis connection restored. EndPoint: {EndPoint}", e.EndPoint);
            connection.ErrorMessage += (_, e) =>
                logger.LogError("Redis error: {Message}", e.Message);
            return connection;
        });

        services.AddSingleton<IRedisService, RedisService>();
        services.AddScoped<IProductCacheService, ProductCacheService>();

        return services;
    }

    private static ConfigurationOptions ParseConnectionString(string connectionString)
    {
        // If it's already in StackExchange.Redis format (host:port,password=...), parse directly
        if (!connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigurationOptions.Parse(connectionString);
        }

        // Parse "redis://user:password@host:port" URL format
        var uri = new Uri(connectionString);
        var config = new ConfigurationOptions
        {
            EndPoints = { { uri.Host, uri.Port } }
        };

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            config.User = parts[0];
            if (parts.Length > 1)
            {
                config.Password = parts[1];
            }
        }

        return config;
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
