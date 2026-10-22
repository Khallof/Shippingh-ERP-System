using System.Globalization;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Shipments.Labels;

namespace ShippingERP.Infrastructure.Reporting;

/// <summary>
/// Renders a <see cref="ReportDocument"/> as an Excel workbook or a PDF (step 8).
///   Excel: one right-to-left sheet, bold header rows, number formats (#,##0.00), frozen title rows.
///   PDF  : A4 (portrait or landscape), right-to-left, embedded Cairo font, company header, optional QR code,
///          zebra rows, bold totals, signature boxes and "page x of y".
/// Values are written as given: DateTime cells must already be local business dates / times.
/// </summary>
public sealed class ReportFileRenderer
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string PdfContentType = "application/pdf";

    private const string Font = PdfLabelRenderer.FontFamily;
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    public ReportFileRenderer()
    {
        PdfLabelRenderer.EnsureInitialized();
    }

    public FileDownload Render(ReportDocument report, ExportFormat format, InvoiceCompanyInfo company, DateTime generatedAtLocal) =>
        format == ExportFormat.Pdf
            ? new FileDownload(new MemoryStream(ToPdf(report, company, generatedAtLocal)), PdfContentType, report.FileName + ".pdf")
            : new FileDownload(new MemoryStream(ToExcel(report, company, generatedAtLocal)), ExcelContentType, report.FileName + ".xlsx");

    // ------------------------------------------------------------------ Excel

    public byte[] ToExcel(ReportDocument report, InvoiceCompanyInfo company, DateTime generatedAtLocal)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName(report.Title));
        sheet.RightToLeft = true;

        var moneyFormat = report.MoneyDecimals <= 0 ? "#,##0" : "#,##0." + new string('0', Math.Clamp(report.MoneyDecimals, 1, 4));
        var width = Math.Max(1, report.Sections.Select(x => x.Columns.Count).DefaultIfEmpty(1).Max());

        var row = 1;
        sheet.Cell(row, 1).Value = company.ArabicName;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;
        sheet.Cell(row, 1).Value = report.Title;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 14;
        row++;

        foreach (var (label, value) in report.Info)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = value;
            row++;
        }

        sheet.Cell(row, 1).Value = "تاريخ الإصدار";
        sheet.Cell(row, 2).Value = generatedAtLocal.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        row += 2;

        var firstTableRow = row;
        foreach (var section in report.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                sheet.Cell(row, 1).Value = section.Title;
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 1).Style.Font.FontSize = 12;
                row++;
            }

            for (var c = 0; c < section.Columns.Count; c++)
            {
                var cell = sheet.Cell(row, c + 1);
                cell.Value = section.Columns[c].Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            row++;
            foreach (var values in section.Rows)
            {
                WriteExcelRow(sheet, row, section.Columns, values, moneyFormat);
                row++;
            }

            if (section.Totals is not null)
            {
                WriteExcelRow(sheet, row, section.Columns, section.Totals, moneyFormat);
                sheet.Range(row, 1, row, section.Columns.Count).Style.Font.Bold = true;
                sheet.Range(row, 1, row, section.Columns.Count).Style.Border.TopBorder = XLBorderStyleValues.Thin;
                row++;
            }

            row++;
        }

        foreach (var (label, value) in report.Summary)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = value;
            sheet.Cell(row, 2).Style.Font.Bold = true;
            row++;
        }

        if (report.Sections.Count == 1)
        {
            sheet.SheetView.FreezeRows(firstTableRow + (string.IsNullOrWhiteSpace(report.Sections[0].Title) ? 0 : 1));
        }

        sheet.Columns(1, width).AdjustToContents(firstTableRow, Math.Max(firstTableRow, row));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteExcelRow(IXLWorksheet sheet, int row, IReadOnlyList<ReportColumn> columns, object?[] values, string moneyFormat)
    {
        for (var c = 0; c < columns.Count && c < values.Length; c++)
        {
            var cell = sheet.Cell(row, c + 1);
            var value = values[c];
            switch (value)
            {
                case null:
                    break;
                case decimal number:
                    cell.Value = number;
                    cell.Style.NumberFormat.Format = columns[c].Kind switch
                    {
                        ReportColumnKind.Money => moneyFormat,
                        ReportColumnKind.Percent => "0.00\"%\"",
                        ReportColumnKind.Integer => "#,##0",
                        _ => "#,##0.####"
                    };
                    break;
                case int integer:
                    cell.Value = integer;
                    cell.Style.NumberFormat.Format = "#,##0";
                    break;
                case long longValue:
                    cell.Value = longValue;
                    break;
                case DateTime date:
                    cell.Value = date;
                    cell.Style.DateFormat.Format = columns[c].Kind == ReportColumnKind.DateTime ? "yyyy-mm-dd hh:mm" : "yyyy-mm-dd";
                    break;
                case bool flag:
                    cell.Value = flag ? "نعم" : "لا";
                    break;
                default:
                    cell.Value = value.ToString();
                    break;
            }
        }
    }

    /// <summary>Excel sheet names: at most 31 characters, without : \ / ? * [ ].</summary>
    private static string SheetName(string title)
    {
        var clean = new string(title.Where(ch => ch is not (':' or '\\' or '/' or '?' or '*' or '[' or ']')).ToArray()).Trim();
        if (clean.Length == 0)
        {
            clean = "تقرير";
        }

        return clean.Length <= 31 ? clean : clean[..31];
    }

    // ------------------------------------------------------------------ PDF

    public byte[] ToPdf(ReportDocument report, InvoiceCompanyInfo company, DateTime generatedAtLocal)
    {
        var decimals = Math.Clamp(report.MoneyDecimals, 0, 4);
        var qr = string.IsNullOrWhiteSpace(report.QrText) ? null : BarcodeGenerator.QrSvg(report.QrText);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(report.Landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(10, Unit.Millimetre);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(style => style.FontFamily(Font).FontSize(8.5f).LineHeight(1.15f));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        DocumentLogo.Add(row, company.Logo, 16);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(company.ArabicName).Bold().FontSize(12);
                            if (!string.IsNullOrWhiteSpace(company.Phone))
                            {
                                c.Item().Text("هاتف: " + company.Phone).FontSize(7.5f);
                            }

                            if (!string.IsNullOrWhiteSpace(company.TaxNumber))
                            {
                                c.Item().Text("الرقم الضريبي: " + company.TaxNumber).FontSize(7.5f);
                            }
                        });

                        row.RelativeItem(2).AlignCenter().AlignMiddle().Text(report.Title).Bold().FontSize(14);

                        row.RelativeItem().AlignLeft().AlignMiddle().Column(c =>
                        {
                            if (qr is not null)
                            {
                                c.Item().AlignLeft().Width(16, Unit.Millimetre).Height(16, Unit.Millimetre).Svg(qr).FitArea();
                            }

                            c.Item().AlignLeft().Text("أُصدر: " + generatedAtLocal.ToString(DateTimeFormat, CultureInfo.InvariantCulture)).FontSize(7);
                        });
                    });

                    if (report.Info.Count > 0)
                    {
                        header.Item().PaddingTop(3).Background(Colors.Grey.Lighten4).Padding(4).Text(text =>
                        {
                            for (var i = 0; i < report.Info.Count; i++)
                            {
                                if (i > 0)
                                {
                                    text.Span("     ");
                                }

                                text.Span(report.Info[i].Label + ": ").SemiBold();
                                text.Span(report.Info[i].Value);
                            }
                        });
                    }

                    header.Item().PaddingVertical(3).LineHorizontal(0.8f).LineColor(Colors.Grey.Darken2);
                });

                page.Content().Column(content =>
                {
                    content.Spacing(8);

                    foreach (var section in report.Sections)
                    {
                        content.Item().Column(block =>
                        {
                            if (!string.IsNullOrWhiteSpace(section.Title))
                            {
                                block.Item().PaddingBottom(2).Text(section.Title).Bold().FontSize(10.5f);
                            }

                            block.Item().Element(c => PdfTable(c, section, decimals));
                        });
                    }

                    if (report.Summary.Count > 0)
                    {
                        content.Item().Border(0.8f).Background(Colors.Grey.Lighten4).Padding(5).Column(summary =>
                        {
                            foreach (var (label, value) in report.Summary)
                            {
                                summary.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(label).SemiBold();
                                    row.RelativeItem().AlignLeft().Text(value).Bold();
                                });
                            }
                        });
                    }

                    if (report.Signatures.Count > 0)
                    {
                        content.Item().PaddingTop(14).Row(row =>
                        {
                            foreach (var title in report.Signatures)
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().AlignCenter().Text(title).SemiBold();
                                    c.Item().PaddingTop(18).PaddingHorizontal(10).LineHorizontal(0.6f);
                                    c.Item().AlignCenter().Text("الاسم والتوقيع").FontSize(7).FontColor(Colors.Grey.Darken1);
                                });
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Darken1));
                    text.Span("صفحة ");
                    text.CurrentPageNumber();
                    text.Span(" من ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void PdfTable(IContainer container, ReportSection section, int decimals)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var column in section.Columns)
                {
                    columns.RelativeColumn(column.Width <= 0 ? 1 : column.Width);
                }
            });

            table.Header(header =>
            {
                foreach (var column in section.Columns)
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Border(0.5f).BorderColor(Colors.Grey.Darken1)
                        .Padding(2).AlignCenter().Text(column.Header).Bold().FontSize(8);
                }
            });

            var index = 0;
            foreach (var values in section.Rows)
            {
                var background = index++ % 2 == 1 ? Colors.Grey.Lighten5 : Colors.White;
                for (var c = 0; c < section.Columns.Count; c++)
                {
                    var value = c < values.Length ? values[c] : null;
                    Cell(table.Cell().Background(background), section.Columns[c], value, decimals, bold: false);
                }
            }

            if (section.Totals is not null)
            {
                for (var c = 0; c < section.Columns.Count; c++)
                {
                    var value = c < section.Totals.Length ? section.Totals[c] : null;
                    Cell(table.Cell().Background(Colors.Grey.Lighten3), section.Columns[c], value, decimals, bold: true);
                }
            }
        });
    }

    private static void Cell(IContainer container, ReportColumn column, object? value, int decimals, bool bold)
    {
        var text = Format(column.Kind, value, decimals);
        var cell = container.Border(0.4f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(1.5f).PaddingHorizontal(2);
        cell = column.Kind is ReportColumnKind.Money or ReportColumnKind.Number or ReportColumnKind.Integer or ReportColumnKind.Percent
            ? cell.AlignLeft()
            : column.Kind is ReportColumnKind.Date or ReportColumnKind.DateTime ? cell.AlignCenter() : cell.AlignRight();

        var span = cell.Text(text);
        if (bold)
        {
            span.Bold();
        }
    }

    public static string Format(ReportColumnKind kind, object? value, int decimals) => value switch
    {
        null => string.Empty,
        decimal number when kind == ReportColumnKind.Money => number.ToString("N" + decimals, CultureInfo.InvariantCulture),
        decimal number when kind == ReportColumnKind.Percent => number.ToString("0.##", CultureInfo.InvariantCulture) + "%",
        decimal number when kind == ReportColumnKind.Integer => number.ToString("N0", CultureInfo.InvariantCulture),
        decimal number => number.ToString("#,##0.####", CultureInfo.InvariantCulture),
        int integer => integer.ToString("N0", CultureInfo.InvariantCulture),
        long longValue => longValue.ToString(CultureInfo.InvariantCulture),
        DateTime date when kind == ReportColumnKind.DateTime => date.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
        DateTime date => date.ToString(DateFormat, CultureInfo.InvariantCulture),
        bool flag => flag ? "نعم" : "لا",
        _ => value.ToString() ?? string.Empty
    };
}
