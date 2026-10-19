using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>
/// Manual journal entries (القيود اليدوية) and their workflow (step 7):
///
///   create / edit (Draft, number DRF-...) -> submit -> approve (another user) -> post (JV-..., general ledger)
///   Submitted / Approved -> reject (reason) -> edit again
///   Posted -> reverse (a new posted reversal entry; the original becomes Reversed)
///
/// Posted entries are never edited or deleted (JournalIntegrityInterceptor + SQL triggers). Every step that changes
/// the ledger runs in one transaction with the period row locked (LedgerPosting.EnsureOpenPeriodAsync).
/// </summary>
public sealed class JournalEntryService : IJournalEntryService
{
    private const string EntityName = "JournalEntry";

    private readonly AppDbContext _dbContext;
    private readonly AccountingAccess _access;
    private readonly LedgerPosting _ledger;
    private readonly AuditLogWriter _auditLog;

    public JournalEntryService(AppDbContext dbContext, AccountingAccess access, LedgerPosting ledger, AuditLogWriter auditLog)
    {
        _dbContext = dbContext;
        _access = access;
        _ledger = ledger;
        _auditLog = auditLog;
    }

    private AppDbContext Db => _dbContext;

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<JournalEntryListItemDto>> GetPagedAsync(JournalEntryListQuery query, CancellationToken cancellationToken)
    {
        var rows = _access.Entries();

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.JournalType.HasValue)
        {
            var journalType = query.JournalType.Value;
            rows = rows.Where(x => x.JournalType == journalType);
        }

        if (query.FromDate.HasValue)
        {
            var from = BusinessCalendar.AsDate(query.FromDate.Value);
            rows = rows.Where(x => x.EntryDate >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = BusinessCalendar.AsDate(query.ToDate.Value);
            rows = rows.Where(x => x.EntryDate <= to);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId || x.Lines.Any(l => l.BranchId == branchId));
        }

        if (query.AccountId.HasValue)
        {
            var accountId = query.AccountId.Value;
            rows = rows.Where(x => x.Lines.Any(l => l.AccountId == accountId));
        }

        if (!string.IsNullOrWhiteSpace(query.SourceType))
        {
            var sourceType = query.SourceType.Trim();
            rows = rows.Where(x => x.SourceType == sourceType);
        }

        if (query.IsSystemGenerated.HasValue)
        {
            var system = query.IsSystemGenerated.Value;
            rows = rows.Where(x => x.IsSystemGenerated == system);
        }

        if (query.AccountingPeriodId.HasValue)
        {
            var periodId = query.AccountingPeriodId.Value;
            rows = rows.Where(x => x.AccountingPeriodId == periodId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.EntryNumber.Contains(search) ||
                                   x.Description.Contains(search) ||
                                   (x.Reference != null && x.Reference.Contains(search)) ||
                                   (x.SourceNumber != null && x.SourceNumber.Contains(search)));
        }

        var total = await rows.CountAsync(cancellationToken);
        var descending = query.IsDescending();
        IOrderedQueryable<JournalEntry> ordered = query.SortBy?.ToLowerInvariant() switch
        {
            "entrynumber" => descending ? rows.OrderByDescending(x => x.EntryNumber) : rows.OrderBy(x => x.EntryNumber),
            "totaldebit" => descending ? rows.OrderByDescending(x => x.TotalDebitBase) : rows.OrderBy(x => x.TotalDebitBase),
            "entrydate" => descending ? rows.OrderByDescending(x => x.EntryDate) : rows.OrderBy(x => x.EntryDate),
            _ => rows.OrderByDescending(x => x.EntryDate)
        };

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(AccountingProjections.JournalListItem)
            .ToListAsync(cancellationToken);

        return new PagedResult<JournalEntryListItemDto>(items.Select(x => x.WithNames()).ToList(), query.PageNumber, query.PageSize, total);
    }

    public async Task<JournalEntryDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await _access.Entries().Where(x => x.Id == id).Select(AccountingProjections.Journal).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("القيد", id);
        return dto.WithNames();
    }

    // ------------------------------------------------------------------ create / edit / delete

    public async Task<JournalCreationResult> CreateAsync(SaveJournalEntryRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var key = Idempotency.NormalizeKey(idempotencyKey);
        var userId = _access.CurrentUser.UserId;
        string? sourceKey = null;
        string? hash = null;

        if (key is not null)
        {
            sourceKey = string.Create(CultureInfo.InvariantCulture, $"manual:{userId ?? 0}:{key}");
            hash = Idempotency.Hash(request);

            var replay = await FindReplayAsync(sourceKey, hash, userId, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        var entry = new JournalEntry
        {
            Status = JournalEntryStatus.Draft,
            IsSystemGenerated = false,
            SourceKey = sourceKey,
            RequestHash = hash
        };

        await FillAsync(entry, request, cancellationToken);
        if (request.Submit)
        {
            MarkSubmitted(entry);
        }

        try
        {
            await Db.ExecuteInTransactionAsync(async ct =>
            {
                entry.EntryNumber = await _ledger.NextDraftNumberAsync(ct);
                Db.JournalEntries.Add(entry);
                await Db.SaveChangesAsync(ct);

                Audit(AuditAction.JournalEntryCreated, entry.Id, new
                {
                    entry.EntryNumber,
                    entry.JournalType,
                    entry.EntryDate,
                    entry.TotalDebit,
                    entry.Status,
                    lines = entry.Lines.Count
                });
                await Db.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (sourceKey is not null && BillingAccess.IsUniqueViolation(exception))
        {
            // The same Idempotency-Key arrived twice at the same moment: the first request won.
            Db.ChangeTracker.Clear();
            var replay = await FindReplayAsync(sourceKey, hash!, userId, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }

            throw;
        }

        return new JournalCreationResult(await GetByIdAsync(entry.Id, cancellationToken), false);
    }

    public async Task<JournalEntryDto> UpdateAsync(long id, SaveJournalEntryRequest request, CancellationToken cancellationToken)
    {
        var entry = await LoadTrackedAsync(id, withLines: true, cancellationToken);
        if (!JournalRules.CanEdit(entry.Status))
        {
            throw new BusinessRuleException($"القيد {entry.EntryNumber} في حالة «{AccountingNames.Arabic(entry.Status)}»؛ يُعدّل القيد المسودة أو المرفوض فقط.");
        }

        var before = new { entry.EntryDate, entry.TotalDebit, entry.Description, lines = entry.Lines.Count };

        Db.JournalEntryLines.RemoveRange(entry.Lines);
        entry.Lines.Clear();
        await FillAsync(entry, request, cancellationToken);

        entry.Status = JournalEntryStatus.Draft;
        if (request.Submit)
        {
            MarkSubmitted(entry);
        }

        Audit(AuditAction.JournalEntryUpdated, entry.Id, new { before, after = new { entry.EntryDate, entry.TotalDebit, entry.Description, lines = entry.Lines.Count, entry.Status } });
        await Db.ExecuteInTransactionAsync(async ct =>
        {
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var entry = await LoadTrackedAsync(id, withLines: true, cancellationToken);
        if (!JournalRules.CanDelete(entry.Status))
        {
            throw new BusinessRuleException(
                $"القيد {entry.EntryNumber} في حالة «{AccountingNames.Arabic(entry.Status)}» ولا يُحذف. القيد المرحّل يُصحّح بقيد عكسي.");
        }

        Audit(AuditAction.JournalEntryDeleted, entry.Id, new { entry.EntryNumber, entry.EntryDate, entry.TotalDebit, entry.Description });
        Db.JournalEntryLines.RemoveRange(entry.Lines);
        Db.JournalEntries.Remove(entry);
        await Db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ workflow

    public async Task<JournalEntryDto> SubmitAsync(long id, CancellationToken cancellationToken)
    {
        var entry = await LoadTrackedAsync(id, withLines: true, cancellationToken);
        if (!JournalRules.CanSubmit(entry.Status))
        {
            throw new BusinessRuleException($"القيد {entry.EntryNumber} في حالة «{AccountingNames.Arabic(entry.Status)}» ولا يُقدَّم للاعتماد.");
        }

        await _ledger.EnsureOpenPeriodAsync(entry.EntryDate, cancellationToken);
        await _ledger.ValidateAsync(entry, manual: true, cancellationToken);

        MarkSubmitted(entry);
        Audit(AuditAction.JournalEntrySubmitted, entry.Id, new { entry.EntryNumber });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<JournalEntryDto> ApproveAsync(long id, JournalDecisionRequest request, CancellationToken cancellationToken)
    {
        var entry = await LoadTrackedAsync(id, withLines: true, cancellationToken);
        if (!JournalRules.CanApprove(entry.Status))
        {
            throw new BusinessRuleException($"القيد {entry.EntryNumber} في حالة «{AccountingNames.Arabic(entry.Status)}»؛ يُعتمد القيد المقدَّم فقط.");
        }

        _access.EnsureOtherApprover(entry.CreatedByUserId, "القيد");
        _access.EnsureOtherApprover(entry.SubmittedByUserId, "القيد");

        await _ledger.EnsureOpenPeriodAsync(entry.EntryDate, cancellationToken);
        await _ledger.ValidateAsync(entry, manual: true, cancellationToken);

        entry.Status = JournalEntryStatus.Approved;
        entry.ApprovedAtUtc = _access.UtcNow;
        entry.ApprovedByUserId = _access.CurrentUser.UserId;

        Audit(AuditAction.JournalEntryApproved, entry.Id, new { entry.EntryNumber, notes = Trim(request.Notes) });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<JournalEntryDto> RejectAsync(long id, JournalReasonRequest request, CancellationToken cancellationToken)
    {
        var entry = await LoadTrackedAsync(id, withLines: false, cancellationToken);
        if (!JournalRules.CanReject(entry.Status))
        {
            throw new BusinessRuleException($"القيد {entry.EntryNumber} في حالة «{AccountingNames.Arabic(entry.Status)}» ولا يُرفض.");
        }

        var reason = request.Reason.Trim();
        entry.Status = JournalEntryStatus.Rejected;
        entry.RejectedAtUtc = _access.UtcNow;
        entry.RejectedByUserId = _access.CurrentUser.UserId;
        entry.RejectionReason = LedgerPosting.Clip(reason, 500);
        entry.ApprovedAtUtc = null;
        entry.ApprovedByUserId = null;

        Audit(AuditAction.JournalEntryRejected, entry.Id, new { entry.EntryNumber, reason });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<JournalEntryDto> PostAsync(long id, CancellationToken cancellationToken)
    {
        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var entry = await LoadTrackedAsync(id, withLines: true, ct);
            if (!JournalRules.CanPost(entry.Status))
            {
                throw new BusinessRuleException(
                    $"القيد {entry.EntryNumber} في حالة «{AccountingNames.Arabic(entry.Status)}»؛ يُرحّل القيد المعتمد فقط.");
            }

            // The period row stays locked until the commit: a concurrent closing waits (see LedgerPosting).
            var period = await _ledger.EnsureOpenPeriodAsync(entry.EntryDate, ct);
            await _ledger.ValidateAsync(entry, manual: true, ct);

            var draftNumber = entry.EntryNumber;
            var number = await _ledger.NextNumberAsync(ct);

            entry.EntryNumber = number;
            entry.AccountingPeriodId = period.Id;
            _ledger.MarkPosted(entry);

            Audit(AuditAction.JournalEntryPosted, entry.Id, new { draftNumber, entry.EntryNumber, entry.EntryDate, entry.TotalDebitBase });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<JournalEntryDto> ReverseAsync(long id, ReverseJournalEntryRequest request, CancellationToken cancellationToken)
    {
        var reason = request.Reason.Trim();
        var reversalDate = BusinessCalendar.AsDate(request.ReversalDate ?? _access.Today);

        var reversalId = await Db.ExecuteInTransactionAsync(async ct =>
        {
            var original = await LoadTrackedAsync(id, withLines: true, ct);
            if (!JournalRules.CanReverse(original.Status))
            {
                throw new BusinessRuleException(
                    $"القيد {original.EntryNumber} في حالة «{AccountingNames.Arabic(original.Status)}»؛ يُعكس القيد المرحّل فقط.");
            }

            if (original.IsSystemGenerated)
            {
                throw new BusinessRuleException(
                    $"القيد {original.EntryNumber} آلي من المستند {original.SourceNumber}. ألغِ المستند المصدر (أو اعكسه) ليُعكس قيده آلياً، " +
                    "أو صحّح الحسابات بقيد تسوية؛ عكسه يدوياً يفصل الأستاذ العام عن الدفتر المساعد.");
            }

            if (original.ReversalOfEntryId.HasValue)
            {
                throw new BusinessRuleException($"القيد {original.EntryNumber} قيد عكسي؛ لا يُعكس مرة أخرى. أنشئ قيداً جديداً إذا لزم.");
            }

            if (reversalDate < original.EntryDate)
            {
                throw AccountingAccess.Invalid("reversalDate", $"تاريخ القيد العكسي يجب ألا يسبق تاريخ القيد الأصلي {original.EntryDate:yyyy-MM-dd}.");
            }

            var period = await _ledger.EnsureOpenPeriodAsync(reversalDate, ct);
            var number = await _ledger.NextNumberAsync(ct);

            var reversal = _ledger.CreateReversal(original, period, reversalDate, number, reason, automatic: false,
                sourceKey: string.Create(CultureInfo.InvariantCulture, $"reversal:{original.Id}"));

            await Db.SaveChangesAsync(ct);
            Audit(AuditAction.JournalEntryReversed, original.Id, new { original.EntryNumber, reversal = reversal.EntryNumber, reversalDate, reason });
            await Db.SaveChangesAsync(ct);
            return reversal.Id;
        }, cancellationToken);

        return await GetByIdAsync(reversalId, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<JournalCreationResult?> FindReplayAsync(string sourceKey, string hash, long? userId, CancellationToken cancellationToken)
    {
        var existing = await Db.JournalEntries.AsNoTracking()
            .Where(x => x.SourceKey == sourceKey)
            .Select(x => new { x.Id, x.RequestHash, x.CreatedByUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return null;
        }

        Idempotency.EnsureSameRequest(existing.RequestHash ?? string.Empty, existing.CreatedByUserId, hash, userId);
        return new JournalCreationResult(await GetByIdAsync(existing.Id, cancellationToken), true);
    }

    private async Task<JournalEntry> LoadTrackedAsync(long id, bool withLines, CancellationToken cancellationToken)
    {
        var query = _access.Entries(tracked: true);
        if (withLines)
        {
            query = query.Include(x => x.Lines).ThenInclude(x => x.Dimensions);
        }

        return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("القيد", id);
    }

    private void MarkSubmitted(JournalEntry entry)
    {
        entry.Status = JournalEntryStatus.Submitted;
        entry.SubmittedAtUtc = _access.UtcNow;
        entry.SubmittedByUserId = _access.CurrentUser.UserId;
        entry.RejectedAtUtc = null;
        entry.RejectedByUserId = null;
        entry.RejectionReason = null;
    }

    /// <summary>Fills the header and the lines of a new or edited entry from the request and validates everything.</summary>
    private async Task FillAsync(JournalEntry entry, SaveJournalEntryRequest request, CancellationToken cancellationToken)
    {
        var entryDate = BusinessCalendar.AsDate(request.EntryDate);
        var period = await _ledger.EnsureOpenPeriodAsync(entryDate, cancellationToken);

        var baseCurrency = await _access.BaseCurrencyAsync(cancellationToken);
        var currencyId = request.CurrencyId ?? baseCurrency.Id;
        var currencyActive = await Db.Currencies.AsNoTracking().Where(x => x.Id == currencyId).Select(x => (bool?)x.IsActive).FirstOrDefaultAsync(cancellationToken);
        if (currencyActive != true)
        {
            throw AccountingAccess.Invalid("currencyId", "العملة غير موجودة أو غير مفعّلة.");
        }

        decimal rate;
        if (currencyId == baseCurrency.Id)
        {
            if (request.ExchangeRate.HasValue && request.ExchangeRate.Value != 1m)
            {
                throw AccountingAccess.Invalid("exchangeRate", "سعر صرف العملة الأساسية يساوي 1.");
            }

            rate = 1m;
        }
        else
        {
            rate = request.ExchangeRate ?? await _access.ExchangeRateAsync(currencyId, entryDate, cancellationToken);
        }

        var branchId = request.BranchId ?? _access.BranchAccess.CurrentBranchId ?? await _access.DefaultBranchIdAsync(cancellationToken);
        _access.BranchAccess.EnsureCanAccess(branchId);

        entry.EntryDate = entryDate;
        entry.AccountingPeriodId = period.Id;
        entry.JournalType = request.JournalType;
        entry.CurrencyId = currencyId;
        entry.ExchangeRate = rate;
        entry.BranchId = branchId;
        entry.Description = request.Description.Trim();
        entry.Reference = Trim(request.Reference);

        var lines = await BuildLinesAsync(request.Lines, branchId, cancellationToken);
        foreach (var line in lines)
        {
            entry.Lines.Add(line);
        }

        await _ledger.PrepareAsync(entry, manual: true, cancellationToken);
    }

    /// <summary>Lines with their branch and dimensions; every referenced record must exist (400 with the line field).</summary>
    private async Task<List<JournalEntryLine>> BuildLinesAsync(IReadOnlyList<JournalLineRequest> requests, long entryBranchId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        void Error(int index, string field, string message) =>
            errors[string.Create(CultureInfo.InvariantCulture, $"lines[{index}].{field}")] = new[] { message };

        // Branches.
        var branchIds = requests.Select(x => x.BranchId ?? entryBranchId).Append(entryBranchId).Distinct().ToList();
        var activeBranches = await Db.Branches.AsNoTracking().Where(x => branchIds.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToListAsync(cancellationToken);

        // Dimensions (cost center shortcut + the list).
        var dimensionIds = requests.SelectMany(x => x.Dimensions.Select(d => d.DimensionId).Concat(x.CostCenterId.HasValue ? new[] { x.CostCenterId.Value } : Array.Empty<long>()))
            .Distinct().ToList();
        var dimensions = await Db.AccountingDimensions.AsNoTracking()
            .Where(x => dimensionIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DimensionType, x.IsActive })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        // Sub-ledger references.
        var customers = await ExistingAsync(Db.Customers.AsNoTracking().Select(x => x.Id), requests.Select(x => x.CustomerId), cancellationToken);
        var suppliers = await ExistingAsync(Db.Suppliers.AsNoTracking().Select(x => x.Id), requests.Select(x => x.SupplierId), cancellationToken);
        var drivers = await ExistingAsync(Db.DriverProfiles.AsNoTracking().Select(x => x.Id), requests.Select(x => x.DriverId), cancellationToken);
        var employees = await ExistingAsync(Db.Employees.AsNoTracking().Select(x => x.Id), requests.Select(x => x.EmployeeId), cancellationToken);
        var cashboxes = await ExistingAsync(Db.Cashboxes.AsNoTracking().Select(x => x.Id), requests.Select(x => x.CashboxId), cancellationToken);
        var banks = await ExistingAsync(Db.BankAccounts.AsNoTracking().Select(x => x.Id), requests.Select(x => x.BankAccountId), cancellationToken);
        var categories = await ExistingAsync(Db.ExpenseCategories.AsNoTracking().Select(x => x.Id), requests.Select(x => x.ExpenseCategoryId), cancellationToken);

        var lines = new List<JournalEntryLine>();
        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var branchId = request.BranchId ?? entryBranchId;

            if (!activeBranches.Contains(branchId))
            {
                Error(i, "branchId", "الفرع غير موجود أو غير مفعّل.");
            }
            else if (!_access.BranchAccess.CanAccess(branchId))
            {
                Error(i, "branchId", "لا يمكنك القيد على فرع آخر.");
            }

            var lineDimensions = request.Dimensions.Select(d => (d.DimensionType, d.DimensionId)).ToList();
            if (request.CostCenterId.HasValue)
            {
                if (lineDimensions.Any(d => d.DimensionType == DimensionType.CostCenter))
                {
                    Error(i, "costCenterId", "مركز التكلفة مكرر في السطر (costCenterId و dimensions).");
                }
                else
                {
                    lineDimensions.Add((DimensionType.CostCenter, request.CostCenterId.Value));
                }
            }

            foreach (var (type, dimensionId) in lineDimensions)
            {
                if (!dimensions.TryGetValue(dimensionId, out var dimension) || !dimension.IsActive)
                {
                    Error(i, "dimensions", $"البعد {dimensionId} غير موجود أو غير مفعّل.");
                }
                else if (dimension.DimensionType != type)
                {
                    Error(i, "dimensions", $"البعد {dimensionId} من نوع «{AccountingNames.Arabic(dimension.DimensionType)}» وليس «{AccountingNames.Arabic(type)}».");
                }
            }

            Check(i, "customerId", request.CustomerId, customers, "العميل غير موجود.");
            Check(i, "supplierId", request.SupplierId, suppliers, "المورد غير موجود.");
            Check(i, "driverId", request.DriverId, drivers, "السائق غير موجود.");
            Check(i, "employeeId", request.EmployeeId, employees, "الموظف غير موجود.");
            Check(i, "cashboxId", request.CashboxId, cashboxes, "الصندوق غير موجود.");
            Check(i, "bankAccountId", request.BankAccountId, banks, "الحساب البنكي غير موجود.");
            Check(i, "expenseCategoryId", request.ExpenseCategoryId, categories, "فئة المصروف غير موجودة.");

            var line = new JournalEntryLine
            {
                LineNumber = i + 1,
                AccountId = request.AccountId,
                Description = Trim(request.Description),
                Debit = request.Debit,
                Credit = request.Credit,
                BranchId = branchId,
                CustomerId = request.CustomerId,
                SupplierId = request.SupplierId,
                DriverId = request.DriverId,
                EmployeeId = request.EmployeeId,
                CashboxId = request.CashboxId,
                BankAccountId = request.BankAccountId,
                ExpenseCategoryId = request.ExpenseCategoryId
            };

            foreach (var (type, dimensionId) in lineDimensions.DistinctBy(d => d.DimensionType))
            {
                line.Dimensions.Add(new JournalEntryDimension { DimensionType = type, AccountingDimensionId = dimensionId });
            }

            lines.Add(line);
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        return lines;

        void Check(int index, string field, long? value, HashSet<long> existing, string message)
        {
            if (value.HasValue && !existing.Contains(value.Value))
            {
                Error(index, field, message);
            }
        }
    }

    private static async Task<HashSet<long>> ExistingAsync(IQueryable<long> source, IEnumerable<long?> requested, CancellationToken cancellationToken)
    {
        var ids = requested.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new HashSet<long>();
        }

        return (await source.Where(id => ids.Contains(id)).ToListAsync(cancellationToken)).ToHashSet();
    }

    private void Audit(AuditAction action, long id, object details) =>
        _auditLog.Add(action, entityName: EntityName, entityId: id.ToString(CultureInfo.InvariantCulture), details: details);

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
