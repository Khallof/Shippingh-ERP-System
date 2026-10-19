using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.Interfaces.Reports;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1;

/// <summary>
/// سجل التدقيق (step 8): read-only list of every recorded action (sign-ins, changes, approvals, postings...).
/// Filters: action, userId, entityName, entityId, succeeded, fromUtc / toUtc; search: user name, entity.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit-logs")]
[HasPermission(Permissions.AuditLogs.View)]
public sealed class AuditLogsController : ApiControllerBase
{
    private readonly IAuditLogQueryService _auditLogs;

    public AuditLogsController(IAuditLogQueryService auditLogs)
    {
        _auditLogs = auditLogs;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> GetAll([FromQuery] AuditLogListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _auditLogs.GetPagedAsync(query, cancellationToken));
    }
}
