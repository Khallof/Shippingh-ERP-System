using FluentValidation;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Validators.Cod;
using ShippingERP.Application.Validators.Common;
using ShippingERP.Application.Validators.Shipments;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.Validators.Cash;

public sealed class CashboxListQueryValidator : AbstractValidator<CashboxListQuery>
{
    public CashboxListQueryValidator()
    {
        Include(new PagedQueryRules<CashboxListQuery>(CashboxListQuery.AllowedSortFields));
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
    }
}

public sealed class CreateCashboxRequestValidator : AbstractValidator<CreateCashboxRequest>
{
    public CreateCashboxRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(30);
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.BranchId).ValidId("الفرع مطلوب.");
        RuleFor(x => x.CurrencyId).ValidId("العملة مطلوبة.");
        RuleFor(x => x.CustodianUserId).ValidOptionalId("أمين الصندوق غير صالح.").When(x => x.CustodianUserId.HasValue);
        RuleFor(x => x.OpeningBalance).Money("الرصيد الافتتاحي");
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class UpdateCashboxRequestValidator : AbstractValidator<UpdateCashboxRequest>
{
    public UpdateCashboxRequestValidator()
    {
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.CustodianUserId).ValidOptionalId("أمين الصندوق غير صالح.").When(x => x.CustodianUserId.HasValue);
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class CashboxTransactionQueryValidator : AbstractValidator<CashboxTransactionQuery>
{
    public CashboxTransactionQueryValidator()
    {
        Include(new PagedQueryRules<CashboxTransactionQuery>(Array.Empty<string>()));
        RuleFor(x => x.TransactionType).IsInEnum().WithMessage("نوع الحركة غير صالح.").When(x => x.TransactionType.HasValue);
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
    }
}

public sealed class CloseCashboxDayRequestValidator : AbstractValidator<CloseCashboxDayRequest>
{
    public CloseCashboxDayRequestValidator()
    {
        RuleFor(x => x.BusinessDate).ValidOptionalDate("تاريخ اليوم غير صالح.");
        RuleFor(x => x.CountedBalance).Money("المبلغ المعدود");
        RuleFor(x => x.DiscrepancyReason).OptionalText(500);
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class DecideCashboxClosingRequestValidator : AbstractValidator<DecideCashboxClosingRequest>
{
    public DecideCashboxClosingRequestValidator()
    {
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class CashboxClosingQueryValidator : AbstractValidator<CashboxClosingQuery>
{
    public CashboxClosingQueryValidator()
    {
        Include(new PagedQueryRules<CashboxClosingQuery>(Array.Empty<string>()));
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة الإقفال غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
    }
}

public sealed class BankAccountListQueryValidator : AbstractValidator<BankAccountListQuery>
{
    public BankAccountListQueryValidator()
    {
        Include(new PagedQueryRules<BankAccountListQuery>(Array.Empty<string>()));
    }
}

public sealed class SaveBankAccountRequestValidator : AbstractValidator<SaveBankAccountRequest>
{
    public SaveBankAccountRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(30);
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.BankName).NotEmpty().WithMessage("اسم البنك مطلوب.").MaximumLength(150).WithMessage("اسم البنك طويل جداً.");
        RuleFor(x => x.AccountNumber)
            .NotEmpty().WithMessage("رقم الحساب مطلوب.")
            .MaximumLength(50).WithMessage("رقم الحساب طويل جداً.")
            .Matches("^[A-Za-z0-9 -]+$").WithMessage("رقم الحساب يقبل الحروف الإنجليزية والأرقام والمسافة و - فقط.");
        RuleFor(x => x.Iban)
            .Matches("^[A-Z]{2}[0-9]{2}[A-Z0-9]{10,30}$").WithMessage("رقم IBAN غير صالح (حرفان ثم رقمان ثم حتى 30 حرفاً/رقماً بلا مسافات).")
            .When(x => !string.IsNullOrWhiteSpace(x.Iban));
        RuleFor(x => x.CurrencyId).ValidId("العملة مطلوبة.");
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.Notes).OptionalText(500);
        RuleFor(x => x.AccountId).ValidOptionalId("حساب الأستاذ العام غير صالح.").When(x => x.AccountId.HasValue);
    }
}

public sealed class CreateCashReceiptRequestValidator : AbstractValidator<CreateCashReceiptRequest>
{
    public CreateCashReceiptRequestValidator()
    {
        RuleFor(x => x.CashboxId).ValidId("الصندوق مطلوب.");
        RuleFor(x => x.ReceiptType)
            .IsInEnum().WithMessage("نوع سند القبض غير صالح.")
            .Must(t => t is not (CashReceiptType.DriverHandover or CashReceiptType.CounterCodCollection))
            .WithMessage("سندات استلام السائقين والتحصيل في الفرع تُنشأ من شاشات الدفع عند الاستلام فقط.");
        RuleFor(x => x.Amount).PositiveMoney("المبلغ");
        RuleFor(x => x.ReceivedFrom).NotEmpty().WithMessage("اسم الدافع مطلوب.").MaximumLength(150).WithMessage("اسم الدافع طويل جداً.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("البيان مطلوب.").MaximumLength(500).WithMessage("البيان طويل جداً.");
        RuleFor(x => x.ReferenceNumber).OptionalText(100);
        RuleFor(x => x.CustomerPaymentId)
            .NotNull().WithMessage("حدد دفعة العميل النقدية (سند القبض PAY) المراد إيداعها في الصندوق.")
            .When(x => x.ReceiptType == CashReceiptType.CustomerPayment);
        RuleFor(x => x.CustomerPaymentId)
            .Null().WithMessage("دفعة العميل تخص نوع «دفعة عميل نقدية» فقط.")
            .When(x => x.ReceiptType != CashReceiptType.CustomerPayment);
        RuleFor(x => x.CustomerId).ValidOptionalId("العميل غير صالح.").When(x => x.CustomerId.HasValue);
    }
}

public sealed class CreateCashPaymentRequestValidator : AbstractValidator<CreateCashPaymentRequest>
{
    public CreateCashPaymentRequestValidator()
    {
        RuleFor(x => x.CashboxId).ValidId("الصندوق مطلوب.");
        RuleFor(x => x.PaymentType)
            .IsInEnum().WithMessage("نوع سند الصرف غير صالح.")
            .Must(t => t != CashPaymentType.SenderSettlement)
            .WithMessage("سندات صرف تسويات المرسلين تُنشأ عند دفع التسوية فقط.");
        RuleFor(x => x.Amount).PositiveMoney("المبلغ");
        RuleFor(x => x.PaidTo).NotEmpty().WithMessage("اسم المستلم مطلوب.").MaximumLength(150).WithMessage("اسم المستلم طويل جداً.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("البيان مطلوب.").MaximumLength(500).WithMessage("البيان طويل جداً.");
        RuleFor(x => x.ReferenceNumber).OptionalText(100);

        RuleFor(x => x.ExpenseCategoryId).NotNull().WithMessage("بند المصروف مطلوب.").When(x => x.PaymentType == CashPaymentType.Expense);
        RuleFor(x => x.SupplierId).NotNull().WithMessage("المورد مطلوب.").When(x => x.PaymentType == CashPaymentType.SupplierPayment);
        RuleFor(x => x.EmployeeId).NotNull().WithMessage("الموظف مطلوب.").When(x => x.PaymentType == CashPaymentType.EmployeeAdvance);
        RuleFor(x => x.CustomerId).NotNull().WithMessage("العميل مطلوب.").When(x => x.PaymentType == CashPaymentType.CustomerRefund);

        RuleFor(x => x.ExpenseCategoryId).ValidOptionalId("بند المصروف غير صالح.").When(x => x.ExpenseCategoryId.HasValue);
        RuleFor(x => x.SupplierId).ValidOptionalId("المورد غير صالح.").When(x => x.SupplierId.HasValue);
        RuleFor(x => x.EmployeeId).ValidOptionalId("الموظف غير صالح.").When(x => x.EmployeeId.HasValue);
        RuleFor(x => x.CustomerId).ValidOptionalId("العميل غير صالح.").When(x => x.CustomerId.HasValue);
    }
}

public sealed class CancelCashVoucherRequestValidator : AbstractValidator<CancelCashVoucherRequest>
{
    public CancelCashVoucherRequestValidator()
    {
        RuleFor(x => x.Reason).ClearReason("سبب الإلغاء");
    }
}

public sealed class CashVoucherListQueryValidator : AbstractValidator<CashVoucherListQuery>
{
    public CashVoucherListQueryValidator()
    {
        Include(new PagedQueryRules<CashVoucherListQuery>(CashVoucherListQuery.AllowedSortFields));
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة السند غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.Type)
            .Must(t => string.IsNullOrWhiteSpace(t) ||
                       Enum.TryParse<CashReceiptType>(t, true, out _) ||
                       Enum.TryParse<CashPaymentType>(t, true, out _))
            .WithMessage("نوع السند غير صالح.");
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

public sealed class CreateCashTransferRequestValidator : AbstractValidator<CreateCashTransferRequest>
{
    public CreateCashTransferRequestValidator()
    {
        RuleFor(x => x.FromCashboxId).ValidId("الصندوق المحوِّل مطلوب.");
        RuleFor(x => x)
            .Must(x => x.ToCashboxId.HasValue ^ x.ToBankAccountId.HasValue)
            .WithMessage("حدد صندوق الوجهة أو الحساب البنكي (واحداً فقط).")
            .OverridePropertyName("toCashboxId");
        RuleFor(x => x.ToCashboxId)
            .NotEqual(x => x.FromCashboxId).WithMessage("لا يمكن التحويل إلى الصندوق نفسه.")
            .When(x => x.ToCashboxId.HasValue);
        RuleFor(x => x.Amount).PositiveMoney("مبلغ التحويل");
        RuleFor(x => x.Reason).ClearReason("سبب التحويل");
        RuleFor(x => x.BankReference).OptionalText(100);
    }
}

public sealed class ApproveCashTransferRequestValidator : AbstractValidator<ApproveCashTransferRequest>
{
    public ApproveCashTransferRequestValidator()
    {
        RuleFor(x => x.BankReference).OptionalText(100);
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class CashTransferReasonRequestValidator : AbstractValidator<CashTransferReasonRequest>
{
    public CashTransferReasonRequestValidator()
    {
        RuleFor(x => x.Reason).ClearReason();
    }
}

public sealed class CashTransferListQueryValidator : AbstractValidator<CashTransferListQuery>
{
    public CashTransferListQueryValidator()
    {
        Include(new PagedQueryRules<CashTransferListQuery>(CashTransferListQuery.AllowedSortFields));
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة التحويل غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.TransferType).IsInEnum().WithMessage("نوع التحويل غير صالح.").When(x => x.TransferType.HasValue);
    }
}

public sealed class DailyCashMovementQueryValidator : AbstractValidator<DailyCashMovementQuery>
{
    public const int MaxDays = 62;

    public DailyCashMovementQueryValidator()
    {
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
        RuleFor(x => x)
            .Must(x => x.FromDate is null || x.ToDate is null || (x.ToDate.Value.Date - x.FromDate.Value.Date).TotalDays < MaxDays)
            .WithMessage($"الفترة القصوى {MaxDays} يوماً.")
            .OverridePropertyName("toDate");
        RuleFor(x => x.CashboxId).ValidOptionalId("الصندوق غير صالح.").When(x => x.CashboxId.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
    }
}
