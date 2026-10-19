using Microsoft.EntityFrameworkCore;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.MasterData;

public sealed class DemoDataSeederTests
{
    [Fact]
    public async Task DemoData_HasTheRequiredRecords_AndRunningTwiceAddsNothing()
    {
        await using var context = new IdentityTestContext();

        await context.SeedDemoDataAsync();
        await context.SeedDemoDataAsync();

        var counts = await context.QueryAsync(async db => new
        {
            Companies = await db.CompanyProfiles.CountAsync(),
            Branches = await db.Branches.CountAsync(),
            Warehouses = await db.Warehouses.CountAsync(),
            Countries = await db.Countries.CountAsync(),
            Cities = await db.Cities.CountAsync(),
            Zones = await db.Zones.CountAsync(),
            ServiceTypes = await db.ServiceTypes.CountAsync(),
            PaymentMethods = await db.PaymentMethods.CountAsync(),
            Currencies = await db.Currencies.CountAsync(),
            BaseCurrencies = await db.Currencies.CountAsync(c => c.IsBaseCurrency),
            TaxRates = await db.TaxRates.CountAsync(),
            ExpenseCategories = await db.ExpenseCategories.CountAsync(),
            Customers = await db.Customers.CountAsync(),
            CustomersWithDefaultAddress = await db.Customers.CountAsync(c => c.Addresses.Any(a => a.IsDefault)),
            Employees = await db.Employees.CountAsync(),
            Drivers = await db.DriverProfiles.CountAsync(),
            Vehicles = await db.Vehicles.CountAsync(),
            HeadOffices = await db.Branches.CountAsync(b => b.IsHeadOffice)
        });

        Assert.Equal(1, counts.Companies);
        Assert.Equal(2, counts.Branches);
        Assert.Equal(3, counts.Warehouses);
        Assert.Equal(2, counts.Countries);
        Assert.Equal(5, counts.Cities);
        Assert.Equal(13, counts.Zones);
        Assert.Equal(3, counts.ServiceTypes);
        Assert.Equal(5, counts.PaymentMethods);
        Assert.Equal(3, counts.Currencies);
        Assert.Equal(1, counts.BaseCurrencies);
        Assert.Equal(3, counts.TaxRates);
        Assert.Equal(7, counts.ExpenseCategories);
        Assert.Equal(10, counts.Customers);
        Assert.Equal(10, counts.CustomersWithDefaultAddress);
        Assert.Equal(3, counts.Employees);
        Assert.Equal(2, counts.Drivers);
        Assert.Equal(2, counts.Vehicles);
        Assert.Equal(1, counts.HeadOffices);
    }
}
