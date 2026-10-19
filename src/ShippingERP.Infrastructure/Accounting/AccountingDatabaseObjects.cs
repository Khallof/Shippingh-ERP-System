using Microsoft.EntityFrameworkCore;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Persistence.Configurations.Accounting;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>
/// SQL Server triggers that protect the general ledger in the database itself (step 7), in addition to the
/// JournalIntegrityInterceptor of the application. They are created (CREATE OR ALTER, so it is safe to run every
/// time) at start-up by the ChartOfAccountsSeeder once the accounting tables exist, so nothing has to be added by
/// hand to the generated migration.
///
///   TR_JournalEntries_Immutable    : a Posted entry can only become Reversed (reversal columns only); a Reversed
///                                    entry never changes; only Draft / Rejected entries can be deleted.
///   TR_JournalEntryLines_Immutable : lines are never updated; lines of a non-draft entry are never deleted.
///
/// Errors 51001-51006 reach the API as a 500 with a trace id: the application never sends such a statement, so
/// one would mean a bug or a direct database change, exactly what must be stopped.
/// </summary>
public static class AccountingDatabaseObjects
{
    public const string JournalEntriesTriggerSql = $"""
        CREATE OR ALTER TRIGGER [dbo].[{AccountingTriggers.JournalEntries}] ON [dbo].[JournalEntries]
        AFTER UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF NOT EXISTS (SELECT 1 FROM inserted)
            BEGIN
                IF EXISTS (SELECT 1 FROM deleted WHERE [Status] NOT IN ('Draft', 'Rejected'))
                    THROW 51001, N'Only Draft or Rejected journal entries can be deleted.', 1;
                RETURN;
            END;

            IF EXISTS (SELECT 1 FROM deleted WHERE [Status] = 'Reversed')
                THROW 51002, N'A reversed journal entry cannot be changed.', 1;

            IF EXISTS (SELECT 1 FROM deleted AS d INNER JOIN inserted AS i ON i.[Id] = d.[Id]
                       WHERE d.[Status] = 'Posted' AND i.[Status] NOT IN ('Posted', 'Reversed'))
                THROW 51003, N'A posted journal entry can only be reversed.', 1;

            IF EXISTS (SELECT 1 FROM deleted WHERE [Status] = 'Posted')
               AND (UPDATE([EntryNumber]) OR UPDATE([JournalType]) OR UPDATE([EntryDate]) OR UPDATE([AccountingPeriodId])
                    OR UPDATE([BranchId]) OR UPDATE([CurrencyId]) OR UPDATE([ExchangeRate]) OR UPDATE([Description])
                    OR UPDATE([Reference]) OR UPDATE([TotalDebit]) OR UPDATE([TotalCredit]) OR UPDATE([TotalDebitBase])
                    OR UPDATE([TotalCreditBase]) OR UPDATE([IsSystemGenerated]) OR UPDATE([SourceType]) OR UPDATE([SourceId])
                    OR UPDATE([SourceNumber]) OR UPDATE([SourceEventType]) OR UPDATE([SourceKey]) OR UPDATE([PostedAtUtc])
                    OR UPDATE([PostedByUserId]) OR UPDATE([ReversalOfEntryId]) OR UPDATE([CreatedAtUtc]) OR UPDATE([CreatedByUserId]))
                THROW 51004, N'A posted journal entry is immutable: correct it with a reversal or an adjustment entry.', 1;
        END;
        """;

    public const string JournalEntryLinesTriggerSql = $"""
        CREATE OR ALTER TRIGGER [dbo].[{AccountingTriggers.JournalEntryLines}] ON [dbo].[JournalEntryLines]
        AFTER UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM inserted)
                THROW 51005, N'Journal entry lines are never updated.', 1;

            IF EXISTS (SELECT 1 FROM deleted AS d INNER JOIN [dbo].[JournalEntries] AS e ON e.[Id] = d.[JournalEntryId]
                       WHERE e.[Status] NOT IN ('Draft', 'Rejected'))
                THROW 51006, N'Lines of a submitted, approved, posted or reversed journal entry cannot be deleted.', 1;
        END;
        """;

    /// <summary>Creates or updates the triggers (SQL Server only; nothing to do for other providers).</summary>
    public static async Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(JournalEntriesTriggerSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(JournalEntryLinesTriggerSql, cancellationToken);
    }
}
