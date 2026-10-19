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
/// Authorization and validation of the COD and cash endpoints (step 6) through the real HTTP pipeline. Every
/// request here is rejected before any database access, so no SQL Server is needed.
/// </summary>
public sealed class CodCashEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CodCashEndpointTests(CustomWebApplicationFactory factory)
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
    [InlineData("/api/v1/cod")]
    [InlineData("/api/v1/cod/driver-balances")]
    [InlineData("/api/v1/cod/sender-balances")]
    [InlineData("/api/v1/cod/aging")]
    [InlineData("/api/v1/cod/settlements")]
    [InlineData("/api/v1/cashboxes")]
    [InlineData("/api/v1/cashboxes/1/transactions")]
    [InlineData("/api/v1/cash-receipts")]
    [InlineData("/api/v1/reports/daily-cash-movement")]
    public async Task Endpoints_WithoutToken_Return401(string url)
    {
        var response = await SendAsync(HttpMethod.Get, url, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/cod/collect")]
    [InlineData("POST", "/api/v1/cod/driver-handover")]
    [InlineData("POST", "/api/v1/cod/mark-ready")]
    [InlineData("POST", "/api/v1/cod/1/resolve-dispute")]
    [InlineData("POST", "/api/v1/cod/settlements")]
    [InlineData("POST", "/api/v1/cod/settlements/1/approve")]
    [InlineData("POST", "/api/v1/cod/settlements/1/pay")]
    [InlineData("POST", "/api/v1/cod/adjustments/1/approve")]
    [InlineData("POST", "/api/v1/cashboxes")]
    [InlineData("POST", "/api/v1/cashboxes/1/daily-closings")]
    [InlineData("POST", "/api/v1/cash-receipts")]
    [InlineData("POST", "/api/v1/cash-payments")]
    [InlineData("POST", "/api/v1/cash-payments/1/cancel")]
    [InlineData("POST", "/api/v1/cash-transfers")]
    [InlineData("POST", "/api/v1/cash-transfers/1/approve")]
    public async Task ChangingEndpoints_WithViewPermissionsOnly_Return403(string method, string url)
    {
        var token = Token(600, Permissions.Cod.View, Permissions.Cashboxes.View);

        var response = await SendAsync(new HttpMethod(method), url, token, new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CodEndpoints_NeedCodView_EvenWithTheActionPermission()
    {
        var token = Token(601, Permissions.Cod.Collect, Permissions.Cod.Settle);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, "/api/v1/cod/collect", token, new { shipmentId = 1, amount = 1000 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/api/v1/reports/driver-collections", token)).StatusCode);
    }

    [Fact]
    public async Task DailyCashMovement_NeedsCashboxesView()
    {
        var token = Token(602, Permissions.Cod.View, Permissions.Reports.ViewFinancial);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/api/v1/reports/daily-cash-movement", token)).StatusCode);
    }

    [Fact]
    public async Task Collect_WithoutShipmentAndWithZeroAmount_Returns400()
    {
        var token = Token(603, Permissions.Cod.View, Permissions.Cod.Collect);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/cod/collect", token, new { amount = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = await DetailsAsync(response);
        Assert.True(details.TryGetProperty("shipmentId", out _));
        Assert.True(details.TryGetProperty("amount", out _));
    }

    [Fact]
    public async Task Handover_WithoutDriverAndCashbox_Returns400()
    {
        var token = Token(604, Permissions.Cod.View, Permissions.Cod.Handover);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/cod/driver-handover", token, new { amount = 5000, codTransactionIds = new[] { 1, 1 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = await DetailsAsync(response);
        Assert.True(details.TryGetProperty("driverId", out _));
        Assert.True(details.TryGetProperty("cashboxId", out _));
        Assert.True(details.TryGetProperty("codTransactionIds", out _));
    }

    [Fact]
    public async Task Adjustment_WithoutReason_AndMissingFields_Returns400()
    {
        var token = Token(605, Permissions.Cod.View, Permissions.Cod.Adjust);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/cod/adjustments", token, new { adjustmentType = "DriverShortageWriteOff", reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = await DetailsAsync(response);
        Assert.True(details.TryGetProperty("reason", out _));
        Assert.True(details.TryGetProperty("driverId", out _));
        Assert.True(details.TryGetProperty("amount", out _));
    }

    [Fact]
    public async Task ManualReceipt_OfASystemType_Returns400()
    {
        var token = Token(606, Permissions.Cashboxes.View, Permissions.CashVouchers.Create);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/cash-receipts", token, new
        {
            cashboxId = 1, receiptType = "DriverHandover", amount = 1000, receivedFrom = "x", description = "y"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await DetailsAsync(response)).TryGetProperty("receiptType", out _));
    }

    [Fact]
    public async Task Transfer_ToACashboxAndABankAtOnce_Returns400()
    {
        var token = Token(607, Permissions.Cashboxes.View, Permissions.CashTransfers.Create);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/cash-transfers", token, new
        {
            fromCashboxId = 1, toCashboxId = 2, toBankAccountId = 3, amount = 1000, reason = "تحويل تجريبي"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await DetailsAsync(response)).TryGetProperty("toCashboxId", out _));
    }

    [Fact]
    public async Task Settlement_WithDuplicateLines_Returns400()
    {
        var token = Token(608, Permissions.Cod.View, Permissions.Cod.Settle);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/cod/settlements", token, new
        {
            senderCustomerId = 3,
            lines = new[] { new { codTransactionId = 5 }, new { codTransactionId = 5 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await DetailsAsync(response)).TryGetProperty("lines", out _));
    }

    [Fact]
    public async Task CashboxTransactions_WithAnInvertedDateRange_Returns400()
    {
        var token = Token(609, Permissions.Cashboxes.View);

        var response = await SendAsync(HttpMethod.Get, "/api/v1/cashboxes/1/transactions?fromDate=2026-10-10&toDate=2026-10-01", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
