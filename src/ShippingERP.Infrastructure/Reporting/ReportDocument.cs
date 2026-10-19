namespace ShippingERP.Infrastructure.Reporting;

/// <summary>How a report column is formatted (Excel number formats, PDF alignment).</summary>
public enum ReportColumnKind
{
    Text = 1,
    Money = 2,
    Number = 3,
    Integer = 4,
    Date = 5,
    DateTime = 6,
    Percent = 7
}

/// <summary>One column of a tabular report.</summary>
public sealed record ReportColumn(string Header, ReportColumnKind Kind = ReportColumnKind.Text, float Width = 1f);

/// <summary>A titled block of rows inside a report (e.g. "الإيرادات", "المصاريف").</summary>
public sealed class ReportSection
{
    public string? Title { get; init; }

    public IReadOnlyList<ReportColumn> Columns { get; init; } = Array.Empty<ReportColumn>();

    /// <summary>Cell values: string, decimal, int, long, DateTime or null.</summary>
    public IReadOnlyList<object?[]> Rows { get; init; } = Array.Empty<object?[]>();

    /// <summary>Optional bold totals row (same number of cells as the columns).</summary>
    public object?[]? Totals { get; init; }
}

/// <summary>
/// A printable / exportable report, independent of the file format (step 8). ReportFileRenderer turns it into an
/// Excel workbook (one sheet, right-to-left) or an A4 PDF (Cairo font, right-to-left, company header, page numbers).
/// </summary>
public sealed class ReportDocument
{
    public string Title { get; init; } = string.Empty;

    /// <summary>Short header lines: period, branch, currency, filters...</summary>
    public IReadOnlyList<(string Label, string Value)> Info { get; init; } = Array.Empty<(string, string)>();

    public IReadOnlyList<ReportSection> Sections { get; init; } = Array.Empty<ReportSection>();

    /// <summary>Lines printed under the tables (e.g. "صافي الربح: 1,000").</summary>
    public IReadOnlyList<(string Label, string Value)> Summary { get; init; } = Array.Empty<(string, string)>();

    /// <summary>Signature boxes at the end of the PDF (manifests, statements).</summary>
    public IReadOnlyList<string> Signatures { get; init; } = Array.Empty<string>();

    /// <summary>A4 landscape for wide tables.</summary>
    public bool Landscape { get; init; }

    /// <summary>Decimals of the money columns.</summary>
    public int MoneyDecimals { get; init; } = 2;

    /// <summary>File name without extension (Latin letters, digits and dashes).</summary>
    public string FileName { get; init; } = "report";

    /// <summary>Optional text encoded as a QR code in the PDF header (document number, total...).</summary>
    public string? QrText { get; init; }
}
