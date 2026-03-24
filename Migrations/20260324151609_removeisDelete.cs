using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class removeisDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDelete",
                table: "Patients");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 16, 9, 348, DateTimeKind.Local).AddTicks(2112));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 16, 9, 348, DateTimeKind.Local).AddTicks(2326));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 16, 9, 348, DateTimeKind.Local).AddTicks(2331));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 16, 9, 348, DateTimeKind.Local).AddTicks(2333));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 16, 9, 348, DateTimeKind.Local).AddTicks(2335));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 16, 9, 348, DateTimeKind.Local).AddTicks(2337));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 16, 9, 348, DateTimeKind.Local).AddTicks(2339));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 16, 9, 348, DateTimeKind.Local).AddTicks(2272));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 16, 9, 348, DateTimeKind.Local).AddTicks(2301));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 16, 9, 348, DateTimeKind.Local).AddTicks(2303));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDelete",
                table: "Patients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 9, 53, 688, DateTimeKind.Local).AddTicks(1597));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 9, 53, 688, DateTimeKind.Local).AddTicks(1835));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 9, 53, 688, DateTimeKind.Local).AddTicks(1841));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 9, 53, 688, DateTimeKind.Local).AddTicks(1843));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 9, 53, 688, DateTimeKind.Local).AddTicks(1844));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 9, 53, 688, DateTimeKind.Local).AddTicks(1846));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 9, 53, 688, DateTimeKind.Local).AddTicks(1848));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 9, 53, 688, DateTimeKind.Local).AddTicks(1809));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 9, 53, 688, DateTimeKind.Local).AddTicks(1813));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 9, 53, 688, DateTimeKind.Local).AddTicks(1815));
        }
    }
}
