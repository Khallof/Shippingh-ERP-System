using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ShippingERP.Application.Common.Security;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Security;

/// <summary>The requirement created for each [HasPermission(...)] policy: the user needs ANY of these permissions.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(IReadOnlyList<string> permissions)
    {
        Permissions = permissions;
    }

    public IReadOnlyList<string> Permissions { get; }
}

/// <summary>
/// Creates "Permission:xxx" policies on demand, so we don't have to register one policy per permission.
/// Any other policy name is handled by the default provider.
/// </summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName);
        }

        var permissions = policyName[HasPermissionAttribute.PolicyPrefix.Length..]
            .Split(HasPermissionAttribute.Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissions))
            .Build();
    }
}

/// <summary>
/// Checks the "permission" claims of the access token.
///
/// The claims can be trusted to be current: the JWT validation step (JwtAuthenticationSetup) rejects any token
/// whose security stamp is older than the user's, and the stamp changes whenever the user's roles or the
/// permissions of one of their roles change. So no database query is needed here.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var user = context.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var isSystemAdmin = user.HasClaim(AppClaimTypes.Role, Roles.SystemAdmin);

        if (isSystemAdmin || requirement.Permissions.Any(permission => user.HasClaim(AppClaimTypes.Permission, permission)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "User {UserId} was denied access; requires any of: {Permissions}",
            user.FindFirst(AppClaimTypes.UserId)?.Value,
            string.Join(", ", requirement.Permissions));

        return Task.CompletedTask;
    }
}
