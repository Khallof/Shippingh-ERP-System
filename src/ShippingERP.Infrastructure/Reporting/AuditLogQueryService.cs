using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.Interfaces.Reports;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Reporting;

/// <summary>
/// Read-only list of the audit log (step 8). The log is append-only; this service never changes it.
/// Newest first by default. ActionName is the enum name (the web app translates it).
/// </summary>
public sealed class AuditLogQueryService : IAuditLogQueryService
{
    private readonly AppDbContext _dbContext;

    public AuditLogQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<AuditLogDto>> GetPagedAsync(AuditLogListQuery query, CancellationToken cancellationToken)
    {
        var rows = _dbContext.AuditLogs.AsNoTracking();

        if (query.Action.HasValue)
        {
            var action = query.Action.Value;
            rows = rows.Where(x => x.Action == action);
        }

        if (query.UserId.HasValue)
        {
            var userId = query.UserId.Value;
            rows = rows.Where(x => x.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            var entity = query.EntityName.Trim();
            rows = rows.Where(x => x.EntityName == entity);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            var entityId = query.EntityId.Trim();
            rows = rows.Where(x => x.EntityId == entityId);
        }

        if (query.Succeeded.HasValue)
        {
            var succeeded = query.Succeeded.Value;
            rows = rows.Where(x => x.Succeeded == succeeded);
        }

        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value;
            rows = rows.Where(x => x.CreatedAtUtc >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = query.ToUtc.Value;
            rows = rows.Where(x => x.CreatedAtUtc < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => (x.UserName != null && x.UserName.Contains(search)) ||
                                   (x.EntityName != null && x.EntityName.Contains(search)) ||
                                   (x.EntityId != null && x.EntityId == search));
        }

        var total = await rows.CountAsync(cancellationToken);
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var ordered = query.SortBy?.ToLowerInvariant() switch
        {
            "action" => descending ? rows.OrderByDescending(x => x.Action) : rows.OrderBy(x => x.Action),
            "username" => descending ? rows.OrderByDescending(x => x.UserName) : rows.OrderBy(x => x.UserName),
            _ => descending ? rows.OrderByDescending(x => x.CreatedAtUtc) : rows.OrderBy(x => x.CreatedAtUtc)
        };

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                Action = x.Action,
                Succeeded = x.Succeeded,
                UserId = x.UserId,
                UserName = x.UserName,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                Details = x.Details
            })
            .ToListAsync(cancellationToken);

        var named = items.Select(x => new AuditLogDto
        {
            Id = x.Id,
            CreatedAtUtc = x.CreatedAtUtc,
            Action = x.Action,
            ActionName = x.Action.ToString(),
            Succeeded = x.Succeeded,
            UserId = x.UserId,
            UserName = x.UserName,
            EntityName = x.EntityName,
            EntityId = x.EntityId,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            Details = x.Details
        }).ToList();

        return new PagedResult<AuditLogDto>(named, query.PageNumber, query.PageSize, total);
    }
}
