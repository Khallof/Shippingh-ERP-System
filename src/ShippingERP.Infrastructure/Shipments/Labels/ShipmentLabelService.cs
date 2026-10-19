using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments.Labels;

/// <summary>Builds shipment labels (PDF or HTML) for visible shipments. Cancelled shipments get no label.</summary>
public sealed class ShipmentLabelService : IShipmentLabelService
{
    public const string PdfContentType = "application/pdf";
    public const string HtmlContentType = "text/html; charset=utf-8";

    private readonly AppDbContext _dbContext;
    private readonly ShipmentAccessService _access;
    private readonly ShippingSettingsService _settings;
    private readonly PdfLabelRenderer _pdfRenderer;
    private readonly HtmlLabelRenderer _htmlRenderer;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ShipmentLabelService(
        AppDbContext dbContext,
        ShipmentAccessService access,
        ShippingSettingsService settings,
        PdfLabelRenderer pdfRenderer,
        HtmlLabelRenderer htmlRenderer,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _access = access;
        _settings = settings;
        _pdfRenderer = pdfRenderer;
        _htmlRenderer = htmlRenderer;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<FileDownload> GenerateAsync(long shipmentId, LabelFormat format, CancellationToken cancellationToken)
    {
        var labels = await LoadAsync(new[] { shipmentId }, cancellationToken);
        return await RenderAsync(labels, format, $"label-{labels[0].ShipmentNumber}", cancellationToken);
    }

    public async Task<FileDownload> GenerateBulkAsync(BulkLabelRequest request, CancellationToken cancellationToken)
    {
        var labels = await LoadAsync(request.ShipmentIds.Distinct().ToList(), cancellationToken);
        var name = string.Create(CultureInfo.InvariantCulture, $"labels-{_dateTimeProvider.UtcNow:yyyyMMdd-HHmm}");
        return await RenderAsync(labels, request.Format, name, cancellationToken);
    }

    private async Task<FileDownload> RenderAsync(IReadOnlyList<ShipmentLabelData> labels, LabelFormat format, string fileName, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var options = new LabelOptions(settings.LabelWidthMm, settings.LabelHeightMm, settings.TrackingUrlTemplate);

        if (format == LabelFormat.Html)
        {
            var html = _htmlRenderer.Render(labels, options);
            return FileDownload.FromBytes(Encoding.UTF8.GetBytes(html), HtmlContentType, fileName + ".html");
        }

        var pdf = _pdfRenderer.Render(labels, options);
        return FileDownload.FromBytes(pdf, PdfContentType, fileName + ".pdf");
    }

    private async Task<IReadOnlyList<ShipmentLabelData>> LoadAsync(IReadOnlyCollection<long> shipmentIds, CancellationToken cancellationToken)
    {
        var companyName = await _dbContext.CompanyProfiles.AsNoTracking()
                              .OrderBy(x => x.Id)
                              .Select(x => x.ArabicName)
                              .FirstOrDefaultAsync(cancellationToken)
                          ?? "ShippingERP";

        var query = await _access.ScopeAsync(_dbContext.Shipments.AsNoTracking(), cancellationToken);
        var rows = await query
            .Where(x => shipmentIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Status,
                Label = new ShipmentLabelData
                {
                    CompanyName = companyName,
                    ShipmentNumber = x.ShipmentNumber,
                    TrackingNumber = x.TrackingNumber,
                    CreatedAtUtc = x.CreatedAtUtc,
                    ServiceTypeName = x.ServiceType.ArabicName,
                    OriginBranchCode = x.OriginBranch.Code,
                    DestinationBranchCode = x.DestinationBranch != null ? x.DestinationBranch.Code : null,
                    DestinationBranchName = x.DestinationBranch != null ? x.DestinationBranch.ArabicName : null,
                    SenderName = x.SenderCustomer.ArabicName,
                    SenderPhone = x.SenderCustomer.Phone,
                    OriginCityName = x.OriginCity.ArabicName,
                    RecipientName = x.RecipientAddress != null && x.RecipientAddress.ContactName != null
                        ? x.RecipientAddress.ContactName
                        : x.RecipientCustomer.ArabicName,
                    RecipientPhone = x.RecipientAddress != null && x.RecipientAddress.Phone != null
                        ? x.RecipientAddress.Phone
                        : x.RecipientCustomer.Phone,
                    RecipientAddressLine = x.RecipientAddress != null ? x.RecipientAddress.AddressLine : null,
                    RecipientLandmark = x.RecipientAddress != null ? x.RecipientAddress.Landmark : null,
                    DestinationCityName = x.DestinationCity.ArabicName,
                    DestinationZoneName = x.DestinationZone != null ? x.DestinationZone.ArabicName : null,
                    PackageCount = x.PackageCount,
                    ChargeableWeight = x.ChargeableWeight,
                    IsCod = x.IsCOD,
                    CodAmount = x.CODAmount,
                    CurrencyCode = x.Currency.Code,
                    ChargesOnRecipient = x.ChargesPaidBy == ChargesPayer.Recipient,
                    ChargesAmount = x.TotalCharge,
                    PackageDescription = x.PackageDescription,
                    SenderNotes = x.SenderNotes,
                    Packages = x.Packages
                        .OrderBy(p => p.SequenceNumber)
                        .Select(p => new ShipmentLabelPackage(p.SequenceNumber, p.PackageBarcode, p.WeightKg))
                        .ToList()
                }
            })
            .ToListAsync(cancellationToken);

        var missing = shipmentIds.FirstOrDefault(id => rows.All(r => r.Id != id));
        if (missing != 0)
        {
            throw new NotFoundException("الشحنة", missing);
        }

        var cancelled = rows.FirstOrDefault(r => r.Status == ShipmentStatus.Cancelled);
        if (cancelled is not null)
        {
            throw new BusinessRuleException($"الشحنة {cancelled.Label.ShipmentNumber} ملغاة؛ لا يمكن طباعة ملصق لها.");
        }

        // Keep the order requested by the caller.
        return shipmentIds.Select(id => rows.First(r => r.Id == id).Label).ToList();
    }
}
