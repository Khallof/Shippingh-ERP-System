using FluentValidation;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Validators.Common;

namespace ShippingERP.Application.Validators.MasterData;

public sealed class SaveBranchRequestValidator : AbstractValidator<SaveBranchRequest>
{
    public SaveBranchRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(10);
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.CityId).ValidId("المدينة مطلوبة.");
        RuleFor(x => x.Address).OptionalText(300);
        RuleFor(x => x.Phone).OptionalPhone();
        RuleFor(x => x.Email).OptionalEmail();
        RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class BranchListQueryValidator : AbstractValidator<BranchListQuery>
{
    public BranchListQueryValidator()
    {
        Include(new PagedQueryRules<BranchListQuery>(BranchListQuery.AllowedSortFields));
    }
}

public sealed class SaveWarehouseRequestValidator : AbstractValidator<SaveWarehouseRequest>
{
    public SaveWarehouseRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(20);
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.BranchId).ValidId("الفرع مطلوب.");
        RuleFor(x => x.Address).OptionalText(300);
        RuleFor(x => x.Phone).OptionalPhone();
        RuleFor(x => x.CapacityCubicMeters)
            .GreaterThan(0m).WithMessage("السعة يجب أن تكون أكبر من صفر.")
            .LessThan(100_000_000m).WithMessage("السعة كبيرة جداً.")
            .When(x => x.CapacityCubicMeters.HasValue);
        RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class WarehouseListQueryValidator : AbstractValidator<WarehouseListQuery>
{
    public WarehouseListQueryValidator()
    {
        Include(new PagedQueryRules<WarehouseListQuery>(WarehouseListQuery.AllowedSortFields));
    }
}
