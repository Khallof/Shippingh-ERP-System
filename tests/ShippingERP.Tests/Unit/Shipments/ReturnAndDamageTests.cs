using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Shipments;

/// <summary>Return-to-sender workflow and damage / loss cases.</summary>
public sealed class ReturnAndDamageTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        _ids = await ShipmentTestData.LoadIdsAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private async Task<ShipmentDto> NewShipmentAtBranchAsync(string recipient = "CUS-00002")
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, recipient: recipient));
        return await ShipmentTestData.ReceiveAtBranchAsync(_context, shipment.Id);
    }

    private Task<ShipmentReturnDto> CreateReturnAsync(long shipmentId, ShipmentReturnType type, long? returnBranchId = null) =>
        _context.UseAsync<IShipmentReturnService, ShipmentReturnDto>(s => s.CreateAsync(
            new CreateShipmentReturnRequest { ShipmentId = shipmentId, ReturnType = type, Reason = "طلب المرسل", ReturnBranchId = returnBranchId },
            CancellationToken.None));

    private Task<ProofOfDeliveryDto> DeliverAtCounterAsync(long shipmentId) =>
        _context.UseAsync<IDeliveryService, ProofOfDeliveryDto>(s => s.DeliverAsync(
            shipmentId, new DeliverShipmentRequest { RecipientName = "سارة" }, null, null, CancellationToken.None));

    [Fact]
    public async Task Return_AtTheOriginBranch_CompletesAsReturned()
    {
        var shipment = await NewShipmentAtBranchAsync();

        var opened = await CreateReturnAsync(shipment.Id, ShipmentReturnType.SenderRequest);
        Assert.StartsWith("RET-", opened.ReturnNumber);
        Assert.Equal(_ids.Damascus, opened.ReturnBranchId);

        // While the return is open the shipment cannot be delivered to the recipient.
        await Assert.ThrowsAsync<BusinessRuleException>(() => DeliverAtCounterAsync(shipment.Id));

        var completed = await _context.UseAsync<IShipmentReturnService, ShipmentReturnDto>(s =>
            s.CompleteAsync(opened.Id, new CompleteShipmentReturnRequest { Notes = "استلمها المرسل" }, CancellationToken.None));

        Assert.Equal(ShipmentReturnStatus.Completed, completed.Status);
        var returned = await ShipmentTestData.GetAsync(_context, shipment.Id);
        Assert.Equal(ShipmentStatus.Returned, returned.Status);
        Assert.NotNull(returned.ReturnedAtUtc);
    }

    [Fact]
    public async Task Return_ToAnotherBranch_CannotCompleteBeforeTheShipmentArrives()
    {
        var shipment = await NewShipmentAtBranchAsync();
        var opened = await CreateReturnAsync(shipment.Id, ShipmentReturnType.SenderRequest, _ids.Aleppo);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<IShipmentReturnService, ShipmentReturnDto>(s =>
            s.CompleteAsync(opened.Id, new CompleteShipmentReturnRequest(), CancellationToken.None)));
    }

    [Fact]
    public async Task AfterDeliveryReturn_IsTheOnlyNormalWayOutOfDelivered()
    {
        var shipment = await NewShipmentAtBranchAsync();
        await DeliverAtCounterAsync(shipment.Id);

        // Other return types are refused for a delivered shipment.
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateReturnAsync(shipment.Id, ShipmentReturnType.SenderRequest));

        var opened = await CreateReturnAsync(shipment.Id, ShipmentReturnType.AfterDelivery);

        // Must be received back before completion.
        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<IShipmentReturnService, ShipmentReturnDto>(s =>
            s.CompleteAsync(opened.Id, new CompleteShipmentReturnRequest(), CancellationToken.None)));

        var received = await _context.UseAsync<IShipmentReturnService, ShipmentReturnDto>(s =>
            s.ReceiveBackAsync(opened.Id, new ReceiveShipmentReturnRequest { BranchId = _ids.Damascus }, CancellationToken.None));
        Assert.Equal(ShipmentStatus.ReceivedAtBranch, received.ShipmentStatus);

        await _context.UseAsync<IShipmentReturnService, ShipmentReturnDto>(s =>
            s.CompleteAsync(opened.Id, new CompleteShipmentReturnRequest(), CancellationToken.None));

        var history = await _context.UseAsync<IShipmentService, IReadOnlyList<ShipmentStatusHistoryDto>>(s => s.GetHistoryAsync(shipment.Id, CancellationToken.None));
        Assert.Equal(
            new[] { ShipmentStatus.Created, ShipmentStatus.ReceivedAtBranch, ShipmentStatus.Delivered, ShipmentStatus.ReceivedAtBranch, ShipmentStatus.Returned },
            history.Select(x => x.ToStatus).ToArray());
        Assert.Equal(opened.ReturnNumber, history[3].ReferenceNumber);
    }

    [Fact]
    public async Task OnlyOneOpenReturn_PerShipment()
    {
        var shipment = await NewShipmentAtBranchAsync();
        await CreateReturnAsync(shipment.Id, ShipmentReturnType.SenderRequest);

        await Assert.ThrowsAsync<ConflictException>(() => CreateReturnAsync(shipment.Id, ShipmentReturnType.RecipientRefused));
    }

    [Fact]
    public async Task DamageCase_LostShipment_IsMarkedAndCanBeRecovered()
    {
        var shipment = await NewShipmentAtBranchAsync();

        var opened = await _context.UseAsync<IDamageCaseService, DamageCaseDto>(s => s.CreateAsync(
            new CreateDamageCaseRequest { ShipmentId = shipment.Id, CaseType = DamageCaseType.Lost, Description = "لم يعثر عليها في الجرد", EstimatedLossAmount = 100000m },
            CancellationToken.None));

        Assert.StartsWith("DLC-", opened.CaseNumber);
        Assert.True(opened.ShipmentMarkedLostOrDamaged);
        Assert.Equal(ShipmentStatus.ReceivedAtBranch, opened.ShipmentStatusBefore);
        Assert.Equal(ShipmentStatus.LostOrDamaged, (await ShipmentTestData.GetAsync(_context, shipment.Id)).Status);

        await Assert.ThrowsAsync<ConflictException>(() => _context.UseAsync<IDamageCaseService, DamageCaseDto>(s => s.CreateAsync(
            new CreateDamageCaseRequest { ShipmentId = shipment.Id, CaseType = DamageCaseType.Damaged, Description = "ثانية" },
            CancellationToken.None)));

        var resolved = await _context.UseAsync<IDamageCaseService, DamageCaseDto>(s => s.ResolveAsync(
            opened.Id,
            new ResolveDamageCaseRequest { Resolution = DamageCaseResolution.FoundAndRecovered, Notes = "وُجدت في المستودع" },
            CancellationToken.None));

        Assert.Equal(DamageCaseStatus.Resolved, resolved.Status);
        Assert.Equal(ShipmentStatus.ReceivedAtBranch, (await ShipmentTestData.GetAsync(_context, shipment.Id)).Status);
    }

    [Fact]
    public async Task DamageCase_EvidenceAttachment_IsCounted()
    {
        var shipment = await NewShipmentAtBranchAsync();
        var opened = await _context.UseAsync<IDamageCaseService, DamageCaseDto>(s => s.CreateAsync(
            new CreateDamageCaseRequest { ShipmentId = shipment.Id, CaseType = DamageCaseType.PartiallyDamaged, Description = "كرتونة ممزقة", MarkShipmentAsLostOrDamaged = false },
            CancellationToken.None));

        await _context.UseAsync<IShipmentAttachmentService, ShipmentAttachmentDto>(s => s.UploadAsync(
            shipment.Id,
            new UploadShipmentAttachmentRequest { AttachmentType = ShipmentAttachmentType.DamageEvidence, DamageCaseId = opened.Id },
            ShipmentTestData.Png("damage.png"),
            CancellationToken.None));

        var reloaded = await _context.UseAsync<IDamageCaseService, DamageCaseDto>(s => s.GetByIdAsync(opened.Id, CancellationToken.None));
        Assert.Equal(1, reloaded.EvidenceCount);
        Assert.False(reloaded.ShipmentMarkedLostOrDamaged);
        Assert.Equal(ShipmentStatus.ReceivedAtBranch, reloaded.ShipmentStatus);
    }
}
