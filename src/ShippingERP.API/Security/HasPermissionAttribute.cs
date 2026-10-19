using Microsoft.AspNetCore.Authorization;

namespace ShippingERP.API.Security;

/// <summary>
/// Protects a controller or action with one or more permission codes:
///
///     [HasPermission("Shipments.Create")]
///     [HasPermission(Permissions.Shipments.Create)]                       // same, with the constant
///     [HasPermission(Permissions.Users.View, Permissions.Roles.Manage)]   // ANY of the listed permissions
///
/// Put the attribute twice to require BOTH permissions.
/// The user must be signed in with a valid, up-to-date token. SystemAdmin passes every check.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";
    public const char Separator = '|';

    public HasPermissionAttribute(params string[] permissions)
        : base(PolicyPrefix + string.Join(Separator, permissions))
    {
        if (permissions.Length == 0)
        {
            throw new ArgumentException("At least one permission is required.", nameof(permissions));
        }

        Permissions = permissions;
    }

    public IReadOnlyList<string> Permissions { get; }
}
