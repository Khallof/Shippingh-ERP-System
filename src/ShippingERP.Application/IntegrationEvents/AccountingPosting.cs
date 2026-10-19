namespace ShippingERP.Application.IntegrationEvents;

/// <summary>
/// The operational "accounts" a posting request uses (step 6). They are ROLES, not general-ledger account numbers:
/// the accounting module (step 7) maps each role (plus the branch / currency / cashbox / bank / expense category of
/// the line) to a real GL account with the AccountingPostingRules table. Keeping roles here means the operational
/// modules never need the chart of accounts.
/// </summary>
public static class AccountRoles
{
    /// <summary>Cash in a branch cashbox (line has CashboxId).</summary>
    public const string CashOnHand = "CashOnHand";

    /// <summary>Cash sent to another branch and not received yet.</summary>
    public const string CashInTransit = "CashInTransit";

    /// <summary>A company bank account (line has BankAccountId).</summary>
    public const string Bank = "Bank";

    /// <summary>Cash held by a driver (عهدة السائقين, line has DriverId).</summary>
    public const string DriverCustody = "DriverCustody";

    /// <summary>COD money held on behalf of senders (أمانات المرسلين - a liability, line has CustomerId).</summary>
    public const string CodPayableToSenders = "CodPayableToSenders";

    public const string ShippingRevenue = "ShippingRevenue";

    public const string CodCommissionRevenue = "CodCommissionRevenue";

    public const string TaxPayable = "TaxPayable";

    /// <summary>Approved extra fees deducted from senders (return, storage, damaged goods...).</summary>
    public const string OtherFeesRevenue = "OtherFeesRevenue";

    /// <summary>Approved compensation paid to senders.</summary>
    public const string SenderCompensationExpense = "SenderCompensationExpense";

    /// <summary>Driver shortage written off (loss, or a receivable from the employee: decided by accounting).</summary>
    public const string DriverShortageLoss = "DriverShortageLoss";

    /// <summary>Cash over / short (فروقات النقدية): excess cash of drivers and cashbox count differences.</summary>
    public const string CashOverShort = "CashOverShort";

    /// <summary>Customer cash payments of step 5 waiting to be deposited in a cashbox.</summary>
    public const string CashClearing = "CashClearing";

    public const string OtherIncome = "OtherIncome";

    /// <summary>Expense of an expense category (line has ExpenseCategoryId).</summary>
    public const string Expense = "Expense";

    /// <summary>Supplier payable (line has SupplierId).</summary>
    public const string AccountsPayable = "AccountsPayable";

    /// <summary>Employee advances (line has EmployeeId).</summary>
    public const string EmployeeAdvances = "EmployeeAdvances";

    /// <summary>Customer advances / refunds (line has CustomerId).</summary>
    public const string CustomerAdvances = "CustomerAdvances";

    /// <summary>Opening balances entered when a cashbox is created.</summary>
    public const string OpeningBalanceEquity = "OpeningBalanceEquity";

    /// <summary>Anything that needs a manual classification by the accountant.</summary>
    public const string Suspense = "Suspense";

    // Step 7 (accounting): roles of the receivables events and of refunds.

    /// <summary>Customer receivables of issued invoices (line has CustomerId).</summary>
    public const string AccountsReceivable = "AccountsReceivable";

    /// <summary>Revenue of the additional services of a shipment (pickup, packaging, insurance, remote area...).</summary>
    public const string AdditionalServiceRevenue = "AdditionalServiceRevenue";

    /// <summary>Money paid back to a customer (refund expense by default; map it to receivables if preferred).</summary>
    public const string CustomerRefund = "CustomerRefund";

    /// <summary>Every role, for validation and for the posting rules screen.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        CashOnHand, CashInTransit, Bank, DriverCustody, CodPayableToSenders, ShippingRevenue, CodCommissionRevenue,
        TaxPayable, OtherFeesRevenue, SenderCompensationExpense, DriverShortageLoss, CashOverShort, CashClearing,
        OtherIncome, Expense, AccountsPayable, EmployeeAdvances, CustomerAdvances, OpeningBalanceEquity, Suspense,
        AccountsReceivable, AdditionalServiceRevenue, CustomerRefund
    };

    public static bool IsKnown(string? role) =>
        role is not null && All.Contains(role, StringComparer.OrdinalIgnoreCase);
}

/// <summary>One line of a posting request. Exactly one of Debit / Credit is &gt; 0.</summary>
public sealed record PostingLine
{
    public string AccountRole { get; init; } = string.Empty;

    public decimal Debit { get; init; }

    public decimal Credit { get; init; }

    public long? BranchId { get; init; }

    public long? CashboxId { get; init; }

    public long? BankAccountId { get; init; }

    public long? DriverId { get; init; }

    public long? CustomerId { get; init; }

    public long? SupplierId { get; init; }

    public long? EmployeeId { get; init; }

    public long? ExpenseCategoryId { get; init; }

    public string? Description { get; init; }
}

/// <summary>
/// A balanced journal entry PROPOSED by an operational document (AccountingPostingRequest). The operations
/// module never writes the general ledger: it attaches this request to its integration event (in the outbox, in
/// the same transaction as the money movement), and the accounting module (step 7, AccountingPostingProcessor)
/// turns it into a posted journal entry. Sum(Debit) always equals Sum(Credit): <see cref="Create"/> refuses anything else.
/// </summary>
public sealed record AccountingPostingRequest
{
    /// <summary>"CashReceiptVoucher", "SenderSettlement", "CashTransfer"...</summary>
    public string SourceType { get; init; } = string.Empty;

    public long SourceId { get; init; }

    public string SourceNumber { get; init; } = string.Empty;

    public long CurrencyId { get; init; }

    public DateTime PostingDateUtc { get; init; }

    public string Description { get; init; } = string.Empty;

    /// <summary>True when this request cancels an earlier one (cancelled voucher, reversed settlement...).</summary>
    public bool IsReversal { get; init; }

    public IReadOnlyList<PostingLine> Lines { get; init; } = Array.Empty<PostingLine>();

    public decimal TotalDebit => Lines.Sum(x => x.Debit);

    public decimal TotalCredit => Lines.Sum(x => x.Credit);

    /// <summary>
    /// Builds a request from lines; zero lines are dropped and a line with a negative amount is moved to the other
    /// side. Throws when the result is not balanced (a programming error, never a user error).
    /// </summary>
    public static AccountingPostingRequest Create(
        string sourceType,
        long sourceId,
        string sourceNumber,
        long currencyId,
        DateTime postingDateUtc,
        string description,
        IEnumerable<PostingLine> lines,
        bool isReversal = false)
    {
        var normalized = new List<PostingLine>();
        foreach (var line in lines)
        {
            var net = line.Debit - line.Credit;
            if (net == 0m)
            {
                continue;
            }

            normalized.Add(net > 0m ? line with { Debit = net, Credit = 0m } : line with { Debit = 0m, Credit = -net });
        }

        var request = new AccountingPostingRequest
        {
            SourceType = sourceType,
            SourceId = sourceId,
            SourceNumber = sourceNumber,
            CurrencyId = currencyId,
            PostingDateUtc = postingDateUtc,
            Description = description,
            IsReversal = isReversal,
            Lines = normalized
        };

        if (request.TotalDebit != request.TotalCredit)
        {
            throw new InvalidOperationException(
                $"Unbalanced posting request for {sourceType} {sourceNumber}: debit {request.TotalDebit} <> credit {request.TotalCredit}.");
        }

        return request;
    }

    /// <summary>The opposite entry (debits become credits), for cancellations and reversals.</summary>
    public AccountingPostingRequest Reverse(string description, DateTime postingDateUtc) => this with
    {
        Description = description,
        PostingDateUtc = postingDateUtc,
        IsReversal = !IsReversal,
        Lines = Lines.Select(x => x with { Debit = x.Credit, Credit = x.Debit }).ToList()
    };

    public static PostingLine Debit(string role, decimal amount, long? branchId = null) =>
        new() { AccountRole = role, Debit = amount, BranchId = branchId };

    public static PostingLine Credit(string role, decimal amount, long? branchId = null) =>
        new() { AccountRole = role, Credit = amount, BranchId = branchId };
}

/// <summary>
/// Base of the step-6 money events. Every operational money movement (collection, handover, voucher, transfer,
/// settlement, cash difference) publishes one, carrying the proposed journal entry. The accounting module
/// (step 7) posts <see cref="Posting"/> once per EventId.
/// </summary>
public abstract record AccountingIntegrationEvent : IntegrationEvent
{
    public AccountingPostingRequest Posting { get; init; } = new();
}
