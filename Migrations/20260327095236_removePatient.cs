using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class removePatient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "QueueTickets_PatientId_fkey",
                table: "QueueTickets");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DoB",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 16, 52, 31, 241, DateTimeKind.Local).AddTicks(1550));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 16, 52, 31, 241, DateTimeKind.Local).AddTicks(1740));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 16, 52, 31, 241, DateTimeKind.Local).AddTicks(1744));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 16, 52, 31, 241, DateTimeKind.Local).AddTicks(1747));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 16, 52, 31, 241, DateTimeKind.Local).AddTicks(1749));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 16, 52, 31, 241, DateTimeKind.Local).AddTicks(1752));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 16, 52, 31, 241, DateTimeKind.Local).AddTicks(1754));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 16, 52, 31, 241, DateTimeKind.Local).AddTicks(1702));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 16, 52, 31, 241, DateTimeKind.Local).AddTicks(1707));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 16, 52, 31, 241, DateTimeKind.Local).AddTicks(1710));

            // 1) Tạo map tạm Patient.Id -> User.Id
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE patient_user_map (
                    ""PatientId"" integer PRIMARY KEY,
                    ""UserId"" integer NOT NULL
                ) ON COMMIT DROP;
            ");

            // 2) Ưu tiên map sang user bệnh nhân đã tồn tại (theo PhoneNumber)
            migrationBuilder.Sql(@"
                INSERT INTO patient_user_map(""PatientId"", ""UserId"")
                SELECT p.""Id"", u.""Id""
                FROM ""Patients"" p
                JOIN LATERAL (
                    SELECT u0.""Id""
                    FROM ""Users"" u0
                    WHERE p.""Phone"" IS NOT NULL
                      AND u0.""PhoneNumber"" = p.""Phone""
                      AND (u0.""RoleMask"" & 128) = 128
                    ORDER BY u0.""Id""
                    LIMIT 1
                ) u ON TRUE;
            ");

            // Đồng bộ sequence Users.Id để tránh đụng khóa chính khi insert dữ liệu migrate
            migrationBuilder.Sql(@"
                SELECT setval(
                    pg_get_serial_sequence('""Users""', 'Id'),
                    GREATEST((SELECT COALESCE(MAX(""Id""), 0) FROM ""Users""), 1),
                    true
                );
            ");

            // 3) Tạo mới User cho các Patient chưa map
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    p_record RECORD;
                    new_user_id integer;
                BEGIN
                    FOR p_record IN
                        SELECT p.*
                        FROM ""Patients"" p
                        LEFT JOIN patient_user_map m ON m.""PatientId"" = p.""Id""
                        WHERE m.""PatientId"" IS NULL
                    LOOP
                        INSERT INTO ""Users"" (
                            ""Username"", ""PasswordHash"", ""FullName"", ""Email"", ""PhoneNumber"", ""Address"",
                            ""Gender"", ""DoB"", ""RoleMask"", ""IsActive"", ""CreatedAt""
                        )
                        VALUES (
                            CONCAT('patient_', p_record.""Id"", '_', SUBSTRING(MD5(RANDOM()::text), 1, 8)),
                            MD5(RANDOM()::text),
                            p_record.""FullName"",
                            NULL,
                            p_record.""Phone"",
                            p_record.""Address"",
                            CASE WHEN (p_record.""Flags"" & 1) = 1 THEN TRUE ELSE FALSE END,
                            p_record.""DoB"",
                            128,
                            TRUE,
                            p_record.""CreatedAt""
                        )
                        RETURNING ""Id"" INTO new_user_id;

                        INSERT INTO patient_user_map(""PatientId"", ""UserId"")
                        VALUES (p_record.""Id"", new_user_id);
                    END LOOP;
                END $$;
            ");

            // 4) Đổi QueueTickets.PatientId từ Patient.Id sang User.Id mới
            migrationBuilder.Sql(@"
                UPDATE ""QueueTickets"" q
                SET ""PatientId"" = m.""UserId""
                FROM patient_user_map m
                WHERE q.""PatientId"" = m.""PatientId"";
            ");

            // 5) Dọn orphan nếu có (đảm bảo add FK không fail)
            migrationBuilder.Sql(@"
                UPDATE ""QueueTickets"" q
                SET ""PatientId"" = NULL
                WHERE q.""PatientId"" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM ""Users"" u WHERE u.""Id"" = q.""PatientId""
                  );
            ");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.AddForeignKey(
                name: "QueueTickets_PatientId_fkey",
                table: "QueueTickets",
                column: "PatientId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "QueueTickets_PatientId_fkey",
                table: "QueueTickets");

            migrationBuilder.DropColumn(
                name: "DoB",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DoB = table.Column<DateOnly>(type: "date", nullable: true),
                    Flags = table.Column<short>(type: "smallint", nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Patients_pkey", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 22, 43, 8, 267, DateTimeKind.Local).AddTicks(7028));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 22, 43, 8, 267, DateTimeKind.Local).AddTicks(7247));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 22, 43, 8, 267, DateTimeKind.Local).AddTicks(7251));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 22, 43, 8, 267, DateTimeKind.Local).AddTicks(7253));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 22, 43, 8, 267, DateTimeKind.Local).AddTicks(7254));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 22, 43, 8, 267, DateTimeKind.Local).AddTicks(7256));

            migrationBuilder.UpdateData(
                table: "LabTests",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 22, 43, 8, 267, DateTimeKind.Local).AddTicks(7258));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 22, 43, 8, 267, DateTimeKind.Local).AddTicks(7218));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 22, 43, 8, 267, DateTimeKind.Local).AddTicks(7222));

            migrationBuilder.UpdateData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 26, 22, 43, 8, 267, DateTimeKind.Local).AddTicks(7224));

            migrationBuilder.AddForeignKey(
                name: "QueueTickets_PatientId_fkey",
                table: "QueueTickets",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
