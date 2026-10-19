using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Entities.Settings;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>The single shipping settings row (defaults are used until the first PUT).</summary>
public sealed class ShippingSettingsService : IShippingSettingsService
{
    private readonly MasterDataServiceDependencies _dependencies;

    public ShippingSettingsService(MasterDataServiceDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    private AppDbContext Db => _dependencies.DbContext;

    /// <summary>The saved settings, or a new (not saved) object with the default values.</summary>
    public async Task<ShippingSettings> GetEffectiveAsync(CancellationToken cancellationToken)
    {
        return await Db.ShippingSettings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken)
               ?? new ShippingSettings();
    }

    public async Task<ShippingSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        return ToDto(await GetEffectiveAsync(cancellationToken));
    }

    public async Task<ShippingSettingsDto> SaveAsync(SaveShippingSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = await Db.ShippingSettings.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        var isNew = settings is null;
        settings ??= new ShippingSettings();

        var before = new
        {
            settings.VolumetricDivisor,
            settings.WeightRoundingStepKg,
            settings.TrackingNumberPrefix,
            settings.LabelWidthMm,
            settings.LabelHeightMm,
            settings.TrackingUrlTemplate,
            settings.PriceOverrideApprovalThresholdPercent,
            settings.DefaultPaymentTermsDays
        };

        settings.VolumetricDivisor = request.VolumetricDivisor;
        settings.WeightRoundingStepKg = request.WeightRoundingStepKg;
        settings.TrackingNumberPrefix = request.TrackingNumberPrefix.Trim().ToUpperInvariant();
        settings.LabelWidthMm = request.LabelWidthMm;
        settings.LabelHeightMm = request.LabelHeightMm;
        settings.TrackingUrlTemplate = string.IsNullOrWhiteSpace(request.TrackingUrlTemplate) ? null : request.TrackingUrlTemplate.Trim();
        settings.PriceOverrideApprovalThresholdPercent = request.PriceOverrideApprovalThresholdPercent;
        settings.DefaultPaymentTermsDays = request.DefaultPaymentTermsDays;

        if (isNew)
        {
            Db.ShippingSettings.Add(settings);
        }

        _dependencies.AuditLog.Add(
            AuditAction.ShippingSettingsChanged,
            entityName: "ShippingSettings",
            details: new { before, after = request });

        // One SaveChanges = one transaction (settings + audit row).
        await Db.SaveChangesAsync(cancellationToken);

        return ToDto(settings);
    }

    private static ShippingSettingsDto ToDto(ShippingSettings settings) => new()
    {
        VolumetricDivisor = settings.VolumetricDivisor,
        WeightRoundingStepKg = settings.WeightRoundingStepKg,
        TrackingNumberPrefix = settings.TrackingNumberPrefix,
        LabelWidthMm = settings.LabelWidthMm,
        LabelHeightMm = settings.LabelHeightMm,
        TrackingUrlTemplate = settings.TrackingUrlTemplate,
        PriceOverrideApprovalThresholdPercent = settings.PriceOverrideApprovalThresholdPercent,
        DefaultPaymentTermsDays = settings.DefaultPaymentTermsDays,
        IsDefault = settings.Id == 0,
        UpdatedAtUtc = settings.UpdatedAtUtc ?? (settings.Id == 0 ? null : settings.CreatedAtUtc)
    };
}
