using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ShippingERP.Application.Common.Security;
using ShippingERP.Application.DTOs.Auth;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.Identity;

namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// Creates signed JWT access tokens (HMAC-SHA256) and random refresh tokens.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private const int RefreshTokenByteLength = 64;

    private readonly JwtOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly JsonWebTokenHandler _tokenHandler = new();
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider dateTimeProvider)
    {
        _options = options.Value;
        _dateTimeProvider = dateTimeProvider;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenResult CreateAccessToken(TokenUserInfo user)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var expiresAtUtc = utcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [AppClaimTypes.UserId] = user.UserId.ToString(CultureInfo.InvariantCulture),
            [AppClaimTypes.TokenId] = Guid.NewGuid().ToString("N"),
            [AppClaimTypes.UserName] = user.UserName,
            [AppClaimTypes.FullName] = user.FullName,
            [AppClaimTypes.SecurityStamp] = user.SecurityStamp,

            // Arrays are written as JSON arrays: "role": ["Accountant"], "permission": ["COD.View", ...]
            [AppClaimTypes.Role] = user.Roles.ToArray(),
            [AppClaimTypes.Permission] = user.Permissions.ToArray()
        };

        if (user.BranchId.HasValue)
        {
            claims[AppClaimTypes.BranchId] = user.BranchId.Value.ToString(CultureInfo.InvariantCulture);
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = utcNow,
            NotBefore = utcNow,
            Expires = expiresAtUtc,
            Claims = claims,
            SigningCredentials = _signingCredentials
        };

        var token = _tokenHandler.CreateToken(descriptor);

        return new AccessTokenResult(token, expiresAtUtc);
    }

    public RefreshTokenResult CreateRefreshToken()
    {
        // 64 random bytes from a cryptographic generator, URL-safe Base64 (86 characters).
        var rawToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenByteLength));
        var expiresAtUtc = _dateTimeProvider.UtcNow.AddDays(_options.RefreshTokenDays);

        return new RefreshTokenResult(rawToken, HashRefreshToken(rawToken), expiresAtUtc);
    }

    public string HashRefreshToken(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes);
    }
}
