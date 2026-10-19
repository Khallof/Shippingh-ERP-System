using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Protects the general ledger in the application (step 7). The SQL Server triggers TR_JournalEntries_Immutable and
/// TR_JournalEntryLines_Immutable enforce the same rules in the database (defense in depth).
///
/// - A journal line or line dimension is never UPDATED.
/// - A journal line is ADDED only with a new entry or to a Draft / Rejected entry being edited (a posted entry is
///   created with all its lines in one SaveChanges; nothing can be added to it later).
/// - A journal line is DELETED only together with the editing of its Draft / Rejected entry (loaded in the context).
/// - A journal entry is DELETED only while Draft / Rejected.
/// - A Posted entry may only become Reversed (status, reversal columns and audit columns); a Reversed entry never
///   changes again. Every other change throws: corrections are reversal or adjustment entries.
/// </summary>
public sealed class JournalIntegrityInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<string> ReversalColumns = new(StringComparer.Ordinal)
    {
        nameof(JournalEntry.Status),
        nameof(JournalEntry.ReversedAtUtc),
        nameof(JournalEntry.ReversedByUserId),
        nameof(JournalEntry.ReversedByEntryId),
        nameof(JournalEntry.UpdatedAtUtc),
        nameof(JournalEntry.UpdatedByUserId),
        nameof(JournalEntry.RowVersion)
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public static void Guard(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker.Entries().ToList();

        foreach (var entry in entries)
        {
            switch (entry.Entity)
            {
                case JournalEntry journal:
                    GuardEntry(entry, journal);
                    break;
                case JournalEntryLine line:
                    GuardLine(context, entry, line);
                    break;
                case JournalEntryDimension dimension:
                    GuardDimension(context, entry, dimension);
                    break;
            }
        }
    }

    private static void GuardEntry(EntityEntry entry, JournalEntry journal)
    {
        if (entry.State is not (EntityState.Modified or EntityState.Deleted))
        {
            return;
        }

        var original = entry.OriginalValues.GetValue<JournalEntryStatus>(nameof(JournalEntry.Status));

        if (entry.State == EntityState.Deleted)
        {
            if (!JournalRules.CanDelete(original))
            {
                throw Immutable($"لا يمكن حذف القيد {journal.EntryNumber} في حالة «{AccountingNames.Arabic(original)}».");
            }

            return;
        }

        if (original == JournalEntryStatus.Reversed)
        {
            throw Immutable($"القيد {journal.EntryNumber} معكوس ولا يمكن تعديله.");
        }

        if (original == JournalEntryStatus.Posted)
        {
            var changed = entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList();
            var forbidden = changed.Where(name => !ReversalColumns.Contains(name)).ToList();

            if (forbidden.Count > 0 || journal.Status is not (JournalEntryStatus.Posted or JournalEntryStatus.Reversed))
            {
                throw Immutable(
                    $"القيد {journal.EntryNumber} مرحّل ولا يمكن تعديله ({string.Join(", ", forbidden.DefaultIfEmpty(nameof(JournalEntry.Status)))}). " +
                    "صحّحه بقيد عكسي أو قيد تسوية.");
            }
        }
    }

    private static void GuardLine(DbContext context, EntityEntry entry, JournalEntryLine line)
    {
        if (entry.State == EntityState.Modified)
        {
            throw Immutable("سطور القيود لا تُعدَّل؛ عدّل القيد المسودة باستبدال سطوره.");
        }

        if (entry.State == EntityState.Added)
        {
            var owner = entry.Reference(nameof(JournalEntryLine.JournalEntry)).TargetEntry
                        ?? context.ChangeTracker.Entries<JournalEntry>().FirstOrDefault(x => x.Entity.Id == line.JournalEntryId);
            if (owner is null)
            {
                throw Immutable("يُضاف سطر القيد مع قيده فقط.");
            }

            if (owner.State != EntityState.Added)
            {
                var ownerStatus = owner.OriginalValues.GetValue<JournalEntryStatus>(nameof(JournalEntry.Status));
                if (!JournalRules.CanEdit(ownerStatus))
                {
                    throw Immutable($"لا تُضاف سطور إلى القيد {((JournalEntry)owner.Entity).EntryNumber} في حالة «{AccountingNames.Arabic(ownerStatus)}».");
                }
            }

            return;
        }

        if (entry.State != EntityState.Deleted)
        {
            return;
        }

        var parent = context.ChangeTracker.Entries<JournalEntry>().FirstOrDefault(x => x.Entity.Id == line.JournalEntryId);
        if (parent is null)
        {
            throw Immutable("لا يُحذف سطر قيد إلا مع تعديل قيده المسودة.");
        }

        var parentStatus = parent.State == EntityState.Added
            ? parent.Entity.Status
            : parent.OriginalValues.GetValue<JournalEntryStatus>(nameof(JournalEntry.Status));

        if (!JournalRules.CanEdit(parentStatus))
        {
            throw Immutable($"سطور القيد {parent.Entity.EntryNumber} لا تُحذف في حالة «{AccountingNames.Arabic(parentStatus)}».");
        }
    }

    private static void GuardDimension(DbContext context, EntityEntry entry, JournalEntryDimension dimension)
    {
        if (entry.State == EntityState.Modified)
        {
            throw Immutable("أبعاد سطور القيود لا تُعدَّل.");
        }

        if (entry.State == EntityState.Deleted &&
            !context.ChangeTracker.Entries<JournalEntryLine>().Any(x => x.Entity.Id == dimension.JournalEntryLineId && x.State == EntityState.Deleted))
        {
            throw Immutable("لا يُحذف بعد سطر قيد إلا مع حذف السطر نفسه.");
        }
    }

    private static InvalidOperationException Immutable(string message) => new(message);
}
