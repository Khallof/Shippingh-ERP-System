namespace ShippingERP.Application.Interfaces;

/// <summary>
/// Gives the Application and Infrastructure layers information about the caller of the current request,
/// without depending on ASP.NET Core. Implemented in the API layer (reads JWT claims and the HTTP connection).
/// </summary>
public interface ICurrentUserService
{
    /// <summary>The current user's id, or null for anonymous requests and background jobs.</summary>
    long? UserId { get; }

    bool IsAuthenticated { get; }

    /// <summary>Branch from the token. Null = head office or anonymous.</summary>
    long? BranchId { get; }

    /// <summary>True when the caller has the SystemAdmin role.</summary>
    bool IsSystemAdmin { get; }

    /// <summary>Client IP address (for audit logs and refresh tokens).</summary>
    string? IpAddress { get; }

    /// <summary>Client User-Agent header (for audit logs and refresh tokens).</summary>
    string? UserAgent { get; }

    /// <summary>True when the caller holds the permission (SystemAdmin holds all).</summary>
    bool HasPermission(string permission);

    /// <summary>True when the caller's token contains the role (e.g. Roles.Driver).</summary>
    bool IsInRole(string role);
}
