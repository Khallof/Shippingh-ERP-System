using FluentValidation;
using ShippingERP.Application.DTOs.Users;
using ShippingERP.Application.Validators.Common;

namespace ShippingERP.Application.Validators.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("اسم المستخدم مطلوب.")
            .Length(3, 50).WithMessage("يجب أن يكون اسم المستخدم بين 3 و 50 حرفاً.")
            .Matches(ValidationRules.UserNamePattern)
            .WithMessage("اسم المستخدم يقبل الحروف الإنجليزية والأرقام والرموز . _ - فقط.");

        RuleFor(x => x.Password).StrongPassword();

        Include(new UserDetailsRules<CreateUserRequest>(
            x => x.Email, x => x.PhoneNumber, x => x.FullName, x => x.ArabicFullName,
            x => x.BranchId, x => x.EmployeeCode, x => x.Roles));
    }
}
