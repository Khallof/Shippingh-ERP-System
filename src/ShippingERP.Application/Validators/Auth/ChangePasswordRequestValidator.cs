using FluentValidation;
using ShippingERP.Application.DTOs.Auth;
using ShippingERP.Application.Validators.Common;

namespace ShippingERP.Application.Validators.Auth;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("كلمة المرور الحالية مطلوبة.");

        RuleFor(x => x.NewPassword)
            .StrongPassword()
            .NotEqual(x => x.CurrentPassword).WithMessage("يجب أن تختلف كلمة المرور الجديدة عن الحالية.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("تأكيد كلمة المرور غير مطابق.");
    }
}
