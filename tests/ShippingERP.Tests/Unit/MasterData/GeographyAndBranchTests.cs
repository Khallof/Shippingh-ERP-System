using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.MasterData;

/// <summary>Countries, cities, zones, branches, warehouses and the shared CRUD behaviour.</summary>
public sealed class GeographyAndBranchTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<CountryDto> CreateCountryAsync(string code, string name) =>
        _context.UseAsync<ICountryService, CountryDto>(service => service.CreateAsync(
            new SaveCountryRequest { Code = code, ArabicName = name }, CancellationToken.None));

    private Task<CityDto> CreateCityAsync(long countryId, string code, string name) =>
        _context.UseAsync<ICityService, CityDto>(service => service.CreateAsync(
            new SaveCityRequest { CountryId = countryId, Code = code, ArabicName = name }, CancellationToken.None));

    [Fact]
    public async Task CountryCode_IsStoredUpperCase_AndDuplicatesAreRejected()
    {
        var syria = await CreateCountryAsync(" sy ", "سوريا");

        Assert.Equal("SY", syria.Code);
        Assert.True(syria.IsActive);
        await Assert.ThrowsAsync<ConflictException>(() => CreateCountryAsync("SY", "سوريا 2"));
    }

    [Fact]
    public async Task CityCode_IsUniquePerCountry_NotGlobally()
    {
        var syria = await CreateCountryAsync("SY", "سوريا");
        var lebanon = await CreateCountryAsync("LB", "لبنان");

        await CreateCityAsync(syria.Id, "CEN", "مركز سوريا");
        var lebaneseCity = await CreateCityAsync(lebanon.Id, "CEN", "مركز لبنان");

        Assert.Equal("CEN", lebaneseCity.Code);
        await Assert.ThrowsAsync<ConflictException>(() => CreateCityAsync(syria.Id, "cen", "مكرر"));
    }

    [Fact]
    public async Task Zone_WithUnknownCity_ReturnsValidationErrorOnCityId()
    {
        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _context.UseAsync<IZoneService, ZoneDto>(service => service.CreateAsync(
                new SaveZoneRequest { CityId = 999, Code = "Z1", ArabicName = "منطقة" }, CancellationToken.None)));

        Assert.True(exception.Errors.ContainsKey("cityId"));
    }

    [Fact]
    public async Task Zone_InInactiveCity_IsRejected()
    {
        var syria = await CreateCountryAsync("SY", "سوريا");
        var city = await CreateCityAsync(syria.Id, "HMS", "حمص");
        await _context.UseAsync<ICityService, CityDto>(service => service.SetActiveAsync(city.Id, false, CancellationToken.None));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _context.UseAsync<IZoneService, ZoneDto>(service => service.CreateAsync(
                new SaveZoneRequest { CityId = city.Id, Code = "Z1", ArabicName = "منطقة" }, CancellationToken.None)));

        Assert.Contains("غير مفعّل", exception.Errors["cityId"][0]);
    }

    [Fact]
    public async Task DeleteCountry_WithCities_IsRefused()
    {
        var syria = await CreateCountryAsync("SY", "سوريا");
        await CreateCityAsync(syria.Id, "DAM", "دمشق");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.UseAsync<ICountryService>(service => service.DeleteAsync(syria.Id, CancellationToken.None)));
    }

    [Fact]
    public async Task Delete_IsSoft_RowIsHidden_AndTheCodeIsNeverReused()
    {
        var category = await _context.UseAsync<IExpenseCategoryService, ExpenseCategoryDto>(service => service.CreateAsync(
            new SaveExpenseCategoryRequest { Code = "FUEL", ArabicName = "وقود" }, CancellationToken.None));

        await _context.UseAsync<IExpenseCategoryService>(service => service.DeleteAsync(category.Id, CancellationToken.None));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _context.UseAsync<IExpenseCategoryService, ExpenseCategoryDto>(service => service.GetByIdAsync(category.Id, CancellationToken.None)));

        // Still in the database, marked as deleted.
        var row = await _context.QueryAsync(db => db.ExpenseCategories.IgnoreQueryFilters().SingleAsync(x => x.Id == category.Id));
        Assert.True(row.IsDeleted);
        Assert.False(row.IsActive);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _context.UseAsync<IExpenseCategoryService, ExpenseCategoryDto>(service => service.CreateAsync(
                new SaveExpenseCategoryRequest { Code = "FUEL", ArabicName = "وقود 2" }, CancellationToken.None)));

        Assert.True(await _context.QueryAsync(db => db.AuditLogs.AnyAsync(a =>
            a.Action == AuditAction.MasterDataDeleted && a.EntityName == "ExpenseCategory" && a.EntityId == category.Id.ToString())));
    }

    [Fact]
    public async Task List_SupportsSearchSortPagingAndActiveFilter()
    {
        await CreateCountryAsync("SY", "سوريا");
        await CreateCountryAsync("LB", "لبنان");
        var jordan = await CreateCountryAsync("JO", "الأردن");
        await _context.UseAsync<ICountryService, CountryDto>(service => service.SetActiveAsync(jordan.Id, false, CancellationToken.None));

        var page = await _context.UseAsync<ICountryService, PagedResult<CountryDto>>(service =>
            service.GetPagedAsync(new CountryListQuery { SortBy = "code", SortDirection = "desc", PageSize = 2 }, CancellationToken.None));

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(new[] { "SY", "LB" }, page.Items.Select(x => x.Code));

        var active = await _context.UseAsync<ICountryService, PagedResult<CountryDto>>(service =>
            service.GetPagedAsync(new CountryListQuery { IsActive = true }, CancellationToken.None));
        Assert.Equal(2, active.TotalCount);

        var search = await _context.UseAsync<ICountryService, PagedResult<CountryDto>>(service =>
            service.GetPagedAsync(new CountryListQuery { Search = "لبن" }, CancellationToken.None));
        Assert.Equal("LB", Assert.Single(search.Items).Code);
    }

    [Fact]
    public async Task DeactivateAndActivate_AreAudited()
    {
        var country = await CreateCountryAsync("SY", "سوريا");

        var inactive = await _context.UseAsync<ICountryService, CountryDto>(service => service.SetActiveAsync(country.Id, false, CancellationToken.None));
        var active = await _context.UseAsync<ICountryService, CountryDto>(service => service.SetActiveAsync(country.Id, true, CancellationToken.None));

        Assert.False(inactive.IsActive);
        Assert.True(active.IsActive);
        Assert.Equal(1, await _context.QueryAsync(db => db.AuditLogs.CountAsync(a => a.Action == AuditAction.MasterDataDeactivated)));
        Assert.Equal(1, await _context.QueryAsync(db => db.AuditLogs.CountAsync(a => a.Action == AuditAction.MasterDataActivated)));
    }

    [Fact]
    public async Task Update_WritesChangedFieldsToTheAuditLog()
    {
        var country = await CreateCountryAsync("SY", "سوريا");

        await _context.UseAsync<ICountryService, CountryDto>(service => service.UpdateAsync(
            country.Id,
            new SaveCountryRequest { Code = "SY", ArabicName = "الجمهورية العربية السورية", PhoneCode = "+963" },
            CancellationToken.None));

        var audit = await _context.QueryAsync(db => db.AuditLogs.SingleAsync(a => a.Action == AuditAction.MasterDataUpdated));
        Assert.Contains("arabicName", audit.Details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("phoneCode", audit.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"code\"", audit.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OnlyOneBranch_IsTheHeadOffice()
    {
        await _context.SeedDemoDataAsync();
        var aleppoId = await _context.QueryAsync(db => db.Branches.Where(b => b.Code == "ALP").Select(b => b.Id).SingleAsync());

        var aleppo = await _context.UseAsync<IBranchService, BranchDto>(service => service.GetByIdAsync(aleppoId, CancellationToken.None));
        await _context.UseAsync<IBranchService, BranchDto>(service => service.UpdateAsync(aleppoId, new SaveBranchRequest
        {
            Code = aleppo.Code,
            ArabicName = aleppo.ArabicName,
            CityId = aleppo.CityId,
            IsHeadOffice = true
        }, CancellationToken.None));

        var headOffices = await _context.QueryAsync(db => db.Branches.Where(b => b.IsHeadOffice).Select(b => b.Code).ToListAsync());
        Assert.Equal(new[] { "ALP" }, headOffices);
    }

    [Fact]
    public async Task BranchUser_SeesOnlyTheirOwnBranch_AndCannotCreateBranches()
    {
        await _context.SeedDemoDataAsync();
        var (damascusId, aleppoId) = await _context.QueryAsync(async db => (
            await db.Branches.Where(b => b.Code == "DAM").Select(b => b.Id).SingleAsync(),
            await db.Branches.Where(b => b.Code == "ALP").Select(b => b.Id).SingleAsync()));

        _context.CurrentUser.SignInAs(50, aleppoId, Permissions.Branches.Manage);

        var branches = await _context.UseAsync<IBranchService, PagedResult<BranchDto>>(service =>
            service.GetPagedAsync(new BranchListQuery(), CancellationToken.None));
        Assert.Equal("ALP", Assert.Single(branches.Items).Code);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _context.UseAsync<IBranchService, BranchDto>(service => service.GetByIdAsync(damascusId, CancellationToken.None)));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _context.UseAsync<IBranchService, BranchDto>(service => service.CreateAsync(
                new SaveBranchRequest { Code = "HMS", ArabicName = "فرع حمص", CityId = 1 }, CancellationToken.None)));

        // A user with Branches.ViewAll sees both.
        _context.CurrentUser.SignInAs(51, aleppoId, Permissions.Branches.ViewAll);
        var all = await _context.UseAsync<IBranchService, PagedResult<BranchDto>>(service =>
            service.GetPagedAsync(new BranchListQuery(), CancellationToken.None));
        Assert.Equal(2, all.TotalCount);
    }

    [Fact]
    public async Task BranchUser_CannotCreateWarehouseInAnotherBranch()
    {
        await _context.SeedDemoDataAsync();
        var (damascusId, aleppoId) = await _context.QueryAsync(async db => (
            await db.Branches.Where(b => b.Code == "DAM").Select(b => b.Id).SingleAsync(),
            await db.Branches.Where(b => b.Code == "ALP").Select(b => b.Id).SingleAsync()));

        _context.CurrentUser.SignInAs(52, aleppoId, Permissions.Branches.Manage);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _context.UseAsync<IWarehouseService, WarehouseDto>(service => service.CreateAsync(
                new SaveWarehouseRequest { Code = "X-WH", ArabicName = "مستودع", BranchId = damascusId }, CancellationToken.None)));

        var own = await _context.UseAsync<IWarehouseService, WarehouseDto>(service => service.CreateAsync(
            new SaveWarehouseRequest { Code = "ALP-WH2", ArabicName = "مستودع حلب 2", BranchId = aleppoId }, CancellationToken.None));
        Assert.Equal("فرع حلب", own.BranchName);

        var visible = await _context.UseAsync<IWarehouseService, PagedResult<WarehouseDto>>(service =>
            service.GetPagedAsync(new WarehouseListQuery(), CancellationToken.None));
        Assert.All(visible.Items, warehouse => Assert.Equal(aleppoId, warehouse.BranchId));
    }

    [Fact]
    public async Task DeleteBranch_WithWarehouses_IsRefused()
    {
        await _context.SeedDemoDataAsync();
        var aleppoId = await _context.QueryAsync(db => db.Branches.Where(b => b.Code == "ALP").Select(b => b.Id).SingleAsync());

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.UseAsync<IBranchService>(service => service.DeleteAsync(aleppoId, CancellationToken.None)));

        Assert.Contains("مستودعات", exception.Message);
    }
}
