using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ShippingERP.Application.Common.Models;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace ShippingERP.API.Extensions;

public static class HttpContextExtensions
{
    /// <summary>
    /// Returns the W3C trace id of the current request (same value in logs, X-Trace-Id header and API responses).
    /// Falls back to ASP.NET Core's TraceIdentifier if no Activity exists.
    /// </summary>
    public static string GetTraceId(this HttpContext context)
    {
        return Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }

    /// <summary>
    /// Writes an ApiResponse as JSON using the same serializer settings as the controllers
    /// (camelCase, Arabic characters not escaped). Used by middleware and status-code pages.
    /// </summary>
    public static async Task WriteApiResponseAsync<T>(this HttpContext context, int statusCode, ApiResponse<T> response)
    {
        var jsonOptions = context.RequestServices
            .GetRequiredService<IOptions<MvcJsonOptions>>()
            .Value
            .JsonSerializerOptions;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await JsonSerializer.SerializeAsync(context.Response.Body, response, jsonOptions, context.RequestAborted);
    }
}
