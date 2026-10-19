using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Shipments;

/// <summary>Registration, pricing, numbering, editing, scanning, cancelling and correcting shipments.</summary>
public sealed class ShipmentServiceTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        _ids = await ShipmentTestData.LoadIdsAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<T> Use<T>(Func<IShipmentService, Task<T>> action) => _context.UseAsync(action);

    [Fact]
    public async Task Create_GeneratesNumbersPackagesHistoryAndPrice()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, codAmount: 125000m));

        Assert.Equal($"SHP-{_context.Clock.UtcNow.Year}-000001", shipment.ShipmentNumber);
        Assert.StartsWith("SE", shipment.TrackingNumber);
        Assert.Equal(13, shipment.TrackingNumber.Length);
        Assert.Equal(ShipmentStatus.Created, shipment.Status);
        Assert.Equal("تم الإنشاء", shipment.StatusName);
        Assert.Equal(_ids.Damascus, shipment.CurrentBranchId);
        Assert.Equal(1, shipment.PackageCount);
        Assert.Equal($"{shipment.TrackingNumber}-001", Assert.Single(shipment.Packages).PackageBarcode);

        // Standard 0-5 kg = 15,000; COD fee = 2,000 + 1% x 125,000 = 3,250; default tax = 0%.
        Assert.Equal(2.5m, shipment.ChargeableWeight);
        Assert.Equal(18250m, shipment.ShippingCharge);
        Assert.Equal(0m, shipment.TaxAmount);
        Assert.Equal(18250m, shipment.TotalCharge);
        Assert.Equal("SYP", shipment.CurrencyCode);

        var history = await Use(s => s.GetHistoryAsync(shipment.Id, CancellationToken.None));
        var created = Assert.Single(history);
        Assert.Null(created.FromStatus);
        Assert.Equal(ShipmentStatus.Created, created.ToStatus);

        var pricing = Assert.Single(await Use(s => s.GetPricingHistoryAsync(shipment.Id, CancellationToken.None)));
        Assert.Equal(PricingSource.PricingRule, pricing.PricingSource);
        Assert.Equal(3250m, pricing.CodCommission);
    }

    [Fact]
    public async Task Create_TwoShipments_GetConsecutiveNumbersAndDifferentTrackingNumbers()
    {
        var first = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        var second = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        Assert.EndsWith("-000001", first.ShipmentNumber);
        Assert.EndsWith("-000002", second.ShipmentNumber);
        Assert.NotEqual(first.TrackingNumber, second.TrackingNumber);
    }

    [Fact]
    public async Task Create_FillsAddressesCitiesAndDestinationBranchAutomatically()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, recipient: "CUS-00006"));

        // CUS-00006 lives in Aleppo (Hamdaniyeh); Aleppo has exactly one branch.
        Assert.NotNull(shipment.RecipientAddressId);
        Assert.Equal("حلب", shipment.DestinationCityName);
        Assert.Equal("الحمدانية", shipment.DestinationZoneName);
        Assert.Equal(_ids.Aleppo, shipment.DestinationBranchId);
        Assert.Equal("دمشق", shipment.OriginCityName);
    }

    [Fact]
    public async Task Create_UsesTheVolumetricWeight_WhenItIsBigger()
    {
        var request = ShipmentTestData.NewRequest(_ids, weightKg: 2m);
        request.Packages[0].LengthCm = 50m;
        request.Packages[0].WidthCm = 40m;
        request.Packages[0].HeightCm = 30m;

        var shipment = await ShipmentTestData.CreateAsync(_context, request);

        // 50 x 40 x 30 / 5000 = 12 kg. Standard 5+ kg: 15,000 + (12 - 5) x 2,000 = 29,000.
        Assert.Equal(2m, shipment.ActualWeight);
        Assert.Equal(12m, shipment.VolumetricWeight);
        Assert.Equal(12m, shipment.ChargeableWeight);
        Assert.Equal(29000m, shipment.TotalCharge);
    }

    [Fact]
    public async Task Create_WithRecipientOnlyCustomerAsSender_IsRejected()
    {
        var request = ShipmentTestData.NewRequest(_ids, sender: "CUS-00002", recipient: "CUS-00001");

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => ShipmentTestData.CreateAsync(_context, request));

        Assert.True(exception.Errors.ContainsKey("senderCustomerId"));
    }

    [Fact]
    public async Task Create_WhenNoPricingRuleMatches_IsABusinessRuleViolation()
    {
        // Same-day service is priced only up to 10 kg in the demo plan.
        var request = ShipmentTestData.NewRequest(_ids, weightKg: 15m, serviceTypeId: _ids.SameDay);

        await Assert.ThrowsAsync<BusinessRuleException>(() => ShipmentTestData.CreateAsync(_context, request));
    }

    [Fact]
    public async Task Create_ManualPrice_RequiresOverridePricePermission_AndApprovalWhenTheChangeIsBig()
    {
        var request = ShipmentTestData.NewRequest(_ids);
        request.ManualShippingCharge = 50000m;
        request.DiscountAmount = 5000m;
        request.ManualPriceReason = "سعر خاص متفق عليه مع العميل";

        _context.CurrentUser.SignInAs(10, _ids.Damascus, Permissions.Shipments.Create, Permissions.Shipments.View);
        await Assert.ThrowsAsync<ForbiddenException>(() => ShipmentTestData.CreateAsync(_context, request));

        // 45,000 instead of the rule price 15,000 (+200 %) is above the 20 % threshold: an approver is needed.
        _context.CurrentUser.SignInAs(10, _ids.Damascus, Permissions.Shipments.Create, Permissions.Shipments.View, Permissions.Shipments.OverridePrice);
        await Assert.ThrowsAsync<BusinessRuleException>(() => ShipmentTestData.CreateAsync(_context, request));

        _context.CurrentUser.SignInAs(10, _ids.Damascus, Permissions.Shipments.Create, Permissions.Shipments.View, Permissions.Shipments.OverridePrice,
            Permissions.Pricing.ApproveOverride);
        var shipment = await ShipmentTestData.CreateAsync(_context, request);

        Assert.Equal(50000m, shipment.ShippingCharge);
        Assert.Equal(5000m, shipment.DiscountAmount);
        Assert.Equal(45000m, shipment.TotalCharge);
        Assert.Equal(50000m, shipment.ManualShippingCharge);
        var pricing = Assert.Single(await Use(s => s.GetPricingHistoryAsync(shipment.Id, CancellationToken.None)));
        Assert.Equal(PricingSource.Manual, pricing.PricingSource);
        Assert.Equal("سعر خاص متفق عليه مع العميل", pricing.OverrideReason);
    }

    [Fact]
    public async Task Create_SmallManualDiscount_IsAppliedWithOverridePriceOnly()
    {
        var request = ShipmentTestData.NewRequest(_ids);
        request.DiscountAmount = 1500m; // 10 % of 15,000: below the 20 % threshold
        request.ManualPriceReason = "عميل دائم";

        _context.CurrentUser.SignInAs(10, _ids.Damascus, Permissions.Shipments.Create, Permissions.Shipments.View, Permissions.Shipments.OverridePrice);
        var shipment = await ShipmentTestData.CreateAsync(_context, request);

        Assert.Equal(15000m, shipment.ShippingCharge);
        Assert.Equal(1500m, shipment.DiscountAmount);
        Assert.Equal(13500m, shipment.TotalCharge);
    }

    [Fact]
    public async Task Create_InAnotherBranch_WithoutViewAll_IsForbidden()
    {
        _context.CurrentUser.SignInAs(11, _ids.Damascus, Permissions.Shipments.Create);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, sender: "CUS-00005", originBranchId: _ids.Aleppo)));
    }

    [Fact]
    public async Task Create_WithDuplicateExternalReferenceForTheSameSender_IsRejected()
    {
        await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, externalReference: "ORD-1"));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, externalReference: "ORD-1")));
        Assert.True(exception.Errors.ContainsKey("externalReference"));

        // Another sender may use the same reference.
        await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, sender: "CUS-00007", externalReference: "ORD-1"));
    }

    [Fact]
    public async Task Quote_CalculatesWithoutSaving()
    {
        var quote = await Use(s => s.QuoteAsync(ShipmentTestData.NewRequest(_ids, weightKg: 7m), CancellationToken.None));

        // 15,000 + (7 - 5) x 2,000
        Assert.Equal(19000m, quote.TotalCharge);
        Assert.Equal(0, await _context.QueryAsync(db => db.Shipments.CountAsync()));
    }

    [Fact]
    public async Task Update_RecalculatesThePriceAndAddsASnapshot()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        var request = ShipmentTestData.NewRequest(_ids, weightKg: 8m);
        request.Packages.Add(new ShipmentPackageRequest { WeightKg = 1m });

        var updated = await Use(s => s.UpdateAsync(shipment.Id, request, CancellationToken.None));

        Assert.Equal(2, updated.PackageCount);
        Assert.Equal(9m, updated.ChargeableWeight);
        Assert.Equal(23000m, updated.TotalCharge);
        Assert.Equal(new[] { 1, 2 }, updated.Packages.Select(p => p.SequenceNumber).ToArray());
        Assert.Equal(2, (await Use(s => s.GetPricingHistoryAsync(shipment.Id, CancellationToken.None))).Count);
    }

    [Fact]
    public async Task ManualScan_PickedUp_SetsTheMilestoneAndTheHistory()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        var updated = await Use(s => s.ChangeStatusAsync(
            shipment.Id,
            new ChangeShipmentStatusRequest { Status = ShipmentStatus.PickedUp, Notes = "استلام من المرسل" },
            CancellationToken.None));

        Assert.Equal(ShipmentStatus.PickedUp, updated.Status);
        Assert.NotNull(updated.PickedUpAtUtc);
        var history = await Use(s => s.GetHistoryAsync(shipment.Id, CancellationToken.None));
        Assert.Equal(2, history.Count);
        Assert.Equal(ShipmentStatusChangeSource.Manual, history[1].Source);
        Assert.Equal("استلام من المرسل", history[1].Notes);
    }

    [Fact]
    public async Task ManualScan_ToAWarehouseOfAnotherBranch_IsRejected()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => Use(s => s.ChangeStatusAsync(
            shipment.Id,
            new ChangeShipmentStatusRequest { Status = ShipmentStatus.InWarehouse, WarehouseId = _ids.AleppoWarehouse },
            CancellationToken.None)));

        Assert.True(exception.Errors.ContainsKey("warehouseId"));
    }

    [Fact]
    public async Task Cancel_IsFinal_AndBlocksEditing()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        var cancelled = await Use(s => s.CancelAsync(shipment.Id, new CancelShipmentRequest { Reason = "طلب المرسل" }, CancellationToken.None));

        Assert.Equal(ShipmentStatus.Cancelled, cancelled.Status);
        Assert.Equal("طلب المرسل", cancelled.CancellationReason);
        Assert.NotNull(cancelled.CancelledAtUtc);
        Assert.Empty(cancelled.NextStatuses);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Use(s => s.CancelAsync(shipment.Id, new CancelShipmentRequest { Reason = "مرة ثانية" }, CancellationToken.None)));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Use(s => s.UpdateAsync(shipment.Id, ShipmentTestData.NewRequest(_ids), CancellationToken.None)));
    }

    [Fact]
    public async Task Correction_CanReopenACancelledShipment_AndIsMarkedInTheHistory()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        await Use(s => s.CancelAsync(shipment.Id, new CancelShipmentRequest { Reason = "خطأ" }, CancellationToken.None));

        var corrected = await Use(s => s.CorrectStatusAsync(
            shipment.Id,
            new CorrectShipmentStatusRequest { Status = ShipmentStatus.ReceivedAtBranch, Reason = "أُلغيت بالخطأ من موظف الاستقبال" },
            CancellationToken.None));

        Assert.Equal(ShipmentStatus.ReceivedAtBranch, corrected.Status);
        Assert.Null(corrected.CancelledAtUtc);
        Assert.Null(corrected.CancellationReason);
        var last = (await Use(s => s.GetHistoryAsync(shipment.Id, CancellationToken.None))).Last();
        Assert.True(last.IsCorrection);
        Assert.Equal(ShipmentStatusChangeSource.Correction, last.Source);
    }

    [Fact]
    public async Task StatusHistory_IsAppendOnly()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _context.QueryAsync(async db =>
        {
            var row = await db.ShipmentStatusHistory.FirstAsync(x => x.ShipmentId == shipment.Id);
            row.Notes = "edited";
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task AssignDriver_FromAnotherBranch_IsRejected_AndSameBranchWorks()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.AssignDriverAsync(
            shipment.Id, new AssignDriverRequest { DriverId = _ids.AleppoDriver }, CancellationToken.None)));

        var assigned = await Use(s => s.AssignDriverAsync(
            shipment.Id, new AssignDriverRequest { DriverId = _ids.DamascusDriver }, CancellationToken.None));

        Assert.Equal(_ids.DamascusDriver, assigned.AssignedDriverId);
        Assert.Equal(_ids.Van, assigned.VehicleId); // the driver's default vehicle
        Assert.Equal("علي حسين", assigned.AssignedDriverName);
    }

    [Fact]
    public async Task List_FiltersSearchesAndRestrictsToTheCallersBranch()
    {
        Assert.Equal(6, await _context.SeedDemoShipmentsAsync());

        Task<PagedResult<ShipmentListItemDto>> List(ShipmentListQuery query) =>
            Use(s => s.GetPagedAsync(query, CancellationToken.None));

        Assert.Equal(6, (await List(new ShipmentListQuery())).TotalCount);
        Assert.Equal(3, (await List(new ShipmentListQuery { IsCOD = true })).TotalCount);
        Assert.Equal(3, (await List(new ShipmentListQuery { Status = ShipmentStatus.Created })).TotalCount);
        Assert.Equal(1, (await List(new ShipmentListQuery { Search = "NOOR-5521" })).TotalCount);
        Assert.Equal(2, (await List(new ShipmentListQuery { Recipient = "سارة" })).TotalCount);
        Assert.Equal(2, (await List(new ShipmentListQuery { OriginBranchId = _ids.Aleppo })).TotalCount);

        // An Aleppo user sees the Aleppo shipments plus the Damascus shipment going to Aleppo.
        _context.CurrentUser.SignInAs(20, _ids.Aleppo, Permissions.Shipments.View);
        var aleppo = await List(new ShipmentListQuery());
        Assert.Equal(3, aleppo.TotalCount);
        Assert.Contains(aleppo.Items, x => x.ExternalReference == "NOOR-5521");
    }

    [Fact]
    public async Task Track_FindsTheShipmentByPackageBarcode()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        var tracking = await Use(s => s.TrackAsync(shipment.Packages[0].PackageBarcode.ToLowerInvariant(), CancellationToken.None));

        Assert.Equal(shipment.ShipmentNumber, tracking.ShipmentNumber);
        Assert.Single(tracking.Events);
    }

    [Fact]
    public async Task DriverOnlyUser_SeesOnlyShipmentsAssignedToHisProfile()
    {
        await _context.SeedRolesAndPermissionsAsync();
        await _context.SeedDemoShipmentsAsync();
        var assigned = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        await Use(s => s.AssignDriverAsync(assigned.Id, new AssignDriverRequest { DriverId = _ids.DamascusDriver }, CancellationToken.None));

        var userId = await _context.CreateUserAsync("driver.ali", Roles.Driver, branchId: _ids.Damascus);
        await _context.QueryAsync(async db =>
        {
            var profile = await db.DriverProfiles.FirstAsync(x => x.Id == _ids.DamascusDriver);
            profile.UserId = userId;
            return await db.SaveChangesAsync();
        });

        _context.CurrentUser.SignInWithRole(userId, _ids.Damascus, Roles.Driver, Permissions.Shipments.View, Permissions.Shipments.ChangeStatus);

        var visible = await Use(s => s.GetPagedAsync(new ShipmentListQuery(), CancellationToken.None));
        Assert.Equal(assigned.Id, Assert.Single(visible.Items).Id);

        var other = await _context.QueryAsync(db => db.Shipments.Where(x => x.Id != assigned.Id).Select(x => x.Id).FirstAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => Use(s => s.GetByIdAsync(other, CancellationToken.None)));
    }
}
