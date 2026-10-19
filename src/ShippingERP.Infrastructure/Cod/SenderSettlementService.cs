using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cod;
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
/// Sender settlements (step 6): pay senders the NET amount of their collected COD.
///
///   Draft    : the ready COD shipments (or parts of them) are reserved; the totals and deductions are fixed.
///   Approved : by a user other than the creator.
///   Paid     : cash (payment voucher + cashbox out) or bank transfer; the CODs become (partially) settled; the
///              event carries the journal entry that turns the deductions into revenue.
///   Cancelled: Draft / Approved; the CODs are released.
///   Reversed : Paid, by mistake; only the latest payment of its CODs; the money comes back and the CODs are
///              payable again.
/// </summary>
public sealed class SenderSettlementService : ISenderSettlementService
{
    private const string EntityName = "SenderSettlement";

    private static readonly Dictionary<string, Expression<Func<SenderSettlement, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["settlementDateUtc"] = x => x.SettlementDateUtc,
        ["settlementNumber"] = x => x.SettlementNumber,
        ["netPayable"] = x => x.NetPayable
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly CashAccess _access;
    private readonly CashLedger _ledger;
    private readonly DocumentNumberGenerator _numbers;
    private readonly IIntegrationEventPublisher _events;
    private readonly CashDocumentPdfRenderer _pdf;

    public SenderSettlementService(
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

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<SenderSettlementListItemDto>> GetPagedAsync(SenderSettlementListQuery query, CancellationToken cancellationToken)
    {
        var rows = Filter(_access.Settlements(), query.Status, query.SenderCustomerId, query.BranchId, query.CurrencyId, query.FromUtc, query.ToUtc);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.SettlementNumber.Contains(search) || x.SenderCustomer.Code.Contains(search) || x.SenderCustomer.ArabicName.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<SenderSettlement> ordered = !string.IsNullOrWhiteSpace(query.SortBy) && Sorts.TryGetValue(query.SortBy, out var sort)
            ? (query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort))
            : rows.OrderByDescending(x => x.SettlementDateUtc);

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ListProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<SenderSettlementListItemDto>(items.Select(WithName).ToList(), query.PageNumber, query.PageSize, totalCount);
    }

    public static IQueryable<SenderSettlement> Filter(
        IQueryable<SenderSettlement> rows,
        SettlementStatus? status,
        long? senderCustomerId,
        long? branchId,
        long? currencyId,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (status.HasValue)
        {
            var value = status.Value;
            rows = rows.Where(x => x.Status == value);
        }

        if (senderCustomerId.HasValue)
        {
            var value = senderCustomerId.Value;
            rows = rows.Where(x => x.SenderCustomerId == value);
        }

        if (branchId.HasValue)
        {
            var value = branchId.Value;
            rows = rows.Where(x => x.BranchId == value);
        }

        if (currencyId.HasValue)
        {
            var value = currencyId.Value;
            rows = rows.Where(x => x.CurrencyId == value);
        }

        if (fromUtc.HasValue)
        {
            var value = fromUtc.Value;
            rows = rows.Where(x => x.SettlementDateUtc >= value);
        }

        if (toUtc.HasValue)
        {
            var value = toUtc.Value;
            rows = rows.Where(x => x.SettlementDateUtc < value);
        }

        return rows;
    }

    public async Task<SenderSettlementDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await _access.Settlements().Where(x => x.Id == id).Select(DetailProjection).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("التسوية", id);
        return WithName(dto);
    }

    public async Task<IReadOnlyList<SettlementCandidateDto>> GetCandidatesAsync(SettlementCandidatesQuery query, CancellationToken cancellationToken)
    {
        var currency = await _access.Billing.GetCurrencyAsync(query.CurrencyId, cancellationToken);
        var scope = await _access.CodsAsync(tracked: false, cancellationToken);

        return await scope
            .Where(x => x.SenderCustomerId == query.SenderCustomerId &&
                        x.CurrencyId == currency.Id &&
                        (x.Status == CODStatus.ReadyForSenderSettlement || x.Status == CODStatus.PartiallySettled) &&
                        x.NetPayableAmount != x.SettledAmount &&
                        !x.SettlementLines.Any(l => l.IsOpen))
            .OrderBy(x => x.CollectedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new SettlementCandidateDto
            {
                CodTransactionId = x.Id,
                ShipmentId = x.ShipmentId,
                ShipmentNumber = x.Shipment.ShipmentNumber,
                TrackingNumber = x.Shipment.TrackingNumber,
                Status = x.Status,
                CollectedAmount = x.CollectedAmount,
                ShippingChargeDeduction = x.ShippingChargeDeduction,
                CodCommissionDeduction = x.CodCommissionDeduction,
                TaxDeduction = x.TaxDeduction,
                OtherFeesAmount = x.OtherFeesAmount,
                CompensationAmount = x.CompensationAmount,
                NetPayableAmount = x.NetPayableAmount,
                SettledAmount = x.SettledAmount,
                RemainingPayable = x.NetPayableAmount - x.SettledAmount,
                CollectedAtUtc = x.CollectedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ create

    public async Task<SenderSettlementDto> CreateAsync(CreateSenderSettlementRequest request, CancellationToken cancellationToken)
    {
        var sender = await Db.Customers.AsNoTracking()
                         .Where(x => x.Id == request.SenderCustomerId)
                         .Select(x => new { x.Id, x.DefaultBranchId })
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? throw CashAccess.Invalid("senderCustomerId", "المرسل غير موجود.");

        var currency = await _access.Billing.GetCurrencyAsync(request.CurrencyId, cancellationToken);
        var branchId = await _access.Billing.ResolveBranchAsync(request.BranchId, sender.DefaultBranchId, cancellationToken);
        var scope = await _access.CodsAsync(tracked: true, cancellationToken);

        // What was requested: id -> amount (null = everything remaining).
        Dictionary<long, decimal?> requested;
        List<CODTransaction> cods;

        if (request.Lines.Count > 0 || request.CodTransactionIds.Count > 0)
        {
            requested = request.Lines.Count > 0
                ? request.Lines.ToDictionary(x => x.CodTransactionId, x => x.Amount)
                : request.CodTransactionIds.ToDictionary(x => x, _ => (decimal?)null);

            var ids = requested.Keys.ToList();
            cods = await scope.Include(x => x.Shipment).Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);

            var missing = ids.Except(cods.Select(x => x.Id)).ToList();
            if (missing.Count > 0)
            {
                throw new NotFoundException($"شحنات التحصيل غير موجودة: {string.Join(", ", missing)}.");
            }
        }
        else
        {
            cods = await scope.Include(x => x.Shipment)
                .Where(x => x.SenderCustomerId == sender.Id &&
                            x.CurrencyId == currency.Id &&
                            (x.Status == CODStatus.ReadyForSenderSettlement || x.Status == CODStatus.PartiallySettled) &&
                            x.NetPayableAmount != x.SettledAmount &&
                            !x.SettlementLines.Any(l => l.IsOpen))
                .OrderBy(x => x.CollectedAtUtc)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
            requested = cods.ToDictionary(x => x.Id, _ => (decimal?)null);
        }

        var codIds = cods.Select(x => x.Id).ToList();
        var reserved = await Db.SenderSettlementLines.AsNoTracking()
            .Where(x => x.IsOpen && codIds.Contains(x.CODTransactionId))
            .Select(x => new { x.CODTransactionId, x.SenderSettlement.SettlementNumber })
            .ToListAsync(cancellationToken);

        var problems = new List<string>();
        var lines = new List<(CODTransaction Cod, SettlementLinePlan Plan)>();

        foreach (var cod in cods)
        {
            var label = cod.Shipment.ShipmentNumber;
            var reservedBy = reserved.FirstOrDefault(x => x.CODTransactionId == cod.Id);

            if (cod.SenderCustomerId != sender.Id)
            {
                problems.Add($"{label}: تخص مرسلاً آخر");
            }
            else if (cod.CurrencyId != currency.Id)
            {
                problems.Add($"{label}: بعملة أخرى");
            }
            else if (!CodStatusRules.IsSettleable(cod.Status))
            {
                problems.Add($"{label}: الحالة «{CodNames.Arabic(cod.Status)}» (يجب أن تكون جاهزة للتسوية)");
            }
            else if (reservedBy is not null)
            {
                problems.Add($"{label}: محجوزة في التسوية {reservedBy.SettlementNumber}");
            }
            else
            {
                var requestedAmount = requested[cod.Id];
                var plan = CodSettlementMath.PlanLine(
                    cod.NetPayableAmount,
                    cod.SettledAmount,
                    cod.DeductionsRecognized,
                    CodService.Deductions(cod),
                    requestedAmount.HasValue ? BillingAccess.Round(requestedAmount.Value, currency.DecimalPlaces) : null,
                    out var error);

                if (plan is null)
                {
                    problems.Add($"{label}: {error}");
                }
                else
                {
                    lines.Add((cod, plan));
                }
            }
        }

        if (problems.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن إنشاء التسوية: " + string.Join("؛ ", problems) + ".");
        }

        if (lines.Count == 0)
        {
            throw new BusinessRuleException("لا توجد مبالغ جاهزة للتسوية لهذا المرسل بهذه العملة.");
        }

        var net = lines.Sum(x => x.Plan.Amount);
        if (net <= 0m)
        {
            throw new BusinessRuleException(
                $"صافي التسوية ({net}) يجب أن يكون أكبر من صفر: الرسوم المستحقة على المرسل أكبر من المبالغ المحصّلة. أضف شحنات أخرى أو فوتر الرسوم.");
        }

        var settlement = new SenderSettlement
        {
            SenderCustomerId = sender.Id,
            BranchId = branchId,
            CurrencyId = currency.Id,
            Status = SettlementStatus.Draft,
            SettlementDateUtc = UtcNow,
            TotalGrossReleased = lines.Sum(x => x.Plan.GrossReleased),
            TotalShippingCharges = lines.Sum(x => x.Plan.Recognized.ShippingCharge),
            TotalCodCommission = lines.Sum(x => x.Plan.Recognized.CodCommission),
            TotalTax = lines.Sum(x => x.Plan.Recognized.Tax),
            TotalOtherFees = lines.Sum(x => x.Plan.Recognized.OtherFees),
            TotalCompensation = lines.Sum(x => x.Plan.Recognized.Compensation),
            NetPayable = net,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        foreach (var (cod, plan) in lines)
        {
            settlement.Lines.Add(new SenderSettlementLine
            {
                CODTransactionId = cod.Id,
                ShipmentId = cod.ShipmentId,
                CollectedAmount = cod.CollectedAmount,
                RemainingBefore = plan.RemainingBefore,
                ShippingCharge = plan.Recognized.ShippingCharge,
                CodCommission = plan.Recognized.CodCommission,
                Tax = plan.Recognized.Tax,
                OtherFees = plan.Recognized.OtherFees,
                Compensation = plan.Recognized.Compensation,
                RecognizesDeductions = plan.RecognizesDeductions,
                Amount = plan.Amount,
                GrossReleased = plan.GrossReleased,
                IsOpen = true
            });
        }

        try
        {
            await Db.ExecuteInTransactionAsync(async ct =>
            {
                settlement.SettlementNumber = await _numbers.NextAsync(DocumentPrefixes.SenderSettlement, ct);
                Db.SenderSettlements.Add(settlement);
                await Db.SaveChangesAsync(ct);

                Audit(AuditAction.SenderSettlementCreated, settlement.Id, new
                {
                    settlement.SettlementNumber,
                    settlement.SenderCustomerId,
                    settlement.NetPayable,
                    shipments = lines.Select(x => new { x.Cod.Shipment.ShipmentNumber, x.Plan.Amount })
                });
                await Db.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (BillingAccess.IsUniqueViolation(exception))
        {
            // UX_SenderSettlementLines_OpenCod: another settlement reserved one of the CODs a moment ago.
            throw new ConflictException("إحدى الشحنات حُجزت للتو في تسوية أخرى. حدّث القائمة وأعد المحاولة.");
        }

        return await GetByIdAsync(settlement.Id, cancellationToken);
    }

    // ------------------------------------------------------------------ approve / pay

    public async Task<SenderSettlementDto> ApproveAsync(long id, ApproveSenderSettlementRequest request, CancellationToken cancellationToken)
    {
        var settlement = await FindAsync(id, cancellationToken);

        if (settlement.Status != SettlementStatus.Draft)
        {
            throw new BusinessRuleException($"التسوية في حالة «{CodNames.Arabic(settlement.Status)}»؛ الاعتماد للمسودات فقط.");
        }

        _access.EnsureOtherApprover(settlement.CreatedByUserId, "التسوية");
        await EnsureLinesStillValidAsync(settlement, cancellationToken);

        settlement.Status = SettlementStatus.Approved;
        settlement.ApprovedByUserId = _dependencies.CurrentUser.UserId;
        settlement.ApprovedAtUtc = UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            settlement.Notes = Clip(string.IsNullOrWhiteSpace(settlement.Notes) ? request.Notes.Trim() : $"{settlement.Notes} | {request.Notes.Trim()}", 1000);
        }

        Audit(AuditAction.SenderSettlementApproved, settlement.Id, new { settlement.SettlementNumber, settlement.NetPayable });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<SenderSettlementDto> PayAsync(long id, PaySenderSettlementRequest request, CancellationToken cancellationToken)
    {
        var settlement = await FindAsync(id, cancellationToken);

        if (settlement.Status != SettlementStatus.Approved)
        {
            throw new BusinessRuleException(settlement.Status == SettlementStatus.Draft
                ? "اعتمد التسوية أولاً، ثم ادفعها."
                : $"التسوية في حالة «{CodNames.Arabic(settlement.Status)}» ولا يمكن دفعها.");
        }

        var method = await Db.PaymentMethods.AsNoTracking()
                         .Where(x => x.Id == request.PaymentMethodId)
                         .Select(x => new { x.Id, x.Kind, x.IsActive, x.RequiresReference, x.ArabicName })
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? throw CashAccess.Invalid("paymentMethodId", "طريقة الدفع غير موجودة.");

        if (!method.IsActive)
        {
            throw CashAccess.Invalid("paymentMethodId", "طريقة الدفع غير مفعّلة.");
        }

        if (method.Kind is not (PaymentMethodKind.Cash or PaymentMethodKind.BankTransfer))
        {
            throw CashAccess.Invalid("paymentMethodId", "تُدفع التسوية نقداً من صندوق أو بتحويل بنكي فقط.");
        }

        var reference = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim();
        Cashbox? cashbox = null;
        BankAccount? bank = null;

        if (method.Kind == PaymentMethodKind.Cash)
        {
            if (request.CashboxId is null)
            {
                throw CashAccess.Invalid("cashboxId", "حدد الصندوق الذي يُدفع منه.");
            }

            cashbox = await _access.CashboxForPostingAsync(request.CashboxId.Value, settlement.CurrencyId, "cashboxId", cancellationToken);
            if (cashbox.CurrentBalance < settlement.NetPayable)
            {
                throw new BusinessRuleException(
                    $"رصيد الصندوق {cashbox.Code} ({cashbox.CurrentBalance}) لا يكفي لدفع التسوية ({settlement.NetPayable}). حوّل نقدية إلى الصندوق أو ادفع بتحويل بنكي.");
            }
        }
        else
        {
            if (request.BankAccountId is null)
            {
                throw CashAccess.Invalid("bankAccountId", "حدد الحساب البنكي الذي حُوّل منه المبلغ.");
            }

            bank = await Db.BankAccounts.FirstOrDefaultAsync(x => x.Id == request.BankAccountId.Value, cancellationToken)
                   ?? throw CashAccess.Invalid("bankAccountId", "الحساب البنكي غير موجود.");

            if (!bank.IsActive || bank.CurrencyId != settlement.CurrencyId)
            {
                throw CashAccess.Invalid("bankAccountId", "الحساب البنكي غير مفعّل أو بعملة أخرى.");
            }

            if (reference is null)
            {
                throw CashAccess.Invalid("referenceNumber", "رقم الحوالة البنكية مطلوب.");
            }
        }

        var now = UtcNow;
        var paidAt = request.PaidAtUtc ?? now;
        if (paidAt > now.AddMinutes(5) || paidAt < settlement.SettlementDateUtc.AddMinutes(-5))
        {
            throw CashAccess.Invalid("paidAtUtc", "تاريخ الدفع يجب أن يكون بين تاريخ التسوية والآن.");
        }

        await EnsureLinesStillValidAsync(settlement, cancellationToken);

        var codIds = settlement.Lines.Select(x => x.CODTransactionId).ToList();
        var cods = await Db.CODTransactions.Include(x => x.Shipment).Where(x => codIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var senderName = await Db.Customers.AsNoTracking().Where(x => x.Id == settlement.SenderCustomerId).Select(x => x.ArabicName).FirstAsync(cancellationToken);

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            if (cashbox is not null)
            {
                var voucher = new CashPaymentVoucher
                {
                    VoucherNumber = await _numbers.NextAsync(DocumentPrefixes.CashPayment, ct),
                    CashboxId = cashbox.Id,
                    BranchId = cashbox.BranchId,
                    CurrencyId = cashbox.CurrencyId,
                    PaymentType = CashPaymentType.SenderSettlement,
                    Status = CashVoucherStatus.Posted,
                    VoucherDateUtc = paidAt,
                    Amount = settlement.NetPayable,
                    PaidTo = Clip(senderName, 150),
                    Description = $"دفع تسوية مبالغ التحصيل {settlement.SettlementNumber} ({settlement.Lines.Count} شحنة)",
                    ReferenceNumber = reference,
                    CustomerId = settlement.SenderCustomerId,
                    SenderSettlementId = settlement.Id
                };
                Db.CashPaymentVouchers.Add(voucher);
                await Db.SaveChangesAsync(ct);

                await _ledger.PostCashboxAsync(cashbox, CashboxTransactionType.SenderSettlementPayment, 0m, settlement.NetPayable,
                    CashLedger.PaymentDocument, voucher.Id, voucher.VoucherNumber, voucher.Description, senderName, ct);

                settlement.CashPaymentVoucherId = voucher.Id;
                settlement.CashboxId = cashbox.Id;
            }
            else
            {
                settlement.BankAccountId = bank!.Id;
            }

            var codReference = new CodReference(CashLedger.SettlementDocument, settlement.Id, settlement.SettlementNumber, CashboxId: cashbox?.Id);
            foreach (var line in settlement.Lines)
            {
                var cod = cods[line.CODTransactionId];
                cod.SettledAmount += line.Amount;
                if (line.RecognizesDeductions)
                {
                    cod.DeductionsRecognized = true;
                }

                var fullySettled = cod.SettledAmount == cod.NetPayableAmount;
                if (fullySettled)
                {
                    cod.SettledAtUtc = paidAt;
                }

                _ledger.MoveCod(cod, fullySettled ? CODStatus.SettledToSender : CODStatus.PartiallySettled, CODTransactionType.SettlementPaid, line.Amount, codReference);
                line.IsOpen = false;
            }

            settlement.Status = SettlementStatus.Paid;
            settlement.PaymentMethodId = method.Id;
            settlement.PaymentReference = reference;
            settlement.PaidAtUtc = paidAt;
            settlement.PaidByUserId = _dependencies.CurrentUser.UserId;

            _events.Publish(new SenderSettlementPaidEvent
            {
                SettlementId = settlement.Id,
                SettlementNumber = settlement.SettlementNumber,
                SenderCustomerId = settlement.SenderCustomerId,
                BranchId = settlement.BranchId,
                CurrencyId = settlement.CurrencyId,
                PaymentMethodKind = method.Kind.ToString(),
                CashboxId = settlement.CashboxId,
                BankAccountId = settlement.BankAccountId,
                CashPaymentVoucherId = settlement.CashPaymentVoucherId,
                PaymentReference = reference,
                TotalGrossReleased = settlement.TotalGrossReleased,
                TotalShippingCharges = settlement.TotalShippingCharges,
                TotalCodCommission = settlement.TotalCodCommission,
                TotalTax = settlement.TotalTax,
                TotalOtherFees = settlement.TotalOtherFees,
                TotalCompensation = settlement.TotalCompensation,
                NetPayable = settlement.NetPayable,
                PaidAtUtc = paidAt,
                Lines = settlement.Lines.Select(x => new SettlementEventLine(x.CODTransactionId, x.ShipmentId, x.Amount, x.GrossReleased, x.RecognizesDeductions)).ToList(),
                Posting = Posting(settlement, paidAt)
            });
            Audit(AuditAction.SenderSettlementPaid, settlement.Id, new { settlement.SettlementNumber, settlement.NetPayable, method = method.Kind, settlement.CashboxId, settlement.BankAccountId, reference });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ cancel / reverse

    public async Task<SenderSettlementDto> CancelAsync(long id, SettlementReasonRequest request, CancellationToken cancellationToken)
    {
        var settlement = await FindAsync(id, cancellationToken);

        if (settlement.Status is not (SettlementStatus.Draft or SettlementStatus.Approved))
        {
            throw new BusinessRuleException(settlement.Status == SettlementStatus.Paid
                ? "التسوية مدفوعة: استخدم عكس التسوية بدلاً من الإلغاء."
                : $"التسوية في حالة «{CodNames.Arabic(settlement.Status)}».");
        }

        if (settlement.Status == SettlementStatus.Approved)
        {
            _access.EnsurePermission(Permissions.Cod.ApproveSettlement, "إلغاء تسوية معتمدة يحتاج صلاحية COD.ApproveSettlement.");
        }

        foreach (var line in settlement.Lines)
        {
            line.IsOpen = false;
        }

        settlement.Status = SettlementStatus.Cancelled;
        settlement.CancelledAtUtc = UtcNow;
        settlement.CancellationReason = request.Reason.Trim();

        Audit(AuditAction.SenderSettlementCancelled, settlement.Id, new { settlement.SettlementNumber, reason = settlement.CancellationReason });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<SenderSettlementDto> ReverseAsync(long id, SettlementReasonRequest request, CancellationToken cancellationToken)
    {
        var settlement = await FindAsync(id, cancellationToken);

        if (settlement.Status != SettlementStatus.Paid)
        {
            throw new BusinessRuleException("يمكن عكس التسويات المدفوعة فقط.");
        }

        var codIds = settlement.Lines.Select(x => x.CODTransactionId).ToList();
        var later = await Db.SenderSettlementLines.AsNoTracking()
            .Where(x => codIds.Contains(x.CODTransactionId) &&
                        x.SenderSettlementId != settlement.Id &&
                        (x.IsOpen || (x.SenderSettlement.Status == SettlementStatus.Paid && x.SenderSettlement.PaidAtUtc > settlement.PaidAtUtc)))
            .Select(x => x.SenderSettlement.SettlementNumber)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (later.Count > 0)
        {
            throw new BusinessRuleException($"لا يمكن عكس التسوية: شحناتها مشمولة بتسويات لاحقة ({string.Join("، ", later)}). اعكس/ألغِ التسويات اللاحقة أولاً.");
        }

        var cods = await Db.CODTransactions.Where(x => codIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (cods.Values.Any(x => x.Status == CODStatus.Disputed))
        {
            throw new BusinessRuleException("إحدى شحنات التسوية في نزاع؛ احسم النزاع أولاً.");
        }

        var reason = request.Reason.Trim();
        CashPaymentVoucher? voucher = settlement.CashPaymentVoucherId.HasValue
            ? await Db.CashPaymentVouchers.FirstAsync(x => x.Id == settlement.CashPaymentVoucherId.Value, cancellationToken)
            : null;
        var cashbox = voucher is not null ? await Db.Cashboxes.FirstAsync(x => x.Id == voucher.CashboxId, cancellationToken) : null;

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var now = UtcNow;

            if (voucher is not null && cashbox is not null)
            {
                await _ledger.ReverseDocumentAsync(cashbox, CashLedger.PaymentDocument, voucher.Id, $"عكس التسوية {settlement.SettlementNumber}: {reason}", ct);
                voucher.Status = CashVoucherStatus.Cancelled;
                voucher.CancelledAtUtc = now;
                voucher.CancelledByUserId = _dependencies.CurrentUser.UserId;
                voucher.CancellationReason = $"عكس التسوية {settlement.SettlementNumber}: {reason}";
            }

            var codReference = new CodReference(CashLedger.SettlementDocument, settlement.Id, settlement.SettlementNumber, CashboxId: cashbox?.Id);
            foreach (var line in settlement.Lines)
            {
                var cod = cods[line.CODTransactionId];
                cod.SettledAmount -= line.Amount;
                if (line.RecognizesDeductions)
                {
                    cod.DeductionsRecognized = false;
                }

                cod.SettledAtUtc = null;
                _ledger.MoveCod(cod, cod.SettledAmount == 0m ? CODStatus.ReadyForSenderSettlement : CODStatus.PartiallySettled,
                    CODTransactionType.SettlementReversed, line.Amount, codReference, reason);
            }

            settlement.Status = SettlementStatus.Reversed;
            settlement.ReversedAtUtc = now;
            settlement.ReversedByUserId = _dependencies.CurrentUser.UserId;
            settlement.ReversalReason = reason;

            _events.Publish(new SenderSettlementReversedEvent
            {
                SettlementId = settlement.Id,
                SettlementNumber = settlement.SettlementNumber,
                SenderCustomerId = settlement.SenderCustomerId,
                CurrencyId = settlement.CurrencyId,
                NetPayable = settlement.NetPayable,
                Reason = reason,
                Posting = Posting(settlement, settlement.PaidAtUtc ?? now).Reverse($"عكس التسوية {settlement.SettlementNumber}", now)
            });
            Audit(AuditAction.SenderSettlementReversed, settlement.Id, new { settlement.SettlementNumber, settlement.NetPayable, reason });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<FileDownload> GetPdfAsync(long id, CancellationToken cancellationToken)
    {
        var settlement = await GetByIdAsync(id, cancellationToken);
        var company = await CashDocumentPdfRenderer.LoadCompanyAsync(Db, cancellationToken);
        return FileDownload.FromBytes(_pdf.RenderSettlement(settlement, company), "application/pdf", $"{settlement.SettlementNumber}.pdf");
    }

    // ------------------------------------------------------------------ helpers

    private async Task<SenderSettlement> FindAsync(long id, CancellationToken cancellationToken) =>
        await _access.Settlements(tracked: true).Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("التسوية", id);

    /// <summary>The reserved CODs must still be settleable with the same remaining amount as when the line was made.</summary>
    private async Task EnsureLinesStillValidAsync(SenderSettlement settlement, CancellationToken cancellationToken)
    {
        var ids = settlement.Lines.Select(x => x.CODTransactionId).ToList();
        var cods = await Db.CODTransactions.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Status, x.NetPayableAmount, x.SettledAmount, x.Shipment.ShipmentNumber })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var changed = settlement.Lines
            .Where(line => !CodStatusRules.IsSettleable(cods[line.CODTransactionId].Status) ||
                           cods[line.CODTransactionId].NetPayableAmount - cods[line.CODTransactionId].SettledAmount != line.RemainingBefore)
            .Select(line => cods[line.CODTransactionId].ShipmentNumber)
            .ToList();

        if (changed.Count > 0)
        {
            throw new ConflictException($"تغيّرت مبالغ أو حالات بعض الشحنات بعد إنشاء التسوية ({string.Join("، ", changed)}). ألغِ التسوية وأنشئ تسوية جديدة.");
        }
    }

    private static AccountingPostingRequest Posting(SenderSettlement settlement, DateTime dateUtc) =>
        CashPostings.SettlementPaid(
            settlement.Id, settlement.SettlementNumber, settlement.CurrencyId, dateUtc, settlement.BranchId, settlement.SenderCustomerId,
            settlement.TotalGrossReleased, settlement.TotalShippingCharges, settlement.TotalCodCommission, settlement.TotalTax,
            settlement.TotalOtherFees, settlement.TotalCompensation, settlement.NetPayable, settlement.CashboxId, settlement.BankAccountId);

    private void Audit(AuditAction action, long id, object details) =>
        _dependencies.AuditLog.Add(action, entityName: EntityName, entityId: id.ToString(CultureInfo.InvariantCulture), details: details);

    private static string Clip(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    // ------------------------------------------------------------------ projections

    public static readonly Expression<Func<SenderSettlement, SenderSettlementListItemDto>> ListProjection = x => new SenderSettlementListItemDto
    {
        Id = x.Id,
        SettlementNumber = x.SettlementNumber,
        SenderCustomerId = x.SenderCustomerId,
        SenderName = x.SenderCustomer.ArabicName,
        BranchName = x.Branch.ArabicName,
        CurrencyCode = x.Currency.Code,
        Status = x.Status,
        SettlementDateUtc = x.SettlementDateUtc,
        LineCount = x.Lines.Count,
        TotalGrossReleased = x.TotalGrossReleased,
        TotalDeductions = x.TotalShippingCharges + x.TotalCodCommission + x.TotalTax + x.TotalOtherFees,
        NetPayable = x.NetPayable,
        PaidAtUtc = x.PaidAtUtc
    };

    private static readonly Expression<Func<SenderSettlement, SenderSettlementDto>> DetailProjection = x => new SenderSettlementDto
    {
        Id = x.Id,
        SettlementNumber = x.SettlementNumber,
        SenderCustomerId = x.SenderCustomerId,
        SenderCode = x.SenderCustomer.Code,
        SenderName = x.SenderCustomer.ArabicName,
        SenderPhone = x.SenderCustomer.Phone,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        CurrencyName = x.Currency.ArabicName,
        CurrencyDecimals = x.Currency.DecimalPlaces,
        Status = x.Status,
        SettlementDateUtc = x.SettlementDateUtc,
        TotalGrossReleased = x.TotalGrossReleased,
        TotalShippingCharges = x.TotalShippingCharges,
        TotalCodCommission = x.TotalCodCommission,
        TotalTax = x.TotalTax,
        TotalOtherFees = x.TotalOtherFees,
        TotalCompensation = x.TotalCompensation,
        NetPayable = x.NetPayable,
        Notes = x.Notes,
        CreatedByUserId = x.CreatedByUserId,
        ApprovedByUserId = x.ApprovedByUserId,
        ApprovedAtUtc = x.ApprovedAtUtc,
        PaymentMethodId = x.PaymentMethodId,
        PaymentMethodName = x.PaymentMethod != null ? x.PaymentMethod.ArabicName : null,
        CashboxId = x.CashboxId,
        CashboxName = x.Cashbox != null ? x.Cashbox.ArabicName : null,
        BankAccountId = x.BankAccountId,
        BankAccountName = x.BankAccount != null ? x.BankAccount.ArabicName : null,
        PaymentReference = x.PaymentReference,
        CashPaymentVoucherId = x.CashPaymentVoucherId,
        CashPaymentVoucherNumber = x.CashPaymentVoucher != null ? x.CashPaymentVoucher.VoucherNumber : null,
        PaidAtUtc = x.PaidAtUtc,
        PaidByUserId = x.PaidByUserId,
        CancelledAtUtc = x.CancelledAtUtc,
        CancellationReason = x.CancellationReason,
        ReversedAtUtc = x.ReversedAtUtc,
        ReversalReason = x.ReversalReason,
        Lines = x.Lines
            .OrderBy(l => l.Id)
            .Select(l => new SenderSettlementLineDto
            {
                Id = l.Id,
                CodTransactionId = l.CODTransactionId,
                ShipmentId = l.ShipmentId,
                ShipmentNumber = l.CODTransaction.Shipment.ShipmentNumber,
                TrackingNumber = l.CODTransaction.Shipment.TrackingNumber,
                RecipientName = l.CODTransaction.Shipment.RecipientCustomer.ArabicName,
                DeliveredAtUtc = l.CODTransaction.Shipment.DeliveredAtUtc,
                CollectedAmount = l.CollectedAmount,
                RemainingBefore = l.RemainingBefore,
                ShippingCharge = l.ShippingCharge,
                CodCommission = l.CodCommission,
                Tax = l.Tax,
                OtherFees = l.OtherFees,
                Compensation = l.Compensation,
                RecognizesDeductions = l.RecognizesDeductions,
                Amount = l.Amount,
                GrossReleased = l.GrossReleased
            })
            .ToList()
    };

    public static SenderSettlementListItemDto WithName(SenderSettlementListItemDto x) => new()
    {
        Id = x.Id,
        SettlementNumber = x.SettlementNumber,
        SenderCustomerId = x.SenderCustomerId,
        SenderName = x.SenderName,
        BranchName = x.BranchName,
        CurrencyCode = x.CurrencyCode,
        Status = x.Status,
        StatusName = CodNames.Arabic(x.Status),
        SettlementDateUtc = x.SettlementDateUtc,
        LineCount = x.LineCount,
        TotalGrossReleased = x.TotalGrossReleased,
        TotalDeductions = x.TotalDeductions,
        NetPayable = x.NetPayable,
        PaidAtUtc = x.PaidAtUtc
    };

    private static SenderSettlementDto WithName(SenderSettlementDto x) => new()
    {
        Id = x.Id,
        SettlementNumber = x.SettlementNumber,
        SenderCustomerId = x.SenderCustomerId,
        SenderCode = x.SenderCode,
        SenderName = x.SenderName,
        SenderPhone = x.SenderPhone,
        BranchId = x.BranchId,
        BranchName = x.BranchName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.CurrencyCode,
        CurrencyName = x.CurrencyName,
        CurrencyDecimals = x.CurrencyDecimals,
        Status = x.Status,
        StatusName = CodNames.Arabic(x.Status),
        SettlementDateUtc = x.SettlementDateUtc,
        TotalGrossReleased = x.TotalGrossReleased,
        TotalShippingCharges = x.TotalShippingCharges,
        TotalCodCommission = x.TotalCodCommission,
        TotalTax = x.TotalTax,
        TotalOtherFees = x.TotalOtherFees,
        TotalCompensation = x.TotalCompensation,
        NetPayable = x.NetPayable,
        Notes = x.Notes,
        CreatedByUserId = x.CreatedByUserId,
        ApprovedByUserId = x.ApprovedByUserId,
        ApprovedAtUtc = x.ApprovedAtUtc,
        PaymentMethodId = x.PaymentMethodId,
        PaymentMethodName = x.PaymentMethodName,
        CashboxId = x.CashboxId,
        CashboxName = x.CashboxName,
        BankAccountId = x.BankAccountId,
        BankAccountName = x.BankAccountName,
        PaymentReference = x.PaymentReference,
        CashPaymentVoucherId = x.CashPaymentVoucherId,
        CashPaymentVoucherNumber = x.CashPaymentVoucherNumber,
        PaidAtUtc = x.PaidAtUtc,
        PaidByUserId = x.PaidByUserId,
        CancelledAtUtc = x.CancelledAtUtc,
        CancellationReason = x.CancellationReason,
        ReversedAtUtc = x.ReversedAtUtc,
        ReversalReason = x.ReversalReason,
        Lines = x.Lines
    };
}
