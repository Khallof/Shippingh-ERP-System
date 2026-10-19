using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Extensions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;

namespace ShippingERP.API.Controllers;

/// <summary>
/// Base class for all API controllers. Gives every controller:
/// - [Authorize]: every endpoint requires a valid access token unless it is marked [AllowAnonymous],
/// - [ApiController] behaviour (automatic 400 on invalid input),
/// - JSON as the only content type,
/// - helper methods that wrap data in the standard ApiResponse envelope with the trace id.
///
/// Every controller in the Controllers/V1 folder should inherit from this class and declare:
/// [ApiVersion("1.0")] and [Route("api/v{version:apiVersion}/your-resource")].
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Returns 200 OK with the standard success envelope.</summary>
    protected ActionResult<ApiResponse<T>> OkResponse<T>(T data, string? message = null)
    {
        return Ok(ApiResponse<T>.Ok(data, message, HttpContext.GetTraceId()));
    }

    /// <summary>Returns 200 OK with a message and no data.</summary>
    protected ActionResult<ApiResponse<object>> OkMessage(string message)
    {
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = message,
            TraceId = HttpContext.GetTraceId()
        });
    }

    /// <summary>Streams a file (label, attachment, Excel template). The stream is disposed after the response.</summary>
    protected FileStreamResult FileDownloadResult(FileDownload file)
    {
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Returns 201 Created with a Location header and the standard success envelope.</summary>
    protected ActionResult<ApiResponse<T>> CreatedResponse<T>(string actionName, object? routeValues, T data, string? message = null)
    {
        return CreatedAtAction(
            actionName,
            routeValues,
            ApiResponse<T>.Ok(data, message ?? "تم الإنشاء بنجاح", HttpContext.GetTraceId()));
    }
}
