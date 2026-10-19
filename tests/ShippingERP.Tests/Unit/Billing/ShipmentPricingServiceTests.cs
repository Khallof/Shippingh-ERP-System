using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Pricing;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Pricing;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Billing;

/// <summary>
/// The pricing engine with the demo rules (step 5): fees, contracts, repricing and the approval of manual prices.
/// Demo prices: standard 15,000 for 5 kg then 2,000 / kg; express 30,000 + 3,000 / kg, 1,000 per extra package;
/// pickup 3,000; packaging 2,500; insurance 1 % (min 1,000); remote area 5,000; return 7,500; tax 0 %.
/// </summary>
public sealed class ShipmentPricingServiceTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        _ids = await ShipmentTestData.LoadIdsAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<ShipmentPriceResultDto> Calculate(long shipmentId, ShipmentPriceRequest request) =>
        _context.UseAsync<IShipmentPricingService, ShipmentPriceResultDto>(s => s.CalculateAsync(shipmentId, request, CancellationToken.None));

    private Task<ShipmentPriceResultDto> Apply(long shipmentId, ShipmentPriceRequest request) =>
        _context.UseAsync<IShipmentPricingService, ShipmentPriceResultDto>(s => s.ApplyAsync(shipmentId, request, CancellationToken.None));

    private Task<IReadOnlyList<ShipmentPricingDto>> History(long shipmentId) =>
        _context.UseAsync<IShipmentService, IReadOnlyList<ShipmentPricingDto>>(s => s.GetPricingHistoryAsync(shipmentId, CancellationToken.None));

    [Fact]
    public async Task Create_ToARemoteZone_AddsTheRemoteAreaFee()
    {
        // CUS-00009 lives in Latakia / Ziraa, a remote area in the demo data.
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, recipient: "CUS-00009"));

        var pricing = Assert.Single(await History(shipment.Id));
        Assert.True(pricing.IsRemoteArea);
        Assert.Equal(5000m, pricing.RemoteAreaFee);
        Assert.Equal(20000m, shipment.TotalCharge);
    }

    [Fact]
    public async Task Create_WithOptionalServicesInsuranceAndExtraPackages_ShowsEveryFeeInTheBreakdown()
    {
        var request = ShipmentTestData.NewRequest(_ids, weightKg: 1m, serviceTypeId: _ids.Express);
        request.Packages.Add(new ShipmentPackageRequest { WeightKg = 1m });
        request.Packages.Add(new ShipmentPackageRequest { WeightKg = 1m });
        request.DeclaredValue = 50000m;
        request.InsuranceValue = 50000m;
        request.RequiresPickup = true;
        request.RequiresPackaging = true;

        var shipment = await ShipmentTestData.CreateAsync(_context, request);
        var pricing = Assert.Single(await History(shipment.Id));

        Assert.Equal(30000m, pricing.BaseCharge);
        Assert.Equal(9000m, pricing.WeightCharge);        // 3 kg x 3,000
        Assert.Equal(2000m, pricing.PackageFee);          // 2 extra packages
        Assert.Equal(1000m, pricing.InsuranceFee);        // 1 % of 50,000 = 500 -> minimum 1,000
        Assert.Equal(3000m, pricing.PickupFee);
        Assert.Equal(2500m, pricing.PackagingFee);
        Assert.Equal(8500m, pricing.ServiceFeesTotal);
        Assert.Equal(47500m, pricing.ShippingSubtotal);
        Assert.Equal(47500m, pricing.NetTotal);
        Assert.Single(pricing.WeightTiers);
        Assert.True(shipment.RequiresPickup);
        Assert.Equal(47500m, shipment.TotalCharge);
    }

    [Fact]
    public async Task CustomerContract_PricesTheCustomersShipments_AndOtherServicesFallBackToTheGeneralPlan()
    {
        // CUS-00010 has the demo contract CNT-MED: standard shipments with a 10 % discount.
        var standard = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, sender: "CUS-00010"));
        var express = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, sender: "CUS-00010", weightKg: 1m, serviceTypeId: _ids.Express));

        var contractPricing = Assert.Single(await History(standard.Id));
        Assert.Equal(PricingSource.CustomerContract, contractPricing.PricingSource);
        Assert.Equal("CNT-MED", contractPricing.PricingPlanCode);
        Assert.Equal(1500m, contractPricing.ContractDiscountAmount);
        Assert.Equal(13500m, standard.TotalCharge);

        var generalPricing = Assert.Single(await History(express.Id));
        Assert.Equal(PricingSource.PricingRule, generalPricing.PricingSource);
        Assert.Equal("GENERAL", generalPricing.PricingPlanCode);
        Assert.Equal(33000m, express.TotalCharge);
    }

    [Fact]
    public async Task CalculatePrice_ShowsTheRuleAndTheManualBreakdown_WithoutSavingAnything()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        var result = await Calculate(shipment.Id, new ShipmentPriceRequest { ManualShippingCharge = 20000m, Reason = "معاينة" });

        Assert.True(result.IsManual);
        Assert.Equal(15000m, result.RulePricing!.NetTotal);
        Assert.Equal(20000m, result.Pricing.NetTotal);
        Assert.Equal(33.33m, result.DeviationPercentage);
        Assert.True(result.RequiresApproval);
        Assert.False(result.Applied);
        Assert.Single(await History(shipment.Id));
    }

    [Fact]
    public async Task ApplyPrice_WithoutManualValues_RecalculatesWithTheCurrentRules()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        await _context.QueryAsync(async db =>
        {
            var rule = await db.PriceRules.SingleAsync(x => x.ArabicName == "عادي - أول 5 كغ مشمولة");
            rule.BasePrice = 16000m;
            return await db.SaveChangesAsync();
        });

        // The existing shipment keeps its price until it is repriced.
        Assert.Equal(15000m, (await ShipmentTestData.GetAsync(_context, shipment.Id)).TotalCharge);

        var result = await Apply(shipment.Id, new ShipmentPriceRequest());

        Assert.True(result.Applied);
        Assert.Equal(16000m, (await ShipmentTestData.GetAsync(_context, shipment.Id)).TotalCharge);
        var history = await History(shipment.Id);
        Assert.Equal(new[] { "Created", "Repriced" }, history.Select(x => x.Reason).ToArray());
    }

    [Fact]
    public async Task ApplyPrice_BigManualChange_WaitsForApproval_ThenAnotherUserApproves()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        _context.CurrentUser.SignInAs(20, _ids.Damascus,
            Permissions.Shipments.View, Permissions.Shipments.Update, Permissions.Shipments.OverridePrice);
        var requested = await Apply(shipment.Id, new ShipmentPriceRequest { ManualShippingCharge = 30000m, Reason = "شحنة بأبعاد خاصة" });

        Assert.False(requested.Applied);
        Assert.Equal(PriceOverrideStatus.Pending, requested.Override!.Status);
        Assert.Equal(100m, requested.Override.DeviationPercentage);
        Assert.Equal(15000m, (await ShipmentTestData.GetAsync(_context, shipment.Id)).TotalCharge);
        Assert.True((await ShipmentTestData.GetAsync(_context, shipment.Id)).HasPendingPriceOverride);

        // The requester cannot approve his own request, even with the permission.
        _context.CurrentUser.SignInAs(20, _ids.Damascus, Permissions.Shipments.View, Permissions.Pricing.View, Permissions.Pricing.ApproveOverride);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<IShipmentPricingService, ShipmentPriceResultDto>(s =>
            s.ApproveOverrideAsync(requested.Override.Id, new DecidePriceOverrideRequest(), CancellationToken.None)));

        _context.CurrentUser.SignInAs(21, _ids.Damascus, Permissions.Shipments.View, Permissions.Pricing.View, Permissions.Pricing.ApproveOverride);
        var approved = await _context.UseAsync<IShipmentPricingService, ShipmentPriceResultDto>(s =>
            s.ApproveOverrideAsync(requested.Override.Id, new DecidePriceOverrideRequest { Notes = "موافق" }, CancellationToken.None));

        Assert.True(approved.Applied);
        Assert.Equal(PriceOverrideStatus.Approved, approved.Override!.Status);
        Assert.Equal(21L, approved.Override.DecidedByUserId);
        var updated = await ShipmentTestData.GetAsync(_context, shipment.Id);
        Assert.Equal(30000m, updated.TotalCharge);
        Assert.False(updated.HasPendingPriceOverride);

        var last = (await History(shipment.Id)).Last();
        Assert.Equal("ManualOverride", last.Reason);
        Assert.Equal(PricingSource.Manual, last.PricingSource);
        Assert.Equal(requested.Override.Id, last.PriceOverrideId);
        Assert.Equal("شحنة بأبعاد خاصة", last.OverrideReason);
    }

    [Fact]
    public async Task ApplyPrice_SmallManualDiscount_IsAppliedAtOnce_AndRecorded()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        _context.CurrentUser.SignInAs(20, _ids.Damascus,
            Permissions.Shipments.View, Permissions.Shipments.Update, Permissions.Shipments.OverridePrice);
        var result = await Apply(shipment.Id, new ShipmentPriceRequest { ManualDiscountAmount = 1500m, Reason = "تعويض تأخير سابق" });

        Assert.True(result.Applied);
        Assert.False(result.RequiresApproval);
        Assert.Equal(10m, result.DeviationPercentage);
        Assert.Equal(PriceOverrideStatus.Approved, result.Override!.Status);
        Assert.Equal(13500m, (await ShipmentTestData.GetAsync(_context, shipment.Id)).TotalCharge);
    }

    [Fact]
    public async Task ApplyPrice_ManualWithoutOverridePermission_IsForbidden()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        _context.CurrentUser.SignInAs(20, _ids.Damascus, Permissions.Shipments.View, Permissions.Shipments.Update);

        await Assert.ThrowsAsync<ForbiddenException>(() => Apply(shipment.Id, new ShipmentPriceRequest { ManualDiscountAmount = 100m, Reason = "خصم صغير" }));
    }

    [Fact]
    public async Task RejectOverride_NeedsAReason_AndKeepsThePrice()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        _context.CurrentUser.SignInAs(20, _ids.Damascus,
            Permissions.Shipments.View, Permissions.Shipments.Update, Permissions.Shipments.OverridePrice);
        var requested = await Apply(shipment.Id, new ShipmentPriceRequest { ManualShippingCharge = 5000m, Reason = "طلب العميل" });

        _context.CurrentUser.SignInAs(21, _ids.Damascus, Permissions.Shipments.View, Permissions.Pricing.View, Permissions.Pricing.ApproveOverride);
        await Assert.ThrowsAsync<RequestValidationException>(() => _context.UseAsync<IShipmentPricingService, PriceOverrideDto>(s =>
            s.RejectOverrideAsync(requested.Override!.Id, new DecidePriceOverrideRequest(), CancellationToken.None)));

        var rejected = await _context.UseAsync<IShipmentPricingService, PriceOverrideDto>(s =>
            s.RejectOverrideAsync(requested.Override!.Id, new DecidePriceOverrideRequest { Notes = "السعر أقل من التكلفة" }, CancellationToken.None));

        Assert.Equal(PriceOverrideStatus.Rejected, rejected.Status);
        Assert.Equal(15000m, (await ShipmentTestData.GetAsync(_context, shipment.Id)).TotalCharge);
    }

    [Fact]
    public async Task ApplyPrice_IsRefused_ForAnInvoicedShipment()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Apply(shipment.Id, new ShipmentPriceRequest()));
    }

    [Fact]
    public async Task ReturnFee_IsAdded_WhenTheShipmentIsBeingReturned()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        await ShipmentTestData.ReceiveAtBranchAsync(_context, shipment.Id);
        await _context.UseAsync<IShipmentReturnService, ShipmentReturnDto>(s => s.CreateAsync(
            new CreateShipmentReturnRequest { ShipmentId = shipment.Id, ReturnType = ShipmentReturnType.SenderRequest, Reason = "طلب المرسل" },
            CancellationToken.None));

        var result = await Calculate(shipment.Id, new ShipmentPriceRequest());

        Assert.True(result.Pricing.IsReturn);
        Assert.Equal(7500m, result.Pricing.ReturnFee);
        Assert.Equal(22500m, result.Pricing.NetTotal);
    }
}
