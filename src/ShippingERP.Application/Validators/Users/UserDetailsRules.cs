using System.Linq.Expressions;
using FluentValidation;

namespace ShippingERP.Application.Validators.Users;

/// <summary>Rules shared by CreateUserRequest and UpdateUserRequest.</summary>
public sealed class UserDetailsRules<T> : AbstractValidator<T>
{
    public UserDetailsRules(
        Expression<Func<T, string>> email,
        Expression<Func<T, string?>> phoneNumber,
        Expression<Func<T, string>> fullName,
        Expression<Func<T, string>> arabicFullName,
        Expression<Func<T, long?>> branchId,
        Expression<Func<T, string?>> employeeCode,
        Expression<Func<T, List<string>>> roles)
    {
        var getPhoneNumber = phoneNumber.Compile();
        var getBranchId = branchId.Compile();

        RuleFor(email)
            .NotEmpty().WithMessage("البريد الإلكتروني مطلوب.")
            .MaximumLength(256).WithMessage("البريد الإلكتروني طويل جداً.")
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح.");

        RuleFor(phoneNumber)
            .Matches(@"^\+?[0-9 ]{6,20}$").WithMessage("رقم الهاتف غير صالح.")
            .When(x => !string.IsNullOrWhiteSpace(getPhoneNumber(x)));

        RuleFor(fullName)
            .NotEmpty().WithMessage("الاسم الكامل مطلوب.")
            .MaximumLength(150).WithMessage("الاسم الكامل طويل جداً.");

        RuleFor(arabicFullName)
            .NotEmpty().WithMessage("الاسم الكامل بالعربية مطلوب.")
            .MaximumLength(150).WithMessage("الاسم الكامل بالعربية طويل جداً.");

        RuleFor(branchId)
            .GreaterThan(0L).WithMessage("رقم الفرع غير صالح.")
            .When(x => getBranchId(x).HasValue);

        RuleFor(employeeCode)
            .MaximumLength(30).WithMessage("الرقم الوظيفي طويل جداً.");

        RuleFor(roles)
            .NotNull().WithMessage("يجب اختيار دور واحد على الأقل.")
            .Must(list => list.Count > 0).WithMessage("يجب اختيار دور واحد على الأقل.")
            .Must(list => list.Count <= 10).WithMessage("عدد الأدوار كبير جداً.")
            .Must(list => list.All(role => !string.IsNullOrWhiteSpace(role)))
            .WithMessage("اسم الدور لا يمكن أن يكون فارغاً.");
    }
}
