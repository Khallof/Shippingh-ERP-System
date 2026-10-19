using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using ZXing.Rendering;

namespace ShippingERP.Infrastructure.Shipments.Labels;

/// <summary>
/// Barcodes as SVG text (ZXing.Net, Apache-2.0). SVG is vector: sharp on 203/300 dpi thermal printers and used
/// as-is in both the PDF (QuestPDF) and the printable HTML label.
/// The human-readable number is printed separately by the label, so the SVG contains bars only.
/// </summary>
public static class BarcodeGenerator
{
    /// <summary>Code 128 (the standard for courier labels). The SVG stretches to the box it is drawn in.</summary>
    public static string Code128Svg(string content)
    {
        var writer = new BarcodeWriterSvg
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                PureBarcode = true,
                Margin = 0,
                Height = 40
            }
        };

        var svg = CleanSvg(writer.Write(content));
        return svg.Replace("<svg ", "<svg preserveAspectRatio=\"none\" ", StringComparison.Ordinal);
    }

    /// <summary>QR code (error correction M, UTF-8), square.</summary>
    public static string QrSvg(string content)
    {
        var writer = new BarcodeWriterSvg
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Margin = 0,
                Width = 0,
                Height = 0,
                ErrorCorrection = ErrorCorrectionLevel.M,
                CharacterSet = "UTF-8"
            }
        };

        return CleanSvg(writer.Write(content));
    }

    /// <summary>Removes the XML declaration / DOCTYPE / comment so the SVG can be embedded inline.</summary>
    private static string CleanSvg(SvgRenderer.SvgImage image)
    {
        var content = image.Content;
        var start = content.IndexOf("<svg", StringComparison.Ordinal);
        return start > 0 ? content[start..] : content;
    }
}
