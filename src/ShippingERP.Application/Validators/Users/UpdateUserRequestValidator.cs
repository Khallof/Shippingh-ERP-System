using FluentValidation;
using ShippingERP.Application.DTOs.Users;

namespace ShippingERP.Application.Validators.Users;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        Include(new UserDetailsRules<UpdateUserRequest>(
            x => x.Email, x => x.PhoneNumber, x => x.FullName, x => x.ArabicFullName,
            x => x.BranchId, x => x.EmployeeCode, x => x.Roles));
    }
}
