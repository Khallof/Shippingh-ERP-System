namespace ShippingERP.Application.Common.Models;

/// <summary>
/// Error part of the standard API response.
/// </summary>
public sealed class ApiError
{
    /// <summary>Stable machine-readable code (see <see cref="ErrorCodes"/>). The frontend switches on this, not on the message.</summary>
    public string Code { get; init; } = ErrorCodes.InternalServerError;

    /// <summary>Human-readable message (Arabic by default).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional field-level details. Key = field name (e.g. "phoneNumber"), value = list of messages.
    /// Used mainly for validation errors.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Details { get; init; }
}
