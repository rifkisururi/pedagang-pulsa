using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedagangPulsa.Infrastructure.Migrations
{
    public partial class RenameSellPriceToMargin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "sell_price",
                table: "product_level_prices",
                newName: "margin");

            migrationBuilder.Sql(@"
                UPDATE product_level_prices
                SET margin = 200
                WHERE margin IS NULL OR margin <= 0;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "margin",
                table: "product_level_prices",
                newName: "sell_price");
        }
    }
}
