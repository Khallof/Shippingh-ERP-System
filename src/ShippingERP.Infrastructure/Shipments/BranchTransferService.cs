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
/// Branch transfer manifests. The sending branch creates and dispatches; the receiving branch receives.
/// Users see transfers where their branch is the sender or the receiver (all with Branches.ViewAll).
///
/// On receive, a shipment becomes ArrivedAtDestination when the receiving branch is its destination branch
/// (or its return branch while a return is open); otherwise ReceivedAtBranch (a hub on the way).
/// Shipments not received are marked Missing and stay InTransit until found (status correction) or reported
/// lost (damage / loss case).
/// </summary>
public sealed class BranchTransferService : IBranchTransferService
{
    private const string ReferenceType = "BranchTransfer";

    private static readonly Expression<Func<BranchTransfer, BranchTransferDto>> ToListDto = x => new BranchTransferDto
    {
        Id = x.Id,
        TransferNumber = x.TransferNumber,
        FromBranchId = x.FromBranchId,
        FromBranchName = x.FromBranch.ArabicName,
        ToBranchId = x.ToBranchId,
        ToBranchName = x.ToBranch.ArabicName,
        VehicleId = x.VehicleId,
        VehiclePlateNumber = x.Vehicle != null ? x.Vehicle.PlateNumber : null,
        DriverId = x.DriverId,
        DriverName = x.Driver != null ? x.Driver.Employee.ArabicName : null,
        SealNumber = x.SealNumber,
        Status = x.Status,
        ItemCount = x.Items.Count(),
        ReceivedCount = x.Items.Count(i => i.ItemStatus == BranchTransferItemStatus.Received),
        MissingCount = x.Items.Count(i => i.ItemStatus == BranchTransferItemStatus.Missing),
        DispatchedAtUtc = x.DispatchedAtUtc,
        DispatchedByUserId = x.DispatchedByUserId,
        ReceivedAtUtc = x.ReceivedAtUtc,
        ReceivedByUserId = x.ReceivedByUserId,
        Notes = x.Notes,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentStatusChanger _statusChanger;
    private readonly DocumentNumberGenerator _numbers;

    public BranchTransferService(
        MasterDataServiceDependencies dependencies,
        ShipmentStatusChanger statusChanger,
        DocumentNumberGenerator numbers)
    {
        _dependencies = dependencies;
        _statusChanger = statusChanger;
        _numbers = numbers;
    }

    private AppDbContext Db => _dependencies.DbContext;

    public async Task<PagedResult<BranchTransferDto>> GetPagedAsync(BranchTransferListQuery query, CancellationToken cancellationToken)
    {
        var rows = DocumentQueryHelpers.ApplyDateRange(Scoped().AsNoTracking(), query);

        if (query.FromBranchId.HasValue)
        {
            var fromBranchId = query.FromBranchId.Value;
            rows = rows.Where(x => x.FromBranchId == fromBranchId);
        }

        if (query.ToBranchId.HasValue)
        {
            var toBranchId = query.ToBranchId.Value;
            rows = rows.Where(x => x.ToBranchId == toBranchId);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.TransferNumber.Contains(search) ||
                                   (x.SealNumber != null && x.SealNumber.Contains(search)) ||
                                   x.Items.Any(i => i.Shipment.TrackingNumber == search));
        }

        return await DocumentQueryHelpers.ToPagedAsync(rows, query, x => x.TransferNumber, ToListDto, cancellationToken);
    }

    public async Task<BranchTransferDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var transfer = await Scoped().AsNoTracking()
                           .Where(x => x.Id == id)
                           .Select(ToListDto)
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw new NotFoundException("التحويل", id);

        var items = await Db.BranchTransferItems.AsNoTracking()
            .Where(x => x.BranchTransferId == id)
            .OrderBy(x => x.Id)
            .Select(x => new BranchTransferItemDto
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
                CODAmount = x.Shipment.CODAmount,
                ItemStatus = x.ItemStatus,
                ReceivedAtUtc = x.ReceivedAtUtc,
                Notes = x.Notes
            })
            .ToListAsync(cancellationToken);

        return WithItems(transfer, items);
    }

    public async Task<BranchTransferDto> CreateAsync(CreateBranchTransferRequest request, CancellationToken cancellationToken)
    {
        _dependencies.BranchAccess.EnsureCanAccess(request.FromBranchId);

        await EnsureActiveBranchAsync(request.FromBranchId, "fromBranchId", cancellationToken);
        await EnsureActiveBranchAsync(request.ToBranchId, "toBranchId", cancellationToken);
        await ValidateTransportAsync(request.VehicleId, request.DriverId, cancellationToken);

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            var transfer = new BranchTransfer
            {
                TransferNumber = await _numbers.NextAsync(DocumentPrefixes.BranchTransfer, ct),
                FromBranchId = request.FromBranchId,
                ToBranchId = request.ToBranchId,
                VehicleId = request.VehicleId,
                DriverId = request.DriverId,
                SealNumber = Optional(request.SealNumber),
                Notes = Optional(request.Notes),
                Status = BranchTransferStatus.Draft
            };

            Db.BranchTransfers.Add(transfer);

            if (request.Codes.Count > 0)
            {
                await AddItemsAsync(transfer, request.Codes, ct);
            }

            await Db.SaveChangesAsync(ct);
            return transfer.Id;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<BranchTransferDto> AddShipmentsAsync(long id, ShipmentCodesRequest request, CancellationToken cancellationToken)
    {
        var transfer = await FindAsync(id, BranchTransferStatus.Draft, cancellationToken);
        _dependencies.BranchAccess.EnsureCanAccess(transfer.FromBranchId);

        await AddItemsAsync(transfer, request.Codes, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<BranchTransferDto> RemoveShipmentAsync(long id, long shipmentId, CancellationToken cancellationToken)
    {
        var transfer = await FindAsync(id, BranchTransferStatus.Draft, cancellationToken);
        _dependencies.BranchAccess.EnsureCanAccess(transfer.FromBranchId);

        var item = transfer.Items.FirstOrDefault(x => x.ShipmentId == shipmentId)
                   ?? throw new NotFoundException("الشحنة غير موجودة في هذا التحويل.");

        transfer.Items.Remove(item);
        Db.BranchTransferItems.Remove(item);
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<BranchTransferDto> DispatchAsync(long id, DispatchBranchTransferRequest request, CancellationToken cancellationToken)
    {
        var transfer = await FindAsync(id, BranchTransferStatus.Draft, cancellationToken);
        _dependencies.BranchAccess.EnsureCanAccess(transfer.FromBranchId);

        if (transfer.Items.Count == 0)
        {
            throw new BusinessRuleException("لا يمكن إرسال تحويل فارغ.");
        }

        var vehicleId = request.VehicleId ?? transfer.VehicleId;
        var driverId = request.DriverId ?? transfer.DriverId;
        await ValidateTransportAsync(vehicleId, driverId, cancellationToken);

        var shipments = await LoadShipmentsAsync(transfer, cancellationToken);
        var errors = transfer.Items
            .Select(item => ValidateLine(transfer, shipments[item.ShipmentId]))
            .Where(error => error is not null)
            .Select(error => error!)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["items"] = errors });
        }

        var now = _dependencies.DateTimeProvider.UtcNow;
        var toBranchName = await Db.Branches.AsNoTracking()
            .Where(x => x.Id == transfer.ToBranchId)
            .Select(x => x.ArabicName)
            .FirstOrDefaultAsync(cancellationToken);

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            transfer.VehicleId = vehicleId;
            transfer.DriverId = driverId;
            transfer.SealNumber = Optional(request.SealNumber) ?? transfer.SealNumber;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                transfer.Notes = request.Notes.Trim();
            }

            foreach (var item in transfer.Items)
            {
                var shipment = shipments[item.ShipmentId];
                _statusChanger.Change(shipment, ShipmentStatus.InTransit, new StatusChange(ShipmentStatusChangeSource.BranchTransfer)
                {
                    OccurredAtUtc = now,
                    DriverId = driverId,
                    Notes = $"إلى {toBranchName}",
                    ReferenceType = ReferenceType,
                    ReferenceId = transfer.Id,
                    ReferenceNumber = transfer.TransferNumber
                });
                item.ItemStatus = BranchTransferItemStatus.InTransit;
            }

            transfer.Status = BranchTransferStatus.Dispatched;
            transfer.DispatchedAtUtc = now;
            transfer.DispatchedByUserId = _dependencies.CurrentUser.UserId;

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<BranchTransferDto> ReceiveAsync(long id, ReceiveBranchTransferRequest request, CancellationToken cancellationToken)
    {
        var transfer = await FindAsync(id, BranchTransferStatus.Dispatched, cancellationToken);

        // Only the receiving branch confirms what arrived.
        _dependencies.BranchAccess.EnsureCanAccess(transfer.ToBranchId);

        var shipments = await LoadShipmentsAsync(transfer, cancellationToken);
        var receivedIds = new HashSet<long>();

        if (request.ReceivedCodes.Count == 0)
        {
            receivedIds.UnionWith(transfer.Items.Select(x => x.ShipmentId));
        }
        else
        {
            var errors = new List<string>();
            foreach (var code in request.ReceivedCodes.Select(ShipmentCodeResolver.Normalize).Where(c => c.Length > 0).Distinct())
            {
                var match = shipments.Values.FirstOrDefault(s =>
                    s.TrackingNumber == code || s.ShipmentNumber == code || code.StartsWith(s.TrackingNumber + "-", StringComparison.Ordinal));

                if (match is null)
                {
                    errors.Add($"{code}: ليست على هذا التحويل.");
                }
                else
                {
                    receivedIds.Add(match.Id);
                }
            }

            if (errors.Count > 0)
            {
                throw new RequestValidationException(new Dictionary<string, string[]> { ["receivedCodes"] = errors.ToArray() });
            }
        }

        var now = _dependencies.DateTimeProvider.UtcNow;
        var toBranch = await Db.Branches.AsNoTracking()
            .Where(x => x.Id == transfer.ToBranchId)
            .Select(x => new { x.Id, x.CityId })
            .FirstAsync(cancellationToken);

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var item in transfer.Items)
            {
                var shipment = shipments[item.ShipmentId];

                if (!receivedIds.Contains(item.ShipmentId) || shipment.Status != ShipmentStatus.InTransit)
                {
                    item.ItemStatus = BranchTransferItemStatus.Missing;
                    item.Notes = shipment.Status == ShipmentStatus.InTransit ? "لم تصل مع التحويل" : $"حالتها {ShipmentStatusNames.Arabic(shipment.Status)}";
                    continue;
                }

                // Destination not known yet and this branch serves the destination city: it is the destination.
                if (shipment.DestinationBranchId is null && shipment.ReturnBranchId is null && shipment.DestinationCityId == toBranch.CityId)
                {
                    shipment.DestinationBranchId = toBranch.Id;
                }

                var arrived = shipment.ReturnBranchId.HasValue
                    ? shipment.ReturnBranchId == toBranch.Id
                    : shipment.DestinationBranchId == toBranch.Id;

                shipment.CurrentBranchId = toBranch.Id;
                _statusChanger.Change(
                    shipment,
                    arrived ? ShipmentStatus.ArrivedAtDestination : ShipmentStatus.ReceivedAtBranch,
                    new StatusChange(ShipmentStatusChangeSource.BranchTransfer)
                    {
                        OccurredAtUtc = now,
                        ReferenceType = ReferenceType,
                        ReferenceId = transfer.Id,
                        ReferenceNumber = transfer.TransferNumber
                    });

                item.ItemStatus = BranchTransferItemStatus.Received;
                item.ReceivedAtUtc = now;
            }

            transfer.Status = transfer.Items.All(x => x.ItemStatus == BranchTransferItemStatus.Received)
                ? BranchTransferStatus.Received
                : BranchTransferStatus.PartiallyReceived;
            transfer.ReceivedAtUtc = now;
            transfer.ReceivedByUserId = _dependencies.CurrentUser.UserId;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                transfer.Notes = string.IsNullOrWhiteSpace(transfer.Notes) ? request.Notes.Trim() : $"{transfer.Notes} | {request.Notes.Trim()}";
                if (transfer.Notes.Length > 500)
                {
                    transfer.Notes = transfer.Notes[..500];
                }
            }

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<BranchTransferDto> CancelAsync(long id, CancellationToken cancellationToken)
    {
        var transfer = await FindAsync(id, BranchTransferStatus.Draft, cancellationToken);
        _dependencies.BranchAccess.EnsureCanAccess(transfer.FromBranchId);

        transfer.Status = BranchTransferStatus.Cancelled;
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private IQueryable<BranchTransfer> Scoped()
    {
        if (_dependencies.BranchAccess.CanAccessAllBranches)
        {
            return Db.BranchTransfers;
        }

        var branchId = _dependencies.BranchAccess.CurrentBranchId;
        return Db.BranchTransfers.Where(x => x.FromBranchId == branchId || x.ToBranchId == branchId);
    }

    private async Task<BranchTransfer> FindAsync(long id, BranchTransferStatus requiredStatus, CancellationToken cancellationToken)
    {
        var transfer = await Scoped().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                       ?? throw new NotFoundException("التحويل", id);

        if (transfer.Status != requiredStatus)
        {
            throw new BusinessRuleException(requiredStatus == BranchTransferStatus.Draft
                ? $"التحويل {transfer.TransferNumber} لم يعد مسودة؛ لا يمكن تعديله أو إرساله أو إلغاؤه."
                : $"التحويل {transfer.TransferNumber} ليس في حالة «مُرسل»؛ لا يمكن استلامه.");
        }

        return transfer;
    }

    private async Task<Dictionary<long, Shipment>> LoadShipmentsAsync(BranchTransfer transfer, CancellationToken cancellationToken)
    {
        var ids = transfer.Items.Select(x => x.ShipmentId).ToList();
        return await Db.Shipments.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    private async Task AddItemsAsync(BranchTransfer transfer, IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
    {
        var resolved = await ShipmentCodeResolver.ResolveAsync(Db, codes, cancellationToken);
        var errors = new List<string>();
        var toAdd = new List<Shipment>();
        long? exceptId = transfer.Id == 0 ? null : transfer.Id;

        foreach (var (code, shipment) in resolved)
        {
            if (shipment is null)
            {
                errors.Add($"{code}: لم يتم العثور على الشحنة.");
                continue;
            }

            if (transfer.Items.Any(x => x.ShipmentId == shipment.Id) || toAdd.Contains(shipment))
            {
                continue;
            }

            var error = ValidateLine(transfer, shipment);
            if (error is not null)
            {
                errors.Add(error);
                continue;
            }

            var otherTransfer = await ShipmentDocumentGuards.FindOpenTransferAsync(Db, shipment.Id, exceptId, cancellationToken);
            var otherManifest = otherTransfer ?? await ShipmentDocumentGuards.FindOpenDriverManifestAsync(Db, shipment.Id, null, cancellationToken);
            if (otherManifest is not null)
            {
                errors.Add($"{shipment.TrackingNumber}: موجودة على مستند مفتوح آخر ({otherManifest}).");
                continue;
            }

            toAdd.Add(shipment);
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["codes"] = errors.ToArray() });
        }

        foreach (var shipment in toAdd)
        {
            transfer.Items.Add(new BranchTransferItem
            {
                BranchTransfer = transfer,
                ShipmentId = shipment.Id,
                ItemStatus = BranchTransferItemStatus.Pending
            });
        }
    }

    private static string? ValidateLine(BranchTransfer transfer, Shipment shipment)
    {
        if (shipment.CurrentBranchId != transfer.FromBranchId)
        {
            return $"{shipment.TrackingNumber}: الشحنة ليست في فرع الإرسال.";
        }

        return ShipmentStatusChanger.CanChange(shipment, ShipmentStatus.InTransit, ShipmentStatusChangeSource.BranchTransfer)
            ? null
            : $"{shipment.TrackingNumber}: لا يمكن تحويلها وهي في حالة «{ShipmentStatusNames.Arabic(shipment.Status)}».";
    }

    private async Task EnsureActiveBranchAsync(long branchId, string fieldName, CancellationToken cancellationToken)
    {
        if (!await Db.Branches.AnyAsync(x => x.Id == branchId && x.IsActive, cancellationToken))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                [fieldName] = new[] { "الفرع غير موجود أو غير مفعّل." }
            });
        }
    }

    private async Task ValidateTransportAsync(long? vehicleId, long? driverId, CancellationToken cancellationToken)
    {
        if (vehicleId.HasValue)
        {
            await DriverRules.EnsureUsableVehicleAsync(Db, vehicleId.Value, null, cancellationToken);
        }

        if (driverId.HasValue)
        {
            await DriverRules.GetUsableDriverAsync(Db, driverId.Value, null, _dependencies.DateTimeProvider.UtcNow, cancellationToken);
        }
    }

    private static BranchTransferDto WithItems(BranchTransferDto x, IReadOnlyList<BranchTransferItemDto> items) => new()
    {
        Id = x.Id,
        TransferNumber = x.TransferNumber,
        FromBranchId = x.FromBranchId,
        FromBranchName = x.FromBranchName,
        ToBranchId = x.ToBranchId,
        ToBranchName = x.ToBranchName,
        VehicleId = x.VehicleId,
        VehiclePlateNumber = x.VehiclePlateNumber,
        DriverId = x.DriverId,
        DriverName = x.DriverName,
        SealNumber = x.SealNumber,
        Status = x.Status,
        ItemCount = x.ItemCount,
        ReceivedCount = x.ReceivedCount,
        MissingCount = x.MissingCount,
        DispatchedAtUtc = x.DispatchedAtUtc,
        DispatchedByUserId = x.DispatchedByUserId,
        ReceivedAtUtc = x.ReceivedAtUtc,
        ReceivedByUserId = x.ReceivedByUserId,
        Notes = x.Notes,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        Items = items
    };

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
