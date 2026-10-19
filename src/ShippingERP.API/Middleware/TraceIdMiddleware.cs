using Serilog.Context;
using ShippingERP.API.Extensions;

namespace ShippingERP.API.Middleware;

/// <summary>
/// Adds the X-Trace-Id header to every response and attaches the same TraceId to every log line
/// written during the request. When a user reports an error, ask for the traceId and search the logs for it.
/// </summary>
public sealed class TraceIdMiddleware
{
    public const string HeaderName = "X-Trace-Id";

    private readonly RequestDelegate _next;

    public TraceIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.GetTraceId();

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = traceId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("TraceId", traceId))
        {
            await _next(context);
        }
    }
}
