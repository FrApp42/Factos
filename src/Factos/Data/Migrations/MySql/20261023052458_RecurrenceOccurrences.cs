using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Factos.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class RecurrenceOccurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recurrence_occurrences",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    schedule_type = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    schedule_id = table.Column<long>(type: "bigint", nullable: false),
                    scheduled_for = table.Column<DateOnly>(type: "date", nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    record_type = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    record_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurrence_occurrences", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_recurrence_occurrences_record_type_record_id",
                table: "recurrence_occurrences",
                columns: new[] { "record_type", "record_id" });

            migrationBuilder.CreateIndex(
                name: "recurrence_occurrence_day",
                table: "recurrence_occurrences",
                columns: new[] { "schedule_type", "schedule_id", "scheduled_for" });

            migrationBuilder.CreateIndex(
                name: "recurrence_occurrence_unique",
                table: "recurrence_occurrences",
                columns: new[] { "schedule_type", "schedule_id", "scheduled_at" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recurrence_occurrences");
        }
    }
}
