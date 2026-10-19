using FluentValidation;
using ShippingERP.Application.DTOs.Roles;

namespace ShippingERP.Application.Validators.Roles;

public sealed class UpdateRolePermissionsRequestValidator : AbstractValidator<UpdateRolePermissionsRequest>
{
    public UpdateRolePermissionsRequestValidator()
    {
        RuleFor(x => x.Permissions).ValidPermissionCodes();
    }
}
