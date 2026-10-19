using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Shipments;

/// <summary>
/// Shipments table. Indexes follow the list filters: status, branches (origin / current / destination),
/// driver, customers, cities, service type, COD flag and creation date.
/// Every foreign key is RESTRICT: nothing that a shipment uses can be deleted physically.
/// </summary>
public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");

        builder.Property(x => x.ShipmentNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.TrackingNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.ExternalReference).HasMaxLength(50);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.ActualWeight).HasPrecision(10, 3);
        builder.Property(x => x.VolumetricWeight).HasPrecision(10, 3);
        builder.Property(x => x.ChargeableWeight).HasPrecision(10, 3);
        builder.Property(x => x.TotalVolumeCbm).HasPrecision(10, 3);
        builder.Property(x => x.PricingBasis).HasConversion<string>().HasMaxLength(20).IsUnicode(false).HasDefaultValue(PricingBasis.Weight);
        builder.Property(x => x.ChargesPaidBy).HasConversion<string>().HasMaxLength(20).IsUnicode(false).HasDefaultValue(ChargesPayer.Sender);
        builder.Ignore(x => x.PayerCustomerId);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.PackageDescription).HasMaxLength(300).IsRequired();
        builder.Property(x => x.SenderNotes).HasMaxLength(500);
        builder.Property(x => x.InternalNotes).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);

        builder.HasIndex(x => x.ShipmentNumber).IsUnique();
        builder.HasIndex(x => x.TrackingNumber).IsUnique();
        builder.HasIndex(x => new { x.SenderCustomerId, x.ExternalReference })
            .IsUnique()
            .HasFilter("[ExternalReference] IS NOT NULL")
            .HasDatabaseName("UX_Shipments_Sender_ExternalReference");

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => new { x.OriginBranchId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.CurrentBranchId, x.Status });
        builder.HasIndex(x => new { x.DestinationBranchId, x.Status });
        builder.HasIndex(x => new { x.AssignedDriverId, x.Status });
        builder.HasIndex(x => x.SenderCustomerId);
        builder.HasIndex(x => x.RecipientCustomerId);
        builder.HasIndex(x => x.DestinationCityId);
        builder.HasIndex(x => x.ServiceTypeId);
        builder.HasIndex(x => x.IsCOD).HasFilter("[IsCOD] = 1");

        builder.HasOne(x => x.SenderCustomer).WithMany().HasForeignKey(x => x.SenderCustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecipientCustomer).WithMany().HasForeignKey(x => x.RecipientCustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SenderAddress).WithMany().HasForeignKey(x => x.SenderAddressId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecipientAddress).WithMany().HasForeignKey(x => x.RecipientAddressId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginBranch).WithMany().HasForeignKey(x => x.OriginBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CurrentBranch).WithMany().HasForeignKey(x => x.CurrentBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CurrentWarehouse).WithMany().HasForeignKey(x => x.CurrentWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DestinationBranch).WithMany().HasForeignKey(x => x.DestinationBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReturnBranch).WithMany().HasForeignKey(x => x.ReturnBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginCity).WithMany().HasForeignKey(x => x.OriginCityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DestinationCity).WithMany().HasForeignKey(x => x.DestinationCityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginZone).WithMany().HasForeignKey(x => x.OriginZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DestinationZone).WithMany().HasForeignKey(x => x.DestinationZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ServiceType).WithMany().HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedDriver).WithMany().HasForeignKey(x => x.AssignedDriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);

        // Packages are replaced while the shipment is editable, so they are the only cascading child.
        builder.HasMany(x => x.Packages).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.StatusHistory).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Attachments).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.PricingSnapshots).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShipmentPackageConfiguration : IEntityTypeConfiguration<ShipmentPackage>
{
    public void Configure(EntityTypeBuilder<ShipmentPackage> builder)
    {
        builder.ToTable("ShipmentPackages");

        builder.Property(x => x.PackageBarcode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.WeightKg).HasPrecision(10, 3);
        builder.Property(x => x.VolumetricWeightKg).HasPrecision(10, 3);
        builder.Property(x => x.LengthCm).HasPrecision(10, 2);
        builder.Property(x => x.WidthCm).HasPrecision(10, 2);
        builder.Property(x => x.HeightCm).HasPrecision(10, 2);

        builder.HasIndex(x => x.PackageBarcode).IsUnique();
        builder.HasIndex(x => new { x.ShipmentId, x.SequenceNumber }).IsUnique();
    }
}

public sealed class ShipmentStatusHistoryConfiguration : IEntityTypeConfiguration<ShipmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<ShipmentStatusHistory> builder)
    {
        builder.ToTable("ShipmentStatusHistory");

        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.ReferenceType).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(30).IsUnicode(false);

        builder.HasIndex(x => new { x.ShipmentId, x.OccurredAtUtc });

        // Foreign keys without navigation properties: integrity only.
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShipmentAttachmentConfiguration : IEntityTypeConfiguration<ShipmentAttachment>
{
    public void Configure(EntityTypeBuilder<ShipmentAttachment> builder)
    {
        builder.ToTable("ShipmentAttachments");

        builder.Property(x => x.AttachmentType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(300).IsUnicode(false).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300);

        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => x.ShipmentId);

        builder.HasOne(x => x.DamageCase).WithMany().HasForeignKey(x => x.DamageCaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShipmentPricingSnapshotConfiguration : IEntityTypeConfiguration<ShipmentPricingSnapshot>
{
    public void Configure(EntityTypeBuilder<ShipmentPricingSnapshot> builder)
    {
        builder.ToTable("ShipmentPricingSnapshots");

        builder.Property(x => x.Reason).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.PricingSource).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.PricingPlanCode).HasMaxLength(20);
        builder.Property(x => x.PricingRuleName).HasMaxLength(150);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10).IsUnicode(false).IsRequired();
        builder.Property(x => x.ActualWeight).HasPrecision(10, 3);
        builder.Property(x => x.VolumetricWeight).HasPrecision(10, 3);
        builder.Property(x => x.ChargeableWeight).HasPrecision(10, 3);
        builder.Property(x => x.WeightRoundingStepKg).HasPrecision(6, 3);
        builder.Property(x => x.IncludedWeightKg).HasPrecision(10, 3);
        builder.Property(x => x.AdditionalWeightKg).HasPrecision(10, 3);
        builder.Property(x => x.PricingBasis).HasConversion<string>().HasMaxLength(20).IsUnicode(false).HasDefaultValue(PricingBasis.Weight);
        builder.Property(x => x.VolumeCbm).HasPrecision(10, 3);
        builder.Property(x => x.WeightTiersJson).HasMaxLength(4000);
        builder.Property(x => x.CodFeePercentage).HasPrecision(5, 2);
        builder.Property(x => x.ContractDiscountPercentage).HasPrecision(5, 2);
        builder.Property(x => x.TaxPercentage).HasPrecision(5, 2);
        builder.Property(x => x.OverrideReason).HasMaxLength(500);

        builder.HasIndex(x => x.ShipmentId);

        // No foreign key to the price rule: snapshots from step 4 point to the old CustomerPricingPlanRules table.
        builder.HasOne<CustomerPricingPlan>().WithMany().HasForeignKey(x => x.PricingPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxRate>().WithMany().HasForeignKey(x => x.TaxRateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProofOfDeliveryConfiguration : IEntityTypeConfiguration<ProofOfDelivery>
{
    public void Configure(EntityTypeBuilder<ProofOfDelivery> builder)
    {
        builder.ToTable("ProofsOfDelivery");

        builder.Property(x => x.RecipientName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.RecipientRelation).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);

        builder.HasIndex(x => x.ShipmentId);

        builder.HasOne(x => x.Shipment).WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SignatureAttachment).WithMany().HasForeignKey(x => x.SignatureAttachmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PhotoAttachment).WithMany().HasForeignKey(x => x.PhotoAttachmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DriverDeliveryManifest>().WithMany().HasForeignKey(x => x.DriverManifestId).OnDelete(DeleteBehavior.Restrict);
    }
}
