namespace ShippingERP.Application.DTOs.Auth;

/// <summary>Everything the token service needs to build the access token claims.</summary>
public sealed record TokenUserInfo(
    long UserId,
    string UserName,
    string FullName,
    long? BranchId,
    string SecurityStamp,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

/// <summary>A signed JWT and its expiry.</summary>
public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// A new refresh token. RawToken goes to the client once; only TokenHash is saved in the database.
/// </summary>
public sealed record RefreshTokenResult(string RawToken, string TokenHash, DateTime ExpiresAtUtc);

/// <summary>What the JWT validation step needs to know about a user (cached for a short time).</summary>
public sealed record UserSessionState(long UserId, bool IsActive, string SecurityStamp);
