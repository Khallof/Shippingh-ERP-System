using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShippingERP.Application.DTOs.Auth;
using ShippingERP.Domain.Authorization;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Integration;

/// <summary>
/// Real HTTP requests through the whole pipeline (JWT validation, user-state check, [HasPermission],
/// standard JSON errors, Swagger). No database is needed: the endpoints used here do not query it.
/// </summary>
public sealed class AuthorizationEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PermissionCatalogUrl = "/api/v1/roles/permissions";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthorizationEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private string CreateAccessToken(
        long userId,
        string[] roles,
        string[] permissions,
        string securityStamp = FakeUserSessionCache.DefaultSecurityStamp,
        DateTime? issuedAtUtc = null)
    {
        var jwtOptions = _factory.Services.GetRequiredService<IOptions<JwtOptions>>();
        var clock = new TestClock();
        if (issuedAtUtc.HasValue)
        {
            clock.UtcNow = issuedAtUtc.Value;
        }

        var tokenService = new JwtTokenService(jwtOptions, clock);
        return tokenService.CreateAccessToken(new TokenUserInfo(
            userId, $"user{userId}", "Test User", null, securityStamp, roles, permissions)).Token;
    }

    private async Task<HttpResponseMessage> GetWithTokenAsync(string url, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("success").GetBoolean());
        return json.GetProperty("error").GetProperty("code").GetString();
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401_Unauthorized()
    {
        var response = await GetWithTokenAsync(PermissionCatalogUrl, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHORIZED", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task ProtectedEndpoint_WithGarbageToken_Returns401_Unauthorized()
    {
        var response = await GetWithTokenAsync(PermissionCatalogUrl, "not-a-jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHORIZED", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task TokenWithoutRequiredPermission_Returns403_Forbidden()
    {
        var token = CreateAccessToken(101, new[] { Roles.BranchManager }, new[] { Permissions.Users.View });

        var response = await GetWithTokenAsync(PermissionCatalogUrl, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("FORBIDDEN", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task TokenWithRequiredPermission_Returns200()
    {
        var token = CreateAccessToken(102, new[] { "CustomRole" }, new[] { Permissions.Roles.Manage });

        var response = await GetWithTokenAsync(PermissionCatalogUrl, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Permissions.All.Count, json.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task SystemAdminToken_PassesEveryPermissionCheck()
    {
        var token = CreateAccessToken(103, new[] { Roles.SystemAdmin }, Array.Empty<string>());

        var response = await GetWithTokenAsync(PermissionCatalogUrl, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401_TokenExpired_WithHeader()
    {
        var token = CreateAccessToken(
            104,
            new[] { Roles.SystemAdmin },
            Array.Empty<string>(),
            issuedAtUtc: DateTime.UtcNow.AddHours(-1));

        var response = await GetWithTokenAsync(PermissionCatalogUrl, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("TOKEN_EXPIRED", await ReadErrorCodeAsync(response));
        Assert.True(response.Headers.Contains("X-Token-Expired"));
    }

    [Fact]
    public async Task TokenIssuedBeforePermissionChange_Returns401_TokenExpired()
    {
        // The user's stamp changed after the token was issued (role, permission, branch or password change).
        _factory.SessionCache.Set(105, isActive: true, securityStamp: "stamp-after-change");
        var token = CreateAccessToken(105, new[] { Roles.SystemAdmin }, Array.Empty<string>(), securityStamp: "stamp-before-change");

        var response = await GetWithTokenAsync(PermissionCatalogUrl, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("TOKEN_EXPIRED", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task TokenOfDeactivatedUser_Returns401_Unauthorized()
    {
        _factory.SessionCache.Set(106, isActive: false, securityStamp: FakeUserSessionCache.DefaultSecurityStamp);
        var token = CreateAccessToken(106, new[] { Roles.SystemAdmin }, Array.Empty<string>());

        var response = await GetWithTokenAsync(PermissionCatalogUrl, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHORIZED", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Login_WithEmptyFields_Returns400_WithFieldErrors()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { UserNameOrEmail = "", Password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = json.GetProperty("error");
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());

        var details = error.GetProperty("details");
        Assert.True(details.TryGetProperty("userNameOrEmail", out _));
        Assert.True(details.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/change-password",
            new ChangePasswordRequest { CurrentPassword = "x", NewPassword = "y", ConfirmNewPassword = "y" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_DefinesBearerSecurityScheme()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var bearer = json.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.True(json.GetProperty("paths").TryGetProperty("/api/v1/auth/login", out _));
        Assert.True(json.GetProperty("paths").TryGetProperty("/api/v1/users/{id}", out _));
    }
}
