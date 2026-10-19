using FluentValidation;
using ShippingERP.Application.DTOs.Auth;

namespace ShippingERP.Application.Validators.Auth;

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("رمز التحديث مطلوب.")
            .MaximumLength(200).WithMessage("رمز التحديث غير صالح.");
    }
}
