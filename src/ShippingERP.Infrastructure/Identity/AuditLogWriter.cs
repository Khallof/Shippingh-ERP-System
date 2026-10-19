using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Entities.Auditing;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// Adds AuditLog rows to the DbContext. It does NOT call SaveChanges: the caller saves, so the audit row
/// is committed in the same transaction as the change it describes (or not at all).
/// IP address and User-Agent are taken from the current request automatically.
/// </summary>
public sealed class AuditLogWriter
{
    private static readonly JsonSerializerOptions DetailsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public AuditLogWriter(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    /// <param name="action">What happened.</param>
    /// <param name="succeeded">False for refused attempts (failed sign-in, reused token).</param>
    /// <param name="userId">The user the event is about.</param>
    /// <param name="userName">User name (as typed for failed sign-ins).</param>
    /// <param name="entityName">Affected table, e.g. "User".</param>
    /// <param name="entityId">Affected record id.</param>
    /// <param name="details">Any object; serialized to JSON. Never pass passwords or tokens.</param>
    /// <param name="actorUserId">Who did it. Defaults to the signed-in user.</param>
    public AuditLog Add(
        AuditAction action,
        bool succeeded = true,
        long? userId = null,
        string? userName = null,
        string? entityName = null,
        string? entityId = null,
        object? details = null,
        long? actorUserId = null)
    {
        var auditLog = new AuditLog
        {
            Action = action,
            Succeeded = succeeded,
            UserId = userId,
            UserName = Truncate(userName, 256),
            EntityName = entityName,
            EntityId = entityId,
            IpAddress = Truncate(_currentUserService.IpAddress, 64),
            UserAgent = Truncate(_currentUserService.UserAgent, 512),
            Details = details is null ? null : Truncate(JsonSerializer.Serialize(details, DetailsJsonOptions), 4000),
            CreatedByUserId = actorUserId ?? _currentUserService.UserId
        };

        _dbContext.AuditLogs.Add(auditLog);
        return auditLog;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
