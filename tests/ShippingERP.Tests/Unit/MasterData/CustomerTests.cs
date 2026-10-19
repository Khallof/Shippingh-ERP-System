using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Customers;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.MasterData;

/// <summary>Customers and customer addresses (uses the demo data: 10 customers in 2 branches).</summary>
public sealed class CustomerTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private long _damascusId;
    private long _aleppoId;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        (_damascusId, _aleppoId) = await _context.QueryAsync(async db => (
            await db.Branches.Where(b => b.Code == "DAM").Select(b => b.Id).SingleAsync(),
            await db.Branches.Where(b => b.Code == "ALP").Select(b => b.Id).SingleAsync()));
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private SaveCustomerRequest NewCustomer(string phone, long? branchId = null) => new()
    {
        ArabicName = "عميل جديد",
        Phone = phone,
        CustomerType = CustomerType.Individual,
        Roles = CustomerRole.Both,
        DefaultBranchId = branchId ?? _damascusId
    };

    private Task<CustomerDto> CreateAsync(SaveCustomerRequest request) =>
        _context.UseAsync<ICustomerService, CustomerDto>(service => service.CreateAsync(request, CancellationToken.None));

    private Task<PagedResult<CustomerDto>> ListAsync(CustomerListQuery query) =>
        _context.UseAsync<ICustomerService, PagedResult<CustomerDto>>(service => service.GetPagedAsync(query, CancellationToken.None));

    [Fact]
    public async Task Create_WithoutCode_GetsTheNextAutomaticCode()
    {
        var customer = await CreateAsync(NewCustomer("+963944999001"));

        Assert.Equal("CUS-00011", customer.Code);
        Assert.Equal("فرع دمشق", customer.DefaultBranchName);
        Assert.Equal(0, customer.AddressCount);
    }

    [Fact]
    public async Task Create_WithExistingCode_ReturnsConflict()
    {
        var request = NewCustomer("+963944999002");
        request.Code = "cus-00001";

        await Assert.ThrowsAsync<ConflictException>(() => CreateAsync(request));
    }

    [Fact]
    public async Task Search_FindsCustomerByPhone()
    {
        var result = await ListAsync(new CustomerListQuery { Search = "000105" });

        Assert.Equal("مؤسسة الشهباء للألبسة", Assert.Single(result.Items).ArabicName);
    }

    [Fact]
    public async Task RoleFilter_Sender_IncludesCustomersWithRoleBoth()
    {
        var senders = await ListAsync(new CustomerListQuery { Role = CustomerRole.Sender, PageSize = 100 });
        var recipientsOnly = await ListAsync(new CustomerListQuery { Role = CustomerRole.Recipient, PageSize = 100 });

        // Demo data: 2 Sender, 4 Both, 4 Recipient.
        Assert.Equal(6, senders.TotalCount);
        Assert.Equal(8, recipientsOnly.TotalCount);
        Assert.All(senders.Items, c => Assert.True(c.Roles.HasFlag(CustomerRole.Sender)));
    }

    [Fact]
    public async Task BranchUser_SeesOnlyCustomersOfTheirBranch_AndCannotCreateInAnotherBranch()
    {
        _context.CurrentUser.SignInAs(60, _aleppoId, Permissions.Customers.View, Permissions.Customers.Manage);

        var visible = await ListAsync(new CustomerListQuery { PageSize = 100 });
        Assert.Equal(4, visible.TotalCount);
        Assert.All(visible.Items, c => Assert.Equal(_aleppoId, c.DefaultBranchId));

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(NewCustomer("+963944999003", _damascusId)));

        var created = await CreateAsync(NewCustomer("+963944999004", _aleppoId));
        Assert.Equal(_aleppoId, created.DefaultBranchId);
    }

    [Fact]
    public async Task Address_FirstIsDefault_AndANewDefaultMovesTheFlag()
    {
        var customer = await CreateAsync(NewCustomer("+963944999005"));
        var (cityId, zoneId) = await _context.QueryAsync(async db =>
        {
            var zone = await db.Zones.Where(z => z.Code == "MAZZEH").Select(z => new { z.Id, z.CityId }).SingleAsync();
            return (zone.CityId, zone.Id);
        });

        var first = await _context.UseAsync<ICustomerAddressService, CustomerAddressDto>(service => service.CreateAsync(
            customer.Id,
            new SaveCustomerAddressRequest { Label = "المنزل", CityId = cityId, ZoneId = zoneId, AddressLine = "شارع 1", Phone = "+963944999006" },
            CancellationToken.None));

        var second = await _context.UseAsync<ICustomerAddressService, CustomerAddressDto>(service => service.CreateAsync(
            customer.Id,
            new SaveCustomerAddressRequest { Label = "المكتب", CityId = cityId, AddressLine = "شارع 2", IsDefault = true },
            CancellationToken.None));

        Assert.True(first.IsDefault);
        Assert.True(second.IsDefault);

        var all = await _context.UseAsync<ICustomerAddressService, IReadOnlyList<CustomerAddressDto>>(service =>
            service.GetAllAsync(customer.Id, CancellationToken.None));
        Assert.Equal(2, all.Count);
        Assert.Equal(second.Id, Assert.Single(all, a => a.IsDefault).Id);

        // Deleting the default promotes the remaining address.
        await _context.UseAsync<ICustomerAddressService>(service => service.DeleteAsync(customer.Id, second.Id, CancellationToken.None));
        var remaining = await _context.UseAsync<ICustomerAddressService, IReadOnlyList<CustomerAddressDto>>(service =>
            service.GetAllAsync(customer.Id, CancellationToken.None));
        Assert.True(Assert.Single(remaining).IsDefault);
    }

    [Fact]
    public async Task Address_ZoneMustBelongToTheCity()
    {
        var customer = await CreateAsync(NewCustomer("+963944999007"));
        var (damascusCityId, aleppoZoneId) = await _context.QueryAsync(async db => (
            await db.Cities.Where(c => c.Code == "DAM").Select(c => c.Id).SingleAsync(),
            await db.Zones.Where(z => z.Code == "AZIZIYEH").Select(z => z.Id).SingleAsync()));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _context.UseAsync<ICustomerAddressService, CustomerAddressDto>(service => service.CreateAsync(
                customer.Id,
                new SaveCustomerAddressRequest { Label = "خطأ", CityId = damascusCityId, ZoneId = aleppoZoneId, AddressLine = "x" },
                CancellationToken.None)));

        Assert.True(exception.Errors.ContainsKey("zoneId"));
    }

    [Fact]
    public async Task AddressesOfAnotherBranchCustomer_AreNotFound()
    {
        var damascusCustomerId = await _context.QueryAsync(db =>
            db.Customers.Where(c => c.DefaultBranchId == _damascusId).Select(c => c.Id).FirstAsync());

        _context.CurrentUser.SignInAs(61, _aleppoId, Permissions.Customers.View);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _context.UseAsync<ICustomerAddressService, IReadOnlyList<CustomerAddressDto>>(service =>
                service.GetAllAsync(damascusCustomerId, CancellationToken.None)));
    }

    [Fact]
    public async Task DeleteCustomer_WithOpeningBalance_IsRefused_ButWithoutBalanceItIsSoftDeleted()
    {
        var withBalance = NewCustomer("+963944999008");
        withBalance.OpeningBalance = 25000m;
        var indebted = await CreateAsync(withBalance);
        var clean = await CreateAsync(NewCustomer("+963944999009"));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.UseAsync<ICustomerService>(service => service.DeleteAsync(indebted.Id, CancellationToken.None)));

        await _context.UseAsync<ICustomerService>(service => service.DeleteAsync(clean.Id, CancellationToken.None));
        Assert.True(await _context.QueryAsync(db => db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Id == clean.Id && c.IsDeleted)));
    }

    [Fact]
    public async Task Create_WithUnknownPricingPlan_ReturnsValidationError()
    {
        var request = NewCustomer("+963944999010");
        request.PricingPlanId = 999;

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => CreateAsync(request));

        Assert.True(exception.Errors.ContainsKey("pricingPlanId"));
    }
}
