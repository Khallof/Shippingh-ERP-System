using FluentValidation;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.Application.Validators.Roles;

/// <summary>Every code must exist in Domain/Authorization/Permissions.cs (case-sensitive) and appear once.</summary>
public static class PermissionCodeRules
{
    public static IRuleBuilderOptions<T, List<string>> ValidPermissionCodes<T>(this IRuleBuilder<T, List<string>> rule)
    {
        return rule
            .NotNull().WithMessage("قائمة الصلاحيات مطلوبة.")
            .Must(list => list.Count == list.Distinct(StringComparer.Ordinal).Count())
            .WithMessage("قائمة الصلاحيات تحتوي على تكرار.")
            .Must(list => list.All(code => !string.IsNullOrWhiteSpace(code) && Permissions.IsKnown(code)))
            .WithMessage((_, list) =>
                "صلاحيات غير معروفة: " +
                string.Join(", ", list.Where(code => string.IsNullOrWhiteSpace(code) || !Permissions.IsKnown(code))));
    }
}
