using ShippingERP.Application.DTOs.Auth;

namespace ShippingERP.Application.Interfaces.Identity;

/// <summary>Creates JWT access tokens and random refresh tokens.</summary>
public interface ITokenService
{
    AccessTokenResult CreateAccessToken(TokenUserInfo user);

    RefreshTokenResult CreateRefreshToken();

    /// <summary>SHA-256 hash (hex) used to store and look up refresh tokens.</summary>
    string HashRefreshToken(string rawToken);
}
