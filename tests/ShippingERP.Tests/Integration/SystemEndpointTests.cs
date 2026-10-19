using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Integration;

/// <summary>
/// Integration tests: real HTTP requests against the in-memory API.
/// </summary>
public sealed class SystemEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SystemEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSystemHealth_ReturnsOk_WithStandardEnvelope_AndUtcTime()
    {
        var before = DateTime.UtcNow.AddMinutes(-1);

        var response = await _client.GetAsync("/api/v1/system/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Trace-Id"));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));

        var data = json.GetProperty("data");
        Assert.Equal("Healthy", data.GetProperty("status").GetString());
        Assert.Equal("Healthy", data.GetProperty("databaseStatus").GetString());
        Assert.Equal("Testing", data.GetProperty("environment").GetString());

        var serverTimeText = data.GetProperty("serverTimeUtc").GetString();
        Assert.NotNull(serverTimeText);
        Assert.EndsWith("Z", serverTimeText); // UTC marker

        var serverTime = data.GetProperty("serverTimeUtc").GetDateTime();
        Assert.True(serverTime >= before && serverTime <= DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task GetSystemHealth_ReturnsArabicMessage_NotEscaped()
    {
        var response = await _client.GetAsync("/api/v1/system/health");
        var rawJson = await response.Content.ReadAsStringAsync();

        Assert.Contains("النظام", rawJson);
        Assert.DoesNotContain("\\u0627", rawJson);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsStandardNotFoundError()
    {
        var response = await _client.GetAsync("/api/v1/this-route-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal("NOT_FOUND", json.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task LivenessHealthCheck_ReturnsOk()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadinessHealthCheck_ReturnsOk_WhenDatabaseIsHealthy()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
