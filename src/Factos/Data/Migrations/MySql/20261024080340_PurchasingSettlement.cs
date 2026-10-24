using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Factos.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class PurchasingSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bill_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    bill_id = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    expense_category_id = table.Column<int>(type: "int", nullable: false),
                    source_bill_item_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    price = table.Column<long>(type: "bigint", nullable: false),
                    discount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    discount_type = table.Column<string>(type: "longtext", nullable: false),
                    sub_total = table.Column<long>(type: "bigint", nullable: false),
                    discount_val = table.Column<long>(type: "bigint", nullable: false),
                    tax = table.Column<long>(type: "bigint", nullable: false),
                    total = table.Column<long>(type: "bigint", nullable: false),
                    base_total = table.Column<long>(type: "bigint", nullable: false),
                    taxes = table.Column<string>(type: "longtext", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_items", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bills",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    supplier_id = table.Column<long>(type: "bigint", nullable: false),
                    currency_id = table.Column<int>(type: "int", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "decimal(20,6)", precision: 20, scale: 6, nullable: false),
                    number = table.Column<string>(type: "varchar(255)", nullable: true),
                    sequence_number = table.Column<int>(type: "int", nullable: true),
                    reference = table.Column<string>(type: "longtext", nullable: true),
                    status = table.Column<string>(type: "longtext", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true),
                    voided_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    void_reason = table.Column<string>(type: "longtext", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    tax_included = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    financial_locked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    sub_total = table.Column<long>(type: "bigint", nullable: false),
                    tax = table.Column<long>(type: "bigint", nullable: false),
                    total = table.Column<long>(type: "bigint", nullable: false),
                    base_total = table.Column<long>(type: "bigint", nullable: false),
                    due_amount = table.Column<long>(type: "bigint", nullable: false),
                    base_due_amount = table.Column<long>(type: "bigint", nullable: false),
                    source_bill_id = table.Column<long>(type: "bigint", nullable: true),
                    source_expense_id = table.Column<int>(type: "int", nullable: true),
                    supplier_snapshot = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bills", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "supplier_credit_allocations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    supplier_credit_id = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    bill_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    base_amount = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_allocations", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "supplier_credit_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    supplier_credit_id = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    expense_category_id = table.Column<int>(type: "int", nullable: false),
                    source_bill_item_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    price = table.Column<long>(type: "bigint", nullable: false),
                    discount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    discount_type = table.Column<string>(type: "longtext", nullable: false),
                    sub_total = table.Column<long>(type: "bigint", nullable: false),
                    discount_val = table.Column<long>(type: "bigint", nullable: false),
                    tax = table.Column<long>(type: "bigint", nullable: false),
                    total = table.Column<long>(type: "bigint", nullable: false),
                    base_total = table.Column<long>(type: "bigint", nullable: false),
                    taxes = table.Column<string>(type: "longtext", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_items", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "supplier_credits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    supplier_id = table.Column<long>(type: "bigint", nullable: false),
                    currency_id = table.Column<int>(type: "int", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "decimal(20,6)", precision: 20, scale: 6, nullable: false),
                    number = table.Column<string>(type: "varchar(255)", nullable: true),
                    sequence_number = table.Column<int>(type: "int", nullable: true),
                    reference = table.Column<string>(type: "longtext", nullable: true),
                    status = table.Column<string>(type: "longtext", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true),
                    voided_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    void_reason = table.Column<string>(type: "longtext", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    tax_included = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    financial_locked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    sub_total = table.Column<long>(type: "bigint", nullable: false),
                    tax = table.Column<long>(type: "bigint", nullable: false),
                    total = table.Column<long>(type: "bigint", nullable: false),
                    base_total = table.Column<long>(type: "bigint", nullable: false),
                    due_amount = table.Column<long>(type: "bigint", nullable: false),
                    base_due_amount = table.Column<long>(type: "bigint", nullable: false),
                    source_bill_id = table.Column<long>(type: "bigint", nullable: true),
                    source_expense_id = table.Column<int>(type: "int", nullable: true),
                    supplier_snapshot = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credits", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "supplier_payment_allocations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    supplier_payment_id = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    bill_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    base_amount = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payment_allocations", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "supplier_payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    supplier_id = table.Column<long>(type: "bigint", nullable: false),
                    currency_id = table.Column<int>(type: "int", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "decimal(20,6)", precision: 20, scale: 6, nullable: false),
                    number = table.Column<string>(type: "varchar(255)", nullable: true),
                    sequence_number = table.Column<int>(type: "int", nullable: true),
                    reference = table.Column<string>(type: "longtext", nullable: true),
                    status = table.Column<string>(type: "longtext", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true),
                    voided_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    void_reason = table.Column<string>(type: "longtext", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    base_amount = table.Column<long>(type: "bigint", nullable: false),
                    payment_method_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payments", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "supplier_refunds",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    supplier_payment_id = table.Column<long>(type: "bigint", nullable: true),
                    supplier_credit_id = table.Column<long>(type: "bigint", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    supplier_id = table.Column<long>(type: "bigint", nullable: false),
                    currency_id = table.Column<int>(type: "int", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "decimal(20,6)", precision: 20, scale: 6, nullable: false),
                    number = table.Column<string>(type: "varchar(255)", nullable: true),
                    sequence_number = table.Column<int>(type: "int", nullable: true),
                    reference = table.Column<string>(type: "longtext", nullable: true),
                    status = table.Column<string>(type: "longtext", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true),
                    voided_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    void_reason = table.Column<string>(type: "longtext", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    base_amount = table.Column<long>(type: "bigint", nullable: false),
                    payment_method_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_refunds", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bills_company_id_number",
                table: "bills",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "supplier_credit_bill_unique",
                table: "supplier_credit_allocations",
                columns: new[] { "supplier_credit_id", "bill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_company_id_number",
                table: "supplier_credits",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "supplier_payment_bill_unique",
                table: "supplier_payment_allocations",
                columns: new[] { "supplier_payment_id", "bill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_company_id_number",
                table: "supplier_payments",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_refunds_company_id_number",
                table: "supplier_refunds",
                columns: new[] { "company_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_items");

            migrationBuilder.DropTable(
                name: "bills");

            migrationBuilder.DropTable(
                name: "supplier_credit_allocations");

            migrationBuilder.DropTable(
                name: "supplier_credit_items");

            migrationBuilder.DropTable(
                name: "supplier_credits");

            migrationBuilder.DropTable(
                name: "supplier_payment_allocations");

            migrationBuilder.DropTable(
                name: "supplier_payments");

            migrationBuilder.DropTable(
                name: "supplier_refunds");
        }
    }
}
