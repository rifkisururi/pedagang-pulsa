using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedagangPulsa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Columns already exist in DB - use IF NOT EXISTS
            migrationBuilder.Sql(@"
                ALTER TABLE ""Products"" ADD COLUMN IF NOT EXISTS ""QuotaMb"" integer;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Products"" ADD COLUMN IF NOT EXISTS ""QuotaText"" text;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuotaMb",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "QuotaText",
                table: "Products");
        }
    }
}
