using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Identity;

namespace ShippingERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// DEMO price rules of the step-5 pricing engine (fictional prices in SYP):
///
///   Standard  : 15,000 for the first 5 kg, then 2,000 / kg
///   Express   : 30,000 + 3,000 / kg from the first kg, 1,000 per package after the first
///   Same day  : 45,000 + 4,000 / kg, only up to 10 kg (weight condition)
///   All rules : COD commission 2,000 + 1 % of the COD amount, insurance 1 % (min 1,000), pickup 3,000,
///               packaging 2,500, remote area 5,000, return fee 7,500
///
/// It also marks the Latakia zones as remote areas and creates a customer contract "CNT-MED" for the pharmacy
/// company (CUS-00010): standard shipments with a 10 % contract discount and a cheaper COD commission.
///
/// Runs only when Seed:DemoData = true, the demo pricing plan "GENERAL" exists and there is NO price rule yet.
/// That also covers databases created with step 3 / 4: the migration of step 5 replaces the old rule table, and
/// this seeder puts equivalent demo rules back.
/// </summary>
public sealed class DemoPricingSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly SeedOptions _options;
    private readonly ILogger<DemoPricingSeeder> _logger;

    public DemoPricingSeeder(AppDbContext dbContext, IOptions<SeedOptions> options, ILogger<DemoPricingSeeder> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    /// <param name="force">Ignore Seed:DemoData (used by tests).</param>
    /// <returns>The number of rules created.</returns>
    public async Task<int> SeedAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && (!_options.Enabled || !_options.DemoData))
        {
            return 0;
        }

        if (_dbContext.Database.IsRelational() &&
            (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            _logger.LogWarning("Demo price rules skipped: there are pending migrations");
            return 0;
        }

        if (await _dbContext.PriceRules.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Demo price rules skipped: price rules already exist");
            return 0;
        }

        var plan = await _dbContext.CustomerPricingPlans.FirstOrDefaultAsync(x => x.Code == "GENERAL", cancellationToken);
        var services = await _dbContext.ServiceTypes
            .Where(x => x.Code == "STD" || x.Code == "EXP" || x.Code == "SDD")
            .ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);

        if (plan is null || services.Count != 3)
        {
            _logger.LogDebug("Demo price rules skipped: the demo master data of step 3 was not found");
            return 0;
        }

        var count = await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            var rules = new List<PriceRule>
            {
                Rule(plan.Id, "عادي - أول 5 كغ مشمولة", services["STD"], basePrice: 15000m, includedKg: 5m, pricePerKg: 2000m),
                Rule(plan.Id, "سريع", services["EXP"], basePrice: 30000m, includedKg: 0m, pricePerKg: 3000m, perPackageFee: 1000m),
                Rule(plan.Id, "نفس اليوم - حتى 10 كغ", services["SDD"], basePrice: 45000m, includedKg: 0m, pricePerKg: 4000m, maxWeightKg: 10m)
            };
            _dbContext.PriceRules.AddRange(rules);

            // Remote areas (only when the user did not mark any zone yet).
            if (!await _dbContext.Zones.AnyAsync(x => x.IsRemoteArea, ct))
            {
                var remote = await _dbContext.Zones.Where(x => x.Code == "ZIRAA" || x.Code == "SHEIKHDAHER").ToListAsync(ct);
                foreach (var zone in remote)
                {
                    zone.IsRemoteArea = true;
                }
            }

            // A customer contract (only when the demo customer exists and has no contract yet).
            var pharmacy = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Code == "CUS-00010", ct);
            if (pharmacy is not null && !await _dbContext.CustomerPricingPlans.AnyAsync(x => x.ContractCustomerId == pharmacy.Id, ct))
            {
                var contract = new CustomerPricingPlan
                {
                    Code = "CNT-MED",
                    ArabicName = "عقد شركة المتوسط للصيدليات",
                    EnglishName = "Al-Mutawasset pharmacies contract",
                    CurrencyId = plan.CurrencyId,
                    ContractCustomerId = pharmacy.Id,
                    ContractReference = "CTR-2026-001",
                    ValidFromUtc = plan.ValidFromUtc,
                    Notes = "عقد تجريبي: خصم 10% على التوصيل العادي وعمولة تحصيل مخفضة",
                    IsActive = true
                };
                _dbContext.CustomerPricingPlans.Add(contract);
                await _dbContext.SaveChangesAsync(ct);

                var contractRule = Rule(contract.Id, "عقد - توصيل عادي", services["STD"], basePrice: 15000m, includedKg: 5m, pricePerKg: 2000m);
                contractRule.DiscountPercentage = 10m;
                contractRule.CodFeeFixed = 1500m;
                contractRule.CodFeePercentage = 0.75m;
                contractRule.Priority = 20;
                _dbContext.PriceRules.Add(contractRule);
                rules.Add(contractRule);
            }

            await _dbContext.SaveChangesAsync(ct);
            return rules.Count;
        }, cancellationToken);

        _logger.LogInformation("Demo price rules created ({Count})", count);
        return count;
    }

    private static PriceRule Rule(
        long planId,
        string name,
        long serviceTypeId,
        decimal basePrice,
        decimal includedKg,
        decimal pricePerKg,
        decimal perPackageFee = 0m,
        decimal? maxWeightKg = null)
    {
        var rule = new PriceRule
        {
            PricingPlanId = planId,
            ArabicName = name,
            Priority = 10,
            BasePrice = basePrice,
            IncludedWeightKg = includedKg,
            IncludedPackageCount = 1,
            PerPackageFee = perPackageFee,
            CodFeeFixed = 2000m,
            CodFeePercentage = 1m,
            InsuranceFeePercentage = 1m,
            InsuranceFeeMinimum = 1000m,
            PickupFee = 3000m,
            PackagingFee = 2500m,
            RemoteAreaFee = 5000m,
            ReturnFee = 7500m,
            IsActive = true
        };

        rule.Conditions.Add(new PriceRuleCondition { ConditionType = PriceConditionType.ServiceType, ReferenceId = serviceTypeId });
        if (maxWeightKg.HasValue)
        {
            rule.Conditions.Add(new PriceRuleCondition { ConditionType = PriceConditionType.ChargeableWeight, MinValue = 0m, MaxValue = maxWeightKg });
        }

        rule.WeightTiers.Add(new PriceRuleWeightTier { FromWeightKg = includedKg, ToWeightKg = null, PricePerKg = pricePerKg });
        return rule;
    }
}
