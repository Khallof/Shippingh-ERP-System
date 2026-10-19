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
/// Authorization and validation of the master-data endpoints through the real HTTP pipeline.
/// These requests are rejected before any database access, so no SQL Server is needed.
/// </summary>
public sealed class MasterDataEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MasterDataEndpointTests(CustomWebApplicationFactory factory)
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
    [InlineData("/api/v1/customers")]
    [InlineData("/api/v1/branches")]
    [InlineData("/api/v1/countries")]
    [InlineData("/api/v1/pricing-plans/1/rules")]
    [InlineData("/api/v1/company-profile")]
    public async Task MasterData_WithoutToken_Returns401(string url)
    {
        var response = await SendAsync(HttpMethod.Get, url, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHORIZED", (await ErrorAsync(response)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("/api/v1/customers")]
    [InlineData("/api/v1/suppliers")]
    [InlineData("/api/v1/employees")]
    [InlineData("/api/v1/drivers")]
    [InlineData("/api/v1/vehicles")]
    [InlineData("/api/v1/pricing-plans")]
    public async Task ProtectedLists_WithoutViewPermission_Return403(string url)
    {
        var response = await SendAsync(HttpMethod.Get, url, Token(200, Permissions.Shipments.View));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("FORBIDDEN", (await ErrorAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateCountry_WithoutMasterDataManage_Returns403()
    {
        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/countries",
            Token(201, Permissions.Customers.View),
            new { code = "SY", arabicName = "سوريا" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCountry_WithInvalidBody_Returns400WithFieldErrors()
    {
        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/countries",
            Token(202, Permissions.MasterData.Manage),
            new { code = "SYRIA", arabicName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await ErrorAsync(response);
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("details").TryGetProperty("code", out _));
        Assert.True(error.GetProperty("details").TryGetProperty("arabicName", out _));
    }

    [Fact]
    public async Task CustomerList_WithInvalidSortField_Returns400()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/customers?sortBy=password",
            Token(203, Permissions.Customers.View));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorAsync(response)).GetProperty("details").TryGetProperty("sortBy", out _));
    }

    [Fact]
    public async Task CreateCustomer_WithViewButWithoutManage_Returns403()
    {
        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/customers",
            Token(204, Permissions.Customers.View),
            new { arabicName = "عميل", phone = "+963944000999", defaultBranchId = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_ContainsTheMasterDataEndpoints()
    {
        var json = await _client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = json.GetProperty("paths");

        foreach (var path in new[]
                 {
                     "/api/v1/branches", "/api/v1/warehouses/{id}", "/api/v1/countries", "/api/v1/cities", "/api/v1/zones",
                     "/api/v1/service-types", "/api/v1/payment-methods", "/api/v1/currencies", "/api/v1/exchange-rates/effective",
                     "/api/v1/tax-rates", "/api/v1/expense-categories", "/api/v1/suppliers", "/api/v1/vehicles/{id}/deactivate",
                     "/api/v1/customers", "/api/v1/customers/{customerId}/addresses", "/api/v1/pricing-plans",
                     "/api/v1/pricing-plans/{planId}/rules", "/api/v1/employees", "/api/v1/drivers", "/api/v1/company-profile"
                 })
        {
            Assert.True(paths.TryGetProperty(path, out _), $"Swagger is missing {path}");
        }
    }
}
