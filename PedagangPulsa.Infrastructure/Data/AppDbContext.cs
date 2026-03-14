using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Entities;

namespace PedagangPulsa.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // User & Auth
    public DbSet<UserLevel> UserLevels { get; set; }
    public DbSet<UserLevelConfig> UserLevelConfigs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserBalance> UserBalances { get; set; }
    public DbSet<BalanceLedger> BalanceLedgers { get; set; }
    public DbSet<PinResetToken> PinResetTokens { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    // Referral
    public DbSet<ReferralLog> ReferralLogs { get; set; }

    // Balance & Topup
    public DbSet<BankAccount> BankAccounts { get; set; }
    public DbSet<TopupRequest> TopupRequests { get; set; }
    public DbSet<PeerTransfer> PeerTransfers { get; set; }

    // Product
    public DbSet<ProductCategory> ProductCategories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductLevelPrice> ProductLevelPrices { get; set; }

    // Supplier
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<SupplierProduct> SupplierProducts { get; set; }
    public DbSet<SupplierBalance> SupplierBalances { get; set; }
    public DbSet<SupplierBalanceLedger> SupplierBalanceLedgers { get; set; }

    // Transaction
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<TransactionAttempt> TransactionAttempts { get; set; }
    public DbSet<SupplierCallback> SupplierCallbacks { get; set; }

    // Idempotency
    public DbSet<IdempotencyKey> IdempotencyKeys { get; set; }

    // Notification
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; }
    public DbSet<NotificationLog> NotificationLogs { get; set; }

    // Admin & Audit
    public DbSet<AdminUser> AdminUsers { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // UserLevel
        modelBuilder.Entity<UserLevel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MarkupValue).HasPrecision(10, 4);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // UserLevelConfig
        modelBuilder.Entity<UserLevelConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConfigKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ConfigValue).IsRequired();
            entity.HasOne(e => e.Level)
                .WithMany(l => l.Configs)
                .HasForeignKey(e => e.LevelId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.LevelId, e.ConfigKey }).IsUnique();
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.PinHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ReferralCode).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Phone).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.LevelId);
            entity.HasIndex(e => e.ReferralCode).IsUnique();
            entity.HasIndex(e => e.ReferredBy);
            entity.HasOne(e => e.Level)
                .WithMany(l => l.Users)
                .HasForeignKey(e => e.LevelId);
            entity.HasOne(e => e.Referrer)
                .WithMany(r => r.Referees)
                .HasForeignKey(e => e.ReferredBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // UserBalance
        modelBuilder.Entity<UserBalance>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.ActiveBalance).HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.HeldBalance).HasPrecision(15, 2).IsRequired();
            entity.HasOne(e => e.User)
                .WithOne(u => u.Balance)
                .HasForeignKey<UserBalance>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BalanceLedger
        modelBuilder.Entity<BalanceLedger>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.CreatedAt });
            entity.Property(e => e.Amount).HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.ActiveBefore).HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.ActiveAfter).HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.HeldBefore).HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.HeldAfter).HasPrecision(15, 2).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => new { e.RefType, e.RefId });
            entity.HasOne(e => e.User)
                .WithMany(u => u.BalanceLedgers)
                .HasPrincipalKey(u => u.Id)
                .HasForeignKey(e => e.UserId);
        });

        // PinResetToken
        modelBuilder.Entity<PinResetToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(10);
            entity.HasIndex(e => new { e.UserId, e.ExpiresAt });
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.Token);
            entity.HasIndex(e => new { e.UserId, e.IsRevoked, e.ExpiresAt });
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ReferralLog
        modelBuilder.Entity<ReferralLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BonusAmount).HasPrecision(15, 2);
            entity.HasIndex(e => new { e.ReferrerId, e.BonusStatus });
            entity.HasIndex(e => new { e.BonusStatus, e.CreatedAt });
            entity.HasOne(e => e.Referrer)
                .WithMany(u => u.ReferralLogsAsReferrer)
                .HasForeignKey(e => e.ReferrerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Referee)
                .WithMany(u => u.ReferralLogsAsReferee)
                .HasForeignKey(e => e.RefereeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.RefereeId).IsUnique();
        });

        // BankAccount
        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BankName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(30);
            entity.Property(e => e.AccountName).IsRequired().HasMaxLength(100);
        });

        // TopupRequest
        modelBuilder.Entity<TopupRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(15, 2).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasOne(e => e.User)
                .WithMany(u => u.TopupRequests)
                .HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.BankAccount)
                .WithMany(b => b.TopupRequests)
                .HasForeignKey(e => e.BankAccountId);
        });

        // PeerTransfer
        modelBuilder.Entity<PeerTransfer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(15, 2).IsRequired();
            entity.HasIndex(e => new { e.FromUserId, e.CreatedAt });
            entity.HasIndex(e => new { e.ToUserId, e.CreatedAt });
            entity.HasOne(e => e.FromUser)
                .WithMany(u => u.SentTransfers)
                .HasForeignKey(e => e.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ToUser)
                .WithMany(u => u.ReceivedTransfers)
                .HasForeignKey(e => e.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProductCategory
        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Denomination).HasPrecision(15, 2);
            entity.Property(e => e.Operator).HasMaxLength(50);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => new { e.CategoryId, e.IsActive });
            entity.HasIndex(e => e.Operator);
            entity.HasOne(e => e.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(e => e.CategoryId);
        });

        // ProductLevelPrice
        modelBuilder.Entity<ProductLevelPrice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SellPrice).HasPrecision(15, 2).IsRequired();
            entity.HasIndex(e => new { e.ProductId, e.LevelId, e.IsActive });
            entity.HasOne(e => e.Product)
                .WithMany(p => p.ProductLevelPrices)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Level)
                .WithMany(l => l.ProductLevelPrices)
                .HasForeignKey(e => e.LevelId);
            entity.HasIndex(e => new { e.ProductId, e.LevelId }).IsUnique();
        });

        // Supplier
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasOne(e => e.Balance)
                .WithOne(b => b.Supplier)
                .HasForeignKey<SupplierBalance>(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SupplierBalance
        modelBuilder.Entity<SupplierBalance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActiveBalance).HasPrecision(15, 2).IsRequired();
            entity.HasIndex(e => e.SupplierId).IsUnique();
            entity.HasOne(e => e.Supplier)
                .WithOne(s => s.Balance)
                .HasForeignKey<SupplierBalance>(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SupplierBalanceLedger
        modelBuilder.Entity<SupplierBalanceLedger>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.BalanceBefore).HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.BalanceAfter).HasPrecision(15, 2).IsRequired();
            entity.HasIndex(e => new { e.SupplierId, e.CreatedAt });
            entity.HasOne(e => e.Supplier)
                .WithMany(s => s.Ledgers)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SupplierProduct
        modelBuilder.Entity<SupplierProduct>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SupplierProductCode).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SupplierProductName).HasMaxLength(150);
            entity.Property(e => e.CostPrice).HasPrecision(15, 2).IsRequired();
            entity.HasIndex(e => new { e.ProductId, e.Seq, e.IsActive });
            entity.HasOne(e => e.Product)
                .WithMany(p => p.SupplierProducts)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Supplier)
                .WithMany(s => s.SupplierProducts)
                .HasForeignKey(e => e.SupplierId);
            entity.HasIndex(e => new { e.ProductId, e.SupplierId }).IsUnique();
            entity.HasIndex(e => new { e.ProductId, e.Seq }).IsUnique();
        });

        // Transaction
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.CreatedAt });
            entity.Property(e => e.ReferenceId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Destination).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SellPrice).HasPrecision(15, 2).IsRequired();
            entity.Property(e => e.CostPrice).HasPrecision(15, 2);
            entity.HasIndex(e => e.ReferenceId).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasFilter("status IN ('pending', 'processing')");
            entity.HasOne(e => e.User)
                .WithMany(u => u.Transactions)
                .HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Product)
                .WithMany(p => p.Transactions)
                .HasForeignKey(e => e.ProductId);
        });

        // TransactionAttempt
        modelBuilder.Entity<TransactionAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SupplierRefId).HasMaxLength(100);
            entity.Property(e => e.SupplierTrxId).HasMaxLength(100);
            entity.Property(e => e.ErrorCode).HasMaxLength(50);
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.SupplierRefId)
                .HasFilter("supplier_ref_id IS NOT NULL");
            entity.HasIndex(e => new { e.Status, e.AttemptedAt })
                .HasFilter("status IN ('pending', 'processing')");
            entity.HasOne(e => e.Transaction)
                .WithMany(t => t.Attempts)
                .HasPrincipalKey(t => t.Id)
                .HasForeignKey(e => e.TransactionId);
            entity.HasOne(e => e.Supplier)
                .WithMany(s => s.TransactionAttempts)
                .HasForeignKey(e => e.SupplierId);
            entity.HasOne(e => e.SupplierProduct)
                .WithMany(sp => sp.TransactionAttempts)
                .HasForeignKey(e => e.SupplierProductId);
            entity.HasIndex(e => new { e.TransactionId, e.Seq }).IsUnique();
        });

        // SupplierCallback
        modelBuilder.Entity<SupplierCallback>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RawPayload).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasIndex(e => new { e.CreatedAt, e.IsProcessed })
                .HasFilter("is_processed = false");
            entity.HasOne(e => e.Supplier)
                .WithMany(s => s.SupplierCallbacks)
                .HasForeignKey(e => e.SupplierId);
            entity.HasOne(e => e.Attempt)
                .WithMany(a => a.SupplierCallbacks)
                .HasForeignKey(e => e.AttemptId);
        });

        // IdempotencyKey
        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.Key });
            entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // NotificationTemplate
        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Subject).HasMaxLength(200);
            entity.Property(e => e.Body).IsRequired();
            entity.HasIndex(e => new { e.Code, e.Channel }).IsUnique();
        });

        // NotificationLog
        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.CreatedAt });
            entity.Property(e => e.TemplateCode).HasMaxLength(50);
            entity.Property(e => e.Recipient).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Subject).HasMaxLength(200);
            entity.Property(e => e.RefType).HasMaxLength(50);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasFilter("status = 'pending'");
            entity.HasOne(e => e.User)
                .WithMany()
                .HasPrincipalKey(u => u.Id)
                .HasForeignKey(e => e.UserId);
        });

        // AdminUser
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.CreatedAt });
            entity.Property(e => e.ActorType).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Entity).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasIndex(e => new { e.ActorId, e.CreatedAt });
            entity.HasIndex(e => new { e.Entity, e.EntityId });
        });

        // Configure PostgreSQL enum mappings
        modelBuilder.HasPostgresEnum("user_status", new[] { "active", "inactive", "suspended" });
        modelBuilder.HasPostgresEnum("transaction_status", new[] { "pending", "processing", "success", "failed", "refunded", "cancelled" });
        modelBuilder.HasPostgresEnum("attempt_status", new[] { "pending", "processing", "success", "failed", "timeout" });
        modelBuilder.HasPostgresEnum("topup_status", new[] { "pending", "approved", "rejected" });
        modelBuilder.HasPostgresEnum("notification_channel", new[] { "email", "sms", "whatsapp" });
        modelBuilder.HasPostgresEnum("markup_type", new[] { "percentage", "fixed" });
        modelBuilder.HasPostgresEnum("admin_role", new[] { "superadmin", "admin", "finance", "staff" });
        modelBuilder.HasPostgresEnum("referral_bonus_status", new[] { "pending", "paid", "cancelled" });
        modelBuilder.HasPostgresEnum("balance_tx_type", new[] { "topup", "purchase_hold", "purchase_debit", "purchase_release", "transfer_out", "transfer_in", "refund", "adjustment" });
    }
}
