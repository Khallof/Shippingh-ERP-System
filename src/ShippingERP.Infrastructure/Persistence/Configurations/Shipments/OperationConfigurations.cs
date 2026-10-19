using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Numbering;
using ShippingERP.Domain.Entities.Settings;
using ShippingERP.Domain.Entities.Shipments;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Shipments;

public sealed class ShipmentManifestConfiguration : IEntityTypeConfiguration<ShipmentManifest>
{
    public void Configure(EntityTypeBuilder<ShipmentManifest> builder)
    {
        builder.ToTable("ShipmentManifests");

        builder.Property(x => x.ManifestNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.ManifestNumber).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.Status });
        builder.HasIndex(x => x.CreatedAtUtc);

        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Items).WithOne(x => x.Manifest).HasForeignKey(x => x.ManifestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ShipmentManifestItemConfiguration : IEntityTypeConfiguration<ShipmentManifestItem>
{
    public void Configure(EntityTypeBuilder<ShipmentManifestItem> builder)
    {
        builder.ToTable("ShipmentManifestItems");

        builder.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(30).IsUnicode(false);

        builder.HasIndex(x => new { x.ManifestId, x.ShipmentId }).IsUnique();
        builder.HasIndex(x => x.ShipmentId);

        builder.HasOne(x => x.Shipment).WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BranchTransferConfiguration : IEntityTypeConfiguration<BranchTransfer>
{
    public void Configure(EntityTypeBuilder<BranchTransfer> builder)
    {
        builder.ToTable("BranchTransfers");

        builder.Property(x => x.TransferNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.SealNumber).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.TransferNumber).IsUnique();
        builder.HasIndex(x => new { x.FromBranchId, x.Status });
        builder.HasIndex(x => new { x.ToBranchId, x.Status });
        builder.HasIndex(x => x.CreatedAtUtc);

        builder.HasOne(x => x.FromBranch).WithMany().HasForeignKey(x => x.FromBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToBranch).WithMany().HasForeignKey(x => x.ToBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Items).WithOne(x => x.BranchTransfer).HasForeignKey(x => x.BranchTransferId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BranchTransferItemConfiguration : IEntityTypeConfiguration<BranchTransferItem>
{
    public void Configure(EntityTypeBuilder<BranchTransferItem> builder)
    {
        builder.ToTable("BranchTransferItems");

        builder.Property(x => x.ItemStatus).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => new { x.BranchTransferId, x.ShipmentId }).IsUnique();
        builder.HasIndex(x => new { x.ShipmentId, x.ItemStatus });

        builder.HasOne(x => x.Shipment).WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DriverDeliveryManifestConfiguration : IEntityTypeConfiguration<DriverDeliveryManifest>
{
    public void Configure(EntityTypeBuilder<DriverDeliveryManifest> builder)
    {
        builder.ToTable("DriverDeliveryManifests");

        builder.Property(x => x.ManifestNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.ManifestNumber).IsUnique();
        builder.HasIndex(x => new { x.DriverId, x.Status });
        builder.HasIndex(x => new { x.BranchId, x.ManifestDateUtc });

        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Items).WithOne(x => x.Manifest).HasForeignKey(x => x.ManifestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DriverDeliveryManifestItemConfiguration : IEntityTypeConfiguration<DriverDeliveryManifestItem>
{
    public void Configure(EntityTypeBuilder<DriverDeliveryManifestItem> builder)
    {
        builder.ToTable("DriverDeliveryManifestItems");

        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.FailureReason).HasMaxLength(300);

        builder.HasIndex(x => new { x.ManifestId, x.ShipmentId }).IsUnique();
        builder.HasIndex(x => new { x.ShipmentId, x.Result });

        builder.HasOne(x => x.Shipment).WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProofOfDelivery).WithMany().HasForeignKey(x => x.ProofOfDeliveryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShipmentReturnConfiguration : IEntityTypeConfiguration<ShipmentReturn>
{
    public void Configure(EntityTypeBuilder<ShipmentReturn> builder)
    {
        builder.ToTable("ShipmentReturns");

        builder.Property(x => x.ReturnNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.ReturnType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.ReturnNumber).IsUnique();
        builder.HasIndex(x => x.ShipmentId);
        builder.HasIndex(x => new { x.ReturnBranchId, x.Status });

        // At most one open return per shipment.
        builder.HasIndex(x => x.ShipmentId, "UX_ShipmentReturns_SingleOpen")
            .IsUnique()
            .HasFilter("[Status] = 'Open'");

        builder.HasOne(x => x.Shipment).WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReturnBranch).WithMany().HasForeignKey(x => x.ReturnBranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShipmentDamageOrLossCaseConfiguration : IEntityTypeConfiguration<ShipmentDamageOrLossCase>
{
    public void Configure(EntityTypeBuilder<ShipmentDamageOrLossCase> builder)
    {
        builder.ToTable("ShipmentDamageOrLossCases");

        builder.Property(x => x.CaseNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.CaseType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Resolution).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.ShipmentStatusBefore).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ResolutionNotes).HasMaxLength(1000);

        builder.HasIndex(x => x.CaseNumber).IsUnique();
        builder.HasIndex(x => x.ShipmentId);
        builder.HasIndex(x => new { x.BranchId, x.Status });

        builder.HasOne(x => x.Shipment).WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShippingSettingsConfiguration : IEntityTypeConfiguration<ShippingSettings>
{
    public void Configure(EntityTypeBuilder<ShippingSettings> builder)
    {
        builder.ToTable("ShippingSettings");

        builder.Property(x => x.TrackingNumberPrefix).HasMaxLength(4).IsUnicode(false).IsRequired();
        builder.Property(x => x.WeightRoundingStepKg).HasPrecision(6, 3);
        builder.Property(x => x.TrackingUrlTemplate).HasMaxLength(300);

        // Step 5. The column defaults fill the existing settings row when the migration runs; the sentinel -1 tells
        // EF Core that 0 is a real value (a threshold of 0 % or terms of 0 days must be saved as 0, not the default).
        builder.Property(x => x.PriceOverrideApprovalThresholdPercent)
            .HasPrecision(5, 2)
            .HasDefaultValue(ShippingSettings.DefaultPriceOverrideApprovalThresholdPercent)
            .HasSentinel(-1m);
        builder.Property(x => x.DefaultPaymentTermsDays)
            .HasDefaultValue(ShippingSettings.StandardPaymentTermsDays)
            .HasSentinel(-1);
    }
}

public sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("DocumentSequences");

        builder.Property(x => x.Name).HasMaxLength(10).IsUnicode(false).IsRequired();

        builder.HasIndex(x => new { x.Name, x.Year }).IsUnique();
    }
}
