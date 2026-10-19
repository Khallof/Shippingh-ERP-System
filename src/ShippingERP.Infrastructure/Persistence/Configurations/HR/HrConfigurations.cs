using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Infrastructure.Persistence.Configurations.MasterData;

namespace ShippingERP.Infrastructure.Persistence.Configurations.HR;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 20, nameLength: 150);
        builder.Property(x => x.JobTitle).HasMaxLength(100);
        builder.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.NationalId).HasMaxLength(30);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        // A system user belongs to at most one employee.
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DriverProfile)
            .WithOne(x => x.Employee)
            .HasForeignKey<DriverProfile>(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DriverProfileConfiguration : IEntityTypeConfiguration<DriverProfile>
{
    public void Configure(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.ToTable("DriverProfiles");

        builder.Property(x => x.LicenseNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.LicenseCategory).HasMaxLength(20);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        // One driver profile per employee (the one-to-one above already creates a unique index on EmployeeId).
        builder.HasIndex(x => x.LicenseNumber).IsUnique();
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        builder.HasIndex(x => x.LicenseExpiryDateUtc);
        builder.HasIndex(x => x.IsActive);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DefaultVehicle)
            .WithMany()
            .HasForeignKey(x => x.DefaultVehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
