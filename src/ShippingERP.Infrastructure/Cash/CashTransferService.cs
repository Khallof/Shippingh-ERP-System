using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Cash;

/// <summary>
/// Cash transfers between cashboxes, between branches and from a cashbox to the bank (step 6).
/// Requested -> approved by ANOTHER user (money leaves the source) -> received by the destination branch when it
/// is another branch. Same currency only (no exchange in this phase).
/// </summary>
public sealed class CashTransferService : ICashTransferService
{
    private static readonly Dictionary<string, Expression<Func<CashTransfer, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["createdAtUtc"] = x => x.CreatedAtUtc,
        ["amount"] = x => x.Amount,
        ["transferNumber"] = x => x.TransferNumber
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly CashAccess _access;
    private readonly CashLedger _ledger;
    private readonly DocumentNumberGenerator _numbers;
    private readonly IIntegrationEventPublisher _events;

    public CashTransferService(
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

    public async Task<PagedResult<CashTransferDto>> GetPagedAsync(CashTransferListQuery query, CancellationToken cancellationToken)
    {
        var rows = _access.Transfers();

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.TransferType.HasValue)
        {
            var type = query.TransferType.Value;
            rows = rows.Where(x => x.TransferType == type);
        }

        if (query.CashboxId.HasValue)
        {
            var cashboxId = query.CashboxId.Value;
            rows = rows.Where(x => x.FromCashboxId == cashboxId || x.ToCashboxId == cashboxId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.FromBranchId == branchId || x.ToBranchId == branchId);
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
            rows = rows.Where(x => x.TransferNumber.Contains(search) || x.Reason.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<CashTransfer> ordered = !string.IsNullOrWhiteSpace(query.SortBy) && Sorts.TryGetValue(query.SortBy, out var sort)
            ? (query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort))
            : rows.OrderByDescending(x => x.CreatedAtUtc);

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<CashTransferDto>(items.Select(WithNames).ToList(), query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<CashTransferDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await _access.Transfers().Where(x => x.Id == id).Select(Projection).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("التحويل", id);
        return WithNames(dto);
    }

    public async Task<CashTransferDto> CreateAsync(CreateCashTransferRequest request, CancellationToken cancellationToken)
    {
        var from = await _access.CashboxForPostingAsync(request.FromCashboxId, null, "fromCashboxId", cancellationToken);
        var amount = BillingAccess.Round(request.Amount, from.Currency.DecimalPlaces);

        if (amount > from.CurrentBalance)
        {
            throw new BusinessRuleException($"رصيد الصندوق {from.Code} ({from.CurrentBalance}) لا يكفي للتحويل ({amount}).");
        }

        var transfer = new CashTransfer
        {
            FromCashboxId = from.Id,
            FromBranchId = from.BranchId,
            CurrencyId = from.CurrencyId,
            Amount = amount,
            Reason = request.Reason.Trim(),
            BankReference = string.IsNullOrWhiteSpace(request.BankReference) ? null : request.BankReference.Trim(),
            Status = CashTransferStatus.PendingApproval
        };

        if (request.ToCashboxId.HasValue)
        {
            var to = await Db.Cashboxes.AsNoTracking()
                         .Where(x => x.Id == request.ToCashboxId.Value)
                         .Select(x => new { x.Id, x.Code, x.BranchId, x.CurrencyId, x.IsActive })
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? throw CashAccess.Invalid("toCashboxId", "صندوق الوجهة غير موجود.");

            if (!to.IsActive)
            {
                throw CashAccess.Invalid("toCashboxId", $"صندوق الوجهة {to.Code} غير مفعّل.");
            }

            if (to.CurrencyId != from.CurrencyId)
            {
                throw CashAccess.Invalid("toCashboxId", "التحويل بين عملتين مختلفتين غير مدعوم في هذه المرحلة.");
            }

            transfer.ToCashboxId = to.Id;
            transfer.ToBranchId = to.BranchId;
            transfer.TransferType = to.BranchId == from.BranchId ? CashTransferType.BetweenCashboxes : CashTransferType.BetweenBranches;
        }
        else
        {
            var bank = await Db.BankAccounts.AsNoTracking()
                           .Where(x => x.Id == request.ToBankAccountId!.Value)
                           .Select(x => new { x.Id, x.Code, x.CurrencyId, x.IsActive })
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw CashAccess.Invalid("toBankAccountId", "الحساب البنكي غير موجود.");

            if (!bank.IsActive)
            {
                throw CashAccess.Invalid("toBankAccountId", $"الحساب البنكي {bank.Code} غير مفعّل.");
            }

            if (bank.CurrencyId != from.CurrencyId)
            {
                throw CashAccess.Invalid("toBankAccountId", "عملة الحساب البنكي تختلف عن عملة الصندوق.");
            }

            transfer.ToBankAccountId = bank.Id;
            transfer.TransferType = CashTransferType.CashboxToBank;
        }

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            transfer.TransferNumber = await _numbers.NextAsync(DocumentPrefixes.CashTransfer, ct);
            Db.CashTransfers.Add(transfer);
            await Db.SaveChangesAsync(ct);

            Audit(AuditAction.CashTransferRequested, transfer.Id, new { transfer.TransferNumber, transfer.TransferType, from = from.Code, transfer.ToCashboxId, transfer.ToBankAccountId, amount });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(transfer.Id, cancellationToken);
    }

    public async Task<CashTransferDto> ApproveAsync(long id, ApproveCashTransferRequest request, CancellationToken cancellationToken)
    {
        var transfer = await FindAsync(id, cancellationToken);
        _access.BranchAccess.EnsureCanAccess(transfer.FromBranchId);
        EnsurePending(transfer);
        _access.EnsureOtherApprover(transfer.CreatedByUserId, "التحويل");

        if (transfer.TransferType == CashTransferType.CashboxToBank)
        {
            var reference = string.IsNullOrWhiteSpace(request.BankReference) ? transfer.BankReference : request.BankReference.Trim();
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw CashAccess.Invalid("bankReference", "رقم إشعار الإيداع البنكي مطلوب لاعتماد الإيداع.");
            }

            transfer.BankReference = reference;
        }

        var from = await Db.Cashboxes.FirstAsync(x => x.Id == transfer.FromCashboxId, cancellationToken);
        var to = transfer.ToCashboxId.HasValue ? await Db.Cashboxes.FirstAsync(x => x.Id == transfer.ToCashboxId.Value, cancellationToken) : null;

        if (to is not null && !to.IsActive)
        {
            throw new BusinessRuleException($"صندوق الوجهة {to.Code} غير مفعّل.");
        }

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var now = UtcNow;
            var description = DescribeOut(transfer, to);
            await _ledger.PostCashboxAsync(from, CashboxTransactionType.TransferOut, 0m, transfer.Amount,
                CashLedger.TransferDocument, transfer.Id, transfer.TransferNumber, description, to?.ArabicName, ct);

            transfer.SentAtUtc = now;
            transfer.DecidedByUserId = _dependencies.CurrentUser.UserId;
            transfer.DecidedAtUtc = now;
            transfer.DecisionNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            if (transfer.TransferType == CashTransferType.BetweenCashboxes)
            {
                await _ledger.PostCashboxAsync(to!, CashboxTransactionType.TransferIn, transfer.Amount, 0m,
                    CashLedger.TransferDocument, transfer.Id, transfer.TransferNumber, $"تحويل وارد من الصندوق {from.Code}", from.ArabicName, ct);
                transfer.Status = CashTransferStatus.Completed;
                transfer.ReceivedAtUtc = now;
                transfer.ReceivedByUserId = _dependencies.CurrentUser.UserId;
            }
            else
            {
                transfer.Status = transfer.TransferType == CashTransferType.BetweenBranches ? CashTransferStatus.InTransit : CashTransferStatus.Completed;
            }

            _events.Publish(new CashTransferApprovedEvent
            {
                TransferId = transfer.Id,
                TransferNumber = transfer.TransferNumber,
                TransferType = transfer.TransferType.ToString(),
                FromCashboxId = transfer.FromCashboxId,
                ToCashboxId = transfer.ToCashboxId,
                ToBankAccountId = transfer.ToBankAccountId,
                CurrencyId = transfer.CurrencyId,
                Amount = transfer.Amount,
                Completed = transfer.Status == CashTransferStatus.Completed,
                Posting = CashPostings.TransferApproved(transfer.Id, transfer.TransferNumber, transfer.TransferType, transfer.CurrencyId, now,
                    transfer.Amount, transfer.FromBranchId, transfer.FromCashboxId, transfer.ToBranchId, transfer.ToCashboxId, transfer.ToBankAccountId)
            });
            Audit(AuditAction.CashTransferApproved, transfer.Id, new { transfer.TransferNumber, transfer.Status, transfer.Amount, transfer.BankReference });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<CashTransferDto> RejectAsync(long id, CashTransferReasonRequest request, CancellationToken cancellationToken)
    {
        var transfer = await FindAsync(id, cancellationToken);
        _access.BranchAccess.EnsureCanAccess(transfer.FromBranchId);
        EnsurePending(transfer);
        _access.EnsureOtherApprover(transfer.CreatedByUserId, "رفض التحويل (ألغِه بدلاً من ذلك)");

        transfer.Status = CashTransferStatus.Rejected;
        transfer.DecidedByUserId = _dependencies.CurrentUser.UserId;
        transfer.DecidedAtUtc = UtcNow;
        transfer.DecisionNotes = request.Reason.Trim();

        Audit(AuditAction.CashTransferRejected, transfer.Id, new { transfer.TransferNumber, reason = transfer.DecisionNotes });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<CashTransferDto> CancelAsync(long id, CashTransferReasonRequest request, CancellationToken cancellationToken)
    {
        var transfer = await FindAsync(id, cancellationToken);
        EnsurePending(transfer);

        var user = _dependencies.CurrentUser;
        if (user.IsAuthenticated && transfer.CreatedByUserId != user.UserId && !user.HasPermission(Permissions.CashTransfers.Approve))
        {
            throw new ForbiddenException("يلغي التحويل مقدّم الطلب أو من يملك صلاحية اعتماد التحويلات.");
        }

        transfer.Status = CashTransferStatus.Cancelled;
        transfer.DecidedByUserId = user.UserId;
        transfer.DecidedAtUtc = UtcNow;
        transfer.DecisionNotes = request.Reason.Trim();

        Audit(AuditAction.CashTransferCancelled, transfer.Id, new { transfer.TransferNumber, reason = transfer.DecisionNotes });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<CashTransferDto> ReceiveAsync(long id, CancellationToken cancellationToken)
    {
        var transfer = await FindAsync(id, cancellationToken);

        if (transfer.Status != CashTransferStatus.InTransit)
        {
            throw new BusinessRuleException($"التحويل في حالة «{CodNames.Arabic(transfer.Status)}»؛ الاستلام للتحويلات قيد النقل فقط.");
        }

        _access.BranchAccess.EnsureCanAccess(transfer.ToBranchId);
        var to = await Db.Cashboxes.FirstAsync(x => x.Id == transfer.ToCashboxId!.Value, cancellationToken);
        var fromCode = await Db.Cashboxes.AsNoTracking().Where(x => x.Id == transfer.FromCashboxId).Select(x => x.Code).FirstAsync(cancellationToken);

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var now = UtcNow;
            await _ledger.PostCashboxAsync(to, CashboxTransactionType.TransferIn, transfer.Amount, 0m,
                CashLedger.TransferDocument, transfer.Id, transfer.TransferNumber, $"تحويل وارد من الصندوق {fromCode}", fromCode, ct);

            transfer.Status = CashTransferStatus.Completed;
            transfer.ReceivedAtUtc = now;
            transfer.ReceivedByUserId = _dependencies.CurrentUser.UserId;

            _events.Publish(new CashTransferReceivedEvent
            {
                TransferId = transfer.Id,
                TransferNumber = transfer.TransferNumber,
                ToCashboxId = to.Id,
                CurrencyId = transfer.CurrencyId,
                Amount = transfer.Amount,
                Posting = CashPostings.TransferReceived(transfer.Id, transfer.TransferNumber, transfer.CurrencyId, now, transfer.Amount,
                    transfer.FromBranchId, to.BranchId, to.Id)
            });
            Audit(AuditAction.CashTransferReceived, transfer.Id, new { transfer.TransferNumber, transfer.Amount, to = to.Code });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<CashTransfer> FindAsync(long id, CancellationToken cancellationToken) =>
        await _access.Transfers(tracked: true).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("التحويل", id);

    private static void EnsurePending(CashTransfer transfer)
    {
        if (transfer.Status != CashTransferStatus.PendingApproval)
        {
            throw new BusinessRuleException($"التحويل {transfer.TransferNumber} في حالة «{CodNames.Arabic(transfer.Status)}» ولم يعد بانتظار الموافقة.");
        }
    }

    private static string DescribeOut(CashTransfer transfer, Cashbox? to) => transfer.TransferType switch
    {
        CashTransferType.CashboxToBank => $"إيداع في البنك - إشعار {transfer.BankReference}",
        CashTransferType.BetweenBranches => $"تحويل صادر إلى صندوق {to?.Code} (فرع آخر - قيد النقل)",
        _ => $"تحويل صادر إلى الصندوق {to?.Code}"
    };

    private void Audit(AuditAction action, long id, object details) =>
        _dependencies.AuditLog.Add(action, entityName: CashLedger.TransferDocument, entityId: id.ToString(CultureInfo.InvariantCulture), details: details);

    private static readonly Expression<Func<CashTransfer, CashTransferDto>> Projection = x => new CashTransferDto
    {
        Id = x.Id,
        TransferNumber = x.TransferNumber,
        TransferType = x.TransferType,
        Status = x.Status,
        FromCashboxId = x.FromCashboxId,
        FromCashboxName = x.FromCashbox.ArabicName,
        FromBranchId = x.FromBranchId,
        FromBranchName = x.FromCashbox.Branch.ArabicName,
        ToCashboxId = x.ToCashboxId,
        ToCashboxName = x.ToCashbox != null ? x.ToCashbox.ArabicName : null,
        ToBranchId = x.ToBranchId,
        ToBranchName = x.ToCashbox != null ? x.ToCashbox.Branch.ArabicName : null,
        ToBankAccountId = x.ToBankAccountId,
        ToBankAccountName = x.ToBankAccount != null ? x.ToBankAccount.ArabicName : null,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        Amount = x.Amount,
        Reason = x.Reason,
        BankReference = x.BankReference,
        RequestedByUserId = x.CreatedByUserId,
        CreatedAtUtc = x.CreatedAtUtc,
        DecidedByUserId = x.DecidedByUserId,
        DecidedAtUtc = x.DecidedAtUtc,
        DecisionNotes = x.DecisionNotes,
        SentAtUtc = x.SentAtUtc,
        ReceivedAtUtc = x.ReceivedAtUtc,
        ReceivedByUserId = x.ReceivedByUserId
    };

    private static CashTransferDto WithNames(CashTransferDto x) => new()
    {
        Id = x.Id,
        TransferNumber = x.TransferNumber,
        TransferType = x.TransferType,
        TransferTypeName = CodNames.Arabic(x.TransferType),
        Status = x.Status,
        StatusName = CodNames.Arabic(x.Status),
        FromCashboxId = x.FromCashboxId,
        FromCashboxName = x.FromCashboxName,
        FromBranchId = x.FromBranchId,
        FromBranchName = x.FromBranchName,
        ToCashboxId = x.ToCashboxId,
        ToCashboxName = x.ToCashboxName,
        ToBranchId = x.ToBranchId,
        ToBranchName = x.ToBranchName,
        ToBankAccountId = x.ToBankAccountId,
        ToBankAccountName = x.ToBankAccountName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.CurrencyCode,
        Amount = x.Amount,
        Reason = x.Reason,
        BankReference = x.BankReference,
        RequestedByUserId = x.RequestedByUserId,
        CreatedAtUtc = x.CreatedAtUtc,
        DecidedByUserId = x.DecidedByUserId,
        DecidedAtUtc = x.DecidedAtUtc,
        DecisionNotes = x.DecisionNotes,
        SentAtUtc = x.SentAtUtc,
        ReceivedAtUtc = x.ReceivedAtUtc,
        ReceivedByUserId = x.ReceivedByUserId
    };
}
