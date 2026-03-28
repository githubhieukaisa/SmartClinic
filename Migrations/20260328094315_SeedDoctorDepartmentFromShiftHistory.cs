using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class SeedDoctorDepartmentFromShiftHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 28, 16, 43, 15, 457, DateTimeKind.Local).AddTicks(6093));

            // Gán DepartmentId cho bác sĩ dựa trên lịch sử ca trực:
            // Lấy khoa mà bác sĩ trực nhiều nhất (most common department from DoctorShift → Room → Department)
            migrationBuilder.Sql(@"
                UPDATE ""Users"" u
                SET ""DepartmentId"" = sub.""DepartmentId""
                FROM (
                    SELECT ds.""DoctorId"", r.""DepartmentId"",
                           ROW_NUMBER() OVER (PARTITION BY ds.""DoctorId"" ORDER BY COUNT(*) DESC) as rn
                    FROM ""DoctorShifts"" ds
                    JOIN ""Rooms"" r ON ds.""RoomId"" = r.""Id""
                    GROUP BY ds.""DoctorId"", r.""DepartmentId""
                ) sub
                WHERE u.""Id"" = sub.""DoctorId""
                  AND sub.rn = 1
                  AND u.""DepartmentId"" IS NULL
                  AND (u.""RoleMask"" & 2) = 2;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 28, 16, 1, 22, 126, DateTimeKind.Local).AddTicks(9252));
        }
    }
}
