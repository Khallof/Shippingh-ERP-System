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
/// Authorization and validation of the pricing, invoice, payment and report endpoints (step 5) through the real
/// HTTP pipeline. Every request here is rejected before any database access, so no SQL Server is needed.
/// The controllers require their "View" permission on every action PLUS the action permission.
/// </summary>
public sealed class BillingEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public BillingEndpointTests(CustomWebApplicationFactory factory)
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

    private static async Task<JsonElement> DetailsAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("details");

    [Theory]
    [InlineData("/api/v1/invoices")]
    [InlineData("/api/v1/invoices/1/pdf")]
    [InlineData("/api/v1/customer-payments")]
    [InlineData("/api/v1/customers/1/statement")]
    [InlineData("/api/v1/reports/customer-aging")]
    [InlineData("/api/v1/price-overrides")]
    public async Task BillingEndpoints_WithoutToken_Return401(string url)
    {
        var response = await SendAsync(HttpMethod.Get, url, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/invoices/from-shipment/1")]
    [InlineData("POST", "/api/v1/invoices/consolidated")]
    [InlineData("PUT", "/api/v1/invoices/1")]
    [InlineData("POST", "/api/v1/invoices/1/issue")]
    [InlineData("POST", "/api/v1/invoices/1/cancel")]
    [InlineData("POST", "/api/v1/invoices/1/credit-notes")]
    [InlineData("GET", "/api/v1/invoices/export")]
    [InlineData("POST", "/api/v1/customer-payments")]
    [InlineData("POST", "/api/v1/customer-payments/1/allocations")]
    [InlineData("POST", "/api/v1/shipments/1/apply-price")]
    [InlineData("POST", "/api/v1/price-overrides/1/approve")]
    [InlineData("POST", "/api/v1/price-overrides/1/reject")]
    public async Task ChangingEndpoints_WithViewPermissionsOnly_Return403(string method, string url)
    {
        var token = Token(500, Permissions.Invoices.View, Permissions.Payments.View, Permissions.Shipments.View, Permissions.Pricing.View);

        var response = await SendAsync(new HttpMethod(method), url, token, method == "GET" ? null : new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reports_RequireTheirOwnPermissions()
    {
        var token = Token(501, Permissions.Invoices.View, Permissions.Payments.View);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/api/v1/reports/customer-aging", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/api/v1/customers/1/statement", token)).StatusCode);
    }

    [Fact]
    public async Task CreatePayment_WithZeroAmountAndAllocationsAboveTheAmount_Returns400WithFieldErrors()
    {
        var token = Token(502, Permissions.Payments.View, Permissions.Payments.Create);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/customer-payments", token, new
        {
            customerId = 0,
            paymentMethodId = 1,
            amount = 0,
            allocations = new[] { new { invoiceId = 1, amount = 500 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = await DetailsAsync(response);
        Assert.True(details.TryGetProperty("customerId", out _));
        Assert.True(details.TryGetProperty("amount", out _));
        Assert.True(details.TryGetProperty("allocations", out _));
    }

    [Fact]
    public async Task CreditNote_WithoutReasonOrLines_Returns400()
    {
        var token = Token(503, Permissions.Invoices.View, Permissions.Invoices.CreditNotes);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/invoices/1/credit-notes", token, new { reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = await DetailsAsync(response);
        Assert.True(details.TryGetProperty("reason", out _));
        Assert.True(details.TryGetProperty("lines", out _));
    }

    [Fact]
    public async Task CalculatePrice_ManualDiscountWithoutReason_Returns400()
    {
        var token = Token(504, Permissions.Shipments.View);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/shipments/1/calculate-price", token, new { manualDiscountAmount = 1000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await DetailsAsync(response)).TryGetProperty("reason", out _));
    }

    [Fact]
    public async Task InvoiceList_WithAnUnknownSortField_Returns400()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/invoices?sortBy=customerPassword", Token(505, Permissions.Invoices.View));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
