using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        builder.ToTable("ServiceTypes");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 20, nameLength: 100);
        builder.Property(x => x.Description).HasMaxLength(500);
    }
}

public sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("PaymentMethods");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 20, nameLength: 100);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
    }
}

public sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("Currencies");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 3, nameLength: 100);
        builder.Property(x => x.Symbol).HasMaxLength(10).IsRequired();

        // At most one base currency (soft-deleted rows excluded).
        builder.HasIndex(x => x.IsBaseCurrency)
            .IsUnique()
            .HasFilter("[IsBaseCurrency] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Currencies_SingleBaseCurrency");
    }
}

public sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");

        builder.Property(x => x.Rate).HasPrecision(18, 6);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasOne(x => x.FromCurrency)
            .WithMany()
            .HasForeignKey(x => x.FromCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToCurrency)
            .WithMany()
            .HasForeignKey(x => x.ToCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // One rate per pair and start time; the lookup "latest rate for a pair" uses this index.
        builder.HasIndex(x => new { x.FromCurrencyId, x.ToCurrencyId, x.EffectiveFromUtc }).IsUnique();
    }
}

public sealed class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.ToTable("TaxRates");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 20, nameLength: 100);
        builder.Property(x => x.Percentage).HasPrecision(5, 2);
    }
}

public sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.ToTable("ExpenseCategories");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 20, nameLength: 100);
        builder.Property(x => x.Description).HasMaxLength(500);
    }
}

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 20, nameLength: 150);
        builder.Property(x => x.ContactPerson).HasMaxLength(150);
        builder.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.AlternatePhone).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.TaxNumber).HasMaxLength(50);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.Phone);
    }
}

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");

        builder.Property(x => x.PlateNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(100).IsRequired();
        builder.Property(x => x.VehicleType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.CapacityKg).HasPrecision(10, 2);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.PlateNumber).IsUnique();
        builder.HasIndex(x => x.IsActive);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
