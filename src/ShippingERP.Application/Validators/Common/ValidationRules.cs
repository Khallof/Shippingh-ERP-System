using FluentValidation;

namespace ShippingERP.Application.Validators.Common;

/// <summary>
/// Reusable rules. Password rules MUST match the Identity options in Infrastructure
/// (IdentityServiceRegistration): at least 8 characters, upper case, lower case, digit and symbol.
/// Identity checks them again when the password is saved, so they can never be bypassed.
/// </summary>
public static class ValidationRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    /// <summary>Same characters Identity accepts in a user name, minus "@" and "+".</summary>
    public const string UserNamePattern = "^[a-zA-Z0-9._-]+$";

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule)
    {
        return rule
            .NotEmpty().WithMessage("كلمة المرور مطلوبة.")
            .MinimumLength(PasswordMinLength).WithMessage($"يجب ألا تقل كلمة المرور عن {PasswordMinLength} أحرف.")
            .MaximumLength(PasswordMaxLength).WithMessage($"يجب ألا تزيد كلمة المرور عن {PasswordMaxLength} حرفاً.")
            .Matches("[A-Z]").WithMessage("يجب أن تحتوي كلمة المرور على حرف إنجليزي كبير (A-Z).")
            .Matches("[a-z]").WithMessage("يجب أن تحتوي كلمة المرور على حرف إنجليزي صغير (a-z).")
            .Matches("[0-9]").WithMessage("يجب أن تحتوي كلمة المرور على رقم (0-9).")
            .Matches("[^a-zA-Z0-9]").WithMessage("يجب أن تحتوي كلمة المرور على رمز مثل ! @ # $.");
    }
}
