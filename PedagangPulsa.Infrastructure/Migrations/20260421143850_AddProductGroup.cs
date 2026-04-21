using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedagangPulsa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Table "ProductGroups" and column "Products.ProductGroupId" already exist in DB.
            // Only create indexes and foreign keys that are missing.

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Products_ProductGroupId"" ON ""Products"" (""ProductGroupId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductGroups_CategoryId"" ON ""ProductGroups"" (""CategoryId"");
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Products_ProductGroups_ProductGroupId'
                    ) THEN
                        ALTER TABLE ""Products""
                            ADD CONSTRAINT ""FK_Products_ProductGroups_ProductGroupId""
                            FOREIGN KEY (""ProductGroupId"") REFERENCES ""ProductGroups""(""Id"");
                    END IF;
                END
                $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_ProductGroups_ProductCategories_CategoryId'
                    ) THEN
                        ALTER TABLE ""ProductGroups""
                            ADD CONSTRAINT ""FK_ProductGroups_ProductCategories_CategoryId""
                            FOREIGN KEY (""CategoryId"") REFERENCES ""ProductCategories""(""Id"") ON DELETE CASCADE;
                    END IF;
                END
                $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Products"" DROP CONSTRAINT IF EXISTS ""FK_Products_ProductGroups_ProductGroupId"";
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""ProductGroups"" DROP CONSTRAINT IF EXISTS ""FK_ProductGroups_ProductCategories_CategoryId"";
            ");

            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_Products_ProductGroupId"";
            ");

            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_ProductGroups_CategoryId"";
            ");
        }
    }
}
