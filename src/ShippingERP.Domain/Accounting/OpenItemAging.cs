namespace ShippingERP.Domain.Accounting;

/// <summary>A ledger movement of a party: Increase raises what is owed (AP credit / AR debit), Decrease lowers it.</summary>
public readonly record struct PartyMovement(DateTime Date, decimal Increase, decimal Decrease);

/// <summary>An amount still open since <see cref="Date"/>.</summary>
public readonly record struct OpenItem(DateTime Date, decimal Amount);

public sealed record AgingResult(
    IReadOnlyList<OpenItem> OpenItems,
    decimal Unapplied,
    decimal Days0To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90Days)
{
    public decimal TotalOpen => Days0To30 + Days31To60 + Days61To90 + Over90Days;

    /// <summary>Open items minus payments not applied to anything (advances).</summary>
    public decimal NetBalance => TotalOpen - Unapplied;
}

/// <summary>
/// First-in first-out aging of a party balance from its ledger movements (pure function, unit tested): every
/// decrease (payment) settles the oldest open increase (bill); a payment made before any bill waits as an
/// advance and is used by the next bills. The remaining open increases are aged by their date.
/// </summary>
public static class OpenItemAging
{
    public static AgingResult Compute(IEnumerable<PartyMovement> movements, DateTime asOfDate)
    {
        var open = new LinkedList<OpenItem>();
        var advance = 0m;

        foreach (var movement in movements.OrderBy(x => x.Date))
        {
            var net = movement.Increase - movement.Decrease;
            if (net > 0m)
            {
                var used = Math.Min(advance, net);
                advance -= used;
                net -= used;
                if (net > 0m)
                {
                    open.AddLast(new OpenItem(movement.Date, net));
                }
            }
            else if (net < 0m)
            {
                var payment = -net;
                while (payment > 0m && open.First is not null)
                {
                    var first = open.First.Value;
                    if (first.Amount <= payment)
                    {
                        payment -= first.Amount;
                        open.RemoveFirst();
                    }
                    else
                    {
                        open.First.Value = first with { Amount = first.Amount - payment };
                        payment = 0m;
                    }
                }

                advance += payment;
            }
        }

        decimal d30 = 0m, d60 = 0m, d90 = 0m, over = 0m;
        foreach (var item in open)
        {
            var days = (asOfDate.Date - item.Date.Date).Days;
            if (days <= 30)
            {
                d30 += item.Amount;
            }
            else if (days <= 60)
            {
                d60 += item.Amount;
            }
            else if (days <= 90)
            {
                d90 += item.Amount;
            }
            else
            {
                over += item.Amount;
            }
        }

        return new AgingResult(open.ToList(), advance, d30, d60, d90, over);
    }
}
