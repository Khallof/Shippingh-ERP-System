using Microsoft.Extensions.Options;

namespace ShippingERP.Infrastructure.Cash;

/// <summary>Settings of the cash module (appsettings section "Cash").</summary>
public sealed class CashOptions
{
    public const string SectionName = "Cash";

    /// <summary>
    /// IANA time zone of the business day used by cashbox closings and the daily cash report. All dates are stored
    /// in UTC; only the "which day does this belong to" question uses this zone. Unknown zone = UTC.
    /// </summary>
    public string BusinessTimeZoneId { get; set; } = "Asia/Damascus";

    /// <summary>
    /// False (default, recommended): approvals (settlements, adjustments, transfers, closing differences) must be
    /// made by a user other than the requester. True only for a one-person test installation.
    /// </summary>
    public bool AllowSelfApproval { get; set; }
}

/// <summary>Converts UTC instants to local business days and back (see <see cref="CashOptions.BusinessTimeZoneId"/>).</summary>
public sealed class BusinessCalendar
{
    public BusinessCalendar(IOptions<CashOptions> options)
    {
        var id = options.Value.BusinessTimeZoneId;
        try
        {
            Zone = string.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            Zone = TimeZoneInfo.Utc;
        }
    }

    public TimeZoneInfo Zone { get; }

    public string ZoneId => Zone.Id;

    /// <summary>The local business day of a UTC instant, as a date (kind Utc so it is stored unchanged).</summary>
    public DateTime BusinessDate(DateTime utc)
    {
        var instant = utc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(utc, DateTimeKind.Utc) : utc.ToUniversalTime();
        var local = TimeZoneInfo.ConvertTimeFromUtc(instant, Zone);
        return DateTime.SpecifyKind(local.Date, DateTimeKind.Utc);
    }

    /// <summary>A date (any kind; only Y-M-D is used) normalized like <see cref="BusinessDate"/>.</summary>
    public static DateTime AsDate(DateTime date) => DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    /// <summary>The UTC instants [start, end) of a local business day.</summary>
    public (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateTime businessDate) =>
        (LocalMidnightToUtc(businessDate.Date), LocalMidnightToUtc(businessDate.Date.AddDays(1)));

    private DateTime LocalMidnightToUtc(DateTime date)
    {
        var local = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);

        // Some zones start daylight saving time at midnight: that local time does not exist, use the next valid hour.
        while (Zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(local, Zone), DateTimeKind.Utc);
    }
}
