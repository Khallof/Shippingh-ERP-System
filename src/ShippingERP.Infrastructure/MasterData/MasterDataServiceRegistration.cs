using Microsoft.Extensions.DependencyInjection;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Infrastructure.Persistence.Seeding;

namespace ShippingERP.Infrastructure.MasterData;

/// <summary>Registers the master-data services (step 3). Called from Infrastructure/DependencyInjection.cs.</summary>
public static class MasterDataServiceRegistration
{
    public static IServiceCollection AddMasterDataServices(this IServiceCollection services)
    {
        services.AddScoped<IBranchAccessService, BranchAccessService>();
        services.AddScoped<MasterDataServiceDependencies>();

        services.AddScoped<ICompanyProfileService, CompanyProfileService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<ICountryService, CountryService>();
        services.AddScoped<ICityService, CityService>();
        services.AddScoped<IZoneService, ZoneService>();
        services.AddScoped<IServiceTypeService, ServiceTypeService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<ICurrencyService, CurrencyService>();
        services.AddScoped<IExchangeRateService, ExchangeRateService>();
        services.AddScoped<ITaxRateService, TaxRateService>();
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICustomerAddressService, CustomerAddressService>();
        services.AddScoped<IPricingPlanService, PricingPlanService>();
        services.AddScoped<IPricingRuleService, PricingRuleService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IDriverService, DriverService>();

        services.AddScoped<DemoDataSeeder>();

        return services;
    }
}
