using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments.Labels;

namespace ShippingERP.Infrastructure.Cash;

/// <summary>
/// Printable cash documents (QuestPDF, embedded Cairo font, right-to-left), with the company header, a QR code
/// (number | date | amount), the amount in figures AND in Arabic words, and signature boxes:
///   - cash receipt voucher (A5)            سند قبض نقدي
///   - driver handover receipt (A5 / A4)    إيصال استلام عهدة سائق: the COD shipments, expected / counted / shortage
///   - counter COD collection receipt (A5)  إيصال تحصيل في الفرع
///   - cash payment voucher (A5)            سند صرف نقدي
///   - sender settlement statement (A4)     كشف تسوية مبالغ التحصيل: one row per shipment with every deduction
/// Times are printed in the business time zone (Cash:BusinessTimeZoneId). Cancelled / draft documents are marked.
/// </summary>
public sealed class CashDocumentPdfRenderer
{
    private const string Font = PdfLabelRenderer.FontFamily;

    private readonly BusinessCalendar _calendar;

    public CashDocumentPdfRenderer(BusinessCalendar calendar)
    {
        _calendar = calendar;
        PdfLabelRenderer.EnsureInitialized();
    }

    public static async Task<InvoiceCompanyInfo> LoadCompanyAsync(AppDbContext dbContext, CancellationToken cancellationToken) =>
        await dbContext.CompanyProfiles.AsNoTracking()
            .Select(x => new InvoiceCompanyInfo(x.ArabicName, x.EnglishName, x.TaxNumber, x.CommercialRegistrationNumber, x.Phone, x.Email, x.Address))
            .FirstOrDefaultAsync(cancellationToken)
        ?? new InvoiceCompanyInfo("شركة الشحن", null, null, null, null, null, null);

    // ------------------------------------------------------------------ receipt / handover

    public byte[] RenderReceipt(CashReceiptVoucherDto voucher, InvoiceCompanyInfo company)
    {
        string Money(decimal value) => FormatMoney(value, voucher.CurrencyDecimals);

        var title = voucher.ReceiptType switch
        {
            CashReceiptType.DriverHandover => "إيصال استلام عهدة سائق",
            CashReceiptType.CounterCodCollection => "إيصال تحصيل دفع عند الاستلام في الفرع",
            _ => "سند قبض نقدي"
        };

        var hasLines = voucher.CodLines.Count > 0;
        var qr = BarcodeGenerator.QrSvg(string.Join("|", voucher.VoucherNumber, LocalTime(voucher.VoucherDateUtc), Money(voucher.Amount) + " " + voucher.CurrencyCode));

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(hasLines && voucher.CodLines.Count > 8 ? PageSizes.A4 : PageSizes.A5.Landscape());
                page.Margin(10, Unit.Millimetre);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(style => style.FontFamily(Font).FontSize(9.5f).LineHeight(1.2f));

                page.Header().Element(c => Header(c, company, title, voucher.VoucherNumber, qr, voucher.Status == CashVoucherStatus.Cancelled ? "ملغى" : null));

                page.Content().PaddingTop(5).Column(column =>
                {
                    column.Spacing(5);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Border(0.6f).Padding(5).Column(c =>
                        {
                            c.Item().Text("التاريخ: " + LocalTime(voucher.VoucherDateUtc));
                            c.Item().Text($"الصندوق: {voucher.CashboxName} ({voucher.CashboxCode})");
                            c.Item().Text("الفرع: " + voucher.BranchName);
                        });
                        row.ConstantItem(5);
                        row.RelativeItem().Border(0.6f).Padding(5).Column(c =>
                        {
                            c.Item().Text("استلمنا من: " + voucher.ReceivedFrom).SemiBold();
                            c.Item().Text("نوع القبض: " + voucher.ReceiptTypeName);
                            if (!string.IsNullOrWhiteSpace(voucher.ReferenceNumber))
                            {
                                c.Item().Text("المرجع: " + voucher.ReferenceNumber);
                            }

                            if (!string.IsNullOrWhiteSpace(voucher.CustomerPaymentNumber))
                            {
                                c.Item().Text("دفعة العميل: " + voucher.CustomerPaymentNumber);
                            }
                        });
                    });

                    column.Item().Element(c => AmountBox(c, Money(voucher.Amount), voucher.CurrencyCode, voucher.AmountInWords));
                    column.Item().Text("البيان: " + voucher.Description);

                    if (hasLines)
                    {
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(8, Unit.Millimetre);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                            });

                            table.Header(h =>
                            {
                                foreach (var t in new[] { "#", "رقم الشحنة", "رقم التتبع", "المرسل", "المبلغ المحصّل" })
                                {
                                    h.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(2).AlignCenter().Text(t).Bold().FontSize(8.5f);
                                }
                            });

                            var n = 1;
                            foreach (var line in voucher.CodLines)
                            {
                                table.Cell().Border(0.5f).Padding(2).AlignCenter().Text((n++).ToString(CultureInfo.InvariantCulture)).FontSize(8.5f);
                                table.Cell().Border(0.5f).Padding(2).Text(line.ShipmentNumber).FontSize(8.5f);
                                table.Cell().Border(0.5f).Padding(2).Text(line.TrackingNumber).FontSize(8.5f);
                                table.Cell().Border(0.5f).Padding(2).Text(line.SenderName).FontSize(8.5f);
                                table.Cell().Border(0.5f).Padding(2).AlignLeft().Text(Money(line.Amount)).FontSize(8.5f);
                            }
                        });
                    }

                    if (voucher.ReceiptType == CashReceiptType.DriverHandover)
                    {
                        column.Item().Border(0.6f).Padding(5).Column(c =>
                        {
                            c.Item().Text("السائق: " + voucher.DriverName).SemiBold();
                            c.Item().Text($"مجموع التحصيل المسلَّم: {Money(voucher.ExpectedAmount ?? 0m)}   المبلغ المعدود: {Money(voucher.Amount)}");
                            if (voucher.ShortageAmount > 0m)
                            {
                                c.Item().Text($"العجز: {Money(voucher.ShortageAmount)} (يبقى ذمة على السائق)").FontColor(Colors.Red.Darken2).SemiBold();
                            }

                            if (voucher.ExcessAmount > 0m)
                            {
                                c.Item().Text($"الزيادة: {Money(voucher.ExcessAmount)} (تسديد من رصيد سابق)");
                            }

                            if (voucher.DriverBalanceAfter.HasValue)
                            {
                                c.Item().Text($"رصيد عهدة السائق بعد التسليم: {Money(voucher.DriverBalanceAfter.Value)} {voucher.CurrencyCode}");
                            }
                        });
                    }

                    if (voucher.Status == CashVoucherStatus.Cancelled)
                    {
                        column.Item().Text($"أُلغي السند في {LocalTime(voucher.CancelledAtUtc)}: {voucher.CancellationReason}").FontColor(Colors.Red.Darken2);
                    }

                    var deliverer = voucher.ReceiptType == CashReceiptType.DriverHandover ? "المسلِّم (السائق)" : "الدافع";
                    column.Item().PaddingTop(8).Element(c => Signatures(c, deliverer, "المستلم (أمين الصندوق)", "المحاسب"));
                });

                page.Footer().Element(Footer);
            });
        });

        return document.GeneratePdf();
    }

    // ------------------------------------------------------------------ payment

    public byte[] RenderPayment(CashPaymentVoucherDto voucher, InvoiceCompanyInfo company)
    {
        string Money(decimal value) => FormatMoney(value, voucher.CurrencyDecimals);
        var qr = BarcodeGenerator.QrSvg(string.Join("|", voucher.VoucherNumber, LocalTime(voucher.VoucherDateUtc), Money(voucher.Amount) + " " + voucher.CurrencyCode));

        var link = voucher.ExpenseCategoryName ?? voucher.SupplierName ?? voucher.EmployeeName ?? voucher.CustomerName;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5.Landscape());
                page.Margin(10, Unit.Millimetre);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(style => style.FontFamily(Font).FontSize(9.5f).LineHeight(1.2f));

                page.Header().Element(c => Header(c, company, "سند صرف نقدي", voucher.VoucherNumber, qr, voucher.Status == CashVoucherStatus.Cancelled ? "ملغى" : null));

                page.Content().PaddingTop(5).Column(column =>
                {
                    column.Spacing(5);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Border(0.6f).Padding(5).Column(c =>
                        {
                            c.Item().Text("التاريخ: " + LocalTime(voucher.VoucherDateUtc));
                            c.Item().Text($"الصندوق: {voucher.CashboxName} ({voucher.CashboxCode})");
                            c.Item().Text("الفرع: " + voucher.BranchName);
                        });
                        row.ConstantItem(5);
                        row.RelativeItem().Border(0.6f).Padding(5).Column(c =>
                        {
                            c.Item().Text("صرفنا إلى: " + voucher.PaidTo).SemiBold();
                            c.Item().Text("نوع الصرف: " + voucher.PaymentTypeName + (link is null ? string.Empty : " - " + link));
                            if (!string.IsNullOrWhiteSpace(voucher.ReferenceNumber))
                            {
                                c.Item().Text("المرجع: " + voucher.ReferenceNumber);
                            }
                        });
                    });

                    column.Item().Element(c => AmountBox(c, Money(voucher.Amount), voucher.CurrencyCode, voucher.AmountInWords));
                    column.Item().Text("البيان: " + voucher.Description);

                    if (voucher.Status == CashVoucherStatus.Cancelled)
                    {
                        column.Item().Text($"أُلغي السند في {LocalTime(voucher.CancelledAtUtc)}: {voucher.CancellationReason}").FontColor(Colors.Red.Darken2);
                    }

                    column.Item().PaddingTop(10).Element(c => Signatures(c, "المستلم", "أمين الصندوق", "المدير / المحاسب"));
                });

                page.Footer().Element(Footer);
            });
        });

        return document.GeneratePdf();
    }

    // ------------------------------------------------------------------ settlement statement

    public byte[] RenderSettlement(SenderSettlementDto settlement, InvoiceCompanyInfo company)
    {
        string Money(decimal value) => FormatMoney(value, settlement.CurrencyDecimals);
        var qr = BarcodeGenerator.QrSvg(string.Join("|", settlement.SettlementNumber, LocalTime(settlement.SettlementDateUtc), Money(settlement.NetPayable) + " " + settlement.CurrencyCode));

        var mark = settlement.Status switch
        {
            SettlementStatus.Draft => "مسودة - بانتظار الاعتماد",
            SettlementStatus.Approved => "معتمدة - غير مدفوعة",
            SettlementStatus.Cancelled => "ملغاة",
            SettlementStatus.Reversed => "معكوسة",
            _ => null
        };

        var words = ArabicAmountInWords.Format(settlement.NetPayable, settlement.CurrencyName, settlement.CurrencyDecimals);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(10, Unit.Millimetre);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(style => style.FontFamily(Font).FontSize(9f).LineHeight(1.2f));

                page.Header().Element(c => Header(c, company, "كشف تسوية مبالغ الدفع عند الاستلام", settlement.SettlementNumber, qr, mark));

                page.Content().PaddingTop(5).Column(column =>
                {
                    column.Spacing(6);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Border(0.6f).Padding(5).Column(c =>
                        {
                            c.Item().Text("المرسل").Bold();
                            c.Item().Text($"{settlement.SenderName} ({settlement.SenderCode})").SemiBold();
                            if (!string.IsNullOrWhiteSpace(settlement.SenderPhone))
                            {
                                c.Item().Text("الهاتف: " + settlement.SenderPhone);
                            }
                        });
                        row.ConstantItem(5);
                        row.RelativeItem().Border(0.6f).Padding(5).Column(c =>
                        {
                            c.Item().Text("تاريخ التسوية: " + LocalTime(settlement.SettlementDateUtc));
                            c.Item().Text("الفرع: " + settlement.BranchName);
                            c.Item().Text("العملة: " + settlement.CurrencyCode);
                            c.Item().Text("الحالة: " + settlement.StatusName);
                        });
                        row.ConstantItem(5);
                        row.RelativeItem().Border(0.6f).Padding(5).Column(c =>
                        {
                            c.Item().Text("الدفع").Bold();
                            c.Item().Text("الطريقة: " + (settlement.PaymentMethodName ?? "-"));
                            c.Item().Text("من: " + (settlement.CashboxName ?? settlement.BankAccountName ?? "-"));
                            c.Item().Text("المرجع / السند: " + (settlement.PaymentReference ?? settlement.CashPaymentVoucherNumber ?? "-"));
                            c.Item().Text("تاريخ الدفع: " + LocalTime(settlement.PaidAtUtc));
                        });
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(7, Unit.Millimetre);
                            columns.RelativeColumn(2.2f);
                            columns.RelativeColumn(2.2f);
                            columns.RelativeColumn(2.6f);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(1.8f);
                        });

                        table.Header(h =>
                        {
                            foreach (var t in new[] { "#", "رقم الشحنة", "رقم التتبع", "المستلم", "تاريخ التسليم", "المبلغ المحصّل", "أجور الشحن", "عمولة التحصيل", "الضريبة", "رسوم أخرى", "تعويض", "المبلغ المدفوع" })
                            {
                                h.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(2).AlignCenter().Text(t).Bold().FontSize(8f);
                            }
                        });

                        var n = 1;
                        foreach (var line in settlement.Lines)
                        {
                            table.Cell().Border(0.5f).Padding(2).AlignCenter().Text((n++).ToString(CultureInfo.InvariantCulture)).FontSize(8f);
                            table.Cell().Border(0.5f).Padding(2).Text(line.ShipmentNumber).FontSize(8f);
                            table.Cell().Border(0.5f).Padding(2).Text(line.TrackingNumber).FontSize(8f);
                            table.Cell().Border(0.5f).Padding(2).Text(line.RecipientName).FontSize(8f);
                            table.Cell().Border(0.5f).Padding(2).AlignCenter().Text(LocalDate(line.DeliveredAtUtc)).FontSize(8f);
                            table.Cell().Border(0.5f).Padding(2).AlignLeft().Text(Money(line.CollectedAmount)).FontSize(8f);
                            table.Cell().Border(0.5f).Padding(2).AlignLeft().Text(Money(line.ShippingCharge)).FontSize(8f);
                            table.Cell().Border(0.5f).Padding(2).AlignLeft().Text(Money(line.CodCommission)).FontSize(8f);
                            table.Cell().Border(0.5f).Padding(2).AlignLeft().Text(Money(line.Tax)).FontSize(8f);
                            table.Cell().Border(0.5f).Padding(2).AlignLeft().Text(Money(line.OtherFees)).FontSize(8f);
                            table.Cell().Border(0.5f).Padding(2).AlignLeft().Text(Money(line.Compensation)).FontSize(8f);
                            table.Cell().Border(0.5f).Padding(2).AlignLeft().Text(Money(line.Amount)).SemiBold().FontSize(8f);
                        }
                    });

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("ملاحظة: المبالغ المحصّلة أمانة للمرسل؛ تُخصم منها أجور الشحن وعمولة التحصيل والضريبة والرسوم المعتمدة، ويُدفع الصافي.").FontSize(8).FontColor(Colors.Grey.Darken2);
                            if (!string.IsNullOrWhiteSpace(settlement.Notes))
                            {
                                c.Item().Text("ملاحظات: " + settlement.Notes).FontSize(8.5f);
                            }

                            if (settlement.Status == SettlementStatus.Reversed)
                            {
                                c.Item().Text("سبب العكس: " + settlement.ReversalReason).FontColor(Colors.Red.Darken2);
                            }

                            if (settlement.Status == SettlementStatus.Cancelled)
                            {
                                c.Item().Text("سبب الإلغاء: " + settlement.CancellationReason).FontColor(Colors.Red.Darken2);
                            }
                        });

                        row.ConstantItem(6);

                        row.ConstantItem(90, Unit.Millimetre).Border(0.6f).Padding(5).Column(totals =>
                        {
                            void Line(string label, decimal value, bool bold = false)
                            {
                                totals.Item().Row(r =>
                                {
                                    var l = r.RelativeItem().Text(label);
                                    var v = r.AutoItem().Text($"{Money(value)} {settlement.CurrencyCode}");
                                    if (bold)
                                    {
                                        l.Bold();
                                        v.Bold();
                                    }
                                });
                            }

                            Line("المبالغ المحرّرة من الأمانة", settlement.TotalGrossReleased);
                            Line("أجور الشحن", settlement.TotalShippingCharges);
                            Line("عمولة التحصيل", settlement.TotalCodCommission);
                            Line("الضريبة", settlement.TotalTax);
                            Line("رسوم أخرى معتمدة", settlement.TotalOtherFees);
                            Line("تعويضات للمرسل", settlement.TotalCompensation);
                            totals.Item().PaddingVertical(2).LineHorizontal(0.6f);
                            Line("الصافي المستحق للمرسل", settlement.NetPayable, bold: true);
                        });
                    });

                    column.Item().Text(words).SemiBold();
                    column.Item().PaddingTop(8).Element(c => Signatures(c, "المرسل / المستلم", "أمين الصندوق", "المحاسب", "المدير"));
                });

                page.Footer().Element(Footer);
            });
        });

        return document.GeneratePdf();
    }

    // ------------------------------------------------------------------ parts

    private static void Header(IContainer container, InvoiceCompanyInfo company, string title, string number, string qrSvg, string? mark)
    {
        container.Column(header =>
        {
            header.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(company.ArabicName).Bold().FontSize(13);
                    if (!string.IsNullOrWhiteSpace(company.Address))
                    {
                        c.Item().Text(company.Address).FontSize(8);
                    }

                    if (!string.IsNullOrWhiteSpace(company.Phone))
                    {
                        c.Item().Text("هاتف: " + company.Phone).FontSize(8);
                    }
                });

                row.RelativeItem().AlignCenter().AlignMiddle().Column(c =>
                {
                    c.Item().AlignCenter().Text(title).Bold().FontSize(14);
                    c.Item().AlignCenter().Text("رقم: " + number).SemiBold();
                });

                row.ConstantItem(20, Unit.Millimetre).Height(20, Unit.Millimetre).Svg(qrSvg).FitArea();
            });

            if (mark is not null)
            {
                header.Item().AlignCenter().Background(Colors.Red.Lighten4).Border(1).BorderColor(Colors.Red.Darken2).PaddingHorizontal(8).PaddingVertical(2)
                    .Text(mark).Bold().FontColor(Colors.Red.Darken3);
            }

            header.Item().PaddingVertical(3).LineHorizontal(1).LineColor(Colors.Grey.Darken2);
        });
    }

    private static void AmountBox(IContainer container, string amount, string currencyCode, string words)
    {
        container.Border(1).Background(Colors.Grey.Lighten4).Padding(6).Row(row =>
        {
            row.AutoItem().AlignMiddle().Text($"المبلغ: {amount} {currencyCode}").Bold().FontSize(12);
            row.ConstantItem(10);
            row.RelativeItem().AlignMiddle().Text(words).SemiBold();
        });
    }

    private static void Signatures(IContainer container, params string[] titles)
    {
        container.Row(row =>
        {
            foreach (var title in titles)
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().AlignCenter().Text(title).SemiBold();
                    c.Item().PaddingTop(16).PaddingHorizontal(10).LineHorizontal(0.6f);
                    c.Item().AlignCenter().Text("الاسم والتوقيع").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                });
            }
        });
    }

    private static void Footer(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Colors.Grey.Darken1));
            text.Span("صفحة ");
            text.CurrentPageNumber();
            text.Span(" من ");
            text.TotalPages();
        });
    }

    private string LocalTime(DateTime? utc) =>
        utc is null
            ? "-"
            : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), _calendar.Zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private string LocalDate(DateTime? utc) =>
        utc is null
            ? "-"
            : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), _calendar.Zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatMoney(decimal value, int decimals) =>
        value.ToString("N" + Math.Clamp(decimals, 0, 4), CultureInfo.InvariantCulture);
}
