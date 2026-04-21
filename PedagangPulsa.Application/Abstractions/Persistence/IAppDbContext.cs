using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Application.Abstractions.Persistence;

public interface IAppDbContext
{
    DbSet<UserLevel> UserLevels { get; }
    DbSet<UserLevelConfig> UserLevelConfigs { get; }
    DbSet<User> Users { get; }
    DbSet<UserBalance> UserBalances { get; }
    DbSet<BalanceLedger> BalanceLedgers { get; }
    DbSet<PinResetToken> PinResetTokens { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<ReferralLog> ReferralLogs { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<TopupRequest> TopupRequests { get; }
    DbSet<PeerTransfer> PeerTransfers { get; }
    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<ProductGroup> ProductGroups { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductLevelPrice> ProductLevelPrices { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierProduct> SupplierProducts { get; }
    DbSet<SupplierBalance> SupplierBalances { get; }
    DbSet<SupplierBalanceLedger> SupplierBalanceLedgers { get; }
    DbSet<SupplierRegexPattern> SupplierRegexPatterns { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<TransactionAttempt> TransactionAttempts { get; }
    DbSet<SupplierCallback> SupplierCallbacks { get; }
    DbSet<IdempotencyKey> IdempotencyKeys { get; }
    DbSet<NotificationTemplate> NotificationTemplates { get; }
    DbSet<NotificationLog> NotificationLogs { get; }
    DbSet<UserDevice> UserDevices { get; }
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
