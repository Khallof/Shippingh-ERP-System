using ShippingERP.Application.Interfaces;

namespace ShippingERP.Infrastructure.Services;

/// <summary>Production implementation of IDateTimeProvider: the real UTC clock.</summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
