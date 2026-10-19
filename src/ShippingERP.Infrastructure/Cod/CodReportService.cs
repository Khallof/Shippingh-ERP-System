using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Cod;

/// <summary>
/// COD dashboards and reports (step 6): driver balances and movements, sender balances, COD aging, the COD
/// dashboard, the driver collection / handover report and the sender settlement report. Read-only; every query
/// applies the same visibility rules as the documents (CashAccess).
///
/// Amount meaning by stage (also used by the dashboard):
///   not collected (Pending / AssignedToDriver) -> expected COD amount
///   collected / in cashbox / disputed          -> collected amount (sender money held)
///   ready / partially settled                  -> remaining NET amount to pay
/// </summary>
public sealed class CodReportService : ICodReportService
{
    private const int MaxReportRows = 5000;

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly CashAccess _access;
    private readonly BusinessCalendar _calendar;

    public CodReportService(MasterDataServiceDependencies dependencies, CashAccess access, BusinessCalendar calendar)
    {
        _dependencies = dependencies;
        _access = access;
        _calendar = calendar;
    }

    /// <summary>Default period: the last 30 local business days, today included (end = end of today).</summary>
    private (DateTime From, DateTime To) Period(DateTime? fromUtc, DateTime? toUtc)
    {
        var to = toUtc ?? _calendar.DayRangeUtc(_calendar.BusinessDate(UtcNow)).EndUtc;
        var from = fromUtc ?? _calendar.DayRangeUtc(_calendar.BusinessDate(to.AddTicks(-1)).AddDays(-29)).StartUtc;
        return (from, to);
    }

    private AppDbContext Db => _dependencies.DbContext;

    private DateTime UtcNow => _dependencies.DateTimeProvider.UtcNow;

    // ------------------------------------------------------------------ drivers

    public async Task<PagedResult<DriverBalanceDto>> GetDriverBalancesAsync(DriverBalanceQuery query, CancellationToken cancellationToken)
    {
        var rows = await _access.DriverAccountsAsync(cancellationToken);

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.Driver.Employee.BranchId == branchId);
        }

        if (query.CurrencyId.HasValue)
        {
            var currencyId = query.CurrencyId.Value;
            rows = rows.Where(x => x.CurrencyId == currencyId);
        }

        if (query.DriverId.HasValue)
        {
            var driverId = query.DriverId.Value;
            rows = rows.Where(x => x.DriverId == driverId);
        }

        if (query.OnlyOutstanding)
        {
            rows = rows.Where(x => x.Balance != 0m);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Driver.Employee.ArabicName.Contains(search) || x.Driver.Employee.Code.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var descending = query.SortDirection is null || query.IsDescending();
        var ordered = (query.SortBy ?? "balance").ToLowerInvariant() switch
        {
            "drivername" => query.IsDescending() ? rows.OrderByDescending(x => x.Driver.Employee.ArabicName) : rows.OrderBy(x => x.Driver.Employee.ArabicName),
            "lastmovementatutc" => descending ? rows.OrderByDescending(x => x.LastMovementAtUtc) : rows.OrderBy(x => x.LastMovementAtUtc),
            _ => descending ? rows.OrderByDescending(x => x.Balance) : rows.OrderBy(x => x.Balance)
        };

        var page = await ordered
            .ThenBy(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new
            {
                x.DriverId,
                DriverName = x.Driver.Employee.ArabicName,
                EmployeeCode = x.Driver.Employee.Code,
                x.Driver.Employee.BranchId,
                BranchName = x.Driver.Employee.Branch.ArabicName,
                x.CurrencyId,
                CurrencyCode = x.Currency.Code,
                x.Balance,
                x.LastHandoverAtUtc,
                x.LastMovementAtUtc
            })
            .ToListAsync(cancellationToken);

        var driverIds = page.Select(x => (long?)x.DriverId).Distinct().ToList();
        var unhanded = await Db.CODTransactions.AsNoTracking()
            .Where(x => x.Status == CODStatus.CollectedByDriver && driverIds.Contains(x.DriverId))
            .GroupBy(x => new { x.DriverId, x.CurrencyId })
            .Select(g => new { g.Key.DriverId, g.Key.CurrencyId, Count = g.Count(), Amount = g.Sum(x => x.CollectedAmount), Oldest = g.Min(x => x.CollectedAtUtc) })
            .ToListAsync(cancellationToken);

        var items = page.Select(x =>
        {
            var open = unhanded.FirstOrDefault(u => u.DriverId == x.DriverId && u.CurrencyId == x.CurrencyId);
            return new DriverBalanceDto
            {
                DriverId = x.DriverId,
                DriverName = x.DriverName,
                EmployeeCode = x.EmployeeCode,
                BranchId = x.BranchId,
                BranchName = x.BranchName,
                CurrencyId = x.CurrencyId,
                CurrencyCode = x.CurrencyCode,
                Balance = x.Balance,
                UnhandedCodCount = open?.Count ?? 0,
                UnhandedCodAmount = open?.Amount ?? 0m,
                OldestUnhandedCollectionAtUtc = open?.Oldest,
                LastHandoverAtUtc = x.LastHandoverAtUtc,
                LastMovementAtUtc = x.LastMovementAtUtc
            };
        }).ToList();

        return new PagedResult<DriverBalanceDto>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<PagedResult<DriverCashMovementDto>> GetDriverMovementsAsync(long driverId, DriverMovementQuery query, CancellationToken cancellationToken)
    {
        await EnsureDriverVisibleAsync(driverId, cancellationToken);

        var rows = Db.DriverCashMovements.AsNoTracking().Where(x => x.DriverId == driverId);

        if (query.CurrencyId.HasValue)
        {
            var currencyId = query.CurrencyId.Value;
            rows = rows.Where(x => x.CurrencyId == currencyId);
        }

        if (query.MovementType.HasValue)
        {
            var type = query.MovementType.Value;
            rows = rows.Where(x => x.MovementType == type);
        }

        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value;
            rows = rows.Where(x => x.MovementDateUtc >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = query.ToUtc.Value;
            rows = rows.Where(x => x.MovementDateUtc < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.DocumentNumber.Contains(search) || x.Description.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new DriverCashMovementDto
            {
                Id = x.Id,
                MovementType = x.MovementType,
                CurrencyCode = x.DriverCashAccount.Currency.Code,
                Debit = x.Debit,
                Credit = x.Credit,
                BalanceAfter = x.BalanceAfter,
                MovementDateUtc = x.MovementDateUtc,
                CodTransactionId = x.CODTransactionId,
                DocumentType = x.DocumentType,
                DocumentId = x.DocumentId,
                DocumentNumber = x.DocumentNumber,
                Description = x.Description,
                CreatedByUserId = x.CreatedByUserId
            })
            .ToListAsync(cancellationToken);

        var named = items.Select(x => new DriverCashMovementDto
        {
            Id = x.Id,
            MovementType = x.MovementType,
            MovementTypeName = CodNames.Arabic(x.MovementType),
            CurrencyCode = x.CurrencyCode,
            Debit = x.Debit,
            Credit = x.Credit,
            BalanceAfter = x.BalanceAfter,
            MovementDateUtc = x.MovementDateUtc,
            CodTransactionId = x.CodTransactionId,
            DocumentType = x.DocumentType,
            DocumentId = x.DocumentId,
            DocumentNumber = x.DocumentNumber,
            Description = x.Description,
            CreatedByUserId = x.CreatedByUserId
        }).ToList();

        return new PagedResult<DriverCashMovementDto>(named, query.PageNumber, query.PageSize, totalCount);
    }

    private async Task EnsureDriverVisibleAsync(long driverId, CancellationToken cancellationToken)
    {
        if (_access.IsDriverOnly)
        {
            if (await _access.CurrentDriverIdAsync(cancellationToken) != driverId)
            {
                throw new NotFoundException("السائق", driverId);
            }

            return;
        }

        var drivers = Db.DriverProfiles.AsNoTracking().Where(x => x.Id == driverId);
        if (!_access.SeesAllBranches)
        {
            var branchId = _access.BranchAccess.CurrentBranchId;
            drivers = drivers.Where(x => x.Employee.BranchId == branchId);
        }

        if (!await drivers.AnyAsync(cancellationToken))
        {
            throw new NotFoundException("السائق", driverId);
        }
    }

    // ------------------------------------------------------------------ senders

    public async Task<PagedResult<SenderBalanceDto>> GetSenderBalancesAsync(SenderBalanceQuery query, CancellationToken cancellationToken)
    {
        var cods = (await _access.CodsAsync(tracked: false, cancellationToken))
            .Where(x => x.Status != CODStatus.Cancelled && x.Status != CODStatus.SettledToSender);

        if (query.SenderCustomerId.HasValue)
        {
            var senderId = query.SenderCustomerId.Value;
            cods = cods.Where(x => x.SenderCustomerId == senderId);
        }

        if (query.CurrencyId.HasValue)
        {
            var currencyId = query.CurrencyId.Value;
            cods = cods.Where(x => x.CurrencyId == currencyId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            cods = cods.Where(x => x.OriginBranchId == branchId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            cods = cods.Where(x => x.SenderCustomer.Code.Contains(search) || x.SenderCustomer.ArabicName.Contains(search));
        }

        var groups = await cods
            .GroupBy(x => new { x.SenderCustomerId, x.CurrencyId })
            .Select(g => new
            {
                g.Key.SenderCustomerId,
                g.Key.CurrencyId,
                PendingCount = g.Count(x => x.Status == CODStatus.Pending || x.Status == CODStatus.AssignedToDriver),
                PendingAmount = g.Sum(x => x.Status == CODStatus.Pending || x.Status == CODStatus.AssignedToDriver ? x.ExpectedAmount : 0m),
                WithDrivers = g.Sum(x => x.Status == CODStatus.CollectedByDriver ? x.CollectedAmount : 0m),
                InCashbox = g.Sum(x => x.Status == CODStatus.ReceivedAtBranchCashbox ? x.CollectedAmount : 0m),
                ReadyNet = g.Sum(x => x.Status == CODStatus.ReadyForSenderSettlement || x.Status == CODStatus.PartiallySettled ? x.NetPayableAmount - x.SettledAmount : 0m),
                Disputed = g.Sum(x => x.Status == CODStatus.Disputed ? x.CollectedAmount : 0m),
                Held = g.Sum(x => x.Status == CODStatus.Pending || x.Status == CODStatus.AssignedToDriver
                    ? 0m
                    : x.DeductionsRecognized ? x.NetPayableAmount - x.SettledAmount : x.CollectedAmount)
            })
            .ToListAsync(cancellationToken);

        if (query.OnlyWithBalance)
        {
            groups = groups.Where(x => x.PendingCount > 0 || x.Held != 0m).ToList();
        }

        var senderIds = groups.Select(x => x.SenderCustomerId).Distinct().ToList();
        var senders = await Db.Customers.AsNoTracking().Where(x => senderIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => new { x.Code, x.ArabicName }, cancellationToken);
        var currencies = await Db.Currencies.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);

        var openReserved = await Db.SenderSettlementLines.AsNoTracking()
            .Where(x => x.IsOpen && senderIds.Contains(x.SenderSettlement.SenderCustomerId))
            .GroupBy(x => new { x.SenderSettlement.SenderCustomerId, x.SenderSettlement.CurrencyId })
            .Select(g => new { g.Key.SenderCustomerId, g.Key.CurrencyId, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var all = groups.Select(x => new SenderBalanceDto
        {
            SenderCustomerId = x.SenderCustomerId,
            SenderCode = senders.TryGetValue(x.SenderCustomerId, out var s) ? s.Code : string.Empty,
            SenderName = s?.ArabicName ?? string.Empty,
            CurrencyId = x.CurrencyId,
            CurrencyCode = currencies.TryGetValue(x.CurrencyId, out var c) ? c : string.Empty,
            PendingCount = x.PendingCount,
            PendingAmount = x.PendingAmount,
            WithDriversAmount = x.WithDrivers,
            InCashboxAmount = x.InCashbox,
            ReadyNetAmount = x.ReadyNet,
            DisputedAmount = x.Disputed,
            TotalHeldAmount = x.Held,
            OpenSettlementAmount = openReserved.FirstOrDefault(o => o.SenderCustomerId == x.SenderCustomerId && o.CurrencyId == x.CurrencyId)?.Amount ?? 0m
        }).ToList();

        var descending = query.SortDirection is null || query.IsDescending();
        IOrderedEnumerable<SenderBalanceDto> sorted = (query.SortBy ?? "totalHeldAmount").ToLowerInvariant() switch
        {
            "sendername" => query.IsDescending() ? all.OrderByDescending(x => x.SenderName) : all.OrderBy(x => x.SenderName),
            "readynetamount" => descending ? all.OrderByDescending(x => x.ReadyNetAmount) : all.OrderBy(x => x.ReadyNetAmount),
            _ => descending ? all.OrderByDescending(x => x.TotalHeldAmount) : all.OrderBy(x => x.TotalHeldAmount)
        };

        var items = sorted.ThenBy(x => x.SenderCode, StringComparer.Ordinal)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResult<SenderBalanceDto>(items, query.PageNumber, query.PageSize, all.Count);
    }

    // ------------------------------------------------------------------ aging

    public async Task<CodAgingReportDto> GetAgingAsync(CodAgingQuery query, CancellationToken cancellationToken)
    {
        var asOf = query.AsOfUtc ?? UtcNow;
        var currency = await _access.Billing.GetCurrencyAsync(query.CurrencyId, cancellationToken);

        var cods = (await _access.CodsAsync(tracked: false, cancellationToken))
            .Where(x => x.CurrencyId == currency.Id &&
                        (x.Status == CODStatus.CollectedByDriver ||
                         x.Status == CODStatus.ReceivedAtBranchCashbox ||
                         x.Status == CODStatus.ReadyForSenderSettlement ||
                         x.Status == CODStatus.PartiallySettled ||
                         x.Status == CODStatus.Disputed ||
                         ((x.Status == CODStatus.Pending || x.Status == CODStatus.AssignedToDriver) && x.Shipment.Status == ShipmentStatus.Delivered)));

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            cods = cods.Where(x => x.OriginBranchId == branchId);
        }

        if (query.SenderCustomerId.HasValue)
        {
            var senderId = query.SenderCustomerId.Value;
            cods = cods.Where(x => x.SenderCustomerId == senderId);
        }

        var rows = await cods
            .Select(x => new
            {
                x.SenderCustomerId,
                SenderCode = x.SenderCustomer.Code,
                SenderName = x.SenderCustomer.ArabicName,
                x.Status,
                x.StatusBeforeDispute,
                x.ExpectedAmount,
                x.CollectedAmount,
                x.NetPayableAmount,
                x.SettledAmount,
                x.DeductionsRecognized,
                x.CollectedAtUtc,
                x.Shipment.DeliveredAtUtc
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x =>
            {
                var notCollected = x.Status is CODStatus.Pending or CODStatus.AssignedToDriver;
                var date = notCollected ? x.DeliveredAtUtc : x.CollectedAtUtc;
                var amount = notCollected
                    ? x.ExpectedAmount
                    : x.DeductionsRecognized ? x.NetPayableAmount - x.SettledAmount : x.CollectedAmount;
                return new { x.SenderCustomerId, x.SenderCode, x.SenderName, x.Status, Date = date ?? asOf, Amount = amount };
            })
            .Where(x => x.Date <= asOf && x.Amount != 0m)
            .ToList();

        var report = items
            .GroupBy(x => new { x.SenderCustomerId, x.SenderCode, x.SenderName })
            .Select(g =>
            {
                decimal Bucket(int fromDays, int? toDays) => g
                    .Where(x =>
                    {
                        var days = (int)Math.Floor((asOf - x.Date).TotalDays);
                        return days >= fromDays && (toDays is null || days <= toDays.Value);
                    })
                    .Sum(x => x.Amount);

                return new CodAgingRowDto
                {
                    SenderCustomerId = g.Key.SenderCustomerId,
                    SenderCode = g.Key.SenderCode,
                    SenderName = g.Key.SenderName,
                    Count = g.Count(),
                    Days0To7 = Bucket(0, 7),
                    Days8To15 = Bucket(8, 15),
                    Days16To30 = Bucket(16, 30),
                    Days31To60 = Bucket(31, 60),
                    Over60Days = Bucket(61, null),
                    Total = g.Sum(x => x.Amount),
                    DeliveredNotCollected = g.Where(x => x.Status is CODStatus.Pending or CODStatus.AssignedToDriver).Sum(x => x.Amount),
                    WithDrivers = g.Where(x => x.Status == CODStatus.CollectedByDriver).Sum(x => x.Amount),
                    InCashboxes = g.Where(x => x.Status == CODStatus.ReceivedAtBranchCashbox).Sum(x => x.Amount),
                    ReadyForSettlement = g.Where(x => x.Status is CODStatus.ReadyForSenderSettlement or CODStatus.PartiallySettled).Sum(x => x.Amount),
                    Disputed = g.Where(x => x.Status == CODStatus.Disputed).Sum(x => x.Amount),
                    OldestDateUtc = g.Min(x => x.Date)
                };
            })
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.SenderCode, StringComparer.Ordinal)
            .ToList();

        var totals = new CodAgingRowDto
        {
            SenderName = "الإجمالي",
            Count = report.Sum(x => x.Count),
            Days0To7 = report.Sum(x => x.Days0To7),
            Days8To15 = report.Sum(x => x.Days8To15),
            Days16To30 = report.Sum(x => x.Days16To30),
            Days31To60 = report.Sum(x => x.Days31To60),
            Over60Days = report.Sum(x => x.Over60Days),
            Total = report.Sum(x => x.Total),
            DeliveredNotCollected = report.Sum(x => x.DeliveredNotCollected),
            WithDrivers = report.Sum(x => x.WithDrivers),
            InCashboxes = report.Sum(x => x.InCashboxes),
            ReadyForSettlement = report.Sum(x => x.ReadyForSettlement),
            Disputed = report.Sum(x => x.Disputed),
            OldestDateUtc = report.Count == 0 ? null : report.Min(x => x.OldestDateUtc)
        };

        return new CodAgingReportDto { AsOfUtc = asOf, CurrencyId = currency.Id, CurrencyCode = currency.Code, Rows = report, Totals = totals };
    }

    // ------------------------------------------------------------------ dashboard

    public async Task<CodDashboardDto> GetDashboardAsync(CodDashboardQuery query, CancellationToken cancellationToken)
    {
        var currency = await _access.Billing.GetCurrencyAsync(query.CurrencyId, cancellationToken);
        if (query.BranchId.HasValue)
        {
            _access.BranchAccess.EnsureCanAccess(query.BranchId.Value);
        }

        var branchId = query.BranchId;
        var cods = (await _access.CodsAsync(tracked: false, cancellationToken)).Where(x => x.CurrencyId == currency.Id);
        if (branchId.HasValue)
        {
            cods = cods.Where(x => x.OriginBranchId == branchId.Value);
        }

        var stages = await cods
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count(),
                Expected = g.Sum(x => x.ExpectedAmount),
                Collected = g.Sum(x => x.CollectedAmount),
                Remaining = g.Sum(x => x.NetPayableAmount - x.SettledAmount)
            })
            .ToListAsync(cancellationToken);

        var deliveredNotCollected = cods
            .Where(x => (x.Status == CODStatus.Pending || x.Status == CODStatus.AssignedToDriver) && x.Shipment.Status == ShipmentStatus.Delivered);

        var drivers = (await _access.DriverAccountsAsync(cancellationToken)).Where(x => x.CurrencyId == currency.Id && x.Balance != 0m);
        if (branchId.HasValue)
        {
            drivers = drivers.Where(x => x.Driver.Employee.BranchId == branchId.Value);
        }

        var cashboxes = _access.Cashboxes().Where(x => x.CurrencyId == currency.Id && x.IsActive);
        var settlements = _access.Settlements().Where(x => x.CurrencyId == currency.Id && (x.Status == SettlementStatus.Draft || x.Status == SettlementStatus.Approved));
        var adjustments = _access.Adjustments().Where(x => x.Status == CODAdjustmentStatus.PendingApproval);
        var transfers = _access.Transfers().Where(x => x.CurrencyId == currency.Id);
        var closings = _access.Closings().Where(x => x.Status == CashboxClosingStatus.PendingApproval);

        if (branchId.HasValue)
        {
            var id = branchId.Value;
            cashboxes = cashboxes.Where(x => x.BranchId == id);
            settlements = settlements.Where(x => x.BranchId == id);
            adjustments = adjustments.Where(x => x.BranchId == id);
            transfers = transfers.Where(x => x.FromBranchId == id || x.ToBranchId == id);
            closings = closings.Where(x => x.Cashbox.BranchId == id);
        }

        var stageList = Enum.GetValues<CODStatus>()
            .Select(status =>
            {
                var row = stages.FirstOrDefault(x => x.Status == status);
                var amount = row is null
                    ? 0m
                    : status switch
                    {
                        CODStatus.Pending or CODStatus.AssignedToDriver or CODStatus.Cancelled => row.Expected,
                        CODStatus.ReadyForSenderSettlement or CODStatus.PartiallySettled => row.Remaining,
                        _ => row.Collected
                    };
                return new CodStageSummaryDto { Status = status, StatusName = CodNames.Arabic(status), Count = row?.Count ?? 0, Amount = amount };
            })
            .ToList();

        return new CodDashboardDto
        {
            AsOfUtc = UtcNow,
            CurrencyId = currency.Id,
            CurrencyCode = currency.Code,
            Stages = stageList,
            DeliveredNotCollectedCount = await deliveredNotCollected.CountAsync(cancellationToken),
            DeliveredNotCollectedAmount = await deliveredNotCollected.SumAsync(x => x.ExpectedAmount, cancellationToken),
            DriversWithBalance = await drivers.CountAsync(cancellationToken),
            DriversOutstandingTotal = await drivers.SumAsync(x => x.Balance, cancellationToken),
            CashboxCount = await cashboxes.CountAsync(cancellationToken),
            CashboxBalanceTotal = await cashboxes.SumAsync(x => x.CurrentBalance, cancellationToken),
            UnsettledSenderNet = stageList.Where(x => x.Status is CODStatus.ReadyForSenderSettlement or CODStatus.PartiallySettled).Sum(x => x.Amount),
            OpenSettlementsCount = await settlements.CountAsync(cancellationToken),
            OpenSettlementsAmount = await settlements.SumAsync(x => x.NetPayable, cancellationToken),
            PendingAdjustments = await adjustments.CountAsync(cancellationToken),
            PendingTransfers = await transfers.CountAsync(x => x.Status == CashTransferStatus.PendingApproval, cancellationToken),
            InTransitTransfersAmount = await transfers.Where(x => x.Status == CashTransferStatus.InTransit).SumAsync(x => x.Amount, cancellationToken),
            PendingClosingApprovals = await closings.CountAsync(cancellationToken)
        };
    }

    // ------------------------------------------------------------------ period reports

    public async Task<DriverCollectionReportDto> GetDriverCollectionReportAsync(DriverCollectionReportQuery query, CancellationToken cancellationToken)
    {
        var (from, to) = Period(query.FromUtc, query.ToUtc);
        var currency = await _access.Billing.GetCurrencyAsync(query.CurrencyId, cancellationToken);

        var accounts = (await _access.DriverAccountsAsync(cancellationToken)).Where(x => x.CurrencyId == currency.Id);
        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            accounts = accounts.Where(x => x.Driver.Employee.BranchId == branchId);
        }

        if (query.DriverId.HasValue)
        {
            var driverId = query.DriverId.Value;
            accounts = accounts.Where(x => x.DriverId == driverId);
        }

        var drivers = await accounts
            .Select(x => new { x.Id, x.DriverId, Name = x.Driver.Employee.ArabicName, BranchName = x.Driver.Employee.Branch.ArabicName })
            .ToListAsync(cancellationToken);
        var accountIds = drivers.Select(x => x.Id).ToList();

        var movements = await Db.DriverCashMovements.AsNoTracking()
            .Where(x => accountIds.Contains(x.DriverCashAccountId) && x.MovementDateUtc < to)
            .Select(x => new { x.Id, x.DriverCashAccountId, x.MovementType, x.Debit, x.Credit, x.BalanceAfter, x.MovementDateUtc })
            .ToListAsync(cancellationToken);

        var rows = new List<DriverCollectionRowDto>();
        foreach (var driver in drivers.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            var own = movements.Where(x => x.DriverCashAccountId == driver.Id).OrderBy(x => x.Id).ToList();
            var opening = own.LastOrDefault(x => x.MovementDateUtc < from)?.BalanceAfter ?? 0m;
            var period = own.Where(x => x.MovementDateUtc >= from).ToList();

            if (opening == 0m && period.Count == 0)
            {
                continue;
            }

            var collected = period.Where(x => x.MovementType == DriverCashMovementType.CodCollected).ToList();
            var handovers = period.Where(x => x.MovementType == DriverCashMovementType.Handover).ToList();
            var others = period.Where(x => x.MovementType is DriverCashMovementType.HandoverReversed or DriverCashMovementType.CollectionCorrected or DriverCashMovementType.CollectionReversed);

            rows.Add(new DriverCollectionRowDto
            {
                DriverId = driver.DriverId,
                DriverName = driver.Name,
                BranchName = driver.BranchName,
                OpeningBalance = opening,
                CollectedCount = collected.Count,
                CollectedAmount = collected.Sum(x => x.Debit),
                HandoverCount = handovers.Count,
                HandedOverAmount = handovers.Sum(x => x.Credit),
                ShortageWrittenOff = period.Where(x => x.MovementType == DriverCashMovementType.ShortageWrittenOff).Sum(x => x.Credit),
                ExcessRecorded = period.Where(x => x.MovementType == DriverCashMovementType.ExcessRecorded).Sum(x => x.Debit),
                OtherMovements = others.Sum(x => x.Debit - x.Credit),
                ClosingBalance = opening + period.Sum(x => x.Debit - x.Credit)
            });
        }

        var totals = new DriverCollectionRowDto
        {
            DriverName = "الإجمالي",
            OpeningBalance = rows.Sum(x => x.OpeningBalance),
            CollectedCount = rows.Sum(x => x.CollectedCount),
            CollectedAmount = rows.Sum(x => x.CollectedAmount),
            HandoverCount = rows.Sum(x => x.HandoverCount),
            HandedOverAmount = rows.Sum(x => x.HandedOverAmount),
            ShortageWrittenOff = rows.Sum(x => x.ShortageWrittenOff),
            ExcessRecorded = rows.Sum(x => x.ExcessRecorded),
            OtherMovements = rows.Sum(x => x.OtherMovements),
            ClosingBalance = rows.Sum(x => x.ClosingBalance)
        };

        return new DriverCollectionReportDto { FromUtc = from, ToUtc = to, CurrencyId = currency.Id, CurrencyCode = currency.Code, Rows = rows, Totals = totals };
    }

    public async Task<SenderSettlementReportDto> GetSenderSettlementReportAsync(SenderSettlementReportQuery query, CancellationToken cancellationToken)
    {
        var (from, to) = Period(query.FromUtc, query.ToUtc);
        var status = query.Status ?? SettlementStatus.Paid;
        var currency = await _access.Billing.GetCurrencyAsync(query.CurrencyId, cancellationToken);

        var rows = SenderSettlementService.Filter(_access.Settlements(), status, query.SenderCustomerId, query.BranchId, currency.Id, null, null);
        rows = status == SettlementStatus.Paid
            ? rows.Where(x => x.PaidAtUtc >= from && x.PaidAtUtc < to)
            : rows.Where(x => x.SettlementDateUtc >= from && x.SettlementDateUtc < to);

        var settlementCount = await rows.CountAsync(cancellationToken);
        var shipmentCount = await rows.SelectMany(x => x.Lines).CountAsync(cancellationToken);
        var gross = await rows.SumAsync(x => x.TotalGrossReleased, cancellationToken);
        var shipping = await rows.SumAsync(x => x.TotalShippingCharges, cancellationToken);
        var commission = await rows.SumAsync(x => x.TotalCodCommission, cancellationToken);
        var tax = await rows.SumAsync(x => x.TotalTax, cancellationToken);
        var otherFees = await rows.SumAsync(x => x.TotalOtherFees, cancellationToken);
        var compensation = await rows.SumAsync(x => x.TotalCompensation, cancellationToken);
        var net = await rows.SumAsync(x => x.NetPayable, cancellationToken);

        var items = await rows
            .OrderByDescending(x => x.PaidAtUtc ?? x.SettlementDateUtc)
            .ThenByDescending(x => x.Id)
            .Take(MaxReportRows)
            .Select(SenderSettlementService.ListProjection)
            .ToListAsync(cancellationToken);

        return new SenderSettlementReportDto
        {
            FromUtc = from,
            ToUtc = to,
            CurrencyCode = currency.Code,
            Status = status,
            Rows = items.Select(SenderSettlementService.WithName).ToList(),
            SettlementCount = settlementCount,
            ShipmentCount = shipmentCount,
            TotalGrossReleased = gross,
            TotalShippingCharges = shipping,
            TotalCodCommission = commission,
            TotalTax = tax,
            TotalOtherFees = otherFees,
            TotalCompensation = compensation,
            TotalNetPaid = net
        };
    }
}
