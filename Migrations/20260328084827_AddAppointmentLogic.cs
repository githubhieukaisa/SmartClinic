using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DoctorShiftId",
                table: "QueueTickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainCapacity",
                table: "DoctorShifts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 28, 15, 48, 27, 256, DateTimeKind.Local).AddTicks(5738));

            migrationBuilder.CreateIndex(
                name: "IX_QueueTickets_DoctorShiftId",
                table: "QueueTickets",
                column: "DoctorShiftId");

            migrationBuilder.AddForeignKey(
                name: "QueueTickets_DoctorShiftId_fkey",
                table: "QueueTickets",
                column: "DoctorShiftId",
                principalTable: "DoctorShifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "QueueTickets_DoctorShiftId_fkey",
                table: "QueueTickets");

            migrationBuilder.DropIndex(
                name: "IX_QueueTickets_DoctorShiftId",
                table: "QueueTickets");

            migrationBuilder.DropColumn(
                name: "DoctorShiftId",
                table: "QueueTickets");

            migrationBuilder.DropColumn(
                name: "RemainCapacity",
                table: "DoctorShifts");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 28, 14, 58, 57, 99, DateTimeKind.Local).AddTicks(8477));
        }
    }
}
