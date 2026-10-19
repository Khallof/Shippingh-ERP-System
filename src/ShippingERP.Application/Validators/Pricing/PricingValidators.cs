using FluentValidation;
using ShippingERP.Application.DTOs.Pricing;
using ShippingERP.Application.Validators.Common;
using ShippingERP.Application.Validators.Shipments;

namespace ShippingERP.Application.Validators.Pricing;

public sealed class ShipmentPriceRequestValidator : AbstractValidator<ShipmentPriceRequest>
{
    public ShipmentPriceRequestValidator()
    {
        RuleFor(x => x.ManualShippingCharge!.Value)
            .Money("سعر الشحن اليدوي")
            .OverridePropertyName("manualShippingCharge")
            .When(x => x.ManualShippingCharge.HasValue);

        RuleFor(x => x.ManualDiscountAmount).Money("الخصم اليدوي");
        RuleFor(x => x.ManualDiscountAmount)
            .LessThanOrEqualTo(x => x.ManualShippingCharge!.Value)
            .WithMessage("الخصم لا يمكن أن يزيد عن سعر الشحن.")
            .When(x => x.ManualShippingCharge.HasValue);

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("سبب تعديل السعر مطلوب.")
            .MinimumLength(5).WithMessage("اكتب سبب تعديل السعر بوضوح (5 أحرف على الأقل).")
            .When(x => x.ManualShippingCharge.HasValue || x.ManualDiscountAmount > 0m);
        RuleFor(x => x.Reason).OptionalText(500);
    }
}

public sealed class PriceOverrideListQueryValidator : AbstractValidator<PriceOverrideListQuery>
{
    public PriceOverrideListQueryValidator()
    {
        Include(new PagedQueryRules<PriceOverrideListQuery>(PriceOverrideListQuery.AllowedSortFields));
        RuleFor(x => x.Status).IsInEnum().WithMessage("الحالة غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.ShipmentId).ValidOptionalId("الشحنة غير صالحة.").When(x => x.ShipmentId.HasValue);
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

public sealed class DecidePriceOverrideRequestValidator : AbstractValidator<DecidePriceOverrideRequest>
{
    public DecidePriceOverrideRequestValidator()
    {
        RuleFor(x => x.Notes).OptionalText(500);
    }
}
