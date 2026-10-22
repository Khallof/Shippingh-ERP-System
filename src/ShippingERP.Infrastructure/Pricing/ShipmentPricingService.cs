using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Pricing;
using ShippingERP.Application.Interfaces.Pricing;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Billing;
using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Entities.Settings;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Pricing;

/// <summary>
/// Repricing of existing shipments and the approval of manual price changes (step 5).
///
/// - calculate-price: preview only.
/// - apply-price without manual values: recalculates from the current rules (snapshot "Repriced").
/// - apply-price with manual values (Shipments.OverridePrice + reason):
///     * small change (deviation &lt;= threshold) or caller holds Pricing.ApproveOverride -> applied at once
///       (snapshot "ManualOverride", override row Approved);
///     * otherwise -> override row Pending, the shipment price does not change until an approver approves.
/// A new apply-price replaces (cancels) the shipment's pending request. Cancelled or invoiced shipments cannot be
/// repriced. Every write is one transaction with its audit row.
/// </summary>
public sealed class ShipmentPricingService : IShipmentPricingService
{
    private const string EntityName = "Shipment";

    private static readonly Dictionary<string, Func<IQueryable<ShipmentPriceOverride>, bool, IOrderedQueryable<ShipmentPriceOverride>>> Sorts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["createdAtUtc"] = (q, desc) => desc ? q.OrderByDescending(x => x.CreatedAtUtc) : q.OrderBy(x => x.CreatedAtUtc),
            ["deviationPercentage"] = (q, desc) => desc ? q.OrderByDescending(x => x.DeviationPercentage) : q.OrderBy(x => x.DeviationPercentage),
            ["status"] = (q, desc) => desc ? q.OrderByDescending(x => x.Status) : q.OrderBy(x => x.Status)
        };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentAccessService _access;
    private readonly ShipmentPricingCalculator _calculator;
    private readonly ShippingSettingsService _settings;

    public ShipmentPricingService(
        MasterDataServiceDependencies dependencies,
        ShipmentAccessService access,
        ShipmentPricingCalculator calculator,
        ShippingSettingsService settings,
        ShipmentAutoBilling autoBilling)
    {
        _dependencies = dependencies;
        _access = access;
        _calculator = calculator;
        _settings = settings;
        _autoBilling = autoBilling;
    }

    private readonly ShipmentAutoBilling _autoBilling;

    private AppDbContext Db => _dependencies.DbContext;

    // ------------------------------------------------------------------ calculate / apply

    public async Task<ShipmentPriceResultDto> CalculateAsync(long shipmentId, ShipmentPriceRequest request, CancellationToken cancellationToken)
    {
        var query = await _access.ScopeAsync(Db.Shipments.AsNoTracking(), cancellationToken);
        var shipment = await query.FirstOrDefaultAsync(x => x.Id == shipmentId, cancellationToken)
                       ?? throw new NotFoundException("الشحنة", shipmentId);

        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var result = await PriceAsync(shipment, request.ManualShippingCharge, request.ManualDiscountAmount, settings, cancellationToken);
        var evaluation = PriceOverrideEvaluation.From(result, settings.PriceOverrideApprovalThresholdPercent);

        return ToResult(shipment.Id, result, evaluation, settings, applied: false, overrideDto: null, reason: "Preview");
    }

    public async Task<ShipmentPriceResultDto> ApplyAsync(long shipmentId, ShipmentPriceRequest request, CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(shipmentId, cancellationToken);
        await EnsureCanRepriceAsync(shipment, cancellationToken);

        var isManual = request.ManualShippingCharge.HasValue || request.ManualDiscountAmount > 0m;
        // Code without a signed-in user (seeding, jobs) is trusted like everywhere else.
        var user = _dependencies.CurrentUser;
        if (isManual && user.IsAuthenticated && !user.HasPermission(Permissions.Shipments.OverridePrice))
        {
            throw new ForbiddenException("إدخال سعر يدوي أو خصم يحتاج صلاحية Shipments.OverridePrice.");
        }

        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var result = await PriceAsync(shipment, request.ManualShippingCharge, request.ManualDiscountAmount, settings, cancellationToken);
        var evaluation = PriceOverrideEvaluation.From(result, settings.PriceOverrideApprovalThresholdPercent);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var now = _dependencies.DateTimeProvider.UtcNow;
        var userId = _dependencies.CurrentUser.UserId;

        if (!isManual)
        {
            await ReleaseInvoiceAsync(shipment, cancellationToken);
            await Db.ExecuteInTransactionAsync(async ct =>
            {
                await ShipmentBillingGuards.CancelPendingOverridesAsync(Db, shipment.Id, userId, now, "أُلغي: أُعيد تسعير الشحنة بالقواعد.", ct);
                var before = new { shipment.ShippingCharge, shipment.DiscountAmount, shipment.TaxAmount, shipment.TotalCharge };

                ApplyToShipment(shipment, result);
                Db.ShipmentPricingSnapshots.Add(result.ToSnapshot(shipment, "Repriced"));
                _dependencies.AuditLog.Add(
                    AuditAction.ShipmentRepriced,
                    entityName: EntityName,
                    entityId: shipment.Id.ToString(CultureInfo.InvariantCulture),
                    details: new { before, after = new { shipment.ShippingCharge, shipment.DiscountAmount, shipment.TaxAmount, shipment.TotalCharge } });

                await Db.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);

            await _autoBilling.InvoiceAsync(shipment.Id, cancellationToken);
            return ToResult(shipment.Id, result, evaluation, settings, applied: true, overrideDto: null, reason: "Repriced");
        }

        var canApprove = !user.IsAuthenticated || user.HasPermission(Permissions.Pricing.ApproveOverride);
        var applyNow = !evaluation.RequiresApproval || canApprove;

        if (applyNow)
        {
            await ReleaseInvoiceAsync(shipment, cancellationToken);
        }

        var overrideId = await Db.ExecuteInTransactionAsync(async ct =>
        {
            await ShipmentBillingGuards.CancelPendingOverridesAsync(Db, shipment.Id, userId, now, "أُلغي: حلّ محله طلب أحدث.", ct);

            // Save the cancellation first: the unique index allows only one pending request per shipment.
            await Db.SaveChangesAsync(ct);

            var priceOverride = new ShipmentPriceOverride
            {
                ShipmentId = shipment.Id,
                Status = applyNow ? PriceOverrideStatus.Approved : PriceOverrideStatus.Pending,
                ManualShippingCharge = request.ManualShippingCharge,
                ManualDiscountAmount = request.ManualDiscountAmount,
                Reason = reason ?? string.Empty,
                CurrencyId = result.CurrencyId,
                CalculatedTotalShippingCharge = evaluation.RuleTotalShippingCharge,
                ProposedTotalShippingCharge = evaluation.ProposedTotalShippingCharge,
                DeviationPercentage = evaluation.DeviationPercentage,
                RequiresApproval = evaluation.RequiresApproval,
                DecidedByUserId = applyNow ? userId : null,
                DecidedAtUtc = applyNow ? now : null,
                DecisionNotes = applyNow
                    ? (evaluation.RequiresApproval ? "طُبّق مباشرة: المستخدم يملك صلاحية الاعتماد." : "طُبّق مباشرة: ضمن الحد المسموح.")
                    : null
            };
            Db.ShipmentPriceOverrides.Add(priceOverride);
            await Db.SaveChangesAsync(ct);

            if (applyNow)
            {
                await ApplyOverrideAsync(shipment, priceOverride, result, ct);
            }

            _dependencies.AuditLog.Add(
                applyNow ? AuditAction.PriceOverrideApproved : AuditAction.PriceOverrideRequested,
                entityName: EntityName,
                entityId: shipment.Id.ToString(CultureInfo.InvariantCulture),
                details: new
                {
                    overrideId = priceOverride.Id,
                    request.ManualShippingCharge,
                    request.ManualDiscountAmount,
                    reason,
                    evaluation.RuleTotalShippingCharge,
                    evaluation.ProposedTotalShippingCharge,
                    evaluation.DeviationPercentage
                });
            await Db.SaveChangesAsync(ct);

            return priceOverride.Id;
        }, cancellationToken);

        // Step 9: issue the invoice again when the new price was applied (a pending request keeps the old invoice).
        await _autoBilling.InvoiceAsync(shipment.Id, cancellationToken);

        var dto = await GetOverrideAsync(overrideId, cancellationToken);
        return ToResult(shipment.Id, result, evaluation, settings, applied: applyNow, overrideDto: dto, reason: applyNow ? "ManualOverride" : "Pending");
    }

    // ------------------------------------------------------------------ approval

    public async Task<PagedResult<PriceOverrideDto>> GetOverridesAsync(PriceOverrideListQuery query, CancellationToken cancellationToken)
    {
        var rows = await VisibleOverridesAsync(cancellationToken);

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.ShipmentId.HasValue)
        {
            var shipmentId = query.ShipmentId.Value;
            rows = rows.Where(x => x.ShipmentId == shipmentId);
        }

        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value;
            rows = rows.Where(x => x.CreatedAtUtc >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = query.ToUtc.Value;
            rows = rows.Where(x => x.CreatedAtUtc < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                x.Shipment.ShipmentNumber.Contains(search) ||
                x.Shipment.TrackingNumber.Contains(search) ||
                x.Shipment.SenderCustomer.ArabicName.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = !string.IsNullOrWhiteSpace(query.SortBy) && Sorts.TryGetValue(query.SortBy, out var sort)
            ? sort(rows, query.IsDescending())
            : rows.OrderByDescending(x => x.CreatedAtUtc);

        var items = await Project(ordered.ThenByDescending(x => x.Id)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<PriceOverrideDto>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<PriceOverrideDto> GetOverrideAsync(long id, CancellationToken cancellationToken)
    {
        var rows = await VisibleOverridesAsync(cancellationToken);
        return await Project(rows.Where(x => x.Id == id)).FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("طلب تعديل السعر", id);
    }

    public async Task<ShipmentPriceResultDto> ApproveOverrideAsync(long id, DecidePriceOverrideRequest request, CancellationToken cancellationToken)
    {
        var priceOverride = await FindPendingAsync(id, cancellationToken);
        var shipment = await _access.FindForUpdateAsync(priceOverride.ShipmentId, cancellationToken);
        await EnsureCanRepriceAsync(shipment, cancellationToken);

        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var result = await PriceAsync(shipment, priceOverride.ManualShippingCharge, priceOverride.ManualDiscountAmount, settings, cancellationToken);
        var evaluation = PriceOverrideEvaluation.From(result, settings.PriceOverrideApprovalThresholdPercent);
        var now = _dependencies.DateTimeProvider.UtcNow;

        await ReleaseInvoiceAsync(shipment, cancellationToken);
        await Db.ExecuteInTransactionAsync(async ct =>
        {
            priceOverride.Status = PriceOverrideStatus.Approved;
            priceOverride.DecidedByUserId = _dependencies.CurrentUser.UserId;
            priceOverride.DecidedAtUtc = now;
            priceOverride.DecisionNotes = Optional(request.Notes);
            priceOverride.CalculatedTotalShippingCharge = evaluation.RuleTotalShippingCharge;
            priceOverride.ProposedTotalShippingCharge = evaluation.ProposedTotalShippingCharge;
            priceOverride.DeviationPercentage = evaluation.DeviationPercentage;

            await ApplyOverrideAsync(shipment, priceOverride, result, ct);

            _dependencies.AuditLog.Add(
                AuditAction.PriceOverrideApproved,
                entityName: EntityName,
                entityId: shipment.Id.ToString(CultureInfo.InvariantCulture),
                details: new { overrideId = priceOverride.Id, request.Notes, shipment.TotalCharge });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        await _autoBilling.InvoiceAsync(shipment.Id, cancellationToken);

        var dto = await GetOverrideAsync(id, cancellationToken);
        return ToResult(shipment.Id, result, evaluation, settings, applied: true, overrideDto: dto, reason: "ManualOverride");
    }

    public async Task<PriceOverrideDto> RejectOverrideAsync(long id, DecidePriceOverrideRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Notes))
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["notes"] = new[] { "سبب الرفض مطلوب." } });
        }

        var priceOverride = await FindPendingAsync(id, cancellationToken);

        priceOverride.Status = PriceOverrideStatus.Rejected;
        priceOverride.DecidedByUserId = _dependencies.CurrentUser.UserId;
        priceOverride.DecidedAtUtc = _dependencies.DateTimeProvider.UtcNow;
        priceOverride.DecisionNotes = request.Notes.Trim();

        _dependencies.AuditLog.Add(
            AuditAction.PriceOverrideRejected,
            entityName: EntityName,
            entityId: priceOverride.ShipmentId.ToString(CultureInfo.InvariantCulture),
            details: new { overrideId = priceOverride.Id, request.Notes });

        await Db.SaveChangesAsync(cancellationToken);
        return await GetOverrideAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<PricingResult> PriceAsync(
        Shipment shipment,
        decimal? manualShippingCharge,
        decimal manualDiscountAmount,
        ShippingSettings settings,
        CancellationToken cancellationToken)
    {
        var shipmentId = shipment.Id;
        var senderId = shipment.SenderCustomerId;

        var senderPlanId = await Db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.Id == senderId)
            .Select(x => x.PricingPlanId)
            .FirstOrDefaultAsync(cancellationToken);

        var isReturn = await Db.ShipmentReturns.AsNoTracking()
            .AnyAsync(x => x.ShipmentId == shipmentId && x.Status != ShipmentReturnStatus.Cancelled, cancellationToken);

        return await _calculator.CalculateAsync(
            new PricingInput
            {
                SenderCustomerId = senderId,
                CustomerPricingPlanId = senderPlanId,
                OriginZoneId = shipment.OriginZoneId,
                DestinationZoneId = shipment.DestinationZoneId,
                OriginCityId = shipment.OriginCityId,
                DestinationCityId = shipment.DestinationCityId,
                ServiceTypeId = shipment.ServiceTypeId,
                ActualWeight = shipment.ActualWeight,
                VolumetricWeight = shipment.VolumetricWeight,
                ChargeableWeight = shipment.ChargeableWeight,
                PackageCount = shipment.PackageCount,
                PricingBasis = shipment.PricingBasis,
                VolumeCbm = shipment.TotalVolumeCbm,
                // A manual charge (price override) replaces the typed unit price.
                UnitPrice = manualShippingCharge.HasValue ? null : shipment.UnitPrice,
                UnitPriceCurrencyId = shipment.UnitPrice.HasValue && !manualShippingCharge.HasValue ? shipment.CurrencyId : null,
                ExtraFee = shipment.ExtraFee,
                IsCod = shipment.IsCOD,
                CodAmount = shipment.CODAmount,
                InsuranceValue = shipment.InsuranceValue,
                RequiresPickup = shipment.RequiresPickup,
                RequiresPackaging = shipment.RequiresPackaging,
                IsReturn = isReturn,
                ManualShippingCharge = manualShippingCharge,
                ManualDiscountAmount = manualDiscountAmount
            },
            settings,
            cancellationToken);
    }

    /// <summary>Step 9: cancels the automatic invoice just before a new price is saved (no-op when disabled).</summary>
    private async Task ReleaseInvoiceAsync(Shipment shipment, CancellationToken cancellationToken) =>
        await _autoBilling.ReleaseAsync(shipment.Id, $"تغيير سعر الشحنة {shipment.ShipmentNumber}", cancellationToken);

    /// <summary>Cancelled and invoiced shipments keep their price.</summary>
    private async Task EnsureCanRepriceAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        if (shipment.Status == ShipmentStatus.Cancelled)
        {
            throw new BusinessRuleException("لا يمكن تسعير شحنة ملغاة.");
        }

        if (_autoBilling.Enabled)
        {
            // Step 9: the automatic invoice is cancelled right before the new price is saved (ReleaseInvoiceAsync)
            // and issued again afterwards.
            return;
        }

        await ShipmentBillingGuards.EnsureNotInvoicedAsync(Db, shipment.Id, "تغيير سعر الشحنة", cancellationToken);
    }

    /// <summary>Writes the new price on the shipment and its snapshot; links the snapshot to the override.</summary>
    private async Task ApplyOverrideAsync(Shipment shipment, ShipmentPriceOverride priceOverride, PricingResult result, CancellationToken cancellationToken)
    {
        ApplyToShipment(shipment, result);

        var snapshot = result.ToSnapshot(shipment, "ManualOverride", priceOverride.Reason, priceOverride.Id);
        Db.ShipmentPricingSnapshots.Add(snapshot);
        await Db.SaveChangesAsync(cancellationToken);

        priceOverride.AppliedPricingSnapshotId = snapshot.Id;
    }

    private static void ApplyToShipment(Shipment shipment, PricingResult result)
    {
        shipment.CurrencyId = result.CurrencyId;
        shipment.ShippingCharge = result.ShippingCharge;
        shipment.DiscountAmount = result.DiscountAmount;
        shipment.TaxAmount = result.TaxAmount;
        shipment.TotalCharge = result.TotalCharge;
    }

    private async Task<ShipmentPriceOverride> FindPendingAsync(long id, CancellationToken cancellationToken)
    {
        var rows = await VisibleOverridesAsync(cancellationToken, tracked: true);
        var priceOverride = await rows.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                            ?? throw new NotFoundException("طلب تعديل السعر", id);

        if (priceOverride.Status != PriceOverrideStatus.Pending)
        {
            throw new BusinessRuleException($"الطلب ليس بانتظار الموافقة (الحالة: {BillingNames.Arabic(priceOverride.Status)}).");
        }

        var userId = _dependencies.CurrentUser.UserId;
        if (userId.HasValue && priceOverride.CreatedByUserId == userId)
        {
            throw new BusinessRuleException("لا يمكن اعتماد أو رفض طلب تعديل السعر الذي قدّمته بنفسك؛ يجب أن يقرره مستخدم آخر.");
        }

        return priceOverride;
    }

    /// <summary>Overrides of the shipments the caller can see.</summary>
    private async Task<IQueryable<ShipmentPriceOverride>> VisibleOverridesAsync(CancellationToken cancellationToken, bool tracked = false)
    {
        // AsNoTracking anywhere in an EF query makes the WHOLE query untracked, so the shipment sub-query must not
        // use it when the overrides are loaded for an update.
        var shipments = await _access.ScopeAsync(tracked ? Db.Shipments : Db.Shipments.AsNoTracking(), cancellationToken);
        var overrides = tracked ? Db.ShipmentPriceOverrides : Db.ShipmentPriceOverrides.AsNoTracking();
        return overrides.Where(x => shipments.Any(s => s.Id == x.ShipmentId));
    }

    private IQueryable<PriceOverrideDto> Project(IQueryable<ShipmentPriceOverride> query) => query.Select(x => new PriceOverrideDto
    {
        Id = x.Id,
        ShipmentId = x.ShipmentId,
        ShipmentNumber = x.Shipment.ShipmentNumber,
        TrackingNumber = x.Shipment.TrackingNumber,
        SenderName = x.Shipment.SenderCustomer.ArabicName,
        Status = x.Status,
        StatusName = BillingNames.Arabic(x.Status),
        ManualShippingCharge = x.ManualShippingCharge,
        ManualDiscountAmount = x.ManualDiscountAmount,
        Reason = x.Reason,
        CurrencyCode = Db.Currencies.IgnoreQueryFilters().Where(c => c.Id == x.CurrencyId).Select(c => c.Code).FirstOrDefault() ?? string.Empty,
        CalculatedTotalShippingCharge = x.CalculatedTotalShippingCharge,
        ProposedTotalShippingCharge = x.ProposedTotalShippingCharge,
        DeviationPercentage = x.DeviationPercentage,
        RequiresApproval = x.RequiresApproval,
        RequestedByUserId = x.CreatedByUserId,
        RequestedByUserName = Db.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault(),
        RequestedAtUtc = x.CreatedAtUtc,
        DecidedByUserId = x.DecidedByUserId,
        DecidedByUserName = Db.Users.Where(u => u.Id == x.DecidedByUserId).Select(u => u.FullName).FirstOrDefault(),
        DecidedAtUtc = x.DecidedAtUtc,
        DecisionNotes = x.DecisionNotes,
        AppliedPricingSnapshotId = x.AppliedPricingSnapshotId
    });

    private static ShipmentPriceResultDto ToResult(
        long shipmentId,
        PricingResult result,
        PriceOverrideEvaluation evaluation,
        ShippingSettings settings,
        bool applied,
        PriceOverrideDto? overrideDto,
        string reason) => new()
    {
        ShipmentId = shipmentId,
        Pricing = result.ToDto(reason),
        RulePricing = result.ToRuleDto(),
        IsManual = evaluation.IsManual,
        DeviationPercentage = evaluation.DeviationPercentage,
        ApprovalThresholdPercent = settings.PriceOverrideApprovalThresholdPercent,
        RequiresApproval = evaluation.RequiresApproval,
        Applied = applied,
        Override = overrideDto
    };

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
