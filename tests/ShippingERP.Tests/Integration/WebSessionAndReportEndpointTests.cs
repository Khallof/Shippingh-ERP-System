using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShippingERP.API.Controllers.V1;
using ShippingERP.Application.DTOs.Auth;
using ShippingERP.Domain.Authorization;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Integration;

/// <summary>
/// Step 8 endpoints through the real HTTP pipeline, without SQL Server: the browser session (httpOnly cookie) guards,
/// and the authorization / validation of the dashboard, operational reports, exports and the audit log.
/// </summary>
public sealed class WebSessionAndReportEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public WebSessionAndReportEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private string Token(long userId, params string[] permissions)
    {
        var tokenService = new JwtTokenService(_factory.Services.GetRequiredService<IOptions<JwtOptions>>(), new TestClock());
        return tokenService.CreateAccessToken(new TokenUserInfo(
            userId, $"user{userId}", "Test User", 1, FakeUserSessionCache.DefaultSecurityStamp, new[] { "CustomRole" }, permissions)).Token;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token = null, bool cookieMode = false, string? cookie = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (cookieMode)
        {
            request.Headers.Add(AuthController.AuthModeHeader, AuthController.CookieMode);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{AuthController.RefreshCookieName}={cookie}");
        }

        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");

    // ------------------------------------------------------------------ browser session

    [Theory]
    [InlineData("/api/v1/auth/web/refresh")]
    [InlineData("/api/v1/auth/web/logout")]
    public async Task WebSessionEndpoints_RequireTheAuthModeHeader(string url)
    {
        var response = await SendAsync(HttpMethod.Post, url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorAsync(response)).GetProperty("details").TryGetProperty(AuthController.AuthModeHeader, out _));
    }

    [Fact]
    public async Task WebRefresh_WithoutTheCookie_Returns401()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/auth/web/refresh", cookieMode: true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_REFRESH_TOKEN", (await ErrorAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task WebLogout_WithoutACookie_Returns200_AndExpiresTheCookie()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/auth/web/logout", cookieMode: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith(AuthController.RefreshCookieName + "=", setCookie);
        Assert.Contains("path=/api/v1/auth/web", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ reports

    [Theory]
    [InlineData("/api/v1/dashboard")]
    [InlineData("/api/v1/reports/operations")]
    [InlineData("/api/v1/audit-logs")]
    [InlineData("/api/v1/shipments/export")]
    [InlineData("/api/v1/accounting/trial-balance/export")]
    [InlineData("/api/v1/driver-manifests/1/pdf")]
    public async Task ReportEndpoints_WithoutToken_Return401(string url)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, url)).StatusCode);
    }

    [Fact]
    public async Task OperationalReport_NeedsReportsViewOperational_AndAuditLog_NeedsAuditLogsView()
    {
        var token = Token(800, Permissions.Shipments.View);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/api/v1/reports/operations", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/api/v1/reports/operations/export", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/api/v1/audit-logs", token)).StatusCode);
    }

    [Fact]
    public async Task FinancialExports_NeedTheFinancialPermissions()
    {
        var token = Token(801, Permissions.Accounting.View);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/api/v1/accounting/trial-balance/export?format=pdf", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/api/v1/accounting/balance-sheet/export?format=excel", token)).StatusCode);
    }

    [Fact]
    public async Task Dashboard_WithTooManyDays_Returns400()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/dashboard?days=1000", Token(802, Permissions.Shipments.View));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorAsync(response)).GetProperty("details").TryGetProperty("days", out _));
    }

    [Fact]
    public async Task OperationalReport_WithAnInvertedPeriod_Returns400()
    {
        var token = Token(803, Permissions.Shipments.View, Permissions.Reports.ViewOperational);

        var response = await SendAsync(HttpMethod.Get, "/api/v1/reports/operations?fromDate=2026-10-10&toDate=2026-10-01", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorAsync(response)).GetProperty("details").TryGetProperty("toDate", out _));
    }

    [Fact]
    public async Task Export_WithAnUnknownFormat_Returns400()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/shipments/export?format=word", Token(804, Permissions.Shipments.View));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
