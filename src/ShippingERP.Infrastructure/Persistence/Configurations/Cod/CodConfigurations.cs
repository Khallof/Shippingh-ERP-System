using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Cod;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Cod;

/// <summary>
/// COD transactions (step 6). One row per COD shipment (unique ShipmentId). Every foreign key is RESTRICT and
/// nothing is deleted: money documents are cancelled or reversed.
/// </summary>
public sealed class CODTransactionConfiguration : IEntityTypeConfiguration<CODTransaction>
{
    public void Configure(EntityTypeBuilder<CODTransaction> builder)
    {
        builder.ToTable("CODTransactions", table => table.HasCheckConstraint(
            "CK_CODTransactions_Amounts",
            "[ExpectedAmount] >= 0 AND [CollectedAmount] >= 0 AND [ShippingChargeDeduction] >= 0 AND [CodCommissionDeduction] >= 0 " +
            "AND [TaxDeduction] >= 0 AND [OtherFeesAmount] >= 0 AND [CompensationAmount] >= 0"));

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.StatusBeforeDispute).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.CollectionDifferenceReason).HasMaxLength(500);
        builder.Property(x => x.DisputeReason).HasMaxLength(500);

        // One COD record per shipment.
        builder.HasIndex(x => x.ShipmentId).IsUnique();

        builder.HasIndex(x => new { x.Status, x.CurrencyId });
        builder.HasIndex(x => new { x.SenderCustomerId, x.Status });
        builder.HasIndex(x => new { x.DriverId, x.Status });
        builder.HasIndex(x => new { x.OriginBranchId, x.Status });
        builder.HasIndex(x => x.ReceiptVoucherId);
        builder.HasIndex(x => x.CollectedAtUtc);

        builder.HasOne(x => x.Shipment).WithOne(x => x.CodTransaction).HasForeignKey<CODTransaction>(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SenderCustomer).WithMany().HasForeignKey(x => x.SenderCustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginBranch).WithMany().HasForeignKey(x => x.OriginBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReceivedCashbox).WithMany().HasForeignKey(x => x.ReceivedCashboxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReceiptVoucher).WithMany().HasForeignKey(x => x.ReceiptVoucherId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.History).WithOne(x => x.CODTransaction).HasForeignKey(x => x.CODTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.SettlementLines).WithOne(x => x.CODTransaction).HasForeignKey(x => x.CODTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CODTransactionHistoryConfiguration : IEntityTypeConfiguration<CODTransactionHistory>
{
    public void Configure(EntityTypeBuilder<CODTransactionHistory> builder)
    {
        builder.ToTable("CODTransactionHistory");

        builder.Property(x => x.TransactionType).HasConversion<string>().HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.ReferenceType).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => new { x.CODTransactionId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
    }
}

public sealed class CODAdjustmentConfiguration : IEntityTypeConfiguration<CODAdjustment>
{
    public void Configure(EntityTypeBuilder<CODAdjustment> builder)
    {
        builder.ToTable("CODAdjustments", table => table.HasCheckConstraint("CK_CODAdjustments_Amount", "[Amount] >= 0"));

        builder.Property(x => x.AdjustmentNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.AdjustmentType).HasConversion<string>().HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DecisionNotes).HasMaxLength(500);

        builder.HasIndex(x => x.AdjustmentNumber).IsUnique();
        builder.HasIndex(x => new { x.Status, x.BranchId });
        builder.HasIndex(x => x.CODTransactionId);
        builder.HasIndex(x => x.DriverId);

        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CODTransaction).WithMany().HasForeignKey(x => x.CODTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DriverCashAccountConfiguration : IEntityTypeConfiguration<DriverCashAccount>
{
    public void Configure(EntityTypeBuilder<DriverCashAccount> builder)
    {
        builder.ToTable("DriverCashAccounts");

        // One account per driver and currency.
        builder.HasIndex(x => new { x.DriverId, x.CurrencyId }).IsUnique();

        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Movements).WithOne(x => x.DriverCashAccount).HasForeignKey(x => x.DriverCashAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DriverCashMovementConfiguration : IEntityTypeConfiguration<DriverCashMovement>
{
    public void Configure(EntityTypeBuilder<DriverCashMovement> builder)
    {
        builder.ToTable("DriverCashMovements", table => table.HasCheckConstraint(
            "CK_DriverCashMovements_DebitOrCredit",
            "[Debit] >= 0 AND [Credit] >= 0 AND ([Debit] = 0 OR [Credit] = 0)"));

        builder.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.DocumentType).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.DocumentNumber).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();

        builder.HasIndex(x => new { x.DriverId, x.CurrencyId, x.MovementDateUtc });
        builder.HasIndex(x => new { x.DocumentType, x.DocumentId });
        builder.HasIndex(x => x.CODTransactionId);
    }
}

public sealed class SenderSettlementConfiguration : IEntityTypeConfiguration<SenderSettlement>
{
    public void Configure(EntityTypeBuilder<SenderSettlement> builder)
    {
        builder.ToTable("SenderSettlements", table => table.HasCheckConstraint("CK_SenderSettlements_NetPayable", "[NetPayable] > 0"));

        builder.Property(x => x.SettlementNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.PaymentReference).HasMaxLength(100);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.ReversalReason).HasMaxLength(500);

        builder.HasIndex(x => x.SettlementNumber).IsUnique();
        builder.HasIndex(x => new { x.SenderCustomerId, x.Status });
        builder.HasIndex(x => new { x.BranchId, x.SettlementDateUtc });
        builder.HasIndex(x => x.PaidAtUtc);

        builder.HasOne(x => x.SenderCustomer).WithMany().HasForeignKey(x => x.SenderCustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Cashbox).WithMany().HasForeignKey(x => x.CashboxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CashPaymentVoucher).WithMany().HasForeignKey(x => x.CashPaymentVoucherId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines).WithOne(x => x.SenderSettlement).HasForeignKey(x => x.SenderSettlementId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SenderSettlementLineConfiguration : IEntityTypeConfiguration<SenderSettlementLine>
{
    public void Configure(EntityTypeBuilder<SenderSettlementLine> builder)
    {
        builder.ToTable("SenderSettlementLines");

        builder.HasIndex(x => x.SenderSettlementId);

        // THE double-settlement guard: a COD can be reserved by only one open (Draft / Approved) settlement.
        builder.HasIndex(x => x.CODTransactionId)
            .IsUnique()
            .HasFilter("[IsOpen] = 1")
            .HasDatabaseName("UX_SenderSettlementLines_OpenCod");

        builder.HasOne<Domain.Entities.Shipments.Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
