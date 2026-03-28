using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class UpdateShiftSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "DoctorShifts");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "DoctorShifts");

            migrationBuilder.DropColumn(
                name: "StatusEnum",
                table: "DoctorShifts");

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "DoctorShifts",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                table: "DoctorShifts",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ShiftDefinitionId",
                table: "DoctorShifts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ShiftDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ShiftDefinitions_pkey", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Slots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DoctorShiftId = table.Column<int>(type: "integer", nullable: false),
                    SlotNumber = table.Column<int>(type: "integer", nullable: false),
                    IsBooked = table.Column<bool>(type: "boolean", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Slots_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Slots_DoctorShiftId_fkey",
                        column: x => x.DoctorShiftId,
                        principalTable: "DoctorShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "Slots_PatientId_fkey",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });



            migrationBuilder.InsertData(
                table: "ShiftDefinitions",
                columns: new[] { "Id", "CreatedAt", "EndTime", "Name", "SortOrder", "StartTime" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 28, 9, 31, 19, 386, DateTimeKind.Local).AddTicks(5664), new TimeSpan(0, 11, 30, 0, 0), "Ca Sáng", 1, new TimeSpan(0, 7, 30, 0, 0) },
                    { 2, new DateTime(2026, 3, 28, 9, 31, 19, 386, DateTimeKind.Local).AddTicks(5677), new TimeSpan(0, 17, 30, 0, 0), "Ca Chiều", 2, new TimeSpan(0, 13, 30, 0, 0) },
                    { 3, new DateTime(2026, 3, 28, 9, 31, 19, 386, DateTimeKind.Local).AddTicks(5679), new TimeSpan(0, 21, 0, 0, 0), "Ca Tối", 3, new TimeSpan(0, 18, 0, 0, 0) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorShifts_ShiftDefinitionId",
                table: "DoctorShifts",
                column: "ShiftDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Slots_DoctorShiftId",
                table: "Slots",
                column: "DoctorShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Slots_PatientId",
                table: "Slots",
                column: "PatientId");

            migrationBuilder.AddForeignKey(
                name: "DoctorShifts_ShiftDefinitionId_fkey",
                table: "DoctorShifts",
                column: "ShiftDefinitionId",
                principalTable: "ShiftDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "DoctorShifts_ShiftDefinitionId_fkey",
                table: "DoctorShifts");

            migrationBuilder.DropTable(
                name: "ShiftDefinitions");

            migrationBuilder.DropTable(
                name: "Slots");

            migrationBuilder.DropIndex(
                name: "IX_DoctorShifts_ShiftDefinitionId",
                table: "DoctorShifts");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "DoctorShifts");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "DoctorShifts");

            migrationBuilder.DropColumn(
                name: "ShiftDefinitionId",
                table: "DoctorShifts");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndTime",
                table: "DoctorShifts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartTime",
                table: "DoctorShifts",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<byte>(
                name: "StatusEnum",
                table: "DoctorShifts",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);


        }
    }
}
