using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Accounting;

/// <summary>One line of a journal entry as seen by the balance checks.</summary>
public readonly record struct JournalLineAmount(decimal Debit, decimal Credit);

/// <summary>
/// The rules every journal entry must follow (pure functions, unit tested):
///
///   Draft / Rejected --submit--> Submitted --approve--> Approved --post--> Posted --reverse--> Reversed
///                                 Submitted / Approved --reject--> Rejected
///
/// Draft and Rejected entries are the only editable or deletable ones.
/// </summary>
public static class JournalRules
{
    public const int MaxLines = 500;

    public static bool CanEdit(JournalEntryStatus status) => status is JournalEntryStatus.Draft or JournalEntryStatus.Rejected;

    public static bool CanDelete(JournalEntryStatus status) => CanEdit(status);

    public static bool CanSubmit(JournalEntryStatus status) => CanEdit(status);

    public static bool CanApprove(JournalEntryStatus status) => status == JournalEntryStatus.Submitted;

    public static bool CanReject(JournalEntryStatus status) => status is JournalEntryStatus.Submitted or JournalEntryStatus.Approved;

    public static bool CanPost(JournalEntryStatus status) => status == JournalEntryStatus.Approved;

    public static bool CanReverse(JournalEntryStatus status) => status == JournalEntryStatus.Posted;

    /// <summary>Statuses whose lines are part of the general ledger.</summary>
    public static bool IsInLedger(JournalEntryStatus status) => status is JournalEntryStatus.Posted or JournalEntryStatus.Reversed;

    /// <summary>
    /// The balance errors of a set of lines (empty = valid): at least two lines, every amount ≥ 0, exactly one side
    /// per line, and total debit = total credit &gt; 0.
    /// </summary>
    public static IReadOnlyList<string> Validate(IReadOnlyList<JournalLineAmount> lines)
    {
        var errors = new List<string>();

        if (lines.Count < 2)
        {
            errors.Add("القيد يحتاج سطرين على الأقل (مدين ودائن).");
        }

        if (lines.Count > MaxLines)
        {
            errors.Add($"الحد الأقصى {MaxLines} سطر في القيد الواحد.");
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var number = i + 1;

            if (line.Debit < 0m || line.Credit < 0m)
            {
                errors.Add($"السطر {number}: المبالغ السالبة غير مسموحة؛ استخدم الجانب الآخر.");
            }
            else if (line.Debit > 0m && line.Credit > 0m)
            {
                errors.Add($"السطر {number}: لا يجوز أن يكون السطر مديناً ودائناً معاً.");
            }
            else if (line.Debit == 0m && line.Credit == 0m)
            {
                errors.Add($"السطر {number}: أدخل مبلغاً مديناً أو دائناً.");
            }
        }

        var totalDebit = lines.Sum(x => x.Debit);
        var totalCredit = lines.Sum(x => x.Credit);

        if (totalDebit != totalCredit)
        {
            errors.Add($"القيد غير متوازن: مجموع المدين {totalDebit:0.##} لا يساوي مجموع الدائن {totalCredit:0.##}.");
        }
        else if (totalDebit == 0m && lines.Count >= 2)
        {
            errors.Add("مجموع القيد يجب أن يكون أكبر من صفر.");
        }

        return errors;
    }

    public static bool IsBalanced(IReadOnlyList<JournalLineAmount> lines) => Validate(lines).Count == 0;
}
