using ShippingERP.Domain.Entities.Billing;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Billing;

/// <summary>
/// Writes the customer sub-ledger (CustomerAccountTransactions). Like the audit log it only ADDS rows to the
/// DbContext: the caller saves them in the same transaction as the document that caused them.
/// </summary>
public sealed class CustomerLedger
{
    public const string InvoiceDocument = "Invoice";
    public const string PaymentDocument = "CustomerPayment";
    public const string CreditNoteDocument = "CreditNote";

    private readonly AppDbContext _dbContext;

    public CustomerLedger(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Debit: the invoice total.</summary>
    public void InvoiceIssued(Invoice invoice) => Add(
        invoice.CustomerId,
        invoice.BranchId,
        invoice.CurrencyId,
        invoice.InvoiceDateUtc!.Value,
        CustomerAccountTransactionType.InvoiceIssued,
        debit: invoice.TotalAmount,
        credit: 0m,
        InvoiceDocument,
        invoice.Id,
        invoice.InvoiceNumber!,
        $"فاتورة شحن رقم {invoice.InvoiceNumber}");

    /// <summary>Credit: reverses the invoice debit.</summary>
    public void InvoiceCancelled(Invoice invoice, DateTime dateUtc) => Add(
        invoice.CustomerId,
        invoice.BranchId,
        invoice.CurrencyId,
        dateUtc,
        CustomerAccountTransactionType.InvoiceCancelled,
        debit: 0m,
        credit: invoice.TotalAmount,
        InvoiceDocument,
        invoice.Id,
        invoice.InvoiceNumber!,
        $"إلغاء الفاتورة رقم {invoice.InvoiceNumber}");

    /// <summary>Credit: the money received (allocated or not).</summary>
    public void PaymentReceived(CustomerPayment payment, string paymentMethodName) => Add(
        payment.CustomerId,
        payment.BranchId,
        payment.CurrencyId,
        payment.PaymentDateUtc,
        CustomerAccountTransactionType.PaymentReceived,
        debit: 0m,
        credit: payment.Amount,
        PaymentDocument,
        payment.Id,
        payment.PaymentNumber,
        string.IsNullOrWhiteSpace(payment.ReferenceNumber)
            ? $"سند قبض ({paymentMethodName})"
            : $"سند قبض ({paymentMethodName}) - مرجع {payment.ReferenceNumber}");

    /// <summary>Credit: the credit note total.</summary>
    public void CreditNoteIssued(CreditNote creditNote, string? invoiceNumber) => Add(
        creditNote.CustomerId,
        creditNote.BranchId,
        creditNote.CurrencyId,
        creditNote.CreditNoteDateUtc,
        CustomerAccountTransactionType.CreditNoteIssued,
        debit: 0m,
        credit: creditNote.TotalAmount,
        CreditNoteDocument,
        creditNote.Id,
        creditNote.CreditNoteNumber,
        $"إشعار دائن على الفاتورة {invoiceNumber}: {Clip(creditNote.Reason, 200)}");

    private void Add(
        long customerId,
        long branchId,
        long currencyId,
        DateTime dateUtc,
        CustomerAccountTransactionType type,
        decimal debit,
        decimal credit,
        string documentType,
        long documentId,
        string documentNumber,
        string description)
    {
        _dbContext.CustomerAccountTransactions.Add(new CustomerAccountTransaction
        {
            CustomerId = customerId,
            BranchId = branchId,
            CurrencyId = currencyId,
            TransactionDateUtc = dateUtc,
            TransactionType = type,
            Debit = debit,
            Credit = credit,
            DocumentType = documentType,
            DocumentId = documentId,
            DocumentNumber = documentNumber,
            Description = Clip(description, 300)
        });
    }

    private static string Clip(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
