using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.HR;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.MasterData;

/// <summary>Employees, driver profiles and vehicles (uses the demo data).</summary>
public sealed class EmployeeDriverVehicleTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private long _damascusId;
    private long _aleppoId;

    public async Task InitializeAsync()
    {
        await _context.SeedRolesAndPermissionsAsync();
        await _context.SeedDemoDataAsync();
        (_damascusId, _aleppoId) = await _context.QueryAsync(async db => (
            await db.Branches.Where(b => b.Code == "DAM").Select(b => b.Id).SingleAsync(),
            await db.Branches.Where(b => b.Code == "ALP").Select(b => b.Id).SingleAsync()));
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<EmployeeDto> CreateEmployeeAsync(string code, long branchId, long? userId = null) =>
        _context.UseAsync<IEmployeeService, EmployeeDto>(service => service.CreateAsync(
            new SaveEmployeeRequest { Code = code, ArabicName = "موظف " + code, BranchId = branchId, UserId = userId },
            CancellationToken.None));

    private Task<VehicleDto> CreateVehicleAsync(string plate, long branchId) =>
        _context.UseAsync<IVehicleService, VehicleDto>(service => service.CreateAsync(
            new SaveVehicleRequest { PlateNumber = plate, Model = "Kia", VehicleType = VehicleType.Car, CapacityKg = 300m, BranchId = branchId },
            CancellationToken.None));

    [Fact]
    public async Task PlateNumber_IsNormalized_AndUnique()
    {
        var vehicle = await CreateVehicleAsync("  dam   777-888 ", _damascusId);

        Assert.Equal("DAM 777-888", vehicle.PlateNumber);
        await Assert.ThrowsAsync<ConflictException>(() => CreateVehicleAsync("DAM 777-888", _aleppoId));
    }

    [Fact]
    public async Task Driver_VehicleMustBelongToTheEmployeesBranch()
    {
        var employee = await CreateEmployeeAsync("EMP-100", _damascusId);
        var aleppoVehicle = await CreateVehicleAsync("ALP 111-222", _aleppoId);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _context.UseAsync<IDriverService, DriverDto>(service => service.CreateAsync(
                new SaveDriverRequest
                {
                    EmployeeId = employee.Id,
                    LicenseNumber = "DL-9001",
                    LicenseExpiryDateUtc = DateTime.UtcNow.AddYears(1),
                    DefaultVehicleId = aleppoVehicle.Id
                },
                CancellationToken.None)));

        Assert.True(exception.Errors.ContainsKey("defaultVehicleId"));
    }

    [Fact]
    public async Task Driver_OneProfilePerEmployee_AndBranchComesFromTheEmployee()
    {
        var employee = await CreateEmployeeAsync("EMP-101", _aleppoId);
        var request = new SaveDriverRequest
        {
            EmployeeId = employee.Id,
            LicenseNumber = "dl-9002",
            LicenseExpiryDateUtc = DateTime.UtcNow.AddYears(1)
        };

        var driver = await _context.UseAsync<IDriverService, DriverDto>(service => service.CreateAsync(request, CancellationToken.None));
        Assert.Equal(_aleppoId, driver.BranchId);
        Assert.Equal("DL-9002", driver.LicenseNumber);
        Assert.False(driver.IsLicenseExpired);

        request.LicenseNumber = "DL-9003";
        await Assert.ThrowsAsync<ConflictException>(() =>
            _context.UseAsync<IDriverService, DriverDto>(service => service.CreateAsync(request, CancellationToken.None)));

        var employeeDto = await _context.UseAsync<IEmployeeService, EmployeeDto>(service => service.GetByIdAsync(employee.Id, CancellationToken.None));
        Assert.True(employeeDto.IsDriver);
    }

    [Fact]
    public async Task Driver_UsesTheEmployeesUserWhenNoneIsGiven()
    {
        var userId = await _context.CreateUserAsync("driver.app", Roles.Driver);
        var employee = await CreateEmployeeAsync("EMP-102", _damascusId, userId);

        var driver = await _context.UseAsync<IDriverService, DriverDto>(service => service.CreateAsync(
            new SaveDriverRequest { EmployeeId = employee.Id, LicenseNumber = "DL-9004", LicenseExpiryDateUtc = DateTime.UtcNow.AddYears(1) },
            CancellationToken.None));

        Assert.Equal(userId, driver.UserId);
        Assert.Equal("driver.app", driver.UserName);
    }

    [Fact]
    public async Task SystemUser_CanBeLinkedToOnlyOneEmployee()
    {
        var userId = await _context.CreateUserAsync("shared.user", Roles.ShippingAgent);
        await CreateEmployeeAsync("EMP-103", _damascusId, userId);

        await Assert.ThrowsAsync<ConflictException>(() => CreateEmployeeAsync("EMP-104", _damascusId, userId));
    }

    [Fact]
    public async Task DeleteEmployee_WithDriverProfile_IsRefused()
    {
        var driverEmployeeId = await _context.QueryAsync(db => db.DriverProfiles.Select(d => d.EmployeeId).FirstAsync());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.UseAsync<IEmployeeService>(service => service.DeleteAsync(driverEmployeeId, CancellationToken.None)));
    }

    [Fact]
    public async Task DeleteVehicle_UsedByADriver_IsRefused()
    {
        var vehicleId = await _context.QueryAsync(db => db.DriverProfiles.Where(d => d.DefaultVehicleId != null).Select(d => d.DefaultVehicleId!.Value).FirstAsync());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.UseAsync<IVehicleService>(service => service.DeleteAsync(vehicleId, CancellationToken.None)));
    }

    [Fact]
    public async Task DriverList_CanShowLicensesExpiringSoon()
    {
        var expiring = await _context.UseAsync<IDriverService, PagedResult<DriverDto>>(service =>
            service.GetPagedAsync(new DriverListQuery { LicenseExpiringSoon = true }, CancellationToken.None));

        Assert.Equal("DL-100300", Assert.Single(expiring.Items).LicenseNumber);
    }

    [Fact]
    public async Task BranchUser_SeesOnlyEmployeesVehiclesAndDriversOfTheirBranch()
    {
        _context.CurrentUser.SignInAs(70, _damascusId, Permissions.Employees.View, Permissions.Vehicles.View);

        var employees = await _context.UseAsync<IEmployeeService, PagedResult<EmployeeDto>>(service =>
            service.GetPagedAsync(new EmployeeListQuery(), CancellationToken.None));
        var vehicles = await _context.UseAsync<IVehicleService, PagedResult<VehicleDto>>(service =>
            service.GetPagedAsync(new VehicleListQuery(), CancellationToken.None));
        var drivers = await _context.UseAsync<IDriverService, PagedResult<DriverDto>>(service =>
            service.GetPagedAsync(new DriverListQuery(), CancellationToken.None));

        Assert.Equal(2, employees.TotalCount);
        Assert.Equal("DAM 123-456", Assert.Single(vehicles.Items).PlateNumber);
        Assert.Equal("علي حسين", Assert.Single(drivers.Items).EmployeeName);
    }
}
