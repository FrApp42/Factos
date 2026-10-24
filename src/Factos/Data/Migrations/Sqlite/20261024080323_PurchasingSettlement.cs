using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Factos.Data.Migrations.Sqlite
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
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    bill_id = table.Column<long>(type: "INTEGER", nullable: false),
                    company_id = table.Column<int>(type: "INTEGER", nullable: false),
                    expense_category_id = table.Column<int>(type: "INTEGER", nullable: false),
                    source_bill_item_id = table.Column<long>(type: "INTEGER", nullable: true),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    price = table.Column<long>(type: "INTEGER", nullable: false),
                    discount = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    discount_type = table.Column<string>(type: "TEXT", nullable: false),
                    sub_total = table.Column<long>(type: "INTEGER", nullable: false),
                    discount_val = table.Column<long>(type: "INTEGER", nullable: false),
                    tax = table.Column<long>(type: "INTEGER", nullable: false),
                    total = table.Column<long>(type: "INTEGER", nullable: false),
                    base_total = table.Column<long>(type: "INTEGER", nullable: false),
                    taxes = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bills",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    company_id = table.Column<int>(type: "INTEGER", nullable: false),
                    creator_id = table.Column<int>(type: "INTEGER", nullable: true),
                    supplier_id = table.Column<long>(type: "INTEGER", nullable: false),
                    currency_id = table.Column<int>(type: "INTEGER", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "TEXT", precision: 20, scale: 6, nullable: false),
                    number = table.Column<string>(type: "TEXT", nullable: true),
                    sequence_number = table.Column<int>(type: "INTEGER", nullable: true),
                    reference = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    voided_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    void_reason = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    document_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    due_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    tax_included = table.Column<bool>(type: "INTEGER", nullable: false),
                    financial_locked_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    sub_total = table.Column<long>(type: "INTEGER", nullable: false),
                    tax = table.Column<long>(type: "INTEGER", nullable: false),
                    total = table.Column<long>(type: "INTEGER", nullable: false),
                    base_total = table.Column<long>(type: "INTEGER", nullable: false),
                    due_amount = table.Column<long>(type: "INTEGER", nullable: false),
                    base_due_amount = table.Column<long>(type: "INTEGER", nullable: false),
                    source_bill_id = table.Column<long>(type: "INTEGER", nullable: true),
                    source_expense_id = table.Column<int>(type: "INTEGER", nullable: true),
                    supplier_snapshot = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_credit_allocations",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    supplier_credit_id = table.Column<long>(type: "INTEGER", nullable: false),
                    company_id = table.Column<int>(type: "INTEGER", nullable: false),
                    bill_id = table.Column<long>(type: "INTEGER", nullable: false),
                    amount = table.Column<long>(type: "INTEGER", nullable: false),
                    base_amount = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_allocations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_credit_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    supplier_credit_id = table.Column<long>(type: "INTEGER", nullable: false),
                    company_id = table.Column<int>(type: "INTEGER", nullable: false),
                    expense_category_id = table.Column<int>(type: "INTEGER", nullable: false),
                    source_bill_item_id = table.Column<long>(type: "INTEGER", nullable: true),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    price = table.Column<long>(type: "INTEGER", nullable: false),
                    discount = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    discount_type = table.Column<string>(type: "TEXT", nullable: false),
                    sub_total = table.Column<long>(type: "INTEGER", nullable: false),
                    discount_val = table.Column<long>(type: "INTEGER", nullable: false),
                    tax = table.Column<long>(type: "INTEGER", nullable: false),
                    total = table.Column<long>(type: "INTEGER", nullable: false),
                    base_total = table.Column<long>(type: "INTEGER", nullable: false),
                    taxes = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_credits",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    company_id = table.Column<int>(type: "INTEGER", nullable: false),
                    creator_id = table.Column<int>(type: "INTEGER", nullable: true),
                    supplier_id = table.Column<long>(type: "INTEGER", nullable: false),
                    currency_id = table.Column<int>(type: "INTEGER", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "TEXT", precision: 20, scale: 6, nullable: false),
                    number = table.Column<string>(type: "TEXT", nullable: true),
                    sequence_number = table.Column<int>(type: "INTEGER", nullable: true),
                    reference = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    voided_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    void_reason = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    document_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    due_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    tax_included = table.Column<bool>(type: "INTEGER", nullable: false),
                    financial_locked_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    sub_total = table.Column<long>(type: "INTEGER", nullable: false),
                    tax = table.Column<long>(type: "INTEGER", nullable: false),
                    total = table.Column<long>(type: "INTEGER", nullable: false),
                    base_total = table.Column<long>(type: "INTEGER", nullable: false),
                    due_amount = table.Column<long>(type: "INTEGER", nullable: false),
                    base_due_amount = table.Column<long>(type: "INTEGER", nullable: false),
                    source_bill_id = table.Column<long>(type: "INTEGER", nullable: true),
                    source_expense_id = table.Column<int>(type: "INTEGER", nullable: true),
                    supplier_snapshot = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_payment_allocations",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    supplier_payment_id = table.Column<long>(type: "INTEGER", nullable: false),
                    company_id = table.Column<int>(type: "INTEGER", nullable: false),
                    bill_id = table.Column<long>(type: "INTEGER", nullable: false),
                    amount = table.Column<long>(type: "INTEGER", nullable: false),
                    base_amount = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payment_allocations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    company_id = table.Column<int>(type: "INTEGER", nullable: false),
                    creator_id = table.Column<int>(type: "INTEGER", nullable: true),
                    supplier_id = table.Column<long>(type: "INTEGER", nullable: false),
                    currency_id = table.Column<int>(type: "INTEGER", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "TEXT", precision: 20, scale: 6, nullable: false),
                    number = table.Column<string>(type: "TEXT", nullable: true),
                    sequence_number = table.Column<int>(type: "INTEGER", nullable: true),
                    reference = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    voided_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    void_reason = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    payment_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    amount = table.Column<long>(type: "INTEGER", nullable: false),
                    base_amount = table.Column<long>(type: "INTEGER", nullable: false),
                    payment_method_id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_refunds",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    supplier_payment_id = table.Column<long>(type: "INTEGER", nullable: true),
                    supplier_credit_id = table.Column<long>(type: "INTEGER", nullable: true),
                    company_id = table.Column<int>(type: "INTEGER", nullable: false),
                    creator_id = table.Column<int>(type: "INTEGER", nullable: true),
                    supplier_id = table.Column<long>(type: "INTEGER", nullable: false),
                    currency_id = table.Column<int>(type: "INTEGER", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "TEXT", precision: 20, scale: 6, nullable: false),
                    number = table.Column<string>(type: "TEXT", nullable: true),
                    sequence_number = table.Column<int>(type: "INTEGER", nullable: true),
                    reference = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    voided_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    void_reason = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    payment_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    amount = table.Column<long>(type: "INTEGER", nullable: false),
                    base_amount = table.Column<long>(type: "INTEGER", nullable: false),
                    payment_method_id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_refunds", x => x.id);
                });

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
