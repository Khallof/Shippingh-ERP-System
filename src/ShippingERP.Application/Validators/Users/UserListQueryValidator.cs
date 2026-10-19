using FluentValidation;
using ShippingERP.Application.DTOs.Users;
using ShippingERP.Application.Validators.Common;

namespace ShippingERP.Application.Validators.Users;

public sealed class UserListQueryValidator : AbstractValidator<UserListQuery>
{
    public UserListQueryValidator()
    {
        Include(new PagedQueryRules<UserListQuery>(UserListQuery.AllowedSortFields));
    }
}
