using ShippingERP.Application.Common.Models;

namespace ShippingERP.Application.Common.Exceptions;

/// <summary>
/// Thrown when the caller could not be authenticated (wrong password, invalid refresh token...).
/// Mapped to HTTP 401.
/// </summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string errorCode, string message)
        : base(errorCode, message)
    {
    }

    public UnauthorizedException()
        : base(ErrorCodes.Unauthorized, "يجب تسجيل الدخول للوصول إلى هذا المورد.")
    {
    }
}
