namespace ShippingERP.Application.Common.Models;

/// <summary>
/// Stable error codes returned in ApiError.Code.
/// Never rename an existing code: the frontend and mobile apps depend on them.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string RequestCancelled = "REQUEST_CANCELLED";
    public const string HttpError = "HTTP_ERROR";
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";

    // ----- Authentication (step 2) -----

    /// <summary>Wrong user name / e-mail or password. Deliberately does not say which one.</summary>
    public const string InvalidCredentials = "INVALID_CREDENTIALS";

    /// <summary>The account was deactivated by an administrator.</summary>
    public const string AccountDisabled = "ACCOUNT_DISABLED";

    /// <summary>Too many wrong passwords; the account is locked for a few minutes.</summary>
    public const string AccountLocked = "ACCOUNT_LOCKED";

    /// <summary>The access token expired or is outdated: call POST /auth/refresh-token, then retry.</summary>
    public const string TokenExpired = "TOKEN_EXPIRED";

    /// <summary>The refresh token is unknown, expired, revoked or reused: the user must sign in again.</summary>
    public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";

    /// <summary>Rate limit reached (too many sign-in attempts from one IP address).</summary>
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
}
