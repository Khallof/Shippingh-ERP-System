using ShippingERP.Application.DTOs.Customers;
using ShippingERP.Application.DTOs.HR;
using ShippingERP.Application.DTOs.MasterData;

namespace ShippingERP.Application.Interfaces.MasterData;

// One interface per master-data table. They add nothing to ICrudService except a name the API can inject,
// plus a few extra operations where noted.

public interface IBranchService : ICrudService<BranchDto, SaveBranchRequest, BranchListQuery>
{
}

public interface IWarehouseService : ICrudService<WarehouseDto, SaveWarehouseRequest, WarehouseListQuery>
{
}

public interface ICountryService : ICrudService<CountryDto, SaveCountryRequest, CountryListQuery>
{
}

public interface ICityService : ICrudService<CityDto, SaveCityRequest, CityListQuery>
{
}

public interface IZoneService : ICrudService<ZoneDto, SaveZoneRequest, ZoneListQuery>
{
}

public interface IServiceTypeService : ICrudService<ServiceTypeDto, SaveServiceTypeRequest, ServiceTypeListQuery>
{
}

public interface IPaymentMethodService : ICrudService<PaymentMethodDto, SavePaymentMethodRequest, PaymentMethodListQuery>
{
}

public interface ICurrencyService : ICrudService<CurrencyDto, SaveCurrencyRequest, CurrencyListQuery>
{
}

public interface IExchangeRateService : ICrudService<ExchangeRateDto, SaveExchangeRateRequest, ExchangeRateListQuery>
{
    /// <summary>The rate valid at <paramref name="atUtc"/> (default: now) for FROM -> TO, by currency code.</summary>
    Task<ExchangeRateDto> GetEffectiveRateAsync(string fromCode, string toCode, DateTime? atUtc, CancellationToken cancellationToken);
}

public interface ITaxRateService : ICrudService<TaxRateDto, SaveTaxRateRequest, TaxRateListQuery>
{
}

public interface IExpenseCategoryService : ICrudService<ExpenseCategoryDto, SaveExpenseCategoryRequest, ExpenseCategoryListQuery>
{
}

public interface ISupplierService : ICrudService<SupplierDto, SaveSupplierRequest, SupplierListQuery>
{
}

public interface IVehicleService : ICrudService<VehicleDto, SaveVehicleRequest, VehicleListQuery>
{
}

public interface ICustomerService : ICrudService<CustomerDto, SaveCustomerRequest, CustomerListQuery>
{
}

public interface IPricingPlanService : ICrudService<PricingPlanDto, SavePricingPlanRequest, PricingPlanListQuery>
{
}

public interface IEmployeeService : ICrudService<EmployeeDto, SaveEmployeeRequest, EmployeeListQuery>
{
}

public interface IDriverService : ICrudService<DriverDto, SaveDriverRequest, DriverListQuery>
{
}

/// <summary>Addresses of one customer (/api/v1/customers/{customerId}/addresses).</summary>
public interface ICustomerAddressService
{
    Task<IReadOnlyList<CustomerAddressDto>> GetAllAsync(long customerId, CancellationToken cancellationToken);

    Task<CustomerAddressDto> GetByIdAsync(long customerId, long addressId, CancellationToken cancellationToken);

    Task<CustomerAddressDto> CreateAsync(long customerId, SaveCustomerAddressRequest request, CancellationToken cancellationToken);

    Task<CustomerAddressDto> UpdateAsync(long customerId, long addressId, SaveCustomerAddressRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(long customerId, long addressId, CancellationToken cancellationToken);
}

/// <summary>Rules of one pricing plan (/api/v1/pricing-plans/{planId}/rules).</summary>
public interface IPricingRuleService
{
    Task<IReadOnlyList<PricingRuleDto>> GetAllAsync(long planId, CancellationToken cancellationToken);

    Task<PricingRuleDto> GetByIdAsync(long planId, long ruleId, CancellationToken cancellationToken);

    Task<PricingRuleDto> CreateAsync(long planId, SavePricingRuleRequest request, CancellationToken cancellationToken);

    Task<PricingRuleDto> UpdateAsync(long planId, long ruleId, SavePricingRuleRequest request, CancellationToken cancellationToken);

    Task<PricingRuleDto> SetActiveAsync(long planId, long ruleId, bool isActive, CancellationToken cancellationToken);

    Task DeleteAsync(long planId, long ruleId, CancellationToken cancellationToken);
}

/// <summary>The single company profile row.</summary>
public interface ICompanyProfileService
{
    Task<CompanyProfileDto> GetAsync(CancellationToken cancellationToken);

    Task<CompanyProfileDto> SaveAsync(SaveCompanyProfileRequest request, CancellationToken cancellationToken);
}
