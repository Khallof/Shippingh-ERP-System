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
/// Authorization and validation of the shipment endpoints through the real HTTP pipeline.
/// The shipments controller requires Shipments.View on every action PLUS the action permission, so the tokens of
/// the validation tests carry both.
/// Every request here is rejected before any database access, so no SQL Server is needed.
/// </summary>
public sealed class ShipmentEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ShipmentEndpointTests(CustomWebApplicationFactory factory)
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

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");

    [Theory]
    [InlineData("/api/v1/shipments")]
    [InlineData("/api/v1/shipments/1/label")]
    [InlineData("/api/v1/branch-transfers")]
    [InlineData("/api/v1/driver-manifests")]
    [InlineData("/api/v1/shipments/import/template")]
    public async Task ShipmentEndpoints_WithoutToken_Return401(string url)
    {
        var response = await SendAsync(HttpMethod.Get, url, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/shipments")]
    [InlineData("POST", "/api/v1/shipments/1/cancel")]
    [InlineData("POST", "/api/v1/shipments/1/status-corrections")]
    [InlineData("PATCH", "/api/v1/shipments/1/driver")]
    [InlineData("POST", "/api/v1/branch-transfers")]
    [InlineData("POST", "/api/v1/warehouse-manifests")]
    [InlineData("POST", "/api/v1/driver-manifests")]
    [InlineData("POST", "/api/v1/shipment-returns")]
    [InlineData("POST", "/api/v1/damage-cases")]
    [InlineData("PUT", "/api/v1/shipping-settings")]
    public async Task ChangingEndpoints_WithViewPermissionOnly_Return403(string method, string url)
    {
        var response = await SendAsync(new HttpMethod(method), url, Token(300, Permissions.Shipments.View, Permissions.Transfers.View), new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("FORBIDDEN", (await ErrorAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateShipment_WithInvalidBody_Returns400WithFieldErrors()
    {
        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/shipments",
            Token(301, Permissions.Shipments.View, Permissions.Shipments.Create),
            new { senderCustomerId = 0, isCOD = true, codAmount = 0, packages = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = (await ErrorAsync(response)).GetProperty("details");
        Assert.True(details.TryGetProperty("senderCustomerId", out _));
        Assert.True(details.TryGetProperty("packages", out _));
        Assert.True(details.TryGetProperty("codAmount", out _));
        Assert.True(details.TryGetProperty("packageDescription", out _));
    }

    [Fact]
    public async Task ManualStatusChange_ToDelivered_Returns400()
    {
        var response = await SendAsync(
            new HttpMethod("PATCH"),
            "/api/v1/shipments/1/status",
            Token(302, Permissions.Shipments.View, Permissions.Shipments.ChangeStatus),
            new { status = "Delivered" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorAsync(response)).GetProperty("details").TryGetProperty("status", out _));
    }

    [Fact]
    public async Task Deliver_WithoutRecipientName_Returns400_FromTheMultipartValidator()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/shipments/1/deliver");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(303, Permissions.Shipments.View, Permissions.Shipments.ConfirmDelivery));
        request.Content = new MultipartFormDataContent { { new StringContent("note"), "notes" } };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorAsync(response)).GetProperty("details").TryGetProperty("recipientName", out _));
    }

    [Fact]
    public async Task ShipmentList_WithInvalidSortField_Returns400()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/shipments?sortBy=internalNotes", Token(304, Permissions.Shipments.View));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_ContainsTheShipmentEndpoints()
    {
        var json = await _client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = json.GetProperty("paths");

        foreach (var path in new[]
                 {
                     "/api/v1/shipments", "/api/v1/shipments/{id}", "/api/v1/shipments/quote", "/api/v1/shipments/track/{code}",
                     "/api/v1/shipments/{id}/history", "/api/v1/shipments/{id}/status", "/api/v1/shipments/{id}/cancel",
                     "/api/v1/shipments/{id}/driver", "/api/v1/shipments/{id}/status-corrections", "/api/v1/shipments/{id}/deliver",
                     "/api/v1/shipments/{id}/delivery-failed", "/api/v1/shipments/{id}/proof-of-delivery", "/api/v1/shipments/{id}/label",
                     "/api/v1/shipments/labels", "/api/v1/shipments/{shipmentId}/attachments",
                     "/api/v1/shipments/{shipmentId}/attachments/{attachmentId}/content", "/api/v1/shipments/import",
                     "/api/v1/shipments/import/preview", "/api/v1/shipments/import/template", "/api/v1/warehouse-manifests/{id}/confirm",
                     "/api/v1/branch-transfers/{id}/dispatch", "/api/v1/branch-transfers/{id}/receive", "/api/v1/driver-manifests/{id}/close",
                     "/api/v1/shipment-returns/{id}/complete", "/api/v1/damage-cases/{id}/resolve", "/api/v1/shipping-settings"
                 })
        {
            Assert.True(paths.TryGetProperty(path, out _), $"Swagger is missing {path}");
        }
    }
}
