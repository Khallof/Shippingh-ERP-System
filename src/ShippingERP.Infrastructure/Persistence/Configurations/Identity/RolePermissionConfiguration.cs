using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Identity;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Identity;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        // A role holds each permission at most once.
        builder.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
        builder.HasIndex(x => x.PermissionId);
    }
}
