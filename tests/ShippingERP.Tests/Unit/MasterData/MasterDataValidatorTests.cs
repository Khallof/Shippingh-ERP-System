using FluentValidation.Results;
using ShippingERP.Application.DTOs.Customers;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Validators.Customers;
using ShippingERP.Application.Validators.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Tests.Unit.MasterData;

/// <summary>Input rules that are checked before any database access.</summary>
public sealed class MasterDataValidatorTests
{
    private static bool HasError(ValidationResult result, string property) =>
        result.Errors.Any(error => string.Equals(error.PropertyName, property, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Customer_InvalidPhone_NegativeCreditLimit_AndInvalidRole_AreRejected()
    {
        var result = new SaveCustomerRequestValidator().Validate(new SaveCustomerRequest
        {
            ArabicName = "عميل",
            Phone = "abc",
            CreditLimit = -1m,
            Roles = (CustomerRole)0,
            DefaultBranchId = 1
        });

        Assert.True(HasError(result, "phone"));
        Assert.True(HasError(result, "creditLimit"));
        Assert.True(HasError(result, "roles"));
    }

    [Fact]
    public void Customer_ValidRequest_Passes()
    {
        var result = new SaveCustomerRequestValidator().Validate(new SaveCustomerRequest
        {
            ArabicName = "أحمد",
            Phone = "+963944000101",
            Email = "ahmad@example.com",
            CreditLimit = 100000m,
            Roles = CustomerRole.Both,
            DefaultBranchId = 1
        });

        Assert.True(result.IsValid, string.Join(" | ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void PricingRule_WeightConditionMaxMustBeAboveMin()
    {
        var request = new SavePricingRuleRequest { ArabicName = "قاعدة", BasePrice = 1000m };
        request.Conditions.Add(new SavePriceRuleConditionRequest { ConditionType = PriceConditionType.ChargeableWeight, MinValue = 10m, MaxValue = 5m });

        var result = new SavePricingRuleRequestValidator().Validate(request);

        Assert.True(HasError(result, "conditions[0].maxValue"));
    }

    [Fact]
    public void PricingRule_CodPercentageAbove100_IsRejected()
    {
        var result = new SavePricingRuleRequestValidator().Validate(new SavePricingRuleRequest
        {
            ArabicName = "قاعدة",
            BasePrice = 1000m,
            CodFeePercentage = 120m
        });

        Assert.True(HasError(result, "codFeePercentage"));
    }

    [Fact]
    public void PricingRule_WeightTiersWithAGap_OrAnOpenTierInTheMiddle_AreRejected()
    {
        var gap = new SavePricingRuleRequest { ArabicName = "قاعدة", IncludedWeightKg = 5m };
        gap.WeightTiers.Add(new SavePriceRuleWeightTierRequest { FromWeightKg = 5m, ToWeightKg = 10m, PricePerKg = 1000m });
        gap.WeightTiers.Add(new SavePriceRuleWeightTierRequest { FromWeightKg = 12m, ToWeightKg = null, PricePerKg = 800m });

        var openMiddle = new SavePricingRuleRequest { ArabicName = "قاعدة", IncludedWeightKg = 0m };
        openMiddle.WeightTiers.Add(new SavePriceRuleWeightTierRequest { FromWeightKg = 0m, ToWeightKg = null, PricePerKg = 1000m });
        openMiddle.WeightTiers.Add(new SavePriceRuleWeightTierRequest { FromWeightKg = 10m, ToWeightKg = null, PricePerKg = 800m });

        var valid = new SavePricingRuleRequest { ArabicName = "قاعدة", IncludedWeightKg = 5m };
        valid.WeightTiers.Add(new SavePriceRuleWeightTierRequest { FromWeightKg = 5m, ToWeightKg = 20m, PricePerKg = 2000m });
        valid.WeightTiers.Add(new SavePriceRuleWeightTierRequest { FromWeightKg = 20m, ToWeightKg = null, PricePerKg = 1500m });

        var validator = new SavePricingRuleRequestValidator();
        Assert.True(HasError(validator.Validate(gap), "weightTiers"));
        Assert.True(HasError(validator.Validate(openMiddle), "weightTiers"));
        Assert.True(validator.Validate(valid).IsValid);
    }

    [Fact]
    public void PricingRule_ReferenceConditionWithoutId_AndRangeConditionWithId_AreRejected()
    {
        var request = new SavePricingRuleRequest { ArabicName = "قاعدة" };
        request.Conditions.Add(new SavePriceRuleConditionRequest { ConditionType = PriceConditionType.DestinationZone });
        request.Conditions.Add(new SavePriceRuleConditionRequest { ConditionType = PriceConditionType.PackageCount, ReferenceId = 3, MinValue = 2m });

        var result = new SavePricingRuleRequestValidator().Validate(request);

        Assert.True(HasError(result, "conditions[0].referenceId"));
        Assert.True(HasError(result, "conditions[1].referenceId"));
    }

    [Fact]
    public void ExchangeRate_SameCurrency_AndZeroRate_AreRejected()
    {
        var result = new SaveExchangeRateRequestValidator().Validate(new SaveExchangeRateRequest
        {
            FromCurrencyId = 1,
            ToCurrencyId = 1,
            Rate = 0m,
            EffectiveFromUtc = DateTime.UtcNow
        });

        Assert.True(HasError(result, "toCurrencyId"));
        Assert.True(HasError(result, "rate"));
    }

    [Fact]
    public void Address_LatitudeWithoutLongitude_IsRejected()
    {
        var result = new SaveCustomerAddressRequestValidator().Validate(new SaveCustomerAddressRequest
        {
            Label = "المنزل",
            CityId = 1,
            AddressLine = "شارع",
            Latitude = 33.5m
        });

        Assert.True(HasError(result, "longitude"));
    }

    [Theory]
    [InlineData("SYR1")]
    [InlineData("S")]
    [InlineData("س")]
    public void Country_InvalidIsoCode_IsRejected(string code)
    {
        var result = new SaveCountryRequestValidator().Validate(new SaveCountryRequest { Code = code, ArabicName = "دولة" });

        Assert.True(HasError(result, "code"));
    }

    [Fact]
    public void Code_WithSpacesOrArabicLetters_IsRejected()
    {
        var result = new SaveBranchRequestValidator().Validate(new SaveBranchRequest { Code = "فرع 1", ArabicName = "فرع", CityId = 1 });

        Assert.True(HasError(result, "code"));
    }
}
