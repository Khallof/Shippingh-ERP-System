using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>
/// Fiscal years and accounting periods (step 7).
///
/// Closing a period (POST /accounting/periods/{id}/close) runs in a transaction that UPDATES the period row: a
/// posting in progress holds a shared lock on that row (LedgerPosting.EnsureOpenPeriodAsync, HOLDLOCK) so the closing
/// waits for it, and every posting that starts afterwards reads the Closed status and is refused. The RowVersion
/// of the period turns two simultaneous close / reopen requests into a 409.
///
/// A period closes only when nothing is left behind: no draft / waiting manual entry dated in it, no operational
/// event up to its last day still waiting for the automatic posting, no failed automatic posting dated in it.
/// </summary>
public sealed class FiscalPeriodService : IFiscalPeriodService
{
    private const string YearEntity = "FiscalYear";
    private const string PeriodEntity = "AccountingPeriod";

    private readonly AppDbContext _dbContext;
    private readonly AccountingAccess _access;
    private readonly AuditLogWriter _auditLog;

    public FiscalPeriodService(AppDbContext dbContext, AccountingAccess access, AuditLogWriter auditLog)
    {
        _dbContext = dbContext;
        _access = access;
        _auditLog = auditLog;
    }

    private AppDbContext Db => _dbContext;

    // ------------------------------------------------------------------ fiscal years

    public async Task<IReadOnlyList<FiscalYearDto>> GetFiscalYearsAsync(CancellationToken cancellationToken)
    {
        var years = await Db.FiscalYears.AsNoTracking()
            .Include(x => x.Periods)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);

        return years.Select(ToDto).ToList();
    }

    public async Task<FiscalYearDto> GetFiscalYearAsync(long id, CancellationToken cancellationToken)
    {
        var year = await Db.FiscalYears.AsNoTracking().Include(x => x.Periods).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                   ?? throw new NotFoundException("السنة المالية", id);
        return ToDto(year);
    }

    public async Task<FiscalYearDto> CreateFiscalYearAsync(CreateFiscalYearRequest request, CancellationToken cancellationToken)
    {
        var year = await CreateYearAsync(request, audit: true, cancellationToken);
        return await GetFiscalYearAsync(year.Id, cancellationToken);
    }

    /// <summary>Creates a fiscal year with its monthly periods (also used by the seeder).</summary>
    public async Task<FiscalYear> CreateYearAsync(CreateFiscalYearRequest request, bool audit, CancellationToken cancellationToken)
    {
        var start = BusinessCalendar.AsDate(request.StartDate);
        var end = BusinessCalendar.AsDate(request.EndDate ?? start.AddYears(1).AddDays(-1));
        if (end <= start)
        {
            throw AccountingAccess.Invalid("endDate", "تاريخ نهاية السنة يجب أن يكون بعد تاريخ البداية.");
        }

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? string.Create(CultureInfo.InvariantCulture, $"FY{start.Year}")
            : request.Code.Trim().ToUpperInvariant();

        if (await Db.FiscalYears.AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new ConflictException($"رمز السنة المالية {code} مستخدم من قبل.");
        }

        if (await Db.FiscalYears.AnyAsync(x => x.StartDate <= end && x.EndDate >= start, cancellationToken))
        {
            throw new ConflictException("الفترة تتداخل مع سنة مالية موجودة.");
        }

        var year = new FiscalYear
        {
            Code = code,
            ArabicName = string.IsNullOrWhiteSpace(request.ArabicName)
                ? string.Create(CultureInfo.InvariantCulture, $"السنة المالية {start.Year}")
                : request.ArabicName.Trim(),
            StartDate = start,
            EndDate = end,
            Status = FiscalYearStatus.Open
        };

        foreach (var plan in FiscalCalendar.MonthlyPeriods(start, end))
        {
            year.Periods.Add(new AccountingPeriod
            {
                PeriodNumber = plan.Number,
                Name = plan.Name,
                StartDate = plan.StartDate,
                EndDate = plan.EndDate,
                Status = AccountingPeriodStatus.Open
            });
        }

        Db.FiscalYears.Add(year);
        await Db.SaveChangesAsync(cancellationToken);

        if (audit)
        {
            Audit(AuditAction.FiscalYearCreated, YearEntity, year.Id, new { year.Code, year.StartDate, year.EndDate, periods = year.Periods.Count });
            await Db.SaveChangesAsync(cancellationToken);
        }

        return year;
    }

    public async Task<FiscalYearDto> UpdateFiscalYearAsync(long id, UpdateFiscalYearRequest request, CancellationToken cancellationToken)
    {
        var year = await Db.FiscalYears.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("السنة المالية", id);
        var before = year.ArabicName;
        year.ArabicName = request.ArabicName.Trim();
        Audit(AuditAction.FiscalYearUpdated, YearEntity, year.Id, new { before, after = year.ArabicName });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetFiscalYearAsync(id, cancellationToken);
    }

    public async Task DeleteFiscalYearAsync(long id, CancellationToken cancellationToken)
    {
        var year = await Db.FiscalYears.Include(x => x.Periods).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                   ?? throw new NotFoundException("السنة المالية", id);

        var periodIds = year.Periods.Select(x => x.Id).ToList();
        if (await Db.JournalEntries.AnyAsync(x => periodIds.Contains(x.AccountingPeriodId), cancellationToken))
        {
            throw new BusinessRuleException($"للسنة المالية {year.Code} قيود؛ لا تُحذف. أقفل فتراتها بدلاً من ذلك.");
        }

        Audit(AuditAction.FiscalYearDeleted, YearEntity, year.Id, new { year.Code, year.StartDate, year.EndDate });
        Db.AccountingPeriods.RemoveRange(year.Periods);
        Db.FiscalYears.Remove(year);
        await Db.SaveChangesAsync(cancellationToken);
    }

    public async Task<FiscalYearDto> CloseFiscalYearAsync(long id, CancellationToken cancellationToken)
    {
        var year = await Db.FiscalYears.Include(x => x.Periods).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                   ?? throw new NotFoundException("السنة المالية", id);

        if (year.Status == FiscalYearStatus.Closed)
        {
            throw new BusinessRuleException($"السنة المالية {year.Code} مقفلة من قبل.");
        }

        var open = year.Periods.Where(x => x.Status == AccountingPeriodStatus.Open).OrderBy(x => x.PeriodNumber).Select(x => x.Name).ToList();
        if (open.Count > 0)
        {
            throw new BusinessRuleException($"أقفل كل فترات السنة أولاً. الفترات المفتوحة: {string.Join("، ", open)}.");
        }

        year.Status = FiscalYearStatus.Closed;
        year.ClosedAtUtc = _access.UtcNow;
        year.ClosedByUserId = _access.CurrentUser.UserId;
        Audit(AuditAction.FiscalYearClosed, YearEntity, year.Id, new { year.Code });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetFiscalYearAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ periods

    public async Task<PagedResult<AccountingPeriodDto>> GetPeriodsAsync(AccountingPeriodListQuery query, CancellationToken cancellationToken)
    {
        var rows = Db.AccountingPeriods.AsNoTracking().Include(x => x.FiscalYear).AsQueryable();

        if (query.FiscalYearId.HasValue)
        {
            var yearId = query.FiscalYearId.Value;
            rows = rows.Where(x => x.FiscalYearId == yearId);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.Date.HasValue)
        {
            var day = BusinessCalendar.AsDate(query.Date.Value);
            rows = rows.Where(x => x.StartDate <= day && x.EndDate >= day);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Name.Contains(search) || x.FiscalYear.Code.Contains(search));
        }

        var total = await rows.CountAsync(cancellationToken);
        var ordered = query.IsDescending() ? rows.OrderByDescending(x => x.StartDate) : rows.OrderBy(x => x.StartDate);
        var items = await ordered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AccountingPeriodDto>(items.Select(ToDto).ToList(), query.PageNumber, query.PageSize, total);
    }

    public async Task<AccountingPeriodDto> GetPeriodAsync(long id, CancellationToken cancellationToken)
    {
        var period = await Db.AccountingPeriods.AsNoTracking().Include(x => x.FiscalYear).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException("الفترة المحاسبية", id);
        return ToDto(period);
    }

    public async Task<AccountingPeriodDto> UpdatePeriodAsync(long id, UpdateAccountingPeriodRequest request, CancellationToken cancellationToken)
    {
        var period = await Db.AccountingPeriods.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("الفترة المحاسبية", id);
        var before = period.Name;
        period.Name = request.Name.Trim();
        Audit(AuditAction.AccountingPeriodUpdated, PeriodEntity, period.Id, new { before, after = period.Name });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetPeriodAsync(id, cancellationToken);
    }

    public async Task<PeriodCloseCheckDto> GetCloseCheckAsync(long id, CancellationToken cancellationToken)
    {
        var period = await Db.AccountingPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException("الفترة المحاسبية", id);
        return await CheckAsync(period, cancellationToken);
    }

    public async Task<AccountingPeriodDto> ClosePeriodAsync(long id, CloseAccountingPeriodRequest request, CancellationToken cancellationToken)
    {
        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var period = await Db.AccountingPeriods.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("الفترة المحاسبية", id);
            if (period.Status == AccountingPeriodStatus.Closed)
            {
                throw new BusinessRuleException($"الفترة «{period.Name}» مقفلة من قبل.");
            }

            var check = await CheckAsync(period, ct);
            if (!check.CanClose)
            {
                throw new BusinessRuleException($"لا يمكن إقفال الفترة «{period.Name}»: {string.Join(" ", check.Blockers)}");
            }

            period.Status = AccountingPeriodStatus.Closed;
            period.ClosedAtUtc = _access.UtcNow;
            period.ClosedByUserId = _access.CurrentUser.UserId;
            period.CloseNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            Audit(AuditAction.AccountingPeriodClosed, PeriodEntity, period.Id, new { period.Name, period.StartDate, period.EndDate, notes = period.CloseNotes });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetPeriodAsync(id, cancellationToken);
    }

    public async Task<AccountingPeriodDto> ReopenPeriodAsync(long id, ReopenAccountingPeriodRequest request, CancellationToken cancellationToken)
    {
        var period = await Db.AccountingPeriods.Include(x => x.FiscalYear).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException("الفترة المحاسبية", id);

        if (period.Status == AccountingPeriodStatus.Open)
        {
            throw new BusinessRuleException($"الفترة «{period.Name}» مفتوحة.");
        }

        if (period.FiscalYear.Status == FiscalYearStatus.Closed)
        {
            throw new BusinessRuleException($"السنة المالية {period.FiscalYear.Code} مقفلة؛ لا تُعاد فتح فتراتها.");
        }

        var reason = request.Reason.Trim();
        period.Status = AccountingPeriodStatus.Open;
        period.ReopenedAtUtc = _access.UtcNow;
        period.ReopenedByUserId = _access.CurrentUser.UserId;
        period.ReopenReason = LedgerPosting.Clip(reason, 500);

        Audit(AuditAction.AccountingPeriodReopened, PeriodEntity, period.Id, new { period.Name, reason });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetPeriodAsync(id, cancellationToken);
    }

    private async Task<PeriodCloseCheckDto> CheckAsync(AccountingPeriod period, CancellationToken cancellationToken)
    {
        var start = period.StartDate;
        var end = period.EndDate;
        var endUtc = _access.Calendar.DayRangeUtc(end).EndUtc;

        var inPeriod = Db.JournalEntries.AsNoTracking().Where(x => x.EntryDate >= start && x.EntryDate <= end);
        var unposted = await inPeriod.CountAsync(x => x.Status == JournalEntryStatus.Draft || x.Status == JournalEntryStatus.Rejected, cancellationToken);
        var waiting = await inPeriod.CountAsync(x => x.Status == JournalEntryStatus.Submitted || x.Status == JournalEntryStatus.Approved, cancellationToken);
        var pendingEvents = await Db.OutboxMessages.AsNoTracking().CountAsync(x => x.ProcessedAtUtc == null && x.OccurredAtUtc < endUtc, cancellationToken);
        var failedEvents = await Db.AccountingPostingEvents.AsNoTracking()
            .CountAsync(x => x.Status == PostingEventStatus.Failed && x.PostingDate != null && x.PostingDate >= start && x.PostingDate <= end, cancellationToken);

        var blockers = new List<string>();
        if (unposted > 0)
        {
            blockers.Add($"{unposted} قيد مسودة أو مرفوض بتاريخ داخل الفترة (رحّله أو احذفه أو غيّر تاريخه).");
        }

        if (waiting > 0)
        {
            blockers.Add($"{waiting} قيد بانتظار الاعتماد أو الترحيل.");
        }

        if (pendingEvents > 0)
        {
            blockers.Add($"{pendingEvents} عملية تشغيلية حتى نهاية الفترة لم تُرحّل آلياً بعد (POST /api/v1/accounting/posting-events/process).");
        }

        if (failedEvents > 0)
        {
            blockers.Add($"{failedEvents} ترحيل آلي فاشل بتاريخ داخل الفترة (صحّح السبب ثم أعد المحاولة).");
        }

        return new PeriodCloseCheckDto
        {
            PeriodId = period.Id,
            PeriodName = period.Name,
            Status = period.Status,
            UnpostedEntries = unposted,
            EntriesAwaitingApproval = waiting,
            PendingPostingEvents = pendingEvents,
            FailedPostingEvents = failedEvents,
            CanClose = period.Status == AccountingPeriodStatus.Open && blockers.Count == 0,
            Blockers = blockers
        };
    }

    // ------------------------------------------------------------------ mapping

    private static FiscalYearDto ToDto(FiscalYear year) => new()
    {
        Id = year.Id,
        Code = year.Code,
        ArabicName = year.ArabicName,
        StartDate = year.StartDate,
        EndDate = year.EndDate,
        Status = year.Status,
        StatusName = AccountingNames.Arabic(year.Status),
        ClosedAtUtc = year.ClosedAtUtc,
        ClosedByUserId = year.ClosedByUserId,
        PeriodCount = year.Periods.Count,
        OpenPeriodCount = year.Periods.Count(x => x.Status == AccountingPeriodStatus.Open),
        Periods = year.Periods.OrderBy(x => x.PeriodNumber).Select(p => ToDto(p, year.Code)).ToList()
    };

    private static AccountingPeriodDto ToDto(AccountingPeriod period) => ToDto(period, period.FiscalYear.Code);

    private static AccountingPeriodDto ToDto(AccountingPeriod period, string yearCode) => new()
    {
        Id = period.Id,
        FiscalYearId = period.FiscalYearId,
        FiscalYearCode = yearCode,
        PeriodNumber = period.PeriodNumber,
        Name = period.Name,
        StartDate = period.StartDate,
        EndDate = period.EndDate,
        Status = period.Status,
        StatusName = AccountingNames.Arabic(period.Status),
        ClosedAtUtc = period.ClosedAtUtc,
        ClosedByUserId = period.ClosedByUserId,
        CloseNotes = period.CloseNotes,
        ReopenedAtUtc = period.ReopenedAtUtc,
        ReopenedByUserId = period.ReopenedByUserId,
        ReopenReason = period.ReopenReason
    };

    private void Audit(AuditAction action, string entity, long id, object details) =>
        _auditLog.Add(action, entityName: entity, entityId: id.ToString(CultureInfo.InvariantCulture), details: details);
}
