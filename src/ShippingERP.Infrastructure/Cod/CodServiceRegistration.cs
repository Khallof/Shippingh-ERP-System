using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Persistence.Interceptors;
using ShippingERP.Infrastructure.Persistence.Seeding;

namespace ShippingERP.Infrastructure.Cod;

/// <summary>
/// Registers cash on delivery and cash management (step 6). Called from Infrastructure/DependencyInjection.cs.
/// The COD synchronization interceptor itself is attached to the DbContext there (before the audit interceptor).
/// </summary>
public static class CodServiceRegistration
{
    public static IServiceCollection AddCodAndCashServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CashOptions>(configuration.GetSection(CashOptions.SectionName));

        // Building blocks.
        services.AddSingleton<BusinessCalendar>();
        services.AddSingleton<CashDocumentPdfRenderer>();
        services.AddScoped<CodShipmentSyncInterceptor>();
        services.AddScoped<CashAccess>();
        services.AddScoped<CashLedger>();

        // Use cases.
        services.AddScoped<ICodService, CodService>();
        services.AddScoped<ICodAdjustmentService, CodAdjustmentService>();
        services.AddScoped<ISenderSettlementService, SenderSettlementService>();
        services.AddScoped<ICodReportService, CodReportService>();
        services.AddScoped<ICashboxService, CashboxService>();
        services.AddScoped<IBankAccountService, BankAccountService>();
        services.AddScoped<ICashVoucherService, CashVoucherService>();
        services.AddScoped<ICashTransferService, CashTransferService>();
        services.AddScoped<ICashReportService, CashReportService>();

        // Startup data.
        services.AddScoped<CodBackfillSeeder>();
        services.AddScoped<DemoCashSeeder>();

        return services;
    }
}
