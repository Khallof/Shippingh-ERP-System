using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Infrastructure.Persistence.Configurations.MasterData;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Customers;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 20, nameLength: 150);
        builder.Property(x => x.CustomerType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Roles); // stored as int (flags)
        builder.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.AlternatePhone).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.NationalOrCompanyNumber).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        // Phone search when creating a shipment ("who is this caller?").
        builder.HasIndex(x => x.Phone);
        builder.HasIndex(x => x.AlternatePhone);
        builder.HasIndex(x => x.NationalOrCompanyNumber);

        builder.HasOne(x => x.DefaultBranch)
            .WithMany()
            .HasForeignKey(x => x.DefaultBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PricingPlan)
            .WithMany()
            .HasForeignKey(x => x.PricingPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Addresses)
            .WithOne(x => x.Customer)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CustomerAddresses");

        builder.Property(x => x.Label).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AddressLine).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Landmark).HasMaxLength(200);
        builder.Property(x => x.BuildingNumber).HasMaxLength(20);
        builder.Property(x => x.PostalCode).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.ContactName).HasMaxLength(150);
        builder.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.AlternatePhone).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);

        builder.HasIndex(x => x.Phone);

        // At most one default address per customer.
        builder.HasIndex(x => x.CustomerId)
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_CustomerAddresses_SingleDefault");

        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Zone)
            .WithMany()
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerPricingPlanConfiguration : IEntityTypeConfiguration<CustomerPricingPlan>
{
    public void Configure(EntityTypeBuilder<CustomerPricingPlan> builder)
    {
        builder.ToTable("CustomerPricingPlans");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 20, nameLength: 150);
        builder.Property(x => x.ContractReference).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.IsDefault)
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_CustomerPricingPlans_SingleDefault");

        // The pricing engine looks for the contract of the sender first.
        builder.HasIndex(x => x.ContractCustomerId);

        builder.HasOne(x => x.Currency)
            .WithMany()
            .HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ContractCustomer)
            .WithMany()
            .HasForeignKey(x => x.ContractCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Rules)
            .WithOne(x => x.PricingPlan)
            .HasForeignKey(x => x.PricingPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
