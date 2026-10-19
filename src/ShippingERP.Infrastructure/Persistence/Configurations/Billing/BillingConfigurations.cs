using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Billing;
using ShippingERP.Domain.Entities.Integration;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Billing;

/// <summary>
/// Invoices. Every foreign key is RESTRICT and nothing is ever deleted: invoices are cancelled or credited.
/// The invoice number is unique once given (drafts have none).
/// </summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", table => table.HasCheckConstraint(
            "CK_Invoices_Amounts",
            "[PaidAmount] >= 0 AND [CreditAppliedAmount] >= 0 AND [BalanceDue] >= 0 AND [PaidAmount] + [CreditAppliedAmount] <= [TotalAmount]"));

        builder.Property(x => x.InvoiceNumber).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);

        builder.HasIndex(x => x.InvoiceNumber)
            .IsUnique()
            .HasFilter("[InvoiceNumber] IS NOT NULL")
            .HasDatabaseName("UX_Invoices_InvoiceNumber");

        builder.HasIndex(x => new { x.CustomerId, x.Status });
        builder.HasIndex(x => new { x.BranchId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.Status, x.DueDateUtc });
        builder.HasIndex(x => x.InvoiceDateUtc);

        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines).WithOne(x => x.Invoice).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.PaymentAllocations).WithOne(x => x.Invoice).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.CreditNotes).WithOne(x => x.Invoice).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");

        builder.Property(x => x.ChargeType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.TaxPercentage).HasPrecision(5, 2);

        builder.HasIndex(x => x.InvoiceId);

        // THE double-billing guard: one active line per shipment and charge type, across all invoices.
        // Lines of cancelled invoices are inactive, so the shipment can be invoiced again.
        builder.HasIndex(x => new { x.ShipmentId, x.ChargeType })
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("UX_InvoiceLines_ActiveCharge");

        builder.HasOne(x => x.Shipment).WithMany(x => x.InvoiceLines).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PricingSnapshot).WithMany().HasForeignKey(x => x.PricingSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.MasterData.TaxRate>().WithMany().HasForeignKey(x => x.TaxRateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.ToTable("CreditNotes");

        builder.Property(x => x.CreditNoteNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();

        builder.HasIndex(x => x.CreditNoteNumber).IsUnique();
        builder.HasIndex(x => x.InvoiceId);
        builder.HasIndex(x => new { x.CustomerId, x.CreditNoteDateUtc });

        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(x => x.CreditNote).HasForeignKey(x => x.CreditNoteId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CreditNoteLineConfiguration : IEntityTypeConfiguration<CreditNoteLine>
{
    public void Configure(EntityTypeBuilder<CreditNoteLine> builder)
    {
        builder.ToTable("CreditNoteLines");

        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.TaxPercentage).HasPrecision(5, 2);

        builder.HasIndex(x => x.CreditNoteId);
        builder.HasIndex(x => x.InvoiceLineId);

        builder.HasOne(x => x.InvoiceLine).WithMany().HasForeignKey(x => x.InvoiceLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Shipments.Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerPaymentConfiguration : IEntityTypeConfiguration<CustomerPayment>
{
    public void Configure(EntityTypeBuilder<CustomerPayment> builder)
    {
        builder.ToTable("CustomerPayments", table => table.HasCheckConstraint(
            "CK_CustomerPayments_Amounts",
            "[Amount] > 0 AND [AllocatedAmount] >= 0 AND [UnallocatedAmount] >= 0 AND [AllocatedAmount] + [UnallocatedAmount] = [Amount]"));

        builder.Property(x => x.PaymentNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false);

        builder.HasIndex(x => x.PaymentNumber).IsUnique();

        // Two requests with the same key can never both be saved, even at the same millisecond.
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_CustomerPayments_IdempotencyKey");

        builder.HasIndex(x => new { x.CustomerId, x.PaymentDateUtc });
        builder.HasIndex(x => new { x.BranchId, x.PaymentDateUtc });

        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Allocations).WithOne(x => x.Payment).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerPaymentAllocationConfiguration : IEntityTypeConfiguration<CustomerPaymentAllocation>
{
    public void Configure(EntityTypeBuilder<CustomerPaymentAllocation> builder)
    {
        builder.ToTable("CustomerPaymentAllocations", table => table.HasCheckConstraint(
            "CK_CustomerPaymentAllocations_Amount", "[Amount] > 0"));

        builder.HasIndex(x => x.PaymentId);
        builder.HasIndex(x => new { x.InvoiceId, x.AllocatedAtUtc });
    }
}

public sealed class CustomerAccountTransactionConfiguration : IEntityTypeConfiguration<CustomerAccountTransaction>
{
    public void Configure(EntityTypeBuilder<CustomerAccountTransaction> builder)
    {
        builder.ToTable("CustomerAccountTransactions", table => table.HasCheckConstraint(
            "CK_CustomerAccountTransactions_DebitOrCredit",
            "[Debit] >= 0 AND [Credit] >= 0 AND ([Debit] = 0 OR [Credit] = 0)"));

        builder.Property(x => x.TransactionType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.DocumentType).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.DocumentNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();

        // Statement: one customer, one currency, by date.
        builder.HasIndex(x => new { x.CustomerId, x.CurrencyId, x.TransactionDateUtc });
        builder.HasIndex(x => new { x.DocumentType, x.DocumentId });
        builder.HasIndex(x => new { x.BranchId, x.TransactionDateUtc });

        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.Property(x => x.EventType).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);

        builder.HasIndex(x => x.EventId).IsUnique();

        // The dispatcher reads the unprocessed messages in order.
        builder.HasIndex(x => x.Id)
            .HasFilter("[ProcessedAtUtc] IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Unprocessed");
    }
}
