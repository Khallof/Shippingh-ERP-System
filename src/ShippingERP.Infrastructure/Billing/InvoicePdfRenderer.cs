using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Shipments.Labels;

namespace ShippingERP.Infrastructure.Billing;

public sealed record InvoiceCompanyInfo(
    string ArabicName,
    string? EnglishName,
    string? TaxNumber,
    string? CommercialRegistrationNumber,
    string? Phone,
    string? Email,
    string? Address);

/// <summary>
/// Printable A4 invoice (QuestPDF, embedded Cairo font, right-to-left). Contains the company and customer data,
/// one row per shipment, the totals, the payments / credit notes summary and a QR code with the invoice number,
/// date and total. A draft is clearly marked "مسودة - غير صالحة للتحصيل" (draft - not valid for collection).
/// </summary>
public sealed class InvoicePdfRenderer
{
    private const string Font = PdfLabelRenderer.FontFamily;

    public InvoicePdfRenderer()
    {
        PdfLabelRenderer.EnsureInitialized();
    }

    public byte[] Render(InvoiceDto invoice, InvoiceCompanyInfo company)
    {
        var decimals = Math.Clamp(invoice.CurrencyDecimals, 0, 4);
        string Money(decimal value) => value.ToString("N" + decimals, CultureInfo.InvariantCulture);
        string Date(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-";

        var isDraft = invoice.Status == InvoiceStatus.Draft;
        var qr = BarcodeGenerator.QrSvg(string.Join(
            "|",
            invoice.InvoiceNumber ?? $"DRAFT-{invoice.Id}",
            Date(invoice.InvoiceDateUtc),
            Money(invoice.TotalAmount) + " " + invoice.CurrencyCode,
            company.TaxNumber ?? string.Empty));

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(14, Unit.Millimetre);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(style => style.FontFamily(Font).FontSize(9.5f).LineHeight(1.2f));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(company.ArabicName).Bold().FontSize(15);
                            if (!string.IsNullOrWhiteSpace(company.EnglishName))
                            {
                                c.Item().Text(company.EnglishName).FontSize(9).FontColor(Colors.Grey.Darken1);
                            }

                            if (!string.IsNullOrWhiteSpace(company.Address))
                            {
                                c.Item().Text(company.Address).FontSize(8.5f);
                            }

                            c.Item().Text(text =>
                            {
                                text.DefaultTextStyle(s => s.FontSize(8.5f));
                                if (!string.IsNullOrWhiteSpace(company.Phone))
                                {
                                    text.Span("هاتف: " + company.Phone + "   ");
                                }

                                if (!string.IsNullOrWhiteSpace(company.TaxNumber))
                                {
                                    text.Span("الرقم الضريبي: " + company.TaxNumber + "   ");
                                }

                                if (!string.IsNullOrWhiteSpace(company.CommercialRegistrationNumber))
                                {
                                    text.Span("السجل التجاري: " + company.CommercialRegistrationNumber);
                                }
                            });
                        });

                        row.ConstantItem(26, Unit.Millimetre).Height(26, Unit.Millimetre).Svg(qr).FitArea();
                    });

                    header.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Darken2);

                    header.Item().Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Text(invoice.TaxAmount > 0m ? "فاتورة ضريبية" : "فاتورة").Bold().FontSize(17);
                        if (isDraft)
                        {
                            row.AutoItem().Background(Colors.Red.Lighten4).Border(1).BorderColor(Colors.Red.Darken2).Padding(4)
                                .Text("مسودة - غير صالحة للتحصيل").Bold().FontColor(Colors.Red.Darken3);
                        }
                        else if (invoice.Status == InvoiceStatus.Cancelled)
                        {
                            row.AutoItem().Background(Colors.Grey.Lighten3).Border(1).Padding(4).Text("ملغاة").Bold();
                        }
                    });
                });

                page.Content().PaddingTop(6).Column(column =>
                {
                    column.Spacing(8);

                    // Invoice and customer data.
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Border(0.6f).Padding(6).Column(c =>
                        {
                            c.Item().Text("بيانات الفاتورة").Bold();
                            c.Item().Text("رقم الفاتورة: " + (invoice.InvoiceNumber ?? "-"));
                            c.Item().Text("تاريخ الفاتورة: " + Date(invoice.InvoiceDateUtc));
                            c.Item().Text("تاريخ الاستحقاق: " + Date(invoice.DueDateUtc));
                            c.Item().Text($"مدة السداد: {invoice.PaymentTermsDays} يوماً");
                            c.Item().Text("الفرع: " + invoice.BranchName);
                            c.Item().Text("العملة: " + invoice.CurrencyCode);
                        });

                        row.ConstantItem(6);

                        row.RelativeItem().Border(0.6f).Padding(6).Column(c =>
                        {
                            c.Item().Text("العميل").Bold();
                            c.Item().Text(invoice.CustomerName).SemiBold();
                            c.Item().Text("رمز العميل: " + invoice.CustomerCode);
                            c.Item().Text("الهاتف: " + invoice.CustomerPhone);
                            if (!string.IsNullOrWhiteSpace(invoice.CustomerTaxNumber))
                            {
                                c.Item().Text("الرقم الضريبي / السجل: " + invoice.CustomerTaxNumber);
                            }
                        });
                    });

                    // Lines.
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(8, Unit.Millimetre);
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(1.7f);
                        });

                        table.Header(h =>
                        {
                            foreach (var title in new[] { "#", "البيان", "الأجور", "الخصم", "الخاضع للضريبة", "الضريبة", "الإجمالي" })
                            {
                                h.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(3).AlignCenter().Text(title).Bold().FontSize(8.5f);
                            }
                        });

                        foreach (var line in invoice.Lines)
                        {
                            table.Cell().Border(0.5f).Padding(3).AlignCenter().Text(line.LineNumber.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Border(0.5f).Padding(3).Text(line.Description).FontSize(8.5f);
                            table.Cell().Border(0.5f).Padding(3).AlignLeft().Text(Money(line.ShippingCharge));
                            table.Cell().Border(0.5f).Padding(3).AlignLeft().Text(Money(line.LineDiscountAmount + line.InvoiceDiscountAmount));
                            table.Cell().Border(0.5f).Padding(3).AlignLeft().Text(Money(line.TaxableAmount));
                            table.Cell().Border(0.5f).Padding(3).AlignLeft()
                                .Text($"{Money(line.TaxAmount)} ({line.TaxPercentage.ToString("0.##", CultureInfo.InvariantCulture)}%)").FontSize(8.5f);
                            table.Cell().Border(0.5f).Padding(3).AlignLeft().Text(Money(line.LineTotal)).SemiBold();
                        }
                    });

                    // Totals.
                    column.Item().AlignLeft().Width(85, Unit.Millimetre).Border(0.6f).Padding(6).Column(totals =>
                    {
                        void Line(string label, decimal value, bool bold = false)
                        {
                            totals.Item().Row(r =>
                            {
                                var labelText = r.RelativeItem().Text(label);
                                var valueText = r.AutoItem().Text($"{Money(value)} {invoice.CurrencyCode}");
                                if (bold)
                                {
                                    labelText.Bold();
                                    valueText.Bold();
                                }
                            });
                        }

                        Line("مجموع الأجور", invoice.SubTotal);
                        Line("الخصومات", invoice.DiscountAmount);
                        Line("الخاضع للضريبة", invoice.TaxableAmount);
                        Line("الضريبة", invoice.TaxAmount);
                        totals.Item().PaddingVertical(2).LineHorizontal(0.6f);
                        Line("إجمالي الفاتورة", invoice.TotalAmount, bold: true);
                        if (!isDraft)
                        {
                            Line("المدفوع", invoice.PaidAmount);
                            Line("الإشعارات الدائنة", invoice.CreditedAmount);
                            Line("الرصيد المستحق", invoice.BalanceDue, bold: true);
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(invoice.Notes))
                    {
                        column.Item().Border(0.5f).Padding(5).Text("ملاحظات: " + invoice.Notes).FontSize(8.5f);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                    text.Span("صفحة ");
                    text.CurrentPageNumber();
                    text.Span(" من ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
