using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedDataForNewRoom_Test_Department : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "Code", "CreatedAt", "Name" },
                values: new object[] { 8, "XN_CDHA", new DateTime(2026, 3, 22, 23, 9, 55, 998, DateTimeKind.Local).AddTicks(1675), "Xét nghiệm & Chẩn đoán hình ảnh" });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "CreatedAt", "DepartmentId", "IsActive", "IsLab", "Location", "Name" },
                values: new object[,]
                {
                    { 9, new DateTime(2026, 3, 22, 23, 9, 55, 998, DateTimeKind.Local).AddTicks(1840), 8, true, true, "Tầng 2", "Phòng Lấy Máu" },
                    { 10, new DateTime(2026, 3, 22, 23, 9, 55, 998, DateTimeKind.Local).AddTicks(1844), 8, true, true, "Tầng 2", "Phòng Siêu Âm" },
                    { 11, new DateTime(2026, 3, 22, 23, 9, 55, 998, DateTimeKind.Local).AddTicks(1846), 8, true, true, "Tầng 1", "Phòng X-Quang" }
                });

            migrationBuilder.InsertData(
                table: "LabTests",
                columns: new[] { "Id", "CreatedAt", "DefaultRoomId", "Description", "Name", "Price", "Unit" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 22, 23, 9, 55, 998, DateTimeKind.Local).AddTicks(1867), 9, "Xét nghiệm máu cơ bản", "Tổng phân tích tế bào máu", 150000m, "Lần" },
                    { 2, new DateTime(2026, 3, 22, 23, 9, 55, 998, DateTimeKind.Local).AddTicks(1874), 9, "Kiểm tra tiểu đường", "Đường huyết mao mạch", 50000m, "Lần" },
                    { 3, new DateTime(2026, 3, 22, 23, 9, 55, 998, DateTimeKind.Local).AddTicks(1876), 9, "AST, ALT, Creatinin, Ure...", "Sinh hóa máu (Chức năng Gan/Thận)", 250000m, "Lần" },
                    { 4, new DateTime(2026, 3, 22, 23, 9, 55, 998, DateTimeKind.Local).AddTicks(1877), 10, "Siêu âm màu", "Siêu âm ổ bụng tổng quát", 200000m, "Lần" },
                    { 5, new DateTime(2026, 3, 22, 23, 9, 55, 998, DateTimeKind.Local).AddTicks(1879), 10, "Siêu âm màu", "Siêu âm tuyến giáp", 150000m, "Lần" },
                    { 6, new DateTime(2026, 3, 22, 23, 9, 55, 998, DateTimeKind.Local).AddTicks(1880), 11, "Chụp X-quang phổi", "X-Quang ngực thẳng", 120000m, "Lần" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8);
        }
    }
}
