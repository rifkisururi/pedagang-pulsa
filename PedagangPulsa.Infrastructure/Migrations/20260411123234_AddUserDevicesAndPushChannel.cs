using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PedagangPulsa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDevicesAndPushChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:admin_role", "superadmin,admin,finance,staff")
                .Annotation("Npgsql:Enum:attempt_status", "pending,processing,success,failed,timeout")
                .Annotation("Npgsql:Enum:balance_tx_type", "topup,purchase_hold,purchase_debit,purchase_release,transfer_out,transfer_in,refund,adjustment")
                .Annotation("Npgsql:Enum:markup_type", "percentage,fixed")
                .Annotation("Npgsql:Enum:notification_channel", "email,sms,whatsapp,push")
                .Annotation("Npgsql:Enum:referral_bonus_status", "pending,paid,cancelled")
                .Annotation("Npgsql:Enum:topup_status", "pending,approved,rejected")
                .Annotation("Npgsql:Enum:transaction_status", "pending,processing,success,failed,refunded,cancelled")
                .Annotation("Npgsql:Enum:user_status", "active,inactive,suspended");

            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Entity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EntityId = table.Column<string>(type: "text", nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => new { x.Id, x.CreatedAt });
                });

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IconUrl = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApiBaseUrl = table.Column<string>(type: "text", nullable: true),
                    MemberId = table.Column<string>(type: "text", nullable: true),
                    Pin = table.Column<string>(type: "text", nullable: true),
                    Password = table.Column<string>(type: "text", nullable: true),
                    TimeoutSeconds = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    MarkupType = table.Column<string>(type: "text", nullable: false),
                    MarkupValue = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    CanTransfer = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Denomination = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    Operator = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    ActiveBalance = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierBalances_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLevelConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LevelId = table.Column<int>(type: "integer", nullable: false),
                    ConfigKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ConfigValue = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLevelConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLevelConfigs_UserLevels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "UserLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FullName = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PinHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PinFailedAttempts = table.Column<short>(type: "smallint", nullable: false),
                    PinLockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LevelId = table.Column<int>(type: "integer", nullable: false),
                    CanTransferOverride = table.Column<bool>(type: "boolean", nullable: true),
                    ReferralCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    EmailVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PhoneVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_UserLevels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "UserLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductLevelPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelId = table.Column<int>(type: "integer", nullable: false),
                    Margin = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductLevelPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductLevelPrices_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductLevelPrices_UserLevels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "UserLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    SupplierProductCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierProductName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CostPrice = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Seq = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierProducts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierBalanceLedgers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AdminNote = table.Column<string>(type: "text", nullable: true),
                    PerformedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupplierBalanceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierBalanceLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierBalanceLedgers_SupplierBalances_SupplierBalanceId",
                        column: x => x.SupplierBalanceId,
                        principalTable: "SupplierBalances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierBalanceLedgers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BalanceLedgers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    ActiveBefore = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    ActiveAfter = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    HeldBefore = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    HeldAfter = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    RefType = table.Column<string>(type: "text", nullable: true),
                    RefId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceLedgers", x => new { x.Id, x.CreatedAt });
                    table.ForeignKey(
                        name: "FK_BalanceLedgers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseCache = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => new { x.UserId, x.Key });
                    table.ForeignKey(
                        name: "FK_IdempotencyKeys_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    TemplateCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Recipient = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RefType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RefId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<short>(type: "smallint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => new { x.Id, x.CreatedAt });
                    table.ForeignKey(
                        name: "FK_NotificationLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeerTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeerTransfers_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeerTransfers_Users_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PinResetTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PinResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PinResetTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReferralLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferrerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefereeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BonusAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    BonusStatus = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PaidBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralLogs_Users_RefereeId",
                        column: x => x.RefereeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferralLogs_Users_ReferrerId",
                        column: x => x.ReferrerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TopupRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    UniqueCode = table.Column<int>(type: "integer", nullable: false),
                    TransferProofUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RejectReason = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopupRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopupRequests_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TopupRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Destination = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SellPrice = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    CostPrice = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CurrentSeq = table.Column<short>(type: "smallint", nullable: false),
                    PinVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Sn = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SerialNumber = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    SupplierTrxId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => new { x.Id, x.CreatedAt });
                    table.UniqueConstraint("AK_Transactions_Id", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Transactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBalances",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveBalance = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    HeldBalance = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBalances", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserBalances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FcmToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AppVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDevices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransactionAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    SupplierProductId = table.Column<int>(type: "integer", nullable: false),
                    Seq = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SupplierRefId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupplierTrxId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestPayload = table.Column<string>(type: "text", nullable: true),
                    ResponsePayload = table.Column<string>(type: "text", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionAttempts_SupplierProducts_SupplierProductId",
                        column: x => x.SupplierProductId,
                        principalTable: "SupplierProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionAttempts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionAttempts_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCallbacks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    RawHeaders = table.Column<string>(type: "text", nullable: true),
                    RawPayload = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    HmacValid = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptId = table.Column<long>(type: "bigint", nullable: true),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCallbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCallbacks_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierCallbacks_TransactionAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "TransactionAttempts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Email",
                table: "AdminUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Username",
                table: "AdminUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "ActorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entity_EntityId",
                table: "AuditLogs",
                columns: new[] { "Entity", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceLedgers_RefType_RefId",
                table: "BalanceLedgers",
                columns: new[] { "RefType", "RefId" });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceLedgers_UserId_CreatedAt",
                table: "BalanceLedgers",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAt",
                table: "IdempotencyKeys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_Status_CreatedAt",
                table: "NotificationLogs",
                columns: new[] { "Status", "CreatedAt" },
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_UserId_CreatedAt",
                table: "NotificationLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Code_Channel",
                table: "NotificationTemplates",
                columns: new[] { "Code", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeerTransfers_FromUserId_CreatedAt",
                table: "PeerTransfers",
                columns: new[] { "FromUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PeerTransfers_ToUserId_CreatedAt",
                table: "PeerTransfers",
                columns: new[] { "ToUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PinResetTokens_UserId_ExpiresAt",
                table: "PinResetTokens",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_Code",
                table: "ProductCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductLevelPrices_LevelId",
                table: "ProductLevelPrices",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductLevelPrices_ProductId_LevelId",
                table: "ProductLevelPrices",
                columns: new[] { "ProductId", "LevelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductLevelPrices_ProductId_LevelId_IsActive",
                table: "ProductLevelPrices",
                columns: new[] { "ProductId", "LevelId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId_IsActive",
                table: "Products",
                columns: new[] { "CategoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                table: "Products",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Operator",
                table: "Products",
                column: "Operator");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralLogs_BonusStatus_CreatedAt",
                table: "ReferralLogs",
                columns: new[] { "BonusStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralLogs_RefereeId",
                table: "ReferralLogs",
                column: "RefereeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralLogs_ReferrerId_BonusStatus",
                table: "ReferralLogs",
                columns: new[] { "ReferrerId", "BonusStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_IsRevoked_ExpiresAt",
                table: "RefreshTokens",
                columns: new[] { "UserId", "IsRevoked", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBalanceLedgers_SupplierBalanceId",
                table: "SupplierBalanceLedgers",
                column: "SupplierBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBalanceLedgers_SupplierId_CreatedAt",
                table: "SupplierBalanceLedgers",
                columns: new[] { "SupplierId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBalances_SupplierId",
                table: "SupplierBalances",
                column: "SupplierId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCallbacks_AttemptId",
                table: "SupplierCallbacks",
                column: "AttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCallbacks_CreatedAt_IsProcessed",
                table: "SupplierCallbacks",
                columns: new[] { "CreatedAt", "IsProcessed" },
                filter: "is_processed = false");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCallbacks_SupplierId",
                table: "SupplierCallbacks",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProducts_ProductId_Seq",
                table: "SupplierProducts",
                columns: new[] { "ProductId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProducts_ProductId_Seq_IsActive",
                table: "SupplierProducts",
                columns: new[] { "ProductId", "Seq", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProducts_ProductId_SupplierId",
                table: "SupplierProducts",
                columns: new[] { "ProductId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProducts_SupplierId",
                table: "SupplierProducts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Code",
                table: "Suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopupRequests_BankAccountId",
                table: "TopupRequests",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TopupRequests_Status_CreatedAt",
                table: "TopupRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TopupRequests_UserId_CreatedAt",
                table: "TopupRequests",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttempts_Status_AttemptedAt",
                table: "TransactionAttempts",
                columns: new[] { "Status", "AttemptedAt" },
                filter: "status IN ('pending', 'processing')");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttempts_SupplierId",
                table: "TransactionAttempts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttempts_SupplierProductId",
                table: "TransactionAttempts",
                column: "SupplierProductId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttempts_SupplierRefId",
                table: "TransactionAttempts",
                column: "SupplierRefId",
                filter: "supplier_ref_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttempts_TransactionId",
                table: "TransactionAttempts",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttempts_TransactionId_Seq",
                table: "TransactionAttempts",
                columns: new[] { "TransactionId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ProductId",
                table: "Transactions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReferenceId",
                table: "Transactions",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Status_CreatedAt",
                table: "Transactions",
                columns: new[] { "Status", "CreatedAt" },
                filter: "status IN ('pending', 'processing')");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SupplierId",
                table: "Transactions",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_CreatedAt",
                table: "Transactions",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_FcmToken",
                table: "UserDevices",
                column: "FcmToken");

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_IsActive",
                table: "UserDevices",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_UserId_FcmToken",
                table: "UserDevices",
                columns: new[] { "UserId", "FcmToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLevelConfigs_LevelId_ConfigKey",
                table: "UserLevelConfigs",
                columns: new[] { "LevelId", "ConfigKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLevels_Name",
                table: "UserLevels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_LevelId",
                table: "Users",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferralCode",
                table: "Users",
                column: "ReferralCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUsers");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BalanceLedgers");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys");

            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "PeerTransfers");

            migrationBuilder.DropTable(
                name: "PinResetTokens");

            migrationBuilder.DropTable(
                name: "ProductLevelPrices");

            migrationBuilder.DropTable(
                name: "ReferralLogs");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "SupplierBalanceLedgers");

            migrationBuilder.DropTable(
                name: "SupplierCallbacks");

            migrationBuilder.DropTable(
                name: "TopupRequests");

            migrationBuilder.DropTable(
                name: "UserBalances");

            migrationBuilder.DropTable(
                name: "UserDevices");

            migrationBuilder.DropTable(
                name: "UserLevelConfigs");

            migrationBuilder.DropTable(
                name: "SupplierBalances");

            migrationBuilder.DropTable(
                name: "TransactionAttempts");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "SupplierProducts");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropTable(
                name: "UserLevels");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:admin_role", "superadmin,admin,finance,staff")
                .OldAnnotation("Npgsql:Enum:attempt_status", "pending,processing,success,failed,timeout")
                .OldAnnotation("Npgsql:Enum:balance_tx_type", "topup,purchase_hold,purchase_debit,purchase_release,transfer_out,transfer_in,refund,adjustment")
                .OldAnnotation("Npgsql:Enum:markup_type", "percentage,fixed")
                .OldAnnotation("Npgsql:Enum:notification_channel", "email,sms,whatsapp,push")
                .OldAnnotation("Npgsql:Enum:referral_bonus_status", "pending,paid,cancelled")
                .OldAnnotation("Npgsql:Enum:topup_status", "pending,approved,rejected")
                .OldAnnotation("Npgsql:Enum:transaction_status", "pending,processing,success,failed,refunded,cancelled")
                .OldAnnotation("Npgsql:Enum:user_status", "active,inactive,suspended");
        }
    }
}
