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
/// Driver delivery manifests. Dispatch makes every shipment OutForDelivery and assigns it to the driver;
/// delivery results come from the DeliveryService (proof of delivery / failed attempt); Close turns the
/// remaining pending shipments into DeliveryFailed.
///
/// Visibility: a driver-only user sees only his own manifests; other users see the manifests of their branch.
/// </summary>
public sealed class DriverManifestService : IDriverManifestService
{
    private const string ReferenceType = "DriverDeliveryManifest";
    private const string DefaultPendingReason = "لم يتم التسليم - أُغلق بيان التوزيع";

    private static readonly Expression<Func<DriverDeliveryManifest, DriverManifestDto>> ToListDto = x => new DriverManifestDto
    {
        Id = x.Id,
        ManifestNumber = x.ManifestNumber,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        DriverId = x.DriverId,
        DriverName = x.Driver.Employee.ArabicName,
        VehicleId = x.VehicleId,
        VehiclePlateNumber = x.Vehicle != null ? x.Vehicle.PlateNumber : null,
        ManifestDateUtc = x.ManifestDateUtc,
        Status = x.Status,
        ItemCount = x.Items.Count(),
        DeliveredCount = x.Items.Count(i => i.Result == DeliveryResult.Delivered),
        FailedCount = x.Items.Count(i => i.Result == DeliveryResult.Failed),
        PendingCount = x.Items.Count(i => i.Result == DeliveryResult.Pending),
        TotalCodAmount = x.Items.Where(i => i.Shipment.IsCOD).Sum(i => i.Shipment.CODAmount),
        DispatchedAtUtc = x.DispatchedAtUtc,
        ClosedAtUtc = x.ClosedAtUtc,
        Notes = x.Notes,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentAccessService _access;
    private readonly ShipmentStatusChanger _statusChanger;
    private readonly DocumentNumberGenerator _numbers;

    public DriverManifestService(
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

    public async Task<PagedResult<DriverManifestDto>> GetPagedAsync(DriverManifestListQuery query, CancellationToken cancellationToken)
    {
        var rows = DocumentQueryHelpers.ApplyDateRange((await ScopedAsync(cancellationToken)).AsNoTracking(), query);

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId);
        }

        if (query.DriverId.HasValue)
        {
            var driverId = query.DriverId.Value;
            rows = rows.Where(x => x.DriverId == driverId);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.ManifestNumber.Contains(search) || x.Items.Any(i => i.Shipment.TrackingNumber == search));
        }

        return await DocumentQueryHelpers.ToPagedAsync(rows, query, x => x.ManifestNumber, ToListDto, cancellationToken);
    }

    public async Task<DriverManifestDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var manifest = await (await ScopedAsync(cancellationToken)).AsNoTracking()
                           .Where(x => x.Id == id)
                           .Select(ToListDto)
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw new NotFoundException("بيان التوزيع", id);

        var items = await Db.DriverDeliveryManifestItems.AsNoTracking()
            .Where(x => x.ManifestId == id)
            .OrderBy(x => x.Sequence)
            .Select(x => new DriverManifestItemDto
            {
                ItemId = x.Id,
                ShipmentId = x.ShipmentId,
                ShipmentNumber = x.Shipment.ShipmentNumber,
                TrackingNumber = x.Shipment.TrackingNumber,
                ShipmentStatus = x.Shipment.Status,
                ShipmentStatusName = ShipmentStatusNames.Arabic(x.Shipment.Status),
                RecipientName = x.Shipment.RecipientCustomer.ArabicName,
                RecipientPhone = x.Shipment.RecipientAddress != null && x.Shipment.RecipientAddress.Phone != null
                    ? x.Shipment.RecipientAddress.Phone
                    : x.Shipment.RecipientCustomer.Phone,
                RecipientAddressLine = x.Shipment.RecipientAddress != null ? x.Shipment.RecipientAddress.AddressLine : null,
                DestinationCityName = x.Shipment.DestinationCity.ArabicName,
                DestinationZoneName = x.Shipment.DestinationZone != null ? x.Shipment.DestinationZone.ArabicName : null,
                PackageCount = x.Shipment.PackageCount,
                ChargeableWeight = x.Shipment.ChargeableWeight,
                IsCOD = x.Shipment.IsCOD,
                CODAmount = x.Shipment.CODAmount,
                ChargesOnRecipient = x.Shipment.ChargesPaidBy == ChargesPayer.Recipient,
                ChargesToCollect = x.Shipment.ChargesPaidBy == ChargesPayer.Recipient ? x.Shipment.TotalCharge : 0m,
                Sequence = x.Sequence,
                Result = x.Result,
                ResultAtUtc = x.ResultAtUtc,
                FailureReason = x.FailureReason,
                ProofOfDeliveryId = x.ProofOfDeliveryId
            })
            .ToListAsync(cancellationToken);

        return WithItems(manifest, items);
    }

    public async Task<DriverManifestDto> CreateAsync(CreateDriverManifestRequest request, CancellationToken cancellationToken)
    {
        var now = _dependencies.DateTimeProvider.UtcNow;
        var manifestDate = DateTime.SpecifyKind((request.ManifestDateUtc ?? now).Date, DateTimeKind.Utc);

        var driver = await DriverRules.GetUsableDriverAsync(Db, request.DriverId, null, manifestDate, cancellationToken);
        _dependencies.BranchAccess.EnsureCanAccess(driver.BranchId);

        var vehicleId = request.VehicleId ?? driver.DefaultVehicleId;
        if (vehicleId.HasValue)
        {
            await DriverRules.EnsureUsableVehicleAsync(Db, vehicleId.Value, driver.BranchId, cancellationToken);
        }

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            var manifest = new DriverDeliveryManifest
            {
                ManifestNumber = await _numbers.NextAsync(DocumentPrefixes.DriverManifest, ct),
                BranchId = driver.BranchId,
                DriverId = driver.Id,
                VehicleId = vehicleId,
                ManifestDateUtc = manifestDate,
                Status = DriverManifestStatus.Draft,
                Notes = Optional(request.Notes)
            };

            Db.DriverDeliveryManifests.Add(manifest);

            if (request.Codes.Count > 0)
            {
                await AddItemsAsync(manifest, request.Codes, ct);
            }

            await Db.SaveChangesAsync(ct);
            return manifest.Id;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DriverManifestDto> AddShipmentsAsync(long id, ShipmentCodesRequest request, CancellationToken cancellationToken)
    {
        var manifest = await FindForUpdateAsync(id, DriverManifestStatus.Draft, cancellationToken);
        await AddItemsAsync(manifest, request.Codes, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DriverManifestDto> RemoveShipmentAsync(long id, long shipmentId, CancellationToken cancellationToken)
    {
        var manifest = await FindForUpdateAsync(id, DriverManifestStatus.Draft, cancellationToken);
        var item = manifest.Items.FirstOrDefault(x => x.ShipmentId == shipmentId)
                   ?? throw new NotFoundException("الشحنة غير موجودة في هذا البيان.");

        manifest.Items.Remove(item);
        Db.DriverDeliveryManifestItems.Remove(item);

        // Keep the stop order without gaps.
        var sequence = 1;
        foreach (var remaining in manifest.Items.OrderBy(x => x.Sequence))
        {
            remaining.Sequence = sequence++;
        }

        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DriverManifestDto> DispatchAsync(long id, CancellationToken cancellationToken)
    {
        var manifest = await FindForUpdateAsync(id, DriverManifestStatus.Draft, cancellationToken);
        if (manifest.Items.Count == 0)
        {
            throw new BusinessRuleException("لا يمكن تسليم بيان فارغ للسائق.");
        }

        var now = _dependencies.DateTimeProvider.UtcNow;
        await DriverRules.GetUsableDriverAsync(Db, manifest.DriverId, manifest.BranchId, now, cancellationToken);

        var shipments = await LoadShipmentsAsync(manifest, cancellationToken);
        var errors = manifest.Items
            .Select(item => ValidateLine(manifest, shipments[item.ShipmentId]))
            .Where(error => error is not null)
            .Select(error => error!)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["items"] = errors });
        }

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var item in manifest.Items.OrderBy(x => x.Sequence))
            {
                var shipment = shipments[item.ShipmentId];
                shipment.AssignedDriverId = manifest.DriverId;
                shipment.VehicleId = manifest.VehicleId;
                shipment.DeliveryAttemptCount++;

                _statusChanger.Change(shipment, ShipmentStatus.OutForDelivery, new StatusChange(ShipmentStatusChangeSource.DriverManifest)
                {
                    OccurredAtUtc = now,
                    DriverId = manifest.DriverId,
                    ReferenceType = ReferenceType,
                    ReferenceId = manifest.Id,
                    ReferenceNumber = manifest.ManifestNumber
                });
            }

            manifest.Status = DriverManifestStatus.Dispatched;
            manifest.DispatchedAtUtc = now;

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DriverManifestDto> CloseAsync(long id, CloseDriverManifestRequest request, CancellationToken cancellationToken)
    {
        var manifest = await FindForUpdateAsync(id, DriverManifestStatus.Dispatched, cancellationToken);
        var shipments = await LoadShipmentsAsync(manifest, cancellationToken);
        var now = _dependencies.DateTimeProvider.UtcNow;
        var reason = string.IsNullOrWhiteSpace(request.PendingReason) ? DefaultPendingReason : request.PendingReason.Trim();

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var item in manifest.Items.Where(x => x.Result == DeliveryResult.Pending))
            {
                var shipment = shipments[item.ShipmentId];

                if (shipment.Status == ShipmentStatus.OutForDelivery)
                {
                    _statusChanger.Change(shipment, ShipmentStatus.DeliveryFailed, new StatusChange(ShipmentStatusChangeSource.DriverManifest)
                    {
                        OccurredAtUtc = now,
                        DriverId = manifest.DriverId,
                        Notes = reason,
                        ReferenceType = ReferenceType,
                        ReferenceId = manifest.Id,
                        ReferenceNumber = manifest.ManifestNumber
                    });
                }

                item.Result = shipment.Status == ShipmentStatus.Delivered ? DeliveryResult.Delivered : DeliveryResult.Failed;
                item.ResultAtUtc = now;
                item.FailureReason = item.Result == DeliveryResult.Failed ? (reason.Length > 300 ? reason[..300] : reason) : null;
            }

            manifest.Status = DriverManifestStatus.Closed;
            manifest.ClosedAtUtc = now;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                var notes = string.IsNullOrWhiteSpace(manifest.Notes) ? request.Notes.Trim() : $"{manifest.Notes} | {request.Notes.Trim()}";
                manifest.Notes = notes.Length > 500 ? notes[..500] : notes;
            }

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DriverManifestDto> CancelAsync(long id, CancellationToken cancellationToken)
    {
        var manifest = await FindForUpdateAsync(id, DriverManifestStatus.Draft, cancellationToken);
        manifest.Status = DriverManifestStatus.Cancelled;
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<IQueryable<DriverDeliveryManifest>> ScopedAsync(CancellationToken cancellationToken)
    {
        if (_access.IsDriverOnly)
        {
            var driverId = await _access.GetCurrentDriverIdAsync(cancellationToken);
            return driverId is null
                ? Db.DriverDeliveryManifests.Where(_ => false)
                : Db.DriverDeliveryManifests.Where(x => x.DriverId == driverId);
        }

        if (_dependencies.BranchAccess.CanAccessAllBranches)
        {
            return Db.DriverDeliveryManifests;
        }

        var branchId = _dependencies.BranchAccess.CurrentBranchId;
        return Db.DriverDeliveryManifests.Where(x => x.BranchId == branchId);
    }

    private async Task<DriverDeliveryManifest> FindForUpdateAsync(long id, DriverManifestStatus requiredStatus, CancellationToken cancellationToken)
    {
        if (_access.IsDriverOnly)
        {
            throw new ForbiddenException("السائق لا يمكنه تعديل بيانات التوزيع.");
        }

        var manifest = await (await ScopedAsync(cancellationToken))
                           .Include(x => x.Items)
                           .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                       ?? throw new NotFoundException("بيان التوزيع", id);

        if (manifest.Status != requiredStatus)
        {
            throw new BusinessRuleException(requiredStatus == DriverManifestStatus.Draft
                ? $"البيان {manifest.ManifestNumber} لم يعد مسودة."
                : $"البيان {manifest.ManifestNumber} ليس مع السائق حالياً؛ لا يمكن إغلاقه.");
        }

        return manifest;
    }

    private async Task<Dictionary<long, Shipment>> LoadShipmentsAsync(DriverDeliveryManifest manifest, CancellationToken cancellationToken)
    {
        var ids = manifest.Items.Select(x => x.ShipmentId).ToList();
        return await Db.Shipments.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    private async Task AddItemsAsync(DriverDeliveryManifest manifest, IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
    {
        var resolved = await ShipmentCodeResolver.ResolveAsync(Db, codes, cancellationToken);
        var errors = new List<string>();
        var toAdd = new List<Shipment>();
        long? exceptId = manifest.Id == 0 ? null : manifest.Id;

        foreach (var (code, shipment) in resolved)
        {
            if (shipment is null)
            {
                errors.Add($"{code}: لم يتم العثور على الشحنة.");
                continue;
            }

            if (manifest.Items.Any(x => x.ShipmentId == shipment.Id) || toAdd.Contains(shipment))
            {
                continue;
            }

            var error = ValidateLine(manifest, shipment);
            if (error is not null)
            {
                errors.Add(error);
                continue;
            }

            var other = await ShipmentDocumentGuards.FindOpenDriverManifestAsync(Db, shipment.Id, exceptId, cancellationToken)
                        ?? await ShipmentDocumentGuards.FindOpenTransferAsync(Db, shipment.Id, null, cancellationToken);
            if (other is not null)
            {
                errors.Add($"{shipment.TrackingNumber}: موجودة على مستند مفتوح آخر ({other}).");
                continue;
            }

            toAdd.Add(shipment);
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["codes"] = errors.ToArray() });
        }

        var sequence = manifest.Items.Count == 0 ? 0 : manifest.Items.Max(x => x.Sequence);
        foreach (var shipment in toAdd)
        {
            manifest.Items.Add(new DriverDeliveryManifestItem
            {
                Manifest = manifest,
                ShipmentId = shipment.Id,
                Sequence = ++sequence,
                Result = DeliveryResult.Pending
            });
        }
    }

    private static string? ValidateLine(DriverDeliveryManifest manifest, Shipment shipment)
    {
        if (shipment.CurrentBranchId != manifest.BranchId)
        {
            return $"{shipment.TrackingNumber}: الشحنة ليست في فرع السائق.";
        }

        if (shipment.ReturnBranchId.HasValue)
        {
            return $"{shipment.TrackingNumber}: عليها مرتجع مفتوح.";
        }

        return ShipmentStatusChanger.CanChange(shipment, ShipmentStatus.OutForDelivery, ShipmentStatusChangeSource.DriverManifest)
            ? null
            : $"{shipment.TrackingNumber}: لا يمكن إخراجها للتوصيل وهي في حالة «{ShipmentStatusNames.Arabic(shipment.Status)}».";
    }

    private static DriverManifestDto WithItems(DriverManifestDto x, IReadOnlyList<DriverManifestItemDto> items) => new()
    {
        Id = x.Id,
        ManifestNumber = x.ManifestNumber,
        BranchId = x.BranchId,
        BranchName = x.BranchName,
        DriverId = x.DriverId,
        DriverName = x.DriverName,
        VehicleId = x.VehicleId,
        VehiclePlateNumber = x.VehiclePlateNumber,
        ManifestDateUtc = x.ManifestDateUtc,
        Status = x.Status,
        ItemCount = x.ItemCount,
        DeliveredCount = x.DeliveredCount,
        FailedCount = x.FailedCount,
        PendingCount = x.PendingCount,
        TotalCodAmount = x.TotalCodAmount,
        DispatchedAtUtc = x.DispatchedAtUtc,
        ClosedAtUtc = x.ClosedAtUtc,
        Notes = x.Notes,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        Items = items
    };

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
