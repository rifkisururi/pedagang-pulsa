using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PedagangPulsa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4_AddProfitViewsAndSupplierFunctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierTrxId",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "TopupRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedBy",
                table: "TopupRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "ReferralLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "ReferralLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledBy",
                table: "ReferralLogs",
                type: "uuid",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SupplierId",
                table: "Transactions",
                column: "SupplierId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Suppliers_SupplierId",
                table: "Transactions",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id");

            // Create Profit Views
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW v_profit_daily AS
                SELECT
                    DATE(t.completed_at) as profit_date,
                    COUNT(*) as total_transactions,
                    SUM(t.sell_price) as total_revenue,
                    SUM(t.cost_price) as total_cost,
                    SUM(t.sell_price - t.cost_price) as total_profit,
                    AVG(t.sell_price - t.cost_price) as avg_profit_per_trx
                FROM transactions t
                WHERE t.status = 'success'
                    AND t.completed_at IS NOT NULL
                GROUP BY DATE(t.completed_at)
                ORDER BY profit_date DESC;
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW v_profit_by_supplier AS
                SELECT
                    s.id as supplier_id,
                    s.name as supplier_name,
                    s.code as supplier_code,
                    COUNT(t.id) as total_transactions,
                    SUM(t.cost_price) as total_cost,
                    SUM(t.sell_price) as total_revenue,
                    SUM(t.sell_price - t.cost_price) as total_profit
                FROM transactions t
                INNER JOIN suppliers s ON t.supplier_id = s.id
                WHERE t.status = 'success'
                GROUP BY s.id, s.name, s.code
                ORDER BY total_profit DESC;
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW v_profit_by_product AS
                SELECT
                    p.id as product_id,
                    p.name as product_name,
                    p.category,
                    COUNT(t.id) as total_transactions,
                    SUM(t.sell_price) as total_revenue,
                    SUM(t.cost_price) as total_cost,
                    SUM(t.sell_price - t.cost_price) as total_profit
                FROM transactions t
                INNER JOIN products p ON t.product_id = p.id
                WHERE t.status = 'success'
                GROUP BY p.id, p.name, p.category
                ORDER BY total_profit DESC;
            ");

            // Create Supplier Balance Functions
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION debit_supplier_balance(
                    p_supplier_id INTEGER,
                    p_amount NUMERIC(15,2),
                    p_type TEXT,
                    p_ref_type TEXT DEFAULT NULL,
                    p_ref_id UUID DEFAULT NULL,
                    p_notes TEXT DEFAULT NULL,
                    p_performed_by TEXT DEFAULT NULL
                )
                RETURNS BOOLEAN AS $$
                DECLARE
                    v_current_balance NUMERIC(15,2);
                BEGIN
                    -- Lock and get current balance
                    SELECT active_balance INTO v_current_balance
                    FROM supplier_balances
                    WHERE supplier_id = p_supplier_id
                    FOR UPDATE;

                    -- Check if balance exists
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Supplier balance not found for supplier_id: %', p_supplier_id;
                    END IF;

                    -- Check sufficient balance
                    IF v_current_balance < p_amount THEN
                        RAISE EXCEPTION 'Insufficient balance. Current: %, Required: %', v_current_balance, p_amount;
                    END IF;

                    -- Deduct balance
                    UPDATE supplier_balances
                    SET active_balance = active_balance - p_amount,
                        updated_at = NOW()
                    WHERE supplier_id = p_supplier_id;

                    -- Record in ledger
                    INSERT INTO supplier_balance_ledgers (
                        supplier_id, type, amount, balance_before, balance_after,
                        description, admin_note, performed_by, created_at
                    )
                    VALUES (
                        p_supplier_id, p_type, p_amount, v_current_balance, (v_current_balance - p_amount),
                        p_ref_type, p_notes, p_performed_by, NOW()
                    );

                    RETURN TRUE;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION credit_supplier_balance(
                    p_supplier_id INTEGER,
                    p_amount NUMERIC(15,2),
                    p_type TEXT,
                    p_ref_type TEXT DEFAULT NULL,
                    p_ref_id UUID DEFAULT NULL,
                    p_notes TEXT DEFAULT NULL,
                    p_performed_by TEXT DEFAULT NULL
                )
                RETURNS BOOLEAN AS $$
                DECLARE
                    v_current_balance NUMERIC(15,2);
                BEGIN
                    -- Lock and get current balance, or create if not exists
                    SELECT active_balance INTO v_current_balance
                    FROM supplier_balances
                    WHERE supplier_id = p_supplier_id
                    FOR UPDATE;

                    -- Create balance if not exists
                    IF NOT FOUND THEN
                        INSERT INTO supplier_balances (supplier_id, active_balance, created_at, updated_at)
                        VALUES (p_supplier_id, 0, NOW(), NOW());
                        v_current_balance := 0;
                    END IF;

                    -- Add balance
                    UPDATE supplier_balances
                    SET active_balance = active_balance + p_amount,
                        updated_at = NOW()
                    WHERE supplier_id = p_supplier_id;

                    -- Record in ledger
                    INSERT INTO supplier_balance_ledgers (
                        supplier_id, type, amount, balance_before, balance_after,
                        description, admin_note, performed_by, created_at
                    )
                    VALUES (
                        p_supplier_id, p_type, p_amount, v_current_balance, (v_current_balance + p_amount),
                        p_ref_type, p_notes, p_performed_by, NOW()
                    );

                    RETURN TRUE;
                END;
                $$ LANGUAGE plpgsql;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop Supplier Balance Functions
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS debit_supplier_balance(INTEGER, NUMERIC(15,2), TEXT, TEXT, UUID, TEXT, TEXT);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS credit_supplier_balance(INTEGER, NUMERIC(15,2), TEXT, TEXT, UUID, TEXT, TEXT);");

            // Drop Profit Views
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_profit_by_product;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_profit_by_supplier;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_profit_daily;");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Suppliers_SupplierId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "SupplierBalanceLedgers");

            migrationBuilder.DropTable(
                name: "SupplierBalances");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_SupplierId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SupplierTrxId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "TopupRequests");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                table: "TopupRequests");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "ReferralLogs");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "ReferralLogs");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "ReferralLogs");
        }
    }
}
