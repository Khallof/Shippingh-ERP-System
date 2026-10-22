using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.DTOs.Shipments;

namespace ShippingERP.Application.Interfaces.Reports;

/// <summary>The management dashboard (step 8): one call, sections filtered by the caller's permissions.</summary>
public interface IDashboardService
{
    Task<DashboardDto> GetAsync(DashboardQuery query, CancellationToken cancellationToken);
}

/// <summary>Operational reports of the shipments (step 8).</summary>
public interface IOperationalReportService
{
    Task<OperationalReportDto> GetAsync(OperationalReportQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Excel / PDF files of the reports (step 8): Arabic right-to-left sheets and A4 PDFs with the Cairo font and the
/// company header. Lists are exported in full (up to 10,000 rows), with the same filters as the screen.
/// </summary>
public interface IReportExportService
{
    Task<FileDownload> ShipmentsAsync(ShipmentListQuery query, ExportFormat format, CancellationToken cancellationToken);

    /// <summary>Step 9: the filtered invoice list (Excel or PDF with the company header and logo).</summary>
    Task<FileDownload> InvoicesAsync(InvoiceListQuery query, ExportFormat format, CancellationToken cancellationToken);

    Task<FileDownload> OperationalReportAsync(OperationalReportQuery query, ExportFormat format, CancellationToken cancellationToken);

    Task<FileDownload> TrialBalanceAsync(TrialBalanceQuery query, ExportFormat format, CancellationToken cancellationToken);

    Task<FileDownload> GeneralLedgerAsync(GeneralLedgerQuery query, ExportFormat format, CancellationToken cancellationToken);

    Task<FileDownload> ProfitLossAsync(ProfitLossQuery query, ExportFormat format, CancellationToken cancellationToken);

    Task<FileDownload> BalanceSheetAsync(BalanceSheetQuery query, ExportFormat format, CancellationToken cancellationToken);

    /// <summary>The delivery manifest of a driver (A4 PDF with the signature boxes).</summary>
    Task<FileDownload> DriverManifestPdfAsync(long manifestId, CancellationToken cancellationToken);
}

/// <summary>Read access to the audit log (step 8).</summary>
public interface IAuditLogQueryService
{
    Task<PagedResult<AuditLogDto>> GetPagedAsync(AuditLogListQuery query, CancellationToken cancellationToken);
}
