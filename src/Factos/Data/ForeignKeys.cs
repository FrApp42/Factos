using Factos.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data;

// Generated from InvoiceShelf's schema: same constraints, same delete behaviour.
internal static class ForeignKeys
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<Company>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(e => e.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<UserCompany>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<UserCompany>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<UserSetting>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<CompanySetting>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CustomField>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CustomFieldValue>().HasOne<CustomField>().WithMany()
            .HasForeignKey(e => e.CustomFieldId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CustomFieldValue>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Note>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ExchangeRateLog>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ExchangeRateLog>().HasOne<Currency>().WithMany()
            .HasForeignKey(e => e.BaseCurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ExchangeRateLog>().HasOne<Currency>().WithMany()
            .HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ExchangeRateProvider>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Unit>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Item>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Item>().HasOne<Unit>().WithMany()
            .HasForeignKey(e => e.UnitId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Item>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(e => e.CreatorId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Item>().HasOne<Currency>().WithMany()
            .HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Customer>().HasOne<Currency>().WithMany()
            .HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Customer>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Customer>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(e => e.CreatorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Address>().HasOne<Country>().WithMany()
            .HasForeignKey(e => e.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Address>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Address>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Address>().HasOne<Customer>().WithMany()
            .HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ExpenseCategory>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PaymentMethod>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Expense>().HasOne<ExpenseCategory>().WithMany()
            .HasForeignKey(e => e.ExpenseCategoryId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Expense>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Expense>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(e => e.CreatorId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Expense>().HasOne<PaymentMethod>().WithMany()
            .HasForeignKey(e => e.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RecurringInvoice>().HasOne<Customer>().WithMany()
            .HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RecurringInvoice>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RecurringInvoice>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(e => e.CreatorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RecurringInvoice>().HasOne<Currency>().WithMany()
            .HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Invoice>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Invoice>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(e => e.CreatorId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Invoice>().HasOne<Customer>().WithMany()
            .HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Invoice>().HasOne<RecurringInvoice>().WithMany()
            .HasForeignKey(e => e.RecurringInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Invoice>().HasOne<Currency>().WithMany()
            .HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<InvoiceItem>().HasOne<Invoice>().WithMany()
            .HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<InvoiceItem>().HasOne<Item>().WithMany()
            .HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<InvoiceItem>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Estimate>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Estimate>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(e => e.CreatorId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Estimate>().HasOne<Customer>().WithMany()
            .HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Estimate>().HasOne<Currency>().WithMany()
            .HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<EstimateItem>().HasOne<Item>().WithMany()
            .HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EstimateItem>().HasOne<Estimate>().WithMany()
            .HasForeignKey(e => e.EstimateId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EstimateItem>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Transaction>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Transaction>().HasOne<Invoice>().WithMany()
            .HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Payment>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Payment>().HasOne<PaymentMethod>().WithMany()
            .HasForeignKey(e => e.PaymentMethodId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Payment>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(e => e.CreatorId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Payment>().HasOne<Customer>().WithMany()
            .HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Payment>().HasOne<Currency>().WithMany()
            .HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TaxType>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Tax>().HasOne<TaxType>().WithMany()
            .HasForeignKey(e => e.TaxTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Tax>().HasOne<Invoice>().WithMany()
            .HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Tax>().HasOne<Estimate>().WithMany()
            .HasForeignKey(e => e.EstimateId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Tax>().HasOne<InvoiceItem>().WithMany()
            .HasForeignKey(e => e.InvoiceItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Tax>().HasOne<EstimateItem>().WithMany()
            .HasForeignKey(e => e.EstimateItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Tax>().HasOne<Item>().WithMany()
            .HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Tax>().HasOne<Company>().WithMany()
            .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Tax>().HasOne<Currency>().WithMany()
            .HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Tax>().HasOne<RecurringInvoice>().WithMany()
            .HasForeignKey(e => e.RecurringInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AssignedRole>().HasOne<Role>().WithMany()
            .HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Permission>().HasOne<Ability>().WithMany()
            .HasForeignKey(e => e.AbilityId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<InvoiceItem>().HasOne<RecurringInvoice>().WithMany()
            .HasForeignKey(e => e.RecurringInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Payment>().HasOne<Transaction>().WithMany()
            .HasForeignKey(e => e.TransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
