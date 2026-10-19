using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Auditing;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Auditing;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        // Stored as text ("LoginFailed") so the table is readable without the enum.
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.UserName).HasMaxLength(256);
        builder.Property(x => x.EntityName).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.EntityId).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.IpAddress).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.Details).HasMaxLength(4000);

        // No foreign key to Users on purpose: a log row must survive anything that happens to other tables.
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.Action, x.CreatedAtUtc });
    }
}
