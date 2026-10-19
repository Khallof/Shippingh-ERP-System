using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Files;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>
/// Delivery confirmation (proof of delivery) and failed delivery attempts.
///
/// - A driver-only user can act only on shipments assigned to him that are OutForDelivery.
/// - Branch staff (Shipments.ConfirmDelivery) can also confirm a counter pickup: the recipient collects the
///   shipment at the branch (ReceivedAtBranch / InWarehouse / ArrivedAtDestination / DeliveryFailed).
/// - The matching line of the open driver manifest is updated automatically.
/// - Files are saved first; if the database transaction fails they are deleted again.
/// </summary>
public sealed class DeliveryService : IDeliveryService
{
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentAccessService _access;
    private readonly ShipmentStatusChanger _statusChanger;
    private readonly ShipmentAttachmentService _attachments;
    private readonly IFileStorage _fileStorage;

    public DeliveryService(
        MasterDataServiceDependencies dependencies,
        ShipmentAccessService access,
        ShipmentStatusChanger statusChanger,
        ShipmentAttachmentService attachments,
        IFileStorage fileStorage)
    {
        _dependencies = dependencies;
        _access = access;
        _statusChanger = statusChanger;
        _attachments = attachments;
        _fileStorage = fileStorage;
    }

    private AppDbContext Db => _dependencies.DbContext;

    public async Task<ProofOfDeliveryDto> DeliverAsync(
        long shipmentId,
        DeliverShipmentRequest request,
        UploadedFile? signature,
        UploadedFile? photo,
        CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(shipmentId, cancellationToken);
        await EnsureCanActAsync(shipment, cancellationToken);

        if (!ShipmentStatusRules.CanTransition(shipment.Status, ShipmentStatus.Delivered, ShipmentStatusChangeSource.Delivery))
        {
            throw new BusinessRuleException(ShipmentStatusChanger.TransitionError(shipment.ShipmentNumber, shipment.Status, ShipmentStatus.Delivered));
        }

        if (shipment.ReturnBranchId.HasValue)
        {
            throw new BusinessRuleException("الشحنة عليها مرتجع مفتوح؛ لا يمكن تسليمها للمستلم. أغلق المرتجع أو ألغِه أولاً.");
        }

        var now = _dependencies.DateTimeProvider.UtcNow;
        var deliveredAt = request.DeliveredAtUtc ?? now;
        if (deliveredAt > now + FutureTolerance || deliveredAt < shipment.CreatedAtUtc)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["deliveredAtUtc"] = new[] { "وقت التسليم يجب أن يكون بين وقت إنشاء الشحنة والآن." }
            });
        }

        var manifestItem = await FindOpenManifestItemAsync(shipment.Id, cancellationToken);
        var driverId = manifestItem?.Manifest.DriverId ?? (shipment.Status == ShipmentStatus.OutForDelivery ? shipment.AssignedDriverId : null);

        // Files first (outside the transaction); removed again if the transaction fails.
        var storedKeys = new List<string>();
        ShipmentAttachment? signatureAttachment = null;
        ShipmentAttachment? photoAttachment = null;

        try
        {
            if (signature is not null)
            {
                signatureAttachment = await _attachments.SaveFileAsync(
                    shipment, signature, ShipmentAttachmentType.Signature, "توقيع المستلم", null, "signature", cancellationToken);
                storedKeys.Add(signatureAttachment.StorageKey);
            }

            if (photo is not null)
            {
                photoAttachment = await _attachments.SaveFileAsync(
                    shipment, photo, ShipmentAttachmentType.DeliveryPhoto, "صورة التسليم", null, "photo", cancellationToken);
                storedKeys.Add(photoAttachment.StorageKey);
            }

            var proof = new ProofOfDelivery
            {
                Shipment = shipment,
                DriverId = driverId,
                DriverManifestId = manifestItem?.ManifestId,
                RecipientName = request.RecipientName.Trim(),
                RecipientRelation = Optional(request.RecipientRelation),
                DeliveredAtUtc = deliveredAt,
                SignatureAttachment = signatureAttachment,
                PhotoAttachment = photoAttachment,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Notes = Optional(request.Notes)
            };

            var proofId = await Db.ExecuteInTransactionAsync(async ct =>
            {
                if (signatureAttachment is not null)
                {
                    Db.ShipmentAttachments.Add(signatureAttachment);
                }

                if (photoAttachment is not null)
                {
                    Db.ShipmentAttachments.Add(photoAttachment);
                }

                Db.ProofsOfDelivery.Add(proof);

                _statusChanger.Change(shipment, ShipmentStatus.Delivered, new StatusChange(ShipmentStatusChangeSource.Delivery)
                {
                    OccurredAtUtc = deliveredAt,
                    DriverId = driverId,
                    Notes = $"المستلم: {proof.RecipientName}",
                    ReferenceType = manifestItem is null ? null : "DriverDeliveryManifest",
                    ReferenceId = manifestItem?.ManifestId,
                    ReferenceNumber = manifestItem?.Manifest.ManifestNumber
                });

                if (manifestItem is not null)
                {
                    manifestItem.Result = DeliveryResult.Delivered;
                    manifestItem.ResultAtUtc = deliveredAt;
                    manifestItem.ProofOfDelivery = proof;
                }

                await Db.SaveChangesAsync(ct);
                return proof.Id;
            }, cancellationToken);

            return await GetProofAsync(proofId, cancellationToken);
        }
        catch
        {
            foreach (var key in storedKeys)
            {
                if (await Db.ShipmentAttachments.IgnoreQueryFilters().AnyAsync(x => x.StorageKey == key, CancellationToken.None))
                {
                    continue;
                }

                await _fileStorage.DeleteAsync(key, CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<ShipmentDto> RecordFailureAsync(long shipmentId, RecordDeliveryFailureRequest request, CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(shipmentId, cancellationToken);
        await EnsureCanActAsync(shipment, cancellationToken);

        var manifestItem = await FindOpenManifestItemAsync(shipment.Id, cancellationToken);
        var occurredAt = request.OccurredAtUtc ?? _dependencies.DateTimeProvider.UtcNow;
        var reason = request.Reason.Trim();
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? reason : $"{reason} - {request.Notes.Trim()}";

        _statusChanger.Change(shipment, ShipmentStatus.DeliveryFailed, new StatusChange(ShipmentStatusChangeSource.Delivery)
        {
            OccurredAtUtc = occurredAt,
            Notes = notes,
            ReferenceType = manifestItem is null ? null : "DriverDeliveryManifest",
            ReferenceId = manifestItem?.ManifestId,
            ReferenceNumber = manifestItem?.Manifest.ManifestNumber
        });

        if (manifestItem is not null)
        {
            manifestItem.Result = DeliveryResult.Failed;
            manifestItem.ResultAtUtc = occurredAt;
            manifestItem.FailureReason = reason.Length > 300 ? reason[..300] : reason;
        }

        await Db.SaveChangesAsync(cancellationToken);

        var shipmentService = new ShipmentReadHelper(Db, _access);
        return await shipmentService.GetAsync(shipmentId, cancellationToken);
    }

    public async Task<ProofOfDeliveryDto> GetProofOfDeliveryAsync(long shipmentId, CancellationToken cancellationToken)
    {
        await _access.EnsureVisibleAsync(shipmentId, cancellationToken);

        var proofId = await Db.ProofsOfDelivery.AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId)
            .OrderByDescending(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("لا يوجد إثبات تسليم لهذه الشحنة.");

        return await GetProofAsync(proofId, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task EnsureCanActAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        if (_access.IsDriverOnly)
        {
            // Visibility already limits the driver to his assigned shipments.
            if (shipment.Status != ShipmentStatus.OutForDelivery)
            {
                throw new BusinessRuleException("السائق يمكنه تأكيد تسليم الشحنات الخارجة معه للتوصيل فقط.");
            }

            var driverId = await _access.GetCurrentDriverIdAsync(cancellationToken);
            if (driverId is null || shipment.AssignedDriverId != driverId)
            {
                throw new ForbiddenException("هذه الشحنة غير مسندة إليك.");
            }

            return;
        }

        _access.EnsureCanOperate(shipment);
    }

    private Task<DriverDeliveryManifestItem?> FindOpenManifestItemAsync(long shipmentId, CancellationToken cancellationToken) =>
        Db.DriverDeliveryManifestItems
            .Include(x => x.Manifest)
            .Where(x => x.ShipmentId == shipmentId &&
                        x.Result == DeliveryResult.Pending &&
                        x.Manifest.Status == DriverManifestStatus.Dispatched)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<ProofOfDeliveryDto> GetProofAsync(long proofId, CancellationToken cancellationToken)
    {
        var proof = await Db.ProofsOfDelivery.AsNoTracking()
            .Where(x => x.Id == proofId)
            .Select(x => new ProofOfDeliveryDto
            {
                Id = x.Id,
                ShipmentId = x.ShipmentId,
                ShipmentNumber = x.Shipment.ShipmentNumber,
                DriverId = x.DriverId,
                DriverName = x.Driver != null ? x.Driver.Employee.ArabicName : null,
                DriverManifestId = x.DriverManifestId,
                RecipientName = x.RecipientName,
                RecipientRelation = x.RecipientRelation,
                DeliveredAtUtc = x.DeliveredAtUtc,
                SignatureAttachmentId = x.SignatureAttachmentId,
                PhotoAttachmentId = x.PhotoAttachmentId,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                Notes = x.Notes,
                CreatedAtUtc = x.CreatedAtUtc,
                CreatedByUserId = x.CreatedByUserId
            })
            .FirstAsync(cancellationToken);

        return new ProofOfDeliveryDto
        {
            Id = proof.Id,
            ShipmentId = proof.ShipmentId,
            ShipmentNumber = proof.ShipmentNumber,
            DriverId = proof.DriverId,
            DriverName = proof.DriverName,
            DriverManifestId = proof.DriverManifestId,
            RecipientName = proof.RecipientName,
            RecipientRelation = proof.RecipientRelation,
            DeliveredAtUtc = proof.DeliveredAtUtc,
            SignatureAttachmentId = proof.SignatureAttachmentId,
            SignatureUrl = proof.SignatureAttachmentId.HasValue
                ? ShipmentAttachmentService.DownloadUrl(proof.ShipmentId, proof.SignatureAttachmentId.Value)
                : null,
            PhotoAttachmentId = proof.PhotoAttachmentId,
            PhotoUrl = proof.PhotoAttachmentId.HasValue
                ? ShipmentAttachmentService.DownloadUrl(proof.ShipmentId, proof.PhotoAttachmentId.Value)
                : null,
            Latitude = proof.Latitude,
            Longitude = proof.Longitude,
            Notes = proof.Notes,
            CreatedAtUtc = proof.CreatedAtUtc,
            CreatedByUserId = proof.CreatedByUserId
        };
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Reads a ShipmentDto with the caller's visibility (used by services that return the updated shipment).</summary>
public sealed class ShipmentReadHelper
{
    private readonly AppDbContext _dbContext;
    private readonly ShipmentAccessService _access;

    public ShipmentReadHelper(AppDbContext dbContext, ShipmentAccessService access)
    {
        _dbContext = dbContext;
        _access = access;
    }

    public async Task<ShipmentDto> GetAsync(long shipmentId, CancellationToken cancellationToken)
    {
        var query = await _access.ScopeAsync(_dbContext.Shipments.AsNoTracking(), cancellationToken);
        return await query
                   .Where(x => x.Id == shipmentId)
                   .Select(ShipmentProjections.Detail)
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("الشحنة", shipmentId);
    }
}
