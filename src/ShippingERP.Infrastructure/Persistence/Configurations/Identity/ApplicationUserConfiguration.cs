using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Identity;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Identity;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ArabicFullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.EmployeeCode).HasMaxLength(30);
        builder.Property(x => x.PhoneNumber).HasMaxLength(20);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // Employee codes are unique, but many users may have none.
        builder.HasIndex(x => x.EmployeeCode)
            .IsUnique()
            .HasFilter("[EmployeeCode] IS NOT NULL");

        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.IsActive);

        // Identity creates a unique index on NormalizedUserName; e-mails are also unique in this system
        // (RequireUniqueEmail = true), so enforce it in the database too.
        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL")
            .HasDatabaseName("UX_Users_NormalizedEmail");

        builder.HasMany(x => x.RefreshTokens)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
