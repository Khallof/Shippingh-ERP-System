using ShippingERP.Application.Common.Models;

namespace ShippingERP.Application.Common.Exceptions;

/// <summary>
/// Thrown when the request is well-formed but breaks a business rule
/// (e.g. "cannot settle COD for a shipment that is not delivered"). Mapped to HTTP 422.
/// </summary>
public sealed class BusinessRuleException : AppException
{
    public BusinessRuleException(string message)
        : base(ErrorCodes.BusinessRuleViolation, message)
    {
    }
}
