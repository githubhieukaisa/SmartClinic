using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class removeIsDeleteAndIsActiveChangeToFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "QueueTickets");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Patients");

            migrationBuilder.AddColumn<short>(
                name: "Flags",
                table: "Patients",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Flags",
                table: "Patients");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "QueueTickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Waiting'::character varying");

            migrationBuilder.AddColumn<bool>(
                name: "Gender",
                table: "Patients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 21, 45, 19, 515, DateTimeKind.Local).AddTicks(6763));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 21, 45, 19, 515, DateTimeKind.Local).AddTicks(7004));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 21, 45, 19, 515, DateTimeKind.Local).AddTicks(7012));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 21, 45, 19, 515, DateTimeKind.Local).AddTicks(7014));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 21, 45, 19, 515, DateTimeKind.Local).AddTicks(7017));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 21, 45, 19, 515, DateTimeKind.Local).AddTicks(7019));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 21, 45, 19, 515, DateTimeKind.Local).AddTicks(7021));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 21, 45, 19, 515, DateTimeKind.Local).AddTicks(6967));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 21, 45, 19, 515, DateTimeKind.Local).AddTicks(6973));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 21, 45, 19, 515, DateTimeKind.Local).AddTicks(6976));
        }
    }
}
