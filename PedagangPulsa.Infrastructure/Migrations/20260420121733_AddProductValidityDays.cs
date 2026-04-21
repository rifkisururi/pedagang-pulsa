using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedagangPulsa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductValidityDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Columns already exist in DB - use IF NOT EXISTS / IF EXISTS

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_SupplierProducts_Products_ProductId'
                    ) THEN
                        ALTER TABLE ""SupplierProducts"" DROP CONSTRAINT ""FK_SupplierProducts_Products_ProductId"";
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Products"" ADD COLUMN IF NOT EXISTS ""ValidityDays"" integer;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Products"" ADD COLUMN IF NOT EXISTS ""ValidityText"" text;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_SupplierProducts_Products_ProductId'
                    ) THEN
                        ALTER TABLE ""SupplierProducts""
                            ADD CONSTRAINT ""FK_SupplierProducts_Products_ProductId""
                            FOREIGN KEY (""ProductId"") REFERENCES ""Products""(""Id"") ON DELETE RESTRICT;
                    END IF;
                END
                $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""SupplierProducts"" DROP CONSTRAINT IF EXISTS ""FK_SupplierProducts_Products_ProductId"";
            ");

            migrationBuilder.DropColumn(
                name: "ValidityDays",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ValidityText",
                table: "Products");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_SupplierProducts_Products_ProductId'
                    ) THEN
                        ALTER TABLE ""SupplierProducts""
                            ADD CONSTRAINT ""FK_SupplierProducts_Products_ProductId""
                            FOREIGN KEY (""ProductId"") REFERENCES ""Products""(""Id"") ON DELETE CASCADE;
                    END IF;
                END
                $$;
            ");
        }
    }
}
