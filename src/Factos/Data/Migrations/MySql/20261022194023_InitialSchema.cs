using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Factos.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "abilities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    title = table.Column<string>(type: "longtext", nullable: true),
                    entity_id = table.Column<long>(type: "bigint", nullable: true),
                    entity_type = table.Column<string>(type: "longtext", nullable: true),
                    only_owned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    options = table.Column<string>(type: "longtext", nullable: true),
                    scope = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_abilities", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    code = table.Column<string>(type: "longtext", nullable: false),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    phonecode = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    code = table.Column<string>(type: "longtext", nullable: false),
                    symbol = table.Column<string>(type: "longtext", nullable: true),
                    precision = table.Column<int>(type: "int", nullable: false),
                    thousand_separator = table.Column<string>(type: "longtext", nullable: false),
                    decimal_separator = table.Column<string>(type: "longtext", nullable: false),
                    swap_currency_symbol = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "email_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    from = table.Column<string>(type: "longtext", nullable: false),
                    to = table.Column<string>(type: "longtext", nullable: false),
                    subject = table.Column<string>(type: "longtext", nullable: false),
                    body = table.Column<string>(type: "longtext", nullable: false),
                    mailable_type = table.Column<string>(type: "longtext", nullable: false),
                    mailable_id = table.Column<string>(type: "longtext", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    token = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_logs", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "file_disks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    type = table.Column<string>(type: "longtext", nullable: false),
                    driver = table.Column<string>(type: "longtext", nullable: false),
                    set_as_default = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    credentials = table.Column<string>(type: "longtext", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_disks", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    payment_id = table.Column<long>(type: "bigint", nullable: false),
                    invoice_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    base_amount = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocations", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "personal_access_tokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    tokenable_id = table.Column<int>(type: "int", nullable: false),
                    tokenable_type = table.Column<string>(type: "longtext", nullable: false),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    token_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    abilities = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    last_used_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_access_tokens", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    title = table.Column<string>(type: "longtext", nullable: true),
                    level = table.Column<int>(type: "int", nullable: true),
                    scope = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    option = table.Column<string>(type: "longtext", nullable: false),
                    value = table.Column<string>(type: "longtext", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    contact_name = table.Column<string>(type: "longtext", nullable: true),
                    email = table.Column<string>(type: "longtext", nullable: true),
                    phone = table.Column<string>(type: "longtext", nullable: true),
                    website = table.Column<string>(type: "longtext", nullable: true),
                    tax_id = table.Column<string>(type: "longtext", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: false),
                    expense_category_id = table.Column<int>(type: "int", nullable: true),
                    payment_terms = table.Column<int>(type: "int", nullable: false),
                    addresses = table.Column<string>(type: "longtext", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true),
                    enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    role = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    facebook_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    google_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    github_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    contact_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    company_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    website = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    enable_portal = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    user_name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    password_hash = table.Column<string>(type: "longtext", nullable: true),
                    security_stamp = table.Column<string>(type: "longtext", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "longtext", nullable: true),
                    phone_number = table.Column<string>(type: "longtext", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    access_failed_count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ability_id = table.Column<long>(type: "bigint", nullable: false),
                    entity_id = table.Column<long>(type: "bigint", nullable: true),
                    entity_type = table.Column<string>(type: "longtext", nullable: true),
                    forbidden = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    scope = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_permissions_abilities_ability_id",
                        column: x => x.ability_id,
                        principalTable: "abilities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "assigned_roles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    entity_id = table.Column<long>(type: "bigint", nullable: false),
                    entity_type = table.Column<string>(type: "longtext", nullable: false),
                    restricted_to_id = table.Column<long>(type: "bigint", nullable: true),
                    restricted_to_type = table.Column<string>(type: "longtext", nullable: true),
                    scope = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assigned_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_assigned_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    logo = table.Column<string>(type: "longtext", nullable: true),
                    unique_hash = table.Column<string>(type: "longtext", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    slug = table.Column<string>(type: "longtext", nullable: true),
                    owner_id = table.Column<int>(type: "int", nullable: true),
                    vat_id = table.Column<string>(type: "longtext", nullable: true),
                    tax_id = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                    table.ForeignKey(
                        name: "FK_companies_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    claim_type = table.Column<string>(type: "longtext", nullable: true),
                    claim_value = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_claims_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "varchar(255)", nullable: false),
                    provider_key = table.Column<string>(type: "varchar(255)", nullable: false),
                    provider_display_name = table.Column<string>(type: "longtext", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "FK_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_settings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    key = table.Column<string>(type: "longtext", nullable: false),
                    value = table.Column<string>(type: "longtext", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    login_provider = table.Column<string>(type: "varchar(255)", nullable: false),
                    name = table.Column<string>(type: "varchar(255)", nullable: false),
                    value = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "FK_user_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "company_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    option = table.Column<string>(type: "longtext", nullable: false),
                    value = table.Column<string>(type: "longtext", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_settings_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "custom_fields",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    slug = table.Column<string>(type: "longtext", nullable: false),
                    label = table.Column<string>(type: "longtext", nullable: false),
                    model_type = table.Column<string>(type: "longtext", nullable: false),
                    type = table.Column<string>(type: "longtext", nullable: false),
                    placeholder = table.Column<string>(type: "longtext", nullable: true),
                    options = table.Column<string>(type: "longtext", nullable: true),
                    boolean_answer = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    date_answer = table.Column<DateOnly>(type: "date", nullable: true),
                    time_answer = table.Column<TimeOnly>(type: "time", nullable: true),
                    string_answer = table.Column<string>(type: "longtext", nullable: true),
                    number_answer = table.Column<long>(type: "bigint", nullable: true),
                    date_time_answer = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    is_required = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    order = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    placement = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    validation = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_fields", x => x.id);
                    table.ForeignKey(
                        name: "FK_custom_fields_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    email = table.Column<string>(type: "longtext", nullable: true),
                    phone = table.Column<string>(type: "longtext", nullable: true),
                    password = table.Column<string>(type: "longtext", nullable: true),
                    remember_token = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    facebook_id = table.Column<string>(type: "longtext", nullable: true),
                    google_id = table.Column<string>(type: "longtext", nullable: true),
                    github_id = table.Column<string>(type: "longtext", nullable: true),
                    contact_name = table.Column<string>(type: "longtext", nullable: true),
                    company_name = table.Column<string>(type: "longtext", nullable: true),
                    website = table.Column<string>(type: "longtext", nullable: true),
                    enable_portal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    prefix = table.Column<string>(type: "longtext", nullable: true),
                    tax_id = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                    table.ForeignKey(
                        name: "FK_customers_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customers_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customers_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "exchange_rate_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    base_currency_id = table.Column<int>(type: "int", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    exchange_rate = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchange_rate_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_exchange_rate_logs_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exchange_rate_logs_currencies_base_currency_id",
                        column: x => x.base_currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exchange_rate_logs_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "exchange_rate_providers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    driver = table.Column<string>(type: "longtext", nullable: false),
                    key = table.Column<string>(type: "longtext", nullable: false),
                    currencies = table.Column<string>(type: "longtext", nullable: true),
                    driver_config = table.Column<string>(type: "longtext", nullable: true),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchange_rate_providers", x => x.id);
                    table.ForeignKey(
                        name: "FK_exchange_rate_providers_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "expense_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    description = table.Column<string>(type: "longtext", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_categories_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    type = table.Column<string>(type: "longtext", nullable: false),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    is_default = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_notes_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_methods",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    driver = table.Column<string>(type: "longtext", nullable: true),
                    type = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    settings = table.Column<string>(type: "longtext", nullable: true),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    use_test_env = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_methods", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_methods_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tax_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    percent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    compound_tax = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    collective_tax = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    description = table.Column<string>(type: "longtext", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    type = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    calculation_type = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    fixed_amount = table.Column<int>(type: "int", nullable: true),
                    transaction_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_types", x => x.id);
                    table.ForeignKey(
                        name: "FK_tax_types_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.id);
                    table.ForeignKey(
                        name: "FK_units_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_company",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_company", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_company_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_company_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "custom_field_values",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    custom_field_valuable_type = table.Column<string>(type: "longtext", nullable: false),
                    custom_field_valuable_id = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<string>(type: "longtext", nullable: false),
                    boolean_answer = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    date_answer = table.Column<DateOnly>(type: "date", nullable: true),
                    time_answer = table.Column<TimeOnly>(type: "time", nullable: true),
                    string_answer = table.Column<string>(type: "longtext", nullable: true),
                    number_answer = table.Column<long>(type: "bigint", nullable: true),
                    date_time_answer = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    custom_field_id = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_field_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_custom_field_values_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_custom_field_values_custom_fields_custom_field_id",
                        column: x => x.custom_field_id,
                        principalTable: "custom_fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "addresses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: true),
                    address_street_1 = table.Column<string>(type: "longtext", nullable: true),
                    address_street_2 = table.Column<string>(type: "longtext", nullable: true),
                    city = table.Column<string>(type: "longtext", nullable: true),
                    state = table.Column<string>(type: "longtext", nullable: true),
                    country_id = table.Column<int>(type: "int", nullable: true),
                    zip = table.Column<string>(type: "longtext", nullable: true),
                    phone = table.Column<string>(type: "longtext", nullable: true),
                    fax = table.Column<string>(type: "longtext", nullable: true),
                    type = table.Column<string>(type: "longtext", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    customer_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_addresses", x => x.id);
                    table.ForeignKey(
                        name: "FK_addresses_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_addresses_countries_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_addresses_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_addresses_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "estimates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    estimate_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    estimate_number = table.Column<string>(type: "longtext", nullable: false),
                    status = table.Column<string>(type: "longtext", nullable: false),
                    reference_number = table.Column<string>(type: "longtext", nullable: true),
                    tax_per_item = table.Column<string>(type: "longtext", nullable: false),
                    discount_per_item = table.Column<string>(type: "longtext", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true),
                    discount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    discount_type = table.Column<string>(type: "longtext", nullable: true),
                    discount_val = table.Column<long>(type: "bigint", nullable: true),
                    sub_total = table.Column<long>(type: "bigint", nullable: false),
                    total = table.Column<long>(type: "bigint", nullable: false),
                    tax = table.Column<long>(type: "bigint", nullable: false),
                    unique_hash = table.Column<string>(type: "longtext", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    template_name = table.Column<string>(type: "longtext", nullable: true),
                    customer_id = table.Column<long>(type: "bigint", nullable: true),
                    exchange_rate = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    base_discount_val = table.Column<long>(type: "bigint", nullable: true),
                    base_sub_total = table.Column<long>(type: "bigint", nullable: true),
                    base_total = table.Column<long>(type: "bigint", nullable: true),
                    base_tax = table.Column<long>(type: "bigint", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    sequence_number = table.Column<int>(type: "int", nullable: true),
                    customer_sequence_number = table.Column<int>(type: "int", nullable: true),
                    sales_tax_type = table.Column<string>(type: "longtext", nullable: true),
                    sales_tax_address_type = table.Column<string>(type: "longtext", nullable: true),
                    tax_included = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estimates", x => x.id);
                    table.ForeignKey(
                        name: "FK_estimates_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_estimates_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_estimates_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_estimates_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "recurring_invoices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    send_automatically = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    next_invoice_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    frequency = table.Column<string>(type: "longtext", nullable: false),
                    limit_by = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    limit_count = table.Column<int>(type: "int", nullable: true),
                    limit_date = table.Column<DateOnly>(type: "date", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    exchange_rate = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    tax_per_item = table.Column<string>(type: "longtext", nullable: false),
                    discount_per_item = table.Column<string>(type: "longtext", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true),
                    discount_type = table.Column<string>(type: "longtext", nullable: true),
                    discount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    discount_val = table.Column<long>(type: "bigint", nullable: true),
                    sub_total = table.Column<long>(type: "bigint", nullable: false),
                    total = table.Column<long>(type: "bigint", nullable: false),
                    tax = table.Column<long>(type: "bigint", nullable: false),
                    template_name = table.Column<string>(type: "longtext", nullable: true),
                    due_amount = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    sales_tax_type = table.Column<string>(type: "longtext", nullable: true),
                    sales_tax_address_type = table.Column<string>(type: "longtext", nullable: true),
                    tax_included = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    last_error = table.Column<string>(type: "longtext", nullable: true),
                    notify_creator = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_invoices_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_invoices_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_invoices_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_invoices_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    expense_date = table.Column<DateOnly>(type: "date", nullable: false),
                    attachment_receipt = table.Column<string>(type: "longtext", nullable: true),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true),
                    expense_category_id = table.Column<int>(type: "int", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    customer_id = table.Column<long>(type: "bigint", nullable: true),
                    exchange_rate = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    base_amount = table.Column<long>(type: "bigint", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    payment_method_id = table.Column<int>(type: "int", nullable: true),
                    expense_number = table.Column<string>(type: "longtext", nullable: true),
                    supplier_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.id);
                    table.ForeignKey(
                        name: "FK_expenses_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expenses_expense_categories_expense_category_id",
                        column: x => x.expense_category_id,
                        principalTable: "expense_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expenses_payment_methods_payment_method_id",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expenses_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    description = table.Column<string>(type: "longtext", nullable: true),
                    price = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    unit_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    tax_per_item = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_items_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_items_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_items_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_items_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    invoice_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    invoice_number = table.Column<string>(type: "longtext", nullable: false),
                    reference_number = table.Column<string>(type: "longtext", nullable: true),
                    status = table.Column<string>(type: "longtext", nullable: false),
                    paid_status = table.Column<string>(type: "longtext", nullable: false),
                    tax_per_item = table.Column<string>(type: "longtext", nullable: false),
                    discount_per_item = table.Column<string>(type: "longtext", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true),
                    discount_type = table.Column<string>(type: "longtext", nullable: true),
                    discount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    discount_val = table.Column<long>(type: "bigint", nullable: true),
                    sub_total = table.Column<long>(type: "bigint", nullable: false),
                    total = table.Column<long>(type: "bigint", nullable: false),
                    tax = table.Column<long>(type: "bigint", nullable: false),
                    due_amount = table.Column<long>(type: "bigint", nullable: false),
                    sent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    viewed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    unique_hash = table.Column<string>(type: "longtext", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    template_name = table.Column<string>(type: "longtext", nullable: true),
                    customer_id = table.Column<long>(type: "bigint", nullable: true),
                    recurring_invoice_id = table.Column<long>(type: "bigint", nullable: true),
                    exchange_rate = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    base_discount_val = table.Column<long>(type: "bigint", nullable: true),
                    base_sub_total = table.Column<long>(type: "bigint", nullable: true),
                    base_total = table.Column<long>(type: "bigint", nullable: true),
                    base_tax = table.Column<long>(type: "bigint", nullable: true),
                    base_due_amount = table.Column<long>(type: "bigint", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    sequence_number = table.Column<int>(type: "int", nullable: true),
                    customer_sequence_number = table.Column<int>(type: "int", nullable: true),
                    sales_tax_type = table.Column<string>(type: "longtext", nullable: true),
                    sales_tax_address_type = table.Column<string>(type: "longtext", nullable: true),
                    overdue = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tax_included = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    type = table.Column<string>(type: "longtext", nullable: false),
                    related_invoice_id = table.Column<int>(type: "int", nullable: true),
                    credit_reason = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoices_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoices_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_recurring_invoices_recurring_invoice_id",
                        column: x => x.recurring_invoice_id,
                        principalTable: "recurring_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "estimate_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    description = table.Column<string>(type: "longtext", nullable: true),
                    discount_type = table.Column<string>(type: "longtext", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    discount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    discount_val = table.Column<long>(type: "bigint", nullable: true),
                    price = table.Column<long>(type: "bigint", nullable: false),
                    tax = table.Column<long>(type: "bigint", nullable: false),
                    total = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<int>(type: "int", nullable: true),
                    estimate_id = table.Column<int>(type: "int", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    unit_name = table.Column<string>(type: "longtext", nullable: true),
                    exchange_rate = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    base_discount_val = table.Column<long>(type: "bigint", nullable: true),
                    base_price = table.Column<long>(type: "bigint", nullable: true),
                    base_tax = table.Column<long>(type: "bigint", nullable: true),
                    base_total = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estimate_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_estimate_items_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_estimate_items_estimates_estimate_id",
                        column: x => x.estimate_id,
                        principalTable: "estimates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_estimate_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    description = table.Column<string>(type: "longtext", nullable: true),
                    discount_type = table.Column<string>(type: "longtext", nullable: false),
                    price = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    discount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    discount_val = table.Column<long>(type: "bigint", nullable: false),
                    tax = table.Column<long>(type: "bigint", nullable: false),
                    total = table.Column<long>(type: "bigint", nullable: false),
                    invoice_id = table.Column<int>(type: "int", nullable: true),
                    item_id = table.Column<int>(type: "int", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    unit_name = table.Column<string>(type: "longtext", nullable: true),
                    recurring_invoice_id = table.Column<long>(type: "bigint", nullable: true),
                    base_price = table.Column<long>(type: "bigint", nullable: true),
                    exchange_rate = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    base_discount_val = table.Column<long>(type: "bigint", nullable: true),
                    base_tax = table.Column<long>(type: "bigint", nullable: true),
                    base_total = table.Column<long>(type: "bigint", nullable: true),
                    source_invoice_item_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_items_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoice_items_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoice_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoice_items_recurring_invoices_recurring_invoice_id",
                        column: x => x.recurring_invoice_id,
                        principalTable: "recurring_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    transaction_id = table.Column<string>(type: "longtext", nullable: true),
                    unique_hash = table.Column<string>(type: "longtext", nullable: true),
                    type = table.Column<string>(type: "longtext", nullable: true),
                    status = table.Column<string>(type: "longtext", nullable: false),
                    transaction_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    invoice_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_transactions_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "taxes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    tax_type_id = table.Column<int>(type: "int", nullable: false),
                    invoice_id = table.Column<int>(type: "int", nullable: true),
                    estimate_id = table.Column<int>(type: "int", nullable: true),
                    invoice_item_id = table.Column<int>(type: "int", nullable: true),
                    estimate_item_id = table.Column<int>(type: "int", nullable: true),
                    item_id = table.Column<int>(type: "int", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "longtext", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    percent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    compound_tax = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    exchange_rate = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    base_amount = table.Column<long>(type: "bigint", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    recurring_invoice_id = table.Column<long>(type: "bigint", nullable: true),
                    calculation_type = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    fixed_amount = table.Column<int>(type: "int", nullable: true),
                    expense_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_taxes_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_taxes_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_taxes_estimate_items_estimate_item_id",
                        column: x => x.estimate_item_id,
                        principalTable: "estimate_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_taxes_estimates_estimate_id",
                        column: x => x.estimate_id,
                        principalTable: "estimates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_taxes_invoice_items_invoice_item_id",
                        column: x => x.invoice_item_id,
                        principalTable: "invoice_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_taxes_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_taxes_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_taxes_recurring_invoices_recurring_invoice_id",
                        column: x => x.recurring_invoice_id,
                        principalTable: "recurring_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_taxes_tax_types_tax_type_id",
                        column: x => x.tax_type_id,
                        principalTable: "tax_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    payment_number = table.Column<string>(type: "longtext", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    unique_hash = table.Column<string>(type: "longtext", nullable: true),
                    company_id = table.Column<int>(type: "int", nullable: true),
                    payment_method_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    creator_id = table.Column<int>(type: "int", nullable: true),
                    customer_id = table.Column<long>(type: "bigint", nullable: true),
                    exchange_rate = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    base_amount = table.Column<long>(type: "bigint", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    sequence_number = table.Column<int>(type: "int", nullable: true),
                    customer_sequence_number = table.Column<int>(type: "int", nullable: true),
                    transaction_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_payments_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payments_currencies_currency_id",
                        column: x => x.currency_id,
                        principalTable: "currencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_payment_methods_payment_method_id",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payments_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_company_id",
                table: "addresses",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_country_id",
                table: "addresses",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_customer_id",
                table: "addresses",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_user_id",
                table: "addresses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_assigned_roles_role_id",
                table: "assigned_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_companies_owner_id",
                table: "companies",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_company_id",
                table: "company_settings",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_values_company_id",
                table: "custom_field_values",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_values_custom_field_id",
                table: "custom_field_values",
                column: "custom_field_id");

            migrationBuilder.CreateIndex(
                name: "IX_custom_fields_company_id",
                table: "custom_fields",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_customers_company_id",
                table: "customers",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_customers_creator_id",
                table: "customers",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "IX_customers_currency_id",
                table: "customers",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_estimate_items_company_id",
                table: "estimate_items",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_estimate_items_estimate_id",
                table: "estimate_items",
                column: "estimate_id");

            migrationBuilder.CreateIndex(
                name: "IX_estimate_items_item_id",
                table: "estimate_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_estimates_company_id",
                table: "estimates",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_estimates_creator_id",
                table: "estimates",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "IX_estimates_currency_id",
                table: "estimates",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_estimates_customer_id",
                table: "estimates",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rate_logs_base_currency_id",
                table: "exchange_rate_logs",
                column: "base_currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rate_logs_company_id",
                table: "exchange_rate_logs",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rate_logs_currency_id",
                table: "exchange_rate_logs",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rate_providers_company_id",
                table: "exchange_rate_providers",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_categories_company_id",
                table: "expense_categories",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_company_id",
                table: "expenses",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_creator_id",
                table: "expenses",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_expense_category_id",
                table: "expenses",
                column: "expense_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_payment_method_id",
                table: "expenses",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_company_id",
                table: "invoice_items",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_invoice_id",
                table: "invoice_items",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_item_id",
                table: "invoice_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_recurring_invoice_id",
                table: "invoice_items",
                column: "recurring_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_company_id",
                table: "invoices",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_creator_id",
                table: "invoices",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_currency_id",
                table: "invoices",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_customer_id",
                table: "invoices",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_recurring_invoice_id",
                table: "invoices",
                column: "recurring_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_company_id",
                table: "items",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_creator_id",
                table: "items",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_currency_id",
                table: "items",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_unit_id",
                table: "items",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_notes_company_id",
                table: "notes",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_methods_company_id",
                table: "payment_methods",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_company_id",
                table: "payments",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_creator_id",
                table: "payments",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_currency_id",
                table: "payments",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_customer_id",
                table: "payments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_payment_method_id",
                table: "payments",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_transaction_id",
                table: "payments",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_ability_id",
                table: "permissions",
                column: "ability_id");

            migrationBuilder.CreateIndex(
                name: "IX_personal_access_tokens_token_hash",
                table: "personal_access_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_invoices_company_id",
                table: "recurring_invoices",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_invoices_creator_id",
                table: "recurring_invoices",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_invoices_currency_id",
                table: "recurring_invoices",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_invoices_customer_id",
                table: "recurring_invoices",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_tax_types_company_id",
                table: "tax_types",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_taxes_company_id",
                table: "taxes",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_taxes_currency_id",
                table: "taxes",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_taxes_estimate_id",
                table: "taxes",
                column: "estimate_id");

            migrationBuilder.CreateIndex(
                name: "IX_taxes_estimate_item_id",
                table: "taxes",
                column: "estimate_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_taxes_invoice_id",
                table: "taxes",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_taxes_invoice_item_id",
                table: "taxes",
                column: "invoice_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_taxes_item_id",
                table: "taxes",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_taxes_recurring_invoice_id",
                table: "taxes",
                column: "recurring_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_taxes_tax_type_id",
                table: "taxes",
                column: "tax_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_company_id",
                table: "transactions",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_invoice_id",
                table: "transactions",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_units_company_id",
                table: "units",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_claims_user_id",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_company_company_id",
                table: "user_company",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_company_user_id",
                table: "user_company",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_logins_user_id",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_settings_user_id",
                table: "user_settings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "normalized_user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "addresses");

            migrationBuilder.DropTable(
                name: "assigned_roles");

            migrationBuilder.DropTable(
                name: "company_settings");

            migrationBuilder.DropTable(
                name: "custom_field_values");

            migrationBuilder.DropTable(
                name: "email_logs");

            migrationBuilder.DropTable(
                name: "exchange_rate_logs");

            migrationBuilder.DropTable(
                name: "exchange_rate_providers");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "file_disks");

            migrationBuilder.DropTable(
                name: "notes");

            migrationBuilder.DropTable(
                name: "payment_allocations");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "personal_access_tokens");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "taxes");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_company");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_settings");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "custom_fields");

            migrationBuilder.DropTable(
                name: "expense_categories");

            migrationBuilder.DropTable(
                name: "payment_methods");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "abilities");

            migrationBuilder.DropTable(
                name: "estimate_items");

            migrationBuilder.DropTable(
                name: "invoice_items");

            migrationBuilder.DropTable(
                name: "tax_types");

            migrationBuilder.DropTable(
                name: "estimates");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "items");

            migrationBuilder.DropTable(
                name: "recurring_invoices");

            migrationBuilder.DropTable(
                name: "units");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "currencies");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
