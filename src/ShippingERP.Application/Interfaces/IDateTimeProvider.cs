namespace ShippingERP.Application.Interfaces;

/// <summary>
/// Abstraction over the system clock. Always use this instead of DateTime.UtcNow in business code,
/// so tests can control the current time (important for COD settlement cut-offs, aging reports, etc.).
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
