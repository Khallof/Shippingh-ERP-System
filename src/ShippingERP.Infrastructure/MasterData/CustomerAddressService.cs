using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Customers;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.MasterData;

/// <summary>
/// Addresses of one customer. The customer must be visible to the caller (branch rule).
/// Exactly one active address is the default: the first address becomes default automatically,
/// marking another address as default moves the flag, and deleting the default promotes the oldest remaining one.
/// </summary>
public sealed class CustomerAddressService : ICustomerAddressService
{
    private const string EntityName = "CustomerAddress";

    private readonly MasterDataServiceDependencies _dependencies;

    public CustomerAddressService(MasterDataServiceDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    private AppDbContext Db => _dependencies.DbContext;

    private static Expression<Func<CustomerAddress, CustomerAddressDto>> ToDto => x => new CustomerAddressDto
    {
        Id = x.Id,
        CustomerId = x.CustomerId,
        Label = x.Label,
        CityId = x.CityId,
        CityName = x.City.ArabicName,
        ZoneId = x.ZoneId,
        ZoneName = x.Zone != null ? x.Zone.ArabicName : null,
        AddressLine = x.AddressLine,
        Landmark = x.Landmark,
        BuildingNumber = x.BuildingNumber,
        PostalCode = x.PostalCode,
        ContactName = x.ContactName,
        Phone = x.Phone,
        AlternatePhone = x.AlternatePhone,
        Latitude = x.Latitude,
        Longitude = x.Longitude,
        IsDefault = x.IsDefault,
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc
    };

    public async Task<IReadOnlyList<CustomerAddressDto>> GetAllAsync(long customerId, CancellationToken cancellationToken)
    {
        await EnsureCustomerAccessibleAsync(customerId, cancellationToken);

        return await Db.CustomerAddresses
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Id)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerAddressDto> GetByIdAsync(long customerId, long addressId, CancellationToken cancellationToken)
    {
        await EnsureCustomerAccessibleAsync(customerId, cancellationToken);

        return await Db.CustomerAddresses
                   .AsNoTracking()
                   .Where(x => x.CustomerId == customerId && x.Id == addressId)
                   .Select(ToDto)
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("العنوان", addressId);
    }

    public async Task<CustomerAddressDto> CreateAsync(
        long customerId,
        SaveCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCustomerAccessibleAsync(customerId, cancellationToken);
        await ValidateLocationAsync(request, null, cancellationToken);

        var hasDefault = await Db.CustomerAddresses.AnyAsync(x => x.CustomerId == customerId && x.IsDefault, cancellationToken);

        var address = new CustomerAddress { CustomerId = customerId, IsActive = true };
        Apply(address, request);
        address.IsDefault = request.IsDefault || !hasDefault;

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            if (address.IsDefault)
            {
                await ClearDefaultAsync(customerId, exceptAddressId: null, ct);
            }

            Db.CustomerAddresses.Add(address);
            await Db.SaveChangesAsync(ct);

            _dependencies.AuditLog.Add(
                AuditAction.MasterDataCreated,
                entityName: EntityName,
                entityId: address.Id.ToString(),
                details: new { customerId, request.Label, request.CityId, request.ZoneId, address.IsDefault });
            await Db.SaveChangesAsync(ct);

            return address.Id;
        }, cancellationToken);

        return await GetByIdAsync(customerId, id, cancellationToken);
    }

    public async Task<CustomerAddressDto> UpdateAsync(
        long customerId,
        long addressId,
        SaveCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCustomerAccessibleAsync(customerId, cancellationToken);

        var address = await Db.CustomerAddresses
                          .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.Id == addressId, cancellationToken)
                      ?? throw new NotFoundException("العنوان", addressId);

        await ValidateLocationAsync(request, address, cancellationToken);

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            // Becoming default: clear the old default first (separate statement, same transaction).
            if (request.IsDefault && !address.IsDefault)
            {
                await ClearDefaultAsync(customerId, address.Id, ct);
                address.IsDefault = true;
            }

            // Unticking "default" on the current default is ignored: choose another default address instead.
            Apply(address, request);

            _dependencies.AuditLog.Add(
                AuditAction.MasterDataUpdated,
                entityName: EntityName,
                entityId: address.Id.ToString(),
                details: new { customerId, request });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(customerId, addressId, cancellationToken);
    }

    public async Task DeleteAsync(long customerId, long addressId, CancellationToken cancellationToken)
    {
        await EnsureCustomerAccessibleAsync(customerId, cancellationToken);

        var address = await Db.CustomerAddresses
                          .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.Id == addressId, cancellationToken)
                      ?? throw new NotFoundException("العنوان", addressId);

        if (await Db.Shipments.AnyAsync(x => x.SenderAddressId == addressId || x.RecipientAddressId == addressId, cancellationToken))
        {
            throw new BusinessRuleException("لا يمكن حذف العنوان لأنه مستخدم في شحنات (يبقى ظاهراً على ملصقاتها وتفاصيلها).");
        }

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var wasDefault = address.IsDefault;
            address.IsDefault = false;

            Db.CustomerAddresses.Remove(address); // soft delete
            _dependencies.AuditLog.Add(
                AuditAction.MasterDataDeleted,
                entityName: EntityName,
                entityId: address.Id.ToString(),
                details: new { customerId });
            await Db.SaveChangesAsync(ct);

            if (wasDefault)
            {
                var next = await Db.CustomerAddresses
                    .Where(x => x.CustomerId == customerId && x.IsActive)
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync(ct);

                if (next is not null)
                {
                    next.IsDefault = true;
                    await Db.SaveChangesAsync(ct);
                }
            }

            return true;
        }, cancellationToken);
    }

    private async Task EnsureCustomerAccessibleAsync(long customerId, CancellationToken cancellationToken)
    {
        var customer = await Db.Customers
            .AsNoTracking()
            .Where(x => x.Id == customerId)
            .Select(x => new { x.DefaultBranchId })
            .FirstOrDefaultAsync(cancellationToken);

        // Another branch's customer is reported as "not found", like in the customer list.
        if (customer is null || !_dependencies.BranchAccess.CanAccess(customer.DefaultBranchId))
        {
            throw new NotFoundException("العميل", customerId);
        }
    }

    private async Task ValidateLocationAsync(
        SaveCustomerAddressRequest request,
        CustomerAddress? existing,
        CancellationToken cancellationToken)
    {
        var city = await Db.Cities
            .AsNoTracking()
            .Where(x => x.Id == request.CityId)
            .Select(x => new { x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (city is null)
        {
            throw Invalid("cityId", "المدينة غير موجودة.");
        }

        if (!city.IsActive && existing?.CityId != request.CityId)
        {
            throw Invalid("cityId", "المدينة غير مفعّلة.");
        }

        if (request.ZoneId.HasValue)
        {
            var zoneId = request.ZoneId.Value;
            var zone = await Db.Zones
                .AsNoTracking()
                .Where(x => x.Id == zoneId)
                .Select(x => new { x.CityId, x.IsActive })
                .FirstOrDefaultAsync(cancellationToken);

            if (zone is null)
            {
                throw Invalid("zoneId", "المنطقة غير موجودة.");
            }

            if (zone.CityId != request.CityId)
            {
                throw Invalid("zoneId", "المنطقة لا تتبع المدينة المختارة.");
            }

            if (!zone.IsActive && existing?.ZoneId != request.ZoneId)
            {
                throw Invalid("zoneId", "المنطقة غير مفعّلة.");
            }
        }
    }

    private async Task ClearDefaultAsync(long customerId, long? exceptAddressId, CancellationToken cancellationToken)
    {
        var defaults = await Db.CustomerAddresses
            .Where(x => x.CustomerId == customerId && x.IsDefault && x.Id != exceptAddressId)
            .ToListAsync(cancellationToken);

        if (defaults.Count == 0)
        {
            return;
        }

        foreach (var other in defaults)
        {
            other.IsDefault = false;
        }

        await Db.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(CustomerAddress address, SaveCustomerAddressRequest request)
    {
        address.Label = request.Label.Trim();
        address.CityId = request.CityId;
        address.ZoneId = request.ZoneId;
        address.AddressLine = request.AddressLine.Trim();
        address.Landmark = Optional(request.Landmark);
        address.BuildingNumber = Optional(request.BuildingNumber);
        address.PostalCode = Optional(request.PostalCode);
        address.ContactName = Optional(request.ContactName);
        address.Phone = Optional(request.Phone);
        address.AlternatePhone = Optional(request.AlternatePhone);
        address.Latitude = request.Latitude;
        address.Longitude = request.Longitude;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RequestValidationException Invalid(string fieldName, string message) =>
        new(new Dictionary<string, string[]> { [fieldName] = new[] { message } });
}
