using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;
using ShippingERP.Infrastructure.Shipments;
using S = ShippingERP.Domain.Enums.ShipmentStatus;
using Src = ShippingERP.Domain.Enums.ShipmentStatusChangeSource;

namespace ShippingERP.Tests.Unit.Shipments;

/// <summary>State machine, weights and number formats (pure logic, no database).</summary>
public sealed class ShipmentRulesTests
{
    [Theory]
    [InlineData(S.Created, S.PickedUp, Src.Manual)]
    [InlineData(S.Created, S.Cancelled, Src.Cancellation)]
    [InlineData(S.ReceivedAtBranch, S.InWarehouse, Src.WarehouseManifest)]
    [InlineData(S.InWarehouse, S.InTransit, Src.BranchTransfer)]
    [InlineData(S.InTransit, S.ArrivedAtDestination, Src.BranchTransfer)]
    [InlineData(S.ArrivedAtDestination, S.OutForDelivery, Src.DriverManifest)]
    [InlineData(S.OutForDelivery, S.Delivered, Src.Delivery)]
    [InlineData(S.OutForDelivery, S.DeliveryFailed, Src.DriverManifest)]
    [InlineData(S.DeliveryFailed, S.Returned, Src.Return)]
    [InlineData(S.InTransit, S.LostOrDamaged, Src.DamageCase)]
    public void AllowedTransitions_AreAccepted(S from, S to, Src source)
    {
        Assert.True(ShipmentStatusRules.CanTransition(from, to, source));
    }

    [Theory]
    [InlineData(S.Delivered, S.InTransit, Src.BranchTransfer)]
    [InlineData(S.Delivered, S.ReceivedAtBranch, Src.Manual)]
    [InlineData(S.InTransit, S.ReceivedAtBranch, Src.Manual)]
    [InlineData(S.ReceivedAtBranch, S.InTransit, Src.Manual)]
    [InlineData(S.ReceivedAtBranch, S.OutForDelivery, Src.Manual)]
    [InlineData(S.OutForDelivery, S.ReceivedAtBranch, Src.Manual)]
    [InlineData(S.Created, S.Delivered, Src.Delivery)]
    [InlineData(S.Cancelled, S.ReceivedAtBranch, Src.Manual)]
    [InlineData(S.InTransit, S.Cancelled, Src.Cancellation)]
    [InlineData(S.PickedUp, S.PickedUp, Src.Manual)]
    public void InvalidTransitions_AreRefused(S from, S to, Src source)
    {
        Assert.False(ShipmentStatusRules.CanTransition(from, to, source));
    }

    [Fact]
    public void Delivered_CanOnlyBeReopenedByTheReturnWorkflowOrACorrection()
    {
        Assert.True(ShipmentStatusRules.CanTransition(S.Delivered, S.ReceivedAtBranch, Src.Return));
        Assert.True(ShipmentStatusRules.CanTransition(S.Delivered, S.InTransit, Src.Correction));
        Assert.False(ShipmentStatusRules.CanTransition(S.Delivered, S.InTransit, Src.Return));
        Assert.False(ShipmentStatusRules.CanTransition(S.Delivered, S.Created, Src.Correction));
    }

    [Fact]
    public void FinalStatuses_HaveNoNormalNextStatus()
    {
        foreach (var status in new[] { S.Delivered, S.Returned, S.Cancelled, S.LostOrDamaged })
        {
            Assert.True(ShipmentStatusRules.IsFinal(status));
            Assert.Empty(ShipmentStatusRules.NextStatuses(status));
        }

        Assert.False(ShipmentStatusRules.IsFinal(S.DeliveryFailed));
    }

    [Fact]
    public void EveryStatus_HasAnArabicName()
    {
        foreach (var status in Enum.GetValues<S>())
        {
            Assert.NotEqual(status.ToString(), ShipmentStatusNames.Arabic(status));
        }
    }

    [Theory]
    [InlineData(30, 20, 10, 5000, 1.2)]
    [InlineData(50, 40, 30, 5000, 12)]
    [InlineData(50, 40, 30, 6000, 10)]
    public void VolumetricWeight_IsLengthTimesWidthTimesHeightDividedByDivisor(int length, int width, int height, int divisor, double expected)
    {
        Assert.Equal((decimal)expected, ShipmentWeightCalculator.VolumetricWeight(length, width, height, divisor));
    }

    [Fact]
    public void VolumetricWeight_IsZero_WhenADimensionIsMissing()
    {
        Assert.Equal(0m, ShipmentWeightCalculator.VolumetricWeight(30m, null, 10m, 5000));
    }

    [Theory]
    [InlineData(2.1, 1.2, 0.5, 2.5)]
    [InlineData(2.0, 1.2, 0.5, 2.0)]
    [InlineData(1.0, 12, 0.5, 12)]
    [InlineData(2.1234, 0, 0, 2.123)]
    [InlineData(0.2, 0, 1, 1)]
    public void ChargeableWeight_IsTheBiggerWeightRoundedUp(double actual, double volumetric, double step, double expected)
    {
        Assert.Equal((decimal)expected, ShipmentWeightCalculator.ChargeableWeight((decimal)actual, (decimal)volumetric, (decimal)step));
    }

    [Fact]
    public void TrackingNumber_HasPrefixTenRandomDigitsAndAValidCheckDigit()
    {
        var number = TrackingNumberGenerator.Create("SE");

        Assert.Equal(13, number.Length);
        Assert.StartsWith("SE", number);
        Assert.True(number[2..].All(char.IsAsciiDigit));
        Assert.True(TrackingNumberGenerator.HasValidCheckDigit(number));

        // Changing one digit breaks the check digit.
        var digit = number[5] == '9' ? '0' : (char)(number[5] + 1);
        var mistyped = number[..5] + digit + number[6..];
        Assert.False(TrackingNumberGenerator.HasValidCheckDigit(mistyped));
    }

    [Fact]
    public void DocumentNumber_HasPrefixYearAndSixDigits()
    {
        Assert.Equal("SHP-2026-000001", DocumentNumberGenerator.Format("SHP", 2026, 1));
        Assert.Equal("BTR-2027-123456", DocumentNumberGenerator.Format("BTR", 2027, 123456));
    }
}
