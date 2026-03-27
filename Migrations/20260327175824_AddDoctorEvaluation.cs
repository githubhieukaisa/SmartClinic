using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DoctorEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    QueueTicketId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    DoctorId = table.Column<int>(type: "integer", nullable: true),
                    Rating = table.Column<int>(type: "integer", maxLength: 1, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "DoctorEvaluations_DoctorId_fkey",
                        column: x => x.DoctorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "DoctorEvaluations_PatientId_fkey",
                        column: x => x.PatientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "DoctorEvaluations_QueueTicketId_fkey",
                        column: x => x.QueueTicketId,
                        principalTable: "QueueTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 28, 0, 58, 23, 260, DateTimeKind.Local).AddTicks(5600));

            migrationBuilder.CreateIndex(
                name: "IX_DoctorEvaluations_DoctorId",
                table: "DoctorEvaluations",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorEvaluations_PatientId",
                table: "DoctorEvaluations",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorEvaluations_QueueTicketId",
                table: "DoctorEvaluations",
                column: "QueueTicketId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoctorEvaluations");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 28, 0, 38, 42, 64, DateTimeKind.Local).AddTicks(1170));
        }
    }
}
