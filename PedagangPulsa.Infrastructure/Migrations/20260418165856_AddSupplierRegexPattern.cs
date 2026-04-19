using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PedagangPulsa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierRegexPattern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierRegexPatterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    SeqNo = table.Column<int>(type: "integer", nullable: false),
                    IsTrxSukses = table.Column<bool>(type: "boolean", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Regex = table.Column<string>(type: "text", nullable: false),
                    SampleMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierRegexPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierRegexPatterns_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRegexPatterns_SupplierId",
                table: "SupplierRegexPatterns",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRegexPatterns_SupplierId_SeqNo",
                table: "SupplierRegexPatterns",
                columns: new[] { "SupplierId", "SeqNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierRegexPatterns");
        }
    }
}
