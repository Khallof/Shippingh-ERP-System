using ShippingERP.Application.Common.Models;

namespace ShippingERP.Application.Common.Exceptions;

/// <summary>
/// Thrown when the user is authenticated but lacks permission for this action or record
/// (e.g. accessing another branch's data), or the account is disabled/locked. Mapped to HTTP 403.
/// </summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "ليس لديك صلاحية لتنفيذ هذه العملية.")
        : base(ErrorCodes.Forbidden, message)
    {
    }

    /// <summary>Use a more specific code, e.g. ErrorCodes.AccountDisabled or ErrorCodes.AccountLocked.</summary>
    public ForbiddenException(string errorCode, string message)
        : base(errorCode, message)
    {
    }
}
