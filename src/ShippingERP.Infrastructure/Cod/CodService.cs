using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Entities.Cod;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Cod;

/// <summary>
/// The COD workflow (step 6): collection, driver handover, review (ready for settlement) and disputes.
///
/// Money rule: the COD amount is the SENDER's money. Collecting it creates a debt of the driver (driver cash
/// account) and a liability to the sender (posting CodPayableToSenders); the handover moves the cash into a
/// cashbox; only the settlement turns the deductions into company revenue.
/// Every operation is one database transaction: COD change + history + sub-ledgers + voucher + audit + outbox event.
/// </summary>
public sealed class CodService : ICodService
{
    private const string EntityName = "CODTransaction";
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<string, Expression<Func<CODTransaction, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["createdAtUtc"] = x => x.CreatedAtUtc,
        ["collectedAtUtc"] = x => x.CollectedAtUtc,
        ["expectedAmount"] = x => x.ExpectedAmount,
        ["collectedAmount"] = x => x.CollectedAmount,
        ["netPayableAmount"] = x => x.NetPayableAmount,
        ["status"] = x => x.Status
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly CashAccess _access;
    private readonly CashLedger _ledger;
    private readonly ShipmentAccessService _shipments;
    private readonly DocumentNumberGenerator _numbers;
    private readonly IIntegrationEventPublisher _events;
    private readonly ICashVoucherService _vouchers;

    public CodService(
        MasterDataServiceDependencies dependencies,
        CashAccess access,
        CashLedger ledger,
        ShipmentAccessService shipments,
        DocumentNumberGenerator numbers,
        IIntegrationEventPublisher events,
        ICashVoucherService vouchers)
    {
        _dependencies = dependencies;
        _access = access;
        _ledger = ledger;
        _shipments = shipments;
        _numbers = numbers;
        _events = events;
        _vouchers = vouchers;
    }

    private AppDbContext Db => _dependencies.DbContext;

    private DateTime UtcNow => _dependencies.DateTimeProvider.UtcNow;

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<CodTransactionListItemDto>> GetPagedAsync(CodTransactionListQuery query, CancellationToken cancellationToken)
    {
        var rows = await _access.CodsAsync(tracked: false, cancellationToken);

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.SenderCustomerId.HasValue)
        {
            var senderId = query.SenderCustomerId.Value;
            rows = rows.Where(x => x.SenderCustomerId == senderId);
        }

        if (query.DriverId.HasValue)
        {
            var driverId = query.DriverId.Value;
            rows = rows.Where(x => x.DriverId == driverId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.OriginBranchId == branchId);
        }

        if (query.CurrencyId.HasValue)
        {
            var currencyId = query.CurrencyId.Value;
            rows = rows.Where(x => x.CurrencyId == currencyId);
        }

        if (query.CashboxId.HasValue)
        {
            var cashboxId = query.CashboxId.Value;
            rows = rows.Where(x => x.ReceivedCashboxId == cashboxId);
        }

        if (query.CollectedFromUtc.HasValue)
        {
            var from = query.CollectedFromUtc.Value;
            rows = rows.Where(x => x.CollectedAtUtc >= from);
        }

        if (query.CollectedToUtc.HasValue)
        {
            var to = query.CollectedToUtc.Value;
            rows = rows.Where(x => x.CollectedAtUtc < to);
        }

        if (query.HasRemainingPayable.HasValue)
        {
            rows = query.HasRemainingPayable.Value
                ? rows.Where(x => (x.Status == CODStatus.ReadyForSenderSettlement || x.Status == CODStatus.PartiallySettled) && x.NetPayableAmount != x.SettledAmount)
                : rows.Where(x => !((x.Status == CODStatus.ReadyForSenderSettlement || x.Status == CODStatus.PartiallySettled) && x.NetPayableAmount != x.SettledAmount));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                x.Shipment.ShipmentNumber.Contains(search) ||
                x.Shipment.TrackingNumber.Contains(search) ||
                x.SenderCustomer.Code.Contains(search) ||
                x.SenderCustomer.ArabicName.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<CODTransaction> ordered = !string.IsNullOrWhiteSpace(query.SortBy) && Sorts.TryGetValue(query.SortBy, out var sort)
            ? (query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort))
            : rows.OrderByDescending(x => x.CreatedAtUtc);

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(CodProjections.ListItem)
            .ToListAsync(cancellationToken);

        return new PagedResult<CodTransactionListItemDto>(items.Select(CodProjections.WithNames).ToList(), query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<CodTransactionDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var rows = await _access.CodsAsync(tracked: false, cancellationToken);
        var dto = await rows.Where(x => x.Id == id).Select(CodProjections.Detail).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("مبلغ التحصيل", id);
        return CodProjections.WithNames(dto);
    }

    public async Task<CodTransactionDto> GetByShipmentAsync(long shipmentId, CancellationToken cancellationToken)
    {
        await _shipments.EnsureVisibleAsync(shipmentId, cancellationToken);

        var id = await Db.CODTransactions.AsNoTracking()
                     .Where(x => x.ShipmentId == shipmentId)
                     .Select(x => (long?)x.Id)
                     .FirstOrDefaultAsync(cancellationToken)
                 ?? throw new NotFoundException("هذه الشحنة ليست شحنة دفع عند الاستلام.");

        var dto = await Db.CODTransactions.AsNoTracking().Where(x => x.Id == id).Select(CodProjections.Detail).FirstAsync(cancellationToken);
        return CodProjections.WithNames(dto);
    }

    // ------------------------------------------------------------------ collect

    public async Task<CodTransactionDto> CollectAsync(CollectCodRequest request, CancellationToken cancellationToken)
    {
        var visible = await _shipments.ScopeAsync(Db.Shipments.AsNoTracking(), cancellationToken);
        if (request.ShipmentId.HasValue)
        {
            var shipmentId = request.ShipmentId.Value;
            visible = visible.Where(x => x.Id == shipmentId);
        }
        else
        {
            var trackingNumber = request.TrackingNumber!.Trim();
            visible = visible.Where(x => x.TrackingNumber == trackingNumber);
        }

        var shipment = await visible
                           .Select(x => new
                           {
                               x.Id,
                               x.ShipmentNumber,
                               x.IsCOD,
                               x.CODAmount,
                               x.Status,
                               x.CurrentBranchId,
                               x.OriginBranchId,
                               x.SenderCustomerId,
                               x.RecipientCustomerId,
                               x.CurrencyId,
                               x.AssignedDriverId,
                               x.DeliveredAtUtc,
                               x.CreatedAtUtc,
                               RecipientName = x.RecipientCustomer.ArabicName
                           })
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw new NotFoundException("الشحنة غير موجودة.");

        if (!shipment.IsCOD || shipment.CODAmount <= 0m)
        {
            throw new BusinessRuleException($"الشحنة {shipment.ShipmentNumber} ليست شحنة دفع عند الاستلام.");
        }

        if (shipment.Status != ShipmentStatus.Delivered)
        {
            throw new BusinessRuleException(
                $"لا يمكن تسجيل التحصيل قبل تأكيد تسليم الشحنة {shipment.ShipmentNumber}. سجّل إثبات التسليم أولاً.");
        }

        var cod = await Db.CODTransactions.FirstOrDefaultAsync(x => x.ShipmentId == shipment.Id, cancellationToken);
        if (cod is null)
        {
            // A COD shipment from before step 6 that the startup backfill did not reach yet.
            cod = new CODTransaction
            {
                ShipmentId = shipment.Id,
                SenderCustomerId = shipment.SenderCustomerId,
                OriginBranchId = shipment.OriginBranchId,
                CurrencyId = shipment.CurrencyId,
                ExpectedAmount = shipment.CODAmount,
                Status = CODStatus.Pending
            };
            Db.CODTransactions.Add(cod);
            _ledger.AddCodHistory(cod, CODTransactionType.Registered, null, CODStatus.Pending, shipment.CODAmount,
                new CodReference("Shipment", shipment.Id, shipment.ShipmentNumber));
        }

        if (cod.Status == CODStatus.Cancelled)
        {
            throw new BusinessRuleException($"مبلغ التحصيل للشحنة {shipment.ShipmentNumber} ملغى.");
        }

        if (!CodStatusRules.IsNotCollected(cod.Status))
        {
            throw new ConflictException(
                $"تم تسجيل تحصيل الشحنة {shipment.ShipmentNumber} من قبل (الحالة: {CodNames.Arabic(cod.Status)}). " +
                "لتصحيح المبلغ اطلب تسوية «تصحيح مبلغ محصّل».");
        }

        var currency = await _access.Billing.GetCurrencyAsync(cod.CurrencyId, cancellationToken);
        var amount = BillingAccess.Round(request.Amount, currency.DecimalPlaces);
        var hasDifference = amount != cod.ExpectedAmount;
        if (hasDifference && string.IsNullOrWhiteSpace(request.DifferenceReason))
        {
            throw CashAccess.Invalid("differenceReason",
                $"المبلغ المحصّل ({amount}) يختلف عن مبلغ التحصيل في الشحنة ({cod.ExpectedAmount}). اكتب سبب الفرق.");
        }

        var now = UtcNow;
        var collectedAt = request.CollectedAtUtc ?? now;
        var earliest = (shipment.DeliveredAtUtc ?? shipment.CreatedAtUtc) - ClockTolerance;
        if (collectedAt > now + ClockTolerance || collectedAt < earliest)
        {
            throw CashAccess.Invalid("collectedAtUtc", "وقت التحصيل يجب أن يكون بين وقت التسليم والآن.");
        }

        if (request.CashboxId.HasValue)
        {
            if (_access.IsDriverOnly)
            {
                throw new ForbiddenException("السائق يسجّل التحصيل في عهدته فقط؛ الاستلام في الصندوق يتم في الفرع.");
            }

            _access.EnsurePermission(Permissions.Cod.Handover, "التحصيل في الفرع مباشرة إلى الصندوق يحتاج صلاحية COD.Handover.");
            var cashbox = await _access.CashboxForPostingAsync(request.CashboxId.Value, cod.CurrencyId, "cashboxId", cancellationToken);

            await CollectAtCounterAsync(cod, shipment.ShipmentNumber, shipment.RecipientCustomerId, shipment.RecipientName, cashbox, amount, collectedAt,
                hasDifference, request, cancellationToken);
        }
        else
        {
            var driverId = await ResolveCollectingDriverAsync(cod, shipment.Id, shipment.AssignedDriverId, shipment.CurrentBranchId, request.DriverId, cancellationToken);
            await CollectByDriverAsync(cod, shipment.ShipmentNumber, driverId, amount, collectedAt, hasDifference, request, cancellationToken);
        }

        return await GetByIdAsync(cod.Id, cancellationToken);
    }

    private async Task<long> ResolveCollectingDriverAsync(
        CODTransaction cod,
        long shipmentId,
        long? assignedDriverId,
        long currentBranchId,
        long? requestedDriverId,
        CancellationToken cancellationToken)
    {
        if (_access.IsDriverOnly)
        {
            var myDriverId = await _access.CurrentDriverIdAsync(cancellationToken)
                             ?? throw new ForbiddenException("حسابك غير مرتبط بملف سائق فعّال.");

            if (requestedDriverId.HasValue && requestedDriverId.Value != myDriverId)
            {
                throw new ForbiddenException("السائق يسجّل التحصيل باسمه فقط.");
            }

            return myDriverId;
        }

        _access.BranchAccess.EnsureCanAccess(currentBranchId);

        var proofDriverId = await Db.ProofsOfDelivery.AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId)
            .OrderByDescending(x => x.Id)
            .Select(x => x.DriverId)
            .FirstOrDefaultAsync(cancellationToken);

        var driverId = requestedDriverId ?? proofDriverId ?? cod.DriverId ?? assignedDriverId
                       ?? throw CashAccess.Invalid("driverId",
                           "لا يوجد سائق لهذه الشحنة (سُلّمت في الفرع). حدد السائق، أو حدد الصندوق لتسجيل التحصيل في الفرع.");

        var exists = await Db.DriverProfiles.AsNoTracking().AnyAsync(x => x.Id == driverId && x.IsActive, cancellationToken);
        if (!exists)
        {
            throw CashAccess.Invalid("driverId", "السائق غير موجود أو غير مفعّل.");
        }

        return driverId;
    }

    private async Task CollectByDriverAsync(
        CODTransaction cod,
        string shipmentNumber,
        long driverId,
        decimal amount,
        DateTime collectedAt,
        bool hasDifference,
        CollectCodRequest request,
        CancellationToken cancellationToken)
    {
        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var account = await _ledger.DriverAccountAsync(driverId, cod.CurrencyId, ct);

            cod.CollectedAmount = amount;
            cod.CollectedAtUtc = collectedAt;
            cod.DriverId = driverId;
            cod.CollectedByUserId = _dependencies.CurrentUser.UserId;
            cod.CollectionDifferenceReason = hasDifference ? request.DifferenceReason!.Trim() : null;

            var reference = new CodReference(CashLedger.CodDocument, cod.Id == 0 ? null : cod.Id, shipmentNumber, DriverId: driverId);
            _ledger.MoveCod(cod, CODStatus.CollectedByDriver, CODTransactionType.Collected, amount, reference, request.Notes);
            await Db.SaveChangesAsync(ct);

            _ledger.PostDriver(account, DriverCashMovementType.CodCollected, debit: amount, credit: 0m, cod.Id,
                CashLedger.CodDocument, cod.Id, shipmentNumber, $"تحصيل الشحنة {shipmentNumber}");

            if (hasDifference)
            {
                OpenDispute(cod, $"فرق في المبلغ المحصّل: {request.DifferenceReason!.Trim()}", reference);
            }

            _events.Publish(new CodCollectedEvent
            {
                CodTransactionId = cod.Id,
                ShipmentId = cod.ShipmentId,
                ShipmentNumber = shipmentNumber,
                SenderCustomerId = cod.SenderCustomerId,
                DriverId = driverId,
                CurrencyId = cod.CurrencyId,
                ExpectedAmount = cod.ExpectedAmount,
                CollectedAmount = amount,
                CollectedAtUtc = collectedAt,
                Posting = CashPostings.CodCollected(cod.Id, shipmentNumber, cod.CurrencyId, collectedAt, amount,
                    cod.SenderCustomerId, cod.OriginBranchId, driverId, null)
            });
            Audit(AuditAction.CodCollected, cod.Id, new { shipmentNumber, driverId, amount, cod.ExpectedAmount, hasDifference });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    private async Task CollectAtCounterAsync(
        CODTransaction cod,
        string shipmentNumber,
        long recipientCustomerId,
        string recipientName,
        Cashbox cashbox,
        decimal amount,
        DateTime collectedAt,
        bool hasDifference,
        CollectCodRequest request,
        CancellationToken cancellationToken)
    {
        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var voucher = new CashReceiptVoucher
            {
                VoucherNumber = await _numbers.NextAsync(DocumentPrefixes.CashReceipt, ct),
                CashboxId = cashbox.Id,
                BranchId = cashbox.BranchId,
                CurrencyId = cashbox.CurrencyId,
                ReceiptType = CashReceiptType.CounterCodCollection,
                Status = CashVoucherStatus.Posted,
                VoucherDateUtc = collectedAt,
                Amount = amount,
                ReceivedFrom = Clip(recipientName, 150),
                Description = $"تحصيل الدفع عند الاستلام للشحنة {shipmentNumber} في الفرع",
                CustomerId = recipientCustomerId,
                ExpectedAmount = cod.ExpectedAmount
            };
            Db.CashReceiptVouchers.Add(voucher);
            await Db.SaveChangesAsync(ct);

            await _ledger.PostCashboxAsync(cashbox, CashboxTransactionType.CounterCodCollection, amount, 0m,
                CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, voucher.Description, voucher.ReceivedFrom, ct);

            cod.CollectedAmount = amount;
            cod.CollectedAtUtc = collectedAt;
            cod.DriverId = null;
            cod.CollectedByUserId = _dependencies.CurrentUser.UserId;
            cod.CollectionDifferenceReason = hasDifference ? request.DifferenceReason!.Trim() : null;
            cod.ReceivedCashboxId = cashbox.Id;
            cod.ReceiptVoucherId = voucher.Id;
            cod.ReceivedAtUtc = UtcNow;

            var reference = new CodReference(CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, CashboxId: cashbox.Id);
            _ledger.MoveCod(cod, CODStatus.ReceivedAtBranchCashbox, CODTransactionType.CollectedAtCounter, amount, reference, request.Notes);

            if (hasDifference)
            {
                OpenDispute(cod, $"فرق في المبلغ المحصّل: {request.DifferenceReason!.Trim()}", reference);
            }

            await Db.SaveChangesAsync(ct);

            _events.Publish(new CodCollectedEvent
            {
                CodTransactionId = cod.Id,
                ShipmentId = cod.ShipmentId,
                ShipmentNumber = shipmentNumber,
                SenderCustomerId = cod.SenderCustomerId,
                CashboxId = cashbox.Id,
                ReceiptVoucherId = voucher.Id,
                CurrencyId = cod.CurrencyId,
                ExpectedAmount = cod.ExpectedAmount,
                CollectedAmount = amount,
                CollectedAtUtc = collectedAt,
                Posting = CashPostings.CodCollected(cod.Id, shipmentNumber, cod.CurrencyId, collectedAt, amount,
                    cod.SenderCustomerId, cashbox.BranchId, null, cashbox.Id)
            });
            Audit(AuditAction.CodCollected, cod.Id, new { shipmentNumber, cashbox = cashbox.Code, voucher.VoucherNumber, amount, cod.ExpectedAmount, hasDifference });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    // ------------------------------------------------------------------ handover

    public async Task<CashVoucherCreationResult<CashReceiptVoucherDto>> HandoverAsync(
        DriverHandoverRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var key = Idempotency.NormalizeKey(idempotencyKey);
        var hash = Idempotency.Hash(request);
        if (key is not null)
        {
            var replay = await FindReplayAsync(key, hash, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        var driver = await Db.DriverProfiles.AsNoTracking()
                         .Where(x => x.Id == request.DriverId)
                         .Select(x => new { x.Id, x.IsActive, Name = x.Employee.ArabicName })
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? throw CashAccess.Invalid("driverId", "السائق غير موجود.");

        var cashbox = await _access.CashboxForPostingAsync(request.CashboxId, null, "cashboxId", cancellationToken);
        var decimals = cashbox.Currency.DecimalPlaces;
        var amount = BillingAccess.Round(request.Amount, decimals);

        var account = await Db.DriverCashAccounts.FirstOrDefaultAsync(x => x.DriverId == driver.Id && x.CurrencyId == cashbox.CurrencyId, cancellationToken);
        var balance = account?.Balance ?? 0m;
        if (amount > balance)
        {
            throw new BusinessRuleException(
                $"المبلغ المسلّم ({amount}) أكبر من رصيد عهدة السائق {driver.Name} ({balance} {cashbox.Currency.Code}). " +
                "إذا كانت الزيادة صحيحة فسجّل تسوية «زيادة نقدية لدى السائق» واعتمدها أولاً.");
        }

        // The COD shipments handed over (default: everything the driver collected in this currency).
        List<CODTransaction> cods;
        if (request.CodTransactionIds.Count > 0)
        {
            cods = await Db.CODTransactions.Include(x => x.Shipment)
                .Where(x => request.CodTransactionIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            var missing = request.CodTransactionIds.Except(cods.Select(x => x.Id)).ToList();
            if (missing.Count > 0)
            {
                throw new NotFoundException($"شحنات التحصيل غير موجودة: {string.Join(", ", missing)}.");
            }

            var problems = cods
                .Where(x => x.Status != CODStatus.CollectedByDriver || x.DriverId != driver.Id || x.CurrencyId != cashbox.CurrencyId)
                .Select(x => x.Status != CODStatus.CollectedByDriver
                    ? $"{x.Shipment.ShipmentNumber}: الحالة «{CodNames.Arabic(x.Status)}»"
                    : x.DriverId != driver.Id ? $"{x.Shipment.ShipmentNumber}: محصّلة بواسطة سائق آخر" : $"{x.Shipment.ShipmentNumber}: بعملة أخرى")
                .ToList();

            if (problems.Count > 0)
            {
                throw new BusinessRuleException("لا يمكن تسليم بعض الشحنات: " + string.Join("؛ ", problems) + ".");
            }
        }
        else
        {
            cods = await Db.CODTransactions.Include(x => x.Shipment)
                .Where(x => x.DriverId == driver.Id && x.Status == CODStatus.CollectedByDriver && x.CurrencyId == cashbox.CurrencyId)
                .OrderBy(x => x.CollectedAtUtc)
                .ToListAsync(cancellationToken);
        }

        var expected = cods.Sum(x => x.CollectedAmount);
        var shortage = Math.Max(expected - amount, 0m);
        var excess = Math.Max(amount - expected, 0m);

        var voucher = new CashReceiptVoucher
        {
            CashboxId = cashbox.Id,
            BranchId = cashbox.BranchId,
            CurrencyId = cashbox.CurrencyId,
            ReceiptType = CashReceiptType.DriverHandover,
            Status = CashVoucherStatus.Posted,
            VoucherDateUtc = UtcNow,
            Amount = amount,
            ReceivedFrom = Clip(driver.Name, 150),
            Description = cods.Count == 0
                ? $"استلام نقدية من السائق {driver.Name} (تسديد رصيد سابق)"
                : $"استلام عهدة السائق {driver.Name}: {cods.Count} شحنة دفع عند الاستلام",
            ReferenceNumber = null,
            DriverId = driver.Id,
            ExpectedAmount = expected,
            ShortageAmount = shortage,
            ExcessAmount = excess,
            IdempotencyKey = key,
            RequestHash = hash
        };
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            voucher.Description = Clip($"{voucher.Description} - {request.Notes.Trim()}", 500);
        }

        long id;
        try
        {
            id = await Db.ExecuteInTransactionAsync(async ct =>
            {
                voucher.VoucherNumber = await _numbers.NextAsync(DocumentPrefixes.CashReceipt, ct);
                Db.CashReceiptVouchers.Add(voucher);
                await Db.SaveChangesAsync(ct);

                await _ledger.PostCashboxAsync(cashbox, CashboxTransactionType.DriverHandover, amount, 0m,
                    CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, voucher.Description, driver.Name, ct);

                var driverAccount = await _ledger.DriverAccountAsync(driver.Id, cashbox.CurrencyId, ct);
                _ledger.PostDriver(driverAccount, DriverCashMovementType.Handover, debit: 0m, credit: amount, null,
                    CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, $"تسليم نقدية للصندوق {cashbox.Code} بالسند {voucher.VoucherNumber}");

                var now = UtcNow;
                var reference = new CodReference(CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, driver.Id, cashbox.Id);
                foreach (var cod in cods)
                {
                    cod.ReceivedCashboxId = cashbox.Id;
                    cod.ReceiptVoucherId = voucher.Id;
                    cod.ReceivedAtUtc = now;
                    _ledger.MoveCod(cod, CODStatus.ReceivedAtBranchCashbox, CODTransactionType.HandedOver, cod.CollectedAmount, reference);
                }

                _events.Publish(new DriverCashHandedOverEvent
                {
                    ReceiptVoucherId = voucher.Id,
                    VoucherNumber = voucher.VoucherNumber,
                    DriverId = driver.Id,
                    CashboxId = cashbox.Id,
                    BranchId = cashbox.BranchId,
                    CurrencyId = cashbox.CurrencyId,
                    Amount = amount,
                    ExpectedAmount = expected,
                    ShortageAmount = shortage,
                    ExcessAmount = excess,
                    CodTransactionIds = cods.Select(x => x.Id).ToList(),
                    Posting = CashPostings.DriverHandover(voucher.Id, voucher.VoucherNumber, cashbox.CurrencyId, now, amount, cashbox.BranchId, cashbox.Id, driver.Id)
                });
                _dependencies.AuditLog.Add(
                    AuditAction.DriverCashHandedOver,
                    entityName: CashLedger.ReceiptDocument,
                    entityId: voucher.Id.ToString(CultureInfo.InvariantCulture),
                    details: new
                    {
                        voucher.VoucherNumber,
                        driverId = driver.Id,
                        cashbox = cashbox.Code,
                        amount,
                        expected,
                        shortage,
                        excess,
                        shipments = cods.Select(x => x.Shipment.ShipmentNumber)
                    });

                await Db.SaveChangesAsync(ct);
                return voucher.Id;
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (key is not null && BillingAccess.IsUniqueViolation(exception))
        {
            Db.ChangeTracker.Clear();
            return await FindReplayAsync(key, hash, cancellationToken)
                   ?? throw new ConflictException("تعذر تسجيل الاستلام بسبب طلب متزامن. أعد المحاولة.");
        }

        return new CashVoucherCreationResult<CashReceiptVoucherDto> { Voucher = await _vouchers.GetReceiptAsync(id, cancellationToken), IsReplay = false };
    }

    private async Task<CashVoucherCreationResult<CashReceiptVoucherDto>?> FindReplayAsync(string key, string hash, CancellationToken cancellationToken)
    {
        var existing = await Db.CashReceiptVouchers.AsNoTracking()
            .Where(x => x.IdempotencyKey == key)
            .Select(x => new { x.Id, x.RequestHash, x.CreatedByUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return null;
        }

        Idempotency.EnsureSameRequest(existing.RequestHash ?? string.Empty, existing.CreatedByUserId, hash, _dependencies.CurrentUser.UserId);
        return new CashVoucherCreationResult<CashReceiptVoucherDto> { Voucher = await _vouchers.GetReceiptAsync(existing.Id, cancellationToken), IsReplay = true };
    }

    // ------------------------------------------------------------------ ready for settlement

    public async Task<IReadOnlyList<CodTransactionListItemDto>> MarkReadyAsync(MarkCodReadyRequest request, CancellationToken cancellationToken)
    {
        var scope = await _access.CodsAsync(tracked: true, cancellationToken);
        List<CODTransaction> cods;

        if (request.CodTransactionIds.Count > 0)
        {
            cods = await scope.Where(x => request.CodTransactionIds.Contains(x.Id)).ToListAsync(cancellationToken);
            var missing = request.CodTransactionIds.Except(cods.Select(x => x.Id)).ToList();
            if (missing.Count > 0)
            {
                throw new NotFoundException($"شحنات التحصيل غير موجودة: {string.Join(", ", missing)}.");
            }

            if (request.SenderCustomerId.HasValue && cods.Any(x => x.SenderCustomerId != request.SenderCustomerId.Value))
            {
                throw new BusinessRuleException("بعض الشحنات المحددة تخص مرسلاً آخر.");
            }
        }
        else
        {
            var senderId = request.SenderCustomerId!.Value;
            cods = await scope.Where(x => x.SenderCustomerId == senderId && x.Status == CODStatus.ReceivedAtBranchCashbox).ToListAsync(cancellationToken);
            if (cods.Count == 0)
            {
                throw new BusinessRuleException("لا توجد شحنات دفع عند الاستلام مستلمة في الصناديق لهذا المرسل.");
            }
        }

        var shipmentIds = cods.Select(x => x.ShipmentId).ToList();
        var shipments = await Db.Shipments.AsNoTracking()
            .Where(x => shipmentIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.ShipmentNumber,
                x.Status,
                IsInvoiced = x.InvoiceLines.Any(l => l.IsActive),
                x.ChargesPaidBy,
                HasPendingOverride = x.PriceOverrides.Any(o => o.Status == PriceOverrideStatus.Pending),
                Snapshot = x.PricingSnapshots
                    .OrderByDescending(p => p.Id)
                    .Select(p => new { p.TotalShippingCharge, p.CodFee, p.TaxAmount })
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var problems = new List<string>();
        foreach (var cod in cods)
        {
            var shipment = shipments[cod.ShipmentId];
            if (cod.Status != CODStatus.ReceivedAtBranchCashbox)
            {
                problems.Add($"{shipment.ShipmentNumber}: الحالة «{CodNames.Arabic(cod.Status)}» (المطلوب: مستلم في صندوق الفرع)");
            }
            else if (shipment.Status != ShipmentStatus.Delivered)
            {
                problems.Add($"{shipment.ShipmentNumber}: الشحنة لم تعد مسلّمة (مرتجع أو تصحيح حالة)؛ افتح نزاعاً لمعالجتها");
            }
            else if (shipment.HasPendingOverride)
            {
                problems.Add($"{shipment.ShipmentNumber}: لها تعديل سعر بانتظار الموافقة");
            }
        }

        if (problems.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن تجهيز بعض الشحنات للتسوية: " + string.Join("؛ ", problems) + ".");
        }

        var currencies = await Db.Currencies.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.DecimalPlaces, cancellationToken);

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var now = UtcNow;
            foreach (var cod in cods)
            {
                var shipment = shipments[cod.ShipmentId];
                var decimals = currencies.TryGetValue(cod.CurrencyId, out var d) ? d : 2;

                // An invoiced shipment pays its charge through the invoice: nothing is deducted here.
                // Step 9: neither when the charges are on the recipient (the recipient is invoiced, not the sender).
                var recipientPays = shipment.ChargesPaidBy == ChargesPayer.Recipient;
                var deducted = !shipment.IsInvoiced && !recipientPays && shipment.Snapshot is not null;
                cod.ShippingChargeDeduction = deducted ? BillingAccess.Round(shipment.Snapshot!.TotalShippingCharge, decimals) : 0m;
                cod.CodCommissionDeduction = deducted ? BillingAccess.Round(shipment.Snapshot!.CodFee, decimals) : 0m;
                cod.TaxDeduction = deducted ? BillingAccess.Round(shipment.Snapshot!.TaxAmount, decimals) : 0m;
                cod.ShippingChargeDeducted = deducted && cod.ShippingChargeDeduction + cod.CodCommissionDeduction + cod.TaxDeduction > 0m;
                cod.NetPayableAmount = CodSettlementMath.NetPayable(cod.CollectedAmount, Deductions(cod));
                cod.ReadyAtUtc = now;

                _ledger.MoveCod(cod, CODStatus.ReadyForSenderSettlement, CODTransactionType.MarkedReady, cod.NetPayableAmount,
                    new CodReference(CashLedger.CodDocument, cod.Id, shipment.ShipmentNumber),
                    shipment.IsInvoiced ? "أجور الشحن مفوترة على فاتورة؛ لا تُخصم من مبلغ التحصيل."
                    : recipientPays ? "أجور الشحن على المستلم؛ لا تُخصم من مبلغ التحصيل." : null);
            }

            _dependencies.AuditLog.Add(
                AuditAction.CodMarkedReady,
                entityName: EntityName,
                entityId: cods.Count == 1 ? cods[0].Id.ToString(CultureInfo.InvariantCulture) : null,
                details: cods.Select(x => new { x.Id, x.CollectedAmount, x.NetPayableAmount, x.ShippingChargeDeducted }));

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        var ids = cods.Select(x => x.Id).ToList();
        var items = await Db.CODTransactions.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(CodProjections.ListItem)
            .ToListAsync(cancellationToken);
        return items.Select(CodProjections.WithNames).ToList();
    }

    // ------------------------------------------------------------------ disputes

    public async Task<CodTransactionDto> OpenDisputeAsync(long id, OpenCodDisputeRequest request, CancellationToken cancellationToken)
    {
        var cod = await FindForUpdateAsync(id, cancellationToken);

        if (!CodStatusRules.CanDispute(cod.Status))
        {
            throw new BusinessRuleException($"لا يمكن فتح نزاع على مبلغ في حالة «{CodNames.Arabic(cod.Status)}».");
        }

        if (await Db.SenderSettlementLines.AnyAsync(x => x.CODTransactionId == cod.Id && x.IsOpen, cancellationToken))
        {
            throw new BusinessRuleException("المبلغ محجوز في تسوية مفتوحة. ألغِ التسوية أولاً ثم افتح النزاع.");
        }

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            OpenDispute(cod, request.Reason.Trim(), new CodReference(CashLedger.CodDocument, cod.Id, null));
            Audit(AuditAction.CodDisputeOpened, cod.Id, new { reason = request.Reason.Trim(), cod.StatusBeforeDispute });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<CodTransactionDto> ResolveDisputeAsync(long id, ResolveCodDisputeRequest request, CancellationToken cancellationToken)
    {
        var cod = await FindForUpdateAsync(id, cancellationToken);

        if (cod.Status != CODStatus.Disputed)
        {
            throw new BusinessRuleException("المبلغ ليس في حالة نزاع.");
        }

        var openedBy = await Db.CODTransactionHistory.AsNoTracking()
            .Where(x => x.CODTransactionId == cod.Id && x.TransactionType == CODTransactionType.DisputeOpened)
            .OrderByDescending(x => x.Id)
            .Select(x => x.CreatedByUserId)
            .FirstOrDefaultAsync(cancellationToken);
        _access.EnsureOtherApprover(openedBy, "حل النزاع");

        var target = cod.StatusBeforeDispute ?? CODStatus.ReceivedAtBranchCashbox;

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            _ledger.MoveCod(cod, target, CODTransactionType.DisputeResolved, 0m, new CodReference(CashLedger.CodDocument, cod.Id, null), request.Notes.Trim());
            cod.StatusBeforeDispute = null;
            Audit(AuditAction.CodDisputeResolved, cod.Id, new { notes = request.Notes.Trim(), status = target });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    public static CodDeductions Deductions(CODTransaction cod) =>
        new(cod.ShippingChargeDeduction, cod.CodCommissionDeduction, cod.TaxDeduction, cod.OtherFeesAmount, cod.CompensationAmount);

    private void OpenDispute(CODTransaction cod, string reason, CodReference reference)
    {
        cod.StatusBeforeDispute = cod.Status;
        cod.DisputeReason = Clip(reason, 500);
        cod.DisputedAtUtc = UtcNow;
        _ledger.MoveCod(cod, CODStatus.Disputed, CODTransactionType.DisputeOpened, 0m, reference, reason);
    }

    private async Task<CODTransaction> FindForUpdateAsync(long id, CancellationToken cancellationToken)
    {
        var scope = await _access.CodsAsync(tracked: true, cancellationToken);
        return await scope.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
               ?? throw new NotFoundException("مبلغ التحصيل", id);
    }

    private void Audit(AuditAction action, long codId, object details) =>
        _dependencies.AuditLog.Add(action, entityName: EntityName, entityId: codId.ToString(CultureInfo.InvariantCulture), details: details);

    private static string Clip(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
