using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>
/// Bank statements and bank reconciliation (التسوية البنكية, step 7).
///
///   1. The statement lines are saved as BankTransactions (an identical unreconciled line already saved is not
///      imported twice).
///   2. Statement lines are matched with the uncleared ledger lines of the bank account: the explicit matches of the
///      request, then (AutoMatch) same signed amount with the same reference, else the closest date within ±7 days.
///   3. Adjusted bank balance = statement balance + deposits in transit − outstanding withdrawals;
///      adjusted book balance = ledger balance + bank-only deposits − bank-only withdrawals; difference = bank − book.
///   4. Completing (difference 0, no statement-only line left) clears the matched pairs for good; a filtered unique
///      index makes it impossible to clear a ledger line or a statement line twice.
/// </summary>
public sealed class BankReconciliationService : IBankReconciliationService
{
    private const string EntityName = "BankReconciliation";
    private const int AutoMatchDays = 7;

    private readonly AppDbContext _dbContext;
    private readonly AccountingAccess _access;
    private readonly PostingAccountResolver _resolver;
    private readonly DocumentNumberGenerator _numbers;
    private readonly AuditLogWriter _auditLog;

    public BankReconciliationService(
        AppDbContext dbContext,
        AccountingAccess access,
        PostingAccountResolver resolver,
        DocumentNumberGenerator numbers,
        AuditLogWriter auditLog)
    {
        _dbContext = dbContext;
        _access = access;
        _resolver = resolver;
        _numbers = numbers;
        _auditLog = auditLog;
    }

    private AppDbContext Db => _dbContext;

    /// <summary>An uncleared ledger line of the bank, in the bank currency (+ money in, − money out).</summary>
    private sealed record LedgerItem(long LineId, long EntryId, string EntryNumber, DateTime Date, string Description, string? Reference, string? SourceNumber, decimal Amount);

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<BankTransactionDto>> GetTransactionsAsync(BankTransactionListQuery query, CancellationToken cancellationToken)
    {
        var rows = Db.BankTransactions.AsNoTracking();

        if (query.BankAccountId.HasValue)
        {
            var bankId = query.BankAccountId.Value;
            rows = rows.Where(x => x.BankAccountId == bankId);
        }

        if (query.IsReconciled.HasValue)
        {
            var reconciled = query.IsReconciled.Value;
            rows = rows.Where(x => x.IsReconciled == reconciled);
        }

        if (query.FromDate.HasValue)
        {
            var from = BusinessCalendar.AsDate(query.FromDate.Value);
            rows = rows.Where(x => x.TransactionDate >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = BusinessCalendar.AsDate(query.ToDate.Value);
            rows = rows.Where(x => x.TransactionDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Description.Contains(search) || (x.Reference != null && x.Reference.Contains(search)));
        }

        var total = await rows.CountAsync(cancellationToken);
        var ordered = string.Equals(query.SortBy, "transactionDate", StringComparison.OrdinalIgnoreCase) && !query.IsDescending()
            ? rows.OrderBy(x => x.TransactionDate).ThenBy(x => x.Id)
            : rows.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id);

        var items = await ordered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new BankTransactionDto
            {
                Id = x.Id,
                BankAccountId = x.BankAccountId,
                BankAccountName = x.BankAccount.ArabicName,
                TransactionDate = x.TransactionDate,
                Description = x.Description,
                Reference = x.Reference,
                Deposit = x.Deposit,
                Withdrawal = x.Withdrawal,
                IsReconciled = x.IsReconciled,
                BankReconciliationId = x.BankReconciliationId,
                MatchedJournalEntryLineId = x.MatchedJournalEntryLineId
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<BankTransactionDto>(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<PagedResult<BankReconciliationDto>> GetPagedAsync(BankReconciliationListQuery query, CancellationToken cancellationToken)
    {
        var rows = Db.BankReconciliations.AsNoTracking();

        if (query.BankAccountId.HasValue)
        {
            var bankId = query.BankAccountId.Value;
            rows = rows.Where(x => x.BankAccountId == bankId);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.ReconciliationNumber.Contains(search) || x.BankAccount.ArabicName.Contains(search));
        }

        var total = await rows.CountAsync(cancellationToken);
        var ordered = string.Equals(query.SortBy, "statementDate", StringComparison.OrdinalIgnoreCase) && !query.IsDescending()
            ? rows.OrderBy(x => x.StatementDate).ThenBy(x => x.Id)
            : rows.OrderByDescending(x => x.StatementDate).ThenByDescending(x => x.Id);

        var ids = await ordered.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).Select(x => x.Id).ToListAsync(cancellationToken);
        var items = new List<BankReconciliationDto>();
        foreach (var id in ids)
        {
            items.Add(await LoadDtoAsync(id, includeLines: false, cancellationToken));
        }

        return new PagedResult<BankReconciliationDto>(items, query.PageNumber, query.PageSize, total);
    }

    public Task<BankReconciliationDto> GetByIdAsync(long id, CancellationToken cancellationToken) => LoadDtoAsync(id, includeLines: true, cancellationToken);

    // ------------------------------------------------------------------ create / complete

    public async Task<BankReconciliationDto> CreateAsync(CreateBankReconciliationRequest request, CancellationToken cancellationToken)
    {
        var bank = await Db.BankAccounts.AsNoTracking().Include(x => x.Currency).FirstOrDefaultAsync(x => x.Id == request.BankAccountId, cancellationToken)
                   ?? throw AccountingAccess.Invalid("bankAccountId", "الحساب البنكي غير موجود.");
        if (bank.BranchId.HasValue)
        {
            _access.BranchAccess.EnsureCanAccess(bank.BranchId.Value);
        }

        var statementDate = BusinessCalendar.AsDate(request.StatementDate);
        await EnsureAfterLastCompletedAsync(bank.Id, statementDate, cancellationToken);
        var glAccountId = await GlAccountIdAsync(bank, cancellationToken);
        var decimals = bank.Currency.DecimalPlaces;

        var reconciliationId = await Db.ExecuteInTransactionAsync(async ct =>
        {
            var imported = await ImportAsync(bank.Id, request.StatementLines, decimals, ct);

            var reconciliation = new BankReconciliation
            {
                ReconciliationNumber = await _numbers.NextAsync(DocumentPrefixes.BankReconciliation, ct),
                BankAccountId = bank.Id,
                AccountId = glAccountId,
                StatementDate = statementDate,
                StatementEndingBalance = JournalMath.Round(request.StatementEndingBalance, decimals),
                Status = BankReconciliationStatus.Draft,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };

            var matches = request.Matches.Select(x => (x.BankTransactionId, x.JournalEntryLineId)).ToList();
            await CalculateAsync(reconciliation, bank, matches, request.AutoMatch, ct);
            Db.BankReconciliations.Add(reconciliation);
            await Db.SaveChangesAsync(ct);

            if (imported > 0)
            {
                Audit(AuditAction.BankStatementImported, reconciliation.Id, new { bank.Code, imported, statementDate });
            }

            Audit(AuditAction.BankReconciliationSaved, reconciliation.Id, new
            {
                reconciliation.ReconciliationNumber,
                bank.Code,
                statementDate,
                reconciliation.StatementEndingBalance,
                reconciliation.BookBalance,
                reconciliation.Difference
            });

            // Complete = true completes it only when it balances; otherwise it stays a Draft and the response shows
            // the difference and the statement-only lines to book first.
            if (request.Complete && reconciliation.Difference == 0m &&
                reconciliation.Lines.All(x => x.LineType != BankReconciliationLineType.BankOnly))
            {
                Complete(reconciliation);
                Audit(AuditAction.BankReconciliationCompleted, reconciliation.Id, new { reconciliation.ReconciliationNumber });
            }

            await Db.SaveChangesAsync(ct);
            return reconciliation.Id;
        }, cancellationToken);

        return await GetByIdAsync(reconciliationId, cancellationToken);
    }

    public async Task<BankReconciliationDto> CompleteAsync(long id, CompleteBankReconciliationRequest request, CancellationToken cancellationToken)
    {
        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var reconciliation = await Db.BankReconciliations.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct)
                                 ?? throw new NotFoundException("التسوية البنكية", id);
            if (reconciliation.Status != BankReconciliationStatus.Draft)
            {
                throw new BusinessRuleException($"التسوية {reconciliation.ReconciliationNumber} مكتملة من قبل.");
            }

            var bank = await Db.BankAccounts.AsNoTracking().Include(x => x.Currency).FirstAsync(x => x.Id == reconciliation.BankAccountId, ct);
            if (bank.BranchId.HasValue)
            {
                _access.BranchAccess.EnsureCanAccess(bank.BranchId.Value);
            }

            await EnsureAfterLastCompletedAsync(bank.Id, reconciliation.StatementDate, ct);

            // Recalculate with the saved pairs: the ledger may have new entries (e.g. the bank fees just booked).
            var matches = reconciliation.Lines
                .Where(x => x.LineType == BankReconciliationLineType.Matched && x.BankTransactionId.HasValue && x.JournalEntryLineId.HasValue)
                .Select(x => (x.BankTransactionId!.Value, x.JournalEntryLineId!.Value))
                .ToList();

            Db.BankReconciliationLines.RemoveRange(reconciliation.Lines);
            reconciliation.Lines.Clear();
            await CalculateAsync(reconciliation, bank, matches, autoMatch: true, ct);

            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                reconciliation.Notes = request.Notes.Trim();
            }

            Complete(reconciliation);
            Audit(AuditAction.BankReconciliationCompleted, reconciliation.Id, new { reconciliation.ReconciliationNumber, reconciliation.Difference });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ core

    /// <summary>Saves the statement lines not saved yet; returns how many were added.</summary>
    private async Task<int> ImportAsync(long bankAccountId, IReadOnlyList<BankStatementLineRequest> lines, int decimals, CancellationToken ct)
    {
        if (lines.Count == 0)
        {
            return 0;
        }

        var from = lines.Min(x => BusinessCalendar.AsDate(x.TransactionDate));
        var to = lines.Max(x => BusinessCalendar.AsDate(x.TransactionDate));
        var existing = await Db.BankTransactions.AsNoTracking()
            .Where(x => x.BankAccountId == bankAccountId && !x.IsReconciled && x.TransactionDate >= from && x.TransactionDate <= to)
            .Select(x => new { x.TransactionDate, x.Deposit, x.Withdrawal, x.Reference, x.Description })
            .ToListAsync(ct);

        // A line already saved (same date, amounts, reference and description) is consumed once per saved copy, so
        // two identical fees on the same day in one statement are both kept.
        var saved = existing
            .GroupBy(x => Key(x.TransactionDate, x.Deposit, x.Withdrawal, x.Reference, x.Description))
            .ToDictionary(g => g.Key, g => g.Count());

        var added = 0;
        foreach (var line in lines)
        {
            var date = BusinessCalendar.AsDate(line.TransactionDate);
            var deposit = JournalMath.Round(line.Deposit, decimals);
            var withdrawal = JournalMath.Round(line.Withdrawal, decimals);
            var reference = string.IsNullOrWhiteSpace(line.Reference) ? null : line.Reference.Trim();
            var description = line.Description.Trim();
            var key = Key(date, deposit, withdrawal, reference, description);

            if (saved.TryGetValue(key, out var count) && count > 0)
            {
                saved[key] = count - 1;
                continue;
            }

            Db.BankTransactions.Add(new BankTransaction
            {
                BankAccountId = bankAccountId,
                TransactionDate = date,
                Description = description,
                Reference = reference,
                Deposit = deposit,
                Withdrawal = withdrawal
            });
            added++;
        }

        if (added > 0)
        {
            await Db.SaveChangesAsync(ct);
        }

        return added;
    }

    private static string Key(DateTime date, decimal deposit, decimal withdrawal, string? reference, string description) =>
        string.Create(CultureInfo.InvariantCulture, $"{date:yyyyMMdd}|{deposit:0.####}|{withdrawal:0.####}|{reference}|{description}");

    /// <summary>Builds the lines and the totals of a draft reconciliation.</summary>
    private async Task CalculateAsync(
        BankReconciliation reconciliation,
        BankAccount bank,
        IReadOnlyList<(long BankTransactionId, long JournalEntryLineId)> explicitMatches,
        bool autoMatch,
        CancellationToken ct)
    {
        var date = reconciliation.StatementDate;
        var bankCurrencyId = bank.CurrencyId;
        var accountId = reconciliation.AccountId;
        var dedicated = bank.AccountId.HasValue && bank.AccountId == accountId;

        var ledger = Db.JournalEntryLines.AsNoTracking()
            .Where(x => (x.JournalEntry.Status == JournalEntryStatus.Posted || x.JournalEntry.Status == JournalEntryStatus.Reversed) &&
                        x.JournalEntry.EntryDate <= date &&
                        x.AccountId == accountId);
        if (!dedicated)
        {
            ledger = ledger.Where(x => x.BankAccountId == bank.Id);
        }

        // Book balance at the statement date (bank currency; base amounts for an entry in another currency).
        var sums = await ledger
            .GroupBy(x => x.JournalEntry.CurrencyId == bankCurrencyId)
            .Select(g => new
            {
                SameCurrency = g.Key,
                Net = g.Sum(x => x.Debit) - g.Sum(x => x.Credit),
                NetBase = g.Sum(x => x.DebitBase) - g.Sum(x => x.CreditBase)
            })
            .ToListAsync(ct);
        reconciliation.BookBalance = sums.Sum(x => x.SameCurrency ? x.Net : x.NetBase);

        var uncleared = (await ledger
                .Where(x => !Db.BankReconciliationLines.Any(r => r.IsCleared && r.JournalEntryLineId == x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.JournalEntryId,
                    x.JournalEntry.EntryNumber,
                    x.JournalEntry.EntryDate,
                    Description = x.Description ?? x.JournalEntry.Description,
                    x.JournalEntry.Reference,
                    x.JournalEntry.SourceNumber,
                    SameCurrency = x.JournalEntry.CurrencyId == bankCurrencyId,
                    x.Debit,
                    x.Credit,
                    x.DebitBase,
                    x.CreditBase
                })
                .OrderBy(x => x.EntryDate).ThenBy(x => x.Id)
                .ToListAsync(ct))
            .Select(x => new LedgerItem(x.Id, x.JournalEntryId, x.EntryNumber, x.EntryDate, x.Description, x.Reference, x.SourceNumber,
                x.SameCurrency ? x.Debit - x.Credit : x.DebitBase - x.CreditBase))
            .ToList();

        // Tracked: completing the reconciliation marks the matched statement lines as reconciled.
        var statement = await Db.BankTransactions
            .Where(x => x.BankAccountId == bank.Id && !x.IsReconciled && x.TransactionDate <= date)
            .OrderBy(x => x.TransactionDate).ThenBy(x => x.Id)
            .ToListAsync(ct);

        var pairs = new List<(BankTransaction Bank, LedgerItem Book)>();
        var freeLedger = uncleared.ToDictionary(x => x.LineId);
        var freeStatement = statement.ToDictionary(x => x.Id);

        // Explicit matches first.
        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < explicitMatches.Count; i++)
        {
            var (bankTransactionId, lineId) = explicitMatches[i];
            var field = string.Create(CultureInfo.InvariantCulture, $"matches[{i}]");

            if (!freeStatement.TryGetValue(bankTransactionId, out var transaction))
            {
                errors[field] = new[] { $"حركة البنك {bankTransactionId} غير موجودة في كشف هذا الحساب أو مطابقة من قبل أو بعد تاريخ الكشف." };
                continue;
            }

            if (!freeLedger.TryGetValue(lineId, out var item))
            {
                errors[field] = new[] { $"سطر القيد {lineId} ليس سطراً غير مسوّى على حساب هذا البنك حتى تاريخ الكشف." };
                continue;
            }

            if (transaction.Deposit - transaction.Withdrawal != item.Amount)
            {
                errors[field] = new[] { $"مبلغ حركة البنك {transaction.Deposit - transaction.Withdrawal:0.##} لا يساوي مبلغ سطر القيد {item.Amount:0.##}." };
                continue;
            }

            pairs.Add((transaction, item));
            freeStatement.Remove(bankTransactionId);
            freeLedger.Remove(lineId);
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        // Automatic matching: same signed amount; same reference first, else the closest date within ±7 days.
        if (autoMatch)
        {
            foreach (var transaction in freeStatement.Values.OrderBy(x => x.TransactionDate).ThenBy(x => x.Id).ToList())
            {
                var amount = transaction.Deposit - transaction.Withdrawal;
                var candidates = freeLedger.Values.Where(x => x.Amount == amount).ToList();
                if (candidates.Count == 0)
                {
                    continue;
                }

                var reference = transaction.Reference;
                var match = reference is null
                    ? null
                    : candidates.FirstOrDefault(x => x.Reference == reference || x.SourceNumber == reference || x.EntryNumber == reference);

                match ??= candidates
                    .Where(x => Math.Abs((x.Date - transaction.TransactionDate).TotalDays) <= AutoMatchDays)
                    .OrderBy(x => Math.Abs((x.Date - transaction.TransactionDate).TotalDays))
                    .ThenBy(x => x.LineId)
                    .FirstOrDefault();

                if (match is not null)
                {
                    pairs.Add((transaction, match));
                    freeStatement.Remove(transaction.Id);
                    freeLedger.Remove(match.LineId);
                }
            }
        }

        foreach (var (transaction, item) in pairs)
        {
            reconciliation.Lines.Add(new BankReconciliationLine
            {
                LineType = BankReconciliationLineType.Matched,
                BankTransactionId = transaction.Id,
                JournalEntryLineId = item.LineId,
                TransactionDate = transaction.TransactionDate,
                Description = LedgerPosting.Clip(transaction.Description, 300),
                Amount = item.Amount
            });
        }

        foreach (var transaction in freeStatement.Values.OrderBy(x => x.TransactionDate))
        {
            reconciliation.Lines.Add(new BankReconciliationLine
            {
                LineType = BankReconciliationLineType.BankOnly,
                BankTransactionId = transaction.Id,
                TransactionDate = transaction.TransactionDate,
                Description = LedgerPosting.Clip(transaction.Description, 300),
                Amount = transaction.Deposit - transaction.Withdrawal
            });
        }

        foreach (var item in freeLedger.Values.OrderBy(x => x.Date))
        {
            reconciliation.Lines.Add(new BankReconciliationLine
            {
                LineType = BankReconciliationLineType.BookOnly,
                JournalEntryLineId = item.LineId,
                TransactionDate = item.Date,
                Description = LedgerPosting.Clip($"{item.EntryNumber} - {item.Description}", 300),
                Amount = item.Amount
            });
        }

        reconciliation.OutstandingDeposits = freeLedger.Values.Where(x => x.Amount > 0m).Sum(x => x.Amount);
        reconciliation.OutstandingWithdrawals = freeLedger.Values.Where(x => x.Amount < 0m).Sum(x => -x.Amount);
        reconciliation.BankOnlyDeposits = freeStatement.Values.Sum(x => x.Deposit);
        reconciliation.BankOnlyWithdrawals = freeStatement.Values.Sum(x => x.Withdrawal);
        reconciliation.AdjustedBankBalance = reconciliation.StatementEndingBalance + reconciliation.OutstandingDeposits - reconciliation.OutstandingWithdrawals;
        reconciliation.AdjustedBookBalance = reconciliation.BookBalance + reconciliation.BankOnlyDeposits - reconciliation.BankOnlyWithdrawals;
        reconciliation.Difference = reconciliation.AdjustedBankBalance - reconciliation.AdjustedBookBalance;
    }

    /// <summary>Completes a calculated reconciliation: 422 unless the difference is 0 and no statement-only line remains.</summary>
    private void Complete(BankReconciliation reconciliation)
    {
        var bankOnly = reconciliation.Lines.Count(x => x.LineType == BankReconciliationLineType.BankOnly);
        if (bankOnly > 0)
        {
            throw new BusinessRuleException(
                $"في الكشف {bankOnly} حركة غير مسجلة في الدفاتر (عمولات، فوائد، إيداعات غير معروفة). سجّل قيداً لكل منها " +
                "(يومية البنك) ثم أكمل التسوية.");
        }

        if (reconciliation.Difference != 0m)
        {
            throw new BusinessRuleException(
                $"التسوية غير متوازنة: الرصيد المعدّل للبنك {reconciliation.AdjustedBankBalance:0.##} والرصيد المعدّل للدفاتر " +
                $"{reconciliation.AdjustedBookBalance:0.##} (الفرق {reconciliation.Difference:0.##}). راجع رصيد الكشف والمطابقات.");
        }

        reconciliation.Status = BankReconciliationStatus.Completed;
        reconciliation.CompletedAtUtc = _access.UtcNow;
        reconciliation.CompletedByUserId = _access.CurrentUser.UserId;

        // The statement lines were loaded (tracked) by CalculateAsync.
        var matched = reconciliation.Lines.Where(x => x.LineType == BankReconciliationLineType.Matched).ToList();
        var transactions = Db.BankTransactions.Local.ToDictionary(x => x.Id);

        foreach (var line in matched)
        {
            line.IsCleared = true;

            var transaction = transactions[line.BankTransactionId!.Value];
            transaction.IsReconciled = true;
            transaction.BankReconciliation = reconciliation;
            transaction.MatchedJournalEntryLineId = line.JournalEntryLineId;
        }
    }

    private async Task EnsureAfterLastCompletedAsync(long bankAccountId, DateTime statementDate, CancellationToken ct)
    {
        var last = await Db.BankReconciliations.AsNoTracking()
            .Where(x => x.BankAccountId == bankAccountId && x.Status == BankReconciliationStatus.Completed)
            .OrderByDescending(x => x.StatementDate)
            .Select(x => (DateTime?)x.StatementDate)
            .FirstOrDefaultAsync(ct);

        if (last.HasValue && statementDate < last.Value)
        {
            throw AccountingAccess.Invalid("statementDate", $"آخر تسوية مكتملة لهذا الحساب بتاريخ {last.Value:yyyy-MM-dd}؛ تاريخ الكشف يجب ألا يسبقه.");
        }
    }

    private async Task<long> GlAccountIdAsync(BankAccount bank, CancellationToken ct)
    {
        if (bank.AccountId.HasValue)
        {
            return bank.AccountId.Value;
        }

        var resolved = await _resolver.ResolveAsync(new PostingScope(AccountRoles.Bank, bank.BranchId, bank.CurrencyId, null, bank.Id, null), ct);
        return resolved.AccountId;
    }

    private async Task<BankReconciliationDto> LoadDtoAsync(long id, bool includeLines, CancellationToken ct)
    {
        var header = await Db.BankReconciliations.AsNoTracking()
                         .Where(x => x.Id == id)
                         .Select(x => new
                         {
                             Entity = x,
                             BankName = x.BankAccount.ArabicName,
                             CurrencyCode = x.BankAccount.Currency.Code,
                             AccountCode = x.Account.Code,
                             Matched = x.Lines.Count(l => l.LineType == BankReconciliationLineType.Matched),
                             BankOnly = x.Lines.Count(l => l.LineType == BankReconciliationLineType.BankOnly),
                             BookOnly = x.Lines.Count(l => l.LineType == BankReconciliationLineType.BookOnly)
                         })
                         .FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("التسوية البنكية", id);

        var lines = new List<BankReconciliationLineDto>();
        if (includeLines)
        {
            var rows = await Db.BankReconciliationLines.AsNoTracking()
                .Where(x => x.BankReconciliationId == id)
                .OrderBy(x => x.LineType).ThenBy(x => x.TransactionDate).ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.LineType,
                    x.BankTransactionId,
                    x.JournalEntryLineId,
                    JournalEntryId = x.JournalEntryLine != null ? (long?)x.JournalEntryLine.JournalEntryId : null,
                    JournalEntryNumber = x.JournalEntryLine != null ? x.JournalEntryLine.JournalEntry.EntryNumber : null,
                    x.TransactionDate,
                    x.Description,
                    x.Amount,
                    x.IsCleared
                })
                .ToListAsync(ct);

            lines = rows.Select(x => new BankReconciliationLineDto
            {
                Id = x.Id,
                LineType = x.LineType,
                LineTypeName = AccountingNames.Arabic(x.LineType),
                BankTransactionId = x.BankTransactionId,
                JournalEntryLineId = x.JournalEntryLineId,
                JournalEntryId = x.JournalEntryId,
                JournalEntryNumber = x.JournalEntryNumber,
                TransactionDate = x.TransactionDate,
                Description = x.Description,
                Amount = x.Amount,
                IsCleared = x.IsCleared
            }).ToList();
        }

        var r = header.Entity;
        return new BankReconciliationDto
        {
            Id = r.Id,
            ReconciliationNumber = r.ReconciliationNumber,
            BankAccountId = r.BankAccountId,
            BankAccountName = header.BankName,
            CurrencyCode = header.CurrencyCode,
            AccountId = r.AccountId,
            AccountCode = header.AccountCode,
            StatementDate = r.StatementDate,
            StatementEndingBalance = r.StatementEndingBalance,
            BookBalance = r.BookBalance,
            OutstandingDeposits = r.OutstandingDeposits,
            OutstandingWithdrawals = r.OutstandingWithdrawals,
            BankOnlyDeposits = r.BankOnlyDeposits,
            BankOnlyWithdrawals = r.BankOnlyWithdrawals,
            AdjustedBankBalance = r.AdjustedBankBalance,
            AdjustedBookBalance = r.AdjustedBookBalance,
            Difference = r.Difference,
            Status = r.Status,
            StatusName = AccountingNames.Arabic(r.Status),
            CompletedAtUtc = r.CompletedAtUtc,
            CompletedByUserId = r.CompletedByUserId,
            Notes = r.Notes,
            CreatedAtUtc = r.CreatedAtUtc,
            MatchedCount = header.Matched,
            BankOnlyCount = header.BankOnly,
            BookOnlyCount = header.BookOnly,
            Lines = lines
        };
    }

    private void Audit(AuditAction action, long id, object details) =>
        _auditLog.Add(action, entityName: EntityName, entityId: id.ToString(CultureInfo.InvariantCulture), details: details);
}
