namespace ShippingERP.Domain.Shipments;

/// <summary>
/// Weight rules used for pricing.
///
/// Volumetric weight (kg) of a package = length x width x height (cm) / divisor.
/// The divisor comes from the shipping settings (5000 is the common courier value; some use 4000 or 6000).
///
/// Chargeable weight = the bigger of the actual weight and the volumetric weight, rounded UP to the
/// rounding step (e.g. 0.5 kg: 2.1 -> 2.5). A step of 0 means no rounding. All values have 3 decimals.
/// </summary>
public static class ShipmentWeightCalculator
{
    public const int WeightDecimals = 3;

    /// <summary>Volumetric weight of one package, or 0 when a dimension is missing.</summary>
    public static decimal VolumetricWeight(decimal? lengthCm, decimal? widthCm, decimal? heightCm, int divisor)
    {
        if (divisor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisor), "The volumetric divisor must be positive.");
        }

        if (lengthCm is not > 0m || widthCm is not > 0m || heightCm is not > 0m)
        {
            return 0m;
        }

        var volume = lengthCm.Value * widthCm.Value * heightCm.Value;
        return Math.Round(volume / divisor, WeightDecimals, MidpointRounding.AwayFromZero);
    }

    public static decimal ChargeableWeight(decimal actualWeight, decimal volumetricWeight, decimal roundingStepKg)
    {
        var weight = Math.Max(actualWeight, volumetricWeight);
        return RoundUp(weight, roundingStepKg);
    }

    /// <summary>Rounds up to the next multiple of <paramref name="step"/> (step 0 = 3-decimal rounding only).</summary>
    public static decimal RoundUp(decimal value, decimal step)
    {
        if (step <= 0m)
        {
            return Math.Round(value, WeightDecimals, MidpointRounding.AwayFromZero);
        }

        var steps = Math.Ceiling(value / step);
        return Math.Round(steps * step, WeightDecimals, MidpointRounding.AwayFromZero);
    }
}
