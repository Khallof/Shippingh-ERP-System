using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Users;
using ShippingERP.Application.Interfaces.Identity;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1;

/// <summary>
/// User administration (إدارة المستخدمين).
/// Callers without Branches.ViewAll only see and manage users of their own branch.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>Lists users with paging, filtering, search and sorting.</summary>
    /// <remarks>
    /// Example: GET /api/v1/users?pageNumber=1&amp;pageSize=20&amp;search=ahmed&amp;role=Driver&amp;isActive=true&amp;sortBy=fullName&amp;sortDirection=asc
    /// sortBy: userName (default), fullName, arabicFullName, email, employeeCode, createdAtUtc, lastLoginAtUtc.
    /// </remarks>
    [HttpGet]
    [HasPermission(Permissions.Users.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> GetUsers(
        [FromQuery] UserListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _userService.GetUsersAsync(query, cancellationToken);
        return OkResponse(result);
    }

    /// <summary>Returns one user.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Users.View)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(long id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetUserAsync(id, cancellationToken);
        return OkResponse(user);
    }

    /// <summary>Creates a user with an initial password and roles.</summary>
    /// <remarks>Only a SystemAdmin can grant the SystemAdmin role.</remarks>
    [HttpPost]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userService.CreateUserAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetUser), new { id = user.Id, version = "1" }, user, "تمت إضافة المستخدم");
    }

    /// <summary>Updates a user's details and replaces their roles.</summary>
    /// <remarks>If roles, branch or e-mail change, the user's current access tokens stop working (they must refresh).</remarks>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(
        long id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateUserAsync(id, request, cancellationToken);
        return OkResponse(user, "تم تحديث بيانات المستخدم");
    }

    /// <summary>Activates a user. Also unlocks an account locked by wrong passwords.</summary>
    [HttpPatch("{id:long}/activate")]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserDto>>> ActivateUser(long id, CancellationToken cancellationToken)
    {
        var user = await _userService.ActivateUserAsync(id, cancellationToken);
        return OkResponse(user, "تم تفعيل المستخدم");
    }

    /// <summary>Deactivates a user: blocks sign-in and ends all their sessions immediately.</summary>
    /// <remarks>You cannot deactivate yourself or the last active SystemAdmin (422).</remarks>
    [HttpPatch("{id:long}/deactivate")]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<UserDto>>> DeactivateUser(long id, CancellationToken cancellationToken)
    {
        var user = await _userService.DeactivateUserAsync(id, cancellationToken);
        return OkResponse(user, "تم إيقاف المستخدم");
    }
}
