using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Infrastructure.Persistence.Interceptors;
using ShippingERP.Infrastructure.Persistence.Seeding;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>
/// Registers the double-entry accounting module (step 7). Called from Infrastructure/DependencyInjection.cs, where
/// the JournalIntegrityInterceptor is also attached to the DbContext. The background dispatcher of the automatic
/// postings (AccountingPostingBackgroundService) is a hosted service of the API project.
/// </summary>
public static class AccountingServiceRegistration
{
    public static IServiceCollection AddAccountingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AccountingOptions>(configuration.GetSection(AccountingOptions.SectionName));

        // Building blocks.
        services.AddSingleton<JournalIntegrityInterceptor>();
        services.AddScoped<AccountingAccess>();
        services.AddScoped<LedgerPosting>();
        services.AddScoped<PostingAccountResolver>();
        services.AddScoped<AccountingEventTranslator>();

        // Use cases.
        services.AddScoped<IChartOfAccountsService, ChartOfAccountsService>();
        services.AddScoped<FiscalPeriodService>();
        services.AddScoped<IFiscalPeriodService>(provider => provider.GetRequiredService<FiscalPeriodService>());
        services.AddScoped<IJournalEntryService, JournalEntryService>();
        services.AddScoped<IPostingRuleService, PostingRuleService>();
        services.AddScoped<AccountingPostingProcessor>();
        services.AddScoped<IAccountingPostingService>(provider => provider.GetRequiredService<AccountingPostingProcessor>());
        services.AddScoped<IAccountingIntegrationHandler>(provider => provider.GetRequiredService<AccountingPostingProcessor>());
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();
        services.AddScoped<IFinancialReportService, FinancialReportService>();

        // Startup data.
        services.AddScoped<ChartOfAccountsSeeder>();
        services.AddScoped<DemoAccountingSeeder>();

        return services;
    }
}
