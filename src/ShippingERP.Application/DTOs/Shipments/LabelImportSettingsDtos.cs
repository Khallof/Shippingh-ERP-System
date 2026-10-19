namespace ShippingERP.Application.DTOs.Shipments;

// ------------------------------------------------------------------ labels

public enum LabelFormat
{
    /// <summary>A PDF page per package, at the configured thermal label size.</summary>
    Pdf = 1,

    /// <summary>A self-contained printable HTML page (inline SVG barcodes), printed from the browser.</summary>
    Html = 2
}

/// <summary>Body of POST /api/v1/shipments/labels (up to 100 shipments in one file).</summary>
public sealed class BulkLabelRequest
{
    public List<long> ShipmentIds { get; set; } = new();

    public LabelFormat Format { get; set; } = LabelFormat.Pdf;
}

// ------------------------------------------------------------------ Excel import

/// <summary>Form field sent with the Excel file.</summary>
public class ImportShipmentsRequest
{
    /// <summary>The branch registering the shipments (default: the caller's branch).</summary>
    public long? OriginBranchId { get; set; }
}

public sealed class ShipmentImportRowDto
{
    /// <summary>Excel row number (the header is row 1).</summary>
    public int RowNumber { get; init; }
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public string? ExternalReference { get; init; }
    public string? SenderCustomerCode { get; init; }
    public string? RecipientCustomerCode { get; init; }
    public string? DestinationCityCode { get; init; }
    public string? ServiceTypeCode { get; init; }
    public int PackageCount { get; init; }
    public decimal WeightKg { get; init; }
    public bool IsCOD { get; init; }
    public decimal CODAmount { get; init; }
    /// <summary>Calculated price (valid rows only).</summary>
    public decimal? TotalCharge { get; init; }
    public string? CurrencyCode { get; init; }
}

public sealed class ShipmentImportPreviewDto
{
    public string FileName { get; init; } = string.Empty;
    public int TotalRows { get; init; }
    public int ValidRows { get; init; }
    public int InvalidRows { get; init; }
    /// <summary>True when every row is valid (the import accepts all rows or none).</summary>
    public bool CanImport { get; init; }
    public IReadOnlyList<ShipmentImportRowDto> Rows { get; init; } = Array.Empty<ShipmentImportRowDto>();
}

public sealed class ImportedShipmentDto
{
    public int RowNumber { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
}

public sealed class ShipmentImportResultDto
{
    public int ImportedCount { get; init; }
    public IReadOnlyList<ImportedShipmentDto> Shipments { get; init; } = Array.Empty<ImportedShipmentDto>();
}

// ------------------------------------------------------------------ settings

public sealed class ShippingSettingsDto
{
    public int VolumetricDivisor { get; init; }
    public decimal WeightRoundingStepKg { get; init; }
    public string TrackingNumberPrefix { get; init; } = string.Empty;
    public int LabelWidthMm { get; init; }
    public int LabelHeightMm { get; init; }
    public string? TrackingUrlTemplate { get; init; }
    public decimal PriceOverrideApprovalThresholdPercent { get; init; }
    public int DefaultPaymentTermsDays { get; init; }
    /// <summary>True when nothing was saved yet and the defaults are used.</summary>
    public bool IsDefault { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

/// <summary>Body of PUT /api/v1/shipping-settings.</summary>
public sealed class SaveShippingSettingsRequest
{
    public int VolumetricDivisor { get; set; } = 5000;
    public decimal WeightRoundingStepKg { get; set; } = 0.5m;
    public string TrackingNumberPrefix { get; set; } = "SE";
    public int LabelWidthMm { get; set; } = 100;
    public int LabelHeightMm { get; set; } = 150;
    /// <summary>Optional; must contain {trackingNumber}, e.g. https://track.example.com/{trackingNumber}.</summary>
    public string? TrackingUrlTemplate { get; set; }

    /// <summary>Manual price changes above this percentage need Pricing.ApproveOverride (0 to 100).</summary>
    public decimal PriceOverrideApprovalThresholdPercent { get; set; } = 20m;

    /// <summary>Payment terms of new invoices (0 to 365 days).</summary>
    public int DefaultPaymentTermsDays { get; set; } = 30;
}
