using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Auth;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Application.Services;

namespace PedagangPulsa.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        string? jwtSecret = null,
        string? jwtIssuer = null,
        string? jwtAudience = null)
    {
        services.AddScoped<UserService>();
        services.AddScoped<ProductService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<SupplierProductService>();
        services.AddScoped<SupplierBalanceService>();
        services.AddScoped<SupplierRegexPatternService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<TopupService>();
        services.AddScoped<BalanceService>();
        services.AddScoped<ReportService>();
        services.AddScoped<ReferralService>();
        services.AddScoped<UserLevelService>();
        services.AddScoped<ExportService>();
        services.AddScoped<FcmService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<PhoneVerificationService>();
        services.AddScoped<AuthService>(serviceProvider =>
            new AuthService(
                serviceProvider.GetRequiredService<IAppDbContext>(),
                serviceProvider.GetRequiredService<ILogger<AuthService>>(),
                serviceProvider.GetService<IRedisService>(),
                serviceProvider.GetService<IGoogleTokenValidator>(),
                jwtSecret: jwtSecret,
                jwtIssuer: jwtIssuer,
                jwtAudience: jwtAudience));

        return services;
    }
}
