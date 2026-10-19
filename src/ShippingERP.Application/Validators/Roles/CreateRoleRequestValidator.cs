using FluentValidation;
using ShippingERP.Application.DTOs.Roles;

namespace ShippingERP.Application.Validators.Roles;

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم الدور مطلوب.")
            .Matches("^[A-Za-z][A-Za-z0-9_]{2,49}$")
            .WithMessage("اسم الدور يبدأ بحرف إنجليزي ويحتوي على حروف إنجليزية وأرقام و _ فقط (3 إلى 50 حرفاً).");

        RuleFor(x => x.ArabicName)
            .NotEmpty().WithMessage("الاسم العربي للدور مطلوب.")
            .MaximumLength(100).WithMessage("الاسم العربي للدور طويل جداً.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("الوصف طويل جداً.");

        RuleFor(x => x.Permissions).ValidPermissionCodes();
    }
}
