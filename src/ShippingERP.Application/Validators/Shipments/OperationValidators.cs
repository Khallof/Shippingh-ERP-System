using FluentValidation;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Validators.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.Validators.Shipments;

/// <summary>Shared rules for the barcode / code lists of manifests and transfers.</summary>
internal static class CodeListRules
{
    public const int MaxCodes = 200;

    public static void Apply<T>(AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, List<string>>> codes, bool required)
    {
        var rule = validator.RuleFor(codes);
        if (required)
        {
            rule.NotEmpty().WithMessage("أدخل رقم تتبع أو رقم شحنة واحداً على الأقل.");
        }

        rule.Must(list => list.Count <= MaxCodes).WithMessage($"الحد الأقصى {MaxCodes} رقماً في المرة الواحدة.");
        rule.Must(list => list.All(code => !string.IsNullOrWhiteSpace(code))).WithMessage("رقم التتبع لا يمكن أن يكون فارغاً.");
        rule.Must(list => list.All(code => code is null || code.Trim().Length <= 40)).WithMessage("رقم التتبع طويل جداً.");
    }
}

/// <summary>Paging, sort (createdAtUtc, number) and date range of document lists.</summary>
public sealed class DocumentListQueryRules<T> : AbstractValidator<T>
    where T : DocumentListQuery
{
    public DocumentListQueryRules()
    {
        Include(new PagedQueryRules<T>(DocumentListQuery.AllowedSortFields));
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

public sealed class ShipmentCodesRequestValidator : AbstractValidator<ShipmentCodesRequest>
{
    public ShipmentCodesRequestValidator()
    {
        CodeListRules.Apply(this, x => x.Codes, required: true);
    }
}

// ---------------- warehouse manifests

public sealed class CreateWarehouseManifestRequestValidator : AbstractValidator<CreateWarehouseManifestRequest>
{
    public CreateWarehouseManifestRequestValidator()
    {
        RuleFor(x => x.MovementType).IsInEnum().WithMessage("نوع الحركة يجب أن يكون CheckIn أو CheckOut.");
        RuleFor(x => x.WarehouseId).ValidId("المستودع مطلوب.");
        RuleFor(x => x.Notes).OptionalText(500);
        CodeListRules.Apply(this, x => x.Codes, required: false);
    }
}

public sealed class WarehouseManifestListQueryValidator : AbstractValidator<WarehouseManifestListQuery>
{
    public WarehouseManifestListQueryValidator()
    {
        Include(new DocumentListQueryRules<WarehouseManifestListQuery>());
    }
}

// ---------------- branch transfers

public sealed class CreateBranchTransferRequestValidator : AbstractValidator<CreateBranchTransferRequest>
{
    public CreateBranchTransferRequestValidator()
    {
        RuleFor(x => x.FromBranchId).ValidId("فرع الإرسال مطلوب.");
        RuleFor(x => x.ToBranchId)
            .ValidId("فرع الاستلام مطلوب.")
            .NotEqual(x => x.FromBranchId).WithMessage("فرع الاستلام يجب أن يختلف عن فرع الإرسال.");
        RuleFor(x => x.VehicleId).ValidOptionalId("المركبة غير صالحة.").When(x => x.VehicleId.HasValue);
        RuleFor(x => x.DriverId).ValidOptionalId("السائق غير صالح.").When(x => x.DriverId.HasValue);
        RuleFor(x => x.SealNumber).OptionalText(50);
        RuleFor(x => x.Notes).OptionalText(500);
        CodeListRules.Apply(this, x => x.Codes, required: false);
    }
}

public sealed class DispatchBranchTransferRequestValidator : AbstractValidator<DispatchBranchTransferRequest>
{
    public DispatchBranchTransferRequestValidator()
    {
        RuleFor(x => x.VehicleId).ValidOptionalId("المركبة غير صالحة.").When(x => x.VehicleId.HasValue);
        RuleFor(x => x.DriverId).ValidOptionalId("السائق غير صالح.").When(x => x.DriverId.HasValue);
        RuleFor(x => x.SealNumber).OptionalText(50);
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class ReceiveBranchTransferRequestValidator : AbstractValidator<ReceiveBranchTransferRequest>
{
    public ReceiveBranchTransferRequestValidator()
    {
        RuleFor(x => x.ReceivedCodes)
            .Must(list => list.Count <= 1000).WithMessage("الحد الأقصى 1000 رقم.");
        RuleForEach(x => x.ReceivedCodes)
            .NotEmpty().WithMessage("رقم التتبع لا يمكن أن يكون فارغاً.")
            .MaximumLength(40).WithMessage("رقم التتبع طويل جداً.");
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class BranchTransferListQueryValidator : AbstractValidator<BranchTransferListQuery>
{
    public BranchTransferListQueryValidator()
    {
        Include(new DocumentListQueryRules<BranchTransferListQuery>());
    }
}

// ---------------- driver manifests

public sealed class CreateDriverManifestRequestValidator : AbstractValidator<CreateDriverManifestRequest>
{
    public CreateDriverManifestRequestValidator()
    {
        RuleFor(x => x.DriverId).ValidId("السائق مطلوب.");
        RuleFor(x => x.VehicleId).ValidOptionalId("المركبة غير صالحة.").When(x => x.VehicleId.HasValue);
        RuleFor(x => x.Notes).OptionalText(500);
        CodeListRules.Apply(this, x => x.Codes, required: false);
    }
}

public sealed class CloseDriverManifestRequestValidator : AbstractValidator<CloseDriverManifestRequest>
{
    public CloseDriverManifestRequestValidator()
    {
        RuleFor(x => x.PendingReason).OptionalText(300);
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class DriverManifestListQueryValidator : AbstractValidator<DriverManifestListQuery>
{
    public DriverManifestListQueryValidator()
    {
        Include(new DocumentListQueryRules<DriverManifestListQuery>());
    }
}

// ---------------- returns

public sealed class CreateShipmentReturnRequestValidator : AbstractValidator<CreateShipmentReturnRequest>
{
    public CreateShipmentReturnRequestValidator()
    {
        RuleFor(x => x.ShipmentId).ValidId("الشحنة مطلوبة.");
        RuleFor(x => x.ReturnType).IsInEnum().WithMessage("نوع المرتجع غير صالح.");
        RuleFor(x => x.Reason).RequiredReason();
        RuleFor(x => x.ReturnBranchId).ValidOptionalId("فرع الإرجاع غير صالح.").When(x => x.ReturnBranchId.HasValue);
        RuleFor(x => x.ReturnCharge).Money("رسوم الإرجاع");
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class ReceiveShipmentReturnRequestValidator : AbstractValidator<ReceiveShipmentReturnRequest>
{
    public ReceiveShipmentReturnRequestValidator()
    {
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class CompleteShipmentReturnRequestValidator : AbstractValidator<CompleteShipmentReturnRequest>
{
    public CompleteShipmentReturnRequestValidator()
    {
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class CancelShipmentReturnRequestValidator : AbstractValidator<CancelShipmentReturnRequest>
{
    public CancelShipmentReturnRequestValidator()
    {
        RuleFor(x => x.Reason).RequiredReason();
    }
}

public sealed class ShipmentReturnListQueryValidator : AbstractValidator<ShipmentReturnListQuery>
{
    public ShipmentReturnListQueryValidator()
    {
        Include(new DocumentListQueryRules<ShipmentReturnListQuery>());
    }
}

// ---------------- damage / loss cases

public sealed class CreateDamageCaseRequestValidator : AbstractValidator<CreateDamageCaseRequest>
{
    public CreateDamageCaseRequestValidator()
    {
        RuleFor(x => x.ShipmentId).ValidId("الشحنة مطلوبة.");
        RuleFor(x => x.CaseType).IsInEnum().WithMessage("نوع الحالة غير صالح.");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("وصف الحالة مطلوب.")
            .MaximumLength(1000).WithMessage("الوصف طويل جداً.");
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.EstimatedLossAmount).Money("قيمة الضرر التقديرية");
    }
}

public sealed class InvestigateDamageCaseRequestValidator : AbstractValidator<InvestigateDamageCaseRequest>
{
    public InvestigateDamageCaseRequestValidator()
    {
        RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class ResolveDamageCaseRequestValidator : AbstractValidator<ResolveDamageCaseRequest>
{
    public ResolveDamageCaseRequestValidator()
    {
        RuleFor(x => x.Resolution).IsInEnum().WithMessage("نتيجة الحالة غير صالحة.");
        RuleFor(x => x.CompensationAmount)
            .NotNull().WithMessage("مبلغ التعويض مطلوب.")
            .GreaterThan(0m).WithMessage("مبلغ التعويض يجب أن يكون أكبر من صفر.")
            .When(x => x.Resolution == DamageCaseResolution.Compensated);
        RuleFor(x => x.CompensationAmount)
            .Null().WithMessage("مبلغ التعويض يُدخل فقط عند اختيار Compensated.")
            .When(x => x.Resolution != DamageCaseResolution.Compensated);
        RuleFor(x => x.CompensationAmount!.Value)
            .Money("مبلغ التعويض")
            .OverridePropertyName("compensationAmount")
            .When(x => x.CompensationAmount.HasValue);
        RuleFor(x => x.Notes)
            .NotEmpty().WithMessage("ملاحظات الإغلاق مطلوبة.")
            .MaximumLength(1000).WithMessage("الملاحظات طويلة جداً.");
        RuleFor(x => x.FoundAtBranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.FoundAtBranchId.HasValue);
    }
}

public sealed class DamageCaseListQueryValidator : AbstractValidator<DamageCaseListQuery>
{
    public DamageCaseListQueryValidator()
    {
        Include(new DocumentListQueryRules<DamageCaseListQuery>());
    }
}
