namespace ShippingERP.Application.DTOs.Auth;

/// <summary>
/// Returned by login, refresh-token and change-password.
/// The client sends AccessToken in the header "Authorization: Bearer {accessToken}" and keeps
/// RefreshToken in secure storage to call /auth/refresh-token when the access token expires.
/// </summary>
public sealed class AuthResponse
{
    public string TokenType { get; init; } = "Bearer";

    public string AccessToken { get; init; } = string.Empty;

    public DateTime AccessTokenExpiresAtUtc { get; init; }

    public string RefreshToken { get; init; } = string.Empty;

    public DateTime RefreshTokenExpiresAtUtc { get; init; }

    public CurrentUserDto User { get; init; } = new();
}
