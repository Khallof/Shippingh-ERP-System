using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;

namespace ShippingERP.Application.Interfaces.Shipments;

// Shipment operations (step 4). Every service applies the same visibility rules:
// - users without Branches.ViewAll see shipments whose origin, current or destination branch is their branch;
// - a driver (Driver role without Shipments.AssignDriver) sees only shipments / manifests assigned to him.
// A record outside the caller's scope is reported as 404 (it is not revealed that it exists).

public interface IShipmentService
{
    Task<PagedResult<ShipmentListItemDto>> GetPagedAsync(ShipmentListQuery query, CancellationToken cancellationToken);

    Task<ShipmentDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>Tracking view by tracking number, shipment number or package barcode.</summary>
    Task<ShipmentTrackingDto> TrackAsync(string code, CancellationToken cancellationToken);

    /// <summary>Calculates weights and price without saving anything.</summary>
    Task<ShipmentPricingDto> QuoteAsync(SaveShipmentRequest request, CancellationToken cancellationToken);

    Task<ShipmentDto> CreateAsync(SaveShipmentRequest request, CancellationToken cancellationToken);

    /// <summary>Allowed only while the shipment has not left the origin branch (Created ... InWarehouse).</summary>
    Task<ShipmentDto> UpdateAsync(long id, SaveShipmentRequest request, CancellationToken cancellationToken);

    /// <summary>Scan-style change: PickedUp, ReceivedAtBranch or InWarehouse.</summary>
    Task<ShipmentDto> ChangeStatusAsync(long id, ChangeShipmentStatusRequest request, CancellationToken cancellationToken);

    Task<ShipmentDto> CancelAsync(long id, CancelShipmentRequest request, CancellationToken cancellationToken);

    Task<ShipmentDto> AssignDriverAsync(long id, AssignDriverRequest request, CancellationToken cancellationToken);

    Task<ShipmentDto> CorrectStatusAsync(long id, CorrectShipmentStatusRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShipmentStatusHistoryDto>> GetHistoryAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShipmentPricingDto>> GetPricingHistoryAsync(long id, CancellationToken cancellationToken);
}

public interface IShipmentAttachmentService
{
    Task<IReadOnlyList<ShipmentAttachmentDto>> GetAllAsync(long shipmentId, CancellationToken cancellationToken);

    Task<ShipmentAttachmentDto> UploadAsync(
        long shipmentId,
        UploadShipmentAttachmentRequest request,
        UploadedFile file,
        CancellationToken cancellationToken);

    Task<FileDownload> DownloadAsync(long shipmentId, long attachmentId, CancellationToken cancellationToken);

    /// <summary>Soft delete. Signature and delivery photo files of a proof of delivery cannot be deleted.</summary>
    Task DeleteAsync(long shipmentId, long attachmentId, CancellationToken cancellationToken);
}

public interface IDeliveryService
{
    /// <summary>Confirms the delivery and records the proof of delivery (signature and photo are optional).</summary>
    Task<ProofOfDeliveryDto> DeliverAsync(
        long shipmentId,
        DeliverShipmentRequest request,
        UploadedFile? signature,
        UploadedFile? photo,
        CancellationToken cancellationToken);

    Task<ShipmentDto> RecordFailureAsync(long shipmentId, RecordDeliveryFailureRequest request, CancellationToken cancellationToken);

    /// <summary>The latest proof of delivery of the shipment.</summary>
    Task<ProofOfDeliveryDto> GetProofOfDeliveryAsync(long shipmentId, CancellationToken cancellationToken);
}

public interface IWarehouseManifestService
{
    Task<PagedResult<WarehouseManifestDto>> GetPagedAsync(WarehouseManifestListQuery query, CancellationToken cancellationToken);
    Task<WarehouseManifestDto> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<WarehouseManifestDto> CreateAsync(CreateWarehouseManifestRequest request, CancellationToken cancellationToken);
    Task<WarehouseManifestDto> AddShipmentsAsync(long id, ShipmentCodesRequest request, CancellationToken cancellationToken);
    Task<WarehouseManifestDto> RemoveShipmentAsync(long id, long shipmentId, CancellationToken cancellationToken);
    /// <summary>Applies the movement to every shipment in one transaction.</summary>
    Task<WarehouseManifestDto> ConfirmAsync(long id, CancellationToken cancellationToken);
    Task<WarehouseManifestDto> CancelAsync(long id, CancellationToken cancellationToken);
}

public interface IBranchTransferService
{
    Task<PagedResult<BranchTransferDto>> GetPagedAsync(BranchTransferListQuery query, CancellationToken cancellationToken);
    Task<BranchTransferDto> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<BranchTransferDto> CreateAsync(CreateBranchTransferRequest request, CancellationToken cancellationToken);
    Task<BranchTransferDto> AddShipmentsAsync(long id, ShipmentCodesRequest request, CancellationToken cancellationToken);
    Task<BranchTransferDto> RemoveShipmentAsync(long id, long shipmentId, CancellationToken cancellationToken);
    Task<BranchTransferDto> DispatchAsync(long id, DispatchBranchTransferRequest request, CancellationToken cancellationToken);
    Task<BranchTransferDto> ReceiveAsync(long id, ReceiveBranchTransferRequest request, CancellationToken cancellationToken);
    Task<BranchTransferDto> CancelAsync(long id, CancellationToken cancellationToken);
}

public interface IDriverManifestService
{
    Task<PagedResult<DriverManifestDto>> GetPagedAsync(DriverManifestListQuery query, CancellationToken cancellationToken);
    Task<DriverManifestDto> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<DriverManifestDto> CreateAsync(CreateDriverManifestRequest request, CancellationToken cancellationToken);
    Task<DriverManifestDto> AddShipmentsAsync(long id, ShipmentCodesRequest request, CancellationToken cancellationToken);
    Task<DriverManifestDto> RemoveShipmentAsync(long id, long shipmentId, CancellationToken cancellationToken);
    /// <summary>Hands the manifest to the driver: every shipment becomes OutForDelivery.</summary>
    Task<DriverManifestDto> DispatchAsync(long id, CancellationToken cancellationToken);
    /// <summary>Closes the manifest; pending shipments become DeliveryFailed.</summary>
    Task<DriverManifestDto> CloseAsync(long id, CloseDriverManifestRequest request, CancellationToken cancellationToken);
    Task<DriverManifestDto> CancelAsync(long id, CancellationToken cancellationToken);
}

public interface IShipmentReturnService
{
    Task<PagedResult<ShipmentReturnDto>> GetPagedAsync(ShipmentReturnListQuery query, CancellationToken cancellationToken);
    Task<ShipmentReturnDto> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<ShipmentReturnDto> CreateAsync(CreateShipmentReturnRequest request, CancellationToken cancellationToken);
    /// <summary>AfterDelivery returns: the delivered shipment is back at a branch.</summary>
    Task<ShipmentReturnDto> ReceiveBackAsync(long id, ReceiveShipmentReturnRequest request, CancellationToken cancellationToken);
    /// <summary>The sender got the shipment back: status Returned.</summary>
    Task<ShipmentReturnDto> CompleteAsync(long id, CompleteShipmentReturnRequest request, CancellationToken cancellationToken);
    Task<ShipmentReturnDto> CancelAsync(long id, CancelShipmentReturnRequest request, CancellationToken cancellationToken);
}

public interface IDamageCaseService
{
    Task<PagedResult<DamageCaseDto>> GetPagedAsync(DamageCaseListQuery query, CancellationToken cancellationToken);
    Task<DamageCaseDto> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<DamageCaseDto> CreateAsync(CreateDamageCaseRequest request, CancellationToken cancellationToken);
    Task<DamageCaseDto> StartInvestigationAsync(long id, InvestigateDamageCaseRequest request, CancellationToken cancellationToken);
    Task<DamageCaseDto> ResolveAsync(long id, ResolveDamageCaseRequest request, CancellationToken cancellationToken);
}

public interface IShipmentLabelService
{
    Task<FileDownload> GenerateAsync(long shipmentId, LabelFormat format, CancellationToken cancellationToken);

    Task<FileDownload> GenerateBulkAsync(BulkLabelRequest request, CancellationToken cancellationToken);
}

public interface IShipmentImportService
{
    /// <summary>An .xlsx file with the expected columns, one example row and an instructions sheet.</summary>
    FileDownload GetTemplate();

    /// <summary>Reads and validates every row; nothing is saved.</summary>
    Task<ShipmentImportPreviewDto> PreviewAsync(UploadedFile file, ImportShipmentsRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Validates again and creates ALL shipments in one transaction. If any row is invalid nothing is
    /// imported and a 400 VALIDATION_ERROR lists the row errors ("row 5": [...]).
    /// </summary>
    Task<ShipmentImportResultDto> ImportAsync(UploadedFile file, ImportShipmentsRequest request, CancellationToken cancellationToken);
}

public interface IShippingSettingsService
{
    Task<ShippingSettingsDto> GetAsync(CancellationToken cancellationToken);

    Task<ShippingSettingsDto> SaveAsync(SaveShippingSettingsRequest request, CancellationToken cancellationToken);
}
