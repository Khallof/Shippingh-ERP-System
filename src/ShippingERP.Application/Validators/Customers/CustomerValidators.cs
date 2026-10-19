using FluentValidation;
using ShippingERP.Application.DTOs.Customers;
using ShippingERP.Application.Validators.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.Validators.Customers;

public sealed class SaveCustomerRequestValidator : AbstractValidator<SaveCustomerRequest>
{
    public SaveCustomerRequestValidator()
    {
        RuleFor(x => x.Code!)
            .ValidCode(20)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));
        RuleFor(x => x.CustomerType).IsInEnum().WithMessage("نوع العميل غير صالح.");
        RuleFor(x => x.Roles)
            .Must(role => role is CustomerRole.Sender or CustomerRole.Recipient or CustomerRole.Both)
            .WithMessage("دور العميل يجب أن يكون Sender أو Recipient أو Both.");
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.Phone).RequiredPhone();
        RuleFor(x => x.AlternatePhone).OptionalPhone();
        RuleFor(x => x.Email).OptionalEmail();
        RuleFor(x => x.NationalOrCompanyNumber).OptionalText(50);
        RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0m).WithMessage("حد الائتمان لا يمكن أن يكون سالباً.")
            .PrecisionScale(18, 2, true).WithMessage("حد الائتمان يقبل منزلتين عشريتين على الأكثر.");
        RuleFor(x => x.OpeningBalance)
            .PrecisionScale(18, 2, true).WithMessage("الرصيد الافتتاحي يقبل منزلتين عشريتين على الأكثر.");
        RuleFor(x => x.Notes).OptionalText(1000);
        RuleFor(x => x.DefaultBranchId).ValidId("الفرع الافتراضي مطلوب.");
        RuleFor(x => x.PricingPlanId).ValidOptionalId("خطة التسعير غير صالحة.").When(x => x.PricingPlanId.HasValue);
    }
}

public sealed class CustomerListQueryValidator : AbstractValidator<CustomerListQuery>
{
    public CustomerListQueryValidator()
    {
        Include(new PagedQueryRules<CustomerListQuery>(CustomerListQuery.AllowedSortFields));
    }
}

public sealed class SaveCustomerAddressRequestValidator : AbstractValidator<SaveCustomerAddressRequest>
{
    public SaveCustomerAddressRequestValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("اسم العنوان مطلوب (مثل: المنزل، المكتب).")
            .MaximumLength(50).WithMessage("اسم العنوان طويل جداً.");
        RuleFor(x => x.CityId).ValidId("المدينة مطلوبة.");
        RuleFor(x => x.ZoneId).ValidOptionalId("المنطقة غير صالحة.").When(x => x.ZoneId.HasValue);
        RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage("العنوان التفصيلي مطلوب.")
            .MaximumLength(300).WithMessage("العنوان طويل جداً.");
        RuleFor(x => x.Landmark).OptionalText(200);
        RuleFor(x => x.BuildingNumber).OptionalText(20);
        RuleFor(x => x.PostalCode).OptionalText(20);
        RuleFor(x => x.ContactName).OptionalText(150);
        RuleFor(x => x.Phone).OptionalPhone();
        RuleFor(x => x.AlternatePhone).OptionalPhone();
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m).WithMessage("خط العرض يجب أن يكون بين -90 و 90.")
            .When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m).WithMessage("خط الطول يجب أن يكون بين -180 و 180.")
            .When(x => x.Longitude.HasValue);
        RuleFor(x => x.Longitude)
            .NotNull().WithMessage("أدخل خط الطول مع خط العرض، أو اتركهما فارغين.")
            .When(x => x.Latitude.HasValue);
        RuleFor(x => x.Latitude)
            .NotNull().WithMessage("أدخل خط العرض مع خط الطول، أو اتركهما فارغين.")
            .When(x => x.Longitude.HasValue);
    }
}

public sealed class SavePricingPlanRequestValidator : AbstractValidator<SavePricingPlanRequest>
{
    public SavePricingPlanRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(20);
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.CurrencyId).ValidId("العملة مطلوبة.");
        RuleFor(x => x.ValidToUtc)
            .GreaterThan(x => x.ValidFromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.ValidFromUtc.HasValue && x.ValidToUtc.HasValue);
        RuleFor(x => x.ContractCustomerId).ValidOptionalId("العميل غير صالح.").When(x => x.ContractCustomerId.HasValue);
        RuleFor(x => x.IsDefault)
            .Equal(false).WithMessage("عقد العميل لا يمكن أن يكون خطة التسعير الافتراضية.")
            .When(x => x.ContractCustomerId.HasValue);
        RuleFor(x => x.ContractReference).OptionalText(50);
        RuleFor(x => x.ContractReference)
            .Empty().WithMessage("مرجع العقد يُستخدم مع عقود العملاء فقط.")
            .When(x => !x.ContractCustomerId.HasValue);
        RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class PricingPlanListQueryValidator : AbstractValidator<PricingPlanListQuery>
{
    public PricingPlanListQueryValidator()
    {
        Include(new PagedQueryRules<PricingPlanListQuery>(PricingPlanListQuery.AllowedSortFields));
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.ContractCustomerId).ValidOptionalId("العميل غير صالح.").When(x => x.ContractCustomerId.HasValue);
    }
}

public sealed class SavePriceRuleConditionRequestValidator : AbstractValidator<SavePriceRuleConditionRequest>
{
    public SavePriceRuleConditionRequestValidator()
    {
        RuleFor(x => x.ConditionType).IsInEnum().WithMessage("نوع الشرط غير صالح.");

        // Reference conditions: an id, no range.
        RuleFor(x => x.ReferenceId)
            .NotNull().WithMessage("حدد المنطقة / المدينة / نوع الخدمة لهذا الشرط.")
            .GreaterThan(0).WithMessage("المعرّف غير صالح.")
            .When(x => PriceConditionTypes.IsReference(x.ConditionType));
        RuleFor(x => x)
            .Must(x => x.MinValue is null && x.MaxValue is null)
            .OverridePropertyName("minValue")
            .WithMessage("شروط المناطق والمدن ونوع الخدمة لا تقبل حداً أدنى أو أعلى.")
            .When(x => PriceConditionTypes.IsReference(x.ConditionType));

        // Range conditions: min and/or max, no id.
        RuleFor(x => x.ReferenceId)
            .Null().WithMessage("شروط الوزن وعدد الطرود لا تقبل معرّفاً.")
            .When(x => PriceConditionTypes.IsRange(x.ConditionType));
        RuleFor(x => x)
            .Must(x => x.MinValue.HasValue || x.MaxValue.HasValue)
            .OverridePropertyName("minValue")
            .WithMessage("أدخل الحد الأدنى أو الحد الأعلى (أو كليهما).")
            .When(x => PriceConditionTypes.IsRange(x.ConditionType));
        RuleFor(x => x.MinValue)
            .GreaterThanOrEqualTo(0m).WithMessage("الحد الأدنى لا يمكن أن يكون سالباً.")
            .PrecisionScale(10, 3, true).WithMessage("القيمة تقبل 3 منازل عشرية على الأكثر.")
            .When(x => x.MinValue.HasValue);
        RuleFor(x => x.MaxValue)
            .GreaterThan(x => x.MinValue ?? 0m).WithMessage("الحد الأعلى يجب أن يكون أكبر من الحد الأدنى.")
            .PrecisionScale(10, 3, true).WithMessage("القيمة تقبل 3 منازل عشرية على الأكثر.")
            .When(x => x.MaxValue.HasValue);
        RuleFor(x => x)
            .Must(x => (x.MinValue is null || x.MinValue == decimal.Truncate(x.MinValue.Value)) &&
                       (x.MaxValue is null || x.MaxValue == decimal.Truncate(x.MaxValue.Value)))
            .OverridePropertyName("minValue")
            .WithMessage("عدد الطرود رقم صحيح.")
            .When(x => x.ConditionType == PriceConditionType.PackageCount);
    }
}

public sealed class SavePriceRuleWeightTierRequestValidator : AbstractValidator<SavePriceRuleWeightTierRequest>
{
    public SavePriceRuleWeightTierRequestValidator()
    {
        RuleFor(x => x.FromWeightKg)
            .GreaterThanOrEqualTo(0m).WithMessage("بداية الشريحة لا يمكن أن تكون سالبة.")
            .PrecisionScale(10, 3, true).WithMessage("الوزن يقبل 3 منازل عشرية على الأكثر.");
        RuleFor(x => x.ToWeightKg)
            .GreaterThan(x => x.FromWeightKg).WithMessage("نهاية الشريحة يجب أن تكون أكبر من بدايتها.")
            .PrecisionScale(10, 3, true).WithMessage("الوزن يقبل 3 منازل عشرية على الأكثر.")
            .When(x => x.ToWeightKg.HasValue);
        RuleFor(x => x.PricePerKg).PriceAmount("سعر الكيلو");
    }
}

public sealed class SavePricingRuleRequestValidator : AbstractValidator<SavePricingRuleRequest>
{
    public const int MaxConditions = 50;
    public const int MaxTiers = 20;

    public SavePricingRuleRequestValidator()
    {
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.Priority).InclusiveBetween(0, 1000).WithMessage("الأولوية يجب أن تكون بين 0 و 1000.");
        RuleFor(x => x.ValidToUtc)
            .GreaterThan(x => x.ValidFromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.ValidFromUtc.HasValue && x.ValidToUtc.HasValue);

        RuleFor(x => x.Conditions)
            .Must(list => list.Count <= MaxConditions).WithMessage($"الحد الأقصى {MaxConditions} شرطاً في القاعدة.")
            .Must(list => list.Where(c => PriceConditionTypes.IsRange(c.ConditionType)).GroupBy(c => c.ConditionType).All(g => g.Count() == 1))
            .WithMessage("شرط الوزن وشرط عدد الطرود يُدخل كل منهما مرة واحدة على الأكثر.")
            .Must(list => list.Where(c => c.ReferenceId.HasValue).GroupBy(c => new { c.ConditionType, c.ReferenceId }).All(g => g.Count() == 1))
            .WithMessage("يوجد شرط مكرر.");
        RuleForEach(x => x.Conditions).Cascade(CascadeMode.Continue).SetValidator(new SavePriceRuleConditionRequestValidator());

        RuleFor(x => x.BasePrice).PriceAmount("السعر الأساسي");
        RuleFor(x => x.PricePerCubicMeter).PriceAmount("سعر المتر المكعب");
        RuleFor(x => x.AppliesToBasis).IsInEnum().WithMessage("طريقة التسعير غير صالحة.").When(x => x.AppliesToBasis.HasValue);
        RuleFor(x => x.IncludedWeightKg)
            .GreaterThanOrEqualTo(0m).WithMessage("الوزن المشمول لا يمكن أن يكون سالباً.")
            .LessThanOrEqualTo(10000m).WithMessage("الوزن المشمول كبير جداً.")
            .PrecisionScale(10, 3, true).WithMessage("الوزن يقبل 3 منازل عشرية على الأكثر.");

        RuleFor(x => x.WeightTiers)
            .Must(list => list.Count <= MaxTiers).WithMessage($"الحد الأقصى {MaxTiers} شريحة وزن.")
            .Must(TiersAreContiguous)
            .WithMessage("شرائح الوزن يجب أن تكون متتالية دون تداخل أو فراغ، ولا تبدأ قبل الوزن المشمول، والأخيرة فقط بلا نهاية.");
        RuleFor(x => x)
            .Must(x => x.WeightTiers.Count == 0 || x.WeightTiers.Min(t => t.FromWeightKg) <= x.IncludedWeightKg)
            .OverridePropertyName("weightTiers")
            .WithMessage("الشريحة الأولى يجب أن تبدأ من الوزن المشمول بالسعر الأساسي أو قبله.");
        RuleForEach(x => x.WeightTiers).Cascade(CascadeMode.Continue).SetValidator(new SavePriceRuleWeightTierRequestValidator());

        RuleFor(x => x.IncludedPackageCount)
            .InclusiveBetween(0, 1000).WithMessage("عدد الطرود المشمولة يجب أن يكون بين 0 و 1000.");
        RuleFor(x => x.PerPackageFee).PriceAmount("رسم الطرد الإضافي");

        RuleFor(x => x.CodFeeFixed).PriceAmount("رسوم التحصيل الثابتة");
        RuleFor(x => x.CodFeePercentage).Percentage("نسبة رسوم التحصيل");
        RuleFor(x => x.CodFeeMinimum).PriceAmount("الحد الأدنى لعمولة التحصيل");
        RuleFor(x => x.InsuranceFeePercentage).Percentage("نسبة رسوم التأمين");
        RuleFor(x => x.InsuranceFeeMinimum).PriceAmount("الحد الأدنى لرسوم التأمين");
        RuleFor(x => x.PickupFee).PriceAmount("رسم الاستلام من المرسل");
        RuleFor(x => x.PackagingFee).PriceAmount("رسم التغليف");
        RuleFor(x => x.RemoteAreaFee).PriceAmount("رسم المناطق النائية");
        RuleFor(x => x.ReturnFee).PriceAmount("رسم الإرجاع");
        RuleFor(x => x.DiscountPercentage).Percentage("نسبة خصم العقد");
        RuleFor(x => x.TaxRateId).ValidOptionalId("نسبة الضريبة غير صالحة.").When(x => x.TaxRateId.HasValue);
        RuleFor(x => x.Notes).OptionalText(500);
    }

    /// <summary>Sorted by start: each tier starts where the previous ends; only the last may be open-ended.</summary>
    private static bool TiersAreContiguous(SavePricingRuleRequest request, List<SavePriceRuleWeightTierRequest> tiers)
    {
        if (tiers.Count == 0)
        {
            return true;
        }

        var ordered = tiers.OrderBy(t => t.FromWeightKg).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var isLast = i == ordered.Count - 1;
            if (!isLast && ordered[i].ToWeightKg is null)
            {
                return false;
            }

            if (!isLast && ordered[i].ToWeightKg != ordered[i + 1].FromWeightKg)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Money and percentage rules of the pricing validators.</summary>
public static class PriceRules
{
    public static IRuleBuilderOptions<T, decimal> PriceAmount<T>(this IRuleBuilder<T, decimal> rule, string name)
    {
        return rule
            .GreaterThanOrEqualTo(0m).WithMessage($"{name} لا يمكن أن يكون سالباً.")
            .LessThanOrEqualTo(999_999_999_999m).WithMessage($"{name} كبير جداً.")
            .PrecisionScale(18, 2, true).WithMessage($"{name} يقبل منزلتين عشريتين على الأكثر.");
    }

    public static IRuleBuilderOptions<T, decimal> Percentage<T>(this IRuleBuilder<T, decimal> rule, string name)
    {
        return rule
            .InclusiveBetween(0m, 100m).WithMessage($"{name} يجب أن تكون بين 0 و 100.")
            .PrecisionScale(5, 2, true).WithMessage($"{name} تقبل منزلتين عشريتين على الأكثر.");
    }
}
