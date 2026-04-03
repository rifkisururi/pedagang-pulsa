using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedagangPulsa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCodeToTopupRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add UniqueCode column to TopupRequests table
            migrationBuilder.AddColumn<int>(
                name: "UniqueCode",
                table: "TopupRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UniqueCode",
                table: "TopupRequests");
        }
    }
}
