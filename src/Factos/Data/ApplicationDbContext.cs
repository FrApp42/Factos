using Factos.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data;

// IdentityUserContext, not IdentityDbContext: authorization comes from InvoiceShelf's
// bouncer tables (abilities/roles/assigned_roles/permissions), so Identity's own role
// tables are not used — and its `roles` table would collide with bouncer's.
//
// Non-generic DbContextOptions so each provider-specific subclass can carry its own
// DbContextOptions<T> and therefore its own migration set (EF Core multi-provider pattern).
public class ApplicationDbContext(DbContextOptions options) : IdentityUserContext<ApplicationUser, int>(options)
{
    public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();

    public DbSet<Ability> Abilities => Set<Ability>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<AssignedRole> AssignedRoles => Set<AssignedRole>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanySetting> CompanySettings => Set<CompanySetting>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();
    public DbSet<CustomField> CustomFields => Set<CustomField>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<EstimateItem> EstimateItems => Set<EstimateItem>();
    public DbSet<Estimate> Estimates => Set<Estimate>();
    public DbSet<ExchangeRateLog> ExchangeRateLogs => Set<ExchangeRateLog>();
    public DbSet<ExchangeRateProvider> ExchangeRateProviders => Set<ExchangeRateProvider>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<FileDisk> FileDisks => Set<FileDisk>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RecurringInvoice> RecurringInvoices => Set<RecurringInvoice>();
    public DbSet<RecurrenceOccurrence> RecurrenceOccurrences => Set<RecurrenceOccurrence>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillItem> BillItems => Set<BillItem>();
    public DbSet<SupplierCredit> SupplierCredits => Set<SupplierCredit>();
    public DbSet<SupplierCreditItem> SupplierCreditItems => Set<SupplierCreditItem>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<SupplierRefund> SupplierRefunds => Set<SupplierRefund>();
    public DbSet<SupplierPaymentAllocation> SupplierPaymentAllocations => Set<SupplierPaymentAllocation>();
    public DbSet<SupplierCreditAllocation> SupplierCreditAllocations => Set<SupplierCreditAllocation>();
    public DbSet<TaxType> TaxTypes => Set<TaxType>();
    public DbSet<Tax> Taxes => Set<Tax>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PersonalAccessToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.Property(t => t.Name).HasMaxLength(255);
            e.Property(t => t.TokenHash).HasMaxLength(64);
            e.Property(t => t.Abilities).HasMaxLength(1000);
        });

        // Identity configures ToTable("AspNetUsers") fluently, which outranks the
        // [Table] attribute on ApplicationUser.
        builder.Entity<ApplicationUser>().ToTable("users");

        ForeignKeys.Configure(builder);

        // Laravel column casing, applied globally so no entity needs HasColumnName.
        // Table names are explicit ([Table] on each entity); only Identity's own
        // tables fall back to the convention below.
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            if (entity.IsOwned())
                continue;   // stored inside its owner's table (or JSON column)
            if (entity.ClrType.GetCustomAttributes(
                    typeof(System.ComponentModel.DataAnnotations.Schema.TableAttribute), false).Length == 0)
                entity.SetTableName(TableName(entity.ClrType.Name));

            foreach (var property in entity.GetProperties())
                property.SetColumnName(Naming.ToSnakeCase(property.Name));
        }
    }

    // ponytail: naive pluralisation, used only for Identity's tables; business
    // entities carry an explicit [Table] name taken from InvoiceShelf.
    private static string TableName(string clrName)
    {
        var name = clrName.Split('`')[0];
        foreach (var prefix in (string[])["Identity", "Application"])
            if (name.StartsWith(prefix) && name != prefix)
                name = name[prefix.Length..];
        return Naming.ToSnakeCase(name) + "s";
    }
}
