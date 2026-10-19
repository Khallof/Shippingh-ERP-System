using Microsoft.Extensions.DependencyInjection;
using ShippingERP.Application.Interfaces.Reports;

namespace ShippingERP.Infrastructure.Reporting;

/// <summary>Dashboard, operational reports, report exports (Excel / PDF) and the audit log list (step 8).</summary>
public static class ReportingServiceRegistration
{
    public static IServiceCollection AddReportingServices(this IServiceCollection services)
    {
        services.AddSingleton<ReportFileRenderer>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<OperationalReportService>();
        services.AddScoped<IOperationalReportService>(provider => provider.GetRequiredService<OperationalReportService>());
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
        return services;
    }
}
