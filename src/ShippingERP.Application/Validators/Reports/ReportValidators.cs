using FluentValidation;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.Validators.Accounting;
using ShippingERP.Application.Validators.Common;

namespace ShippingERP.Application.Validators.Reports;

public sealed class DashboardQueryValidator : AbstractValidator<DashboardQuery>
{
    public DashboardQueryValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 365).WithMessage("عدد الأيام بين 1 و 365.");
        RuleFor(x => x.BranchId).GreaterThan(0).WithMessage("الفرع غير صالح.").When(x => x.BranchId.HasValue);
    }
}

public sealed class OperationalReportQueryValidator : AbstractValidator<OperationalReportQuery>
{
    public OperationalReportQueryValidator()
    {
        RuleFor(x => x.FromDate).ValidOptionalAccountingDate("تاريخ البداية");
        RuleFor(x => x.ToDate).ValidOptionalAccountingDate("تاريخ النهاية");
        RuleFor(x => x.ToDate)
            .Must((query, to) => to!.Value.Date >= query.FromDate!.Value.Date).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .Must((query, to) => (to!.Value.Date - query.FromDate!.Value.Date).TotalDays <= 366).WithMessage("الفترة لا تزيد عن سنة.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
        RuleFor(x => x.BranchId).GreaterThan(0).WithMessage("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.SenderCustomerId).GreaterThan(0).WithMessage("العميل غير صالح.").When(x => x.SenderCustomerId.HasValue);
        RuleFor(x => x.DriverId).GreaterThan(0).WithMessage("السائق غير صالح.").When(x => x.DriverId.HasValue);
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة الشحنة غير صالحة.").When(x => x.Status.HasValue);
    }
}

public sealed class ExportQueryValidator : AbstractValidator<ExportQuery>
{
    public ExportQueryValidator()
    {
        RuleFor(x => x.Format).IsInEnum().WithMessage("صيغة الملف: excel أو pdf.");
    }
}

public sealed class AuditLogListQueryValidator : AbstractValidator<AuditLogListQuery>
{
    public AuditLogListQueryValidator()
    {
        Include(new PagedQueryRules<AuditLogListQuery>(AuditLogListQuery.AllowedSortFields));

        RuleFor(x => x.Action).IsInEnum().WithMessage("نوع العملية غير صالح.").When(x => x.Action.HasValue);
        RuleFor(x => x.EntityName).MaximumLength(100).WithMessage("اسم الكيان طويل جداً.");
        RuleFor(x => x.EntityId).MaximumLength(64).WithMessage("رقم الكيان طويل جداً.");
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}
