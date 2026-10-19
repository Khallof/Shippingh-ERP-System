using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Roles;
using ShippingERP.Application.Interfaces.Identity;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1;

/// <summary>
/// Roles and permissions (الأدوار والصلاحيات).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/roles")]
public sealed class RolesController : ApiControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    /// <summary>Lists all roles with their permissions and number of active users.</summary>
    /// <remarks>Allowed with Roles.Manage, or Users.View (needed to fill the role list on the user form).</remarks>
    [HttpGet]
    [HasPermission(Permissions.Roles.Manage, Permissions.Users.View)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoleDto>>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await _roleService.GetRolesAsync(cancellationToken);
        return OkResponse(roles);
    }

    /// <summary>Returns one role.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Roles.Manage, Permissions.Users.View)]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> GetRole(long id, CancellationToken cancellationToken)
    {
        var role = await _roleService.GetRoleAsync(id, cancellationToken);
        return OkResponse(role);
    }

    /// <summary>The full permission catalog (code, module, Arabic description) for the role editor.</summary>
    [HttpGet("permissions")]
    [HasPermission(Permissions.Roles.Manage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PermissionDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<IReadOnlyList<PermissionDto>>> GetPermissionCatalog()
    {
        return OkResponse(_roleService.GetPermissionCatalog());
    }

    /// <summary>Creates a custom role with a set of permissions.</summary>
    [HttpPost]
    [HasPermission(Permissions.Roles.Manage)]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var role = await _roleService.CreateRoleAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetRole), new { id = role.Id, version = "1" }, role, "تمت إضافة الدور");
    }

    /// <summary>Replaces the permissions of a role (send the complete list).</summary>
    /// <remarks>
    /// Users in the role must refresh their token (their current tokens return TOKEN_EXPIRED).
    /// SystemAdmin cannot be edited (422): it always has every permission.
    /// </remarks>
    [HttpPut("{id:long}/permissions")]
    [HasPermission(Permissions.Roles.Manage)]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> UpdateRolePermissions(
        long id,
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var role = await _roleService.UpdateRolePermissionsAsync(id, request, cancellationToken);
        return OkResponse(role, "تم تحديث صلاحيات الدور");
    }
}
