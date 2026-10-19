using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Entities.Cod;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Cash;

/// <summary>
/// Cash receipt vouchers (سندات القبض) and cash payment vouchers (سندات الصرف) of the cashboxes (step 6).
///
/// Posting = voucher + cashbox transaction + audit + outbox event (with its proposed journal entry), in one
/// database transaction. A posted voucher is never edited: cancelling it adds a reversing cashbox transaction
/// and the opposite journal entry. Cancelling a driver handover gives the money back to the driver and the COD
/// shipments back to "collected by driver"; cancelling a counter COD collection sends the COD back to pending.
/// </summary>
public sealed class CashVoucherService : ICashVoucherService
{
    private static readonly Dictionary<string, Expression<Func<CashReceiptVoucher, object?>>> ReceiptSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["voucherDateUtc"] = x => x.VoucherDateUtc,
        ["voucherNumber"] = x => x.VoucherNumber,
        ["amount"] = x => x.Amount
    };

    private static readonly Dictionary<string, Expression<Func<CashPaymentVoucher, object?>>> PaymentSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["voucherDateUtc"] = x => x.VoucherDateUtc,
        ["voucherNumber"] = x => x.VoucherNumber,
        ["amount"] = x => x.Amount
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly CashAccess _access;
    private readonly CashLedger _ledger;
    private readonly DocumentNumberGenerator _numbers;
    private readonly IIntegrationEventPublisher _events;
    private readonly CashDocumentPdfRenderer _pdf;

    public CashVoucherService(
        MasterDataServiceDependencies dependencies,
        CashAccess access,
        CashLedger ledger,
        DocumentNumberGenerator numbers,
        IIntegrationEventPublisher events,
        CashDocumentPdfRenderer pdf)
    {
        _dependencies = dependencies;
        _access = access;
        _ledger = ledger;
        _numbers = numbers;
        _events = events;
        _pdf = pdf;
    }

    private AppDbContext Db => _dependencies.DbContext;

    private DateTime UtcNow => _dependencies.DateTimeProvider.UtcNow;

    // ================================================================== receipts

    public async Task<PagedResult<CashReceiptVoucherDto>> GetReceiptsAsync(CashVoucherListQuery query, CancellationToken cancellationToken)
    {
        var rows = _access.Receipts();

        if (query.CashboxId.HasValue)
        {
            var cashboxId = query.CashboxId.Value;
            rows = rows.Where(x => x.CashboxId == cashboxId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            if (!Enum.TryParse<CashReceiptType>(query.Type, true, out var type))
            {
                throw CashAccess.Invalid("type", "نوع سند القبض غير صالح.");
            }

            rows = rows.Where(x => x.ReceiptType == type);
        }

        if (query.DriverId.HasValue)
        {
            var driverId = query.DriverId.Value;
            rows = rows.Where(x => x.DriverId == driverId);
        }

        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value;
            rows = rows.Where(x => x.VoucherDateUtc >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = query.ToUtc.Value;
            rows = rows.Where(x => x.VoucherDateUtc < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                x.VoucherNumber.Contains(search) ||
                x.ReceivedFrom.Contains(search) ||
                x.Description.Contains(search) ||
                (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<CashReceiptVoucher> ordered = !string.IsNullOrWhiteSpace(query.SortBy) && ReceiptSorts.TryGetValue(query.SortBy, out var sort)
            ? (query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort))
            : rows.OrderByDescending(x => x.VoucherDateUtc);

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ReceiptProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<CashReceiptVoucherDto>(items.Select(x => CompleteReceipt(x, Array.Empty<HandoverCodLineDto>(), null)).ToList(),
            query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<CashReceiptVoucherDto> GetReceiptAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await _access.Receipts().Where(x => x.Id == id).Select(ReceiptProjection).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("سند القبض", id);

        IReadOnlyList<HandoverCodLineDto> codLines = Array.Empty<HandoverCodLineDto>();
        decimal? driverBalanceAfter = null;

        if (dto.ReceiptType is CashReceiptType.DriverHandover or CashReceiptType.CounterCodCollection)
        {
            // The history keeps the list even after the voucher is cancelled.
            codLines = await Db.CODTransactionHistory.AsNoTracking()
                .Where(x => x.ReferenceType == CashLedger.ReceiptDocument &&
                            x.ReferenceId == id &&
                            (x.TransactionType == CODTransactionType.HandedOver || x.TransactionType == CODTransactionType.CollectedAtCounter))
                .OrderBy(x => x.Id)
                .Select(x => new HandoverCodLineDto
                {
                    CodTransactionId = x.CODTransactionId,
                    ShipmentNumber = x.CODTransaction.Shipment.ShipmentNumber,
                    TrackingNumber = x.CODTransaction.Shipment.TrackingNumber,
                    SenderName = x.CODTransaction.SenderCustomer.ArabicName,
                    Amount = x.Amount
                })
                .ToListAsync(cancellationToken);

            if (dto.ReceiptType == CashReceiptType.DriverHandover)
            {
                driverBalanceAfter = await Db.DriverCashMovements.AsNoTracking()
                    .Where(x => x.DocumentType == CashLedger.ReceiptDocument && x.DocumentId == id && x.MovementType == DriverCashMovementType.Handover)
                    .Select(x => (decimal?)x.BalanceAfter)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        return CompleteReceipt(dto, codLines, driverBalanceAfter);
    }

    public async Task<CashVoucherCreationResult<CashReceiptVoucherDto>> CreateReceiptAsync(
        CreateCashReceiptRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var key = Idempotency.NormalizeKey(idempotencyKey);
        var hash = Idempotency.Hash(request);
        if (key is not null)
        {
            var replay = await FindReceiptReplayAsync(key, hash, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        var cashbox = await _access.CashboxForPostingAsync(request.CashboxId, null, "cashboxId", cancellationToken);
        var amount = BillingAccess.Round(request.Amount, cashbox.Currency.DecimalPlaces);
        var customerId = request.CustomerId;

        if (request.ReceiptType == CashReceiptType.CustomerPayment)
        {
            var paymentId = request.CustomerPaymentId!.Value;
            var payment = await Db.CustomerPayments.AsNoTracking()
                              .Where(x => x.Id == paymentId)
                              .Select(x => new { x.Id, x.PaymentNumber, x.CustomerId, x.CurrencyId, x.Amount, Kind = x.PaymentMethod.Kind })
                              .FirstOrDefaultAsync(cancellationToken)
                          ?? throw CashAccess.Invalid("customerPaymentId", "دفعة العميل غير موجودة.");

            if (payment.Kind != PaymentMethodKind.Cash)
            {
                throw CashAccess.Invalid("customerPaymentId", $"الدفعة {payment.PaymentNumber} ليست نقدية؛ لا تُودع في صندوق.");
            }

            if (payment.CurrencyId != cashbox.CurrencyId)
            {
                throw CashAccess.Invalid("customerPaymentId", $"عملة الدفعة {payment.PaymentNumber} تختلف عن عملة الصندوق.");
            }

            if (payment.Amount != amount)
            {
                throw CashAccess.Invalid("amount", $"مبلغ السند يجب أن يساوي مبلغ الدفعة {payment.PaymentNumber} ({payment.Amount}).");
            }

            if (customerId.HasValue && customerId.Value != payment.CustomerId)
            {
                throw CashAccess.Invalid("customerId", "العميل لا يطابق عميل الدفعة.");
            }

            if (await Db.CashReceiptVouchers.AnyAsync(x => x.CustomerPaymentId == paymentId && x.Status == CashVoucherStatus.Posted, cancellationToken))
            {
                throw new ConflictException($"نقدية الدفعة {payment.PaymentNumber} مودعة في صندوق من قبل.");
            }

            customerId = payment.CustomerId;
        }
        else if (customerId.HasValue && !await Db.Customers.AsNoTracking().AnyAsync(x => x.Id == customerId.Value, cancellationToken))
        {
            throw CashAccess.Invalid("customerId", "العميل غير موجود.");
        }

        var voucher = new CashReceiptVoucher
        {
            CashboxId = cashbox.Id,
            BranchId = cashbox.BranchId,
            CurrencyId = cashbox.CurrencyId,
            ReceiptType = request.ReceiptType,
            Status = CashVoucherStatus.Posted,
            VoucherDateUtc = UtcNow,
            Amount = amount,
            ReceivedFrom = request.ReceivedFrom.Trim(),
            Description = request.Description.Trim(),
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
            CustomerId = customerId,
            CustomerPaymentId = request.CustomerPaymentId,
            IdempotencyKey = key,
            RequestHash = hash
        };

        long id;
        try
        {
            id = await Db.ExecuteInTransactionAsync(async ct =>
            {
                voucher.VoucherNumber = await _numbers.NextAsync(DocumentPrefixes.CashReceipt, ct);
                Db.CashReceiptVouchers.Add(voucher);
                await Db.SaveChangesAsync(ct);

                await _ledger.PostCashboxAsync(cashbox, CashboxTransactionType.CashReceipt, amount, 0m,
                    CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, voucher.Description, voucher.ReceivedFrom, ct);

                _events.Publish(new CashReceiptPostedEvent
                {
                    VoucherId = voucher.Id,
                    VoucherNumber = voucher.VoucherNumber,
                    ReceiptType = voucher.ReceiptType.ToString(),
                    CashboxId = cashbox.Id,
                    BranchId = cashbox.BranchId,
                    CurrencyId = cashbox.CurrencyId,
                    Amount = amount,
                    CustomerId = customerId,
                    CustomerPaymentId = voucher.CustomerPaymentId,
                    Posting = ReceiptPosting(voucher)
                });
                Audit(AuditAction.CashReceiptPosted, CashLedger.ReceiptDocument, voucher.Id,
                    new { voucher.VoucherNumber, voucher.ReceiptType, cashbox = cashbox.Code, amount, voucher.ReceivedFrom });

                await Db.SaveChangesAsync(ct);
                return voucher.Id;
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (key is not null && BillingAccess.IsUniqueViolation(exception))
        {
            Db.ChangeTracker.Clear();
            return await FindReceiptReplayAsync(key, hash, cancellationToken)
                   ?? throw new ConflictException("تعذر تسجيل السند بسبب طلب متزامن. أعد المحاولة.");
        }

        return new CashVoucherCreationResult<CashReceiptVoucherDto> { Voucher = await GetReceiptAsync(id, cancellationToken), IsReplay = false };
    }

    public async Task<CashReceiptVoucherDto> CancelReceiptAsync(long id, CancelCashVoucherRequest request, CancellationToken cancellationToken)
    {
        var voucher = await _access.Receipts(tracked: true).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                      ?? throw new NotFoundException("سند القبض", id);

        if (voucher.Status != CashVoucherStatus.Posted)
        {
            throw new ConflictException($"السند {voucher.VoucherNumber} ملغى من قبل.");
        }

        var cashbox = await Db.Cashboxes.FirstAsync(x => x.Id == voucher.CashboxId, cancellationToken);
        var reason = request.Reason.Trim();

        List<CODTransaction> cods = new();
        if (voucher.ReceiptType is CashReceiptType.DriverHandover or CashReceiptType.CounterCodCollection)
        {
            cods = await Db.CODTransactions.Include(x => x.Shipment).Where(x => x.ReceiptVoucherId == voucher.Id).ToListAsync(cancellationToken);
            var moved = cods.Where(x => x.Status != CODStatus.ReceivedAtBranchCashbox).Select(x => $"{x.Shipment.ShipmentNumber} («{CodNames.Arabic(x.Status)}»)").ToList();
            if (moved.Count > 0)
            {
                throw new BusinessRuleException(
                    "لا يمكن إلغاء السند: بعض شحناته تجاوزت مرحلة الاستلام في الصندوق: " + string.Join("، ", moved) + ".");
            }
        }

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var now = UtcNow;
            await _ledger.ReverseDocumentAsync(cashbox, CashLedger.ReceiptDocument, voucher.Id, $"إلغاء سند القبض {voucher.VoucherNumber}: {reason}", ct);

            AccountingPostingRequest original;
            var reference = new CodReference(CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, voucher.DriverId, cashbox.Id);

            if (voucher.ReceiptType == CashReceiptType.DriverHandover)
            {
                var account = await _ledger.DriverAccountAsync(voucher.DriverId!.Value, voucher.CurrencyId, ct);
                _ledger.PostDriver(account, DriverCashMovementType.HandoverReversed, debit: voucher.Amount, credit: 0m, null,
                    CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, $"إلغاء سند الاستلام {voucher.VoucherNumber}: {reason}");

                foreach (var cod in cods)
                {
                    cod.ReceivedCashboxId = null;
                    cod.ReceiptVoucherId = null;
                    cod.ReceivedAtUtc = null;
                    _ledger.MoveCod(cod, CODStatus.CollectedByDriver, CODTransactionType.HandoverReversed, cod.CollectedAmount, reference, reason);
                }

                original = CashPostings.DriverHandover(voucher.Id, voucher.VoucherNumber, voucher.CurrencyId, voucher.VoucherDateUtc,
                    voucher.Amount, voucher.BranchId, cashbox.Id, voucher.DriverId!.Value);
            }
            else if (voucher.ReceiptType == CashReceiptType.CounterCodCollection)
            {
                var cod = cods.Single();
                var collected = cod.CollectedAmount;
                cod.CollectedAmount = 0m;
                cod.CollectedAtUtc = null;
                cod.CollectedByUserId = null;
                cod.CollectionDifferenceReason = null;
                cod.ReceivedCashboxId = null;
                cod.ReceiptVoucherId = null;
                cod.ReceivedAtUtc = null;
                _ledger.MoveCod(cod, CODStatus.Pending, CODTransactionType.HandoverReversed, collected, reference, $"إلغاء التحصيل في الفرع: {reason}");

                original = CashPostings.CodCollected(cod.Id, cod.Shipment.ShipmentNumber, voucher.CurrencyId, voucher.VoucherDateUtc,
                    collected, cod.SenderCustomerId, voucher.BranchId, null, cashbox.Id);
            }
            else
            {
                original = ReceiptPosting(voucher);
            }

            voucher.Status = CashVoucherStatus.Cancelled;
            voucher.CancelledAtUtc = now;
            voucher.CancelledByUserId = _dependencies.CurrentUser.UserId;
            voucher.CancellationReason = reason;

            _events.Publish(new CashVoucherCancelledEvent
            {
                VoucherKind = CashLedger.ReceiptDocument,
                VoucherId = voucher.Id,
                VoucherNumber = voucher.VoucherNumber,
                VoucherType = voucher.ReceiptType.ToString(),
                CashboxId = cashbox.Id,
                CurrencyId = voucher.CurrencyId,
                Amount = voucher.Amount,
                Reason = reason,
                Posting = original.Reverse($"إلغاء سند القبض {voucher.VoucherNumber}", now)
            });
            Audit(AuditAction.CashVoucherCancelled, CashLedger.ReceiptDocument, voucher.Id,
                new { voucher.VoucherNumber, voucher.ReceiptType, voucher.Amount, reason, shipments = cods.Select(x => x.Shipment.ShipmentNumber) });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetReceiptAsync(id, cancellationToken);
    }

    public async Task<FileDownload> GetReceiptPdfAsync(long id, CancellationToken cancellationToken)
    {
        var voucher = await GetReceiptAsync(id, cancellationToken);
        var company = await CashDocumentPdfRenderer.LoadCompanyAsync(Db, cancellationToken);
        return FileDownload.FromBytes(_pdf.RenderReceipt(voucher, company), "application/pdf", $"{voucher.VoucherNumber}.pdf");
    }

    // ================================================================== payments

    public async Task<PagedResult<CashPaymentVoucherDto>> GetPaymentsAsync(CashVoucherListQuery query, CancellationToken cancellationToken)
    {
        var rows = _access.Payments();

        if (query.CashboxId.HasValue)
        {
            var cashboxId = query.CashboxId.Value;
            rows = rows.Where(x => x.CashboxId == cashboxId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            if (!Enum.TryParse<CashPaymentType>(query.Type, true, out var type))
            {
                throw CashAccess.Invalid("type", "نوع سند الصرف غير صالح.");
            }

            rows = rows.Where(x => x.PaymentType == type);
        }

        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value;
            rows = rows.Where(x => x.VoucherDateUtc >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = query.ToUtc.Value;
            rows = rows.Where(x => x.VoucherDateUtc < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                x.VoucherNumber.Contains(search) ||
                x.PaidTo.Contains(search) ||
                x.Description.Contains(search) ||
                (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<CashPaymentVoucher> ordered = !string.IsNullOrWhiteSpace(query.SortBy) && PaymentSorts.TryGetValue(query.SortBy, out var sort)
            ? (query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort))
            : rows.OrderByDescending(x => x.VoucherDateUtc);

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(PaymentProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<CashPaymentVoucherDto>(items.Select(CompletePayment).ToList(), query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<CashPaymentVoucherDto> GetPaymentAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await _access.Payments().Where(x => x.Id == id).Select(PaymentProjection).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("سند الصرف", id);
        return CompletePayment(dto);
    }

    public async Task<CashVoucherCreationResult<CashPaymentVoucherDto>> CreatePaymentAsync(
        CreateCashPaymentRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var key = Idempotency.NormalizeKey(idempotencyKey);
        var hash = Idempotency.Hash(request);
        if (key is not null)
        {
            var replay = await FindPaymentReplayAsync(key, hash, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        var cashbox = await _access.CashboxForPostingAsync(request.CashboxId, null, "cashboxId", cancellationToken);
        var amount = BillingAccess.Round(request.Amount, cashbox.Currency.DecimalPlaces);
        await ValidatePaymentLinksAsync(request, cancellationToken);

        if (amount > cashbox.CurrentBalance)
        {
            throw new BusinessRuleException($"رصيد الصندوق {cashbox.Code} ({cashbox.CurrentBalance}) لا يكفي لصرف {amount}.");
        }

        var voucher = new CashPaymentVoucher
        {
            CashboxId = cashbox.Id,
            BranchId = cashbox.BranchId,
            CurrencyId = cashbox.CurrencyId,
            PaymentType = request.PaymentType,
            Status = CashVoucherStatus.Posted,
            VoucherDateUtc = UtcNow,
            Amount = amount,
            PaidTo = request.PaidTo.Trim(),
            Description = request.Description.Trim(),
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
            ExpenseCategoryId = request.PaymentType == CashPaymentType.Expense ? request.ExpenseCategoryId : null,
            SupplierId = request.PaymentType == CashPaymentType.SupplierPayment ? request.SupplierId : null,
            EmployeeId = request.PaymentType == CashPaymentType.EmployeeAdvance ? request.EmployeeId : null,
            CustomerId = request.PaymentType == CashPaymentType.CustomerRefund ? request.CustomerId : null,
            IdempotencyKey = key,
            RequestHash = hash
        };

        long id;
        try
        {
            id = await Db.ExecuteInTransactionAsync(async ct =>
            {
                voucher.VoucherNumber = await _numbers.NextAsync(DocumentPrefixes.CashPayment, ct);
                Db.CashPaymentVouchers.Add(voucher);
                await Db.SaveChangesAsync(ct);

                await _ledger.PostCashboxAsync(cashbox, CashboxTransactionType.CashPayment, 0m, amount,
                    CashLedger.PaymentDocument, voucher.Id, voucher.VoucherNumber, voucher.Description, voucher.PaidTo, ct);

                _events.Publish(new CashPaymentPostedEvent
                {
                    VoucherId = voucher.Id,
                    VoucherNumber = voucher.VoucherNumber,
                    PaymentType = voucher.PaymentType.ToString(),
                    CashboxId = cashbox.Id,
                    BranchId = cashbox.BranchId,
                    CurrencyId = cashbox.CurrencyId,
                    Amount = amount,
                    ExpenseCategoryId = voucher.ExpenseCategoryId,
                    SupplierId = voucher.SupplierId,
                    EmployeeId = voucher.EmployeeId,
                    CustomerId = voucher.CustomerId,
                    Posting = PaymentPosting(voucher)
                });
                Audit(AuditAction.CashPaymentPosted, CashLedger.PaymentDocument, voucher.Id,
                    new { voucher.VoucherNumber, voucher.PaymentType, cashbox = cashbox.Code, amount, voucher.PaidTo });

                await Db.SaveChangesAsync(ct);
                return voucher.Id;
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (key is not null && BillingAccess.IsUniqueViolation(exception))
        {
            Db.ChangeTracker.Clear();
            return await FindPaymentReplayAsync(key, hash, cancellationToken)
                   ?? throw new ConflictException("تعذر تسجيل السند بسبب طلب متزامن. أعد المحاولة.");
        }

        return new CashVoucherCreationResult<CashPaymentVoucherDto> { Voucher = await GetPaymentAsync(id, cancellationToken), IsReplay = false };
    }

    public async Task<CashPaymentVoucherDto> CancelPaymentAsync(long id, CancelCashVoucherRequest request, CancellationToken cancellationToken)
    {
        var voucher = await _access.Payments(tracked: true).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                      ?? throw new NotFoundException("سند الصرف", id);

        if (voucher.Status != CashVoucherStatus.Posted)
        {
            throw new ConflictException($"السند {voucher.VoucherNumber} ملغى من قبل.");
        }

        if (voucher.PaymentType == CashPaymentType.SenderSettlement)
        {
            throw new BusinessRuleException("هذا سند دفع تسوية مرسل: اعكس التسوية نفسها (POST /cod/settlements/{id}/reverse) لإلغائه.");
        }

        var cashbox = await Db.Cashboxes.FirstAsync(x => x.Id == voucher.CashboxId, cancellationToken);
        var reason = request.Reason.Trim();

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var now = UtcNow;
            await _ledger.ReverseDocumentAsync(cashbox, CashLedger.PaymentDocument, voucher.Id, $"إلغاء سند الصرف {voucher.VoucherNumber}: {reason}", ct);

            voucher.Status = CashVoucherStatus.Cancelled;
            voucher.CancelledAtUtc = now;
            voucher.CancelledByUserId = _dependencies.CurrentUser.UserId;
            voucher.CancellationReason = reason;

            _events.Publish(new CashVoucherCancelledEvent
            {
                VoucherKind = CashLedger.PaymentDocument,
                VoucherId = voucher.Id,
                VoucherNumber = voucher.VoucherNumber,
                VoucherType = voucher.PaymentType.ToString(),
                CashboxId = cashbox.Id,
                CurrencyId = voucher.CurrencyId,
                Amount = voucher.Amount,
                Reason = reason,
                Posting = PaymentPosting(voucher).Reverse($"إلغاء سند الصرف {voucher.VoucherNumber}", now)
            });
            Audit(AuditAction.CashVoucherCancelled, CashLedger.PaymentDocument, voucher.Id, new { voucher.VoucherNumber, voucher.PaymentType, voucher.Amount, reason });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetPaymentAsync(id, cancellationToken);
    }

    public async Task<FileDownload> GetPaymentPdfAsync(long id, CancellationToken cancellationToken)
    {
        var voucher = await GetPaymentAsync(id, cancellationToken);
        var company = await CashDocumentPdfRenderer.LoadCompanyAsync(Db, cancellationToken);
        return FileDownload.FromBytes(_pdf.RenderPayment(voucher, company), "application/pdf", $"{voucher.VoucherNumber}.pdf");
    }

    // ================================================================== helpers

    private async Task ValidatePaymentLinksAsync(CreateCashPaymentRequest request, CancellationToken cancellationToken)
    {
        switch (request.PaymentType)
        {
            case CashPaymentType.Expense:
                var category = await Db.ExpenseCategories.AsNoTracking().Where(x => x.Id == request.ExpenseCategoryId).Select(x => (bool?)x.IsActive).FirstOrDefaultAsync(cancellationToken);
                if (category is null)
                {
                    throw CashAccess.Invalid("expenseCategoryId", "بند المصروف غير موجود.");
                }

                if (category == false)
                {
                    throw CashAccess.Invalid("expenseCategoryId", "بند المصروف غير مفعّل.");
                }

                break;

            case CashPaymentType.SupplierPayment:
                if (!await Db.Suppliers.AsNoTracking().AnyAsync(x => x.Id == request.SupplierId, cancellationToken))
                {
                    throw CashAccess.Invalid("supplierId", "المورد غير موجود.");
                }

                break;

            case CashPaymentType.EmployeeAdvance:
                if (!await Db.Employees.AsNoTracking().AnyAsync(x => x.Id == request.EmployeeId && x.IsActive, cancellationToken))
                {
                    throw CashAccess.Invalid("employeeId", "الموظف غير موجود أو غير مفعّل.");
                }

                break;

            case CashPaymentType.CustomerRefund:
                if (!await Db.Customers.AsNoTracking().AnyAsync(x => x.Id == request.CustomerId, cancellationToken))
                {
                    throw CashAccess.Invalid("customerId", "العميل غير موجود.");
                }

                break;
        }
    }

    private static AccountingPostingRequest ReceiptPosting(CashReceiptVoucher voucher) =>
        CashPostings.CashReceipt(voucher.Id, voucher.VoucherNumber, voucher.ReceiptType, voucher.CurrencyId, voucher.VoucherDateUtc,
            voucher.Amount, voucher.BranchId, voucher.CashboxId, voucher.CustomerId);

    private static AccountingPostingRequest PaymentPosting(CashPaymentVoucher voucher) =>
        CashPostings.CashPayment(voucher.Id, voucher.VoucherNumber, voucher.PaymentType, voucher.CurrencyId, voucher.VoucherDateUtc,
            voucher.Amount, voucher.BranchId, voucher.CashboxId, voucher.ExpenseCategoryId, voucher.SupplierId, voucher.EmployeeId, voucher.CustomerId);

    private async Task<CashVoucherCreationResult<CashReceiptVoucherDto>?> FindReceiptReplayAsync(string key, string hash, CancellationToken cancellationToken)
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
        return new CashVoucherCreationResult<CashReceiptVoucherDto> { Voucher = await GetReceiptAsync(existing.Id, cancellationToken), IsReplay = true };
    }

    private async Task<CashVoucherCreationResult<CashPaymentVoucherDto>?> FindPaymentReplayAsync(string key, string hash, CancellationToken cancellationToken)
    {
        var existing = await Db.CashPaymentVouchers.AsNoTracking()
            .Where(x => x.IdempotencyKey == key)
            .Select(x => new { x.Id, x.RequestHash, x.CreatedByUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return null;
        }

        Idempotency.EnsureSameRequest(existing.RequestHash ?? string.Empty, existing.CreatedByUserId, hash, _dependencies.CurrentUser.UserId);
        return new CashVoucherCreationResult<CashPaymentVoucherDto> { Voucher = await GetPaymentAsync(existing.Id, cancellationToken), IsReplay = true };
    }

    private void Audit(AuditAction action, string entityName, long id, object details) =>
        _dependencies.AuditLog.Add(action, entityName: entityName, entityId: id.ToString(CultureInfo.InvariantCulture), details: details);

    // ------------------------------------------------------------------ projections

    private static readonly Expression<Func<CashReceiptVoucher, CashReceiptVoucherDto>> ReceiptProjection = x => new CashReceiptVoucherDto
    {
        Id = x.Id,
        VoucherNumber = x.VoucherNumber,
        CashboxId = x.CashboxId,
        CashboxCode = x.Cashbox.Code,
        CashboxName = x.Cashbox.ArabicName,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        CurrencyName = x.Currency.ArabicName,
        CurrencyDecimals = x.Currency.DecimalPlaces,
        ReceiptType = x.ReceiptType,
        Status = x.Status,
        VoucherDateUtc = x.VoucherDateUtc,
        Amount = x.Amount,
        ReceivedFrom = x.ReceivedFrom,
        Description = x.Description,
        ReferenceNumber = x.ReferenceNumber,
        DriverId = x.DriverId,
        DriverName = x.Driver != null ? x.Driver.Employee.ArabicName : null,
        CustomerId = x.CustomerId,
        CustomerName = x.Customer != null ? x.Customer.ArabicName : null,
        CustomerPaymentId = x.CustomerPaymentId,
        CustomerPaymentNumber = x.CustomerPayment != null ? x.CustomerPayment.PaymentNumber : null,
        ExpectedAmount = x.ExpectedAmount,
        ShortageAmount = x.ShortageAmount,
        ExcessAmount = x.ExcessAmount,
        CancelledAtUtc = x.CancelledAtUtc,
        CancelledByUserId = x.CancelledByUserId,
        CancellationReason = x.CancellationReason,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId
    };

    private static readonly Expression<Func<CashPaymentVoucher, CashPaymentVoucherDto>> PaymentProjection = x => new CashPaymentVoucherDto
    {
        Id = x.Id,
        VoucherNumber = x.VoucherNumber,
        CashboxId = x.CashboxId,
        CashboxCode = x.Cashbox.Code,
        CashboxName = x.Cashbox.ArabicName,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        CurrencyName = x.Currency.ArabicName,
        CurrencyDecimals = x.Currency.DecimalPlaces,
        PaymentType = x.PaymentType,
        Status = x.Status,
        VoucherDateUtc = x.VoucherDateUtc,
        Amount = x.Amount,
        PaidTo = x.PaidTo,
        Description = x.Description,
        ReferenceNumber = x.ReferenceNumber,
        ExpenseCategoryId = x.ExpenseCategoryId,
        ExpenseCategoryName = x.ExpenseCategory != null ? x.ExpenseCategory.ArabicName : null,
        SupplierId = x.SupplierId,
        SupplierName = x.Supplier != null ? x.Supplier.ArabicName : null,
        EmployeeId = x.EmployeeId,
        EmployeeName = x.Employee != null ? x.Employee.ArabicName : null,
        CustomerId = x.CustomerId,
        CustomerName = x.Customer != null ? x.Customer.ArabicName : null,
        SenderSettlementId = x.SenderSettlementId,
        CancelledAtUtc = x.CancelledAtUtc,
        CancelledByUserId = x.CancelledByUserId,
        CancellationReason = x.CancellationReason,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId
    };

    private static CashReceiptVoucherDto CompleteReceipt(CashReceiptVoucherDto x, IReadOnlyList<HandoverCodLineDto> codLines, decimal? driverBalanceAfter) => new()
    {
        Id = x.Id,
        VoucherNumber = x.VoucherNumber,
        CashboxId = x.CashboxId,
        CashboxCode = x.CashboxCode,
        CashboxName = x.CashboxName,
        BranchId = x.BranchId,
        BranchName = x.BranchName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.CurrencyCode,
        CurrencyName = x.CurrencyName,
        CurrencyDecimals = x.CurrencyDecimals,
        ReceiptType = x.ReceiptType,
        ReceiptTypeName = CodNames.Arabic(x.ReceiptType),
        Status = x.Status,
        StatusName = CodNames.Arabic(x.Status),
        VoucherDateUtc = x.VoucherDateUtc,
        Amount = x.Amount,
        AmountInWords = ArabicAmountInWords.Format(x.Amount, x.CurrencyName, x.CurrencyDecimals),
        ReceivedFrom = x.ReceivedFrom,
        Description = x.Description,
        ReferenceNumber = x.ReferenceNumber,
        DriverId = x.DriverId,
        DriverName = x.DriverName,
        CustomerId = x.CustomerId,
        CustomerName = x.CustomerName,
        CustomerPaymentId = x.CustomerPaymentId,
        CustomerPaymentNumber = x.CustomerPaymentNumber,
        ExpectedAmount = x.ExpectedAmount,
        ShortageAmount = x.ShortageAmount,
        ExcessAmount = x.ExcessAmount,
        DriverBalanceAfter = driverBalanceAfter,
        CancelledAtUtc = x.CancelledAtUtc,
        CancelledByUserId = x.CancelledByUserId,
        CancellationReason = x.CancellationReason,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        CodLines = codLines
    };

    private static CashPaymentVoucherDto CompletePayment(CashPaymentVoucherDto x) => new()
    {
        Id = x.Id,
        VoucherNumber = x.VoucherNumber,
        CashboxId = x.CashboxId,
        CashboxCode = x.CashboxCode,
        CashboxName = x.CashboxName,
        BranchId = x.BranchId,
        BranchName = x.BranchName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.CurrencyCode,
        CurrencyName = x.CurrencyName,
        CurrencyDecimals = x.CurrencyDecimals,
        PaymentType = x.PaymentType,
        PaymentTypeName = CodNames.Arabic(x.PaymentType),
        Status = x.Status,
        StatusName = CodNames.Arabic(x.Status),
        VoucherDateUtc = x.VoucherDateUtc,
        Amount = x.Amount,
        AmountInWords = ArabicAmountInWords.Format(x.Amount, x.CurrencyName, x.CurrencyDecimals),
        PaidTo = x.PaidTo,
        Description = x.Description,
        ReferenceNumber = x.ReferenceNumber,
        ExpenseCategoryId = x.ExpenseCategoryId,
        ExpenseCategoryName = x.ExpenseCategoryName,
        SupplierId = x.SupplierId,
        SupplierName = x.SupplierName,
        EmployeeId = x.EmployeeId,
        EmployeeName = x.EmployeeName,
        CustomerId = x.CustomerId,
        CustomerName = x.CustomerName,
        SenderSettlementId = x.SenderSettlementId,
        CancelledAtUtc = x.CancelledAtUtc,
        CancelledByUserId = x.CancelledByUserId,
        CancellationReason = x.CancellationReason,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId
    };
}
