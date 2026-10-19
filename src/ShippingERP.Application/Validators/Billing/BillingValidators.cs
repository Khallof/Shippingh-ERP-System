using FluentValidation;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.Validators.Common;
using ShippingERP.Application.Validators.Shipments;

namespace ShippingERP.Application.Validators.Billing;

internal static class InvoiceOptionRules
{
    public const int MaxPaymentTermsDays = 365;

    public static void Apply<T>(AbstractValidator<T> validator)
        where T : CreateInvoiceOptions
    {
        validator.RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        validator.RuleFor(x => x.PaymentTermsDays!.Value)
            .InclusiveBetween(0, MaxPaymentTermsDays).WithMessage($"مدة السداد يجب أن تكون بين 0 و {MaxPaymentTermsDays} يوماً.")
            .OverridePropertyName("paymentTermsDays")
            .When(x => x.PaymentTermsDays.HasValue);
        validator.RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class CreateInvoiceFromShipmentRequestValidator : AbstractValidator<CreateInvoiceFromShipmentRequest>
{
    public CreateInvoiceFromShipmentRequestValidator()
    {
        InvoiceOptionRules.Apply(this);
    }
}

public sealed class CreateConsolidatedInvoiceRequestValidator : AbstractValidator<CreateConsolidatedInvoiceRequest>
{
    public const int MaxShipments = 500;

    public CreateConsolidatedInvoiceRequestValidator()
    {
        InvoiceOptionRules.Apply(this);

        RuleFor(x => x.CustomerId).ValidId("العميل مطلوب.");
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);

        RuleFor(x => x.ShipmentIds)
            .Must(ids => ids.Count <= MaxShipments).WithMessage($"الحد الأقصى {MaxShipments} شحنة في الفاتورة الواحدة.")
            .Must(ids => ids.All(id => id > 0)).WithMessage("أرقام الشحنات غير صالحة.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("توجد شحنة مكررة في القائمة.");

        RuleFor(x => x.FromUtc)
            .NotNull().WithMessage("حدد الشحنات، أو حدد فترة (من - إلى) لاختيار كل الشحنات المؤهلة.")
            .When(x => x.ShipmentIds.Count == 0);
        RuleFor(x => x.ToUtc)
            .NotNull().WithMessage("حدد نهاية الفترة.")
            .When(x => x.ShipmentIds.Count == 0);
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

public sealed class UpdateDraftInvoiceRequestValidator : AbstractValidator<UpdateDraftInvoiceRequest>
{
    public UpdateDraftInvoiceRequestValidator()
    {
        RuleFor(x => x.PaymentTermsDays)
            .InclusiveBetween(0, InvoiceOptionRules.MaxPaymentTermsDays)
            .WithMessage($"مدة السداد يجب أن تكون بين 0 و {InvoiceOptionRules.MaxPaymentTermsDays} يوماً.");
        RuleFor(x => x.InvoiceDiscountAmount).Money("خصم الفاتورة");
        RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class IssueInvoiceRequestValidator : AbstractValidator<IssueInvoiceRequest>
{
    public IssueInvoiceRequestValidator()
    {
        // "Not in the future" is checked by the service with the server clock.
        RuleFor(x => x.InvoiceDateUtc)
            .GreaterThan(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)).WithMessage("تاريخ الفاتورة غير صالح.")
            .When(x => x.InvoiceDateUtc.HasValue);
    }
}

public sealed class CancelInvoiceRequestValidator : AbstractValidator<CancelInvoiceRequest>
{
    public CancelInvoiceRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("سبب الإلغاء مطلوب.")
            .MinimumLength(5).WithMessage("اكتب سبب الإلغاء بوضوح (5 أحرف على الأقل).")
            .MaximumLength(500).WithMessage("سبب الإلغاء طويل جداً.");
    }
}

public sealed class CreditNoteLineRequestValidator : AbstractValidator<CreditNoteLineRequest>
{
    public CreditNoteLineRequestValidator()
    {
        RuleFor(x => x.InvoiceLineId).ValidId("سطر الفاتورة مطلوب.");
        RuleFor(x => x.TaxableAmount!.Value)
            .GreaterThan(0m).WithMessage("مبلغ الإشعار يجب أن يكون أكبر من صفر.")
            .Money("مبلغ الإشعار")
            .OverridePropertyName("taxableAmount")
            .When(x => x.TaxableAmount.HasValue);
    }
}

public sealed class CreateCreditNoteRequestValidator : AbstractValidator<CreateCreditNoteRequest>
{
    public const int MaxLines = 500;

    public CreateCreditNoteRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("سبب الإشعار الدائن مطلوب.")
            .MinimumLength(5).WithMessage("اكتب سبب الإشعار الدائن بوضوح (5 أحرف على الأقل).")
            .MaximumLength(500).WithMessage("سبب الإشعار الدائن طويل جداً.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("حدد سطور الفاتورة المراد إشعارها، أو اختر إشعار الفاتورة بالكامل.")
            .When(x => !x.CreditAll);
        RuleFor(x => x.Lines)
            .Empty().WithMessage("لا ترسل سطوراً عند اختيار إشعار الفاتورة بالكامل.")
            .When(x => x.CreditAll);
        RuleFor(x => x.Lines)
            .Must(lines => lines.Count <= MaxLines).WithMessage($"الحد الأقصى {MaxLines} سطراً.")
            .Must(lines => lines.Select(l => l.InvoiceLineId).Distinct().Count() == lines.Count).WithMessage("يوجد سطر مكرر.");
        RuleForEach(x => x.Lines).Cascade(CascadeMode.Continue).SetValidator(new CreditNoteLineRequestValidator());
    }
}

public sealed class InvoiceListQueryValidator : AbstractValidator<InvoiceListQuery>
{
    public InvoiceListQueryValidator()
    {
        Include(new PagedQueryRules<InvoiceListQuery>(InvoiceListQuery.AllowedSortFields));
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة الفاتورة غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.CustomerId).ValidOptionalId("العميل غير صالح.").When(x => x.CustomerId.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.ShipmentId).ValidOptionalId("الشحنة غير صالحة.").When(x => x.ShipmentId.HasValue);
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

public sealed class EligibleShipmentsQueryValidator : AbstractValidator<EligibleShipmentsQuery>
{
    public EligibleShipmentsQueryValidator()
    {
        Include(new PagedQueryRules<EligibleShipmentsQuery>(EligibleShipmentsQuery.AllowedSortFields));
        RuleFor(x => x.CustomerId).ValidId("العميل مطلوب.");
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

// ------------------------------------------------------------------ payments

public sealed class PaymentAllocationRequestValidator : AbstractValidator<PaymentAllocationRequest>
{
    public PaymentAllocationRequestValidator()
    {
        RuleFor(x => x.InvoiceId).ValidId("الفاتورة مطلوبة.");
        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("مبلغ التخصيص يجب أن يكون أكبر من صفر.")
            .Money("مبلغ التخصيص");
    }
}

internal static class AllocationRules
{
    public const int MaxAllocations = 200;

    public static void Apply<T>(AbstractValidator<T> validator)
        where T : IPaymentAllocationRequest
    {
        validator.RuleFor(x => x.Allocations)
            .Must(list => list.Count <= MaxAllocations).WithMessage($"الحد الأقصى {MaxAllocations} فاتورة في الدفعة الواحدة.")
            .Must(list => list.Select(a => a.InvoiceId).Distinct().Count() == list.Count).WithMessage("توجد فاتورة مكررة في التخصيص.");
        validator.RuleFor(x => x.Allocations)
            .Empty().WithMessage("اختر التخصيص التلقائي أو حدد الفواتير، وليس الاثنين معاً.")
            .When(x => x.AutoAllocate);
        validator.RuleForEach(x => x.Allocations)
            .Cascade(CascadeMode.Continue)
            .SetValidator(new PaymentAllocationRequestValidator());
    }
}

public sealed class CreateCustomerPaymentRequestValidator : AbstractValidator<CreateCustomerPaymentRequest>
{
    public CreateCustomerPaymentRequestValidator()
    {
        RuleFor(x => x.CustomerId).ValidId("العميل مطلوب.");
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.PaymentMethodId).ValidId("طريقة الدفع مطلوبة.");
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("مبلغ الدفعة يجب أن يكون أكبر من صفر.")
            .Money("مبلغ الدفعة");
        RuleFor(x => x.ReferenceNumber).OptionalText(100);
        RuleFor(x => x.Notes).OptionalText(500);
        RuleFor(x => x)
            .Must(x => x.Allocations.Sum(a => a.Amount) <= x.Amount)
            .OverridePropertyName("allocations")
            .WithMessage("مجموع التخصيصات لا يمكن أن يزيد عن مبلغ الدفعة.");

        AllocationRules.Apply(this);
    }
}

public sealed class AllocatePaymentRequestValidator : AbstractValidator<AllocatePaymentRequest>
{
    public AllocatePaymentRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.AutoAllocate || x.Allocations.Count > 0)
            .OverridePropertyName("allocations")
            .WithMessage("حدد الفواتير، أو اختر التخصيص التلقائي.");

        AllocationRules.Apply(this);
    }
}

public sealed class CustomerPaymentListQueryValidator : AbstractValidator<CustomerPaymentListQuery>
{
    public CustomerPaymentListQueryValidator()
    {
        Include(new PagedQueryRules<CustomerPaymentListQuery>(CustomerPaymentListQuery.AllowedSortFields));
        RuleFor(x => x.CustomerId).ValidOptionalId("العميل غير صالح.").When(x => x.CustomerId.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.PaymentMethodId).ValidOptionalId("طريقة الدفع غير صالحة.").When(x => x.PaymentMethodId.HasValue);
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

// ------------------------------------------------------------------ reports

public sealed class CustomerStatementQueryValidator : AbstractValidator<CustomerStatementQuery>
{
    public CustomerStatementQueryValidator()
    {
        Include(new PagedQueryRules<CustomerStatementQuery>(CustomerStatementQuery.AllowedSortFields));
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

public sealed class CustomerAgingQueryValidator : AbstractValidator<CustomerAgingQuery>
{
    public CustomerAgingQueryValidator()
    {
        Include(new PagedQueryRules<CustomerAgingQuery>(CustomerAgingQuery.AllowedSortFields));
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CustomerId).ValidOptionalId("العميل غير صالح.").When(x => x.CustomerId.HasValue);
    }
}
