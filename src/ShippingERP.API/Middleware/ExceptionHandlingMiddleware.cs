using Microsoft.EntityFrameworkCore;
using ShippingERP.API.Extensions;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;

namespace ShippingERP.API.Middleware;

/// <summary>
/// Global exception handler. Catches every exception thrown by controllers/services and converts it
/// into the standard ApiResponse error format with the correct HTTP status code and trace id.
///
/// Rules:
/// - Expected business exceptions (AppException subclasses) -> 4xx, logged as Warning.
/// - Anything else -> 500, logged as Error. Internal details are shown ONLY in Development.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    /// <summary>Non-standard status used by Nginx and others for "client closed request".</summary>
    private const int ClientClosedRequestStatusCode = 499;

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client closed the connection; nobody is waiting for a response.
            _logger.LogInformation(
                "Request {Method} {Path} was cancelled by the client",
                context.Request.Method,
                context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = ClientClosedRequestStatusCode;
            }
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.GetTraceId();
        var (statusCode, errorCode, message, details) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                traceId);
        }
        else
        {
            _logger.LogWarning(
                "Handled {ExceptionType} for {Method} {Path}: {Message}. TraceId: {TraceId}",
                exception.GetType().Name,
                context.Request.Method,
                context.Request.Path,
                exception.Message,
                traceId);
        }

        if (context.Response.HasStarted)
        {
            // Headers were already sent; we cannot change the status code or body anymore.
            _logger.LogWarning("The response has already started; the error response could not be written");
            return;
        }

        context.Response.Clear();

        var response = ApiResponse<object>.Fail(errorCode, message, details, traceId);
        await context.WriteApiResponseAsync(statusCode, response);
    }

    private (int StatusCode, string ErrorCode, string Message, IReadOnlyDictionary<string, string[]>? Details)
        MapException(Exception exception)
    {
        return exception switch
        {
            RequestValidationException validation => (
                StatusCodes.Status400BadRequest,
                validation.ErrorCode,
                validation.Message,
                validation.Errors),

            UnauthorizedException unauthorized => (
                StatusCodes.Status401Unauthorized,
                unauthorized.ErrorCode,
                unauthorized.Message,
                null),

            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                notFound.ErrorCode,
                notFound.Message,
                null),

            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                conflict.ErrorCode,
                conflict.Message,
                null),

            ForbiddenException forbidden => (
                StatusCodes.Status403Forbidden,
                forbidden.ErrorCode,
                forbidden.Message,
                null),

            BusinessRuleException businessRule => (
                StatusCodes.Status422UnprocessableEntity,
                businessRule.ErrorCode,
                businessRule.Message,
                null),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                ErrorCodes.ConcurrencyConflict,
                "تم تعديل هذا السجل من قبل مستخدم آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى.",
                null),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                ErrorCodes.Unauthorized,
                "يجب تسجيل الدخول للوصول إلى هذا المورد.",
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError,
                "حدث خطأ غير متوقع في الخادم. يرجى المحاولة لاحقاً أو التواصل مع الدعم الفني مع ذكر رقم التتبع.",
                BuildDevelopmentDetails(exception))
        };
    }

    /// <summary>Exception details are returned to the client in Development only, never in Production.</summary>
    private IReadOnlyDictionary<string, string[]>? BuildDevelopmentDetails(Exception exception)
    {
        if (!_environment.IsDevelopment())
        {
            return null;
        }

        return new Dictionary<string, string[]>
        {
            ["exception"] = new[] { $"{exception.GetType().FullName}: {exception.Message}" }
        };
    }
}
