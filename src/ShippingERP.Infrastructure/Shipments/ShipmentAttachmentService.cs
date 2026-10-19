using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Files;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>Files attached to a shipment. Access follows the shipment's visibility rules.</summary>
public sealed class ShipmentAttachmentService : IShipmentAttachmentService
{
    public const string StorageFolder = "shipments";

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentAccessService _access;
    private readonly IFileStorage _fileStorage;

    public ShipmentAttachmentService(MasterDataServiceDependencies dependencies, ShipmentAccessService access, IFileStorage fileStorage)
    {
        _dependencies = dependencies;
        _access = access;
        _fileStorage = fileStorage;
    }

    private AppDbContext Db => _dependencies.DbContext;

    public static string DownloadUrl(long shipmentId, long attachmentId) =>
        $"/api/v1/shipments/{shipmentId}/attachments/{attachmentId}/content";

    public async Task<IReadOnlyList<ShipmentAttachmentDto>> GetAllAsync(long shipmentId, CancellationToken cancellationToken)
    {
        await _access.EnsureVisibleAsync(shipmentId, cancellationToken);

        var rows = await Db.ShipmentAttachments.AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<ShipmentAttachmentDto> UploadAsync(
        long shipmentId,
        UploadShipmentAttachmentRequest request,
        UploadedFile file,
        CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(shipmentId, cancellationToken);

        if (request.DamageCaseId.HasValue)
        {
            var caseId = request.DamageCaseId.Value;
            if (!await Db.ShipmentDamageOrLossCases.AnyAsync(x => x.Id == caseId && x.ShipmentId == shipmentId, cancellationToken))
            {
                throw new RequestValidationException(new Dictionary<string, string[]>
                {
                    ["damageCaseId"] = new[] { "حالة التلف لا تخص هذه الشحنة." }
                });
            }
        }

        var attachment = await SaveFileAsync(shipment, file, request.AttachmentType, request.Description, request.DamageCaseId, "file", cancellationToken);

        try
        {
            Db.ShipmentAttachments.Add(attachment);
            await Db.SaveChangesAsync(cancellationToken);

            _dependencies.AuditLog.Add(
                AuditAction.ShipmentAttachmentUploaded,
                entityName: "ShipmentAttachment",
                entityId: attachment.Id.ToString(),
                details: new { shipmentId, attachment.AttachmentType, attachment.OriginalFileName, attachment.SizeBytes, attachment.Sha256 });
            await Db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (attachment.Id == 0)
            {
                await _fileStorage.DeleteAsync(attachment.StorageKey, CancellationToken.None);
            }

            throw;
        }

        return ToDto(attachment);
    }

    public async Task<FileDownload> DownloadAsync(long shipmentId, long attachmentId, CancellationToken cancellationToken)
    {
        await _access.EnsureVisibleAsync(shipmentId, cancellationToken);

        var attachment = await Db.ShipmentAttachments.AsNoTracking()
                             .FirstOrDefaultAsync(x => x.Id == attachmentId && x.ShipmentId == shipmentId, cancellationToken)
                         ?? throw new NotFoundException("المرفق", attachmentId);

        var stream = await _fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return new FileDownload(stream, attachment.ContentType, attachment.OriginalFileName);
    }

    public async Task DeleteAsync(long shipmentId, long attachmentId, CancellationToken cancellationToken)
    {
        await _access.EnsureVisibleAsync(shipmentId, cancellationToken);

        var attachment = await Db.ShipmentAttachments
                             .FirstOrDefaultAsync(x => x.Id == attachmentId && x.ShipmentId == shipmentId, cancellationToken)
                         ?? throw new NotFoundException("المرفق", attachmentId);

        if (await Db.ProofsOfDelivery.AnyAsync(
                x => x.SignatureAttachmentId == attachmentId || x.PhotoAttachmentId == attachmentId,
                cancellationToken))
        {
            throw new BusinessRuleException("لا يمكن حذف توقيع أو صورة تخص إثبات تسليم.");
        }

        // Soft delete: the file stays in the storage for history.
        Db.ShipmentAttachments.Remove(attachment);
        _dependencies.AuditLog.Add(
            AuditAction.ShipmentAttachmentDeleted,
            entityName: "ShipmentAttachment",
            entityId: attachment.Id.ToString(),
            details: new { shipmentId, attachment.OriginalFileName });

        await Db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Stores the file and returns an (unsaved) attachment entity. Also used by the proof of delivery.</summary>
    public async Task<ShipmentAttachment> SaveFileAsync(
        Shipment shipment,
        UploadedFile file,
        ShipmentAttachmentType type,
        string? description,
        long? damageCaseId,
        string fieldName,
        CancellationToken cancellationToken)
    {
        var stored = await _fileStorage.SaveAsync(file, StorageFolder, fieldName, cancellationToken);

        return new ShipmentAttachment
        {
            Shipment = shipment,
            AttachmentType = type,
            DamageCaseId = damageCaseId,
            OriginalFileName = stored.SafeFileName,
            StorageKey = stored.StorageKey,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            Sha256 = stored.Sha256,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsActive = true
        };
    }

    private static ShipmentAttachmentDto ToDto(ShipmentAttachment x) => new()
    {
        Id = x.Id,
        ShipmentId = x.ShipmentId,
        AttachmentType = x.AttachmentType,
        DamageCaseId = x.DamageCaseId,
        OriginalFileName = x.OriginalFileName,
        ContentType = x.ContentType,
        SizeBytes = x.SizeBytes,
        Description = x.Description,
        DownloadUrl = DownloadUrl(x.ShipmentId, x.Id),
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId
    };
}
