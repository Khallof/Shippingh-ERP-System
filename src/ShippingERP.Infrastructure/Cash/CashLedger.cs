using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Entities.Cod;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Cash;

/// <summary>
/// Writes the three operational sub-ledgers of step 6 - cashbox transactions, driver cash movements and COD
/// history - and keeps the stored balances in step. Like CustomerLedger it only ADDS rows / changes tracked
/// entities: the caller saves everything in ONE transaction together with its document, audit log and event.
///
/// Safety rules enforced here, for every caller:
///   - a cashbox balance never goes below zero,
///   - an inactive cashbox or a closed business day accepts no movement,
///   - a COD status only changes along CodStatusRules,
///   - balances are guarded by RowVersion (Cashbox, DriverCashAccount): a parallel change of the same balance
///     makes SaveChanges fail with 409 instead of losing an update.
/// </summary>
public sealed class CashLedger
{
    public const string CashboxDocument = "Cashbox";
    public const string ReceiptDocument = "CashReceiptVoucher";
    public const string PaymentDocument = "CashPaymentVoucher";
    public const string TransferDocument = "CashTransfer";
    public const string SettlementDocument = "SenderSettlement";
    public const string ClosingDocument = "CashboxDailyClosing";
    public const string AdjustmentDocument = "CODAdjustment";
    public const string CodDocument = "CODTransaction";

    private readonly AppDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly BusinessCalendar _calendar;

    public CashLedger(AppDbContext dbContext, IDateTimeProvider dateTimeProvider, BusinessCalendar calendar)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _calendar = calendar;
    }

    public BusinessCalendar Calendar => _calendar;

    // ------------------------------------------------------------------ cashboxes

    /// <summary>
    /// Adds a cashbox movement and updates the balance. <paramref name="allowClosedDay"/> is only used by the
    /// approval of a closing difference (the movement belongs to the closing itself).
    /// </summary>
    public async Task<CashboxTransaction> PostCashboxAsync(
        Cashbox cashbox,
        CashboxTransactionType type,
        decimal amountIn,
        decimal amountOut,
        string documentType,
        long documentId,
        string documentNumber,
        string description,
        string? counterpartyName,
        CancellationToken cancellationToken,
        bool allowClosedDay = false,
        long? reversedTransactionId = null)
    {
        if (amountIn < 0m || amountOut < 0m || (amountIn > 0m && amountOut > 0m) || amountIn + amountOut == 0m)
        {
            throw new InvalidOperationException("A cashbox transaction is either an amount in or an amount out, greater than zero.");
        }

        if (!cashbox.IsActive && type != CashboxTransactionType.Reversal)
        {
            throw new BusinessRuleException($"الصندوق {cashbox.Code} غير مفعّل.");
        }

        var now = _dateTimeProvider.UtcNow;
        var businessDate = _calendar.BusinessDate(now);

        if (!allowClosedDay)
        {
            await EnsureDayOpenAsync(cashbox, businessDate, cancellationToken);
        }

        var newBalance = cashbox.CurrentBalance + amountIn - amountOut;
        if (newBalance < 0m)
        {
            throw new BusinessRuleException(
                $"رصيد الصندوق {cashbox.Code} غير كافٍ: الرصيد الحالي {cashbox.CurrentBalance} والمطلوب صرفه {amountOut}.");
        }

        cashbox.CurrentBalance = newBalance;
        cashbox.LastTransactionAtUtc = now;

        var transaction = new CashboxTransaction
        {
            Cashbox = cashbox,
            BranchId = cashbox.BranchId,
            CurrencyId = cashbox.CurrencyId,
            TransactionType = type,
            AmountIn = amountIn,
            AmountOut = amountOut,
            BalanceAfter = newBalance,
            TransactionDateUtc = now,
            BusinessDate = businessDate,
            DocumentType = documentType,
            DocumentId = documentId,
            DocumentNumber = documentNumber,
            Description = Clip(description, 300),
            CounterpartyName = string.IsNullOrWhiteSpace(counterpartyName) ? null : Clip(counterpartyName.Trim(), 150),
            ReversedTransactionId = reversedTransactionId
        };

        _dbContext.CashboxTransactions.Add(transaction);
        return transaction;
    }

    /// <summary>Reverses every transaction of a document on this cashbox (cancelled voucher, reversed settlement...).</summary>
    public async Task<IReadOnlyList<CashboxTransaction>> ReverseDocumentAsync(
        Cashbox cashbox,
        string documentType,
        long documentId,
        string description,
        CancellationToken cancellationToken)
    {
        var originals = await _dbContext.CashboxTransactions.AsNoTracking()
            .Where(x => x.CashboxId == cashbox.Id &&
                        x.DocumentType == documentType &&
                        x.DocumentId == documentId &&
                        x.TransactionType != CashboxTransactionType.Reversal)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var reversedIds = await _dbContext.CashboxTransactions.AsNoTracking()
            .Where(x => x.CashboxId == cashbox.Id && x.ReversedTransactionId != null &&
                        x.DocumentType == documentType && x.DocumentId == documentId)
            .Select(x => x.ReversedTransactionId!.Value)
            .ToListAsync(cancellationToken);

        var result = new List<CashboxTransaction>();
        foreach (var original in originals.Where(x => !reversedIds.Contains(x.Id)))
        {
            result.Add(await PostCashboxAsync(
                cashbox,
                CashboxTransactionType.Reversal,
                amountIn: original.AmountOut,
                amountOut: original.AmountIn,
                documentType,
                documentId,
                original.DocumentNumber,
                description,
                original.CounterpartyName,
                cancellationToken,
                reversedTransactionId: original.Id));
        }

        return result;
    }

    /// <summary>422 when the business day of the cashbox is already closed (or its closing waits for approval).</summary>
    public async Task EnsureDayOpenAsync(Cashbox cashbox, DateTime businessDate, CancellationToken cancellationToken)
    {
        var closed = await _dbContext.CashboxDailyClosings.AsNoTracking()
            .AnyAsync(x => x.CashboxId == cashbox.Id && x.BusinessDate == businessDate && x.Status != CashboxClosingStatus.Rejected, cancellationToken);

        if (closed)
        {
            throw new BusinessRuleException(
                $"تم إقفال يوم {businessDate:yyyy-MM-dd} للصندوق {cashbox.Code}؛ لا يمكن تسجيل حركات جديدة عليه اليوم. " +
                "استخدم صندوقاً آخر، أو اطلب رفض الإقفال لإعادة فتح اليوم.");
        }
    }

    // ------------------------------------------------------------------ drivers

    /// <summary>The driver's cash account in the currency (tracked; created when missing).</summary>
    public async Task<DriverCashAccount> DriverAccountAsync(long driverId, long currencyId, CancellationToken cancellationToken)
    {
        var account = _dbContext.DriverCashAccounts.Local.FirstOrDefault(x => x.DriverId == driverId && x.CurrencyId == currencyId)
                      ?? await _dbContext.DriverCashAccounts.FirstOrDefaultAsync(x => x.DriverId == driverId && x.CurrencyId == currencyId, cancellationToken);

        if (account is null)
        {
            account = new DriverCashAccount { DriverId = driverId, CurrencyId = currencyId, Balance = 0m, IsActive = true };
            _dbContext.DriverCashAccounts.Add(account);
        }

        return account;
    }

    /// <summary>Adds a driver cash movement and updates the account balance.</summary>
    public DriverCashMovement PostDriver(
        DriverCashAccount account,
        DriverCashMovementType type,
        decimal debit,
        decimal credit,
        long? codTransactionId,
        string documentType,
        long documentId,
        string documentNumber,
        string description)
    {
        if (debit < 0m || credit < 0m || (debit > 0m && credit > 0m) || debit + credit == 0m)
        {
            throw new InvalidOperationException("A driver cash movement is either a debit or a credit, greater than zero.");
        }

        var now = _dateTimeProvider.UtcNow;
        account.Balance += debit - credit;
        account.LastMovementAtUtc = now;
        if (type == DriverCashMovementType.Handover)
        {
            account.LastHandoverAtUtc = now;
        }

        var movement = new DriverCashMovement
        {
            DriverCashAccount = account,
            DriverId = account.DriverId,
            CurrencyId = account.CurrencyId,
            MovementType = type,
            Debit = debit,
            Credit = credit,
            BalanceAfter = account.Balance,
            MovementDateUtc = now,
            CODTransactionId = codTransactionId,
            DocumentType = documentType,
            DocumentId = documentId,
            DocumentNumber = documentNumber,
            Description = Clip(description, 300)
        };

        _dbContext.DriverCashMovements.Add(movement);
        return movement;
    }

    // ------------------------------------------------------------------ COD

    /// <summary>Changes the COD status (checked against CodStatusRules) and adds the history row.</summary>
    public void MoveCod(CODTransaction cod, CODStatus to, CODTransactionType type, decimal amount, CodReference reference, string? notes = null)
    {
        if (!CodStatusRules.CanTransition(cod.Status, to))
        {
            throw new BusinessRuleException(
                $"لا يمكن نقل مبلغ التحصيل للشحنة من «{CodNames.Arabic(cod.Status)}» إلى «{CodNames.Arabic(to)}».");
        }

        var from = cod.Status;
        cod.Status = to;
        AddCodHistory(cod, type, from, to, amount, reference, notes);
    }

    /// <summary>Adds a COD history row without a status change (fees, amount corrections...).</summary>
    public void AddCodHistory(CODTransaction cod, CODTransactionType type, CODStatus? from, CODStatus to, decimal amount, CodReference reference, string? notes = null)
    {
        _dbContext.CODTransactionHistory.Add(new CODTransactionHistory
        {
            CODTransaction = cod,
            TransactionType = type,
            FromStatus = from,
            ToStatus = to,
            Amount = amount,
            OccurredAtUtc = _dateTimeProvider.UtcNow,
            DriverId = reference.DriverId,
            CashboxId = reference.CashboxId,
            ReferenceType = reference.DocumentType,
            ReferenceId = reference.DocumentId,
            ReferenceNumber = reference.DocumentNumber,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : Clip(notes.Trim(), 500)
        });
    }

    private static string Clip(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}

/// <summary>The document behind a COD history row.</summary>
public sealed record CodReference(string? DocumentType, long? DocumentId, string? DocumentNumber, long? DriverId = null, long? CashboxId = null)
{
    public static CodReference None { get; } = new(null, null, null);
}
