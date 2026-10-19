using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Domain.Billing;
using ShippingERP.Domain.Entities.Billing;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Billing;

/// <summary>
/// Customer statement (from the sub-ledger) and the customer aging report (from the invoices, as of a date).
/// Branch users only see their branch's documents; the customer's opening balance (Customer.OpeningBalance, in the
/// base currency) is only part of the statement for users who see all branches.
/// </summary>
public sealed class ReceivablesReportService : IReceivablesReportService
{
    public const int MaxExportRows = 20_000;

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly BillingAccess _access;

    public ReceivablesReportService(MasterDataServiceDependencies dependencies, BillingAccess access)
    {
        _dependencies = dependencies;
        _access = access;
    }

    private AppDbContext Db => _dependencies.DbContext;

    private DateTime UtcNow => _dependencies.DateTimeProvider.UtcNow;

    // ------------------------------------------------------------------ statement

    public async Task<CustomerStatementDto> GetStatementAsync(long customerId, CustomerStatementQuery query, CancellationToken cancellationToken)
    {
        var (header, period, opening) = await PrepareStatementAsync(customerId, query, cancellationToken);
        var ordered = period.OrderBy(x => x.TransactionDateUtc).ThenBy(x => x.Id);

        var totalCount = await period.CountAsync(cancellationToken);
        var totalDebit = await period.SumAsync(x => x.Debit, cancellationToken);
        var totalCredit = await period.SumAsync(x => x.Credit, cancellationToken);

        var skip = (query.PageNumber - 1) * query.PageSize;
        var beforePage = skip == 0 ? 0m : await ordered.Take(skip).SumAsync(x => x.Debit - x.Credit, cancellationToken);

        var rows = await ordered
            .Skip(skip)
            .Take(query.PageSize)
            .Select(x => new
            {
                x.Id,
                x.TransactionDateUtc,
                x.TransactionType,
                x.DocumentType,
                x.DocumentId,
                x.DocumentNumber,
                x.Description,
                BranchName = x.Branch.ArabicName,
                x.Debit,
                x.Credit
            })
            .ToListAsync(cancellationToken);

        var balance = opening + beforePage;
        var lines = new List<StatementLineDto>(rows.Count);
        foreach (var row in rows)
        {
            balance += row.Debit - row.Credit;
            lines.Add(new StatementLineDto
            {
                TransactionId = row.Id,
                TransactionDateUtc = row.TransactionDateUtc,
                TransactionType = row.TransactionType,
                TransactionTypeName = BillingNames.Arabic(row.TransactionType),
                DocumentType = row.DocumentType,
                DocumentId = row.DocumentId,
                DocumentNumber = row.DocumentNumber,
                Description = row.Description,
                BranchName = row.BranchName,
                Debit = row.Debit,
                Credit = row.Credit,
                Balance = balance
            });
        }

        // Open documents right now (not limited to the period).
        var now = UtcNow;
        var currencyId = header.CurrencyId;
        var openInvoices = _access.Invoices()
            .Where(x => x.CustomerId == customerId && x.CurrencyId == currencyId &&
                        (x.Status == InvoiceStatus.Issued || x.Status == InvoiceStatus.PartiallyPaid || x.Status == InvoiceStatus.Overdue));
        var unallocated = await _access.Payments()
            .Where(x => x.CustomerId == customerId && x.CurrencyId == currencyId)
            .SumAsync(x => x.UnallocatedAmount, cancellationToken);
        var unapplied = await _access.CreditNotes()
            .Where(x => x.CustomerId == customerId && x.CurrencyId == currencyId)
            .SumAsync(x => x.UnappliedAmount, cancellationToken);

        return new CustomerStatementDto
        {
            CustomerId = customerId,
            CustomerCode = header.CustomerCode,
            CustomerName = header.CustomerName,
            CustomerPhone = header.CustomerPhone,
            CreditLimit = header.CreditLimit,
            CurrencyId = header.CurrencyId,
            CurrencyCode = header.CurrencyCode,
            FromUtc = query.FromUtc,
            ToUtc = header.ToUtc,
            OpeningBalance = opening,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            ClosingBalance = opening + totalDebit - totalCredit,
            OpenInvoiceCount = await openInvoices.CountAsync(cancellationToken),
            OpenInvoicesBalance = await openInvoices.SumAsync(x => x.BalanceDue, cancellationToken),
            OverdueBalance = await openInvoices.Where(x => x.DueDateUtc < now).SumAsync(x => x.BalanceDue, cancellationToken),
            UnappliedCredits = unallocated + unapplied,
            BranchId = _access.SeesAllBranches ? null : _access.BranchAccess.CurrentBranchId,
            Lines = new PagedResult<StatementLineDto>(lines, query.PageNumber, query.PageSize, totalCount)
        };
    }

    public async Task<FileDownload> ExportStatementAsync(long customerId, CustomerStatementQuery query, CancellationToken cancellationToken)
    {
        var (header, period, opening) = await PrepareStatementAsync(customerId, query, cancellationToken);

        var rows = await period
            .OrderBy(x => x.TransactionDateUtc)
            .ThenBy(x => x.Id)
            .Take(MaxExportRows)
            .Select(x => new { x.TransactionDateUtc, x.TransactionType, x.DocumentNumber, x.Description, x.Debit, x.Credit })
            .ToListAsync(cancellationToken);

        var balance = opening;
        var lines = rows.Select(x =>
        {
            balance += x.Debit - x.Credit;
            return new StatementLineDto
            {
                TransactionDateUtc = x.TransactionDateUtc,
                TransactionType = x.TransactionType,
                TransactionTypeName = BillingNames.Arabic(x.TransactionType),
                DocumentNumber = x.DocumentNumber,
                Description = x.Description,
                Debit = x.Debit,
                Credit = x.Credit,
                Balance = balance
            };
        }).ToList();

        var bytes = BillingExcelExporter.Statement(header.CustomerCode, header.CustomerName, header.CurrencyCode, query.FromUtc, header.ToUtc, opening, lines);
        return FileDownload.FromBytes(bytes, BillingExcelExporter.ContentType, $"statement-{header.CustomerCode}-{header.CurrencyCode}.xlsx");
    }

    private sealed record StatementHeader(
        string CustomerCode,
        string CustomerName,
        string CustomerPhone,
        decimal CreditLimit,
        long CurrencyId,
        string CurrencyCode,
        DateTime ToUtc);

    private async Task<(StatementHeader Header, IQueryable<CustomerAccountTransaction> Period, decimal Opening)> PrepareStatementAsync(
        long customerId,
        CustomerStatementQuery query,
        CancellationToken cancellationToken)
    {
        var customer = await Db.Customers.AsNoTracking()
                           .Where(x => x.Id == customerId)
                           .Select(x => new { x.Code, x.ArabicName, x.Phone, x.CreditLimit, x.OpeningBalance })
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw new NotFoundException("العميل", customerId);

        var currency = await _access.GetCurrencyAsync(query.CurrencyId, cancellationToken);
        var baseCurrency = await _access.GetBaseCurrencyAsync(cancellationToken);
        var to = query.ToUtc ?? UtcNow.AddSeconds(1);

        var transactions = _access.Transactions().Where(x => x.CustomerId == customerId && x.CurrencyId == currency.Id);

        var opening = currency.Id == baseCurrency.Id && _access.SeesAllBranches ? customer.OpeningBalance : 0m;
        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value;
            opening += await transactions.Where(x => x.TransactionDateUtc < from).SumAsync(x => x.Debit - x.Credit, cancellationToken);
            transactions = transactions.Where(x => x.TransactionDateUtc >= from);
        }

        var period = transactions.Where(x => x.TransactionDateUtc < to);
        var header = new StatementHeader(customer.Code, customer.ArabicName, customer.Phone, customer.CreditLimit, currency.Id, currency.Code, to);
        return (header, period, opening);
    }

    // ------------------------------------------------------------------ balances (step 9)

    public async Task<IReadOnlyList<CustomerBalanceDto>> GetBalancesAsync(long customerId, CancellationToken cancellationToken)
    {
        var openingBalance = await Db.Customers.AsNoTracking()
                                 .Where(x => x.Id == customerId)
                                 .Select(x => (decimal?)x.OpeningBalance)
                                 .FirstOrDefaultAsync(cancellationToken)
                             ?? throw new NotFoundException("العميل", customerId);

        var sums = await _access.Transactions()
            .Where(x => x.CustomerId == customerId)
            .GroupBy(x => x.CurrencyId)
            .Select(g => new { CurrencyId = g.Key, Balance = g.Sum(x => x.Debit - x.Credit) })
            .ToListAsync(cancellationToken);

        var balances = sums.ToDictionary(x => x.CurrencyId, x => x.Balance);
        if (openingBalance != 0m && _access.SeesAllBranches)
        {
            var baseCurrency = await _access.GetBaseCurrencyAsync(cancellationToken);
            balances[baseCurrency.Id] = balances.GetValueOrDefault(baseCurrency.Id) + openingBalance;
        }

        var ids = balances.Keys.ToList();
        var codes = await Db.Currencies.AsNoTracking().IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);

        return balances
            .Select(x => new CustomerBalanceDto { CurrencyId = x.Key, CurrencyCode = codes.GetValueOrDefault(x.Key, "?"), Balance = x.Value })
            .OrderBy(x => x.CurrencyCode, StringComparer.Ordinal)
            .ToList();
    }

    // ------------------------------------------------------------------ aging

    public async Task<CustomerAgingReportDto> GetCustomerAgingAsync(CustomerAgingQuery query, CancellationToken cancellationToken)
    {
        var asOf = query.AsOfUtc ?? UtcNow;
        var currency = await _access.GetCurrencyAsync(query.CurrencyId, cancellationToken);
        var currencyId = currency.Id;

        if (query.BranchId.HasValue)
        {
            _access.BranchAccess.EnsureCanAccess(query.BranchId.Value);
        }

        // ---------------- invoices issued on or before the date and not cancelled by then
        var invoices = _access.Invoices()
            .Where(x => x.CurrencyId == currencyId &&
                        x.IssuedAtUtc != null && x.InvoiceDateUtc <= asOf &&
                        (x.CancelledAtUtc == null || x.CancelledAtUtc > asOf));
        var payments = _access.Payments().Where(x => x.CurrencyId == currencyId && x.PaymentDateUtc <= asOf);
        var creditNotes = _access.CreditNotes().Where(x => x.CurrencyId == currencyId && x.CreditNoteDateUtc <= asOf);

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            invoices = invoices.Where(x => x.BranchId == branchId);
            payments = payments.Where(x => x.BranchId == branchId);
            creditNotes = creditNotes.Where(x => x.BranchId == branchId);
        }

        if (query.CustomerId.HasValue)
        {
            var customerId = query.CustomerId.Value;
            invoices = invoices.Where(x => x.CustomerId == customerId);
            payments = payments.Where(x => x.CustomerId == customerId);
            creditNotes = creditNotes.Where(x => x.CustomerId == customerId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            invoices = invoices.Where(x => x.Customer.Code.Contains(search) || x.Customer.ArabicName.Contains(search) || x.Customer.Phone.StartsWith(search));
            payments = payments.Where(x => x.Customer.Code.Contains(search) || x.Customer.ArabicName.Contains(search) || x.Customer.Phone.StartsWith(search));
            creditNotes = creditNotes.Where(x => x.Customer.Code.Contains(search) || x.Customer.ArabicName.Contains(search) || x.Customer.Phone.StartsWith(search));
        }

        // Balances as of the date: payments allocated and credit notes issued after it do not count yet.
        var invoiceRows = await invoices
            .Select(x => new
            {
                x.CustomerId,
                x.TotalAmount,
                x.DueDateUtc,
                Paid = x.PaymentAllocations.Where(a => a.AllocatedAtUtc <= asOf).Sum(a => (decimal?)a.Amount) ?? 0m,
                Credited = x.CreditNotes.Where(c => c.CreditNoteDateUtc <= asOf).Sum(c => (decimal?)c.AppliedToInvoiceAmount) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var unallocatedRows = await payments
            .Select(x => new
            {
                x.CustomerId,
                Unallocated = x.Amount - (x.Allocations.Where(a => a.AllocatedAtUtc <= asOf).Sum(a => (decimal?)a.Amount) ?? 0m)
            })
            .ToListAsync(cancellationToken);

        var unappliedRows = await creditNotes
            .Select(x => new { x.CustomerId, x.UnappliedAmount })
            .ToListAsync(cancellationToken);

        var asOfDate = asOf.Date;
        var buckets = new Dictionary<long, AgingAccumulator>();

        AgingAccumulator For(long customerId)
        {
            if (!buckets.TryGetValue(customerId, out var accumulator))
            {
                accumulator = new AgingAccumulator();
                buckets[customerId] = accumulator;
            }

            return accumulator;
        }

        foreach (var row in invoiceRows)
        {
            var balance = row.TotalAmount - row.Paid - row.Credited;
            if (balance <= 0m)
            {
                continue;
            }

            var accumulator = For(row.CustomerId);
            var daysPastDue = row.DueDateUtc.HasValue ? (asOfDate - row.DueDateUtc.Value.Date).Days : 0;
            accumulator.Add(balance, daysPastDue);
        }

        foreach (var row in unallocatedRows.Where(x => x.Unallocated > 0m))
        {
            For(row.CustomerId).UnappliedCredits += row.Unallocated;
        }

        foreach (var row in unappliedRows.Where(x => x.UnappliedAmount > 0m))
        {
            For(row.CustomerId).UnappliedCredits += row.UnappliedAmount;
        }

        if (query.CustomerId.HasValue && !buckets.ContainsKey(query.CustomerId.Value) && !query.OnlyWithBalance)
        {
            For(query.CustomerId.Value);
        }

        var customerIds = buckets.Keys.ToList();
        var customers = await Db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(x => customerIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Code, x.ArabicName, x.Phone, x.CreditLimit })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var allRows = buckets
            .Where(x => customers.ContainsKey(x.Key))
            .Select(x => x.Value.ToRow(x.Key, customers[x.Key].Code, customers[x.Key].ArabicName, customers[x.Key].Phone, customers[x.Key].CreditLimit))
            .Where(x => !query.OnlyWithBalance || x.TotalDue != 0m || x.UnappliedCredits != 0m)
            .ToList();

        var sorted = (query.SortBy?.ToLowerInvariant(), query.IsDescending()) switch
        {
            ("customername", true) => allRows.OrderByDescending(x => x.CustomerName, StringComparer.CurrentCulture),
            ("customername", false) => allRows.OrderBy(x => x.CustomerName, StringComparer.CurrentCulture),
            ("overdue", false) => allRows.OrderBy(x => x.Overdue),
            ("overdue", true) => allRows.OrderByDescending(x => x.Overdue),
            ("days90plus", false) => allRows.OrderBy(x => x.Days90Plus),
            ("days90plus", true) => allRows.OrderByDescending(x => x.Days90Plus),
            ("totaldue", false) => allRows.OrderBy(x => x.TotalDue),
            _ => allRows.OrderByDescending(x => x.TotalDue)
        };

        var page = sorted
            .ThenBy(x => x.CustomerCode, StringComparer.Ordinal)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var totals = new CustomerAgingRowDto
        {
            CustomerCode = string.Empty,
            CustomerName = "الإجمالي",
            Current = allRows.Sum(x => x.Current),
            Days1To30 = allRows.Sum(x => x.Days1To30),
            Days31To60 = allRows.Sum(x => x.Days31To60),
            Days61To90 = allRows.Sum(x => x.Days61To90),
            Days90Plus = allRows.Sum(x => x.Days90Plus),
            Overdue = allRows.Sum(x => x.Overdue),
            TotalDue = allRows.Sum(x => x.TotalDue),
            UnappliedCredits = allRows.Sum(x => x.UnappliedCredits),
            NetBalance = allRows.Sum(x => x.NetBalance),
            OpenInvoiceCount = allRows.Sum(x => x.OpenInvoiceCount),
            OldestDaysPastDue = allRows.Count == 0 ? 0 : allRows.Max(x => x.OldestDaysPastDue)
        };

        return new CustomerAgingReportDto
        {
            AsOfUtc = asOf,
            CurrencyId = currency.Id,
            CurrencyCode = currency.Code,
            BranchId = query.BranchId ?? (_access.SeesAllBranches ? null : _access.BranchAccess.CurrentBranchId),
            Totals = totals,
            Customers = new PagedResult<CustomerAgingRowDto>(page, query.PageNumber, query.PageSize, allRows.Count)
        };
    }

    /// <summary>Buckets by days past the due date: not due, 1-30, 31-60, 61-90, more than 90.</summary>
    private sealed class AgingAccumulator
    {
        public decimal Current { get; private set; }
        public decimal Days1To30 { get; private set; }
        public decimal Days31To60 { get; private set; }
        public decimal Days61To90 { get; private set; }
        public decimal Days90Plus { get; private set; }
        public decimal UnappliedCredits { get; set; }
        public int OpenInvoiceCount { get; private set; }
        public int OldestDaysPastDue { get; private set; }

        public void Add(decimal balance, int daysPastDue)
        {
            OpenInvoiceCount++;
            OldestDaysPastDue = Math.Max(OldestDaysPastDue, Math.Max(0, daysPastDue));

            switch (daysPastDue)
            {
                case <= 0:
                    Current += balance;
                    break;
                case <= 30:
                    Days1To30 += balance;
                    break;
                case <= 60:
                    Days31To60 += balance;
                    break;
                case <= 90:
                    Days61To90 += balance;
                    break;
                default:
                    Days90Plus += balance;
                    break;
            }
        }

        public CustomerAgingRowDto ToRow(long customerId, string code, string name, string phone, decimal creditLimit)
        {
            var overdue = Days1To30 + Days31To60 + Days61To90 + Days90Plus;
            var totalDue = Current + overdue;
            return new CustomerAgingRowDto
            {
                CustomerId = customerId,
                CustomerCode = code,
                CustomerName = name,
                CustomerPhone = phone,
                CreditLimit = creditLimit,
                Current = Current,
                Days1To30 = Days1To30,
                Days31To60 = Days31To60,
                Days61To90 = Days61To90,
                Days90Plus = Days90Plus,
                Overdue = overdue,
                TotalDue = totalDue,
                UnappliedCredits = UnappliedCredits,
                NetBalance = totalDue - UnappliedCredits,
                OpenInvoiceCount = OpenInvoiceCount,
                OldestDaysPastDue = OldestDaysPastDue
            };
        }
    }
}
