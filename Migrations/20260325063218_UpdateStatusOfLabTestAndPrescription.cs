using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStatusOfLabTestAndPrescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ==========================================
            // 1. Chèn SQL cập nhật dữ liệu Status trước khi Alter kiểu dữ liệu
            // ==========================================
            migrationBuilder.Sql(@"
                UPDATE ""Prescriptions"" SET ""Status"" = '0' WHERE ""Status"" = 'Pending';
                UPDATE ""Prescriptions"" SET ""Status"" = '1' WHERE ""Status"" = 'Dispensed';
                UPDATE ""Prescriptions"" SET ""Status"" = '2' WHERE ""Status"" = 'Cancelled';
                UPDATE ""Prescriptions"" SET ""Status"" = '3' WHERE ""Status"" = 'Paid';
                
                ALTER TABLE ""Prescriptions"" ALTER COLUMN ""Status"" DROP DEFAULT;
                ALTER TABLE ""Prescriptions"" ALTER COLUMN ""Status"" TYPE smallint USING ""Status""::smallint;
            ");

            migrationBuilder.AlterColumn<short>(
                name: "Status",
                table: "Prescriptions",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true,
                oldDefaultValueSql: "'Pending'::character varying");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PrescriptionDetails",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql(@"
                UPDATE ""LabOrders"" SET ""Status"" = '0' WHERE ""Status"" = 'Pending';
                UPDATE ""LabOrders"" SET ""Status"" = '2' WHERE ""Status"" = 'Done';
                
                ALTER TABLE ""LabOrders"" ALTER COLUMN ""Status"" DROP DEFAULT;
                ALTER TABLE ""LabOrders"" ALTER COLUMN ""Status"" TYPE smallint USING ""Status""::smallint;
            ");

            migrationBuilder.AlterColumn<short>(
                name: "Status",
                table: "LabOrders",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Pending'::character varying");


            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "LabOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "LabTests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // ==========================================
            // 2. Tạo bảng LabPrices trước rồi copy Price sang
            // ==========================================
            migrationBuilder.CreateTable(
                name: "LabPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LabTestId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("LabPrices_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "LabPrices_LabTestId_fkey",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabPrices_LabTestId",
                table: "LabPrices",
                column: "LabTestId");

            // ==========================================
            // 3. COPY DATA XONG MỚI XÓA CỘT PRICE
            // ==========================================
            migrationBuilder.Sql(@"
                INSERT INTO ""LabPrices"" (""LabTestId"", ""Price"", ""EffectiveDate"", ""CreatedAt"")
                SELECT ""Id"", ""Price"", CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM ""LabTests"";
            ");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "LabTests");

            // ==========================================
            // 4. Update seed data
            // ==========================================
            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 13, 32, 17, 732, DateTimeKind.Local).AddTicks(5525));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "IsDeleted" },
                values: new object[] { new DateTime(2026, 3, 25, 13, 32, 17, 732, DateTimeKind.Local).AddTicks(5691), false });

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "IsDeleted" },
                values: new object[] { new DateTime(2026, 3, 25, 13, 32, 17, 732, DateTimeKind.Local).AddTicks(5694), false });

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "IsDeleted" },
                values: new object[] { new DateTime(2026, 3, 25, 13, 32, 17, 732, DateTimeKind.Local).AddTicks(5696), false });

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "IsDeleted" },
                values: new object[] { new DateTime(2026, 3, 25, 13, 32, 17, 732, DateTimeKind.Local).AddTicks(5697), false });

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "IsDeleted" },
                values: new object[] { new DateTime(2026, 3, 25, 13, 32, 17, 732, DateTimeKind.Local).AddTicks(5699), false });

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "IsDeleted" },
                values: new object[] { new DateTime(2026, 3, 25, 13, 32, 17, 732, DateTimeKind.Local).AddTicks(5700), false });

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 13, 32, 17, 732, DateTimeKind.Local).AddTicks(5666));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 13, 32, 17, 732, DateTimeKind.Local).AddTicks(5671));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 13, 32, 17, 732, DateTimeKind.Local).AddTicks(5673));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabPrices");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PrescriptionDetails");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "LabTests");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "LabOrders");


            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Prescriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                defaultValueSql: "'Pending'::character varying",
                oldClrType: typeof(short),
                oldType: "smallint",
                oldDefaultValue: (short)0);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "LabTests",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "LabOrders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Pending'::character varying",
                oldClrType: typeof(short),
                oldType: "smallint",
                oldDefaultValue: (short)0);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 23, 16, 10, 19, 652, DateTimeKind.Local).AddTicks(9916));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "Price" },
                values: new object[] { new DateTime(2026, 3, 23, 16, 10, 19, 653, DateTimeKind.Local).AddTicks(62), 150000m });

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "Price" },
                values: new object[] { new DateTime(2026, 3, 23, 16, 10, 19, 653, DateTimeKind.Local).AddTicks(69), 50000m });

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "Price" },
                values: new object[] { new DateTime(2026, 3, 23, 16, 10, 19, 653, DateTimeKind.Local).AddTicks(70), 250000m });

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "Price" },
                values: new object[] { new DateTime(2026, 3, 23, 16, 10, 19, 653, DateTimeKind.Local).AddTicks(72), 200000m });

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "Price" },
                values: new object[] { new DateTime(2026, 3, 23, 16, 10, 19, 653, DateTimeKind.Local).AddTicks(74), 150000m });

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "Price" },
                values: new object[] { new DateTime(2026, 3, 23, 16, 10, 19, 653, DateTimeKind.Local).AddTicks(75), 120000m });

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 23, 16, 10, 19, 653, DateTimeKind.Local).AddTicks(40));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 23, 16, 10, 19, 653, DateTimeKind.Local).AddTicks(43));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 23, 16, 10, 19, 653, DateTimeKind.Local).AddTicks(45));
        }
    }
}
