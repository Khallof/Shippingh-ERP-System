using ShippingERP.Application.DTOs.Auth;

namespace ShippingERP.Application.Interfaces.Identity;

/// <summary>
/// Short-lived cache of (IsActive, SecurityStamp) per user, used on EVERY authenticated request to
/// reject tokens of deactivated users or tokens issued before a password/role/permission change.
/// Services call Invalidate after changing a user so the change takes effect immediately.
/// </summary>
public interface IUserSessionCache
{
    Task<UserSessionState?> GetAsync(long userId, CancellationToken cancellationToken);

    void Invalidate(long userId);
}
