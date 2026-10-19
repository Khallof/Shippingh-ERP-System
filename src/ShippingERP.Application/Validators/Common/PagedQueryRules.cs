using FluentValidation;
using ShippingERP.Application.DTOs.Common;

namespace ShippingERP.Application.Validators.Common;

/// <summary>Paging, search and sort rules shared by every list query.</summary>
public sealed class PagedQueryRules<T> : AbstractValidator<T>
    where T : PagedQuery
{
    public PagedQueryRules(IReadOnlyCollection<string> allowedSortFields)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("رقم الصفحة يجب أن يكون 1 أو أكثر.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PagedQuery.MaxPageSize)
            .WithMessage($"حجم الصفحة يجب أن يكون بين 1 و {PagedQuery.MaxPageSize}.");

        RuleFor(x => x.Search)
            .MaximumLength(100).WithMessage("نص البحث طويل جداً.");

        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) ||
                            allowedSortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"قيمة الترتيب غير مدعومة. القيم المسموحة: {string.Join(", ", allowedSortFields)}.");

        RuleFor(x => x.SortDirection)
            .Must(direction => string.IsNullOrWhiteSpace(direction) ||
                               direction.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                               direction.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("اتجاه الترتيب يجب أن يكون asc أو desc.");
    }
}
