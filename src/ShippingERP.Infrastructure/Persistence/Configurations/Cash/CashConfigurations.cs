using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Infrastructure.Persistence.Configurations.MasterData;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Cash;

/// <summary>
/// Cashboxes, their append-only transactions, daily closings, vouchers, transfers and bank accounts (step 6).
/// Check constraints make the database itself refuse a negative cashbox balance.
/// </summary>
public sealed class CashboxConfiguration : IEntityTypeConfiguration<Cashbox>
{
    public void Configure(EntityTypeBuilder<Cashbox> builder)
    {
        builder.ToTable("Cashboxes", table => table.HasCheckConstraint("CK_Cashboxes_Balance", "[CurrentBalance] >= 0"));

        builder.ConfigureCodeAndNames(x => x.Code, x => x.ArabicName, x => x.EnglishName, codeLength: 30, nameLength: 150);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => new { x.BranchId, x.CurrencyId });

        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CustodianUser).WithMany().HasForeignKey(x => x.CustodianUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Transactions).WithOne(x => x.Cashbox).HasForeignKey(x => x.CashboxId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashboxTransactionConfiguration : IEntityTypeConfiguration<CashboxTransaction>
{
    public void Configure(EntityTypeBuilder<CashboxTransaction> builder)
    {
        builder.ToTable("CashboxTransactions", table => table.HasCheckConstraint(
            "CK_CashboxTransactions_InOrOut",
            "[AmountIn] >= 0 AND [AmountOut] >= 0 AND ([AmountIn] = 0 OR [AmountOut] = 0) AND [BalanceAfter] >= 0"));

        builder.Property(x => x.TransactionType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.BusinessDate).HasColumnType("date");
        builder.Property(x => x.DocumentType).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.DocumentNumber).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.CounterpartyName).HasMaxLength(150);

        // Statement and daily closing: one cashbox, in order.
        builder.HasIndex(x => new { x.CashboxId, x.BusinessDate });
        builder.HasIndex(x => new { x.BranchId, x.BusinessDate });
        builder.HasIndex(x => new { x.DocumentType, x.DocumentId });
    }
}

public sealed class CashboxDailyClosingConfiguration : IEntityTypeConfiguration<CashboxDailyClosing>
{
    public void Configure(EntityTypeBuilder<CashboxDailyClosing> builder)
    {
        builder.ToTable("CashboxDailyClosings", table => table.HasCheckConstraint(
            "CK_CashboxDailyClosings_Counted", "[CountedBalance] >= 0"));

        builder.Property(x => x.BusinessDate).HasColumnType("date");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.DiscrepancyReason).HasMaxLength(500);
        builder.Property(x => x.DecisionNotes).HasMaxLength(500);

        // One active closing per cashbox and day; a rejected count can be redone.
        builder.HasIndex(x => new { x.CashboxId, x.BusinessDate })
            .IsUnique()
            .HasFilter("[Status] <> 'Rejected'")
            .HasDatabaseName("UX_CashboxDailyClosings_ActiveDay");

        builder.HasIndex(x => new { x.Status, x.BusinessDate });

        builder.HasOne(x => x.Cashbox).WithMany().HasForeignKey(x => x.CashboxId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashReceiptVoucherConfiguration : IEntityTypeConfiguration<CashReceiptVoucher>
{
    public void Configure(EntityTypeBuilder<CashReceiptVoucher> builder)
    {
        builder.ToTable("CashReceiptVouchers", table => table.HasCheckConstraint(
            "CK_CashReceiptVouchers_Amounts", "[Amount] > 0 AND [ShortageAmount] >= 0 AND [ExcessAmount] >= 0"));

        builder.Property(x => x.VoucherNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.ReceiptType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.ReceivedFrom).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false);

        builder.HasIndex(x => x.VoucherNumber).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_CashReceiptVouchers_IdempotencyKey");

        // A customer cash payment is deposited into a cashbox only once.
        builder.HasIndex(x => x.CustomerPaymentId)
            .IsUnique()
            .HasFilter("[CustomerPaymentId] IS NOT NULL AND [Status] = 'Posted'")
            .HasDatabaseName("UX_CashReceiptVouchers_CustomerPayment");

        builder.HasIndex(x => new { x.CashboxId, x.VoucherDateUtc });
        builder.HasIndex(x => new { x.BranchId, x.VoucherDateUtc });
        builder.HasIndex(x => x.DriverId);

        builder.HasOne(x => x.Cashbox).WithMany().HasForeignKey(x => x.CashboxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CustomerPayment).WithMany().HasForeignKey(x => x.CustomerPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashPaymentVoucherConfiguration : IEntityTypeConfiguration<CashPaymentVoucher>
{
    public void Configure(EntityTypeBuilder<CashPaymentVoucher> builder)
    {
        builder.ToTable("CashPaymentVouchers", table => table.HasCheckConstraint("CK_CashPaymentVouchers_Amount", "[Amount] > 0"));

        builder.Property(x => x.VoucherNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.PaymentType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.PaidTo).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false);

        builder.HasIndex(x => x.VoucherNumber).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_CashPaymentVouchers_IdempotencyKey");
        builder.HasIndex(x => new { x.CashboxId, x.VoucherDateUtc });
        builder.HasIndex(x => new { x.BranchId, x.VoucherDateUtc });
        builder.HasIndex(x => x.SenderSettlementId);

        builder.HasOne(x => x.Cashbox).WithMany().HasForeignKey(x => x.CashboxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ExpenseCategory).WithMany().HasForeignKey(x => x.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CashTransferConfiguration : IEntityTypeConfiguration<CashTransfer>
{
    public void Configure(EntityTypeBuilder<CashTransfer> builder)
    {
        builder.ToTable("CashTransfers", table => table.HasCheckConstraint(
            "CK_CashTransfers_Target",
            "[Amount] > 0 AND (([ToCashboxId] IS NOT NULL AND [ToBankAccountId] IS NULL) OR ([ToCashboxId] IS NULL AND [ToBankAccountId] IS NOT NULL))"));

        builder.Property(x => x.TransferNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.TransferType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.BankReference).HasMaxLength(100);
        builder.Property(x => x.DecisionNotes).HasMaxLength(500);

        builder.HasIndex(x => x.TransferNumber).IsUnique();
        builder.HasIndex(x => new { x.Status, x.FromBranchId });
        builder.HasIndex(x => new { x.Status, x.ToBranchId });

        builder.HasOne(x => x.FromCashbox).WithMany().HasForeignKey(x => x.FromCashboxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToCashbox).WithMany().HasForeignKey(x => x.ToCashboxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToBankAccount).WithMany().HasForeignKey(x => x.ToBankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.MasterData.Branch>().WithMany().HasForeignKey(x => x.FromBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.MasterData.Branch>().WithMany().HasForeignKey(x => x.ToBranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BankAccounts");

        builder.Property(x => x.Code).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.ArabicName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.BankName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AccountNumber).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.Iban).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.IsActive);

        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);

        // Step 7: the general ledger account of the bank account (optional).
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
