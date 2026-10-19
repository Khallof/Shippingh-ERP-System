using ShippingERP.Application.DTOs.Auth;

namespace ShippingERP.Application.Interfaces.Identity;

/// <summary>Sign-in, token refresh, sign-out and password change.</summary>
public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Exchanges a refresh token for a new access token AND a new refresh token (rotation).</summary>
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    /// <summary>Revokes the refresh token (sign out from this device). Idempotent.</summary>
    Task RevokeTokenAsync(RevokeTokenRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the current user's password, signs out every other device and returns a fresh
    /// token pair so the current device stays signed in.
    /// </summary>
    Task<AuthResponse> ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken cancellationToken);

    Task<CurrentUserDto> GetCurrentUserAsync(long userId, CancellationToken cancellationToken);
}
