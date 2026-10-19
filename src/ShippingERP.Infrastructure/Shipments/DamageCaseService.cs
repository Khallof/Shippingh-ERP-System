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
/// Damage / loss cases. One open case per shipment.
/// - Create: optionally marks the shipment LostOrDamaged (allowed from any non-final status, even on the road
///   or with a driver); an open transfer line becomes Missing and an open driver-manifest line becomes Failed.
/// - Resolve as FoundAndRecovered: a LostOrDamaged shipment comes back to ReceivedAtBranch at the given branch.
/// Amounts are stored only; compensation accounting is a later step.
/// </summary>
public sealed class DamageCaseService : IDamageCaseService
{
    private const string ReferenceType = "ShipmentDamageOrLossCase";

    private static readonly Expression<Func<ShipmentDamageOrLossCase, DamageCaseDto>> ToDto = x => new DamageCaseDto
    {
        Id = x.Id,
        CaseNumber = x.CaseNumber,
        ShipmentId = x.ShipmentId,
        ShipmentNumber = x.Shipment.ShipmentNumber,
        TrackingNumber = x.Shipment.TrackingNumber,
        ShipmentStatus = x.Shipment.Status,
        CaseType = x.CaseType,
        Status = x.Status,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        Description = x.Description,
        ShipmentStatusBefore = x.ShipmentStatusBefore,
        ShipmentMarkedLostOrDamaged = x.ShipmentMarkedLostOrDamaged,
        EstimatedLossAmount = x.EstimatedLossAmount,
        Resolution = x.Resolution,
        CompensationAmount = x.CompensationAmount,
        ResolutionNotes = x.ResolutionNotes,
        ResolvedAtUtc = x.ResolvedAtUtc,
        ResolvedByUserId = x.ResolvedByUserId,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentAccessService _access;
    private readonly ShipmentStatusChanger _statusChanger;
    private readonly DocumentNumberGenerator _numbers;

    public DamageCaseService(
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

    public async Task<PagedResult<DamageCaseDto>> GetPagedAsync(DamageCaseListQuery query, CancellationToken cancellationToken)
    {
        var rows = DocumentQueryHelpers.ApplyDateRange((await ScopedAsync(cancellationToken)).AsNoTracking(), query);

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.CaseType.HasValue)
        {
            var caseType = query.CaseType.Value;
            rows = rows.Where(x => x.CaseType == caseType);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId);
        }

        if (query.ShipmentId.HasValue)
        {
            var shipmentId = query.ShipmentId.Value;
            rows = rows.Where(x => x.ShipmentId == shipmentId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.CaseNumber.Contains(search) ||
                                   x.Shipment.TrackingNumber == search ||
                                   x.Shipment.ShipmentNumber == search);
        }

        var page = await DocumentQueryHelpers.ToPagedAsync(rows, query, x => x.CaseNumber, ToDto, cancellationToken);
        return await WithEvidenceCountsAsync(page, cancellationToken);
    }

    public async Task<DamageCaseDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await (await ScopedAsync(cancellationToken)).AsNoTracking()
                      .Where(x => x.Id == id)
                      .Select(ToDto)
                      .FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("حالة التلف أو الفقدان", id);

        var count = await Db.ShipmentAttachments.CountAsync(x => x.DamageCaseId == id, cancellationToken);
        return WithEvidence(dto, count);
    }

    public async Task<DamageCaseDto> CreateAsync(CreateDamageCaseRequest request, CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(request.ShipmentId, cancellationToken);

        var branchId = request.BranchId ?? shipment.CurrentBranchId;
        _access.BranchAccess.EnsureCanAccess(branchId);
        if (!await Db.Branches.AnyAsync(x => x.Id == branchId, cancellationToken))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["branchId"] = new[] { "الفرع غير موجود." }
            });
        }

        if (shipment.Status == ShipmentStatus.Cancelled)
        {
            throw new BusinessRuleException("لا يمكن فتح حالة تلف أو فقدان لشحنة ملغاة.");
        }

        var shipmentId = shipment.Id;
        if (await Db.ShipmentDamageOrLossCases.AnyAsync(
                x => x.ShipmentId == shipmentId && x.Status != DamageCaseStatus.Resolved,
                cancellationToken))
        {
            throw new ConflictException($"يوجد حالة تلف أو فقدان مفتوحة للشحنة {shipment.ShipmentNumber}.");
        }

        // Claims after delivery / return / an existing LostOrDamaged status do not change the shipment status.
        var markShipment = request.MarkShipmentAsLostOrDamaged && !ShipmentStatusRules.IsFinal(shipment.Status);
        var now = _dependencies.DateTimeProvider.UtcNow;

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            var damageCase = new ShipmentDamageOrLossCase
            {
                CaseNumber = await _numbers.NextAsync(DocumentPrefixes.DamageCase, ct),
                Shipment = shipment,
                CaseType = request.CaseType,
                Status = DamageCaseStatus.Open,
                BranchId = branchId,
                Description = request.Description.Trim(),
                ShipmentStatusBefore = shipment.Status,
                ShipmentMarkedLostOrDamaged = markShipment,
                EstimatedLossAmount = request.EstimatedLossAmount
            };

            Db.ShipmentDamageOrLossCases.Add(damageCase);
            await Db.SaveChangesAsync(ct);

            if (markShipment)
            {
                await CloseOpenLinesAsync(shipment.Id, request.CaseType, now, ct);

                _statusChanger.Change(shipment, ShipmentStatus.LostOrDamaged, new StatusChange(ShipmentStatusChangeSource.DamageCase)
                {
                    OccurredAtUtc = now,
                    Notes = request.Description.Trim(),
                    ReferenceType = ReferenceType,
                    ReferenceId = damageCase.Id,
                    ReferenceNumber = damageCase.CaseNumber
                });

                await Db.SaveChangesAsync(ct);
            }

            return damageCase.Id;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DamageCaseDto> StartInvestigationAsync(long id, InvestigateDamageCaseRequest request, CancellationToken cancellationToken)
    {
        var damageCase = await FindForUpdateAsync(id, cancellationToken);

        if (damageCase.Status != DamageCaseStatus.Open)
        {
            throw new BusinessRuleException("الحالة ليست «مفتوحة».");
        }

        damageCase.Status = DamageCaseStatus.UnderInvestigation;
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            damageCase.ResolutionNotes = Truncate($"تحقيق: {request.Notes.Trim()}", 1000);
        }

        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DamageCaseDto> ResolveAsync(long id, ResolveDamageCaseRequest request, CancellationToken cancellationToken)
    {
        var damageCase = await FindForUpdateAsync(id, cancellationToken);

        if (damageCase.Status == DamageCaseStatus.Resolved)
        {
            throw new BusinessRuleException("الحالة مغلقة مسبقاً.");
        }

        var shipment = damageCase.Shipment;
        var now = _dependencies.DateTimeProvider.UtcNow;

        if (request.Resolution == DamageCaseResolution.FoundAndRecovered && shipment.Status == ShipmentStatus.LostOrDamaged)
        {
            var foundAt = request.FoundAtBranchId ?? damageCase.BranchId;
            _access.BranchAccess.EnsureCanAccess(foundAt);
            if (!await Db.Branches.AnyAsync(x => x.Id == foundAt && x.IsActive, cancellationToken))
            {
                throw new RequestValidationException(new Dictionary<string, string[]>
                {
                    ["foundAtBranchId"] = new[] { "الفرع غير موجود أو غير مفعّل." }
                });
            }

            shipment.CurrentBranchId = foundAt;
            _statusChanger.Change(shipment, ShipmentStatus.ReceivedAtBranch, new StatusChange(ShipmentStatusChangeSource.DamageCase)
            {
                OccurredAtUtc = now,
                Notes = "تم العثور على الشحنة",
                ReferenceType = ReferenceType,
                ReferenceId = damageCase.Id,
                ReferenceNumber = damageCase.CaseNumber
            });
        }

        damageCase.Status = DamageCaseStatus.Resolved;
        damageCase.Resolution = request.Resolution;
        damageCase.CompensationAmount = request.Resolution == DamageCaseResolution.Compensated ? request.CompensationAmount : null;
        damageCase.ResolutionNotes = Truncate(
            string.IsNullOrWhiteSpace(damageCase.ResolutionNotes) ? request.Notes.Trim() : $"{damageCase.ResolutionNotes} | {request.Notes.Trim()}",
            1000);
        damageCase.ResolvedAtUtc = now;
        damageCase.ResolvedByUserId = _dependencies.CurrentUser.UserId;

        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<IQueryable<ShipmentDamageOrLossCase>> ScopedAsync(CancellationToken cancellationToken)
    {
        if (_access.BranchAccess.CanAccessAllBranches && !_access.IsDriverOnly)
        {
            return Db.ShipmentDamageOrLossCases;
        }

        var shipments = await _access.ScopeAsync(Db.Shipments, cancellationToken);
        if (_access.IsDriverOnly)
        {
            return Db.ShipmentDamageOrLossCases.Where(x => shipments.Any(s => s.Id == x.ShipmentId));
        }

        var branchId = _access.BranchAccess.CurrentBranchId;
        return Db.ShipmentDamageOrLossCases.Where(x => x.BranchId == branchId || shipments.Any(s => s.Id == x.ShipmentId));
    }

    private async Task<ShipmentDamageOrLossCase> FindForUpdateAsync(long id, CancellationToken cancellationToken)
    {
        var damageCase = await (await ScopedAsync(cancellationToken))
                             .Include(x => x.Shipment)
                             .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                         ?? throw new NotFoundException("حالة التلف أو الفقدان", id);

        _access.BranchAccess.EnsureCanAccess(damageCase.BranchId);
        return damageCase;
    }

    /// <summary>The shipment leaves any open transfer (Missing) or driver manifest (Failed).</summary>
    private async Task CloseOpenLinesAsync(long shipmentId, DamageCaseType caseType, DateTime now, CancellationToken cancellationToken)
    {
        var transferItems = await Db.BranchTransferItems
            .Where(x => x.ShipmentId == shipmentId &&
                        (x.ItemStatus == BranchTransferItemStatus.Pending || x.ItemStatus == BranchTransferItemStatus.InTransit) &&
                        (x.BranchTransfer.Status == BranchTransferStatus.Draft || x.BranchTransfer.Status == BranchTransferStatus.Dispatched))
            .Include(x => x.BranchTransfer)
            .ToListAsync(cancellationToken);

        foreach (var item in transferItems)
        {
            if (item.BranchTransfer.Status == BranchTransferStatus.Draft)
            {
                Db.BranchTransferItems.Remove(item);
            }
            else
            {
                item.ItemStatus = BranchTransferItemStatus.Missing;
                item.Notes = caseType == DamageCaseType.Lost ? "مفقودة" : "تالفة";
            }
        }

        var manifestItems = await Db.DriverDeliveryManifestItems
            .Where(x => x.ShipmentId == shipmentId &&
                        x.Result == DeliveryResult.Pending &&
                        (x.Manifest.Status == DriverManifestStatus.Draft || x.Manifest.Status == DriverManifestStatus.Dispatched))
            .Include(x => x.Manifest)
            .ToListAsync(cancellationToken);

        foreach (var item in manifestItems)
        {
            if (item.Manifest.Status == DriverManifestStatus.Draft)
            {
                Db.DriverDeliveryManifestItems.Remove(item);
            }
            else
            {
                item.Result = DeliveryResult.Failed;
                item.ResultAtUtc = now;
                item.FailureReason = caseType == DamageCaseType.Lost ? "مفقودة" : "تالفة";
            }
        }

        var warehouseItems = await Db.ShipmentManifestItems
            .Where(x => x.ShipmentId == shipmentId && x.Manifest.Status == WarehouseManifestStatus.Draft)
            .ToListAsync(cancellationToken);
        Db.ShipmentManifestItems.RemoveRange(warehouseItems);
    }

    private async Task<PagedResult<DamageCaseDto>> WithEvidenceCountsAsync(PagedResult<DamageCaseDto> page, CancellationToken cancellationToken)
    {
        var ids = page.Items.Select(x => (long?)x.Id).ToList();
        var rows = await Db.ShipmentAttachments.AsNoTracking()
            .Where(x => ids.Contains(x.DamageCaseId))
            .GroupBy(x => x.DamageCaseId)
            .Select(g => new { CaseId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var counts = rows.Where(x => x.CaseId.HasValue).ToDictionary(x => x.CaseId!.Value, x => x.Count);

        var items = page.Items.Select(x => WithEvidence(x, counts.GetValueOrDefault(x.Id))).ToList();
        return new PagedResult<DamageCaseDto>(items, page.PageNumber, page.PageSize, page.TotalCount);
    }

    private static DamageCaseDto WithEvidence(DamageCaseDto x, int count) => new()
    {
        Id = x.Id,
        CaseNumber = x.CaseNumber,
        ShipmentId = x.ShipmentId,
        ShipmentNumber = x.ShipmentNumber,
        TrackingNumber = x.TrackingNumber,
        ShipmentStatus = x.ShipmentStatus,
        CaseType = x.CaseType,
        Status = x.Status,
        BranchId = x.BranchId,
        BranchName = x.BranchName,
        Description = x.Description,
        ShipmentStatusBefore = x.ShipmentStatusBefore,
        ShipmentMarkedLostOrDamaged = x.ShipmentMarkedLostOrDamaged,
        EstimatedLossAmount = x.EstimatedLossAmount,
        Resolution = x.Resolution,
        CompensationAmount = x.CompensationAmount,
        ResolutionNotes = x.ResolutionNotes,
        ResolvedAtUtc = x.ResolvedAtUtc,
        ResolvedByUserId = x.ResolvedByUserId,
        EvidenceCount = count,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId
    };

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
