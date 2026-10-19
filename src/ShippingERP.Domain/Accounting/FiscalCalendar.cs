using System.Globalization;

namespace ShippingERP.Domain.Accounting;

public sealed record PeriodPlan(int Number, string Name, DateTime StartDate, DateTime EndDate);

/// <summary>Builds the monthly accounting periods of a fiscal year (pure function, unit tested).</summary>
public static class FiscalCalendar
{
    private static readonly string[] MonthNames =
    {
        "كانون الثاني", "شباط", "آذار", "نيسان", "أيار", "حزيران",
        "تموز", "آب", "أيلول", "تشرين الأول", "تشرين الثاني", "كانون الأول"
    };

    public static string ArabicMonthName(int month) => MonthNames[month - 1];

    /// <summary>A date with only Y-M-D, kind UTC (how "date" columns are stored).</summary>
    public static DateTime AsDate(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    /// <summary>
    /// One period per calendar month between <paramref name="start"/> and <paramref name="end"/> (inclusive). A year
    /// that does not start on the 1st gets a shorter first period; the last period ends on <paramref name="end"/>.
    /// </summary>
    public static IReadOnlyList<PeriodPlan> MonthlyPeriods(DateTime start, DateTime end)
    {
        start = AsDate(start);
        end = AsDate(end);
        if (end < start)
        {
            throw new ArgumentException("The end date is before the start date.", nameof(end));
        }

        var periods = new List<PeriodPlan>();
        var cursor = start;
        var number = 1;

        while (cursor <= end)
        {
            var monthEnd = AsDate(new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month)));
            var periodEnd = monthEnd < end ? monthEnd : end;
            var name = string.Create(CultureInfo.InvariantCulture, $"{ArabicMonthName(cursor.Month)} {cursor.Year}");

            periods.Add(new PeriodPlan(number++, name, cursor, periodEnd));
            cursor = periodEnd.AddDays(1);
        }

        return periods;
    }
}
