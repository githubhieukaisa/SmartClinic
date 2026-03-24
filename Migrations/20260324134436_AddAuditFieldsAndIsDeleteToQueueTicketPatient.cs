using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFieldsAndIsDeleteToQueueTicketPatient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "QueueTickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "QueueTickets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "QueueTickets",
                type: "integer",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_QueueTickets_CreatedBy",
                table: "QueueTickets",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTickets_UpdatedBy",
                table: "QueueTickets",
                column: "UpdatedBy");

            migrationBuilder.AddForeignKey(
                name: "QueueTickets_CreatedBy_fkey",
                table: "QueueTickets",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "QueueTickets_UpdatedBy_fkey",
                table: "QueueTickets",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "QueueTickets_CreatedBy_fkey",
                table: "QueueTickets");

            migrationBuilder.DropForeignKey(
                name: "QueueTickets_UpdatedBy_fkey",
                table: "QueueTickets");

            migrationBuilder.DropIndex(
                name: "IX_QueueTickets_CreatedBy",
                table: "QueueTickets");

            migrationBuilder.DropIndex(
                name: "IX_QueueTickets_UpdatedBy",
                table: "QueueTickets");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "QueueTickets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "QueueTickets");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "QueueTickets");

            migrationBuilder.DropColumn(
                name: "IsDelete",
                table: "Patients");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 31, 34, 913, DateTimeKind.Local).AddTicks(1451));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 31, 34, 913, DateTimeKind.Local).AddTicks(1654));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 31, 34, 913, DateTimeKind.Local).AddTicks(1660));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 31, 34, 913, DateTimeKind.Local).AddTicks(1663));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 31, 34, 913, DateTimeKind.Local).AddTicks(1665));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 31, 34, 913, DateTimeKind.Local).AddTicks(1668));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 31, 34, 913, DateTimeKind.Local).AddTicks(1670));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 31, 34, 913, DateTimeKind.Local).AddTicks(1622));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 31, 34, 913, DateTimeKind.Local).AddTicks(1627));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 20, 31, 34, 913, DateTimeKind.Local).AddTicks(1630));
        }
    }
}
