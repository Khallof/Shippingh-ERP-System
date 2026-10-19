namespace ShippingERP.Application.Common.Models;

/// <summary>
/// The single response envelope used by EVERY endpoint (success and error).
///
/// Success example:
/// { "success": true, "message": "تمت العملية بنجاح", "data": { ... }, "traceId": "...", "timestampUtc": "..." }
///
/// Error example:
/// { "success": false, "message": "...", "error": { "code": "NOT_FOUND", "message": "...", "details": null },
///   "traceId": "...", "timestampUtc": "..." }
///
/// The traceId is also returned in the X-Trace-Id response header and written to the logs,
/// so support staff can find the exact log lines for any failed request.
/// </summary>
public sealed class ApiResponse<T>
{
    public const string DefaultSuccessMessage = "تمت العملية بنجاح";

    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public T? Data { get; init; }

    public ApiError? Error { get; init; }

    public string? TraceId { get; init; }

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? message = null, string? traceId = null) => new()
    {
        Success = true,
        Message = message ?? DefaultSuccessMessage,
        Data = data,
        TraceId = traceId
    };

    public static ApiResponse<T> Fail(
        string errorCode,
        string message,
        IReadOnlyDictionary<string, string[]>? details = null,
        string? traceId = null) => new()
    {
        Success = false,
        Message = message,
        Error = new ApiError
        {
            Code = errorCode,
            Message = message,
            Details = details
        },
        TraceId = traceId
    };
}
