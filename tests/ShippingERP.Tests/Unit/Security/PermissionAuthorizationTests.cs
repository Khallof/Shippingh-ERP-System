using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Security;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.Tests.Unit.Security;

/// <summary>[HasPermission] attribute, policy provider and handler.</summary>
public sealed class PermissionAuthorizationTests
{
    private static ClaimsPrincipal CreateUser(IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var claims = new List<Claim> { new(AppClaimTypes.UserId, "7") };
        claims.AddRange(roles.Select(role => new Claim(AppClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim(AppClaimTypes.Permission, permission)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer"));
    }

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal user, params string[] requiredPermissions)
    {
        var requirement = new PermissionRequirement(requiredPermissions);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource: null);
        var handler = new PermissionAuthorizationHandler(NullLogger<PermissionAuthorizationHandler>.Instance);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task UserWithThePermission_IsAllowed()
    {
        var user = CreateUser(new[] { Roles.ShippingAgent }, new[] { Permissions.Shipments.Create });

        Assert.True(await AuthorizeAsync(user, Permissions.Shipments.Create));
    }

    [Fact]
    public async Task UserWithoutThePermission_IsDenied()
    {
        var user = CreateUser(new[] { Roles.Driver }, new[] { Permissions.Shipments.View, Permissions.Cod.Collect });

        Assert.False(await AuthorizeAsync(user, Permissions.Shipments.Create));
    }

    [Fact]
    public async Task PermissionCheck_IsCaseSensitive()
    {
        var user = CreateUser(new[] { Roles.ShippingAgent }, new[] { "shipments.create" });

        Assert.False(await AuthorizeAsync(user, Permissions.Shipments.Create));
    }

    [Fact]
    public async Task SystemAdmin_IsAllowedEverything_EvenWithoutPermissionClaims()
    {
        var user = CreateUser(new[] { Roles.SystemAdmin }, Array.Empty<string>());

        Assert.True(await AuthorizeAsync(user, Permissions.Accounting.ApproveJournal));
    }

    [Fact]
    public async Task MultiplePermissions_AnyOneIsEnough()
    {
        var user = CreateUser(new[] { Roles.BranchManager }, new[] { Permissions.Users.View });

        Assert.True(await AuthorizeAsync(user, Permissions.Roles.Manage, Permissions.Users.View));
    }

    [Fact]
    public async Task AnonymousUser_IsDenied()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(await AuthorizeAsync(anonymous, Permissions.Shipments.View));
    }

    [Fact]
    public void HasPermissionAttribute_BuildsPolicyName()
    {
        var single = new HasPermissionAttribute("Shipments.Create");
        var multiple = new HasPermissionAttribute(Permissions.Roles.Manage, Permissions.Users.View);

        Assert.Equal("Permission:Shipments.Create", single.Policy);
        Assert.Equal("Permission:Roles.Manage|Users.View", multiple.Policy);
    }

    [Fact]
    public async Task PolicyProvider_CreatesPermissionPolicy_RequiringAuthenticatedUser()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync("Permission:Roles.Manage|Users.View");

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
        var permissionRequirement = Assert.Single(policy.Requirements.OfType<PermissionRequirement>());
        Assert.Equal(new[] { "Roles.Manage", "Users.View" }, permissionRequirement.Permissions);
    }

    [Fact]
    public void EveryPermissionCode_IsUnique_AndBelongsToItsModule()
    {
        var codes = Permissions.All.Select(p => p.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(Permissions.All, p => Assert.StartsWith(p.Module + ".", p.Code));
        Assert.All(Roles.All.SelectMany(r => r.DefaultPermissions), code => Assert.True(Permissions.IsKnown(code), code));
        Assert.Equal(8, Roles.All.Count);
    }
}
