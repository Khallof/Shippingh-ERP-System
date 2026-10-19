using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>
/// Return to sender.
/// 1. Create (Open): records the reason and the return branch; Shipment.ReturnBranchId is set, which blocks
///    delivery to the recipient and makes the return branch the "destination" for branch transfers.
/// 2. AfterDelivery only: Receive -> the delivered shipment is back at a branch (Delivered -> ReceivedAtBranch).
/// 3. Move the shipment to the return branch with normal branch transfers (if needed).
/// 4. Complete: the shipment is at the return branch and handed to the sender -> Returned.
/// Cancel: the return is abandoned; the shipment can be delivered again.
/// </summary>
public sealed class ShipmentReturnService : IShipmentReturnService
{
    private const string ReferenceType = "ShipmentReturn";

    private static readonly Expression<Func<ShipmentReturn, ShipmentReturnDto>> ToDto = x => new ShipmentReturnDto
    {
        Id = x.Id,
        ReturnNumber = x.ReturnNumber,
        ShipmentId = x.ShipmentId,
        ShipmentNumber = x.Shipment.ShipmentNumber,
        TrackingNumber = x.Shipment.TrackingNumber,
        ShipmentStatus = x.Shipment.Status,
        ShipmentStatusName = ShipmentStatusNames.Arabic(x.Shipment.Status),
        ShipmentCurrentBranchId = x.Shipment.CurrentBranchId,
        ReturnType = x.ReturnType,
        Status = x.Status,
        Reason = x.Reason,
        ReturnBranchId = x.ReturnBranchId,
        ReturnBranchName = x.ReturnBranch.ArabicName,
        ReturnCharge = x.ReturnCharge,
        ReceivedBackAtUtc = x.ReceivedBackAtUtc,
        CompletedAtUtc = x.CompletedAtUtc,
        CancelledAtUtc = x.CancelledAtUtc,
        Notes = x.Notes,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentAccessService _access;
    private readonly ShipmentStatusChanger _statusChanger;
    private readonly DocumentNumberGenerator _numbers;

    public ShipmentReturnService(
        MasterDataServiceDependencies dependencies,
        ShipmentAccessService access,
        ShipmentStatusChanger statusChanger,
        DocumentNumberGenerator numbers)
    {
        _dependencies = dependencies;
        _access = access;
        _statusChanger = statusChanger;
        _numbers = numbers;
    }

    private AppDbContext Db => _dependencies.DbContext;

    public async Task<PagedResult<ShipmentReturnDto>> GetPagedAsync(ShipmentReturnListQuery query, CancellationToken cancellationToken)
    {
        var rows = DocumentQueryHelpers.ApplyDateRange((await ScopedAsync(cancellationToken)).AsNoTracking(), query);

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.ReturnType.HasValue)
        {
            var returnType = query.ReturnType.Value;
            rows = rows.Where(x => x.ReturnType == returnType);
        }

        if (query.ReturnBranchId.HasValue)
        {
            var branchId = query.ReturnBranchId.Value;
            rows = rows.Where(x => x.ReturnBranchId == branchId);
        }

        if (query.ShipmentId.HasValue)
        {
            var shipmentId = query.ShipmentId.Value;
            rows = rows.Where(x => x.ShipmentId == shipmentId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.ReturnNumber.Contains(search) ||
                                   x.Shipment.TrackingNumber == search ||
                                   x.Shipment.ShipmentNumber == search);
        }

        return await DocumentQueryHelpers.ToPagedAsync(rows, query, x => x.ReturnNumber, ToDto, cancellationToken);
    }

    public async Task<ShipmentReturnDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await (await ScopedAsync(cancellationToken)).AsNoTracking()
                   .Where(x => x.Id == id)
                   .Select(ToDto)
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("المرتجع", id);
    }

    public async Task<ShipmentReturnDto> CreateAsync(CreateShipmentReturnRequest request, CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(request.ShipmentId, cancellationToken);

        if (!_access.BranchAccess.CanAccess(shipment.CurrentBranchId) && !_access.BranchAccess.CanAccess(shipment.OriginBranchId))
        {
            throw new ForbiddenException("فتح المرتجع يتم من الفرع الموجودة فيه الشحنة أو من فرع الإنشاء.");
        }

        var shipmentId = shipment.Id;
        if (await Db.ShipmentReturns.AnyAsync(x => x.ShipmentId == shipmentId && x.Status == ShipmentReturnStatus.Open, cancellationToken))
        {
            throw new ConflictException($"يوجد مرتجع مفتوح للشحنة {shipment.ShipmentNumber}.");
        }

        if (request.ReturnType == ShipmentReturnType.AfterDelivery)
        {
            if (shipment.Status != ShipmentStatus.Delivered)
            {
                throw new BusinessRuleException("مرتجع «بعد التسليم» يُفتح للشحنات المسلّمة فقط.");
            }
        }
        else if (ShipmentStatusRules.IsFinal(shipment.Status) || shipment.Status == ShipmentStatus.OutForDelivery)
        {
            throw new BusinessRuleException(
                $"لا يمكن فتح مرتجع والشحنة في حالة «{ShipmentStatusNames.Arabic(shipment.Status)}». " +
                (shipment.Status == ShipmentStatus.OutForDelivery ? "انتظر نتيجة التوصيل أولاً." : string.Empty));
        }

        await ShipmentDocumentGuards.EnsureNotOnOpenDocumentAsync(Db, shipment, cancellationToken);

        var returnBranchId = request.ReturnBranchId ?? shipment.OriginBranchId;
        if (!await Db.Branches.AnyAsync(x => x.Id == returnBranchId && x.IsActive, cancellationToken))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["returnBranchId"] = new[] { "فرع الإرجاع غير موجود أو غير مفعّل." }
            });
        }

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            var shipmentReturn = new ShipmentReturn
            {
                ReturnNumber = await _numbers.NextAsync(DocumentPrefixes.ShipmentReturn, ct),
                Shipment = shipment,
                ReturnType = request.ReturnType,
                Status = ShipmentReturnStatus.Open,
                Reason = request.Reason.Trim(),
                ReturnBranchId = returnBranchId,
                ReturnCharge = request.ReturnCharge,
                Notes = Optional(request.Notes)
            };

            shipment.ReturnBranchId = returnBranchId;
            Db.ShipmentReturns.Add(shipmentReturn);

            await Db.SaveChangesAsync(ct);
            return shipmentReturn.Id;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ShipmentReturnDto> ReceiveBackAsync(long id, ReceiveShipmentReturnRequest request, CancellationToken cancellationToken)
    {
        var shipmentReturn = await FindOpenAsync(id, cancellationToken);
        var shipment = shipmentReturn.Shipment;

        if (shipmentReturn.ReturnType != ShipmentReturnType.AfterDelivery)
        {
            throw new BusinessRuleException("الاستلام من المستلم مطلوب فقط لمرتجعات «بعد التسليم»؛ باقي المرتجعات موجودة في الفروع أصلاً.");
        }

        if (shipmentReturn.ReceivedBackAtUtc.HasValue || shipment.Status != ShipmentStatus.Delivered)
        {
            throw new BusinessRuleException("تم استلام الشحنة من المستلم مسبقاً.");
        }

        var branchId = request.BranchId ?? _access.BranchAccess.CurrentBranchId;
        if (branchId is null)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["branchId"] = new[] { "حدد الفرع الذي استلم الشحنة." }
            });
        }

        _access.BranchAccess.EnsureCanAccess(branchId.Value);
        var receivingBranchId = branchId.Value;
        if (!await Db.Branches.AnyAsync(x => x.Id == receivingBranchId && x.IsActive, cancellationToken))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["branchId"] = new[] { "الفرع غير موجود أو غير مفعّل." }
            });
        }

        var now = _dependencies.DateTimeProvider.UtcNow;
        shipment.CurrentBranchId = receivingBranchId;

        _statusChanger.Change(shipment, ShipmentStatus.ReceivedAtBranch, new StatusChange(ShipmentStatusChangeSource.Return)
        {
            OccurredAtUtc = now,
            Notes = Combine("استرجاع بعد التسليم", request.Notes),
            ReferenceType = ReferenceType,
            ReferenceId = shipmentReturn.Id,
            ReferenceNumber = shipmentReturn.ReturnNumber
        });

        shipmentReturn.ReceivedBackAtUtc = now;
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ShipmentReturnDto> CompleteAsync(long id, CompleteShipmentReturnRequest request, CancellationToken cancellationToken)
    {
        var shipmentReturn = await FindOpenAsync(id, cancellationToken);
        var shipment = shipmentReturn.Shipment;

        if (shipmentReturn.ReturnType == ShipmentReturnType.AfterDelivery && !shipmentReturn.ReceivedBackAtUtc.HasValue)
        {
            throw new BusinessRuleException("استلم الشحنة من المستلم أولاً (receive).");
        }

        if (shipment.CurrentBranchId != shipmentReturn.ReturnBranchId)
        {
            throw new BusinessRuleException("الشحنة ليست في فرع الإرجاع بعد. حوّلها إلى فرع الإرجاع بتحويل بين الفروع أولاً.");
        }

        _access.BranchAccess.EnsureCanAccess(shipmentReturn.ReturnBranchId);
        await ShipmentDocumentGuards.EnsureNotOnOpenDocumentAsync(Db, shipment, cancellationToken);

        var now = _dependencies.DateTimeProvider.UtcNow;

        _statusChanger.Change(shipment, ShipmentStatus.Returned, new StatusChange(ShipmentStatusChangeSource.Return)
        {
            OccurredAtUtc = now,
            Notes = Combine("سُلّمت إلى المرسل", request.Notes),
            ReferenceType = ReferenceType,
            ReferenceId = shipmentReturn.Id,
            ReferenceNumber = shipmentReturn.ReturnNumber
        });

        shipmentReturn.Status = ShipmentReturnStatus.Completed;
        shipmentReturn.CompletedAtUtc = now;
        shipmentReturn.Notes = Combine(shipmentReturn.Notes, request.Notes);

        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ShipmentReturnDto> CancelAsync(long id, CancelShipmentReturnRequest request, CancellationToken cancellationToken)
    {
        var shipmentReturn = await FindOpenAsync(id, cancellationToken);

        shipmentReturn.Status = ShipmentReturnStatus.Cancelled;
        shipmentReturn.CancelledAtUtc = _dependencies.DateTimeProvider.UtcNow;
        shipmentReturn.Notes = Combine(shipmentReturn.Notes, $"سبب الإلغاء: {request.Reason.Trim()}");
        shipmentReturn.Shipment.ReturnBranchId = null;

        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Returns of visible shipments, plus returns that come back to the caller's branch.</summary>
    private async Task<IQueryable<ShipmentReturn>> ScopedAsync(CancellationToken cancellationToken)
    {
        var driverOnly = _access.IsDriverOnly;
        if (_access.BranchAccess.CanAccessAllBranches && !driverOnly)
        {
            return Db.ShipmentReturns;
        }

        var shipments = await _access.ScopeAsync(Db.Shipments, cancellationToken);
        if (driverOnly)
        {
            return Db.ShipmentReturns.Where(x => shipments.Any(s => s.Id == x.ShipmentId));
        }

        var branchId = _access.BranchAccess.CurrentBranchId;
        return Db.ShipmentReturns.Where(x => x.ReturnBranchId == branchId || shipments.Any(s => s.Id == x.ShipmentId));
    }

    private async Task<ShipmentReturn> FindOpenAsync(long id, CancellationToken cancellationToken)
    {
        var shipmentReturn = await (await ScopedAsync(cancellationToken))
                                 .Include(x => x.Shipment)
                                 .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                             ?? throw new NotFoundException("المرتجع", id);

        if (shipmentReturn.Status != ShipmentReturnStatus.Open)
        {
            throw new BusinessRuleException($"المرتجع {shipmentReturn.ReturnNumber} مغلق.");
        }

        return shipmentReturn;
    }

    private static string? Combine(string? first, string? second)
    {
        var parts = new[] { first, second }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToList();
        if (parts.Count == 0)
        {
            return null;
        }

        var text = string.Join(" | ", parts);
        return text.Length > 500 ? text[..500] : text;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
