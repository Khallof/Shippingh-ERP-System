using System.Globalization;
using System.Net;
using System.Text;

namespace ShippingERP.Infrastructure.Shipments.Labels;

/// <summary>
/// The same labels as a self-contained printable HTML page (open it and press Ctrl+P). Every user value is
/// HTML-encoded; barcodes are inline SVG; a strict Content-Security-Policy blocks scripts and external resources.
/// Set the printer to the label size and "no margins" for best results.
/// </summary>
public sealed class HtmlLabelRenderer
{
    public string Render(IReadOnlyList<ShipmentLabelData> labels, LabelOptions options)
    {
        var width = options.WidthMm.ToString(CultureInfo.InvariantCulture);
        var height = options.HeightMm.ToString(CultureInfo.InvariantCulture);
        var scale = options.Scale;

        string Pt(float size) => (size * scale).ToString("0.##", CultureInfo.InvariantCulture) + "pt";

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"ar\" dir=\"rtl\"><head><meta charset=\"utf-8\">");
        html.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:\">");
        html.Append("<title>").Append(Encode(labels.Count == 1 ? labels[0].ShipmentNumber : "ملصقات الشحنات")).Append("</title><style>");
        html.Append("@page{size:").Append(width).Append("mm ").Append(height).Append("mm;margin:0}");
        html.Append("*{box-sizing:border-box}body{margin:0;font-family:'Cairo','Tahoma','Arial',sans-serif;color:#000;background:#fff}");
        html.Append(".label{width:").Append(width).Append("mm;height:").Append(height).Append("mm;padding:3mm;overflow:hidden;page-break-after:always;break-after:page;display:flex;flex-direction:column;gap:1.2mm;line-height:1.25;font-size:").Append(Pt(8)).Append('}');
        html.Append(".row{display:flex;justify-content:space-between;align-items:center;gap:2mm}");
        html.Append(".box{border:1px solid #000;padding:1.5mm}.thin{border-width:.5px}");
        html.Append(".b{font-weight:700}.small{font-size:").Append(Pt(7)).Append('}');
        html.Append(".service{border:1px solid #000;padding:0 2mm;font-weight:700}");
        html.Append(".logo{height:9mm;max-width:20mm;object-fit:contain;vertical-align:middle;margin-inline-end:1.5mm}");
        html.Append(".barcode{height:").Append((options.HeightMm * 0.11f).ToString("0.##", CultureInfo.InvariantCulture)).Append("mm;flex:none}.barcode svg{width:100%;height:100%}");
        html.Append(".tracking{text-align:center;font-weight:700;letter-spacing:.08em;direction:ltr;font-size:").Append(Pt(13)).Append('}');
        html.Append(".recipient{display:flex;gap:2mm}.recipient .info{flex:1}.name{font-weight:700;font-size:").Append(Pt(11)).Append('}');
        html.Append(".dest{font-weight:700;font-size:").Append(Pt(10)).Append('}');
        html.Append(".code{width:22%;background:#000;color:#fff;font-weight:700;display:flex;align-items:center;justify-content:center;font-size:").Append(Pt(16)).Append(";-webkit-print-color-adjust:exact;print-color-adjust:exact}");
        html.Append(".cod{background:#000;color:#fff;text-align:center;font-weight:700;padding:1mm;-webkit-print-color-adjust:exact;print-color-adjust:exact;font-size:").Append(Pt(12)).Append('}');
        html.Append(".nocod{text-align:center}.qr{width:").Append((options.HeightMm * 0.13f).ToString("0.##", CultureInfo.InvariantCulture)).Append("mm;flex:none}.qr svg{width:100%;height:auto;display:block}");
        html.Append(".ltr{direction:ltr;unicode-bidi:embed}.amount{font-size:").Append(Pt(14)).Append('}');
        // On screen: grey page with centred labels (printing uses the @page size, without margins).
        html.Append("@media screen{body{background:#e5e7eb;padding:4mm 0}.label{margin:0 auto 4mm;background:#fff;box-shadow:0 1px 4px rgba(0,0,0,.25)}}");
        html.Append("</style></head><body>");

        foreach (var label in labels)
        {
            var barcode = BarcodeGenerator.Code128Svg(label.TrackingNumber);
            var qr = BarcodeGenerator.QrSvg(options.QrContent(label.TrackingNumber));

            foreach (var package in label.Packages)
            {
                html.Append("<section class=\"label\">");

                html.Append("<div class=\"row\"><span class=\"b\">");
                if (options.Logo is { Length: > 0 })
                {
                    // Step 9: the company logo before the name (embedded, so the printout needs no network).
                    html.Append("<img class=\"logo\" alt=\"\" src=\"data:").Append(options.LogoContentType ?? "image/png").Append(";base64,")
                        .Append(Convert.ToBase64String(options.Logo)).Append("\"/> ");
                }

                html.Append(Encode(LabelText.Clip(label.CompanyName, 40)))
                    .Append("</span><span class=\"service\">").Append(Encode(LabelText.Clip(label.ServiceTypeName, 20))).Append("</span></div>");

                html.Append("<div class=\"barcode\">").Append(barcode).Append("</div>");
                html.Append("<div class=\"tracking\">").Append(Encode(label.TrackingNumber)).Append("</div>");

                html.Append("<div class=\"row\"><span>رقم الشحنة: <span class=\"b ltr\">").Append(Encode(label.ShipmentNumber))
                    .Append("</span></span><span class=\"ltr\">").Append(label.CreatedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("</span></div>");

                html.Append("<div class=\"box recipient\"><div class=\"info\"><div class=\"small\">إلى (المستلم)</div>");
                html.Append("<div class=\"name\">").Append(Encode(LabelText.Clip(label.RecipientName, 45))).Append("</div>");
                html.Append("<div class=\"ltr\" style=\"text-align:right\">").Append(Encode(label.RecipientPhone)).Append("</div>");
                if (!string.IsNullOrWhiteSpace(label.RecipientAddressLine))
                {
                    html.Append("<div>").Append(Encode(LabelText.Clip(label.RecipientAddressLine, 90))).Append("</div>");
                }

                if (!string.IsNullOrWhiteSpace(label.RecipientLandmark))
                {
                    html.Append("<div class=\"small\">قرب: ").Append(Encode(LabelText.Clip(label.RecipientLandmark, 50))).Append("</div>");
                }

                html.Append("<div class=\"dest\">").Append(Encode(LabelText.Clip(label.DestinationText, 60))).Append("</div></div>");
                html.Append("<div class=\"code\">").Append(Encode(label.DestinationBranchCode ?? "--")).Append("</div></div>");

                html.Append("<div class=\"box thin\"><div class=\"small\">من (المرسل)</div><div><span class=\"b\">")
                    .Append(Encode(LabelText.Clip(label.SenderName, 40))).Append("</span> <span class=\"ltr\">").Append(Encode(label.SenderPhone))
                    .Append("</span></div><div class=\"small\">").Append(Encode($"{label.OriginCityName} - {label.OriginBranchCode}")).Append("</div></div>");

                html.Append("<div class=\"row\"><span><span class=\"b\">").Append(Encode($"الطرد {package.SequenceNumber} من {label.PackageCount}"))
                    .Append("</span> <span class=\"small ltr\">").Append(Encode(package.PackageBarcode)).Append("</span></span><span>الوزن المحتسب: ")
                    .Append(Encode(LabelText.Weight(label.ChargeableWeight))).Append("</span></div>");

                if (label.HasCollection)
                {
                    html.Append("<div class=\"cod\"><div>").Append(Encode(label.CollectTitle)).Append("</div><div class=\"ltr amount\">").Append(Encode(label.CollectText)).Append("</div></div>");
                }
                else
                {
                    html.Append("<div class=\"box thin nocod\">لا يوجد مبلغ للتحصيل</div>");
                }

                html.Append("<div class=\"row\" style=\"align-items:flex-start\"><div class=\"qr\">").Append(qr).Append("</div><div style=\"flex:1\">");
                html.Append("<div class=\"small\">المحتوى: ").Append(Encode(LabelText.Clip(label.PackageDescription, 60))).Append("</div>");
                if (!string.IsNullOrWhiteSpace(label.SenderNotes))
                {
                    html.Append("<div class=\"small\">ملاحظات: ").Append(Encode(LabelText.Clip(label.SenderNotes, 100))).Append("</div>");
                }

                html.Append("</div></div></section>");
            }
        }

        html.Append("</body></html>");
        return html.ToString();
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
