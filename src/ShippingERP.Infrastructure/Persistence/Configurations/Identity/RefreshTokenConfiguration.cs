using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Identity;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Identity;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        // SHA-256 as 64 upper-case hex characters.
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(x => x.CreatedByIp).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.RevokedByIp).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.RevokedReason).HasMaxLength(50).IsUnicode(false);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.FamilyId);
        builder.HasIndex(x => new { x.UserId, x.RevokedAtUtc });

        // Old token -> the token that replaced it. NoAction: SQL Server does not allow a second cascade path.
        builder.HasOne(x => x.ReplacedByToken)
            .WithMany()
            .HasForeignKey(x => x.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Ignore(x => x.IsRevoked);
    }
}
