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
/// Authorization and validation of the accounting endpoints (step 7) through the real HTTP pipeline. Every request
/// here is rejected before any database access, so no SQL Server is needed.
/// </summary>
public sealed class AccountingEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AccountingEndpointTests(CustomWebApplicationFactory factory)
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

    private static object Line(long accountId, decimal debit, decimal credit) => new { accountId, debit, credit };

    [Theory]
    [InlineData("/api/v1/accounting/accounts")]
    [InlineData("/api/v1/accounting/accounts/tree")]
    [InlineData("/api/v1/accounting/journal-entries")]
    [InlineData("/api/v1/accounting/fiscal-years")]
    [InlineData("/api/v1/accounting/periods")]
    [InlineData("/api/v1/accounting/posting-rules")]
    [InlineData("/api/v1/accounting/posting-events")]
    [InlineData("/api/v1/accounting/bank-reconciliation")]
    [InlineData("/api/v1/accounting/general-ledger?accountId=1")]
    [InlineData("/api/v1/accounting/trial-balance")]
    [InlineData("/api/v1/accounting/profit-loss")]
    [InlineData("/api/v1/accounting/balance-sheet")]
    [InlineData("/api/v1/accounting/cash-flow")]
    public async Task Endpoints_WithoutToken_Return401(string url)
    {
        var response = await SendAsync(HttpMethod.Get, url, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/accounting/journal-entries")]
    [InlineData("PUT", "/api/v1/accounting/journal-entries/1")]
    [InlineData("POST", "/api/v1/accounting/journal-entries/1/submit")]
    [InlineData("POST", "/api/v1/accounting/journal-entries/1/approve")]
    [InlineData("POST", "/api/v1/accounting/journal-entries/1/post")]
    [InlineData("POST", "/api/v1/accounting/journal-entries/1/reverse")]
    [InlineData("POST", "/api/v1/accounting/periods/1/close")]
    [InlineData("POST", "/api/v1/accounting/periods/1/reopen")]
    [InlineData("POST", "/api/v1/accounting/fiscal-years")]
    [InlineData("POST", "/api/v1/accounting/accounts")]
    [InlineData("DELETE", "/api/v1/accounting/accounts/1")]
    [InlineData("POST", "/api/v1/accounting/posting-rules")]
    [InlineData("POST", "/api/v1/accounting/posting-events/process")]
    [InlineData("POST", "/api/v1/accounting/bank-reconciliation")]
    public async Task ChangingEndpoints_WithViewPermissionOnly_Return403(string method, string url)
    {
        var token = Token(700, Permissions.Accounting.View, Permissions.Reports.ViewFinancial);

        var response = await SendAsync(new HttpMethod(method), url, token, new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_AndPost_NeedTheirOwnPermissions()
    {
        var author = Token(701, Permissions.Accounting.View, Permissions.Accounting.CreateJournal);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, "/api/v1/accounting/journal-entries/1/approve", author, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, "/api/v1/accounting/journal-entries/1/post", author, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, "/api/v1/accounting/journal-entries/1/reverse", author, new { reason = "x" })).StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/accounting/trial-balance")]
    [InlineData("/api/v1/accounting/profit-loss")]
    [InlineData("/api/v1/accounting/balance-sheet")]
    [InlineData("/api/v1/accounting/cash-flow")]
    [InlineData("/api/v1/accounting/reports/control-accounts")]
    public async Task FinancialStatements_NeedReportsViewFinancial(string url)
    {
        var token = Token(702, Permissions.Accounting.View);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, url, token)).StatusCode);
    }

    [Fact]
    public async Task UnbalancedEntry_Returns400_WithTheLinesError()
    {
        var token = Token(703, Permissions.Accounting.View, Permissions.Accounting.CreateJournal);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/accounting/journal-entries", token, new
        {
            entryDate = "2026-10-15",
            journalType = "General",
            description = "قيد غير متوازن",
            lines = new[] { Line(10, 1000m, 0m), Line(11, 0m, 900m) }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await DetailsAsync(response)).TryGetProperty("lines", out _));
    }

    [Fact]
    public async Task EntryWithOneLine_ALineOnBothSides_AndAnAutomaticJournalType_Returns400()
    {
        var token = Token(704, Permissions.Accounting.View, Permissions.Accounting.CreateJournal);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/accounting/journal-entries", token, new
        {
            entryDate = "2026-10-15",
            journalType = "Sales",
            description = "",
            lines = new[] { Line(10, 500m, 500m) }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = await DetailsAsync(response);
        Assert.True(details.TryGetProperty("journalType", out _));
        Assert.True(details.TryGetProperty("description", out _));
        Assert.True(details.TryGetProperty("lines", out _));
    }

    [Fact]
    public async Task Reverse_WithoutReason_Returns400()
    {
        var token = Token(705, Permissions.Accounting.View, Permissions.Accounting.ReverseJournal);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/accounting/journal-entries/1/reverse", token, new { reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await DetailsAsync(response)).TryGetProperty("reason", out _));
    }

    [Fact]
    public async Task FiscalYear_EndingBeforeItStarts_Returns400()
    {
        var token = Token(706, Permissions.Accounting.View, Permissions.Accounting.ManagePeriods);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/accounting/fiscal-years", token, new { startDate = "2027-01-01", endDate = "2026-12-31" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await DetailsAsync(response)).TryGetProperty("endDate", out _));
    }

    [Fact]
    public async Task BankReconciliation_NeedsItsPermission_EvenToRead()
    {
        var token = Token(707, Permissions.Accounting.View, Permissions.Reports.ViewFinancial);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/api/v1/accounting/bank-transactions", token)).StatusCode);
    }
}
