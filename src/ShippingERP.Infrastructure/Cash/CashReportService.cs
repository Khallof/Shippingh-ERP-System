using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Cash;

/// <summary>
/// Daily cash movement report (step 6): for every visible cashbox and local business day, the opening balance,
/// the money in and out by kind, the closing balance and the result of the daily count.
/// Built from the append-only cashbox transactions, so it always matches the cashbox statements.
/// </summary>
public sealed class CashReportService : ICashReportService
{
    private readonly MasterDataServiceDependencies _dependencies;
    private readonly CashAccess _access;
    private readonly BusinessCalendar _calendar;

    public CashReportService(MasterDataServiceDependencies dependencies, CashAccess access, BusinessCalendar calendar)
    {
        _dependencies = dependencies;
        _access = access;
        _calendar = calendar;
    }

    private AppDbContext Db => _dependencies.DbContext;

    public async Task<DailyCashMovementReportDto> GetDailyCashMovementAsync(DailyCashMovementQuery query, CancellationToken cancellationToken)
    {
        var today = _calendar.BusinessDate(_dependencies.DateTimeProvider.UtcNow);
        var to = query.ToDate.HasValue ? BusinessCalendar.AsDate(query.ToDate.Value) : today;
        var from = query.FromDate.HasValue ? BusinessCalendar.AsDate(query.FromDate.Value) : to.AddDays(-6);

        var cashboxQuery = _access.Cashboxes();
        if (query.CashboxId.HasValue)
        {
            var cashboxId = query.CashboxId.Value;
            cashboxQuery = cashboxQuery.Where(x => x.Id == cashboxId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            cashboxQuery = cashboxQuery.Where(x => x.BranchId == branchId);
        }

        if (query.CurrencyId.HasValue)
        {
            var currencyId = query.CurrencyId.Value;
            cashboxQuery = cashboxQuery.Where(x => x.CurrencyId == currencyId);
        }

        var cashboxes = await cashboxQuery
            .OrderBy(x => x.Code)
            .Select(x => new { x.Id, x.Code, x.ArabicName, BranchName = x.Branch.ArabicName, CurrencyCode = x.Currency.Code })
            .ToListAsync(cancellationToken);
        var ids = cashboxes.Select(x => x.Id).ToList();

        // Opening balance of the first day = balance after the last transaction before it.
        var lastBefore = await Db.CashboxTransactions.AsNoTracking()
            .Where(x => ids.Contains(x.CashboxId) && x.BusinessDate < from)
            .GroupBy(x => x.CashboxId)
            .Select(g => g.Max(x => x.Id))
            .ToListAsync(cancellationToken);
        var openings = await Db.CashboxTransactions.AsNoTracking()
            .Where(x => lastBefore.Contains(x.Id))
            .ToDictionaryAsync(x => x.CashboxId, x => x.BalanceAfter, cancellationToken);

        var transactions = await Db.CashboxTransactions.AsNoTracking()
            .Where(x => ids.Contains(x.CashboxId) && x.BusinessDate >= from && x.BusinessDate <= to)
            .Select(x => new { x.CashboxId, x.BusinessDate, x.TransactionType, x.AmountIn, x.AmountOut })
            .ToListAsync(cancellationToken);

        var closings = await Db.CashboxDailyClosings.AsNoTracking()
            .Where(x => ids.Contains(x.CashboxId) && x.BusinessDate >= from && x.BusinessDate <= to && x.Status != CashboxClosingStatus.Rejected)
            .Select(x => new { x.CashboxId, x.BusinessDate, x.Status, x.CountedBalance, x.Discrepancy })
            .ToListAsync(cancellationToken);

        var rows = new List<DailyCashMovementRowDto>();
        foreach (var cashbox in cashboxes)
        {
            var balance = openings.TryGetValue(cashbox.Id, out var opening) ? opening : 0m;

            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var dayRows = transactions.Where(x => x.CashboxId == cashbox.Id && x.BusinessDate == day).ToList();
                var closing = closings.FirstOrDefault(x => x.CashboxId == cashbox.Id && x.BusinessDate == day);

                decimal In(params CashboxTransactionType[] types) => dayRows.Where(x => types.Contains(x.TransactionType)).Sum(x => x.AmountIn);
                decimal Out(params CashboxTransactionType[] types) => dayRows.Where(x => types.Contains(x.TransactionType)).Sum(x => x.AmountOut);

                var totalIn = dayRows.Sum(x => x.AmountIn);
                var totalOut = dayRows.Sum(x => x.AmountOut);
                var handovers = In(CashboxTransactionType.DriverHandover);
                var counter = In(CashboxTransactionType.CounterCodCollection);
                var receipts = In(CashboxTransactionType.CashReceipt);
                var transfersIn = In(CashboxTransactionType.TransferIn);
                var settlements = Out(CashboxTransactionType.SenderSettlementPayment);
                var payments = Out(CashboxTransactionType.CashPayment);
                var transfersOut = Out(CashboxTransactionType.TransferOut);

                var openingOfDay = balance;
                balance += totalIn - totalOut;

                if (!query.IncludeEmptyDays && dayRows.Count == 0 && closing is null)
                {
                    continue;
                }

                rows.Add(new DailyCashMovementRowDto
                {
                    CashboxId = cashbox.Id,
                    CashboxCode = cashbox.Code,
                    CashboxName = cashbox.ArabicName,
                    BranchName = cashbox.BranchName,
                    CurrencyCode = cashbox.CurrencyCode,
                    BusinessDate = day,
                    OpeningBalance = openingOfDay,
                    DriverHandovers = handovers,
                    CounterCollections = counter,
                    Receipts = receipts,
                    TransfersIn = transfersIn,
                    OtherIn = totalIn - handovers - counter - receipts - transfersIn,
                    TotalIn = totalIn,
                    SettlementPayments = settlements,
                    Payments = payments,
                    TransfersOut = transfersOut,
                    OtherOut = totalOut - settlements - payments - transfersOut,
                    TotalOut = totalOut,
                    ClosingBalance = balance,
                    TransactionCount = dayRows.Count,
                    ClosingStatus = closing?.Status,
                    CountedBalance = closing?.CountedBalance,
                    Discrepancy = closing?.Discrepancy
                });
            }
        }

        var totals = rows
            .GroupBy(x => x.CurrencyCode)
            .Select(g => new DailyCashCurrencyTotalDto
            {
                CurrencyCode = g.Key,
                TotalIn = g.Sum(x => x.TotalIn),
                TotalOut = g.Sum(x => x.TotalOut),
                Net = g.Sum(x => x.TotalIn - x.TotalOut)
            })
            .OrderBy(x => x.CurrencyCode, StringComparer.Ordinal)
            .ToList();

        return new DailyCashMovementReportDto { FromDate = from, ToDate = to, TimeZoneId = _calendar.ZoneId, Rows = rows, Totals = totals };
    }
}
