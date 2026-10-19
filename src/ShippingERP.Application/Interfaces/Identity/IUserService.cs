using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Users;

namespace ShippingERP.Application.Interfaces.Identity;

/// <summary>User administration (Users.View / Users.Manage).</summary>
public interface IUserService
{
    Task<PagedResult<UserDto>> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken);

    Task<UserDto> GetUserAsync(long id, CancellationToken cancellationToken);

    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<UserDto> UpdateUserAsync(long id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<UserDto> ActivateUserAsync(long id, CancellationToken cancellationToken);

    Task<UserDto> DeactivateUserAsync(long id, CancellationToken cancellationToken);
}
