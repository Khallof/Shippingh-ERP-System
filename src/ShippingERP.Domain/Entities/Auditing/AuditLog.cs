using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Auditing;

/// <summary>
/// One security event (sign-in, failed sign-in, password change, user or role change...).
///
/// Append-only: the interceptor refuses any UPDATE or DELETE on this table.
/// CreatedByUserId = the user who performed the action (null when anonymous, e.g. a failed sign-in).
/// </summary>
public class AuditLog : BaseEntity, IAppendOnlyEntity
{
    public AuditAction Action { get; set; }

    /// <summary>True for a successful action, false for a refused one (failed sign-in, reused token...).</summary>
    public bool Succeeded { get; set; } = true;

    /// <summary>The user the event is about (signed-in user, edited user...). Null if unknown.</summary>
    public long? UserId { get; set; }

    /// <summary>User name as typed or as stored. Kept even when UserId is null (unknown user name).</summary>
    public string? UserName { get; set; }

    /// <summary>Affected table, e.g. "User", "Role".</summary>
    public string? EntityName { get; set; }

    /// <summary>Affected record id as text.</summary>
    public string? EntityId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>Short JSON with extra details (reason, changed fields, added/removed permissions). Never passwords or tokens.</summary>
    public string? Details { get; set; }
}
