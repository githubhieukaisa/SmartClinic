using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomFlagsColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Flags",
                table: "Rooms",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 25, 36, 26, DateTimeKind.Local).AddTicks(594));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 25, 36, 26, DateTimeKind.Local).AddTicks(887));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 25, 36, 26, DateTimeKind.Local).AddTicks(894));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 25, 36, 26, DateTimeKind.Local).AddTicks(897));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 25, 36, 26, DateTimeKind.Local).AddTicks(899));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 25, 36, 26, DateTimeKind.Local).AddTicks(901));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 22, 25, 36, 26, DateTimeKind.Local).AddTicks(903));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "Flags" },
                values: new object[] { new DateTime(2026, 3, 24, 22, 25, 36, 26, DateTimeKind.Local).AddTicks(852), (byte)3 });

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "Flags" },
                values: new object[] { new DateTime(2026, 3, 24, 22, 25, 36, 26, DateTimeKind.Local).AddTicks(855), (byte)3 });

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "Flags" },
                values: new object[] { new DateTime(2026, 3, 24, 22, 25, 36, 26, DateTimeKind.Local).AddTicks(857), (byte)3 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Flags",
                table: "Rooms");

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
    }
}
