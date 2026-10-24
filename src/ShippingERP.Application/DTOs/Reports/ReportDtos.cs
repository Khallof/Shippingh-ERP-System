using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Reports;

/// <summary>File format of a report export (?format=excel or ?format=pdf).</summary>
public enum ExportFormat
{
    Excel = 1,
    Pdf = 2
}

// ============================================================ dashboard

/// <summary>GET /api/v1/dashboard.</summary>
public sealed class DashboardQuery
{
    /// <summary>Days of the "recent period" (delivered %, failed %, top customers). Default 30, 1..365.</summary>
    public int Days { get; set; } = 30;

    /// <summary>Optional branch (only branches the caller can see).</summary>
    public long? BranchId { get; set; }

    /// <summary>Step 9: the money figures are in this currency, in its own amounts (no conversion). Default: base.</summary>
    public long? CurrencyId { get; set; }
}

public sealed class StatusCountDto
{
    public ShipmentStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class DailyShipmentCountDto
{
    /// <summary>Local business date.</summary>
    public DateTime Date { get; init; }
    public int Created { get; init; }
    public int Delivered { get; init; }
}

public sealed class DashboardOperationsDto
{
    public int TodayCreated { get; init; }
    public int TodayDelivered { get; init; }
    public int TodayFailed { get; init; }
    public int OpenShipments { get; init; }
    public int OutForDelivery { get; init; }

    /// <summary>Shipments created in the period (cancelled excluded).</summary>
    public int PeriodShipments { get; init; }
    public int PeriodDelivered { get; init; }

    /// <summary>Delivery failed, returned, lost or damaged.</summary>
    public int PeriodFailed { get; init; }

    public decimal DeliveredPercent { get; init; }
    public decimal FailedPercent { get; init; }
    public IReadOnlyList<StatusCountDto> ByStatus { get; init; } = Array.Empty<StatusCountDto>();
    public IReadOnlyList<DailyShipmentCountDto> Last7Days { get; init; } = Array.Empty<DailyShipmentCountDto>();
}

public sealed class DashboardCashboxDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Balance { get; init; }
}

public sealed class DashboardFinanceDto
{
    /// <summary>Month to date (local business dates).</summary>
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public decimal TotalRevenue { get; init; }

    /// <summary>Cost of revenue + expenses.</summary>
    public decimal TotalExpenses { get; init; }

    public decimal NetProfit { get; init; }
    public int OverdueInvoicesCount { get; init; }
    public decimal OverdueInvoicesAmount { get; init; }
    public decimal ReceivablesBalance { get; init; }
}

public sealed class DashboardDriverBalanceDto
{
    public long DriverId { get; init; }
    public string DriverName { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public int UnhandedCodCount { get; init; }
}

public sealed class DashboardTopCustomerDto
{
    public long CustomerId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int ShipmentCount { get; init; }
    public decimal TotalCharges { get; init; }
    public decimal CodAmount { get; init; }
}

public sealed class DashboardActivityDto
{
    public DateTime OccurredAtUtc { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public ShipmentStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string? BranchName { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// The management dashboard. Each section is null when the caller lacks its permission:
/// Operations / top customers / activity: Shipments.View; Cod / driver balances: COD.View; Cashboxes: Cashboxes.View;
/// Finance: Reports.ViewFinancial (overdue invoices also with Invoices.View).
/// </summary>
public sealed class DashboardDto
{
    public DateTime AsOfUtc { get; init; }
    public DateTime Today { get; init; }
    public int PeriodDays { get; init; }
    public long? BranchId { get; init; }
    public DashboardOperationsDto? Operations { get; init; }
    public CodDashboardDto? Cod { get; init; }
    public IReadOnlyList<DashboardCashboxDto>? Cashboxes { get; init; }
    public DashboardFinanceDto? Finance { get; init; }
    public IReadOnlyList<DashboardDriverBalanceDto>? DriverBalances { get; init; }
    public IReadOnlyList<DashboardTopCustomerDto>? TopCustomers { get; init; }
    public IReadOnlyList<DashboardActivityDto>? RecentActivity { get; init; }
}

// ============================================================ operational reports

/// <summary>GET /api/v1/reports/operations: shipments of a period, grouped.</summary>
public sealed class OperationalReportQuery
{
    /// <summary>Local business date (default: 30 days ago).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Local business date, inclusive (default: today).</summary>
    public DateTime? ToDate { get; set; }

    public long? BranchId { get; set; }

    public long? SenderCustomerId { get; set; }

    public long? DriverId { get; set; }

    public ShipmentStatus? Status { get; set; }

    /// <summary>Step 9: charges and COD amounts of this currency only, in its own amounts. Default: base. Counts include every currency.</summary>
    public long? CurrencyId { get; set; }
}

public sealed class OperationalGroupRowDto
{
    public long? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Total { get; init; }
    public int Delivered { get; init; }
    public int Failed { get; init; }
    public int InProgress { get; init; }
    public int Cancelled { get; init; }
    public decimal DeliveredPercent { get; init; }
    public decimal TotalCharges { get; init; }
    public decimal CodAmount { get; init; }
}

public sealed class OperationalReportDto
{
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }

    /// <summary>Currency of TotalCharges / CodAmount.</summary>
    public string CurrencyCode { get; init; } = string.Empty;
    public OperationalGroupRowDto Totals { get; init; } = new();
    public IReadOnlyList<StatusCountDto> ByStatus { get; init; } = Array.Empty<StatusCountDto>();
    public IReadOnlyList<OperationalGroupRowDto> ByBranch { get; init; } = Array.Empty<OperationalGroupRowDto>();
    public IReadOnlyList<OperationalGroupRowDto> ByDriver { get; init; } = Array.Empty<OperationalGroupRowDto>();
    public IReadOnlyList<OperationalGroupRowDto> ByCustomer { get; init; } = Array.Empty<OperationalGroupRowDto>();
    public IReadOnlyList<OperationalGroupRowDto> ByServiceType { get; init; } = Array.Empty<OperationalGroupRowDto>();
    public IReadOnlyList<DailyShipmentCountDto> Daily { get; init; } = Array.Empty<DailyShipmentCountDto>();
}

/// <summary>?format=excel|pdf of the export endpoints.</summary>
public sealed class ExportQuery
{
    public ExportFormat Format { get; set; } = ExportFormat.Excel;
}

// ============================================================ audit log

/// <summary>GET /api/v1/audit-logs. Search: user name, entity name, entity id.</summary>
public sealed class AuditLogListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "createdAtUtc", "action", "userName" };

    public AuditAction? Action { get; set; }

    public long? UserId { get; set; }

    public string? EntityName { get; set; }

    public string? EntityId { get; set; }

    public bool? Succeeded { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}

public sealed class AuditLogDto
{
    public long Id { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public AuditAction Action { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public long? UserId { get; init; }
    public string? UserName { get; init; }
    public string? EntityName { get; init; }
    public string? EntityId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? Details { get; init; }
}
