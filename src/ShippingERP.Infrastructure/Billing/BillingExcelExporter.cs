using ClosedXML.Excel;
using ShippingERP.Application.DTOs.Billing;

namespace ShippingERP.Infrastructure.Billing;

/// <summary>Excel files of the receivables (ClosedXML): right-to-left sheets, frozen header, number formats.</summary>
public static class BillingExcelExporter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string MoneyFormat = "#,##0.00";
    private const string DateFormat = "yyyy-mm-dd";

    public static byte[] Invoices(IReadOnlyList<InvoiceListItemDto> invoices)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("الفواتير");
        sheet.RightToLeft = true;

        var headers = new[]
        {
            "رقم الفاتورة", "الحالة", "رمز العميل", "العميل", "الفرع", "العملة", "عدد الشحنات",
            "تاريخ الفاتورة", "تاريخ الاستحقاق", "الإجمالي", "المدفوع", "الإشعارات الدائنة", "الرصيد"
        };
        WriteHeader(sheet, headers);

        var row = 2;
        foreach (var invoice in invoices)
        {
            sheet.Cell(row, 1).Value = invoice.InvoiceNumber ?? $"مسودة #{invoice.Id}";
            sheet.Cell(row, 2).Value = invoice.StatusName;
            sheet.Cell(row, 3).Value = invoice.CustomerCode;
            sheet.Cell(row, 4).Value = invoice.CustomerName;
            sheet.Cell(row, 5).Value = invoice.BranchName;
            sheet.Cell(row, 6).Value = invoice.CurrencyCode;
            sheet.Cell(row, 7).Value = invoice.LineCount;
            SetDate(sheet.Cell(row, 8), invoice.InvoiceDateUtc);
            SetDate(sheet.Cell(row, 9), invoice.DueDateUtc);
            SetMoney(sheet.Cell(row, 10), invoice.TotalAmount);
            SetMoney(sheet.Cell(row, 11), invoice.PaidAmount);
            SetMoney(sheet.Cell(row, 12), invoice.CreditedAmount);
            SetMoney(sheet.Cell(row, 13), invoice.BalanceDue);
            row++;
        }

        if (invoices.Count > 0)
        {
            sheet.Cell(row, 9).Value = "الإجمالي";
            sheet.Cell(row, 9).Style.Font.Bold = true;
            for (var column = 10; column <= 13; column++)
            {
                var letter = XLHelper.GetColumnLetterFromNumber(column);
                sheet.Cell(row, column).FormulaA1 = $"SUM({letter}2:{letter}{row - 1})";
                sheet.Cell(row, column).Style.NumberFormat.Format = MoneyFormat;
                sheet.Cell(row, column).Style.Font.Bold = true;
            }
        }

        return Save(workbook, sheet);
    }

    public static byte[] Statement(
        string customerCode,
        string customerName,
        string currencyCode,
        DateTime? fromUtc,
        DateTime toUtc,
        decimal openingBalance,
        IReadOnlyList<StatementLineDto> lines)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("كشف حساب");
        sheet.RightToLeft = true;

        sheet.Cell(1, 1).Value = $"كشف حساب العميل: {customerName} ({customerCode})";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 13;
        sheet.Cell(2, 1).Value = $"العملة: {currencyCode}   من: {(fromUtc.HasValue ? fromUtc.Value.ToString("yyyy-MM-dd") : "البداية")}   إلى: {toUtc:yyyy-MM-dd}";

        const int headerRow = 4;
        var headers = new[] { "التاريخ", "النوع", "رقم المستند", "البيان", "مدين", "دائن", "الرصيد" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var row = headerRow + 1;
        sheet.Cell(row, 4).Value = "الرصيد الافتتاحي";
        SetMoney(sheet.Cell(row, 7), openingBalance);
        sheet.Row(row).Style.Font.Italic = true;
        row++;

        foreach (var line in lines)
        {
            SetDate(sheet.Cell(row, 1), line.TransactionDateUtc);
            sheet.Cell(row, 2).Value = line.TransactionTypeName;
            sheet.Cell(row, 3).Value = line.DocumentNumber;
            sheet.Cell(row, 4).Value = line.Description;
            SetMoney(sheet.Cell(row, 5), line.Debit);
            SetMoney(sheet.Cell(row, 6), line.Credit);
            SetMoney(sheet.Cell(row, 7), line.Balance);
            row++;
        }

        sheet.Cell(row, 4).Value = "الإجمالي / الرصيد الختامي";
        sheet.Cell(row, 4).Style.Font.Bold = true;
        SetMoney(sheet.Cell(row, 5), lines.Sum(x => x.Debit));
        SetMoney(sheet.Cell(row, 6), lines.Sum(x => x.Credit));
        SetMoney(sheet.Cell(row, 7), openingBalance + lines.Sum(x => x.Debit - x.Credit));
        sheet.Row(row).Style.Font.Bold = true;

        sheet.SheetView.FreezeRows(headerRow);
        sheet.Columns().AdjustToContents(1, row);
        return SaveWorkbook(workbook);
    }

    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        sheet.SheetView.FreezeRows(1);
    }

    private static void SetMoney(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = MoneyFormat;
    }

    private static void SetDate(IXLCell cell, DateTime? value)
    {
        if (value.HasValue)
        {
            cell.Value = value.Value;
            cell.Style.DateFormat.Format = DateFormat;
        }
    }

    private static byte[] Save(XLWorkbook workbook, IXLWorksheet sheet)
    {
        sheet.Columns().AdjustToContents();
        return SaveWorkbook(workbook);
    }

    private static byte[] SaveWorkbook(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
