using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class CleanHistoryAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodeExpiryTime",
                table: "HistoryAccesses");

            migrationBuilder.DropColumn(
                name: "UnlockCode",
                table: "HistoryAccesses");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 28, 0, 38, 42, 64, DateTimeKind.Local).AddTicks(1170));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CodeExpiryTime",
                table: "HistoryAccesses",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlockCode",
                table: "HistoryAccesses",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 22, 59, 11, 91, DateTimeKind.Local).AddTicks(7096));
        }
    }
}
