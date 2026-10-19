using FluentValidation;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Application.Validators.Cod;
using ShippingERP.Application.Validators.Common;
using ShippingERP.Application.Validators.Shipments;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.Validators.Accounting;

/// <summary>Rules shared by the accounting validators.</summary>
public static class AccountingRules
{
    /// <summary>Account numbers: digits, optionally separated by "." or "-" ("1110", "1-1-10").</summary>
    public const string AccountCodePattern = "^[0-9][0-9.-]*$";

    public static readonly DateTime MinDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime MaxDate = new(2100, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    public static IRuleBuilderOptions<T, string> AccountCode<T>(this IRuleBuilder<T, string> rule)
    {
        return rule
            .NotEmpty().WithMessage("رقم الحساب مطلوب.")
            .MaximumLength(20).WithMessage("رقم الحساب طويل جداً (20 خانة على الأكثر).")
            .Matches(AccountCodePattern).WithMessage("رقم الحساب يقبل الأرقام و . و - فقط ويبدأ برقم.");
    }

    public static IRuleBuilderOptions<T, DateTime> ValidAccountingDate<T>(this IRuleBuilder<T, DateTime> rule, string name)
    {
        return rule
            .Must(value => value >= MinDate && value <= MaxDate).WithMessage($"{name} غير صالح.");
    }

    public static IRuleBuilderOptions<T, DateTime?> ValidOptionalAccountingDate<T>(this IRuleBuilder<T, DateTime?> rule, string name)
    {
        return rule
            .Must(value => value is null || (value.Value >= MinDate && value.Value <= MaxDate)).WithMessage($"{name} غير صالح.");
    }
}

// ------------------------------------------------------------------ chart of accounts

public sealed class AccountListQueryValidator : AbstractValidator<AccountListQuery>
{
    public AccountListQueryValidator()
    {
        Include(new PagedQueryRules<AccountListQuery>(AccountListQuery.AllowedSortFields));
        RuleFor(x => x.AccountType).IsInEnum().WithMessage("نوع الحساب غير صالح.").When(x => x.AccountType.HasValue);
        RuleFor(x => x.AccountCategoryId).ValidOptionalId("التصنيف غير صالح.").When(x => x.AccountCategoryId.HasValue);
        RuleFor(x => x.ParentAccountId).ValidOptionalId("الحساب الأب غير صالح.").When(x => x.ParentAccountId.HasValue);
    }
}

public sealed class AccountTreeQueryValidator : AbstractValidator<AccountTreeQuery>
{
    public AccountTreeQueryValidator()
    {
        RuleFor(x => x.AsOfDate).ValidOptionalAccountingDate("تاريخ الرصيد");
    }
}

public sealed class SaveAccountRequestValidator : AbstractValidator<SaveAccountRequest>
{
    public SaveAccountRequestValidator()
    {
        RuleFor(x => x.Code).AccountCode();
        RuleFor(x => x.ArabicName).ArabicName(200);
        RuleFor(x => x.EnglishName).OptionalText(200);
        RuleFor(x => x.AccountType).IsInEnum().WithMessage("نوع الحساب غير صالح.");
        RuleFor(x => x.AccountCategoryId).ValidOptionalId("التصنيف غير صالح.").When(x => x.AccountCategoryId.HasValue);
        RuleFor(x => x.ParentAccountId).ValidOptionalId("الحساب الأب غير صالح.").When(x => x.ParentAccountId.HasValue);
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.Description).OptionalText(500);
        RuleFor(x => x.IsCashAccount)
            .Must((request, isCash) => !isCash || request.AccountType == AccountType.Assets)
            .WithMessage("حساب النقدية يجب أن يكون من نوع الأصول.");
        RuleFor(x => x.RequiresCostCenter)
            .Must((request, requires) => !requires || request.IsPostable)
            .WithMessage("الحساب الرئيسي لا يُستخدم في القيود فلا يحتاج مركز تكلفة.");
    }
}

public sealed class SaveAccountCategoryRequestValidator : AbstractValidator<SaveAccountCategoryRequest>
{
    public SaveAccountCategoryRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(30);
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.AccountType).IsInEnum().WithMessage("نوع الحساب غير صالح.");
        RuleFor(x => x.CashFlowActivity).IsInEnum().WithMessage("نشاط التدفق النقدي غير صالح.");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000).WithMessage("ترتيب العرض بين 0 و 10000.");
    }
}

public sealed class AccountingDimensionListQueryValidator : AbstractValidator<AccountingDimensionListQuery>
{
    public AccountingDimensionListQueryValidator()
    {
        Include(new PagedQueryRules<AccountingDimensionListQuery>(AccountingDimensionListQuery.AllowedSortFields));
        RuleFor(x => x.DimensionType).IsInEnum().WithMessage("نوع البعد غير صالح.").When(x => x.DimensionType.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
    }
}

public sealed class SaveAccountingDimensionRequestValidator : AbstractValidator<SaveAccountingDimensionRequest>
{
    public SaveAccountingDimensionRequestValidator()
    {
        RuleFor(x => x.DimensionType).IsInEnum().WithMessage("نوع البعد غير صالح.");
        RuleFor(x => x.Code).ValidCode(30);
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.ParentId).ValidOptionalId("البعد الأب غير صالح.").When(x => x.ParentId.HasValue);
        RuleFor(x => x.Description).OptionalText(500);
    }
}

// ------------------------------------------------------------------ fiscal years and periods

public sealed class CreateFiscalYearRequestValidator : AbstractValidator<CreateFiscalYearRequest>
{
    public CreateFiscalYearRequestValidator()
    {
        RuleFor(x => x.Code!).ValidCode(20).When(x => !string.IsNullOrWhiteSpace(x.Code));
        RuleFor(x => x.ArabicName).OptionalText(150);
        RuleFor(x => x.StartDate).ValidAccountingDate("تاريخ بداية السنة");
        RuleFor(x => x.EndDate).ValidOptionalAccountingDate("تاريخ نهاية السنة");
        RuleFor(x => x.EndDate)
            .Must((request, end) => end!.Value.Date > request.StartDate.Date)
            .WithMessage("تاريخ نهاية السنة يجب أن يكون بعد تاريخ البداية.")
            .Must((request, end) => end!.Value.Date <= request.StartDate.Date.AddMonths(18))
            .WithMessage("السنة المالية لا تزيد عن 18 شهراً.")
            .When(x => x.EndDate.HasValue);
    }
}

public sealed class UpdateFiscalYearRequestValidator : AbstractValidator<UpdateFiscalYearRequest>
{
    public UpdateFiscalYearRequestValidator()
    {
        RuleFor(x => x.ArabicName).ArabicName();
    }
}

public sealed class AccountingPeriodListQueryValidator : AbstractValidator<AccountingPeriodListQuery>
{
    public AccountingPeriodListQueryValidator()
    {
        Include(new PagedQueryRules<AccountingPeriodListQuery>(AccountingPeriodListQuery.AllowedSortFields));
        RuleFor(x => x.FiscalYearId).ValidOptionalId("السنة المالية غير صالحة.").When(x => x.FiscalYearId.HasValue);
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة الفترة غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.Date).ValidOptionalAccountingDate("التاريخ");
    }
}

public sealed class UpdateAccountingPeriodRequestValidator : AbstractValidator<UpdateAccountingPeriodRequest>
{
    public UpdateAccountingPeriodRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم الفترة مطلوب.").MaximumLength(100).WithMessage("اسم الفترة طويل جداً.");
    }
}

public sealed class CloseAccountingPeriodRequestValidator : AbstractValidator<CloseAccountingPeriodRequest>
{
    public CloseAccountingPeriodRequestValidator()
    {
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class ReopenAccountingPeriodRequestValidator : AbstractValidator<ReopenAccountingPeriodRequest>
{
    public ReopenAccountingPeriodRequestValidator()
    {
        RuleFor(x => x.Reason).ClearReason("سبب إعادة الفتح");
    }
}

// ------------------------------------------------------------------ journal entries

public sealed class JournalLineRequestValidator : AbstractValidator<JournalLineRequest>
{
    public JournalLineRequestValidator()
    {
        RuleFor(x => x.AccountId).ValidId("الحساب مطلوب.");
        RuleFor(x => x.Description).OptionalText(300);
        RuleFor(x => x.Debit).Money("المبلغ المدين");
        RuleFor(x => x.Credit).Money("المبلغ الدائن");
        RuleFor(x => x)
            .Must(line => !(line.Debit > 0m && line.Credit > 0m)).WithMessage("لا يجوز أن يكون السطر مديناً ودائناً معاً.")
            .Must(line => line.Debit != 0m || line.Credit != 0m).WithMessage("أدخل مبلغاً مديناً أو دائناً.")
            .OverridePropertyName("amount");
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CostCenterId).ValidOptionalId("مركز التكلفة غير صالح.").When(x => x.CostCenterId.HasValue);
        RuleForEach(x => x.Dimensions).ChildRules(dimension =>
        {
            dimension.RuleFor(d => d.DimensionType).IsInEnum().WithMessage("نوع البعد غير صالح.");
            dimension.RuleFor(d => d.DimensionId).ValidId("البعد مطلوب.");
        });
        RuleFor(x => x.Dimensions)
            .Must(list => list.Select(d => d.DimensionType).Distinct().Count() == list.Count)
            .WithMessage("لكل نوع بعد قيمة واحدة في السطر.");
        RuleFor(x => x.CustomerId).ValidOptionalId("العميل غير صالح.").When(x => x.CustomerId.HasValue);
        RuleFor(x => x.SupplierId).ValidOptionalId("المورد غير صالح.").When(x => x.SupplierId.HasValue);
        RuleFor(x => x.DriverId).ValidOptionalId("السائق غير صالح.").When(x => x.DriverId.HasValue);
        RuleFor(x => x.EmployeeId).ValidOptionalId("الموظف غير صالح.").When(x => x.EmployeeId.HasValue);
        RuleFor(x => x.CashboxId).ValidOptionalId("الصندوق غير صالح.").When(x => x.CashboxId.HasValue);
        RuleFor(x => x.BankAccountId).ValidOptionalId("الحساب البنكي غير صالح.").When(x => x.BankAccountId.HasValue);
        RuleFor(x => x.ExpenseCategoryId).ValidOptionalId("فئة المصروف غير صالحة.").When(x => x.ExpenseCategoryId.HasValue);
    }
}

public sealed class SaveJournalEntryRequestValidator : AbstractValidator<SaveJournalEntryRequest>
{
    public SaveJournalEntryRequestValidator()
    {
        RuleFor(x => x.EntryDate).ValidAccountingDate("تاريخ القيد");
        RuleFor(x => x.JournalType)
            .Must(type => type is JournalType.General or JournalType.Opening or JournalType.Adjustment or JournalType.Bank)
            .WithMessage("القيود اليدوية من نوع: عام، افتتاحي، تسوية أو بنك فقط؛ الأنواع الأخرى آلية.");
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.ExchangeRate)
            .GreaterThan(0m).WithMessage("سعر الصرف يجب أن يكون أكبر من صفر.")
            .PrecisionScale(18, 8, true).WithMessage("سعر الصرف يقبل 8 منازل عشرية على الأكثر.")
            .When(x => x.ExchangeRate.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("بيان القيد مطلوب.")
            .MaximumLength(500).WithMessage("بيان القيد طويل جداً (500 حرف على الأكثر).");
        RuleFor(x => x.Reference).OptionalText(100);
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("أدخل سطور القيد.")
            .Must(lines => lines.Count >= 2).WithMessage("القيد يحتاج سطرين على الأقل (مدين ودائن).")
            .Must(lines => lines.Count <= JournalRules.MaxLines).WithMessage($"الحد الأقصى {JournalRules.MaxLines} سطر في القيد الواحد.");
        RuleForEach(x => x.Lines).SetValidator(new JournalLineRequestValidator());
        RuleFor(x => x.Lines)
            .Must(lines => lines.Sum(l => l.Debit) == lines.Sum(l => l.Credit))
            .WithMessage(request => $"القيد غير متوازن: مجموع المدين {request.Lines.Sum(l => l.Debit):0.##} لا يساوي مجموع الدائن {request.Lines.Sum(l => l.Credit):0.##}.")
            .When(x => x.Lines.Count >= 2);
    }
}

public sealed class JournalDecisionRequestValidator : AbstractValidator<JournalDecisionRequest>
{
    public JournalDecisionRequestValidator()
    {
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class JournalReasonRequestValidator : AbstractValidator<JournalReasonRequest>
{
    public JournalReasonRequestValidator()
    {
        RuleFor(x => x.Reason).ClearReason("سبب الرفض");
    }
}

public sealed class ReverseJournalEntryRequestValidator : AbstractValidator<ReverseJournalEntryRequest>
{
    public ReverseJournalEntryRequestValidator()
    {
        RuleFor(x => x.ReversalDate).ValidOptionalAccountingDate("تاريخ القيد العكسي");
        RuleFor(x => x.Reason).ClearReason("سبب العكس");
    }
}

public sealed class JournalEntryListQueryValidator : AbstractValidator<JournalEntryListQuery>
{
    public JournalEntryListQueryValidator()
    {
        Include(new PagedQueryRules<JournalEntryListQuery>(JournalEntryListQuery.AllowedSortFields));
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة القيد غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.JournalType).IsInEnum().WithMessage("نوع اليومية غير صالح.").When(x => x.JournalType.HasValue);
        RuleFor(x => x.FromDate).ValidOptionalAccountingDate("تاريخ البداية");
        RuleFor(x => x.ToDate).ValidOptionalAccountingDate("تاريخ النهاية");
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.AccountId).ValidOptionalId("الحساب غير صالح.").When(x => x.AccountId.HasValue);
        RuleFor(x => x.AccountingPeriodId).ValidOptionalId("الفترة غير صالحة.").When(x => x.AccountingPeriodId.HasValue);
        RuleFor(x => x.SourceType).MaximumLength(50).WithMessage("نوع المستند المصدر طويل جداً.");
    }
}

// ------------------------------------------------------------------ posting rules and events

public sealed class PostingRuleListQueryValidator : AbstractValidator<PostingRuleListQuery>
{
    public PostingRuleListQueryValidator()
    {
        Include(new PagedQueryRules<PostingRuleListQuery>(PostingRuleListQuery.AllowedSortFields));
        RuleFor(x => x.AccountRole).MaximumLength(50).WithMessage("الدور طويل جداً.");
        RuleFor(x => x.AccountId).ValidOptionalId("الحساب غير صالح.").When(x => x.AccountId.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
    }
}

public sealed class SavePostingRuleRequestValidator : AbstractValidator<SavePostingRuleRequest>
{
    public SavePostingRuleRequestValidator()
    {
        RuleFor(x => x.AccountRole)
            .NotEmpty().WithMessage("دور الحساب مطلوب.")
            .Must(AccountRoles.IsKnown).WithMessage($"دور الحساب غير معروف. القيم المسموحة: {string.Join(", ", AccountRoles.All)}.");
        RuleFor(x => x.AccountId).ValidId("الحساب مطلوب.");
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.CashboxId).ValidOptionalId("الصندوق غير صالح.").When(x => x.CashboxId.HasValue);
        RuleFor(x => x.BankAccountId).ValidOptionalId("الحساب البنكي غير صالح.").When(x => x.BankAccountId.HasValue);
        RuleFor(x => x.ExpenseCategoryId).ValidOptionalId("فئة المصروف غير صالحة.").When(x => x.ExpenseCategoryId.HasValue);
        RuleFor(x => x.Priority).InclusiveBetween(-1000, 1000).WithMessage("الأولوية بين -1000 و 1000.");
        RuleFor(x => x.Description).OptionalText(300);
    }
}

public sealed class ResolvePostingAccountQueryValidator : AbstractValidator<ResolvePostingAccountQuery>
{
    public ResolvePostingAccountQueryValidator()
    {
        RuleFor(x => x.AccountRole)
            .NotEmpty().WithMessage("دور الحساب مطلوب.")
            .Must(AccountRoles.IsKnown).WithMessage("دور الحساب غير معروف.");
    }
}

public sealed class PostingEventListQueryValidator : AbstractValidator<PostingEventListQuery>
{
    public PostingEventListQueryValidator()
    {
        Include(new PagedQueryRules<PostingEventListQuery>(PostingEventListQuery.AllowedSortFields));
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة الحدث غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.EventType).MaximumLength(100).WithMessage("نوع الحدث طويل جداً.");
        RuleFor(x => x.SourceType).MaximumLength(50).WithMessage("نوع المستند طويل جداً.");
        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }
}

public sealed class ProcessPostingsRequestValidator : AbstractValidator<ProcessPostingsRequest>
{
    public ProcessPostingsRequestValidator()
    {
        RuleFor(x => x.MaxCount).InclusiveBetween(1, 1000).WithMessage("عدد الأحداث بين 1 و 1000.");
    }
}

public sealed class RetryPostingEventRequestValidator : AbstractValidator<RetryPostingEventRequest>
{
    public RetryPostingEventRequestValidator()
    {
        RuleFor(x => x.PostingDate).ValidOptionalAccountingDate("تاريخ الترحيل");
    }
}

// ------------------------------------------------------------------ bank reconciliation

public sealed class BankTransactionListQueryValidator : AbstractValidator<BankTransactionListQuery>
{
    public BankTransactionListQueryValidator()
    {
        Include(new PagedQueryRules<BankTransactionListQuery>(BankTransactionListQuery.AllowedSortFields));
        RuleFor(x => x.BankAccountId).ValidOptionalId("الحساب البنكي غير صالح.").When(x => x.BankAccountId.HasValue);
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
    }
}

public sealed class BankStatementLineRequestValidator : AbstractValidator<BankStatementLineRequest>
{
    public BankStatementLineRequestValidator()
    {
        RuleFor(x => x.TransactionDate).ValidAccountingDate("تاريخ الحركة");
        RuleFor(x => x.Description).NotEmpty().WithMessage("بيان الحركة مطلوب.").MaximumLength(300).WithMessage("بيان الحركة طويل جداً.");
        RuleFor(x => x.Reference).OptionalText(100);
        RuleFor(x => x.Deposit).Money("الإيداع");
        RuleFor(x => x.Withdrawal).Money("السحب");
        RuleFor(x => x)
            .Must(line => (line.Deposit > 0m) ^ (line.Withdrawal > 0m))
            .WithMessage("أدخل إيداعاً أو سحباً (واحداً فقط) لكل حركة.")
            .OverridePropertyName("amount");
    }
}

public sealed class CreateBankReconciliationRequestValidator : AbstractValidator<CreateBankReconciliationRequest>
{
    public CreateBankReconciliationRequestValidator()
    {
        RuleFor(x => x.BankAccountId).ValidId("الحساب البنكي مطلوب.");
        RuleFor(x => x.StatementDate).ValidAccountingDate("تاريخ كشف البنك");
        RuleFor(x => x.StatementEndingBalance)
            .GreaterThanOrEqualTo(-999_999_999_999m).WithMessage("رصيد الكشف غير صالح.")
            .LessThanOrEqualTo(999_999_999_999m).WithMessage("رصيد الكشف غير صالح.")
            .PrecisionScale(18, 2, true).WithMessage("رصيد الكشف يقبل منزلتين عشريتين على الأكثر.");
        RuleFor(x => x.StatementLines)
            .Must(lines => lines.Count <= 2000).WithMessage("الحد الأقصى 2000 حركة في الطلب الواحد.");
        RuleForEach(x => x.StatementLines).SetValidator(new BankStatementLineRequestValidator());
        RuleFor(x => x.StatementLines)
            .Must((request, lines) => lines.All(l => l.TransactionDate.Date <= request.StatementDate.Date))
            .WithMessage("حركات الكشف يجب ألا تكون بعد تاريخ الكشف.");
        RuleForEach(x => x.Matches).ChildRules(match =>
        {
            match.RuleFor(m => m.BankTransactionId).ValidId("حركة البنك مطلوبة.");
            match.RuleFor(m => m.JournalEntryLineId).ValidId("سطر القيد مطلوب.");
        });
        RuleFor(x => x.Matches)
            .Must(matches => matches.Select(m => m.BankTransactionId).Distinct().Count() == matches.Count &&
                             matches.Select(m => m.JournalEntryLineId).Distinct().Count() == matches.Count)
            .WithMessage("لا يجوز تكرار حركة البنك أو سطر القيد في المطابقات.");
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class CompleteBankReconciliationRequestValidator : AbstractValidator<CompleteBankReconciliationRequest>
{
    public CompleteBankReconciliationRequestValidator()
    {
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class BankReconciliationListQueryValidator : AbstractValidator<BankReconciliationListQuery>
{
    public BankReconciliationListQueryValidator()
    {
        Include(new PagedQueryRules<BankReconciliationListQuery>(BankReconciliationListQuery.AllowedSortFields));
        RuleFor(x => x.BankAccountId).ValidOptionalId("الحساب البنكي غير صالح.").When(x => x.BankAccountId.HasValue);
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة التسوية غير صالحة.").When(x => x.Status.HasValue);
    }
}

// ------------------------------------------------------------------ reports

public sealed class GeneralLedgerQueryValidator : AbstractValidator<GeneralLedgerQuery>
{
    public GeneralLedgerQueryValidator()
    {
        Include(new PagedQueryRules<GeneralLedgerQuery>(Array.Empty<string>()));
        RuleFor(x => x.AccountId).ValidId("الحساب مطلوب.");
        RuleFor(x => x.FromDate).ValidOptionalAccountingDate("تاريخ البداية");
        RuleFor(x => x.ToDate).ValidOptionalAccountingDate("تاريخ النهاية");
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CostCenterId).ValidOptionalId("مركز التكلفة غير صالح.").When(x => x.CostCenterId.HasValue);
    }
}

public sealed class TrialBalanceQueryValidator : AbstractValidator<TrialBalanceQuery>
{
    public TrialBalanceQueryValidator()
    {
        RuleFor(x => x.FromDate).ValidOptionalAccountingDate("تاريخ البداية");
        RuleFor(x => x.ToDate).ValidOptionalAccountingDate("تاريخ النهاية");
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CostCenterId).ValidOptionalId("مركز التكلفة غير صالح.").When(x => x.CostCenterId.HasValue);
        RuleFor(x => x.Level).InclusiveBetween(1, 10).WithMessage("المستوى بين 1 و 10.").When(x => x.Level.HasValue);
    }
}

public sealed class ProfitLossQueryValidator : AbstractValidator<ProfitLossQuery>
{
    public ProfitLossQueryValidator()
    {
        RuleFor(x => x.FromDate).ValidOptionalAccountingDate("تاريخ البداية");
        RuleFor(x => x.ToDate).ValidOptionalAccountingDate("تاريخ النهاية");
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CostCenterId).ValidOptionalId("مركز التكلفة غير صالح.").When(x => x.CostCenterId.HasValue);
    }
}

public sealed class BalanceSheetQueryValidator : AbstractValidator<BalanceSheetQuery>
{
    public BalanceSheetQueryValidator()
    {
        RuleFor(x => x.AsOfDate).ValidOptionalAccountingDate("تاريخ الميزانية");
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
    }
}

public sealed class CashFlowQueryValidator : AbstractValidator<CashFlowQuery>
{
    public CashFlowQueryValidator()
    {
        RuleFor(x => x.FromDate).ValidOptionalAccountingDate("تاريخ البداية");
        RuleFor(x => x.ToDate).ValidOptionalAccountingDate("تاريخ النهاية");
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
    }
}

public sealed class JournalReportQueryValidator : AbstractValidator<JournalReportQuery>
{
    public JournalReportQueryValidator()
    {
        Include(new PagedQueryRules<JournalReportQuery>(Array.Empty<string>()));
        RuleFor(x => x.FromDate).ValidOptionalAccountingDate("تاريخ البداية");
        RuleFor(x => x.ToDate).ValidOptionalAccountingDate("تاريخ النهاية");
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
        RuleFor(x => x.JournalType).IsInEnum().WithMessage("نوع اليومية غير صالح.").When(x => x.JournalType.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
    }
}

public sealed class RevenueByBranchQueryValidator : AbstractValidator<RevenueByBranchQuery>
{
    public RevenueByBranchQueryValidator()
    {
        RuleFor(x => x.FromDate).ValidOptionalAccountingDate("تاريخ البداية");
        RuleFor(x => x.ToDate).ValidOptionalAccountingDate("تاريخ النهاية");
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
    }
}

public sealed class ExpenseByCategoryQueryValidator : AbstractValidator<ExpenseByCategoryQuery>
{
    public ExpenseByCategoryQueryValidator()
    {
        RuleFor(x => x.FromDate).ValidOptionalAccountingDate("تاريخ البداية");
        RuleFor(x => x.ToDate).ValidOptionalAccountingDate("تاريخ النهاية");
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value).WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
        RuleFor(x => x.CostCenterId).ValidOptionalId("مركز التكلفة غير صالح.").When(x => x.CostCenterId.HasValue);
    }
}

public sealed class PartyAgingQueryValidator : AbstractValidator<PartyAgingQuery>
{
    public PartyAgingQueryValidator()
    {
        RuleFor(x => x.AsOfDate).ValidOptionalAccountingDate("تاريخ الأعمار");
        RuleFor(x => x.BranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.BranchId.HasValue);
    }
}
