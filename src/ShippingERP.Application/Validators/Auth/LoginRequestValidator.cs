using FluentValidation;
using ShippingERP.Application.DTOs.Auth;

namespace ShippingERP.Application.Validators.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserNameOrEmail)
            .NotEmpty().WithMessage("اسم المستخدم أو البريد الإلكتروني مطلوب.")
            .MaximumLength(256).WithMessage("اسم المستخدم أو البريد الإلكتروني طويل جداً.");

        // Only "not empty" here: complexity rules are checked when a password is SET, not at sign-in.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة.")
            .MaximumLength(128).WithMessage("كلمة المرور طويلة جداً.");
    }
}
