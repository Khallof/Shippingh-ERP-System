using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>
/// A shipment can be on at most one OPEN document of each kind (draft/dispatched transfer, draft/dispatched
/// driver manifest, draft warehouse manifest). These helpers find such documents.
/// </summary>
public static class ShipmentDocumentGuards
{
    /// <summary>Number of the draft or dispatched transfer that still carries the shipment, or null.</summary>
    public static Task<string?> FindOpenTransferAsync(AppDbContext db, long shipmentId, long? exceptTransferId, CancellationToken cancellationToken) =>
        db.BranchTransferItems.AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId &&
                        x.BranchTransferId != exceptTransferId &&
                        (x.ItemStatus == BranchTransferItemStatus.Pending || x.ItemStatus == BranchTransferItemStatus.InTransit) &&
                        (x.BranchTransfer.Status == BranchTransferStatus.Draft || x.BranchTransfer.Status == BranchTransferStatus.Dispatched))
            .Select(x => x.BranchTransfer.TransferNumber)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Number of the draft or dispatched driver manifest where the shipment is still pending, or null.</summary>
    public static Task<string?> FindOpenDriverManifestAsync(AppDbContext db, long shipmentId, long? exceptManifestId, CancellationToken cancellationToken) =>
        db.DriverDeliveryManifestItems.AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId &&
                        x.ManifestId != exceptManifestId &&
                        x.Result == DeliveryResult.Pending &&
                        (x.Manifest.Status == DriverManifestStatus.Draft || x.Manifest.Status == DriverManifestStatus.Dispatched))
            .Select(x => x.Manifest.ManifestNumber)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Number of the draft warehouse manifest that contains the shipment, or null.</summary>
    public static Task<string?> FindDraftWarehouseManifestAsync(AppDbContext db, long shipmentId, long? exceptManifestId, CancellationToken cancellationToken) =>
        db.ShipmentManifestItems.AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId &&
                        x.ManifestId != exceptManifestId &&
                        x.Manifest.Status == WarehouseManifestStatus.Draft)
            .Select(x => x.Manifest.ManifestNumber)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Refuses when the shipment is on any open document (used before cancelling).</summary>
    public static async Task EnsureNotOnOpenDocumentAsync(AppDbContext db, Shipment shipment, CancellationToken cancellationToken)
    {
        var number = await FindOpenTransferAsync(db, shipment.Id, null, cancellationToken)
                     ?? await FindOpenDriverManifestAsync(db, shipment.Id, null, cancellationToken)
                     ?? await FindDraftWarehouseManifestAsync(db, shipment.Id, null, cancellationToken);

        if (number is not null)
        {
            throw new BusinessRuleException($"الشحنة {shipment.ShipmentNumber} موجودة على المستند المفتوح {number}. احذفها منه أولاً.");
        }
    }

    /// <summary>Refuses when the shipment is on a dispatched transfer or driver manifest (used before a correction).</summary>
    public static async Task EnsureNotOnDispatchedDocumentAsync(AppDbContext db, Shipment shipment, CancellationToken cancellationToken)
    {
        var shipmentId = shipment.Id;

        var transfer = await db.BranchTransferItems.AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId &&
                        x.ItemStatus == BranchTransferItemStatus.InTransit &&
                        x.BranchTransfer.Status == BranchTransferStatus.Dispatched)
            .Select(x => x.BranchTransfer.TransferNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var manifest = transfer ?? await db.DriverDeliveryManifestItems.AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId &&
                        x.Result == DeliveryResult.Pending &&
                        x.Manifest.Status == DriverManifestStatus.Dispatched)
            .Select(x => x.Manifest.ManifestNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (manifest is not null)
        {
            throw new BusinessRuleException(
                $"الشحنة {shipment.ShipmentNumber} على المستند {manifest} الذي لم يُغلق بعد. أكمل المستند (استلام التحويل أو إغلاق بيان التوزيع) ثم صحّح الحالة.");
        }
    }
}

/// <summary>Finds shipments by the codes a user scans: tracking number, shipment number or package barcode.</summary>
public static class ShipmentCodeResolver
{
    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Returns one entry per distinct code (in input order) with the tracked shipment, or null when not found.
    /// </summary>
    public static async Task<IReadOnlyList<(string Code, Shipment? Shipment)>> ResolveAsync(
        AppDbContext db,
        IEnumerable<string> codes,
        CancellationToken cancellationToken)
    {
        var normalized = codes.Select(Normalize).Where(code => code.Length > 0).Distinct().ToList();
        if (normalized.Count == 0)
        {
            return Array.Empty<(string, Shipment?)>();
        }

        var shipments = await db.Shipments
            .Where(x => normalized.Contains(x.TrackingNumber) ||
                        normalized.Contains(x.ShipmentNumber) ||
                        x.Packages.Any(p => normalized.Contains(p.PackageBarcode)))
            .ToListAsync(cancellationToken);

        return normalized
            .Select(code => (code, shipments.FirstOrDefault(s =>
                s.TrackingNumber == code ||
                s.ShipmentNumber == code ||
                code.StartsWith(s.TrackingNumber + "-", StringComparison.Ordinal))))
            .ToList();
    }
}

/// <summary>Checks that a driver / vehicle can be used for a branch.</summary>
public static class DriverRules
{
    public sealed record UsableDriver(long Id, long BranchId, long? DefaultVehicleId, string Name);

    public static async Task<UsableDriver> GetUsableDriverAsync(
        AppDbContext db,
        long driverId,
        long? branchId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var driver = await db.DriverProfiles.AsNoTracking()
            .Where(x => x.Id == driverId)
            .Select(x => new
            {
                x.Id,
                x.IsActive,
                EmployeeIsActive = x.Employee.IsActive,
                x.Employee.BranchId,
                x.DefaultVehicleId,
                x.LicenseExpiryDateUtc,
                Name = x.Employee.ArabicName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (driver is null || !driver.IsActive || !driver.EmployeeIsActive)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["driverId"] = new[] { "السائق غير موجود أو غير مفعّل." }
            });
        }

        if (branchId.HasValue && driver.BranchId != branchId)
        {
            throw new BusinessRuleException($"السائق {driver.Name} لا يتبع الفرع الموجودة فيه الشحنات.");
        }

        if (driver.LicenseExpiryDateUtc < utcNow.Date)
        {
            throw new BusinessRuleException($"رخصة قيادة السائق {driver.Name} منتهية. حدّث بيانات الرخصة أولاً.");
        }

        return new UsableDriver(driver.Id, driver.BranchId, driver.DefaultVehicleId, driver.Name);
    }

    public static async Task EnsureUsableVehicleAsync(AppDbContext db, long vehicleId, long? branchId, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.AsNoTracking()
            .Where(x => x.Id == vehicleId)
            .Select(x => new { x.IsActive, x.BranchId, x.PlateNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (vehicle is null || !vehicle.IsActive)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["vehicleId"] = new[] { "المركبة غير موجودة أو غير مفعّلة." }
            });
        }

        if (branchId.HasValue && vehicle.BranchId != branchId)
        {
            throw new BusinessRuleException($"المركبة {vehicle.PlateNumber} لا تتبع هذا الفرع.");
        }
    }
}
