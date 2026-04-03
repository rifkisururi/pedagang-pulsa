using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PedagangPulsa.Infrastructure.Data;

#nullable disable

namespace PedagangPulsa.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260403153000_AddUserPasswordHash")]
    public partial class AddUserPasswordHash : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");
        }
    }
}
