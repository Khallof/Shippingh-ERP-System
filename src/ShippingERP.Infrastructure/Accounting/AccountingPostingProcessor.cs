using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>
/// Automatic posting (step 7): turns the operational events of the outbox (invoices, payments, COD, cash vouchers,
/// transfers, settlements...) into posted journal entries.
///
/// One outbox message = one database transaction:
///   1. the message and its AccountingPostingEvent are read again inside the transaction;
///   2. the event is translated into a posting plan (AccountingEventTranslator);
///   3. the accounting date must be in an OPEN period (row lock, see LedgerPosting);
///   4. each line role is mapped to an account (PostingAccountResolver) and the entry is validated (balanced,
///      postable active accounts);
///   5. the entry (JV number), the posting event and "message processed" are saved together and committed.
///
/// Idempotency, three independent guards:
///   - the outbox message is marked processed in the same transaction as the entry;
///   - AccountingPostingEvents.EventId is UNIQUE: a message delivered twice is posted once;
///   - JournalEntries.SourceKey is UNIQUE: the same business document ("InvoiceIssued:Invoice:15") is posted once
///     even if it were published twice.
/// A failure (closed period, missing posting rule...) rolls everything back and records Attempts / LastError; the
/// dispatcher retries it up to Accounting:MaxAutomaticAttempts times, then an accountant retries it by hand.
/// </summary>
public sealed class AccountingPostingProcessor : IAccountingIntegrationHandler, IAccountingPostingService
{
    private const string EntityName = "AccountingPostingEvent";

    private readonly AppDbContext _dbContext;
    private readonly AccountingEventTranslator _translator;
    private readonly PostingAccountResolver _resolver;
    private readonly LedgerPosting _ledger;
    private readonly AccountingAccess _access;
    private readonly AuditLogWriter _auditLog;
    private readonly AccountingOptions _options;
    private readonly ILogger<AccountingPostingProcessor> _logger;

    public AccountingPostingProcessor(
        AppDbContext dbContext,
        AccountingEventTranslator translator,
        PostingAccountResolver resolver,
        LedgerPosting ledger,
        AccountingAccess access,
        AuditLogWriter auditLog,
        IOptions<AccountingOptions> options,
        ILogger<AccountingPostingProcessor> logger)
    {
        _dbContext = dbContext;
        _translator = translator;
        _resolver = resolver;
        _ledger = ledger;
        _access = access;
        _auditLog = auditLog;
        _options = options.Value;
        _logger = logger;
    }

    private enum Outcome
    {
        NotFound,
        AlreadyDone,
        Posted,
        Skipped,
        Duplicate,
        Failed
    }

    /// <summary>What the transaction learned before it failed (for the failure record).</summary>
    private sealed class Attempt
    {
        public PostingPlan? Plan { get; set; }

        public DateTime? PostingDate { get; set; }
    }

    // ------------------------------------------------------------------ processing

    public async Task<ProcessPostingsResultDto> ProcessPendingAsync(int maxCount, CancellationToken cancellationToken)
    {
        maxCount = Math.Clamp(maxCount, 1, 1000);
        var maxAttempts = Math.Max(1, _options.MaxAutomaticAttempts);

        var ids = await _dbContext.OutboxMessages.AsNoTracking()
            .Where(x => x.ProcessedAtUtc == null && x.Attempts < maxAttempts)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        int posted = 0, skipped = 0, duplicates = 0, failed = 0, processed = 0;
        foreach (var id in ids)
        {
            var outcome = await ProcessAsync(id, null, retryAfterConflict: false, cancellationToken);
            switch (outcome)
            {
                case Outcome.Posted:
                    posted++;
                    break;
                case Outcome.Skipped:
                    skipped++;
                    break;
                case Outcome.Duplicate:
                    duplicates++;
                    break;
                case Outcome.Failed:
                    failed++;
                    break;
            }

            if (outcome != Outcome.NotFound)
            {
                processed++;
            }
        }

        _dbContext.ChangeTracker.Clear();
        var remaining = await _dbContext.OutboxMessages.CountAsync(x => x.ProcessedAtUtc == null, cancellationToken);

        return new ProcessPostingsResultDto
        {
            Processed = processed,
            Posted = posted,
            Skipped = skipped,
            Duplicates = duplicates,
            Failed = failed,
            Remaining = remaining
        };
    }

    public async Task<bool> HandleOutboxMessageAsync(long outboxMessageId, CancellationToken cancellationToken) =>
        await ProcessAsync(outboxMessageId, null, retryAfterConflict: false, cancellationToken) != Outcome.NotFound;

    public async Task<PostingEventDto> RetryAsync(long postingEventId, RetryPostingEventRequest request, CancellationToken cancellationToken)
    {
        var postingEvent = await _dbContext.AccountingPostingEvents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == postingEventId, cancellationToken)
                           ?? throw new NotFoundException("حدث الترحيل", postingEventId);

        if (postingEvent.Status != PostingEventStatus.Failed)
        {
            throw new BusinessRuleException($"حالة الحدث «{AccountingNames.Arabic(postingEvent.Status)}»؛ تُعاد محاولة الأحداث الفاشلة فقط.");
        }

        if (postingEvent.OutboxMessageId is null)
        {
            throw new BusinessRuleException("الحدث غير مرتبط برسالة في صندوق الأحداث؛ لا يمكن إعادة محاولته.");
        }

        DateTime? overrideDate = request.PostingDate.HasValue ? BusinessCalendar.AsDate(request.PostingDate.Value) : null;
        if (overrideDate.HasValue)
        {
            // Clear 422 now instead of a new failure record.
            await _ledger.EnsureOpenPeriodAsync(overrideDate.Value, cancellationToken);
        }

        var outcome = await ProcessAsync(postingEvent.OutboxMessageId.Value, overrideDate, retryAfterConflict: false, cancellationToken);

        _dbContext.ChangeTracker.Clear();
        _auditLog.Add(AuditAction.PostingEventRetried, entityName: EntityName, entityId: postingEventId.ToString(CultureInfo.InvariantCulture),
            details: new { postingEvent.EventType, postingEvent.SourceNumber, overrideDate, outcome = outcome.ToString() });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetEventAsync(postingEventId, cancellationToken);
    }

    private async Task<Outcome> ProcessAsync(long outboxMessageId, DateTime? overrideDate, bool retryAfterConflict, CancellationToken cancellationToken)
    {
        var attempt = new Attempt();
        try
        {
            return await _dbContext.ExecuteInTransactionAsync(ct => ApplyAsync(outboxMessageId, overrideDate, attempt, ct), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateException exception) when (!retryAfterConflict && BillingAccess.IsUniqueViolation(exception))
        {
            // Another worker posted the same event or document at the same moment. Run once more: the duplicate
            // checks now see its row and nothing is posted twice.
            _dbContext.ChangeTracker.Clear();
            return await ProcessAsync(outboxMessageId, overrideDate, retryAfterConflict: true, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Accounting posting of outbox message {OutboxMessageId} failed: {Error}", outboxMessageId, exception.Message);
            await RecordFailureAsync(outboxMessageId, attempt, Describe(exception), cancellationToken);
            return Outcome.Failed;
        }
    }

    private async Task<Outcome> ApplyAsync(long outboxMessageId, DateTime? overrideDate, Attempt attempt, CancellationToken ct)
    {
        // Everything is read again inside the transaction (also when the execution strategy retries).
        _dbContext.ChangeTracker.Clear();
        _resolver.Reset();

        var message = await _dbContext.OutboxMessages.FirstOrDefaultAsync(x => x.Id == outboxMessageId, ct);
        if (message is null)
        {
            return Outcome.NotFound;
        }

        if (message.ProcessedAtUtc is not null)
        {
            return Outcome.AlreadyDone;
        }

        var postingEvent = await _dbContext.AccountingPostingEvents.FirstOrDefaultAsync(x => x.EventId == message.EventId, ct);
        if (postingEvent is not null && postingEvent.Status != PostingEventStatus.Failed)
        {
            // Handled before, but the outbox row was not marked: mark it now, post nothing.
            message.ProcessedAtUtc = _access.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return Outcome.AlreadyDone;
        }

        var plan = await _translator.TranslateAsync(message, ct);
        attempt.Plan = plan;
        var postingDate = overrideDate ?? _access.Calendar.BusinessDate(plan.PostingDateUtc);
        attempt.PostingDate = postingDate;

        // ---- no accounting effect
        if (plan.NoImpactReason is not null)
        {
            postingEvent = Track(postingEvent, message, plan, postingDate);
            postingEvent.Status = PostingEventStatus.Skipped;
            postingEvent.LastError = LedgerPosting.Clip(plan.NoImpactReason, 1000);
            Finish(message, postingEvent);
            await _dbContext.SaveChangesAsync(ct);
            return Outcome.Skipped;
        }

        // ---- the same business document was already posted (published twice)
        var existingEntryId = await _dbContext.JournalEntries.AsNoTracking()
            .Where(x => x.SourceKey == plan.SourceKey)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (existingEntryId.HasValue)
        {
            postingEvent = Track(postingEvent, message, plan, postingDate);
            postingEvent.Status = PostingEventStatus.Duplicate;
            postingEvent.JournalEntryId = existingEntryId;
            postingEvent.LastError = null;
            Finish(message, postingEvent);
            await _dbContext.SaveChangesAsync(ct);
            return Outcome.Duplicate;
        }

        // ---- checks first (nothing is tracked or changed yet)
        var period = await _ledger.EnsureOpenPeriodAsync(postingDate, ct);
        var description = plan.Description;
        if (overrideDate.HasValue)
        {
            description += string.Create(CultureInfo.InvariantCulture,
                $" (تاريخ العملية الأصلي {_access.Calendar.BusinessDate(plan.PostingDateUtc):yyyy-MM-dd})");
        }

        JournalEntry? original = plan.IsReversal ? await FindOriginalAsync(plan, ct) : null;
        JournalEntry? entry = null;
        if (original is null)
        {
            entry = await BuildEntryAsync(plan, period, postingDate, description, ct);
            await _ledger.PrepareAsync(entry, manual: false, ct);
        }

        // ---- number, then the changes (the in-memory number generator saves the context)
        var number = await _ledger.NextNumberAsync(ct);

        if (original is not null)
        {
            entry = _ledger.CreateReversal(original, period, postingDate, number, description, automatic: true, plan.SourceKey, plan.EventType);
            entry.SourceNumber = Clip(plan.SourceNumber, 50) ?? original.SourceNumber;
        }
        else
        {
            entry!.EntryNumber = number;
            _ledger.MarkPosted(entry);
            _dbContext.JournalEntries.Add(entry);
        }

        postingEvent = Track(postingEvent, message, plan, postingDate);
        postingEvent.Status = PostingEventStatus.Posted;
        postingEvent.JournalEntry = entry;
        postingEvent.LastError = null;
        Finish(message, postingEvent);

        await _dbContext.SaveChangesAsync(ct);
        return Outcome.Posted;
    }

    private async Task<JournalEntry> BuildEntryAsync(PostingPlan plan, AccountingPeriod period, DateTime postingDate, string description, CancellationToken ct)
    {
        var defaultBranchId = plan.BranchId ?? plan.Lines.Select(x => x.BranchId).FirstOrDefault(x => x.HasValue) ?? await _access.DefaultBranchIdAsync(ct);

        var entry = new JournalEntry
        {
            JournalType = plan.JournalType,
            Status = JournalEntryStatus.Posted,
            EntryDate = BusinessCalendar.AsDate(postingDate),
            AccountingPeriodId = period.Id,
            BranchId = defaultBranchId,
            CurrencyId = plan.CurrencyId,
            ExchangeRate = await _access.ExchangeRateAsync(plan.CurrencyId, postingDate, ct),
            Description = LedgerPosting.Clip(string.IsNullOrWhiteSpace(description) ? plan.EventType : description, 500),
            IsSystemGenerated = true,
            SourceType = Clip(plan.SourceType, 50),
            SourceId = plan.SourceId,
            SourceNumber = Clip(plan.SourceNumber, 50),
            SourceEventType = Clip(plan.EventType, 100),
            SourceKey = plan.SourceKey
        };

        var lineNumber = 0;
        foreach (var line in plan.Lines)
        {
            var role = AccountRoleNames.Canonical(line.AccountRole);
            var branchId = line.BranchId ?? defaultBranchId;
            var account = await _resolver.ResolveAsync(
                new PostingScope(role, branchId, plan.CurrencyId, line.CashboxId, line.BankAccountId, line.ExpenseCategoryId), ct);

            entry.Lines.Add(new JournalEntryLine
            {
                LineNumber = ++lineNumber,
                AccountId = account.AccountId,
                Description = Clip(line.Description, 300) ?? LedgerPosting.Clip(AccountRoleNames.Arabic(role), 300),
                Debit = line.Debit,
                Credit = line.Credit,
                BranchId = branchId,
                AccountRole = role,
                CustomerId = line.CustomerId,
                SupplierId = line.SupplierId,
                DriverId = line.DriverId,
                EmployeeId = line.EmployeeId,
                CashboxId = line.CashboxId,
                BankAccountId = line.BankAccountId,
                ExpenseCategoryId = line.ExpenseCategoryId
            });
        }

        return entry;
    }

    /// <summary>The posted entry a cancellation event reverses exactly (tracked, with lines and dimensions).</summary>
    private async Task<JournalEntry?> FindOriginalAsync(PostingPlan plan, CancellationToken ct)
    {
        var entries = _dbContext.JournalEntries.Include(x => x.Lines).ThenInclude(x => x.Dimensions);

        if (plan.ReverseSourceKey is not null)
        {
            return await entries.FirstOrDefaultAsync(x => x.SourceKey == plan.ReverseSourceKey && x.Status == JournalEntryStatus.Posted, ct);
        }

        if (!plan.ReverseLatestOfSource || string.IsNullOrEmpty(plan.SourceType))
        {
            return null;
        }

        var total = plan.Lines.Sum(x => x.Debit);
        return await entries
            .Where(x => x.IsSystemGenerated &&
                        x.Status == JournalEntryStatus.Posted &&
                        x.ReversalOfEntryId == null &&
                        x.SourceType == plan.SourceType &&
                        x.SourceId == plan.SourceId &&
                        x.CurrencyId == plan.CurrencyId &&
                        x.TotalDebit == total)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    private AccountingPostingEvent Track(AccountingPostingEvent? postingEvent, Domain.Entities.Integration.OutboxMessage message, PostingPlan plan, DateTime postingDate)
    {
        if (postingEvent is null)
        {
            postingEvent = new AccountingPostingEvent
            {
                EventId = message.EventId,
                OutboxMessageId = message.Id,
                EventType = Clip(message.EventType, 100)!,
                OccurredAtUtc = message.OccurredAtUtc
            };
            _dbContext.AccountingPostingEvents.Add(postingEvent);
        }

        postingEvent.SourceType = Clip(plan.SourceType, 50);
        postingEvent.SourceId = plan.SourceId == 0 ? null : plan.SourceId;
        postingEvent.SourceNumber = Clip(plan.SourceNumber, 50);
        postingEvent.SourceKey = Clip(plan.SourceKey, 200);
        postingEvent.PostingDate = BusinessCalendar.AsDate(postingDate);
        return postingEvent;
    }

    private void Finish(Domain.Entities.Integration.OutboxMessage message, AccountingPostingEvent postingEvent)
    {
        var now = _access.UtcNow;
        message.Attempts++;
        message.LastError = null;
        message.ProcessedAtUtc = now;
        postingEvent.Attempts = message.Attempts;
        postingEvent.ProcessedAtUtc = now;
    }

    private async Task RecordFailureAsync(long outboxMessageId, Attempt attempt, string error, CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();

        var message = await _dbContext.OutboxMessages.FirstOrDefaultAsync(x => x.Id == outboxMessageId, cancellationToken);
        if (message is null || message.ProcessedAtUtc is not null)
        {
            return;
        }

        message.Attempts++;
        message.LastError = LedgerPosting.Clip(error, 2000);

        var postingEvent = await _dbContext.AccountingPostingEvents.FirstOrDefaultAsync(x => x.EventId == message.EventId, cancellationToken);
        if (postingEvent is null)
        {
            postingEvent = new AccountingPostingEvent
            {
                EventId = message.EventId,
                OutboxMessageId = message.Id,
                EventType = Clip(message.EventType, 100)!,
                OccurredAtUtc = message.OccurredAtUtc
            };
            _dbContext.AccountingPostingEvents.Add(postingEvent);
        }

        if (attempt.Plan is not null)
        {
            postingEvent.SourceType = Clip(attempt.Plan.SourceType, 50);
            postingEvent.SourceId = attempt.Plan.SourceId == 0 ? null : attempt.Plan.SourceId;
            postingEvent.SourceNumber = Clip(attempt.Plan.SourceNumber, 50);
            postingEvent.SourceKey = Clip(attempt.Plan.SourceKey, 200);
        }

        postingEvent.PostingDate = attempt.PostingDate.HasValue ? BusinessCalendar.AsDate(attempt.PostingDate.Value) : null;
        postingEvent.Status = PostingEventStatus.Failed;
        postingEvent.Attempts = message.Attempts;
        postingEvent.LastError = LedgerPosting.Clip(error, 1000);
        postingEvent.JournalEntryId = null;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // A concurrent worker succeeded meanwhile; its result stands.
            _logger.LogWarning(exception, "Could not record the posting failure of outbox message {OutboxMessageId}", outboxMessageId);
            _dbContext.ChangeTracker.Clear();
        }
    }

    private static string Describe(Exception exception) => exception switch
    {
        RequestValidationException validation => string.Join(" ", validation.Errors.Values.SelectMany(x => x)),
        AppException app => app.Message,
        DbUpdateException db => "خطأ في قاعدة البيانات أثناء الترحيل: " + (db.InnerException?.Message ?? db.Message),
        _ => exception.Message
    };

    private static string? Clip(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= maxLength ? value : value[..maxLength];

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<PostingEventDto>> GetEventsAsync(PostingEventListQuery query, CancellationToken cancellationToken)
    {
        var rows = _dbContext.AccountingPostingEvents.AsNoTracking();

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            var eventType = query.EventType.Trim();
            rows = rows.Where(x => x.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(query.SourceType))
        {
            var sourceType = query.SourceType.Trim();
            rows = rows.Where(x => x.SourceType == sourceType);
        }

        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value;
            rows = rows.Where(x => x.OccurredAtUtc >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = query.ToUtc.Value;
            rows = rows.Where(x => x.OccurredAtUtc < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => (x.SourceNumber != null && x.SourceNumber.Contains(search)) || x.EventType.Contains(search));
        }

        var total = await rows.CountAsync(cancellationToken);
        var ordered = string.Equals(query.SortBy, "occurredAtUtc", StringComparison.OrdinalIgnoreCase) && !query.IsDescending()
            ? rows.OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id)
            : rows.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id);

        var items = await ordered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(AccountingProjections.PostingEvent)
            .ToListAsync(cancellationToken);

        return new PagedResult<PostingEventDto>(items.Select(x => x.WithNames()).ToList(), query.PageNumber, query.PageSize, total);
    }

    public async Task<PostingEventDto> GetEventAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await _dbContext.AccountingPostingEvents.AsNoTracking()
                      .Where(x => x.Id == id)
                      .Select(AccountingProjections.PostingEvent)
                      .FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("حدث الترحيل", id);
        return dto.WithNames();
    }

    public async Task<PostingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var pending = _dbContext.OutboxMessages.AsNoTracking().Where(x => x.ProcessedAtUtc == null);
        var events = _dbContext.AccountingPostingEvents.AsNoTracking();

        return new PostingSummaryDto
        {
            PendingOutboxMessages = await pending.CountAsync(cancellationToken),
            OldestPendingAtUtc = await pending.OrderBy(x => x.OccurredAtUtc).Select(x => (DateTime?)x.OccurredAtUtc).FirstOrDefaultAsync(cancellationToken),
            PostedEvents = await events.CountAsync(x => x.Status == PostingEventStatus.Posted, cancellationToken),
            SkippedEvents = await events.CountAsync(x => x.Status == PostingEventStatus.Skipped, cancellationToken),
            DuplicateEvents = await events.CountAsync(x => x.Status == PostingEventStatus.Duplicate, cancellationToken),
            FailedEvents = await events.CountAsync(x => x.Status == PostingEventStatus.Failed, cancellationToken),
            LastProcessedAtUtc = await events.OrderByDescending(x => x.ProcessedAtUtc).Select(x => x.ProcessedAtUtc).FirstOrDefaultAsync(cancellationToken)
        };
    }
}
