using Microsoft.Extensions.DependencyInjection;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Application.Interfaces.Pricing;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Infrastructure.Integration;
using ShippingERP.Infrastructure.Persistence.Seeding;
using ShippingERP.Infrastructure.Pricing;

namespace ShippingERP.Infrastructure.Billing;

/// <summary>Registers the pricing engine services, invoicing and receivables (step 5). Called from Infrastructure/DependencyInjection.cs.</summary>
public static class BillingServiceRegistration
{
    public static IServiceCollection AddBillingServices(this IServiceCollection services)
    {
        // Building blocks.
        services.AddScoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>();
        services.AddScoped<BillingAccess>();
        services.AddScoped<CustomerLedger>();
        services.AddSingleton<InvoicePdfRenderer>();

        // Use cases.
        services.AddScoped<IShipmentPricingService, ShipmentPricingService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<IInvoiceService>(sp => sp.GetRequiredService<InvoiceService>());

        // Step 9: automatic invoice per shipment (section "Billing", default on).
        services.AddOptions<ShipmentBillingOptions>().BindConfiguration(ShipmentBillingOptions.SectionName);
        services.AddScoped<ShipmentAutoBilling>();
        services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
        services.AddScoped<IReceivablesReportService, ReceivablesReportService>();

        services.AddScoped<DemoPricingSeeder>();

        // The hourly overdue check (InvoiceOverdueBackgroundService) is a hosted service of the API project.
        return services;
    }
}
