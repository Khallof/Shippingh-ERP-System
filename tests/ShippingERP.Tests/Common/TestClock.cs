using ShippingERP.Application.Interfaces;

namespace ShippingERP.Tests.Common;

/// <summary>A clock the test controls. Starts at the real current UTC time.</summary>
public sealed class TestClock : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;

    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}
