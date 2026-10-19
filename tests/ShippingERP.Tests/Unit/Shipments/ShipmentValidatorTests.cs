using FluentValidation.Results;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Validators.Shipments;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Tests.Unit.Shipments;

/// <summary>Input rules of the shipment requests (checked before any database access).</summary>
public sealed class ShipmentValidatorTests
{
    private static bool HasError(ValidationResult result, string property) =>
        result.Errors.Any(error => error.PropertyName.EndsWith(property, StringComparison.OrdinalIgnoreCase));

    private static SaveShipmentRequest Valid() => new()
    {
        SenderCustomerId = 1,
        RecipientCustomerId = 2,
        OriginBranchId = 1,
        ServiceTypeId = 1,
        PackageDescription = "ملابس",
        Packages = new List<ShipmentPackageRequest> { new() { WeightKg = 1.5m } }
    };

    [Fact]
    public void ValidShipment_Passes()
    {
        var result = new SaveShipmentRequestValidator().Validate(Valid());

        Assert.True(result.IsValid, string.Join(" | ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Shipment_WithoutPackages_IsRejected()
    {
        var request = Valid();
        request.Packages.Clear();

        Assert.True(HasError(new SaveShipmentRequestValidator().Validate(request), "packages"));
    }

    [Fact]
    public void Cod_AmountMustMatchTheCodFlag()
    {
        var withoutAmount = Valid();
        withoutAmount.IsCOD = true;

        var amountWithoutFlag = Valid();
        amountWithoutFlag.CODAmount = 100m;

        Assert.True(HasError(new SaveShipmentRequestValidator().Validate(withoutAmount), "codAmount"));
        Assert.True(HasError(new SaveShipmentRequestValidator().Validate(amountWithoutFlag), "codAmount"));
    }

    [Fact]
    public void Insurance_CannotExceedTheDeclaredValue_AndMoneyHasTwoDecimals()
    {
        var request = Valid();
        request.DeclaredValue = 100m;
        request.InsuranceValue = 150m;
        request.DiscountAmount = 1.555m;

        var result = new SaveShipmentRequestValidator().Validate(request);

        Assert.True(HasError(result, "insuranceValue"));
        Assert.True(HasError(result, "discountAmount"));
    }

    [Fact]
    public void Package_DimensionsMustBeAllOrNothing_AndWeightPositive()
    {
        var request = Valid();
        request.Packages[0].LengthCm = 30m;
        request.Packages.Add(new ShipmentPackageRequest { WeightKg = 0m });

        var result = new SaveShipmentRequestValidator().Validate(request);

        Assert.True(HasError(result, "dimensions"));
        Assert.True(HasError(result, "weightKg"));
    }

    [Theory]
    [InlineData(ShipmentStatus.PickedUp, true)]
    [InlineData(ShipmentStatus.ReceivedAtBranch, true)]
    [InlineData(ShipmentStatus.Delivered, false)]
    [InlineData(ShipmentStatus.InTransit, false)]
    public void ManualStatusChange_AllowsOnlyScanStatuses(ShipmentStatus status, bool valid)
    {
        var result = new ChangeShipmentStatusRequestValidator().Validate(new ChangeShipmentStatusRequest { Status = status });

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void ManualStatusChange_ToInWarehouse_RequiresTheWarehouse()
    {
        var result = new ChangeShipmentStatusRequestValidator().Validate(new ChangeShipmentStatusRequest { Status = ShipmentStatus.InWarehouse });

        Assert.True(HasError(result, "warehouseId"));
    }

    [Fact]
    public void Correction_NeedsAClearReason_AndCannotGoBackToCreated()
    {
        var result = new CorrectShipmentStatusRequestValidator().Validate(new CorrectShipmentStatusRequest { Status = ShipmentStatus.Created, Reason = "خطأ" });

        Assert.True(HasError(result, "status"));
        Assert.True(HasError(result, "reason"));
    }

    [Fact]
    public void Codes_AreLimitedAndMustNotBeEmpty()
    {
        var tooMany = new ShipmentCodesRequest { Codes = Enumerable.Range(1, 201).Select(i => $"SE{i}").ToList() };
        var empty = new ShipmentCodesRequest { Codes = new List<string> { "SE1", " " } };

        Assert.True(HasError(new ShipmentCodesRequestValidator().Validate(tooMany), "codes"));
        Assert.True(HasError(new ShipmentCodesRequestValidator().Validate(empty), "codes"));
        Assert.True(new ShipmentCodesRequestValidator().Validate(new ShipmentCodesRequest { Codes = new List<string> { "SE1" } }).IsValid);
    }

    [Fact]
    public void Settings_PrefixAndTrackingUrlAreChecked()
    {
        var result = new SaveShippingSettingsRequestValidator().Validate(new SaveShippingSettingsRequest
        {
            TrackingNumberPrefix = "se1",
            TrackingUrlTemplate = "javascript:alert(1)",
            VolumetricDivisor = 100
        });

        Assert.True(HasError(result, "trackingNumberPrefix"));
        Assert.True(HasError(result, "trackingUrlTemplate"));
        Assert.True(HasError(result, "volumetricDivisor"));

        var valid = new SaveShippingSettingsRequestValidator().Validate(new SaveShippingSettingsRequest
        {
            TrackingUrlTemplate = "https://track.example.com/{trackingNumber}"
        });
        Assert.True(valid.IsValid);
    }

    [Fact]
    public void Transfer_ToTheSameBranch_IsRejected()
    {
        var result = new CreateBranchTransferRequestValidator().Validate(new CreateBranchTransferRequest { FromBranchId = 1, ToBranchId = 1 });

        Assert.True(HasError(result, "toBranchId"));
    }

    [Fact]
    public void Attachment_SignatureType_IsReservedForTheProofOfDelivery()
    {
        var result = new UploadShipmentAttachmentRequestValidator().Validate(new UploadShipmentAttachmentRequest { AttachmentType = ShipmentAttachmentType.Signature });

        Assert.True(HasError(result, "attachmentType"));
    }
}
