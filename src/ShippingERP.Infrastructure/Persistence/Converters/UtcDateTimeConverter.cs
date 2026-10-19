using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ShippingERP.Infrastructure.Persistence.Converters;

/// <summary>
/// Guarantees that every DateTime is stored as UTC and comes back from SQL Server marked as UTC.
///
/// SQL Server datetime2 has no time zone, so EF Core returns DateTimeKind.Unspecified by default.
/// Without this converter the JSON output would lose the "Z" suffix and the frontend
/// might treat the value as local time (Damascus, Tripoli, ...) and show the wrong hour.
///
/// Writing:  Local      -> converted to UTC
///           Utc        -> stored as-is
///           Unspecified-> assumed to already be UTC
/// Reading:  always marked as DateTimeKind.Utc
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
