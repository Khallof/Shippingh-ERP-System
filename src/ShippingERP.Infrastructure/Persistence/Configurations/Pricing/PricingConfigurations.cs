using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Entities.Pricing;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PriceRuleConfiguration : IEntityTypeConfiguration<PriceRule>
{
    public void Configure(EntityTypeBuilder<PriceRule> builder)
    {
        builder.ToTable("PriceRules");

        builder.Property(x => x.ArabicName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.EnglishName).HasMaxLength(150);
        builder.Property(x => x.IncludedWeightKg).HasPrecision(10, 3);
        builder.Property(x => x.AppliesToBasis).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.CodFeePercentage).HasPrecision(5, 2);
        builder.Property(x => x.InsuranceFeePercentage).HasPrecision(5, 2);
        builder.Property(x => x.DiscountPercentage).HasPrecision(5, 2);
        builder.Property(x => x.Notes).HasMaxLength(500);

        // The engine loads the active rules of one plan.
        builder.HasIndex(x => new { x.PricingPlanId, x.IsActive, x.Priority });

        builder.HasOne(x => x.TaxRate).WithMany().HasForeignKey(x => x.TaxRateId).OnDelete(DeleteBehavior.Restrict);

        // Conditions and tiers belong to the rule; they are replaced when the rule is saved.
        builder.HasMany(x => x.Conditions).WithOne(x => x.PriceRule).HasForeignKey(x => x.PriceRuleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.WeightTiers).WithOne(x => x.PriceRule).HasForeignKey(x => x.PriceRuleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PriceRuleConditionConfiguration : IEntityTypeConfiguration<PriceRuleCondition>
{
    public void Configure(EntityTypeBuilder<PriceRuleCondition> builder)
    {

        builder.Property(x => x.ConditionType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.MinValue).HasPrecision(10, 3);
        builder.Property(x => x.MaxValue).HasPrecision(10, 3);

        builder.HasIndex(x => x.PriceRuleId);

        // "Is this zone / city / service type used by a rule?" (delete guards of the master data).
        builder.HasIndex(x => new { x.ConditionType, x.ReferenceId });

        // ReferenceId has no foreign key on purpose: depending on ConditionType it points to Zones, Cities or ServiceTypes.
        builder.ToTable("PriceRuleConditions", table => table.HasCheckConstraint(
            "CK_PriceRuleConditions_Shape",
            "([ReferenceId] IS NOT NULL AND [MinValue] IS NULL AND [MaxValue] IS NULL) OR " +
            "([ReferenceId] IS NULL AND ([MinValue] IS NOT NULL OR [MaxValue] IS NOT NULL))"));
    }
}

public sealed class PriceRuleWeightTierConfiguration : IEntityTypeConfiguration<PriceRuleWeightTier>
{
    public void Configure(EntityTypeBuilder<PriceRuleWeightTier> builder)
    {

        builder.Property(x => x.FromWeightKg).HasPrecision(10, 3);
        builder.Property(x => x.ToWeightKg).HasPrecision(10, 3);

        builder.HasIndex(x => new { x.PriceRuleId, x.FromWeightKg }).IsUnique();

        builder.ToTable("PriceRuleWeightTiers", table => table.HasCheckConstraint(
            "CK_PriceRuleWeightTiers_Range",
            "[ToWeightKg] IS NULL OR [ToWeightKg] > [FromWeightKg]"));
    }
}

public sealed class ShipmentPriceOverrideConfiguration : IEntityTypeConfiguration<ShipmentPriceOverride>
{
    public void Configure(EntityTypeBuilder<ShipmentPriceOverride> builder)
    {
        builder.ToTable("ShipmentPriceOverrides");

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DecisionNotes).HasMaxLength(500);
        builder.Property(x => x.DeviationPercentage).HasPrecision(9, 2);

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => x.ShipmentId);

        // At most one pending request per shipment.
        builder.HasIndex(x => x.ShipmentId, "UX_ShipmentPriceOverrides_OnePending")
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");

        builder.HasOne(x => x.Shipment).WithMany(x => x.PriceOverrides).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
    }
}
