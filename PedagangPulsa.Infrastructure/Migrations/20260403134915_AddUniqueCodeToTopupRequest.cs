using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedagangPulsa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCodeToTopupRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_ReferredBy",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ReferredBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReferredBy",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "UniqueCode",
                table: "TopupRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "RefId",
                table: "NotificationLogs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TransactionId",
                table: "IdempotencyKeys",
                type: "integer",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UniqueCode",
                table: "TopupRequests");

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredBy",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RefId",
                table: "NotificationLogs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TransactionId",
                table: "IdempotencyKeys",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferredBy",
                table: "Users",
                column: "ReferredBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_ReferredBy",
                table: "Users",
                column: "ReferredBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
