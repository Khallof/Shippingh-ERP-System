using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShippingERP.Application.Interfaces.Files;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Infrastructure.Files;
using ShippingERP.Infrastructure.Persistence.Seeding;
using ShippingERP.Infrastructure.Shipments.Import;
using ShippingERP.Infrastructure.Shipments.Labels;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>Registers the shipment services (step 4). Called from Infrastructure/DependencyInjection.cs.</summary>
public static class ShipmentServiceRegistration
{
    public static IServiceCollection AddShipmentServices(this IServiceCollection services, IConfiguration configuration)
    {
        // File storage (uploads): FileStorage section of appsettings.json.
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // Building blocks (one instance per request, they cache reference data for the request).
        services.AddScoped<DocumentNumberGenerator>();
        services.AddScoped<TrackingNumberGenerator>();
        services.AddScoped<ShipmentAccessService>();
        services.AddScoped<ShipmentStatusChanger>();
        services.AddScoped<ShipmentPricingCalculator>();
        services.AddScoped<ShipmentBuilder>();
        services.AddScoped<ShippingSettingsService>();
        services.AddScoped<IShippingSettingsService>(sp => sp.GetRequiredService<ShippingSettingsService>());
        services.AddScoped<ShipmentAttachmentService>();
        services.AddScoped<IShipmentAttachmentService>(sp => sp.GetRequiredService<ShipmentAttachmentService>());

        // Use cases.
        services.AddScoped<IShipmentService, ShipmentService>();
        services.AddScoped<IDeliveryService, DeliveryService>();
        services.AddScoped<IWarehouseManifestService, WarehouseManifestService>();
        services.AddScoped<IBranchTransferService, BranchTransferService>();
        services.AddScoped<IDriverManifestService, DriverManifestService>();
        services.AddScoped<IShipmentReturnService, ShipmentReturnService>();
        services.AddScoped<IDamageCaseService, DamageCaseService>();
        services.AddScoped<IShipmentImportService, ShipmentImportService>();

        // Labels (renderers are stateless).
        services.AddSingleton<PdfLabelRenderer>();
        services.AddSingleton<HtmlLabelRenderer>();
        services.AddScoped<IShipmentLabelService, ShipmentLabelService>();

        services.AddScoped<DemoShipmentSeeder>();

        return services;
    }
}
