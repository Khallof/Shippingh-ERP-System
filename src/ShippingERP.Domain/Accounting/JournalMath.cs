namespace ShippingERP.Domain.Accounting;

/// <summary>Base-currency conversion of journal lines (pure functions, unit tested).</summary>
public static class JournalMath
{
    public static decimal Round(decimal value, int decimals) =>
        Math.Round(value, Math.Clamp(decimals, 0, 4), MidpointRounding.AwayFromZero);

    /// <summary>
    /// Converts the lines to the base currency (amount × rate, rounded to the base decimals). Rounding each line can
    /// leave the base totals one unit apart although the entry is balanced in its own currency: that rounding
    /// difference is added to the biggest line of the smaller side, so the base amounts are balanced too.
    /// </summary>
    public static IReadOnlyList<JournalLineAmount> ToBase(IReadOnlyList<JournalLineAmount> lines, decimal exchangeRate, int baseDecimals)
    {
        if (exchangeRate <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(exchangeRate), "The exchange rate must be greater than zero.");
        }

        var result = lines
            .Select(x => new JournalLineAmount(Round(x.Debit * exchangeRate, baseDecimals), Round(x.Credit * exchangeRate, baseDecimals)))
            .ToArray();

        var difference = result.Sum(x => x.Debit) - result.Sum(x => x.Credit);
        if (difference == 0m || result.Length == 0)
        {
            return result;
        }

        if (difference > 0m)
        {
            // Debits are bigger: raise the biggest credit line.
            var index = IndexOfMax(result, x => x.Credit);
            if (index >= 0)
            {
                result[index] = result[index] with { Credit = result[index].Credit + difference };
            }
        }
        else
        {
            var index = IndexOfMax(result, x => x.Debit);
            if (index >= 0)
            {
                result[index] = result[index] with { Debit = result[index].Debit - difference };
            }
        }

        return result;
    }

    private static int IndexOfMax(JournalLineAmount[] lines, Func<JournalLineAmount, decimal> selector)
    {
        var index = -1;
        var max = 0m;
        for (var i = 0; i < lines.Length; i++)
        {
            var value = selector(lines[i]);
            if (value > max)
            {
                max = value;
                index = i;
            }
        }

        return index;
    }
}
