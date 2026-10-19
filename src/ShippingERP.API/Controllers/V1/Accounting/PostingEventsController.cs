using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Accounting;

/// <summary>
/// الترحيل الآلي: what the accounting module did with each operational event (posted, no effect, duplicate, failed).
/// The background dispatcher posts the waiting events every few seconds; "process" does it now and "retry" posts a
/// failed event again (after fixing a posting rule or reopening a period, or on another open date).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounting/posting-events")]
[HasPermission(Permissions.Accounting.View)]
public sealed class PostingEventsController : ApiControllerBase
{
    private readonly IAccountingPostingService _postings;

    public PostingEventsController(IAccountingPostingService postings)
    {
        _postings = postings;
    }

    /// <summary>Filters: status (Posted, Skipped, Duplicate, Failed), eventType, sourceType, fromUtc / toUtc. Search: source number, event type.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PostingEventDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<PostingEventDto>>>> GetAll([FromQuery] PostingEventListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _postings.GetEventsAsync(query, cancellationToken));
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<PostingSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PostingSummaryDto>>> Summary(CancellationToken cancellationToken)
    {
        return OkResponse(await _postings.GetSummaryAsync(cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PostingEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PostingEventDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _postings.GetEventAsync(id, cancellationToken));
    }

    /// <summary>Posts up to maxCount waiting events now (idempotent: an event is never posted twice).</summary>
    [HttpPost("process")]
    [HasPermission(Permissions.Accounting.ManagePostings)]
    [ProducesResponseType(typeof(ApiResponse<ProcessPostingsResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProcessPostingsResultDto>>> Process([FromBody] ProcessPostingsRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _postings.ProcessPendingAsync(request.MaxCount, cancellationToken), "تمت معالجة الأحداث");
    }

    /// <summary>Posts a failed event again, optionally with another accounting date in an open period.</summary>
    [HttpPost("{id:long}/retry")]
    [HasPermission(Permissions.Accounting.ManagePostings)]
    [ProducesResponseType(typeof(ApiResponse<PostingEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<PostingEventDto>>> Retry(long id, [FromBody] RetryPostingEventRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _postings.RetryAsync(id, request, cancellationToken));
    }
}
