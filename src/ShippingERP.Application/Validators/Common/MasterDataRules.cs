using System.Text.RegularExpressions;
using FluentValidation;

namespace ShippingERP.Application.Validators.Common;

/// <summary>Rules shared by all master-data validators (codes, names, phones, e-mails).</summary>
public static class MasterDataRules
{
    public const string CodePattern = "^[A-Za-z0-9][A-Za-z0-9_-]*$";
    public const string PhonePattern = @"^\+?[0-9 ]{6,20}$";

    /// <summary>Required code: letters, digits, - and _ (stored upper case).</summary>
    public static IRuleBuilderOptions<T, string> ValidCode<T>(this IRuleBuilder<T, string> rule, int maxLength = 20)
    {
        return rule
            .NotEmpty().WithMessage("الرمز مطلوب.")
            .MaximumLength(maxLength).WithMessage($"يجب ألا يزيد الرمز عن {maxLength} حرفاً.")
            .Matches(CodePattern).WithMessage("الرمز يقبل الحروف الإنجليزية والأرقام و - و _ فقط.");
    }

    public static IRuleBuilderOptions<T, string> ArabicName<T>(this IRuleBuilder<T, string> rule, int maxLength = 150)
    {
        return rule
            .NotEmpty().WithMessage("الاسم بالعربية مطلوب.")
            .MaximumLength(maxLength).WithMessage($"يجب ألا يزيد الاسم بالعربية عن {maxLength} حرفاً.");
    }

    public static IRuleBuilderOptions<T, string?> OptionalText<T>(this IRuleBuilder<T, string?> rule, int maxLength)
    {
        return rule
            .MaximumLength(maxLength).WithMessage($"يجب ألا يزيد النص عن {maxLength} حرفاً.");
    }

    public static IRuleBuilderOptions<T, string?> RequiredPhone<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule
            .NotEmpty().WithMessage("رقم الهاتف مطلوب.")
            .Matches(PhonePattern).WithMessage("رقم الهاتف غير صالح (أرقام فقط، ويمكن أن يبدأ بـ +).");
    }

    private static readonly Regex PhoneRegex = new(PhonePattern, RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    /// <summary>Empty is allowed; otherwise it must look like a phone number.</summary>
    public static IRuleBuilderOptions<T, string?> OptionalPhone<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule
            .Must(value => string.IsNullOrWhiteSpace(value) || PhoneRegex.IsMatch(value))
            .WithMessage("رقم الهاتف غير صالح (أرقام فقط، ويمكن أن يبدأ بـ +).");
    }

    /// <summary>Empty is allowed; otherwise it must look like an e-mail address.</summary>
    public static IRuleBuilderOptions<T, string?> OptionalEmail<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule
            .MaximumLength(256).WithMessage("البريد الإلكتروني طويل جداً.")
            .Must(value => string.IsNullOrWhiteSpace(value) || EmailRegex.IsMatch(value))
            .WithMessage("البريد الإلكتروني غير صالح.");
    }

    public static IRuleBuilderOptions<T, long> ValidId<T>(this IRuleBuilder<T, long> rule, string message)
    {
        return rule.GreaterThan(0).WithMessage(message);
    }

    public static IRuleBuilderOptions<T, long?> ValidOptionalId<T>(this IRuleBuilder<T, long?> rule, string message)
    {
        return rule.GreaterThan(0L).WithMessage(message);
    }
}
