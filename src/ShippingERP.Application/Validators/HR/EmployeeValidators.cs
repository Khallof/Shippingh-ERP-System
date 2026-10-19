using FluentValidation;
using ShippingERP.Application.DTOs.HR;
using ShippingERP.Application.Validators.Common;

namespace ShippingERP.Application.Validators.HR;

public sealed class SaveEmployeeRequestValidator : AbstractValidator<SaveEmployeeRequest>
{
    public SaveEmployeeRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(20);
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.BranchId).ValidId("الفرع مطلوب.");
        RuleFor(x => x.JobTitle).OptionalText(100);
        RuleFor(x => x.Phone).OptionalPhone();
        RuleFor(x => x.Email).OptionalEmail();
        RuleFor(x => x.NationalId).OptionalText(30);
        RuleFor(x => x.UserId).ValidOptionalId("المستخدم غير صالح.").When(x => x.UserId.HasValue);
        RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class EmployeeListQueryValidator : AbstractValidator<EmployeeListQuery>
{
    public EmployeeListQueryValidator()
    {
        Include(new PagedQueryRules<EmployeeListQuery>(EmployeeListQuery.AllowedSortFields));
    }
}

public sealed class SaveDriverRequestValidator : AbstractValidator<SaveDriverRequest>
{
    public SaveDriverRequestValidator()
    {
        RuleFor(x => x.EmployeeId).ValidId("الموظف مطلوب.");
        RuleFor(x => x.UserId).ValidOptionalId("المستخدم غير صالح.").When(x => x.UserId.HasValue);
        RuleFor(x => x.LicenseNumber)
            .NotEmpty().WithMessage("رقم رخصة القيادة مطلوب.")
            .MaximumLength(50).WithMessage("رقم الرخصة طويل جداً.");
        RuleFor(x => x.LicenseCategory).OptionalText(20);
        RuleFor(x => x.LicenseExpiryDateUtc)
            .NotEmpty().WithMessage("تاريخ انتهاء الرخصة مطلوب.");
        RuleFor(x => x.DefaultVehicleId).ValidOptionalId("المركبة غير صالحة.").When(x => x.DefaultVehicleId.HasValue);
        RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class DriverListQueryValidator : AbstractValidator<DriverListQuery>
{
    public DriverListQueryValidator()
    {
        Include(new PagedQueryRules<DriverListQuery>(DriverListQuery.AllowedSortFields));
    }
}
