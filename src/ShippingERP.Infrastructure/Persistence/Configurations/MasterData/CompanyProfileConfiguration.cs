using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
{
    public void Configure(EntityTypeBuilder<CompanyProfile> builder)
    {
        builder.ToTable("CompanyProfiles");

        builder.Property(x => x.ArabicName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EnglishName).HasMaxLength(200);
        builder.Property(x => x.CommercialRegistrationNumber).HasMaxLength(50);
        builder.Property(x => x.TaxNumber).HasMaxLength(50);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Website).HasMaxLength(200);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.LogoUrl).HasMaxLength(500);
        builder.Property(x => x.LogoContentType).HasMaxLength(50).IsUnicode(false);

        builder.HasOne(x => x.BaseCurrency)
            .WithMany()
            .HasForeignKey(x => x.BaseCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
