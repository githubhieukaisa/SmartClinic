using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStatusFromDoctorShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DoctorShifts_Rooms_RoomId",
                table: "DoctorShifts");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorShifts_Users_DoctorId",
                table: "DoctorShifts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DoctorShifts",
                table: "DoctorShifts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DoctorShifts");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "DoctorShifts",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AddColumn<byte>(
                name: "StatusEnum",
                table: "DoctorShifts",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddPrimaryKey(
                name: "DoctorShifts_pkey",
                table: "DoctorShifts",
                column: "Id");

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

            migrationBuilder.AddForeignKey(
                name: "DoctorShifts_DoctorId_fkey",
                table: "DoctorShifts",
                column: "DoctorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "DoctorShifts_RoomId_fkey",
                table: "DoctorShifts",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "DoctorShifts_DoctorId_fkey",
                table: "DoctorShifts");

            migrationBuilder.DropForeignKey(
                name: "DoctorShifts_RoomId_fkey",
                table: "DoctorShifts");

            migrationBuilder.DropPrimaryKey(
                name: "DoctorShifts_pkey",
                table: "DoctorShifts");

            migrationBuilder.DropColumn(
                name: "StatusEnum",
                table: "DoctorShifts");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "DoctorShifts",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "DoctorShifts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DoctorShifts",
                table: "DoctorShifts",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 44, 35, 267, DateTimeKind.Local).AddTicks(5022));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 44, 35, 267, DateTimeKind.Local).AddTicks(5399));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 44, 35, 267, DateTimeKind.Local).AddTicks(5406));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 44, 35, 267, DateTimeKind.Local).AddTicks(5409));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 44, 35, 267, DateTimeKind.Local).AddTicks(5411));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 44, 35, 267, DateTimeKind.Local).AddTicks(5414));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 44, 35, 267, DateTimeKind.Local).AddTicks(5416));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 44, 35, 267, DateTimeKind.Local).AddTicks(5300));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 44, 35, 267, DateTimeKind.Local).AddTicks(5305));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 44, 35, 267, DateTimeKind.Local).AddTicks(5308));

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorShifts_Rooms_RoomId",
                table: "DoctorShifts",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorShifts_Users_DoctorId",
                table: "DoctorShifts",
                column: "DoctorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
