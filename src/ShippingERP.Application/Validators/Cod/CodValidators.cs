using FluentValidation;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Validators.Common;
using ShippingERP.Application.Validators.Shipments;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.Validators.Cod;

/// <summary>Rules shared by the COD and cash validators (step 6).</summary>
public static class CashRules
{
    public const int MaxDocumentsPerRequest = 500;

    /// <summary>A money amount that must be greater than zero (max 2 decimals).</summary>
    public static IRuleBuilderOptions<T, decimal> PositiveMoney<T>(this IRuleBuilder<T, decimal> rule, string name)
    {
        return rule
            .GreaterThan(0m).WithMessage($"{name} يجب أن يكون أكبر من صفر.")
            .Money(name);
    }

    /// <summary>A clear reason: required, 5 to 500 characters.</summary>
    public static IRuleBuilderOptions<T, string> ClearReason<T>(this IRuleBuilder<T, string> rule, string what = "السبب")
    {
        return rule
            .NotEmpty().WithMessage($"{what} مطلوب.")
            .MinimumLength(5).WithMessage($"اكتب {what} بوضوح (5 أحرف على الأقل).")
            .MaximumLength(500).WithMessage($"{what} طويل جداً (500 حرف على الأكثر).");
    }

    public static IRuleBuilderOptions<T, List<long>> ValidIdList<T>(this IRuleBuilder<T, List<long>> rule, string what)
    {
        return rule
            .Must(ids => ids.Count <= MaxDocumentsPerRequest).WithMessage($"الحد الأقصى {MaxDocumentsPerRequest} {what} في الطلب الواحد.")
            .Must(ids => ids.All(id => id > 0)).WithMessage($"أرقام {what} غير صالحة.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage($"يوجد تكرار في {what}.");
    }

    public static IRuleBuilderOptions<T, DateTime?> ValidOptionalDate<T>(this IRuleBuilder<T, DateTime?> rule, string message)
    {
        return rule.Must(value => value is null || value.Value > new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)).WithMessage(message);
    }
}

public sealed class CodTransactionListQueryValidator : AbstractValidator<CodTransactionListQuery>
{
    public CodTransactionListQueryValidator()
    {
        Include(new PagedQueryRules<CodTransactionListQuery>(CodTransactionListQuery.AllowedSortFields));
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة التحصيل غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.SenderCustomerId).ValidOptionalId("المرسل غير صالح.").When(x => x.SenderCustomerId.HasValue);
        RuleFor(x => x.DriverId).ValidOptionalId("السائق غير صالح.").When(x => x.DriverId.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.CashboxId).ValidOptionalId("الصندوق غير صالح.").When(x => x.CashboxId.HasValue);
        RuleFor(x => x.CollectedToUtc)
            .GreaterThan(x => x.CollectedFromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.CollectedFromUtc.HasValue && x.CollectedToUtc.HasValue);
    }
}

public sealed class CollectCodRequestValidator : AbstractValidator<CollectCodRequest>
{
    public CollectCodRequestValidator()
    {
        RuleFor(x => x.ShipmentId)
            .NotNull().WithMessage("حدد الشحنة برقمها أو برقم التتبع.")
            .When(x => string.IsNullOrWhiteSpace(x.TrackingNumber));
        RuleFor(x => x.ShipmentId).ValidOptionalId("الشحنة غير صالحة.").When(x => x.ShipmentId.HasValue);
        RuleFor(x => x.TrackingNumber).MaximumLength(30).WithMessage("رقم التتبع طويل جداً.");
        RuleFor(x => x.Amount).PositiveMoney("المبلغ المحصّل");
        RuleFor(x => x.CollectedAtUtc).ValidOptionalDate("وقت التحصيل غير صالح.");
        RuleFor(x => x.DriverId).ValidOptionalId("السائق غير صالح.").When(x => x.DriverId.HasValue);
        RuleFor(x => x.CashboxId).ValidOptionalId("الصندوق غير صالح.").When(x => x.CashboxId.HasValue);
        RuleFor(x => x.DriverId)
            .Null().WithMessage("التحصيل في الفرع (صندوق) لا يحتاج سائقاً. أرسل السائق أو الصندوق، وليس كليهما.")
            .When(x => x.CashboxId.HasValue);
        RuleFor(x => x.DifferenceReason).OptionalText(500);
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class DriverHandoverRequestValidator : AbstractValidator<DriverHandoverRequest>
{
    public DriverHandoverRequestValidator()
    {
        RuleFor(x => x.DriverId).ValidId("السائق مطلوب.");
        RuleFor(x => x.CashboxId).ValidId("الصندوق مطلوب.");
        RuleFor(x => x.Amount).PositiveMoney("المبلغ المسلّم");
        RuleFor(x => x.CodTransactionIds).ValidIdList("شحنات التحصيل");
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class MarkCodReadyRequestValidator : AbstractValidator<MarkCodReadyRequest>
{
    public MarkCodReadyRequestValidator()
    {
        RuleFor(x => x.CodTransactionIds).ValidIdList("شحنات التحصيل");
        RuleFor(x => x.CodTransactionIds)
            .NotEmpty().WithMessage("حدد شحنات التحصيل، أو حدد المرسل لاختيار كل شحناته المستلمة.")
            .When(x => x.SenderCustomerId is null);
        RuleFor(x => x.SenderCustomerId).ValidOptionalId("المرسل غير صالح.").When(x => x.SenderCustomerId.HasValue);
    }
}

public sealed class OpenCodDisputeRequestValidator : AbstractValidator<OpenCodDisputeRequest>
{
    public OpenCodDisputeRequestValidator()
    {
        RuleFor(x => x.Reason).ClearReason("سبب النزاع");
    }
}

public sealed class ResolveCodDisputeRequestValidator : AbstractValidator<ResolveCodDisputeRequest>
{
    public ResolveCodDisputeRequestValidator()
    {
        RuleFor(x => x.Notes).ClearReason("ملاحظات الحل");
    }
}

public sealed class CreateCodAdjustmentRequestValidator : AbstractValidator<CreateCodAdjustmentRequest>
{
    public CreateCodAdjustmentRequestValidator()
    {
        RuleFor(x => x.AdjustmentType).IsInEnum().WithMessage("نوع التسوية غير صالح.");
        RuleFor(x => x.Reason).ClearReason("سبب التسوية");
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);

        // Driver adjustments.
        When(x => x.AdjustmentType is CODAdjustmentType.DriverShortageWriteOff or CODAdjustmentType.DriverExcessCash, () =>
        {
            RuleFor(x => x.DriverId).NotNull().WithMessage("السائق مطلوب لهذا النوع.");
            RuleFor(x => x.DriverId).ValidOptionalId("السائق غير صالح.").When(x => x.DriverId.HasValue);
            RuleFor(x => x.CodTransactionId).Null().WithMessage("هذا النوع لا يرتبط بشحنة تحصيل.");
            RuleFor(x => x.Amount).NotNull().WithMessage("المبلغ مطلوب.");
            RuleFor(x => x.Amount!.Value).PositiveMoney("المبلغ").OverridePropertyName("amount").When(x => x.Amount.HasValue);
        });

        // COD adjustments.
        When(x => x.AdjustmentType is CODAdjustmentType.CollectionCorrection or CODAdjustmentType.CollectionReversal
                      or CODAdjustmentType.SenderFee or CODAdjustmentType.SenderCompensation, () =>
        {
            RuleFor(x => x.CodTransactionId).NotNull().WithMessage("شحنة التحصيل مطلوبة لهذا النوع.");
            RuleFor(x => x.CodTransactionId).ValidOptionalId("شحنة التحصيل غير صالحة.").When(x => x.CodTransactionId.HasValue);
            RuleFor(x => x.DriverId).Null().WithMessage("السائق يُحدد تلقائياً من شحنة التحصيل.");
        });

        When(x => x.AdjustmentType == CODAdjustmentType.CollectionCorrection, () =>
        {
            RuleFor(x => x.NewCollectedAmount).NotNull().WithMessage("المبلغ المحصّل الصحيح مطلوب.");
            RuleFor(x => x.NewCollectedAmount!.Value).PositiveMoney("المبلغ المحصّل الصحيح")
                .OverridePropertyName("newCollectedAmount").When(x => x.NewCollectedAmount.HasValue);
            RuleFor(x => x.Amount).Null().WithMessage("أرسل المبلغ المحصّل الصحيح فقط (newCollectedAmount).");
        });

        When(x => x.AdjustmentType == CODAdjustmentType.CollectionReversal, () =>
        {
            RuleFor(x => x.Amount).Null().WithMessage("إلغاء التحصيل يلغي المبلغ كاملاً؛ لا ترسل مبلغاً.");
            RuleFor(x => x.NewCollectedAmount).Null().WithMessage("إلغاء التحصيل يلغي المبلغ كاملاً؛ لا ترسل مبلغاً.");
        });

        When(x => x.AdjustmentType is CODAdjustmentType.SenderFee or CODAdjustmentType.SenderCompensation, () =>
        {
            RuleFor(x => x.Amount).NotNull().WithMessage("المبلغ مطلوب.");
            RuleFor(x => x.Amount!.Value).PositiveMoney("المبلغ").OverridePropertyName("amount").When(x => x.Amount.HasValue);
            RuleFor(x => x.NewCollectedAmount).Null().WithMessage("هذا الحقل لتصحيح التحصيل فقط.");
        });
    }
}

public sealed class CodAdjustmentListQueryValidator : AbstractValidator<CodAdjustmentListQuery>
{
    public CodAdjustmentListQueryValidator()
    {
        Include(new PagedQueryRules<CodAdjustmentListQuery>(CodAdjustmentListQuery.AllowedSortFields));
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة التسوية غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.AdjustmentType).IsInEnum().WithMessage("نوع التسوية غير صالح.").When(x => x.AdjustmentType.HasValue);
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

public sealed class DriverBalanceQueryValidator : AbstractValidator<DriverBalanceQuery>
{
    public DriverBalanceQueryValidator()
    {
        Include(new PagedQueryRules<DriverBalanceQuery>(DriverBalanceQuery.AllowedSortFields));
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.DriverId).ValidOptionalId("السائق غير صالح.").When(x => x.DriverId.HasValue);
    }
}

public sealed class DriverMovementQueryValidator : AbstractValidator<DriverMovementQuery>
{
    public DriverMovementQueryValidator()
    {
        Include(new PagedQueryRules<DriverMovementQuery>(Array.Empty<string>()));
        RuleFor(x => x.MovementType).IsInEnum().WithMessage("نوع الحركة غير صالح.").When(x => x.MovementType.HasValue);
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

public sealed class SenderBalanceQueryValidator : AbstractValidator<SenderBalanceQuery>
{
    public SenderBalanceQueryValidator()
    {
        Include(new PagedQueryRules<SenderBalanceQuery>(SenderBalanceQuery.AllowedSortFields));
        RuleFor(x => x.SenderCustomerId).ValidOptionalId("المرسل غير صالح.").When(x => x.SenderCustomerId.HasValue);
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
    }
}

public sealed class CodAgingQueryValidator : AbstractValidator<CodAgingQuery>
{
    public CodAgingQueryValidator()
    {
        RuleFor(x => x.AsOfUtc).ValidOptionalDate("التاريخ غير صالح.");
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.SenderCustomerId).ValidOptionalId("المرسل غير صالح.").When(x => x.SenderCustomerId.HasValue);
    }
}

public sealed class DriverCollectionReportQueryValidator : AbstractValidator<DriverCollectionReportQuery>
{
    public DriverCollectionReportQueryValidator()
    {
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
        RuleFor(x => x)
            .Must(x => x.FromUtc is null || x.ToUtc is null || (x.ToUtc.Value - x.FromUtc.Value).TotalDays <= 366)
            .WithMessage("الفترة القصوى سنة واحدة.")
            .OverridePropertyName("toUtc");
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.DriverId).ValidOptionalId("السائق غير صالح.").When(x => x.DriverId.HasValue);
    }
}

// ============================================================ settlements

public sealed class SettlementLineRequestValidator : AbstractValidator<SettlementLineRequest>
{
    public SettlementLineRequestValidator()
    {
        RuleFor(x => x.CodTransactionId).ValidId("شحنة التحصيل مطلوبة.");
        RuleFor(x => x.Amount!.Value).Money("المبلغ").OverridePropertyName("amount").When(x => x.Amount.HasValue && x.Amount.Value >= 0m);
        RuleFor(x => x.Amount).Must(a => a is null || a.Value != 0m).WithMessage("المبلغ لا يمكن أن يكون صفراً.");
    }
}

public sealed class CreateSenderSettlementRequestValidator : AbstractValidator<CreateSenderSettlementRequest>
{
    public CreateSenderSettlementRequestValidator()
    {
        RuleFor(x => x.SenderCustomerId).ValidId("المرسل مطلوب.");
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CodTransactionIds).ValidIdList("شحنات التحصيل");
        RuleFor(x => x.Lines)
            .Must(lines => lines.Count <= CashRules.MaxDocumentsPerRequest).WithMessage($"الحد الأقصى {CashRules.MaxDocumentsPerRequest} سطراً.")
            .Must(lines => lines.Select(l => l.CodTransactionId).Distinct().Count() == lines.Count).WithMessage("توجد شحنة مكررة في السطور.");
        RuleFor(x => x.Lines)
            .Empty().WithMessage("أرسل السطور (lines) أو أرقام شحنات التحصيل (codTransactionIds)، وليس كليهما.")
            .When(x => x.CodTransactionIds.Count > 0);
        RuleForEach(x => x.Lines).SetValidator(new SettlementLineRequestValidator());
        RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class ApproveSenderSettlementRequestValidator : AbstractValidator<ApproveSenderSettlementRequest>
{
    public ApproveSenderSettlementRequestValidator()
    {
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class PaySenderSettlementRequestValidator : AbstractValidator<PaySenderSettlementRequest>
{
    public PaySenderSettlementRequestValidator()
    {
        RuleFor(x => x.PaymentMethodId).ValidId("طريقة الدفع مطلوبة.");
        RuleFor(x => x.CashboxId).ValidOptionalId("الصندوق غير صالح.").When(x => x.CashboxId.HasValue);
        RuleFor(x => x.BankAccountId).ValidOptionalId("الحساب البنكي غير صالح.").When(x => x.BankAccountId.HasValue);
        RuleFor(x => x)
            .Must(x => !(x.CashboxId.HasValue && x.BankAccountId.HasValue))
            .WithMessage("حدد الصندوق (دفع نقدي) أو الحساب البنكي (تحويل)، وليس كليهما.")
            .OverridePropertyName("cashboxId");
        RuleFor(x => x.ReferenceNumber).OptionalText(100);
        RuleFor(x => x.PaidAtUtc).ValidOptionalDate("تاريخ الدفع غير صالح.");
    }
}

public sealed class SettlementReasonRequestValidator : AbstractValidator<SettlementReasonRequest>
{
    public SettlementReasonRequestValidator()
    {
        RuleFor(x => x.Reason).ClearReason();
    }
}

public sealed class SenderSettlementListQueryValidator : AbstractValidator<SenderSettlementListQuery>
{
    public SenderSettlementListQueryValidator()
    {
        Include(new PagedQueryRules<SenderSettlementListQuery>(SenderSettlementListQuery.AllowedSortFields));
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة التسوية غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.SenderCustomerId).ValidOptionalId("المرسل غير صالح.").When(x => x.SenderCustomerId.HasValue);
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

public sealed class SettlementCandidatesQueryValidator : AbstractValidator<SettlementCandidatesQuery>
{
    public SettlementCandidatesQueryValidator()
    {
        RuleFor(x => x.SenderCustomerId).ValidId("المرسل مطلوب.");
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
    }
}

public sealed class SenderSettlementReportQueryValidator : AbstractValidator<SenderSettlementReportQuery>
{
    public SenderSettlementReportQueryValidator()
    {
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
        RuleFor(x => x)
            .Must(x => x.FromUtc is null || x.ToUtc is null || (x.ToUtc.Value - x.FromUtc.Value).TotalDays <= 366)
            .WithMessage("الفترة القصوى سنة واحدة.")
            .OverridePropertyName("toUtc");
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة التسوية غير صالحة.").When(x => x.Status.HasValue);
    }
}
