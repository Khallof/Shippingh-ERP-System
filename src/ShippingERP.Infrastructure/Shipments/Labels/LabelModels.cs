using System.Globalization;

namespace ShippingERP.Infrastructure.Shipments.Labels;

/// <summary>Everything printed on the labels of one shipment (one label per package).</summary>
public sealed class ShipmentLabelData
{
    public string CompanyName { get; init; } = string.Empty;
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public string ServiceTypeName { get; init; } = string.Empty;
    public string OriginBranchCode { get; init; } = string.Empty;
    public string? DestinationBranchCode { get; init; }
    public string? DestinationBranchName { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string SenderPhone { get; init; } = string.Empty;
    public string OriginCityName { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string RecipientPhone { get; init; } = string.Empty;
    public string? RecipientAddressLine { get; init; }
    public string? RecipientLandmark { get; init; }
    public string DestinationCityName { get; init; } = string.Empty;
    public string? DestinationZoneName { get; init; }
    public int PackageCount { get; init; }
    public decimal ChargeableWeight { get; init; }
    public bool IsCod { get; init; }
    public decimal CodAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;

    /// <summary>Step 9: the shipping charges are collected from the recipient.</summary>
    public bool ChargesOnRecipient { get; init; }

    /// <summary>Total shipping charge (collected when ChargesOnRecipient).</summary>
    public decimal ChargesAmount { get; init; }

    public string PackageDescription { get; init; } = string.Empty;
    public string? SenderNotes { get; init; }
    public IReadOnlyList<ShipmentLabelPackage> Packages { get; init; } = Array.Empty<ShipmentLabelPackage>();

    /// <summary>"125,000 SYP"</summary>
    public string CodText => string.Create(CultureInfo.InvariantCulture, $"{CodAmount:#,0.##} {CurrencyCode}");

    /// <summary>Step 9: something to collect from the recipient (COD amount and/or the shipping charges).</summary>
    public bool HasCollection => IsCod || (ChargesOnRecipient && ChargesAmount > 0m);

    /// <summary>COD + the charges when they are on the recipient.</summary>
    public decimal CollectAmount => (IsCod ? CodAmount : 0m) + (ChargesOnRecipient ? ChargesAmount : 0m);

    public string CollectText => string.Create(CultureInfo.InvariantCulture, $"{CollectAmount:#,0.##} {CurrencyCode}");

    public string CollectTitle =>
        IsCod && ChargesOnRecipient && ChargesAmount > 0m ? "المطلوب من المستلم (المبلغ + أجور الشحن)"
        : IsCod ? "الدفع عند الاستلام - COD"
        : "أجور الشحن على المستلم";

    /// <summary>Address line, zone, city on one line.</summary>
    public string DestinationText =>
        string.Join(" - ", new[] { DestinationZoneName, DestinationCityName }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public sealed record ShipmentLabelPackage(int SequenceNumber, string PackageBarcode, decimal WeightKg);

/// <summary>Label size and QR content, from the shipping settings.</summary>
public sealed record LabelOptions(int WidthMm, int HeightMm, string? TrackingUrlTemplate)
{
    /// <summary>What the QR code contains: the tracking page URL when configured, otherwise the tracking number.</summary>
    public string QrContent(string trackingNumber) =>
        string.IsNullOrWhiteSpace(TrackingUrlTemplate)
            ? trackingNumber
            : TrackingUrlTemplate.Replace("{trackingNumber}", Uri.EscapeDataString(trackingNumber), StringComparison.Ordinal);

    /// <summary>Font scale relative to the 100 mm reference label.</summary>
    public float Scale => Math.Clamp(WidthMm / 100f, 0.6f, 2f);
}

/// <summary>Shortens long user text so a label never overflows.</summary>
internal static class LabelText
{
    public static string Clip(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim().Replace('\n', ' ').Replace('\r', ' ');
        return text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
    }

    public static string Weight(decimal kg) => string.Create(CultureInfo.InvariantCulture, $"{kg:0.###} كغ");
}
