using FluentValidation;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Validators.Common;

namespace ShippingERP.Application.Validators.MasterData;

public sealed class SaveSupplierRequestValidator : AbstractValidator<SaveSupplierRequest>
{
    public SaveSupplierRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(20);
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.ContactPerson).OptionalText(150);
        RuleFor(x => x.Phone).RequiredPhone();
        RuleFor(x => x.AlternatePhone).OptionalPhone();
        RuleFor(x => x.Email).OptionalEmail();
        RuleFor(x => x.TaxNumber).OptionalText(50);
        RuleFor(x => x.Address).OptionalText(300);
        RuleFor(x => x.OpeningBalance)
            .PrecisionScale(18, 2, true).WithMessage("الرصيد الافتتاحي يقبل منزلتين عشريتين على الأكثر.");
        RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class SupplierListQueryValidator : AbstractValidator<SupplierListQuery>
{
    public SupplierListQueryValidator()
    {
        Include(new PagedQueryRules<SupplierListQuery>(SupplierListQuery.AllowedSortFields));
    }
}

public sealed class SaveVehicleRequestValidator : AbstractValidator<SaveVehicleRequest>
{
    public SaveVehicleRequestValidator()
    {
        RuleFor(x => x.PlateNumber)
            .NotEmpty().WithMessage("رقم اللوحة مطلوب.")
            .MaximumLength(20).WithMessage("رقم اللوحة طويل جداً.")
            .Matches(@"^[\p{L}0-9][\p{L}0-9 -]*$").WithMessage("رقم اللوحة يقبل الحروف والأرقام والمسافة و - فقط.");
        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("الطراز مطلوب.")
            .MaximumLength(100).WithMessage("الطراز طويل جداً.");
        RuleFor(x => x.VehicleType).IsInEnum().WithMessage("نوع المركبة غير صالح.");
        RuleFor(x => x.CapacityKg)
            .GreaterThan(0m).WithMessage("الحمولة يجب أن تكون أكبر من صفر.")
            .LessThanOrEqualTo(100_000m).WithMessage("الحمولة كبيرة جداً.");
        RuleFor(x => x.BranchId).ValidId("الفرع مطلوب.");
        RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class VehicleListQueryValidator : AbstractValidator<VehicleListQuery>
{
    public VehicleListQueryValidator()
    {
        Include(new PagedQueryRules<VehicleListQuery>(VehicleListQuery.AllowedSortFields));
    }
}
