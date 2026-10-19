using FluentValidation;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Validators.Common;

namespace ShippingERP.Application.Validators.MasterData;

public sealed class SaveCurrencyRequestValidator : AbstractValidator<SaveCurrencyRequest>
{
    public SaveCurrencyRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("رمز العملة مطلوب.")
            .Matches("^[A-Za-z]{3}$").WithMessage("رمز العملة ثلاثة أحرف إنجليزية (ISO 4217)، مثل SYP.");
        RuleFor(x => x.ArabicName).ArabicName(100);
        RuleFor(x => x.EnglishName).OptionalText(100);
        RuleFor(x => x.Symbol)
            .NotEmpty().WithMessage("رمز العرض مطلوب.")
            .MaximumLength(10).WithMessage("رمز العرض طويل جداً.");
        RuleFor(x => x.DecimalPlaces)
            .InclusiveBetween(0, 4).WithMessage("عدد المنازل العشرية يجب أن يكون بين 0 و 4.");
    }
}

public sealed class CurrencyListQueryValidator : AbstractValidator<CurrencyListQuery>
{
    public CurrencyListQueryValidator()
    {
        Include(new PagedQueryRules<CurrencyListQuery>(CurrencyListQuery.AllowedSortFields));
    }
}

public sealed class SaveExchangeRateRequestValidator : AbstractValidator<SaveExchangeRateRequest>
{
    public SaveExchangeRateRequestValidator()
    {
        RuleFor(x => x.FromCurrencyId).ValidId("العملة المصدر مطلوبة.");
        RuleFor(x => x.ToCurrencyId)
            .ValidId("العملة الهدف مطلوبة.")
            .NotEqual(x => x.FromCurrencyId).WithMessage("لا يمكن تحويل العملة إلى نفسها.");
        RuleFor(x => x.Rate)
            .GreaterThan(0m).WithMessage("سعر الصرف يجب أن يكون أكبر من صفر.")
            .PrecisionScale(18, 6, true).WithMessage("سعر الصرف يقبل 6 منازل عشرية على الأكثر.");
        RuleFor(x => x.EffectiveFromUtc)
            .NotEmpty().WithMessage("تاريخ بداية السريان مطلوب.");
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class ExchangeRateListQueryValidator : AbstractValidator<ExchangeRateListQuery>
{
    public ExchangeRateListQueryValidator()
    {
        Include(new PagedQueryRules<ExchangeRateListQuery>(ExchangeRateListQuery.AllowedSortFields));
    }
}

public sealed class SaveCompanyProfileRequestValidator : AbstractValidator<SaveCompanyProfileRequest>
{
    public SaveCompanyProfileRequestValidator()
    {
        RuleFor(x => x.ArabicName).ArabicName(200);
        RuleFor(x => x.EnglishName).OptionalText(200);
        RuleFor(x => x.CommercialRegistrationNumber).OptionalText(50);
        RuleFor(x => x.TaxNumber).OptionalText(50);
        RuleFor(x => x.Phone).OptionalPhone();
        RuleFor(x => x.Email).OptionalEmail();
        RuleFor(x => x.Website).OptionalText(200);
        RuleFor(x => x.Address).OptionalText(300);
        RuleFor(x => x.LogoUrl).OptionalText(500);
        RuleFor(x => x.BaseCurrencyId).ValidId("العملة الأساسية مطلوبة.");
    }
}
