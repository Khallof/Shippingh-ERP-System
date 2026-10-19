using ShippingERP.Application.Common.Models;

namespace ShippingERP.Application.Common.Exceptions;

/// <summary>
/// Thrown when the request conflicts with existing data (e.g. duplicate customer code). Mapped to HTTP 409.
/// </summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message)
        : base(ErrorCodes.Conflict, message)
    {
    }
}
