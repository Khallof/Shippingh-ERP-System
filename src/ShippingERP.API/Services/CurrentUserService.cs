using System.Security.Claims;
using ShippingERP.Application.Common.Security;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Services;

/// <summary>
/// Reads the caller's identity from the validated JWT claims, and the IP address / User-Agent from the request.
/// Returns null values when there is no request (background jobs, migrations, seeding) or the caller is anonymous.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public long? UserId => ReadLongClaim(AppClaimTypes.UserId);

    public long? BranchId => ReadLongClaim(AppClaimTypes.BranchId);

    public bool IsSystemAdmin => IsAuthenticated && User!.HasClaim(AppClaimTypes.Role, Roles.SystemAdmin);

    public string? IpAddress
    {
        get
        {
            var address = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
            if (address is null)
            {
                return null;
            }

            // "::ffff:192.168.1.10" -> "192.168.1.10"
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();
        }
    }

    public string? UserAgent
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public bool HasPermission(string permission) =>
        IsAuthenticated && (IsSystemAdmin || User!.HasClaim(AppClaimTypes.Permission, permission));

    public bool IsInRole(string role) =>
        IsAuthenticated && User!.HasClaim(AppClaimTypes.Role, role);

    private long? ReadLongClaim(string claimType)
    {
        if (!IsAuthenticated)
        {
            return null;
        }

        var value = User!.FindFirstValue(claimType);
        return long.TryParse(value, out var number) ? number : null;
    }
}
