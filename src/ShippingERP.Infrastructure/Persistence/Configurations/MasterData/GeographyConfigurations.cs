using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 3, nameLength: 100);
        builder.Property(x => x.PhoneCode).HasMaxLength(5).IsUnicode(false);

        builder.HasMany(x => x.Cities)
            .WithOne(x => x.Country)
            .HasForeignKey(x => x.CountryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("Cities");

        // City codes are unique per country, not globally.
        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 10, nameLength: 100, uniqueCode: false);
        builder.HasIndex(x => new { x.CountryId, x.Code }).IsUnique();

        builder.HasMany(x => x.Zones)
            .WithOne(x => x.City)
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("Zones");

        // Zone codes are unique per city.
        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 20, nameLength: 100, uniqueCode: false);
        builder.HasIndex(x => new { x.CityId, x.Code }).IsUnique();
        builder.Property(x => x.Notes).HasMaxLength(500);
    }
}
