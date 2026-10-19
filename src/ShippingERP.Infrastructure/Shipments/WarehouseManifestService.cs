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
/// Warehouse movement manifests (check-in / check-out). Users see the manifests of their branch
/// (all branches with Branches.ViewAll).
/// </summary>
public sealed class WarehouseManifestService : IWarehouseManifestService
{
    private const string ReferenceType = "ShipmentManifest";

    private static readonly Expression<Func<ShipmentManifest, WarehouseManifestDto>> ToListDto = x => new WarehouseManifestDto
    {
        Id = x.Id,
        ManifestNumber = x.ManifestNumber,
        MovementType = x.MovementType,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        WarehouseId = x.WarehouseId,
        WarehouseName = x.Warehouse.ArabicName,
        Status = x.Status,
        ItemCount = x.Items.Count(),
        ConfirmedAtUtc = x.ConfirmedAtUtc,
        ConfirmedByUserId = x.ConfirmedByUserId,
        Notes = x.Notes,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentStatusChanger _statusChanger;
    private readonly DocumentNumberGenerator _numbers;

    public WarehouseManifestService(
        MasterDataServiceDependencies dependencies,
        ShipmentStatusChanger statusChanger,
        DocumentNumberGenerator numbers)
    {
        _dependencies = dependencies;
        _statusChanger = statusChanger;
        _numbers = numbers;
    }

    private AppDbContext Db => _dependencies.DbContext;

    public async Task<PagedResult<WarehouseManifestDto>> GetPagedAsync(WarehouseManifestListQuery query, CancellationToken cancellationToken)
    {
        var rows = DocumentQueryHelpers.ApplyDateRange(Scoped().AsNoTracking(), query);

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId);
        }

        if (query.WarehouseId.HasValue)
        {
            var warehouseId = query.WarehouseId.Value;
            rows = rows.Where(x => x.WarehouseId == warehouseId);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.MovementType.HasValue)
        {
            var movementType = query.MovementType.Value;
            rows = rows.Where(x => x.MovementType == movementType);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.ManifestNumber.Contains(search) || x.Items.Any(i => i.Shipment.TrackingNumber == search));
        }

        return await DocumentQueryHelpers.ToPagedAsync(rows, query, x => x.ManifestNumber, ToListDto, cancellationToken);
    }

    public async Task<WarehouseManifestDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var manifest = await Scoped().AsNoTracking()
                           .Where(x => x.Id == id)
                           .Select(ToListDto)
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw new NotFoundException("بيان حركة المستودع", id);

        var items = await Db.ShipmentManifestItems.AsNoTracking()
            .Where(x => x.ManifestId == id)
            .OrderBy(x => x.Id)
            .Select(x => new ManifestShipmentDto
            {
                ItemId = x.Id,
                ShipmentId = x.ShipmentId,
                ShipmentNumber = x.Shipment.ShipmentNumber,
                TrackingNumber = x.Shipment.TrackingNumber,
                ShipmentStatus = x.Shipment.Status,
                ShipmentStatusName = ShipmentStatusNames.Arabic(x.Shipment.Status),
                RecipientName = x.Shipment.RecipientCustomer.ArabicName,
                RecipientPhone = x.Shipment.RecipientCustomer.Phone,
                DestinationCityName = x.Shipment.DestinationCity.ArabicName,
                DestinationZoneName = x.Shipment.DestinationZone != null ? x.Shipment.DestinationZone.ArabicName : null,
                PackageCount = x.Shipment.PackageCount,
                ChargeableWeight = x.Shipment.ChargeableWeight,
                IsCOD = x.Shipment.IsCOD,
                CODAmount = x.Shipment.CODAmount
            })
            .ToListAsync(cancellationToken);

        return WithItems(manifest, items);
    }

    public async Task<WarehouseManifestDto> CreateAsync(CreateWarehouseManifestRequest request, CancellationToken cancellationToken)
    {
        var warehouseId = request.WarehouseId;
        var warehouse = await Db.Warehouses.AsNoTracking()
            .Where(x => x.Id == warehouseId)
            .Select(x => new { x.Id, x.BranchId, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (warehouse is null || !warehouse.IsActive)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["warehouseId"] = new[] { "المستودع غير موجود أو غير مفعّل." }
            });
        }

        _dependencies.BranchAccess.EnsureCanAccess(warehouse.BranchId);

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            var manifest = new ShipmentManifest
            {
                ManifestNumber = await _numbers.NextAsync(DocumentPrefixes.WarehouseManifest, ct),
                MovementType = request.MovementType,
                BranchId = warehouse.BranchId,
                WarehouseId = warehouse.Id,
                Status = WarehouseManifestStatus.Draft,
                Notes = Optional(request.Notes)
            };

            Db.ShipmentManifests.Add(manifest);

            if (request.Codes.Count > 0)
            {
                await AddItemsAsync(manifest, request.Codes, ct);
            }

            await Db.SaveChangesAsync(ct);
            return manifest.Id;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<WarehouseManifestDto> AddShipmentsAsync(long id, ShipmentCodesRequest request, CancellationToken cancellationToken)
    {
        var manifest = await FindDraftAsync(id, cancellationToken);
        await AddItemsAsync(manifest, request.Codes, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<WarehouseManifestDto> RemoveShipmentAsync(long id, long shipmentId, CancellationToken cancellationToken)
    {
        var manifest = await FindDraftAsync(id, cancellationToken);
        var item = manifest.Items.FirstOrDefault(x => x.ShipmentId == shipmentId)
                   ?? throw new NotFoundException("الشحنة غير موجودة في هذا البيان.");

        manifest.Items.Remove(item);
        Db.ShipmentManifestItems.Remove(item);
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<WarehouseManifestDto> ConfirmAsync(long id, CancellationToken cancellationToken)
    {
        var manifest = await FindDraftAsync(id, cancellationToken);
        if (manifest.Items.Count == 0)
        {
            throw new BusinessRuleException("لا يمكن تأكيد بيان فارغ.");
        }

        var shipmentIds = manifest.Items.Select(x => x.ShipmentId).ToList();
        var shipments = await Db.Shipments.Where(x => shipmentIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var now = _dependencies.DateTimeProvider.UtcNow;

        // Validate everything first, then apply: all or nothing.
        var errors = new List<string>();
        foreach (var item in manifest.Items)
        {
            var error = ValidateLine(manifest, shipments[item.ShipmentId]);
            if (error is not null)
            {
                errors.Add(error);
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["items"] = errors.ToArray() });
        }

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var item in manifest.Items)
            {
                var shipment = shipments[item.ShipmentId];
                item.PreviousStatus = shipment.Status;
                item.ProcessedAtUtc = now;

                var target = manifest.MovementType == WarehouseMovementType.CheckIn
                    ? ShipmentStatus.InWarehouse
                    : ShipmentStatus.ReceivedAtBranch;

                _statusChanger.Change(shipment, target, new StatusChange(ShipmentStatusChangeSource.WarehouseManifest)
                {
                    OccurredAtUtc = now,
                    WarehouseId = target == ShipmentStatus.InWarehouse ? manifest.WarehouseId : null,
                    ReferenceType = ReferenceType,
                    ReferenceId = manifest.Id,
                    ReferenceNumber = manifest.ManifestNumber
                });
            }

            manifest.Status = WarehouseManifestStatus.Confirmed;
            manifest.ConfirmedAtUtc = now;
            manifest.ConfirmedByUserId = _dependencies.CurrentUser.UserId;

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<WarehouseManifestDto> CancelAsync(long id, CancellationToken cancellationToken)
    {
        var manifest = await FindDraftAsync(id, cancellationToken);
        manifest.Status = WarehouseManifestStatus.Cancelled;
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private IQueryable<ShipmentManifest> Scoped()
    {
        if (_dependencies.BranchAccess.CanAccessAllBranches)
        {
            return Db.ShipmentManifests;
        }

        var branchId = _dependencies.BranchAccess.CurrentBranchId;
        return Db.ShipmentManifests.Where(x => x.BranchId == branchId);
    }

    private async Task<ShipmentManifest> FindDraftAsync(long id, CancellationToken cancellationToken)
    {
        var manifest = await Scoped().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                       ?? throw new NotFoundException("بيان حركة المستودع", id);

        if (manifest.Status != WarehouseManifestStatus.Draft)
        {
            throw new BusinessRuleException($"البيان {manifest.ManifestNumber} ليس مسودة؛ لا يمكن تعديله.");
        }

        return manifest;
    }

    private async Task AddItemsAsync(ShipmentManifest manifest, IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
    {
        var resolved = await ShipmentCodeResolver.ResolveAsync(Db, codes, cancellationToken);
        var errors = new List<string>();
        var toAdd = new List<Shipment>();

        foreach (var (code, shipment) in resolved)
        {
            if (shipment is null)
            {
                errors.Add($"{code}: لم يتم العثور على الشحنة.");
                continue;
            }

            if (manifest.Items.Any(x => x.ShipmentId == shipment.Id) || toAdd.Contains(shipment))
            {
                continue; // already on this manifest
            }

            var error = ValidateLine(manifest, shipment);
            var other = error is null ? await ShipmentDocumentGuards.FindDraftWarehouseManifestAsync(Db, shipment.Id, manifest.Id == 0 ? null : manifest.Id, cancellationToken) : null;

            if (error is not null)
            {
                errors.Add(error);
            }
            else if (other is not null)
            {
                errors.Add($"{shipment.TrackingNumber}: موجودة على بيان مستودع آخر ({other}).");
            }
            else
            {
                toAdd.Add(shipment);
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["codes"] = errors.ToArray() });
        }

        foreach (var shipment in toAdd)
        {
            manifest.Items.Add(new ShipmentManifestItem { Manifest = manifest, ShipmentId = shipment.Id });
        }
    }

    /// <summary>Null when the shipment can be moved by this manifest, otherwise the reason.</summary>
    private static string? ValidateLine(ShipmentManifest manifest, Shipment shipment)
    {
        if (shipment.CurrentBranchId != manifest.BranchId)
        {
            return $"{shipment.TrackingNumber}: الشحنة ليست في فرع هذا المستودع.";
        }

        if (manifest.MovementType == WarehouseMovementType.CheckIn)
        {
            return ShipmentStatusChanger.CanChange(shipment, ShipmentStatus.InWarehouse, ShipmentStatusChangeSource.WarehouseManifest)
                ? null
                : $"{shipment.TrackingNumber}: لا يمكن إدخالها إلى المستودع وهي في حالة «{ShipmentStatusNames.Arabic(shipment.Status)}».";
        }

        if (shipment.Status != ShipmentStatus.InWarehouse || shipment.CurrentWarehouseId != manifest.WarehouseId)
        {
            return $"{shipment.TrackingNumber}: الشحنة ليست في هذا المستودع.";
        }

        return null;
    }

    private static WarehouseManifestDto WithItems(WarehouseManifestDto x, IReadOnlyList<ManifestShipmentDto> items) => new()
    {
        Id = x.Id,
        ManifestNumber = x.ManifestNumber,
        MovementType = x.MovementType,
        BranchId = x.BranchId,
        BranchName = x.BranchName,
        WarehouseId = x.WarehouseId,
        WarehouseName = x.WarehouseName,
        Status = x.Status,
        ItemCount = x.ItemCount,
        ConfirmedAtUtc = x.ConfirmedAtUtc,
        ConfirmedByUserId = x.ConfirmedByUserId,
        Notes = x.Notes,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        Items = items
    };

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
