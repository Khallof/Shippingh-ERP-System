using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Customers;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.MasterData;

/// <summary>Currencies, exchange rates, tax rates, pricing plans and pricing rules.</summary>
public sealed class FinanceAndPricingTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<CurrencyDto> CreateCurrencyAsync(string code, bool isBase = false) =>
        _context.UseAsync<ICurrencyService, CurrencyDto>(service => service.CreateAsync(
            new SaveCurrencyRequest { Code = code, ArabicName = code, Symbol = code, DecimalPlaces = 2, IsBaseCurrency = isBase },
            CancellationToken.None));

    [Fact]
    public async Task FirstCurrency_BecomesBase_AndMarkingAnotherAsBaseMovesTheFlag()
    {
        var syp = await CreateCurrencyAsync("SYP");
        var usd = await CreateCurrencyAsync("USD");

        Assert.True(syp.IsBaseCurrency);
        Assert.False(usd.IsBaseCurrency);

        await _context.UseAsync<ICurrencyService, CurrencyDto>(service => service.UpdateAsync(
            usd.Id,
            new SaveCurrencyRequest { Code = "USD", ArabicName = "دولار", Symbol = "$", DecimalPlaces = 2, IsBaseCurrency = true },
            CancellationToken.None));

        var baseCodes = await _context.QueryAsync(db => db.Currencies.Where(c => c.IsBaseCurrency).Select(c => c.Code).ToListAsync());
        Assert.Equal(new[] { "USD" }, baseCodes);
    }

    [Fact]
    public async Task BaseCurrency_CannotBeDeactivatedOrDeleted()
    {
        var syp = await CreateCurrencyAsync("SYP");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.UseAsync<ICurrencyService, CurrencyDto>(service => service.SetActiveAsync(syp.Id, false, CancellationToken.None)));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.UseAsync<ICurrencyService>(service => service.DeleteAsync(syp.Id, CancellationToken.None)));
    }

    [Fact]
    public async Task EffectiveRate_IsTheLatestRateNotAfterTheRequestedDate()
    {
        var syp = await CreateCurrencyAsync("SYP");
        var usd = await CreateCurrencyAsync("USD");
        var january = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var june = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        foreach (var (date, rate) in new[] { (january, 12000m), (june, 13000m) })
        {
            await _context.UseAsync<IExchangeRateService, ExchangeRateDto>(service => service.CreateAsync(
                new SaveExchangeRateRequest { FromCurrencyId = usd.Id, ToCurrencyId = syp.Id, Rate = rate, EffectiveFromUtc = date },
                CancellationToken.None));
        }

        var march = await _context.UseAsync<IExchangeRateService, ExchangeRateDto>(service =>
            service.GetEffectiveRateAsync("usd", "syp", new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None));
        var july = await _context.UseAsync<IExchangeRateService, ExchangeRateDto>(service =>
            service.GetEffectiveRateAsync("USD", "SYP", new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None));

        Assert.Equal(12000m, march.Rate);
        Assert.Equal(13000m, july.Rate);
        Assert.Equal("USD", july.FromCurrencyCode);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _context.UseAsync<IExchangeRateService, ExchangeRateDto>(service =>
                service.GetEffectiveRateAsync("USD", "SYP", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None)));
    }

    [Fact]
    public async Task DuplicateExchangeRate_ForTheSamePairAndDate_IsRejected()
    {
        var syp = await CreateCurrencyAsync("SYP");
        var usd = await CreateCurrencyAsync("USD");
        var request = new SaveExchangeRateRequest
        {
            FromCurrencyId = usd.Id,
            ToCurrencyId = syp.Id,
            Rate = 13000m,
            EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        await _context.UseAsync<IExchangeRateService, ExchangeRateDto>(service => service.CreateAsync(request, CancellationToken.None));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _context.UseAsync<IExchangeRateService, ExchangeRateDto>(service => service.CreateAsync(request, CancellationToken.None)));
    }

    [Fact]
    public async Task OnlyOneTaxRate_IsTheDefault()
    {
        await _context.UseAsync<ITaxRateService, TaxRateDto>(service => service.CreateAsync(
            new SaveTaxRateRequest { Code = "EXEMPT", ArabicName = "معفى", Percentage = 0m, IsDefault = true }, CancellationToken.None));
        await _context.UseAsync<ITaxRateService, TaxRateDto>(service => service.CreateAsync(
            new SaveTaxRateRequest { Code = "VAT5", ArabicName = "ضريبة 5%", Percentage = 5m, IsDefault = true }, CancellationToken.None));

        var defaults = await _context.QueryAsync(db => db.TaxRates.Where(t => t.IsDefault).Select(t => t.Code).ToListAsync());
        Assert.Equal(new[] { "VAT5" }, defaults);
    }

    [Fact]
    public async Task PricingRules_WithTheSameConditionsAndPriority_CannotOverlap_ButTouchingRangesAreAllowed()
    {
        await _context.SeedDemoDataAsync();
        var (planId, expressId) = await _context.QueryAsync(async db => (
            await db.CustomerPricingPlans.Where(p => p.Code == "GENERAL").Select(p => p.Id).SingleAsync(),
            await db.ServiceTypes.Where(s => s.Code == "EXP").Select(s => s.Id).SingleAsync()));

        SavePricingRuleRequest Rule(int priority, decimal? min, decimal? max, long? originZoneId = null)
        {
            var request = new SavePricingRuleRequest { ArabicName = "قاعدة اختبار", Priority = priority, BasePrice = 1000m };
            request.Conditions.Add(new SavePriceRuleConditionRequest { ConditionType = PriceConditionType.ServiceType, ReferenceId = expressId });
            if (originZoneId.HasValue)
            {
                request.Conditions.Add(new SavePriceRuleConditionRequest { ConditionType = PriceConditionType.OriginZone, ReferenceId = originZoneId });
            }

            if (min.HasValue || max.HasValue)
            {
                request.Conditions.Add(new SavePriceRuleConditionRequest { ConditionType = PriceConditionType.ChargeableWeight, MinValue = min, MaxValue = max });
            }

            return request;
        }

        Task<PricingRuleDto> Create(SavePricingRuleRequest request) =>
            _context.UseAsync<IPricingRuleService, PricingRuleDto>(service => service.CreateAsync(planId, request, CancellationToken.None));

        // The demo plan already has "Express, any weight" with priority 10: 20-30 kg with the same priority overlaps.
        await Assert.ThrowsAsync<ConflictException>(() => Create(Rule(10, 20m, 30m)));

        // Another priority is not ambiguous (the higher one wins).
        var higher = await Create(Rule(20, 20m, 30m));
        Assert.Equal(20, higher.Priority);

        // A new condition combination (specific origin zone) starts a separate set of ranges.
        var zoneId = await _context.QueryAsync(db => db.Zones.Where(z => z.Code == "MAZZEH").Select(z => z.Id).SingleAsync());
        var first = await Create(Rule(30, 0m, 5m, zoneId));
        var touching = await Create(Rule(30, 5m, 10m, zoneId));

        Assert.Contains(first.Conditions, c => c.ConditionType == PriceConditionType.OriginZone && c.ReferenceName == "المزة");
        Assert.Equal(5m, touching.Conditions.Single(c => c.ConditionType == PriceConditionType.ChargeableWeight).MinValue);

        await Assert.ThrowsAsync<ConflictException>(() => Create(Rule(30, 4m, 6m, zoneId)));
    }

    [Fact]
    public async Task PricingRule_WithAnUnknownZone_IsRejected()
    {
        await _context.SeedDemoDataAsync();
        var planId = await _context.QueryAsync(db => db.CustomerPricingPlans.Where(p => p.Code == "GENERAL").Select(p => p.Id).SingleAsync());

        var request = new SavePricingRuleRequest { ArabicName = "منطقة خاطئة", BasePrice = 1000m };
        request.Conditions.Add(new SavePriceRuleConditionRequest { ConditionType = PriceConditionType.DestinationZone, ReferenceId = 999_999 });

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _context.UseAsync<IPricingRuleService, PricingRuleDto>(service => service.CreateAsync(planId, request, CancellationToken.None)));

        Assert.True(exception.Errors.ContainsKey("conditions[0].referenceId"));
    }

    [Fact]
    public async Task DefaultPricingPlan_CannotBeDeleted()
    {
        await _context.SeedDemoDataAsync();
        var planId = await _context.QueryAsync(db => db.CustomerPricingPlans.Where(p => p.IsDefault).Select(p => p.Id).SingleAsync());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.UseAsync<IPricingPlanService>(service => service.DeleteAsync(planId, CancellationToken.None)));
    }

    [Fact]
    public async Task PricingPlan_ShowsRuleAndCustomerCounts()
    {
        await _context.SeedDemoDataAsync();
        var planId = await _context.QueryAsync(db => db.CustomerPricingPlans.Where(p => p.IsDefault).Select(p => p.Id).SingleAsync());

        var plan = await _context.UseAsync<IPricingPlanService, PricingPlanDto>(service => service.GetByIdAsync(planId, CancellationToken.None));

        // Step 5 demo rules: standard, express and same day (the 4th demo rule belongs to the contract CNT-MED).
        Assert.Equal(3, plan.RuleCount);
        Assert.Equal(0, plan.CustomerCount);
        Assert.Equal("SYP", plan.CurrencyCode);
    }
}
