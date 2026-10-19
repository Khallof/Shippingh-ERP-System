using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Cash;

/// <summary>
/// Cashboxes (صناديق النقدية): creation with an opening balance, the cashbox statement (its transactions with the
/// running balance) and the daily closing with discrepancy approval (step 6).
/// </summary>
public sealed class CashboxService : ICashboxService
{
    private const string EntityName = "Cashbox";

    private static readonly Dictionary<string, Expression<Func<Cashbox, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = x => x.Code,
        ["arabicName"] = x => x.ArabicName,
        ["currentBalance"] = x => x.CurrentBalance
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly CashAccess _access;
    private readonly CashLedger _ledger;
    private readonly IIntegrationEventPublisher _events;

    public CashboxService(MasterDataServiceDependencies dependencies, CashAccess access, CashLedger ledger, IIntegrationEventPublisher events)
    {
        _dependencies = dependencies;
        _access = access;
        _ledger = ledger;
        _events = events;
    }

    private AppDbContext Db => _dependencies.DbContext;

    private DateTime UtcNow => _dependencies.DateTimeProvider.UtcNow;

    // ------------------------------------------------------------------ cashboxes

    public async Task<PagedResult<CashboxDto>> GetPagedAsync(CashboxListQuery query, CancellationToken cancellationToken)
    {
        var rows = _access.Cashboxes();

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId);
        }

        if (query.CurrencyId.HasValue)
        {
            var currencyId = query.CurrencyId.Value;
            rows = rows.Where(x => x.CurrencyId == currencyId);
        }

        if (query.IsActive.HasValue)
        {
            var isActive = query.IsActive.Value;
            rows = rows.Where(x => x.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Code.Contains(search) || x.ArabicName.Contains(search) || (x.EnglishName != null && x.EnglishName.Contains(search)));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<Cashbox> ordered = !string.IsNullOrWhiteSpace(query.SortBy) && Sorts.TryGetValue(query.SortBy, out var sort)
            ? (query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort))
            : rows.OrderBy(x => x.Code);

        var items = await ordered
            .ThenBy(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<CashboxDto>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<CashboxDto> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        await _access.Cashboxes().Where(x => x.Id == id).Select(Projection).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("الصندوق", id);

    public async Task<CashboxDto> CreateAsync(CreateCashboxRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await Db.Cashboxes.IgnoreQueryFilters().AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new ConflictException($"رمز الصندوق {code} مستخدم من قبل.");
        }

        _access.BranchAccess.EnsureCanAccess(request.BranchId);
        var branch = await Db.Branches.AsNoTracking().Where(x => x.Id == request.BranchId).Select(x => new { x.Id, x.IsActive }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw CashAccess.Invalid("branchId", "الفرع غير موجود.");
        if (!branch.IsActive)
        {
            throw CashAccess.Invalid("branchId", "الفرع غير مفعّل.");
        }

        var currency = await Db.Currencies.AsNoTracking().Where(x => x.Id == request.CurrencyId).Select(x => new { x.Id, x.IsActive, x.DecimalPlaces }).FirstOrDefaultAsync(cancellationToken)
                       ?? throw CashAccess.Invalid("currencyId", "العملة غير موجودة.");
        if (!currency.IsActive)
        {
            throw CashAccess.Invalid("currencyId", "العملة غير مفعّلة.");
        }

        await EnsureCustodianAsync(request.CustodianUserId, cancellationToken);

        var opening = BillingAccess.Round(request.OpeningBalance, currency.DecimalPlaces);
        var cashbox = new Cashbox
        {
            Code = code,
            ArabicName = request.ArabicName.Trim(),
            EnglishName = string.IsNullOrWhiteSpace(request.EnglishName) ? null : request.EnglishName.Trim(),
            BranchId = branch.Id,
            CurrencyId = currency.Id,
            CustodianUserId = request.CustodianUserId,
            CurrentBalance = 0m,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsActive = true
        };

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            Db.Cashboxes.Add(cashbox);
            await Db.SaveChangesAsync(ct);

            if (opening > 0m)
            {
                await _ledger.PostCashboxAsync(cashbox, CashboxTransactionType.OpeningBalance, opening, 0m,
                    CashLedger.CashboxDocument, cashbox.Id, cashbox.Code, $"الرصيد الافتتاحي للصندوق {cashbox.Code}", null, ct);

                _events.Publish(new CashboxOpeningBalanceEvent
                {
                    CashboxId = cashbox.Id,
                    CashboxCode = cashbox.Code,
                    BranchId = cashbox.BranchId,
                    CurrencyId = cashbox.CurrencyId,
                    Amount = opening,
                    Posting = CashPostings.OpeningBalance(cashbox.Id, cashbox.Code, cashbox.CurrencyId, UtcNow, opening, cashbox.BranchId)
                });
            }

            Audit(AuditAction.CashboxCreated, cashbox.Id, new { cashbox.Code, cashbox.BranchId, cashbox.CurrencyId, opening });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(cashbox.Id, cancellationToken);
    }

    public async Task<CashboxDto> UpdateAsync(long id, UpdateCashboxRequest request, CancellationToken cancellationToken)
    {
        var cashbox = await _access.Cashboxes(tracked: true).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                      ?? throw new NotFoundException("الصندوق", id);

        await EnsureCustodianAsync(request.CustodianUserId, cancellationToken);

        if (cashbox.IsActive && !request.IsActive)
        {
            if (cashbox.CurrentBalance != 0m)
            {
                throw new BusinessRuleException($"لا يمكن إيقاف الصندوق {cashbox.Code} ورصيده {cashbox.CurrentBalance}. حوّل الرصيد إلى صندوق آخر أولاً.");
            }

            var hasOpenTransfers = await Db.CashTransfers.AnyAsync(
                x => (x.FromCashboxId == id || x.ToCashboxId == id) &&
                     (x.Status == CashTransferStatus.PendingApproval || x.Status == CashTransferStatus.InTransit),
                cancellationToken);
            if (hasOpenTransfers)
            {
                throw new BusinessRuleException("للصندوق تحويلات معلقة أو قيد النقل. أنهها أولاً.");
            }
        }

        var before = new { cashbox.ArabicName, cashbox.CustodianUserId, cashbox.IsActive };
        cashbox.ArabicName = request.ArabicName.Trim();
        cashbox.EnglishName = string.IsNullOrWhiteSpace(request.EnglishName) ? null : request.EnglishName.Trim();
        cashbox.CustodianUserId = request.CustodianUserId;
        cashbox.IsActive = request.IsActive;
        cashbox.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        Audit(AuditAction.CashboxUpdated, cashbox.Id, new { before, after = new { cashbox.ArabicName, cashbox.CustodianUserId, cashbox.IsActive } });
        await Db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<PagedResult<CashboxTransactionDto>> GetTransactionsAsync(long id, CashboxTransactionQuery query, CancellationToken cancellationToken)
    {
        if (!await _access.Cashboxes().AnyAsync(x => x.Id == id, cancellationToken))
        {
            throw new NotFoundException("الصندوق", id);
        }

        var rows = Db.CashboxTransactions.AsNoTracking().Where(x => x.CashboxId == id);

        if (query.FromDate.HasValue)
        {
            var from = BusinessCalendar.AsDate(query.FromDate.Value);
            rows = rows.Where(x => x.BusinessDate >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = BusinessCalendar.AsDate(query.ToDate.Value);
            rows = rows.Where(x => x.BusinessDate <= to);
        }

        if (query.TransactionType.HasValue)
        {
            var type = query.TransactionType.Value;
            rows = rows.Where(x => x.TransactionType == type);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                x.DocumentNumber.Contains(search) ||
                x.Description.Contains(search) ||
                (x.CounterpartyName != null && x.CounterpartyName.Contains(search)));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new CashboxTransactionDto
            {
                Id = x.Id,
                TransactionType = x.TransactionType,
                AmountIn = x.AmountIn,
                AmountOut = x.AmountOut,
                BalanceAfter = x.BalanceAfter,
                TransactionDateUtc = x.TransactionDateUtc,
                BusinessDate = x.BusinessDate,
                DocumentType = x.DocumentType,
                DocumentId = x.DocumentId,
                DocumentNumber = x.DocumentNumber,
                Description = x.Description,
                CounterpartyName = x.CounterpartyName,
                ReversedTransactionId = x.ReversedTransactionId,
                CreatedByUserId = x.CreatedByUserId
            })
            .ToListAsync(cancellationToken);

        var named = items.Select(x => new CashboxTransactionDto
        {
            Id = x.Id,
            TransactionType = x.TransactionType,
            TransactionTypeName = CodNames.Arabic(x.TransactionType),
            AmountIn = x.AmountIn,
            AmountOut = x.AmountOut,
            BalanceAfter = x.BalanceAfter,
            TransactionDateUtc = x.TransactionDateUtc,
            BusinessDate = x.BusinessDate,
            DocumentType = x.DocumentType,
            DocumentId = x.DocumentId,
            DocumentNumber = x.DocumentNumber,
            Description = x.Description,
            CounterpartyName = x.CounterpartyName,
            ReversedTransactionId = x.ReversedTransactionId,
            CreatedByUserId = x.CreatedByUserId
        }).ToList();

        return new PagedResult<CashboxTransactionDto>(named, query.PageNumber, query.PageSize, totalCount);
    }

    // ------------------------------------------------------------------ daily closing

    public async Task<CashboxDailyClosingDto> CloseDayAsync(long id, CloseCashboxDayRequest request, CancellationToken cancellationToken)
    {
        var cashbox = await _access.Cashboxes().Include(x => x.Currency).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                      ?? throw new NotFoundException("الصندوق", id);

        var today = _ledger.Calendar.BusinessDate(UtcNow);
        var date = request.BusinessDate.HasValue ? BusinessCalendar.AsDate(request.BusinessDate.Value) : today;
        if (date > today)
        {
            throw CashAccess.Invalid("businessDate", "لا يمكن إقفال يوم لم يأت بعد.");
        }

        if (await Db.CashboxDailyClosings.AnyAsync(x => x.CashboxId == id && x.BusinessDate == date && x.Status != CashboxClosingStatus.Rejected, cancellationToken))
        {
            throw new ConflictException($"يوم {date:yyyy-MM-dd} مقفل من قبل لهذا الصندوق (أو إقفاله بانتظار الموافقة).");
        }

        var opening = await Db.CashboxTransactions.AsNoTracking()
            .Where(x => x.CashboxId == id && x.BusinessDate < date)
            .OrderByDescending(x => x.Id)
            .Select(x => (decimal?)x.BalanceAfter)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

        var dayRows = Db.CashboxTransactions.AsNoTracking().Where(x => x.CashboxId == id && x.BusinessDate == date);
        var totalIn = await dayRows.SumAsync(x => x.AmountIn, cancellationToken);
        var totalOut = await dayRows.SumAsync(x => x.AmountOut, cancellationToken);
        var expected = opening + totalIn - totalOut;
        var counted = BillingAccess.Round(request.CountedBalance, cashbox.Currency.DecimalPlaces);
        var discrepancy = counted - expected;

        if (discrepancy != 0m && string.IsNullOrWhiteSpace(request.DiscrepancyReason))
        {
            throw CashAccess.Invalid("discrepancyReason",
                $"المبلغ المعدود ({counted}) يختلف عن الرصيد المتوقع ({expected}): الفرق {discrepancy}. اكتب سبب الفرق.");
        }

        var closing = new CashboxDailyClosing
        {
            CashboxId = id,
            BusinessDate = date,
            OpeningBalance = opening,
            TotalIn = totalIn,
            TotalOut = totalOut,
            ExpectedBalance = expected,
            CountedBalance = counted,
            Discrepancy = discrepancy,
            Status = discrepancy == 0m ? CashboxClosingStatus.Closed : CashboxClosingStatus.PendingApproval,
            DiscrepancyReason = discrepancy == 0m ? null : request.DiscrepancyReason!.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        Db.CashboxDailyClosings.Add(closing);
        await Db.SaveChangesAsync(cancellationToken);

        _dependencies.AuditLog.Add(AuditAction.CashboxDayClosed, entityName: CashLedger.ClosingDocument,
            entityId: closing.Id.ToString(CultureInfo.InvariantCulture),
            details: new { cashbox = cashbox.Code, date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), expected, counted, discrepancy });
        await Db.SaveChangesAsync(cancellationToken);

        return await GetClosingAsync(closing.Id, cancellationToken);
    }

    public async Task<PagedResult<CashboxDailyClosingDto>> GetClosingsAsync(CashboxClosingQuery query, CancellationToken cancellationToken)
    {
        var rows = _access.Closings();

        if (query.CashboxId.HasValue)
        {
            var cashboxId = query.CashboxId.Value;
            rows = rows.Where(x => x.CashboxId == cashboxId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.Cashbox.BranchId == branchId);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.FromDate.HasValue)
        {
            var from = BusinessCalendar.AsDate(query.FromDate.Value);
            rows = rows.Where(x => x.BusinessDate >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = BusinessCalendar.AsDate(query.ToDate.Value);
            rows = rows.Where(x => x.BusinessDate <= to);
        }

        if (query.HasDiscrepancy.HasValue)
        {
            rows = query.HasDiscrepancy.Value ? rows.Where(x => x.Discrepancy != 0m) : rows.Where(x => x.Discrepancy == 0m);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Cashbox.Code.Contains(search) || x.Cashbox.ArabicName.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(x => x.BusinessDate)
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ClosingProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<CashboxDailyClosingDto>(items.Select(WithName).ToList(), query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<CashboxDailyClosingDto> ApproveClosingAsync(long closingId, DecideCashboxClosingRequest request, CancellationToken cancellationToken)
    {
        var closing = await _access.Closings(tracked: true).FirstOrDefaultAsync(x => x.Id == closingId, cancellationToken)
                      ?? throw new NotFoundException("الإقفال", closingId);

        if (closing.Status != CashboxClosingStatus.PendingApproval)
        {
            throw new BusinessRuleException($"الإقفال في حالة «{CodNames.Arabic(closing.Status)}» ولا ينتظر موافقة.");
        }

        _access.EnsureOtherApprover(closing.CreatedByUserId, "فرق الإقفال");

        var cashbox = await Db.Cashboxes.FirstAsync(x => x.Id == closing.CashboxId, cancellationToken);
        var reference = $"{cashbox.Code}-{closing.BusinessDate:yyyyMMdd}";

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var now = UtcNow;
            var shortage = closing.Discrepancy < 0m;
            var transaction = await _ledger.PostCashboxAsync(
                cashbox,
                shortage ? CashboxTransactionType.ClosingShortage : CashboxTransactionType.ClosingExcess,
                amountIn: shortage ? 0m : closing.Discrepancy,
                amountOut: shortage ? -closing.Discrepancy : 0m,
                CashLedger.ClosingDocument,
                closing.Id,
                reference,
                $"{(shortage ? "عجز" : "زيادة")} إقفال يوم {closing.BusinessDate:yyyy-MM-dd}: {closing.DiscrepancyReason}",
                null,
                ct,
                allowClosedDay: true);

            await Db.SaveChangesAsync(ct);

            closing.Status = CashboxClosingStatus.Approved;
            closing.DecidedByUserId = _dependencies.CurrentUser.UserId;
            closing.DecidedAtUtc = now;
            closing.DecisionNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            closing.AdjustmentTransactionId = transaction.Id;

            _events.Publish(new CashboxDiscrepancyPostedEvent
            {
                ClosingId = closing.Id,
                CashboxId = cashbox.Id,
                CurrencyId = cashbox.CurrencyId,
                BusinessDate = closing.BusinessDate,
                Discrepancy = closing.Discrepancy,
                Reason = closing.DiscrepancyReason ?? string.Empty,
                Posting = CashPostings.ClosingDiscrepancy(closing.Id, reference, cashbox.CurrencyId, now, closing.Discrepancy, cashbox.BranchId, cashbox.Id)
            });
            _dependencies.AuditLog.Add(AuditAction.CashboxClosingApproved, entityName: CashLedger.ClosingDocument,
                entityId: closing.Id.ToString(CultureInfo.InvariantCulture), details: new { reference, closing.Discrepancy });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetClosingAsync(closingId, cancellationToken);
    }

    public async Task<CashboxDailyClosingDto> RejectClosingAsync(long closingId, DecideCashboxClosingRequest request, CancellationToken cancellationToken)
    {
        var closing = await _access.Closings(tracked: true).FirstOrDefaultAsync(x => x.Id == closingId, cancellationToken)
                      ?? throw new NotFoundException("الإقفال", closingId);

        if (closing.Status != CashboxClosingStatus.PendingApproval)
        {
            throw new BusinessRuleException($"الإقفال في حالة «{CodNames.Arabic(closing.Status)}» ولا ينتظر موافقة.");
        }

        if (string.IsNullOrWhiteSpace(request.Notes))
        {
            throw CashAccess.Invalid("notes", "اكتب سبب الرفض (مثلاً: أعد العد بحضور المدير).");
        }

        _access.EnsureOtherApprover(closing.CreatedByUserId, "فرق الإقفال");

        closing.Status = CashboxClosingStatus.Rejected;
        closing.DecidedByUserId = _dependencies.CurrentUser.UserId;
        closing.DecidedAtUtc = UtcNow;
        closing.DecisionNotes = request.Notes.Trim();

        _dependencies.AuditLog.Add(AuditAction.CashboxClosingRejected, entityName: CashLedger.ClosingDocument,
            entityId: closing.Id.ToString(CultureInfo.InvariantCulture), details: new { closing.Discrepancy, notes = closing.DecisionNotes });
        await Db.SaveChangesAsync(cancellationToken);

        return await GetClosingAsync(closingId, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<CashboxDailyClosingDto> GetClosingAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await _access.Closings().Where(x => x.Id == id).Select(ClosingProjection).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("الإقفال", id);
        return WithName(dto);
    }

    private async Task EnsureCustodianAsync(long? userId, CancellationToken cancellationToken)
    {
        if (userId is null)
        {
            return;
        }

        var active = await Db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => (bool?)x.IsActive).FirstOrDefaultAsync(cancellationToken);
        if (active is null)
        {
            throw CashAccess.Invalid("custodianUserId", "المستخدم غير موجود.");
        }

        if (active == false)
        {
            throw CashAccess.Invalid("custodianUserId", "المستخدم غير مفعّل.");
        }
    }

    private void Audit(AuditAction action, long id, object details) =>
        _dependencies.AuditLog.Add(action, entityName: EntityName, entityId: id.ToString(CultureInfo.InvariantCulture), details: details);

    private static readonly Expression<Func<Cashbox, CashboxDto>> Projection = x => new CashboxDto
    {
        Id = x.Id,
        Code = x.Code,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        CurrencyDecimals = x.Currency.DecimalPlaces,
        CustodianUserId = x.CustodianUserId,
        CustodianName = x.CustodianUser != null ? x.CustodianUser.ArabicFullName : null,
        CurrentBalance = x.CurrentBalance,
        LastTransactionAtUtc = x.LastTransactionAtUtc,
        IsActive = x.IsActive,
        Notes = x.Notes,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        UpdatedAtUtc = x.UpdatedAtUtc,
        UpdatedByUserId = x.UpdatedByUserId
    };

    private static readonly Expression<Func<CashboxDailyClosing, CashboxDailyClosingDto>> ClosingProjection = x => new CashboxDailyClosingDto
    {
        Id = x.Id,
        CashboxId = x.CashboxId,
        CashboxCode = x.Cashbox.Code,
        CashboxName = x.Cashbox.ArabicName,
        BranchName = x.Cashbox.Branch.ArabicName,
        CurrencyCode = x.Cashbox.Currency.Code,
        BusinessDate = x.BusinessDate,
        OpeningBalance = x.OpeningBalance,
        TotalIn = x.TotalIn,
        TotalOut = x.TotalOut,
        ExpectedBalance = x.ExpectedBalance,
        CountedBalance = x.CountedBalance,
        Discrepancy = x.Discrepancy,
        Status = x.Status,
        DiscrepancyReason = x.DiscrepancyReason,
        Notes = x.Notes,
        ClosedByUserId = x.CreatedByUserId,
        CreatedAtUtc = x.CreatedAtUtc,
        DecidedByUserId = x.DecidedByUserId,
        DecidedAtUtc = x.DecidedAtUtc,
        DecisionNotes = x.DecisionNotes,
        AdjustmentTransactionId = x.AdjustmentTransactionId
    };

    private static CashboxDailyClosingDto WithName(CashboxDailyClosingDto x) => new()
    {
        Id = x.Id,
        CashboxId = x.CashboxId,
        CashboxCode = x.CashboxCode,
        CashboxName = x.CashboxName,
        BranchName = x.BranchName,
        CurrencyCode = x.CurrencyCode,
        BusinessDate = x.BusinessDate,
        OpeningBalance = x.OpeningBalance,
        TotalIn = x.TotalIn,
        TotalOut = x.TotalOut,
        ExpectedBalance = x.ExpectedBalance,
        CountedBalance = x.CountedBalance,
        Discrepancy = x.Discrepancy,
        Status = x.Status,
        StatusName = CodNames.Arabic(x.Status),
        DiscrepancyReason = x.DiscrepancyReason,
        Notes = x.Notes,
        ClosedByUserId = x.ClosedByUserId,
        CreatedAtUtc = x.CreatedAtUtc,
        DecidedByUserId = x.DecidedByUserId,
        DecidedAtUtc = x.DecidedAtUtc,
        DecisionNotes = x.DecisionNotes,
        AdjustmentTransactionId = x.AdjustmentTransactionId
    };
}
