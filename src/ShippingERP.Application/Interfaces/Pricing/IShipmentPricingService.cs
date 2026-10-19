using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Pricing;

namespace ShippingERP.Application.Interfaces.Pricing;

/// <summary>
/// Pricing of an existing shipment (step 5): preview, apply and the approval of manual price changes.
/// The shipment visibility rules of step 4 apply (a shipment outside the caller's scope is 404).
/// </summary>
public interface IShipmentPricingService
{
    /// <summary>Calculates the price with the current rules (and optional manual values) without saving.</summary>
    Task<ShipmentPriceResultDto> CalculateAsync(long shipmentId, ShipmentPriceRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the new price (new snapshot + shipment totals) in one transaction, or records a Pending override
    /// when the manual change needs approval. Refused when the shipment is cancelled or already invoiced.
    /// </summary>
    Task<ShipmentPriceResultDto> ApplyAsync(long shipmentId, ShipmentPriceRequest request, CancellationToken cancellationToken);

    Task<PagedResult<PriceOverrideDto>> GetOverridesAsync(PriceOverrideListQuery query, CancellationToken cancellationToken);

    Task<PriceOverrideDto> GetOverrideAsync(long id, CancellationToken cancellationToken);

    /// <summary>Applies a pending override (Pricing.ApproveOverride, approver != requester).</summary>
    Task<ShipmentPriceResultDto> ApproveOverrideAsync(long id, DecidePriceOverrideRequest request, CancellationToken cancellationToken);

    Task<PriceOverrideDto> RejectOverrideAsync(long id, DecidePriceOverrideRequest request, CancellationToken cancellationToken);
}
