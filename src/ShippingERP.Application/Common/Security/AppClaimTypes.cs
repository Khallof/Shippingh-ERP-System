namespace ShippingERP.Application.Common.Security;

/// <summary>
/// Claim names written into the JWT access token. Short names keep the token small.
///
/// Example token payload:
/// {
///   "sub": "1", "unique_name": "admin", "full_name": "System Administrator",
///   "role": ["SystemAdmin"], "branch_id": "3",
///   "permission": ["Users.View", "Shipments.Create"],
///   "sstamp": "...", "jti": "...", "exp": 1760000000
/// }
/// </summary>
public static class AppClaimTypes
{
    /// <summary>User id (standard JWT "subject").</summary>
    public const string UserId = "sub";

    public const string UserName = "unique_name";

    public const string FullName = "full_name";

    public const string Role = "role";

    /// <summary>Only present when the user belongs to a branch.</summary>
    public const string BranchId = "branch_id";

    /// <summary>One claim per permission code.</summary>
    public const string Permission = "permission";

    /// <summary>
    /// Identity security stamp at the time the token was issued. When it changes (password change,
    /// deactivation, role or permission change) every older access token is rejected.
    /// </summary>
    public const string SecurityStamp = "sstamp";

    /// <summary>Unique token id.</summary>
    public const string TokenId = "jti";
}
