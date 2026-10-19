using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Shipments;

/// <summary>Warehouse manifests, branch transfers, driver manifests and proof of delivery.</summary>
public sealed class ShipmentOperationsTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        _ids = await ShipmentTestData.LoadIdsAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private async Task<ShipmentDto> NewShipmentAtBranchAsync(string recipient = "CUS-00002", decimal cod = 0m)
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, recipient: recipient, codAmount: cod));
        return await ShipmentTestData.ReceiveAtBranchAsync(_context, shipment.Id);
    }

    // ------------------------------------------------------------------ warehouse

    [Fact]
    public async Task WarehouseCheckIn_Confirm_PutsShipmentsInTheWarehouse()
    {
        var first = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        var second = await NewShipmentAtBranchAsync();

        var manifest = await _context.UseAsync<IWarehouseManifestService, WarehouseManifestDto>(s => s.CreateAsync(
            new CreateWarehouseManifestRequest
            {
                MovementType = WarehouseMovementType.CheckIn,
                WarehouseId = _ids.DamascusWarehouse,
                Codes = new List<string> { first.TrackingNumber, second.ShipmentNumber }
            },
            CancellationToken.None));

        Assert.StartsWith("WHM-", manifest.ManifestNumber);
        Assert.Equal(2, manifest.ItemCount);

        var confirmed = await _context.UseAsync<IWarehouseManifestService, WarehouseManifestDto>(s => s.ConfirmAsync(manifest.Id, CancellationToken.None));
        Assert.Equal(WarehouseManifestStatus.Confirmed, confirmed.Status);

        var shipment = await ShipmentTestData.GetAsync(_context, first.Id);
        Assert.Equal(ShipmentStatus.InWarehouse, shipment.Status);
        Assert.Equal(_ids.DamascusWarehouse, shipment.CurrentWarehouseId);

        var history = await _context.UseAsync<IShipmentService, IReadOnlyList<ShipmentStatusHistoryDto>>(s => s.GetHistoryAsync(first.Id, CancellationToken.None));
        Assert.Equal(manifest.ManifestNumber, history.Last().ReferenceNumber);
    }

    [Fact]
    public async Task WarehouseCheckIn_ShipmentOfAnotherBranch_IsRejected()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _context.UseAsync<IWarehouseManifestService, WarehouseManifestDto>(s => s.CreateAsync(
                new CreateWarehouseManifestRequest
                {
                    WarehouseId = _ids.AleppoWarehouse,
                    Codes = new List<string> { shipment.TrackingNumber, "SE0000000000" }
                },
                CancellationToken.None)));

        Assert.Equal(2, exception.Errors["codes"].Length);
    }

    // ------------------------------------------------------------------ branch transfers

    private async Task<BranchTransferDto> DispatchToAleppoAsync(params ShipmentDto[] shipments)
    {
        var transfer = await _context.UseAsync<IBranchTransferService, BranchTransferDto>(s => s.CreateAsync(
            new CreateBranchTransferRequest
            {
                FromBranchId = _ids.Damascus,
                ToBranchId = _ids.Aleppo,
                Codes = shipments.Select(x => x.TrackingNumber).ToList()
            },
            CancellationToken.None));

        return await _context.UseAsync<IBranchTransferService, BranchTransferDto>(s =>
            s.DispatchAsync(transfer.Id, new DispatchBranchTransferRequest { VehicleId = _ids.Van, SealNumber = "S-77" }, CancellationToken.None));
    }

    [Fact]
    public async Task BranchTransfer_DispatchAndReceive_MovesShipmentsToTheDestinationBranch()
    {
        var shipment = await NewShipmentAtBranchAsync(recipient: "CUS-00006"); // Aleppo recipient

        var dispatched = await DispatchToAleppoAsync(shipment);
        Assert.Equal(BranchTransferStatus.Dispatched, dispatched.Status);
        Assert.Equal(ShipmentStatus.InTransit, (await ShipmentTestData.GetAsync(_context, shipment.Id)).Status);

        // Only the receiving branch can receive.
        _context.CurrentUser.SignInAs(30, _ids.Damascus, Permissions.Transfers.Receive, Permissions.Transfers.View);
        await Assert.ThrowsAsync<ForbiddenException>(() => _context.UseAsync<IBranchTransferService, BranchTransferDto>(s =>
            s.ReceiveAsync(dispatched.Id, new ReceiveBranchTransferRequest(), CancellationToken.None)));

        _context.CurrentUser.SignInAs(31, _ids.Aleppo, Permissions.Transfers.Receive, Permissions.Transfers.View, Permissions.Shipments.View);
        var received = await _context.UseAsync<IBranchTransferService, BranchTransferDto>(s =>
            s.ReceiveAsync(dispatched.Id, new ReceiveBranchTransferRequest(), CancellationToken.None));

        Assert.Equal(BranchTransferStatus.Received, received.Status);
        var arrived = await ShipmentTestData.GetAsync(_context, shipment.Id);
        Assert.Equal(ShipmentStatus.ArrivedAtDestination, arrived.Status);
        Assert.Equal(_ids.Aleppo, arrived.CurrentBranchId);
    }

    [Fact]
    public async Task BranchTransfer_PartialReceive_MarksMissingShipments()
    {
        var arrives = await NewShipmentAtBranchAsync(recipient: "CUS-00006");
        var missing = await NewShipmentAtBranchAsync(recipient: "CUS-00006");
        var dispatched = await DispatchToAleppoAsync(arrives, missing);

        var received = await _context.UseAsync<IBranchTransferService, BranchTransferDto>(s => s.ReceiveAsync(
            dispatched.Id,
            new ReceiveBranchTransferRequest { ReceivedCodes = new List<string> { arrives.TrackingNumber } },
            CancellationToken.None));

        Assert.Equal(BranchTransferStatus.PartiallyReceived, received.Status);
        Assert.Equal(1, received.ReceivedCount);
        Assert.Equal(1, received.MissingCount);
        Assert.Equal(ShipmentStatus.InTransit, (await ShipmentTestData.GetAsync(_context, missing.Id)).Status);
    }

    [Fact]
    public async Task Shipment_OnAnOpenTransfer_CannotBeAddedElsewhere_OrCancelled()
    {
        var shipment = await NewShipmentAtBranchAsync();
        await _context.UseAsync<IBranchTransferService, BranchTransferDto>(s => s.CreateAsync(
            new CreateBranchTransferRequest { FromBranchId = _ids.Damascus, ToBranchId = _ids.Aleppo, Codes = new List<string> { shipment.TrackingNumber } },
            CancellationToken.None));

        await Assert.ThrowsAsync<RequestValidationException>(() => _context.UseAsync<IBranchTransferService, BranchTransferDto>(s => s.CreateAsync(
            new CreateBranchTransferRequest { FromBranchId = _ids.Damascus, ToBranchId = _ids.Aleppo, Codes = new List<string> { shipment.TrackingNumber } },
            CancellationToken.None)));

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<IShipmentService, ShipmentDto>(s =>
            s.CancelAsync(shipment.Id, new CancelShipmentRequest { Reason = "test" }, CancellationToken.None)));
    }

    // ------------------------------------------------------------------ driver manifests and delivery

    private async Task<DriverManifestDto> DispatchToDamascusDriverAsync(params ShipmentDto[] shipments)
    {
        var manifest = await _context.UseAsync<IDriverManifestService, DriverManifestDto>(s => s.CreateAsync(
            new CreateDriverManifestRequest { DriverId = _ids.DamascusDriver, Codes = shipments.Select(x => x.TrackingNumber).ToList() },
            CancellationToken.None));

        return await _context.UseAsync<IDriverManifestService, DriverManifestDto>(s => s.DispatchAsync(manifest.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DriverManifest_Dispatch_DeliverAndClose()
    {
        var delivered = await NewShipmentAtBranchAsync(cod: 50000m);
        var failed = await NewShipmentAtBranchAsync();

        var manifest = await DispatchToDamascusDriverAsync(delivered, failed);
        Assert.Equal(DriverManifestStatus.Dispatched, manifest.Status);
        Assert.Equal(50000m, manifest.TotalCodAmount);

        var outForDelivery = await ShipmentTestData.GetAsync(_context, delivered.Id);
        Assert.Equal(ShipmentStatus.OutForDelivery, outForDelivery.Status);
        Assert.Equal(_ids.DamascusDriver, outForDelivery.AssignedDriverId);
        Assert.Equal(1, outForDelivery.DeliveryAttemptCount);

        var proof = await _context.UseAsync<IDeliveryService, ProofOfDeliveryDto>(s => s.DeliverAsync(
            delivered.Id,
            new DeliverShipmentRequest { RecipientName = "سارة", RecipientRelation = "المستلم نفسه" },
            ShipmentTestData.Png(),
            null,
            CancellationToken.None));

        Assert.Equal(manifest.Id, proof.DriverManifestId);
        Assert.Equal(_ids.DamascusDriver, proof.DriverId);
        Assert.NotNull(proof.SignatureUrl);
        Assert.Equal(ShipmentStatus.Delivered, (await ShipmentTestData.GetAsync(_context, delivered.Id)).Status);

        var closed = await _context.UseAsync<IDriverManifestService, DriverManifestDto>(s =>
            s.CloseAsync(manifest.Id, new CloseDriverManifestRequest { PendingReason = "المستلم غير موجود" }, CancellationToken.None));

        Assert.Equal(DriverManifestStatus.Closed, closed.Status);
        Assert.Equal(1, closed.DeliveredCount);
        Assert.Equal(1, closed.FailedCount);
        Assert.Equal(ShipmentStatus.DeliveryFailed, (await ShipmentTestData.GetAsync(_context, failed.Id)).Status);
    }

    [Fact]
    public async Task Deliver_StoresTheSignatureUnderAGeneratedName()
    {
        var shipment = await NewShipmentAtBranchAsync();
        await DispatchToDamascusDriverAsync(shipment);

        await _context.UseAsync<IDeliveryService, ProofOfDeliveryDto>(s => s.DeliverAsync(
            shipment.Id, new DeliverShipmentRequest { RecipientName = "سارة" }, ShipmentTestData.Png("../../evil.png"), null, CancellationToken.None));

        var attachment = await _context.QueryAsync(db => db.ShipmentAttachments.SingleAsync(x => x.ShipmentId == shipment.Id));
        Assert.Equal(ShipmentAttachmentType.Signature, attachment.AttachmentType);
        Assert.Equal("evil.png", attachment.OriginalFileName);
        Assert.DoesNotContain("evil", attachment.StorageKey);
        Assert.Equal("image/png", attachment.ContentType);
        Assert.True(File.Exists(Path.Combine(_context.UploadsRoot, attachment.StorageKey)));
    }

    [Fact]
    public async Task Deliver_WithAFakeImage_IsRejected_AndNothingChanges()
    {
        var shipment = await NewShipmentAtBranchAsync();
        await DispatchToDamascusDriverAsync(shipment);
        var fake = ShipmentTestData.File("<script>alert(1)</script>"u8.ToArray(), "photo.png");

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => _context.UseAsync<IDeliveryService, ProofOfDeliveryDto>(s =>
            s.DeliverAsync(shipment.Id, new DeliverShipmentRequest { RecipientName = "سارة" }, null, fake, CancellationToken.None)));

        Assert.True(exception.Errors.ContainsKey("photo"));
        Assert.Equal(ShipmentStatus.OutForDelivery, (await ShipmentTestData.GetAsync(_context, shipment.Id)).Status);
        Assert.Equal(0, await _context.QueryAsync(db => db.ProofsOfDelivery.CountAsync()));
    }

    [Fact]
    public async Task CounterPickup_ByBranchStaff_DeliversWithoutADriver()
    {
        var shipment = await NewShipmentAtBranchAsync();

        var proof = await _context.UseAsync<IDeliveryService, ProofOfDeliveryDto>(s => s.DeliverAsync(
            shipment.Id, new DeliverShipmentRequest { RecipientName = "سارة يوسف" }, null, null, CancellationToken.None));

        Assert.Null(proof.DriverId);
        Assert.Equal(ShipmentStatus.Delivered, (await ShipmentTestData.GetAsync(_context, shipment.Id)).Status);
    }

    [Fact]
    public async Task DeliveryFailure_IsOnlyPossibleForShipmentsOutForDelivery()
    {
        var shipment = await NewShipmentAtBranchAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<IDeliveryService, ShipmentDto>(s =>
            s.RecordFailureAsync(shipment.Id, new RecordDeliveryFailureRequest { Reason = "لا أحد" }, CancellationToken.None)));
    }

    [Fact]
    public async Task DeliveredShipment_CannotGoBackOnATransfer()
    {
        var shipment = await NewShipmentAtBranchAsync();
        await _context.UseAsync<IDeliveryService, ProofOfDeliveryDto>(s => s.DeliverAsync(
            shipment.Id, new DeliverShipmentRequest { RecipientName = "سارة" }, null, null, CancellationToken.None));

        await Assert.ThrowsAsync<RequestValidationException>(() => _context.UseAsync<IBranchTransferService, BranchTransferDto>(s => s.CreateAsync(
            new CreateBranchTransferRequest { FromBranchId = _ids.Damascus, ToBranchId = _ids.Aleppo, Codes = new List<string> { shipment.TrackingNumber } },
            CancellationToken.None)));
    }

    [Fact]
    public async Task Driver_SeesOnlyHisManifests_AndCanDeliverOnlyHisShipments()
    {
        await _context.SeedRolesAndPermissionsAsync();
        var mine = await NewShipmentAtBranchAsync();
        var notMine = await NewShipmentAtBranchAsync();
        var manifest = await DispatchToDamascusDriverAsync(mine);

        var userId = await ShipmentTestData.LinkDriverUserAsync(_context, _ids.DamascusDriver, "driver.ali", _ids.Damascus);
        _context.CurrentUser.SignInWithRole(userId, _ids.Damascus, Roles.Driver,
            Permissions.Shipments.View, Permissions.Shipments.ChangeStatus, Permissions.Shipments.ConfirmDelivery);

        var manifests = await _context.UseAsync<IDriverManifestService, Application.Common.Models.PagedResult<DriverManifestDto>>(s =>
            s.GetPagedAsync(new DriverManifestListQuery(), CancellationToken.None));
        Assert.Equal(manifest.Id, Assert.Single(manifests.Items).Id);

        await Assert.ThrowsAsync<NotFoundException>(() => _context.UseAsync<IDeliveryService, ProofOfDeliveryDto>(s =>
            s.DeliverAsync(notMine.Id, new DeliverShipmentRequest { RecipientName = "x" }, null, null, CancellationToken.None)));

        var proof = await _context.UseAsync<IDeliveryService, ProofOfDeliveryDto>(s =>
            s.DeliverAsync(mine.Id, new DeliverShipmentRequest { RecipientName = "سارة" }, null, null, CancellationToken.None));
        Assert.Equal(_ids.DamascusDriver, proof.DriverId);

        // A driver cannot manage manifests.
        await Assert.ThrowsAsync<ForbiddenException>(() => _context.UseAsync<IDriverManifestService, DriverManifestDto>(s =>
            s.CloseAsync(manifest.Id, new CloseDriverManifestRequest(), CancellationToken.None)));
    }
}
