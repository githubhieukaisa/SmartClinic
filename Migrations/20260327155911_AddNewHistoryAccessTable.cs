using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class AddNewHistoryAccessTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoryAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QueueTicketId = table.Column<int>(type: "integer", nullable: false),
                    IsUnlocked = table.Column<bool>(type: "boolean", nullable: false),
                    UnlockCode = table.Column<string>(type: "text", nullable: true),
                    CodeExpiryTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("HistoryAccess_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "HistoryAccess_QueueTicketId_fkey",
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
                value: new DateTime(2026, 3, 27, 22, 59, 11, 91, DateTimeKind.Local).AddTicks(7096));

            migrationBuilder.CreateIndex(
                name: "IX_HistoryAccesses_QueueTicketId",
                table: "HistoryAccesses",
                column: "QueueTicketId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoryAccesses");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 22, 34, 2, 176, DateTimeKind.Local).AddTicks(1703));
        }
    }
}
