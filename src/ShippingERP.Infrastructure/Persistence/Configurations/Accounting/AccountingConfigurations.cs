using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Infrastructure.Persistence.Configurations.MasterData;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Accounting;

/// <summary>Names of the SQL Server triggers that protect posted entries (created by AccountingDatabaseObjects).</summary>
public static class AccountingTriggers
{
    public const string JournalEntries = "TR_JournalEntries_Immutable";
    public const string JournalEntryLines = "TR_JournalEntryLines_Immutable";
}

/// <summary>
/// Double-entry accounting tables (step 7). Every foreign key is RESTRICT: nothing in the ledger is ever deleted
/// by a cascade, except the lines of a Draft / Rejected entry that is deleted (the trigger refuses it otherwise).
/// </summary>
public sealed class AccountCategoryConfiguration : IEntityTypeConfiguration<AccountCategory>
{
    public void Configure(EntityTypeBuilder<AccountCategory> builder)
    {
        builder.ToTable("AccountCategories");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 30, nameLength: 150);
        builder.Property(x => x.AccountType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.CashFlowActivity).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
    }
}

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", table => table.HasCheckConstraint("CK_Accounts_Level", "[Level] BETWEEN 1 AND 10"));

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 20, nameLength: 200);
        builder.Property(x => x.AccountType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.NormalBalance).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasIndex(x => new { x.AccountType, x.IsPostable });
        builder.HasIndex(x => x.ParentAccountId);

        builder.HasOne(x => x.ParentAccount).WithMany(x => x.Children).HasForeignKey(x => x.ParentAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AccountCategory).WithMany(x => x.Accounts).HasForeignKey(x => x.AccountCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.ToTable("FiscalYears", table => table.HasCheckConstraint("CK_FiscalYears_Dates", "[EndDate] > [StartDate]"));

        builder.Property(x => x.Code).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.ArabicName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.StartDate).HasColumnType("date");
        builder.Property(x => x.EndDate).HasColumnType("date");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(10).IsUnicode(false);

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.StartDate, x.EndDate });

        builder.HasMany(x => x.Periods).WithOne(x => x.FiscalYear).HasForeignKey(x => x.FiscalYearId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ToTable("AccountingPeriods", table => table.HasCheckConstraint("CK_AccountingPeriods_Dates", "[EndDate] >= [StartDate]"));

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StartDate).HasColumnType("date");
        builder.Property(x => x.EndDate).HasColumnType("date");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.CloseNotes).HasMaxLength(500);
        builder.Property(x => x.ReopenReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.FiscalYearId, x.PeriodNumber }).IsUnique();
        builder.HasIndex(x => new { x.StartDate, x.EndDate });
    }
}

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries", table =>
        {
            // Posted (and later reversed) entries are balanced in both currencies; the database refuses anything else.
            table.HasCheckConstraint(
                "CK_JournalEntries_Balanced",
                "[Status] NOT IN ('Posted', 'Reversed') OR ([TotalDebit] = [TotalCredit] AND [TotalDebitBase] = [TotalCreditBase] AND [TotalDebit] > 0)");
            table.HasCheckConstraint("CK_JournalEntries_ExchangeRate", "[ExchangeRate] > 0");

            // The trigger blocks changes to posted entries; EF must not use OUTPUT without INTO on this table.
            table.HasTrigger(AccountingTriggers.JournalEntries);
        });

        builder.Property(x => x.EntryNumber).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.JournalType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.EntryDate).HasColumnType("date");
        builder.Property(x => x.ExchangeRate).HasPrecision(18, 8);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Property(x => x.SourceType).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.SourceNumber).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.SourceEventType).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.SourceKey).HasMaxLength(200).IsUnicode(false);
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);

        builder.HasIndex(x => x.EntryNumber).IsUnique();

        // THE duplicate-posting guard: one entry per business document / idempotency key.
        builder.HasIndex(x => x.SourceKey).IsUnique().HasFilter("[SourceKey] IS NOT NULL").HasDatabaseName("UX_JournalEntries_SourceKey");

        builder.HasIndex(x => new { x.Status, x.EntryDate });
        builder.HasIndex(x => new { x.AccountingPeriodId, x.Status });
        builder.HasIndex(x => new { x.SourceType, x.SourceId });
        builder.HasIndex(x => x.ReversalOfEntryId).IsUnique().HasFilter("[ReversalOfEntryId] IS NOT NULL");

        builder.HasOne(x => x.AccountingPeriod).WithMany().HasForeignKey(x => x.AccountingPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReversalOfEntry).WithMany().HasForeignKey(x => x.ReversalOfEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReversedByEntry).WithMany().HasForeignKey(x => x.ReversedByEntryId).OnDelete(DeleteBehavior.Restrict);

        // Lines go with their (draft) entry; the trigger refuses the delete of a posted one.
        builder.HasMany(x => x.Lines).WithOne(x => x.JournalEntry).HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ToTable("JournalEntryLines", table =>
        {
            table.HasCheckConstraint(
                "CK_JournalEntryLines_DebitOrCredit",
                "[Debit] >= 0 AND [Credit] >= 0 AND ([Debit] = 0 OR [Credit] = 0) AND [Debit] + [Credit] > 0");
            table.HasCheckConstraint(
                "CK_JournalEntryLines_BaseDebitOrCredit",
                "[DebitBase] >= 0 AND [CreditBase] >= 0 AND ([DebitBase] = 0 OR [CreditBase] = 0)");
            table.HasTrigger(AccountingTriggers.JournalEntryLines);
        });

        builder.Property(x => x.Description).HasMaxLength(300);
        builder.Property(x => x.AccountRole).HasMaxLength(50).IsUnicode(false);

        builder.HasIndex(x => new { x.JournalEntryId, x.LineNumber }).IsUnique();

        // Ledger and report queries: by account, then the sub-ledger references.
        builder.HasIndex(x => new { x.AccountId, x.JournalEntryId }).IncludeProperties(x => new { x.DebitBase, x.CreditBase, x.BranchId });
        builder.HasIndex(x => new { x.BranchId, x.AccountId });
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.SupplierId);
        builder.HasIndex(x => x.DriverId);
        builder.HasIndex(x => x.CashboxId);
        builder.HasIndex(x => x.BankAccountId);
        builder.HasIndex(x => x.ExpenseCategoryId);

        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Dimensions).WithOne(x => x.JournalEntryLine).HasForeignKey(x => x.JournalEntryLineId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AccountingDimensionConfiguration : IEntityTypeConfiguration<AccountingDimension>
{
    public void Configure(EntityTypeBuilder<AccountingDimension> builder)
    {
        builder.ToTable("AccountingDimensions");

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 30, nameLength: 150, uniqueCode: false);
        builder.Property(x => x.DimensionType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasIndex(x => new { x.DimensionType, x.Code }).IsUnique();

        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JournalEntryDimensionConfiguration : IEntityTypeConfiguration<JournalEntryDimension>
{
    public void Configure(EntityTypeBuilder<JournalEntryDimension> builder)
    {
        builder.ToTable("JournalEntryDimensions");

        builder.Property(x => x.DimensionType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);

        builder.HasIndex(x => new { x.JournalEntryLineId, x.DimensionType }).IsUnique();
        builder.HasIndex(x => x.AccountingDimensionId);

        builder.HasOne(x => x.AccountingDimension).WithMany().HasForeignKey(x => x.AccountingDimensionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ToTable("BankTransactions", table => table.HasCheckConstraint(
            "CK_BankTransactions_DepositOrWithdrawal",
            "[Deposit] >= 0 AND [Withdrawal] >= 0 AND ([Deposit] = 0 OR [Withdrawal] = 0) AND [Deposit] + [Withdrawal] > 0"));

        builder.Ignore(x => x.SignedAmount);
        builder.Property(x => x.TransactionDate).HasColumnType("date");
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(100);

        builder.HasIndex(x => new { x.BankAccountId, x.IsReconciled, x.TransactionDate });

        builder.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BankReconciliation).WithMany().HasForeignKey(x => x.BankReconciliationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntryLine>().WithMany().HasForeignKey(x => x.MatchedJournalEntryLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BankReconciliationConfiguration : IEntityTypeConfiguration<BankReconciliation>
{
    public void Configure(EntityTypeBuilder<BankReconciliation> builder)
    {
        builder.ToTable("BankReconciliations");

        builder.Property(x => x.ReconciliationNumber).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.StatementDate).HasColumnType("date");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.ReconciliationNumber).IsUnique();
        builder.HasIndex(x => new { x.BankAccountId, x.StatementDate });

        builder.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(x => x.BankReconciliation).HasForeignKey(x => x.BankReconciliationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BankReconciliationLineConfiguration : IEntityTypeConfiguration<BankReconciliationLine>
{
    public void Configure(EntityTypeBuilder<BankReconciliationLine> builder)
    {
        builder.ToTable("BankReconciliationLines");

        builder.Property(x => x.LineType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.TransactionDate).HasColumnType("date");
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();

        builder.HasIndex(x => x.BankReconciliationId);

        // A ledger line and a statement line can be cleared by one completed reconciliation only.
        builder.HasIndex(x => x.JournalEntryLineId).IsUnique().HasFilter("[IsCleared] = 1 AND [JournalEntryLineId] IS NOT NULL")
            .HasDatabaseName("UX_BankReconciliationLines_ClearedLedgerLine");
        builder.HasIndex(x => x.BankTransactionId).IsUnique().HasFilter("[IsCleared] = 1 AND [BankTransactionId] IS NOT NULL")
            .HasDatabaseName("UX_BankReconciliationLines_ClearedStatementLine");

        builder.HasOne(x => x.BankTransaction).WithMany().HasForeignKey(x => x.BankTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.JournalEntryLine).WithMany().HasForeignKey(x => x.JournalEntryLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountingPostingRuleConfiguration : IEntityTypeConfiguration<AccountingPostingRule>
{
    public void Configure(EntityTypeBuilder<AccountingPostingRule> builder)
    {
        builder.ToTable("AccountingPostingRules");

        builder.Property(x => x.AccountRole).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300);

        builder.HasIndex(x => new { x.AccountRole, x.IsActive });

        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.MasterData.Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.MasterData.Currency>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Cash.Cashbox>().WithMany().HasForeignKey(x => x.CashboxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Cash.BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.MasterData.ExpenseCategory>().WithMany().HasForeignKey(x => x.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountingPostingEventConfiguration : IEntityTypeConfiguration<AccountingPostingEvent>
{
    public void Configure(EntityTypeBuilder<AccountingPostingEvent> builder)
    {
        builder.ToTable("AccountingPostingEvents");

        builder.Property(x => x.EventType).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.SourceType).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.SourceNumber).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.SourceKey).HasMaxLength(200).IsUnicode(false);
        builder.Property(x => x.PostingDate).HasColumnType("date");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.LastError).HasMaxLength(1000);

        // THE at-least-once delivery guard: an event is processed once.
        builder.HasIndex(x => x.EventId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.OccurredAtUtc });
        builder.HasIndex(x => x.OutboxMessageId);

        builder.HasOne(x => x.JournalEntry).WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Integration.OutboxMessage>().WithMany().HasForeignKey(x => x.OutboxMessageId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReversalJournalLinkConfiguration : IEntityTypeConfiguration<ReversalJournalLink>
{
    public void Configure(EntityTypeBuilder<ReversalJournalLink> builder)
    {
        builder.ToTable("ReversalJournalLinks");

        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();

        builder.HasIndex(x => x.OriginalEntryId).IsUnique();
        builder.HasIndex(x => x.ReversalEntryId).IsUnique();

        builder.HasOne(x => x.OriginalEntry).WithMany().HasForeignKey(x => x.OriginalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReversalEntry).WithMany().HasForeignKey(x => x.ReversalEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
