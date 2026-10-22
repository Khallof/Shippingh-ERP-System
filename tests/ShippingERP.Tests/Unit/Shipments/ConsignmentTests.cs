using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Shipments;

/// <summary>Step 9: رقم الإرسالية on shipments, the bulk assignment and the "selected rows" (ids) filter.</summary>
public sealed class ConsignmentTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        _ids = await ShipmentTestData.LoadIdsAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<PagedResult<ShipmentListItemDto>> ListAsync(ShipmentListQuery query) =>
        _context.UseAsync<IShipmentService, PagedResult<ShipmentListItemDto>>(s => s.GetPagedAsync(query, CancellationToken.None));

    [Fact]
    public async Task Consignment_IsSavedOnTheShipment_AssignedInBulk_AndFiltered()
    {
        var request = ShipmentTestData.NewRequest(_ids);
        request.ConsignmentNumber = " 1 ";
        var first = await ShipmentTestData.CreateAsync(_context, request);
        Assert.Equal("1", first.ConsignmentNumber);

        var second = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        var third = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        var changed = await _context.UseAsync<IShipmentService, int>(s => s.SetConsignmentAsync(
            new SetConsignmentRequest { ShipmentIds = new List<long> { second.Id, third.Id }, ConsignmentNumber = "2" }, CancellationToken.None));
        Assert.Equal(2, changed);

        var consignment2 = await ListAsync(new ShipmentListQuery { ConsignmentNumber = "2" });
        Assert.Equal(new[] { second.Id, third.Id }.OrderBy(x => x), consignment2.Items.Select(x => x.Id).OrderBy(x => x));
        Assert.All(consignment2.Items, x => Assert.Equal("2", x.ConsignmentNumber));

        var selected = await ListAsync(new ShipmentListQuery { Ids = new List<long> { first.Id, third.Id } });
        Assert.Equal(2, selected.TotalCount);
    }
}
