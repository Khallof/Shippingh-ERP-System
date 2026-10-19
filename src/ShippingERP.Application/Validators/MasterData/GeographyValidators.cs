using FluentValidation;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Validators.Common;

namespace ShippingERP.Application.Validators.MasterData;

public sealed class SaveCountryRequestValidator : AbstractValidator<SaveCountryRequest>
{
    public SaveCountryRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("رمز الدولة مطلوب.")
            .Matches("^[A-Za-z]{2,3}$").WithMessage("رمز الدولة حرفان أو ثلاثة أحرف إنجليزية (ISO)، مثل SY.");
        RuleFor(x => x.ArabicName).ArabicName(100);
        RuleFor(x => x.EnglishName).OptionalText(100);
        RuleFor(x => x.PhoneCode)
            .Matches(@"^\+[0-9]{1,4}$").WithMessage("رمز الاتصال يبدأ بـ + ثم 1 إلى 4 أرقام، مثل +963.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneCode));
    }
}

public sealed class CountryListQueryValidator : AbstractValidator<CountryListQuery>
{
    public CountryListQueryValidator()
    {
        Include(new PagedQueryRules<CountryListQuery>(CountryListQuery.AllowedSortFields));
    }
}

public sealed class SaveCityRequestValidator : AbstractValidator<SaveCityRequest>
{
    public SaveCityRequestValidator()
    {
        RuleFor(x => x.CountryId).ValidId("الدولة مطلوبة.");
        RuleFor(x => x.Code).ValidCode(10);
        RuleFor(x => x.ArabicName).ArabicName(100);
        RuleFor(x => x.EnglishName).OptionalText(100);
    }
}

public sealed class CityListQueryValidator : AbstractValidator<CityListQuery>
{
    public CityListQueryValidator()
    {
        Include(new PagedQueryRules<CityListQuery>(CityListQuery.AllowedSortFields));
    }
}

public sealed class SaveZoneRequestValidator : AbstractValidator<SaveZoneRequest>
{
    public SaveZoneRequestValidator()
    {
        RuleFor(x => x.CityId).ValidId("المدينة مطلوبة.");
        RuleFor(x => x.Code).ValidCode(20);
        RuleFor(x => x.ArabicName).ArabicName(100);
        RuleFor(x => x.EnglishName).OptionalText(100);
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class ZoneListQueryValidator : AbstractValidator<ZoneListQuery>
{
    public ZoneListQueryValidator()
    {
        Include(new PagedQueryRules<ZoneListQuery>(ZoneListQuery.AllowedSortFields));
    }
}
