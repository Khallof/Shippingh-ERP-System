using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ShippingERP.Application.Common.Security;
using ShippingERP.Application.DTOs.Auth;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Identity;

/// <summary>Access-token claims, signature, lifetime, and refresh-token generation.</summary>
public sealed class JwtTokenServiceTests
{
    private readonly TestClock _clock = new();
    private readonly JwtOptions _options = new()
    {
        Issuer = "ShippingERP",
        Audience = "ShippingERP.Clients",
        SecretKey = TestSettings.JwtSecretKey,
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    };

    private JwtTokenService CreateService() => new(Options.Create(_options), _clock);

    private static TokenUserInfo SampleUser() => new(
        UserId: 42,
        UserName: "agent1",
        FullName: "Ahmed Ali",
        BranchId: 3,
        SecurityStamp: "stamp-1",
        Roles: new[] { "ShippingAgent" },
        Permissions: new[] { "Shipments.Create", "Shipments.View" });

    [Fact]
    public async Task CreateAccessToken_ContainsUserIdFullNameRoleBranchAndPermissions()
    {
        var token = CreateService().CreateAccessToken(SampleUser());

        var result = await ValidateAsync(token.Token);

        Assert.True(result.IsValid, result.Exception?.Message);
        var claims = result.ClaimsIdentity.Claims.ToList();

        Assert.Equal("42", claims.Single(c => c.Type == AppClaimTypes.UserId).Value);
        Assert.Equal("agent1", claims.Single(c => c.Type == AppClaimTypes.UserName).Value);
        Assert.Equal("Ahmed Ali", claims.Single(c => c.Type == AppClaimTypes.FullName).Value);
        Assert.Equal("3", claims.Single(c => c.Type == AppClaimTypes.BranchId).Value);
        Assert.Equal("stamp-1", claims.Single(c => c.Type == AppClaimTypes.SecurityStamp).Value);
        Assert.Equal(new[] { "ShippingAgent" }, claims.Where(c => c.Type == AppClaimTypes.Role).Select(c => c.Value));
        Assert.Equal(
            new[] { "Shipments.Create", "Shipments.View" },
            claims.Where(c => c.Type == AppClaimTypes.Permission).Select(c => c.Value).OrderBy(v => v));
    }

    [Fact]
    public async Task CreateAccessToken_ForUserWithoutBranch_HasNoBranchClaim()
    {
        var user = SampleUser() with { BranchId = null };

        var result = await ValidateAsync(CreateService().CreateAccessToken(user).Token);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.ClaimsIdentity.Claims, c => c.Type == AppClaimTypes.BranchId);
    }

    [Fact]
    public void CreateAccessToken_ExpiresAfterConfiguredMinutes_InUtc()
    {
        var token = CreateService().CreateAccessToken(SampleUser());

        Assert.Equal(_clock.UtcNow.AddMinutes(15), token.ExpiresAtUtc);
        Assert.Equal(DateTimeKind.Utc, token.ExpiresAtUtc.Kind);

        var jwt = new JsonWebToken(token.Token);
        Assert.Equal("HS256", jwt.Alg);
        Assert.True(Math.Abs((jwt.ValidTo - token.ExpiresAtUtc).TotalSeconds) < 1);
    }

    [Fact]
    public async Task AccessToken_SignedWithAnotherKey_IsRejected()
    {
        var token = CreateService().CreateAccessToken(SampleUser());

        var result = await ValidateAsync(token.Token, "a-completely-different-signing-key-0123456789");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateRefreshToken_IsRandom_AndOnlyItsSha256HashIsStored()
    {
        var service = CreateService();

        var first = service.CreateRefreshToken();
        var second = service.CreateRefreshToken();

        Assert.NotEqual(first.RawToken, second.RawToken);
        Assert.True(first.RawToken.Length >= 80);
        Assert.Equal(64, first.TokenHash.Length);
        Assert.NotEqual(first.RawToken, first.TokenHash);
        Assert.Equal(first.TokenHash, service.HashRefreshToken(first.RawToken));
        Assert.Equal(_clock.UtcNow.AddDays(7), first.ExpiresAtUtc);
    }

    private Task<TokenValidationResult> ValidateAsync(string token, string? key = null)
    {
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? _options.SecretKey)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };

        return new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
    }
}
