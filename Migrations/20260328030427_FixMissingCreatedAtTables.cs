using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartClinic.Migrations
{
    /// <inheritdoc />
    public partial class FixMissingCreatedAtTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var tables = new[] { "Rooms", "Departments", "LabTests", "LabOrders", "LabOrderDetails", "Medicines", "MedicinePrices", "Patients", "Prescriptions", "PrescriptionDetails", "QueueTickets" };
            foreach (var table in tables)
            {
                migrationBuilder.Sql($@"
                    DO $$ 
                    BEGIN 
                        IF NOT EXISTS (
                            SELECT 1 
                            FROM information_schema.columns 
                            WHERE table_name='{table}' AND column_name='CreatedAt'
                        ) THEN 
                            ALTER TABLE ""{table}"" ADD COLUMN ""CreatedAt"" timestamp without time zone DEFAULT CURRENT_TIMESTAMP;
                        END IF; 
                    END $$;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
