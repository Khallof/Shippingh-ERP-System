using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Entities.Cod;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Cod;

/// <summary>
/// COD / driver cash corrections with a reason and the approval of a second user (step 6):
///
///   DriverShortageWriteOff : missing cash written off the driver's balance (loss or employee receivable)
///   DriverExcessCash       : extra cash the driver brought (so it can be handed over)
///   CollectionCorrection   : the collected amount of a COD still with the driver was wrong
///   CollectionReversal     : a collection recorded on the wrong shipment (COD back to pending)
///   SenderFee              : extra fee deducted from the sender (return, storage, damaged goods...)
///   SenderCompensation     : amount added for the sender (damage / delay compensation)
///
/// The request changes nothing. The approval re-checks every rule (the situation may have changed) and applies
/// the change with its sub-ledger rows, history, audit and accounting event in one transaction.
/// </summary>
public sealed class CodAdjustmentService : ICodAdjustmentService
{
    private const string EntityName = "CODAdjustment";

    private static readonly Dictionary<string, Expression<Func<CODAdjustment, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["createdAtUtc"] = x => x.CreatedAtUtc,
        ["amount"] = x => x.Amount
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly CashAccess _access;
    private readonly CashLedger _ledger;
    private readonly DocumentNumberGenerator _numbers;
    private readonly IIntegrationEventPublisher _events;

    public CodAdjustmentService(
        MasterDataServiceDependencies dependencies,
        CashAccess access,
        CashLedger ledger,
        DocumentNumberGenerator numbers,
        IIntegrationEventPublisher events)
    {
        _dependencies = dependencies;
        _access = access;
        _ledger = ledger;
        _numbers = numbers;
        _events = events;
    }

    private AppDbContext Db => _dependencies.DbContext;

    private DateTime UtcNow => _dependencies.DateTimeProvider.UtcNow;

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<CodAdjustmentDto>> GetPagedAsync(CodAdjustmentListQuery query, CancellationToken cancellationToken)
    {
        var rows = _access.Adjustments();

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.AdjustmentType.HasValue)
        {
            var type = query.AdjustmentType.Value;
            rows = rows.Where(x => x.AdjustmentType == type);
        }

        if (query.DriverId.HasValue)
        {
            var driverId = query.DriverId.Value;
            rows = rows.Where(x => x.DriverId == driverId);
        }

        if (query.CodTransactionId.HasValue)
        {
            var codId = query.CodTransactionId.Value;
            rows = rows.Where(x => x.CODTransactionId == codId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId);
        }

        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value;
            rows = rows.Where(x => x.CreatedAtUtc >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = query.ToUtc.Value;
            rows = rows.Where(x => x.CreatedAtUtc < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.AdjustmentNumber.Contains(search) || x.Reason.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<CODAdjustment> ordered = !string.IsNullOrWhiteSpace(query.SortBy) && Sorts.TryGetValue(query.SortBy, out var sort)
            ? (query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort))
            : rows.OrderByDescending(x => x.CreatedAtUtc);

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<CodAdjustmentDto>(items.Select(WithNames).ToList(), query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<CodAdjustmentDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await _access.Adjustments().Where(x => x.Id == id).Select(Projection).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("التسوية", id);
        return WithNames(dto);
    }

    // ------------------------------------------------------------------ request

    public async Task<CodAdjustmentDto> CreateAsync(CreateCodAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var adjustment = new CODAdjustment
        {
            AdjustmentType = request.AdjustmentType,
            Status = CODAdjustmentStatus.PendingApproval,
            Reason = request.Reason.Trim()
        };

        switch (request.AdjustmentType)
        {
            case CODAdjustmentType.DriverShortageWriteOff:
            case CODAdjustmentType.DriverExcessCash:
            {
                var driver = await DriverAsync(request.DriverId!.Value, cancellationToken);
                var currency = await _access.Billing.GetCurrencyAsync(request.CurrencyId, cancellationToken);
                _access.BranchAccess.EnsureCanAccess(driver.BranchId);

                adjustment.DriverId = driver.Id;
                adjustment.BranchId = driver.BranchId;
                adjustment.CurrencyId = currency.Id;
                adjustment.Amount = BillingAccess.Round(request.Amount!.Value, currency.DecimalPlaces);

                if (request.AdjustmentType == CODAdjustmentType.DriverShortageWriteOff)
                {
                    var balance = await DriverBalanceAsync(driver.Id, currency.Id, cancellationToken);
                    if (adjustment.Amount > balance)
                    {
                        throw new BusinessRuleException($"مبلغ الشطب ({adjustment.Amount}) أكبر من رصيد عهدة السائق ({balance}).");
                    }
                }

                break;
            }

            default:
            {
                var scope = await _access.CodsAsync(tracked: false, cancellationToken);
                var cod = await scope.Include(x => x.Shipment).FirstOrDefaultAsync(x => x.Id == request.CodTransactionId!.Value, cancellationToken)
                          ?? throw new NotFoundException("مبلغ التحصيل", request.CodTransactionId!.Value);

                adjustment.CODTransactionId = cod.Id;
                adjustment.CurrencyId = cod.CurrencyId;
                var currency = await _access.Billing.GetCurrencyAsync(cod.CurrencyId, cancellationToken);

                if (request.AdjustmentType is CODAdjustmentType.CollectionCorrection or CODAdjustmentType.CollectionReversal)
                {
                    EnsureWithDriver(cod);
                    var driver = await DriverAsync(cod.DriverId!.Value, cancellationToken);
                    adjustment.DriverId = driver.Id;
                    adjustment.BranchId = driver.BranchId;
                    adjustment.PreviousCollectedAmount = cod.CollectedAmount;

                    if (request.AdjustmentType == CODAdjustmentType.CollectionCorrection)
                    {
                        var newAmount = BillingAccess.Round(request.NewCollectedAmount!.Value, currency.DecimalPlaces);
                        if (newAmount == cod.CollectedAmount)
                        {
                            throw CashAccess.Invalid("newCollectedAmount", "المبلغ الجديد يساوي المبلغ المسجّل؛ لا يوجد ما يُصحَّح.");
                        }

                        adjustment.NewCollectedAmount = newAmount;
                        adjustment.Amount = Math.Abs(newAmount - cod.CollectedAmount);
                    }
                    else
                    {
                        adjustment.NewCollectedAmount = 0m;
                        adjustment.Amount = cod.CollectedAmount;
                    }

                    var pending = await Db.CODAdjustments.AnyAsync(
                        x => x.CODTransactionId == cod.Id &&
                             x.Status == CODAdjustmentStatus.PendingApproval &&
                             (x.AdjustmentType == CODAdjustmentType.CollectionCorrection || x.AdjustmentType == CODAdjustmentType.CollectionReversal),
                        cancellationToken);
                    if (pending)
                    {
                        throw new ConflictException("يوجد طلب تصحيح أو إلغاء تحصيل بانتظار الموافقة لهذه الشحنة.");
                    }
                }
                else
                {
                    await EnsureSenderAdjustableAsync(cod, cancellationToken);
                    adjustment.BranchId = cod.OriginBranchId;
                    adjustment.Amount = BillingAccess.Round(request.Amount!.Value, currency.DecimalPlaces);
                }

                break;
            }
        }

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            adjustment.AdjustmentNumber = await _numbers.NextAsync(DocumentPrefixes.CodAdjustment, ct);
            Db.CODAdjustments.Add(adjustment);
            await Db.SaveChangesAsync(ct);

            Audit(AuditAction.CodAdjustmentRequested, adjustment.Id, new
            {
                adjustment.AdjustmentNumber,
                adjustment.AdjustmentType,
                adjustment.DriverId,
                adjustment.CODTransactionId,
                adjustment.Amount,
                adjustment.Reason
            });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(adjustment.Id, cancellationToken);
    }

    // ------------------------------------------------------------------ approve / reject

    public async Task<CodAdjustmentDto> ApproveAsync(long id, DecideCodAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var adjustment = await FindPendingAsync(id, cancellationToken);
        _access.EnsureOtherApprover(adjustment.CreatedByUserId, "التسوية");

        var now = UtcNow;
        var reference = new CodReference(CashLedger.AdjustmentDocument, adjustment.Id, adjustment.AdjustmentNumber, adjustment.DriverId);

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            switch (adjustment.AdjustmentType)
            {
                case CODAdjustmentType.DriverShortageWriteOff:
                case CODAdjustmentType.DriverExcessCash:
                    await ApplyDriverAdjustmentAsync(adjustment, now, ct);
                    break;

                case CODAdjustmentType.CollectionCorrection:
                case CODAdjustmentType.CollectionReversal:
                    await ApplyCollectionAdjustmentAsync(adjustment, reference, now, ct);
                    break;

                default:
                    await ApplySenderAdjustmentAsync(adjustment, reference, ct);
                    break;
            }

            adjustment.Status = CODAdjustmentStatus.Approved;
            adjustment.DecidedByUserId = _dependencies.CurrentUser.UserId;
            adjustment.DecidedAtUtc = now;
            adjustment.DecisionNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            Audit(AuditAction.CodAdjustmentApproved, adjustment.Id, new { adjustment.AdjustmentNumber, adjustment.AdjustmentType, adjustment.Amount });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<CodAdjustmentDto> RejectAsync(long id, DecideCodAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var adjustment = await FindPendingAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Notes))
        {
            throw CashAccess.Invalid("notes", "اكتب سبب الرفض.");
        }

        _access.EnsureOtherApprover(adjustment.CreatedByUserId, "التسوية");

        adjustment.Status = CODAdjustmentStatus.Rejected;
        adjustment.DecidedByUserId = _dependencies.CurrentUser.UserId;
        adjustment.DecidedAtUtc = UtcNow;
        adjustment.DecisionNotes = request.Notes.Trim();

        Audit(AuditAction.CodAdjustmentRejected, adjustment.Id, new { adjustment.AdjustmentNumber, notes = adjustment.DecisionNotes });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ apply

    private async Task ApplyDriverAdjustmentAsync(CODAdjustment adjustment, DateTime now, CancellationToken cancellationToken)
    {
        var account = await _ledger.DriverAccountAsync(adjustment.DriverId!.Value, adjustment.CurrencyId, cancellationToken);
        var shortage = adjustment.AdjustmentType == CODAdjustmentType.DriverShortageWriteOff;

        if (shortage && adjustment.Amount > account.Balance)
        {
            throw new BusinessRuleException($"رصيد عهدة السائق الآن ({account.Balance}) أقل من مبلغ الشطب ({adjustment.Amount}).");
        }

        _ledger.PostDriver(
            account,
            shortage ? DriverCashMovementType.ShortageWrittenOff : DriverCashMovementType.ExcessRecorded,
            debit: shortage ? 0m : adjustment.Amount,
            credit: shortage ? adjustment.Amount : 0m,
            null,
            CashLedger.AdjustmentDocument,
            adjustment.Id,
            adjustment.AdjustmentNumber,
            $"{CodNames.Arabic(adjustment.AdjustmentType)}: {adjustment.Reason}");

        _events.Publish(new DriverCashAdjustedEvent
        {
            AdjustmentId = adjustment.Id,
            AdjustmentNumber = adjustment.AdjustmentNumber,
            AdjustmentType = adjustment.AdjustmentType.ToString(),
            DriverId = adjustment.DriverId!.Value,
            CurrencyId = adjustment.CurrencyId,
            Amount = adjustment.Amount,
            Reason = adjustment.Reason,
            Posting = CashPostings.DriverCashAdjusted(adjustment.Id, adjustment.AdjustmentNumber, adjustment.AdjustmentType,
                adjustment.CurrencyId, now, adjustment.Amount, adjustment.BranchId, adjustment.DriverId!.Value)
        });
    }

    private async Task ApplyCollectionAdjustmentAsync(CODAdjustment adjustment, CodReference reference, DateTime now, CancellationToken cancellationToken)
    {
        var cod = await Db.CODTransactions.Include(x => x.Shipment).FirstAsync(x => x.Id == adjustment.CODTransactionId!.Value, cancellationToken);
        EnsureWithDriver(cod);

        if (cod.CollectedAmount != adjustment.PreviousCollectedAmount || cod.DriverId != adjustment.DriverId)
        {
            throw new ConflictException("تغيّر المبلغ المحصّل أو السائق بعد تقديم الطلب. ارفض الطلب وقدّم طلباً جديداً.");
        }

        var previous = cod.CollectedAmount;
        var next = adjustment.AdjustmentType == CODAdjustmentType.CollectionReversal ? 0m : adjustment.NewCollectedAmount!.Value;
        var delta = next - previous;

        var account = await _ledger.DriverAccountAsync(cod.DriverId!.Value, cod.CurrencyId, cancellationToken);
        _ledger.PostDriver(
            account,
            adjustment.AdjustmentType == CODAdjustmentType.CollectionReversal ? DriverCashMovementType.CollectionReversed : DriverCashMovementType.CollectionCorrected,
            debit: delta > 0m ? delta : 0m,
            credit: delta < 0m ? -delta : 0m,
            cod.Id,
            CashLedger.AdjustmentDocument,
            adjustment.Id,
            adjustment.AdjustmentNumber,
            $"{CodNames.Arabic(adjustment.AdjustmentType)} للشحنة {cod.Shipment.ShipmentNumber}: {adjustment.Reason}");

        if (adjustment.AdjustmentType == CODAdjustmentType.CollectionReversal)
        {
            var target = cod.Shipment.Status == ShipmentStatus.OutForDelivery && cod.Shipment.AssignedDriverId.HasValue
                ? CODStatus.AssignedToDriver
                : CODStatus.Pending;

            cod.CollectedAmount = 0m;
            cod.CollectedAtUtc = null;
            cod.CollectedByUserId = null;
            cod.CollectionDifferenceReason = null;
            cod.StatusBeforeDispute = null;
            cod.DriverId = target == CODStatus.AssignedToDriver ? cod.Shipment.AssignedDriverId : null;
            _ledger.MoveCod(cod, target, CODTransactionType.CollectionReversed, previous, reference, adjustment.Reason);
        }
        else
        {
            cod.CollectedAmount = next;
            _ledger.AddCodHistory(cod, CODTransactionType.CollectionCorrected, cod.Status, cod.Status, next, reference,
                $"من {previous} إلى {next}: {adjustment.Reason}");
        }

        _events.Publish(new CodCollectionAdjustedEvent
        {
            AdjustmentId = adjustment.Id,
            AdjustmentNumber = adjustment.AdjustmentNumber,
            AdjustmentType = adjustment.AdjustmentType.ToString(),
            CodTransactionId = cod.Id,
            SenderCustomerId = cod.SenderCustomerId,
            DriverId = adjustment.DriverId!.Value,
            CurrencyId = cod.CurrencyId,
            PreviousAmount = previous,
            NewAmount = next,
            Posting = CashPostings.CodCollectionAdjusted(adjustment.Id, adjustment.AdjustmentNumber, cod.CurrencyId, now, delta,
                cod.SenderCustomerId, cod.OriginBranchId, adjustment.DriverId!.Value)
        });
    }

    private async Task ApplySenderAdjustmentAsync(CODAdjustment adjustment, CodReference reference, CancellationToken cancellationToken)
    {
        var cod = await Db.CODTransactions.FirstAsync(x => x.Id == adjustment.CODTransactionId!.Value, cancellationToken);
        await EnsureSenderAdjustableAsync(cod, cancellationToken);

        var fee = adjustment.AdjustmentType == CODAdjustmentType.SenderFee;
        if (fee)
        {
            cod.OtherFeesAmount += adjustment.Amount;
        }
        else
        {
            cod.CompensationAmount += adjustment.Amount;
        }

        // Already reviewed: the net amount follows at once (nothing was paid yet, see EnsureSenderAdjustableAsync).
        if (cod.ReadyAtUtc.HasValue)
        {
            cod.NetPayableAmount = CodSettlementMath.NetPayable(cod.CollectedAmount, CodService.Deductions(cod));
        }

        _ledger.AddCodHistory(cod, fee ? CODTransactionType.SenderFeeAdded : CODTransactionType.SenderCompensationAdded,
            cod.Status, cod.Status, adjustment.Amount, reference with { DriverId = null }, adjustment.Reason);
    }

    // ------------------------------------------------------------------ rules

    private static void EnsureWithDriver(CODTransaction cod)
    {
        if (!CodStatusRules.IsWithDriver(cod.Status, cod.StatusBeforeDispute) || cod.DriverId is null)
        {
            throw new BusinessRuleException(
                $"تصحيح أو إلغاء التحصيل ممكن فقط والمبلغ ما زال لدى السائق (الحالة الآن: «{CodNames.Arabic(cod.Status)}»). " +
                "إذا سُلّم المبلغ للصندوق فألغِ سند الاستلام أولاً.");
        }
    }

    private async Task EnsureSenderAdjustableAsync(CODTransaction cod, CancellationToken cancellationToken)
    {
        var effective = cod.Status == CODStatus.Disputed ? cod.StatusBeforeDispute : cod.Status;
        if (effective is not (CODStatus.ReceivedAtBranchCashbox or CODStatus.ReadyForSenderSettlement) || cod.DeductionsRecognized)
        {
            throw new BusinessRuleException(
                $"الرسوم والتعويضات تُضاف بعد استلام المبلغ في الصندوق وقبل أول دفعة للمرسل (الحالة الآن: «{CodNames.Arabic(cod.Status)}»).");
        }

        if (await Db.SenderSettlementLines.AnyAsync(x => x.CODTransactionId == cod.Id && x.IsOpen, cancellationToken))
        {
            throw new BusinessRuleException("المبلغ محجوز في تسوية مفتوحة. ألغِ التسوية أولاً.");
        }
    }

    private async Task<(long Id, long BranchId)> DriverAsync(long driverId, CancellationToken cancellationToken)
    {
        var driver = await Db.DriverProfiles.AsNoTracking()
                         .Where(x => x.Id == driverId)
                         .Select(x => new { x.Id, x.Employee.BranchId })
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? throw CashAccess.Invalid("driverId", "السائق غير موجود.");
        return (driver.Id, driver.BranchId);
    }

    private async Task<decimal> DriverBalanceAsync(long driverId, long currencyId, CancellationToken cancellationToken) =>
        await Db.DriverCashAccounts.AsNoTracking()
            .Where(x => x.DriverId == driverId && x.CurrencyId == currencyId)
            .Select(x => (decimal?)x.Balance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

    private async Task<CODAdjustment> FindPendingAsync(long id, CancellationToken cancellationToken)
    {
        var adjustment = await _access.Adjustments(tracked: true).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                         ?? throw new NotFoundException("التسوية", id);

        if (adjustment.Status != CODAdjustmentStatus.PendingApproval)
        {
            throw new BusinessRuleException($"التسوية {adjustment.AdjustmentNumber} في حالة «{CodNames.Arabic(adjustment.Status)}».");
        }

        return adjustment;
    }

    private void Audit(AuditAction action, long id, object details) =>
        _dependencies.AuditLog.Add(action, entityName: EntityName, entityId: id.ToString(CultureInfo.InvariantCulture), details: details);

    private static readonly Expression<Func<CODAdjustment, CodAdjustmentDto>> Projection = x => new CodAdjustmentDto
    {
        Id = x.Id,
        AdjustmentNumber = x.AdjustmentNumber,
        AdjustmentType = x.AdjustmentType,
        Status = x.Status,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        DriverId = x.DriverId,
        DriverName = x.Driver != null ? x.Driver.Employee.ArabicName : null,
        CodTransactionId = x.CODTransactionId,
        ShipmentNumber = x.CODTransaction != null ? x.CODTransaction.Shipment.ShipmentNumber : null,
        Amount = x.Amount,
        PreviousCollectedAmount = x.PreviousCollectedAmount,
        NewCollectedAmount = x.NewCollectedAmount,
        Reason = x.Reason,
        RequestedByUserId = x.CreatedByUserId,
        CreatedAtUtc = x.CreatedAtUtc,
        DecidedByUserId = x.DecidedByUserId,
        DecidedAtUtc = x.DecidedAtUtc,
        DecisionNotes = x.DecisionNotes
    };

    private static CodAdjustmentDto WithNames(CodAdjustmentDto x) => new()
    {
        Id = x.Id,
        AdjustmentNumber = x.AdjustmentNumber,
        AdjustmentType = x.AdjustmentType,
        AdjustmentTypeName = CodNames.Arabic(x.AdjustmentType),
        Status = x.Status,
        StatusName = CodNames.Arabic(x.Status),
        BranchId = x.BranchId,
        BranchName = x.BranchName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.CurrencyCode,
        DriverId = x.DriverId,
        DriverName = x.DriverName,
        CodTransactionId = x.CodTransactionId,
        ShipmentNumber = x.ShipmentNumber,
        Amount = x.Amount,
        PreviousCollectedAmount = x.PreviousCollectedAmount,
        NewCollectedAmount = x.NewCollectedAmount,
        Reason = x.Reason,
        RequestedByUserId = x.RequestedByUserId,
        CreatedAtUtc = x.CreatedAtUtc,
        DecidedByUserId = x.DecidedByUserId,
        DecidedAtUtc = x.DecidedAtUtc,
        DecisionNotes = x.DecisionNotes
    };
}
