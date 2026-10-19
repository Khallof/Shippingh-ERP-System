namespace ShippingERP.Domain.Cod;

/// <summary>What is deducted from (or added to) the COD collected for a sender.</summary>
public sealed record CodDeductions(decimal ShippingCharge, decimal CodCommission, decimal Tax, decimal OtherFees, decimal Compensation)
{
    public static CodDeductions None { get; } = new(0m, 0m, 0m, 0m, 0m);

    /// <summary>Everything kept by the company (not the compensation).</summary>
    public decimal TotalDeductions => ShippingCharge + CodCommission + Tax + OtherFees;
}

/// <summary>The amounts of one sender settlement line (see SenderSettlementLine).</summary>
public sealed record SettlementLinePlan(
    decimal RemainingBefore,
    decimal Amount,
    bool RecognizesDeductions,
    CodDeductions Recognized,
    decimal GrossReleased);

/// <summary>
/// The money arithmetic of COD settlements (pure functions, unit-tested).
///
///   Net payable to the sender = collected - shipping charge - COD commission - tax - other fees + compensation
///
/// A COD can be settled in several parts. Its deductions are recognized once, on the first line, so that over
/// all its lines: sum(GrossReleased) = collected and sum(Amount) = net payable.
/// </summary>
public static class CodSettlementMath
{
    public static decimal NetPayable(decimal collected, CodDeductions deductions) =>
        collected - deductions.TotalDeductions + deductions.Compensation;

    /// <summary>
    /// Plans a settlement line. <paramref name="requestedAmount"/> null = the whole remaining amount.
    /// Returns null and an Arabic error when the request is not possible.
    /// </summary>
    public static SettlementLinePlan? PlanLine(
        decimal netPayable,
        decimal settledAmount,
        bool deductionsRecognized,
        CodDeductions deductions,
        decimal? requestedAmount,
        out string? error)
    {
        error = null;
        var remaining = netPayable - settledAmount;

        if (remaining == 0m)
        {
            error = "لا يوجد مبلغ متبقٍ للتسوية.";
            return null;
        }

        decimal amount;
        if (remaining < 0m)
        {
            // The charges are higher than the money collected: the line reduces the settlement and cannot be split.
            if (requestedAmount.HasValue && requestedAmount.Value != remaining)
            {
                error = $"صافي هذه الشحنة سالب ({remaining}): الرسوم أكبر من المبلغ المحصّل، ويُخصم كاملاً ولا يُجزّأ.";
                return null;
            }

            amount = remaining;
        }
        else
        {
            amount = requestedAmount ?? remaining;
            if (amount <= 0m || amount > remaining)
            {
                error = $"المبلغ يجب أن يكون أكبر من صفر ولا يتجاوز المتبقي ({remaining}).";
                return null;
            }
        }

        var recognizes = !deductionsRecognized;
        var recognized = recognizes ? deductions : CodDeductions.None;
        var gross = amount + recognized.TotalDeductions - recognized.Compensation;

        return new SettlementLinePlan(remaining, amount, recognizes, recognized, gross);
    }
}
