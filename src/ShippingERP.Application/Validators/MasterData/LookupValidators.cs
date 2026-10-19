using FluentValidation;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Validators.Common;

namespace ShippingERP.Application.Validators.MasterData;

public sealed class SaveServiceTypeRequestValidator : AbstractValidator<SaveServiceTypeRequest>
{
    public SaveServiceTypeRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(20);
        RuleFor(x => x.ArabicName).ArabicName(100);
        RuleFor(x => x.EnglishName).OptionalText(100);
        RuleFor(x => x.Description).OptionalText(500);
        RuleFor(x => x.EstimatedDeliveryDays)
            .InclusiveBetween(0, 60).WithMessage("مدة التوصيل يجب أن تكون بين 0 و 60 يوماً.");
        RuleFor(x => x.SortOrder)
            .InclusiveBetween(0, 10_000).WithMessage("ترتيب العرض يجب أن يكون بين 0 و 10000.");
    }
}

public sealed class ServiceTypeListQueryValidator : AbstractValidator<ServiceTypeListQuery>
{
    public ServiceTypeListQueryValidator()
    {
        Include(new PagedQueryRules<ServiceTypeListQuery>(ServiceTypeListQuery.AllowedSortFields));
    }
}

public sealed class SavePaymentMethodRequestValidator : AbstractValidator<SavePaymentMethodRequest>
{
    public SavePaymentMethodRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(20);
        RuleFor(x => x.ArabicName).ArabicName(100);
        RuleFor(x => x.EnglishName).OptionalText(100);
        RuleFor(x => x.Kind).IsInEnum().WithMessage("نوع طريقة الدفع غير صالح.");
        RuleFor(x => x.SortOrder)
            .InclusiveBetween(0, 10_000).WithMessage("ترتيب العرض يجب أن يكون بين 0 و 10000.");
    }
}

public sealed class PaymentMethodListQueryValidator : AbstractValidator<PaymentMethodListQuery>
{
    public PaymentMethodListQueryValidator()
    {
        Include(new PagedQueryRules<PaymentMethodListQuery>(PaymentMethodListQuery.AllowedSortFields));
    }
}

public sealed class SaveTaxRateRequestValidator : AbstractValidator<SaveTaxRateRequest>
{
    public SaveTaxRateRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(20);
        RuleFor(x => x.ArabicName).ArabicName(100);
        RuleFor(x => x.EnglishName).OptionalText(100);
        RuleFor(x => x.Percentage)
            .InclusiveBetween(0m, 100m).WithMessage("النسبة يجب أن تكون بين 0 و 100.")
            .PrecisionScale(5, 2, true).WithMessage("النسبة تقبل منزلتين عشريتين على الأكثر.");
    }
}

public sealed class TaxRateListQueryValidator : AbstractValidator<TaxRateListQuery>
{
    public TaxRateListQueryValidator()
    {
        Include(new PagedQueryRules<TaxRateListQuery>(TaxRateListQuery.AllowedSortFields));
    }
}

public sealed class SaveExpenseCategoryRequestValidator : AbstractValidator<SaveExpenseCategoryRequest>
{
    public SaveExpenseCategoryRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(20);
        RuleFor(x => x.ArabicName).ArabicName(100);
        RuleFor(x => x.EnglishName).OptionalText(100);
        RuleFor(x => x.Description).OptionalText(500);
    }
}

public sealed class ExpenseCategoryListQueryValidator : AbstractValidator<ExpenseCategoryListQuery>
{
    public ExpenseCategoryListQueryValidator()
    {
        Include(new PagedQueryRules<ExpenseCategoryListQuery>(ExpenseCategoryListQuery.AllowedSortFields));
    }
}
