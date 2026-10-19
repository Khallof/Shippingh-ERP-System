using ShippingERP.Domain.Billing;
using ShippingERP.Domain.Entities.Billing;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Tests.Unit.Billing;

/// <summary>The invoice state machine (pure rules, no database).</summary>
public sealed class InvoiceStatusRulesTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Invoice Issued(decimal total, DateTime? dueDate = null) => new()
    {
        Id = 1,
        Status = InvoiceStatus.Issued,
        TotalAmount = total,
        BalanceDue = total,
        DueDateUtc = dueDate ?? Now.AddDays(30)
    };

    [Theory]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Issued, true)]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Cancelled, true)]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Paid, false)]
    [InlineData(InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid, true)]
    [InlineData(InvoiceStatus.Issued, InvoiceStatus.Overdue, true)]
    [InlineData(InvoiceStatus.Issued, InvoiceStatus.Draft, false)]
    [InlineData(InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid, true)]
    [InlineData(InvoiceStatus.PartiallyPaid, InvoiceStatus.Cancelled, false)]
    [InlineData(InvoiceStatus.Overdue, InvoiceStatus.Paid, true)]
    [InlineData(InvoiceStatus.Paid, InvoiceStatus.Credited, true)]
    [InlineData(InvoiceStatus.Paid, InvoiceStatus.Issued, false)]
    [InlineData(InvoiceStatus.Cancelled, InvoiceStatus.Issued, false)]
    [InlineData(InvoiceStatus.Credited, InvoiceStatus.Paid, false)]
    public void Transitions_FollowTheStateMachine(InvoiceStatus from, InvoiceStatus to, bool allowed)
    {
        Assert.Equal(allowed, InvoiceStatusRules.CanTransition(from, to));
    }

    [Fact]
    public void PartialPayment_ThenFullPayment_MovesIssuedToPartiallyPaidToPaid()
    {
        var invoice = Issued(10000m);

        invoice.PaidAmount = 4000m;
        Assert.True(InvoiceStatusRules.Refresh(invoice, Now));
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(6000m, invoice.BalanceDue);

        invoice.PaidAmount = 10000m;
        Assert.True(InvoiceStatusRules.Refresh(invoice, Now));
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(0m, invoice.BalanceDue);
        Assert.Equal(Now, invoice.PaidAtUtc);
    }

    [Fact]
    public void PastTheDueDate_AnOpenInvoiceIsOverdue_EvenWhenPartlyPaid()
    {
        var invoice = Issued(10000m, dueDate: Now.AddDays(-1));
        invoice.PaidAmount = 2500m;

        InvoiceStatusRules.Refresh(invoice, Now);

        Assert.Equal(InvoiceStatus.Overdue, invoice.Status);
        Assert.Equal(7500m, invoice.BalanceDue);
    }

    [Fact]
    public void CreditNotesCoveringTheTotal_MakeTheInvoiceCredited_EvenAfterPayment()
    {
        var invoice = Issued(10000m);
        invoice.PaidAmount = 10000m;
        InvoiceStatusRules.Refresh(invoice, Now);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);

        invoice.CreditedAmount = 10000m;
        InvoiceStatusRules.Refresh(invoice, Now);
        Assert.Equal(InvoiceStatus.Credited, invoice.Status);
    }

    [Fact]
    public void DraftsAndCancelledInvoices_NeverChangeByThemselves()
    {
        var draft = new Invoice { Status = InvoiceStatus.Draft, TotalAmount = 5000m, DueDateUtc = Now.AddDays(-10) };
        var cancelled = new Invoice { Status = InvoiceStatus.Cancelled, TotalAmount = 5000m, DueDateUtc = Now.AddDays(-10) };

        Assert.False(InvoiceStatusRules.Refresh(draft, Now));
        Assert.False(InvoiceStatusRules.Refresh(cancelled, Now));
        Assert.Equal(5000m, draft.BalanceDue);
        Assert.Equal(0m, cancelled.BalanceDue);
    }

    [Fact]
    public void Cancel_IsOnlyAllowedForDrafts_OrOpenInvoicesWithoutPaymentsAndCreditNotes()
    {
        Assert.True(InvoiceStatusRules.CanCancel(new Invoice { Status = InvoiceStatus.Draft }));
        Assert.True(InvoiceStatusRules.CanCancel(Issued(1000m)));

        var paid = Issued(1000m);
        paid.PaidAmount = 100m;
        Assert.False(InvoiceStatusRules.CanCancel(paid));

        var credited = Issued(1000m);
        credited.CreditedAmount = 100m;
        Assert.False(InvoiceStatusRules.CanCancel(credited));

        Assert.False(InvoiceStatusRules.CanCancel(new Invoice { Status = InvoiceStatus.Paid }));
    }
}
