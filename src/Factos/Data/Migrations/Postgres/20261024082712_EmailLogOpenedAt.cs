using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Factos.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class EmailLogOpenedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "opened_at",
                table: "email_logs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "opened_at",
                table: "email_logs");
        }
    }
}
