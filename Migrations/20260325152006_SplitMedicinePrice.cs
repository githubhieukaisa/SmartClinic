using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class SplitMedicinePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicinePrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("MedicinePrices_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "MedicinePrices_MedicineId_fkey",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicinePrices_MedicineId",
                table: "MedicinePrices",
                column: "MedicineId");

            // Migrate data from Medicines to MedicinePrices BEFORE dropping Price
            migrationBuilder.Sql(
                "INSERT INTO \"MedicinePrices\" (\"MedicineId\", \"Price\", \"EffectiveFrom\", \"CreatedAt\") SELECT \"Id\", \"Price\", CURRENT_TIMESTAMP, CURRENT_TIMESTAMP FROM \"Medicines\";"
            );

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Medicines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicinePrices");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Medicines",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Restore data from MedicinePrices to Medicines
            migrationBuilder.Sql(
                "UPDATE \"Medicines\" SET \"Price\" = COALESCE((SELECT \"Price\" FROM \"MedicinePrices\" WHERE \"MedicinePrices\".\"MedicineId\" = \"Medicines\".\"Id\" ORDER BY \"EffectiveFrom\" DESC LIMIT 1), 0);"
            );
        }
    }
}
