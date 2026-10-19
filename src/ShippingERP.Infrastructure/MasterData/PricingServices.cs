using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Customers;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.MasterData;

/// <summary>
/// Pricing plans (price lists) and customer contracts. Only one plan can be the default; a contract plan
/// (ContractCustomerId set) can never be the default, and two active contracts of the same customer may not
/// overlap in time.
/// </summary>
public sealed class PricingPlanService
    : CrudServiceBase<CustomerPricingPlan, PricingPlanDto, SavePricingPlanRequest, PricingPlanListQuery>, IPricingPlanService
{
    private static readonly Dictionary<string, Expression<Func<CustomerPricingPlan, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = x => x.Code,
        ["arabicName"] = x => x.ArabicName,
        ["createdAtUtc"] = x => x.CreatedAtUtc
    };

    public PricingPlanService(MasterDataServiceDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override string EntityName => "CustomerPricingPlan";

    protected override string DisplayName => "خطة التسعير";

    protected override IReadOnlyDictionary<string, Expression<Func<CustomerPricingPlan, object?>>> SortFields => Sorts;

    protected override Expression<Func<CustomerPricingPlan, object?>> DefaultSort => x => x.Code;

    protected override Expression<Func<CustomerPricingPlan, PricingPlanDto>> ToDto => x => new PricingPlanDto
    {
        Id = x.Id,
        Code = x.Code,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        IsDefault = x.IsDefault,
        ContractCustomerId = x.ContractCustomerId,
        ContractCustomerName = x.ContractCustomer != null ? x.ContractCustomer.ArabicName : null,
        ContractReference = x.ContractReference,
        ValidFromUtc = x.ValidFromUtc,
        ValidToUtc = x.ValidToUtc,
        Notes = x.Notes,
        RuleCount = x.Rules.Count(),
        CustomerCount = Db.Customers.Count(customer => customer.PricingPlanId == x.Id),
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        UpdatedAtUtc = x.UpdatedAtUtc,
        UpdatedByUserId = x.UpdatedByUserId
    };

    protected override IQueryable<CustomerPricingPlan> ApplyFilters(IQueryable<CustomerPricingPlan> query, PricingPlanListQuery filter)
    {
        if (filter.CurrencyId.HasValue)
        {
            var currencyId = filter.CurrencyId.Value;
            query = query.Where(x => x.CurrencyId == currencyId);
        }

        if (filter.IsContract.HasValue)
        {
            query = filter.IsContract.Value
                ? query.Where(x => x.ContractCustomerId != null)
                : query.Where(x => x.ContractCustomerId == null);
        }

        if (filter.ContractCustomerId.HasValue)
        {
            var customerId = filter.ContractCustomerId.Value;
            query = query.Where(x => x.ContractCustomerId == customerId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                x.Code.Contains(search) ||
                x.ArabicName.Contains(search) ||
                (x.EnglishName != null && x.EnglishName.Contains(search)) ||
                (x.ContractReference != null && x.ContractReference.Contains(search)));
        }

        return query;
    }

    protected override async Task ValidateAsync(SavePricingPlanRequest request, CustomerPricingPlan? existing, CancellationToken cancellationToken)
    {
        var code = NormalizeCode(request.Code);
        await EnsureUniqueAsync(x => x.Code == code, existing, $"رمز خطة التسعير '{code}' مستخدم من قبل.", cancellationToken);
        await EnsureReferenceAsync<Currency>(request.CurrencyId, existing?.CurrencyId, "currencyId", "العملة", cancellationToken);
        await EnsureReferenceAsync<Customer>(request.ContractCustomerId, existing?.ContractCustomerId, "contractCustomerId", "العميل", cancellationToken);

        if (existing is { IsDefault: true } && !request.IsDefault)
        {
            throw new BusinessRuleException("يجب أن تبقى خطة تسعير افتراضية. اجعل خطة أخرى افتراضية بدلاً من إلغاء هذه.");
        }

        if (existing is not null && existing.CurrencyId != request.CurrencyId && await Db.PriceRules.AnyAsync(x => x.PricingPlanId == existing.Id, cancellationToken))
        {
            throw Invalid("currencyId", "لا يمكن تغيير عملة خطة تحتوي على قواعد أسعار.");
        }

        if (request.ContractCustomerId.HasValue)
        {
            await EnsureNoOverlappingContractAsync(request, existing?.Id ?? 0, cancellationToken);
        }
    }

    /// <summary>Two active contracts of the same customer may not be valid at the same moment.</summary>
    private async Task EnsureNoOverlappingContractAsync(SavePricingPlanRequest request, long existingId, CancellationToken cancellationToken)
    {
        var customerId = request.ContractCustomerId!.Value;
        var others = await Db.CustomerPricingPlans
            .AsNoTracking()
            .Where(x => x.ContractCustomerId == customerId && x.IsActive && x.Id != existingId)
            .Select(x => new { x.Code, x.ValidFromUtc, x.ValidToUtc })
            .ToListAsync(cancellationToken);

        var newFrom = request.ValidFromUtc ?? DateTime.MinValue;
        var newTo = request.ValidToUtc ?? DateTime.MaxValue;

        var overlapping = others.FirstOrDefault(x => (x.ValidFromUtc ?? DateTime.MinValue) < newTo && newFrom < (x.ValidToUtc ?? DateTime.MaxValue));
        if (overlapping is not null)
        {
            throw new ConflictException($"للعميل عقد تسعير آخر فعّال في نفس الفترة ({overlapping.Code}). عدّل تواريخ العقدين أو أوقف أحدهما.");
        }
    }

    protected override void Apply(CustomerPricingPlan entity, SavePricingPlanRequest request)
    {
        entity.Code = NormalizeCode(request.Code);
        entity.ArabicName = request.ArabicName.Trim();
        entity.EnglishName = Optional(request.EnglishName);
        entity.CurrencyId = request.CurrencyId;
        entity.IsDefault = request.IsDefault && request.ContractCustomerId is null;
        entity.ContractCustomerId = request.ContractCustomerId;
        entity.ContractReference = request.ContractCustomerId.HasValue ? Optional(request.ContractReference) : null;
        entity.ValidFromUtc = request.ValidFromUtc;
        entity.ValidToUtc = request.ValidToUtc;
        entity.Notes = Optional(request.Notes);
    }

    protected override async Task BeforeSaveAsync(CustomerPricingPlan entity, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew && !entity.IsDefault && entity.ContractCustomerId is null && !await Db.CustomerPricingPlans.AnyAsync(x => x.IsDefault, cancellationToken))
        {
            // The first general plan is the default plan.
            entity.IsDefault = true;
        }

        if (!entity.IsDefault)
        {
            return;
        }

        entity.IsActive = true;

        var others = await Db.CustomerPricingPlans.Where(x => x.IsDefault && x.Id != entity.Id).ToListAsync(cancellationToken);
        foreach (var other in others)
        {
            other.IsDefault = false;
        }

        if (others.Count > 0)
        {
            // Clear the old default first so the "single default plan" unique index is never violated.
            entity.IsDefault = false;
            await Db.SaveChangesAsync(cancellationToken);
            entity.IsDefault = true;
        }
    }

    protected override Task EnsureCanDeactivateAsync(CustomerPricingPlan entity, CancellationToken cancellationToken)
    {
        if (entity.IsDefault)
        {
            throw new BusinessRuleException("لا يمكن إيقاف خطة التسعير الافتراضية.");
        }

        return Task.CompletedTask;
    }

    protected override async Task EnsureCanDeleteAsync(CustomerPricingPlan entity, CancellationToken cancellationToken)
    {
        if (entity.IsDefault)
        {
            throw new BusinessRuleException("لا يمكن حذف خطة التسعير الافتراضية.");
        }

        var id = entity.Id;
        if (await Db.Customers.AnyAsync(x => x.PricingPlanId == id, cancellationToken))
        {
            throw InUse("عملاء");
        }

        if (await Db.PriceRules.AnyAsync(x => x.PricingPlanId == id, cancellationToken))
        {
            throw InUse("قواعد تسعير (احذف القواعد أولاً)");
        }
    }
}

/// <summary>
/// Price rules of one plan, with their conditions and weight tiers (step 5).
///
/// Ambiguity guard: two active rules of the same plan with the same priority and the same conditions (apart from the
/// weight range) may not have overlapping weight ranges; otherwise the engine could not tell which one to use
/// (it would silently take the older one).
/// </summary>
public sealed class PricingRuleService : IPricingRuleService
{
    private const string EntityName = "PriceRule";

    private readonly MasterDataServiceDependencies _dependencies;

    public PricingRuleService(MasterDataServiceDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    private AppDbContext Db => _dependencies.DbContext;

    public async Task<IReadOnlyList<PricingRuleDto>> GetAllAsync(long planId, CancellationToken cancellationToken)
    {
        await EnsurePlanExistsAsync(planId, cancellationToken);

        var rules = await Db.PriceRules
            .AsNoTracking()
            .Include(x => x.Conditions)
            .Include(x => x.WeightTiers)
            .Include(x => x.TaxRate)
            .Where(x => x.PricingPlanId == planId)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(rules, cancellationToken);
    }

    public async Task<PricingRuleDto> GetByIdAsync(long planId, long ruleId, CancellationToken cancellationToken)
    {
        var rule = await Db.PriceRules
                       .AsNoTracking()
                       .Include(x => x.Conditions)
                       .Include(x => x.WeightTiers)
                       .Include(x => x.TaxRate)
                       .FirstOrDefaultAsync(x => x.PricingPlanId == planId && x.Id == ruleId, cancellationToken)
                   ?? throw new NotFoundException("قاعدة التسعير", ruleId);

        return (await ToDtosAsync(new[] { rule }, cancellationToken))[0];
    }

    public async Task<PricingRuleDto> CreateAsync(long planId, SavePricingRuleRequest request, CancellationToken cancellationToken)
    {
        await EnsurePlanExistsAsync(planId, cancellationToken);
        await ValidateAsync(planId, request, null, cancellationToken);

        var rule = new PriceRule { PricingPlanId = planId, IsActive = true };
        Apply(rule, request);

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            Db.PriceRules.Add(rule);
            await Db.SaveChangesAsync(ct);

            _dependencies.AuditLog.Add(
                AuditAction.MasterDataCreated,
                entityName: EntityName,
                entityId: rule.Id.ToString(CultureInfo.InvariantCulture),
                details: new { planId, request });
            await Db.SaveChangesAsync(ct);

            return rule.Id;
        }, cancellationToken);

        return await GetByIdAsync(planId, id, cancellationToken);
    }

    public async Task<PricingRuleDto> UpdateAsync(long planId, long ruleId, SavePricingRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await Db.PriceRules
                       .Include(x => x.Conditions)
                       .Include(x => x.WeightTiers)
                       .FirstOrDefaultAsync(x => x.PricingPlanId == planId && x.Id == ruleId, cancellationToken)
                   ?? throw new NotFoundException("قاعدة التسعير", ruleId);

        await ValidateAsync(planId, request, rule, cancellationToken);

        // Conditions and tiers are replaced as a whole (shipments keep what was applied in their snapshots).
        Db.PriceRuleConditions.RemoveRange(rule.Conditions);
        Db.PriceRuleWeightTiers.RemoveRange(rule.WeightTiers);
        rule.Conditions.Clear();
        rule.WeightTiers.Clear();
        Apply(rule, request);

        _dependencies.AuditLog.Add(
            AuditAction.MasterDataUpdated,
            entityName: EntityName,
            entityId: rule.Id.ToString(CultureInfo.InvariantCulture),
            details: new { planId, request });

        // One SaveChanges = one transaction (rule, children and audit row).
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(planId, ruleId, cancellationToken);
    }

    public async Task<PricingRuleDto> SetActiveAsync(long planId, long ruleId, bool isActive, CancellationToken cancellationToken)
    {
        var rule = await FindAsync(planId, ruleId, cancellationToken);

        if (rule.IsActive != isActive)
        {
            if (isActive)
            {
                // Reactivating must not create an ambiguity with the active rules.
                var full = await Db.PriceRules.AsNoTracking()
                    .Include(x => x.Conditions)
                    .Include(x => x.WeightTiers)
                    .FirstAsync(x => x.Id == ruleId, cancellationToken);
                await EnsureNotAmbiguousAsync(planId, ruleId, full.Priority, full.ValidFromUtc, full.ValidToUtc, ToConditionRequests(full), cancellationToken);
            }

            rule.IsActive = isActive;
            _dependencies.AuditLog.Add(
                isActive ? AuditAction.MasterDataActivated : AuditAction.MasterDataDeactivated,
                entityName: EntityName,
                entityId: rule.Id.ToString(CultureInfo.InvariantCulture));
            await Db.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdAsync(planId, ruleId, cancellationToken);
    }

    public async Task DeleteAsync(long planId, long ruleId, CancellationToken cancellationToken)
    {
        var rule = await FindAsync(planId, ruleId, cancellationToken);

        Db.PriceRules.Remove(rule); // soft delete: shipments priced with it keep their snapshots
        _dependencies.AuditLog.Add(
            AuditAction.MasterDataDeleted,
            entityName: EntityName,
            entityId: rule.Id.ToString(CultureInfo.InvariantCulture),
            details: new { planId });

        await Db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<PriceRule> FindAsync(long planId, long ruleId, CancellationToken cancellationToken)
    {
        return await Db.PriceRules.FirstOrDefaultAsync(x => x.PricingPlanId == planId && x.Id == ruleId, cancellationToken)
               ?? throw new NotFoundException("قاعدة التسعير", ruleId);
    }

    private async Task EnsurePlanExistsAsync(long planId, CancellationToken cancellationToken)
    {
        if (!await Db.CustomerPricingPlans.AnyAsync(x => x.Id == planId, cancellationToken))
        {
            throw new NotFoundException("خطة التسعير", planId);
        }
    }

    private async Task ValidateAsync(long planId, SavePricingRuleRequest request, PriceRule? existing, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var current = existing?.Conditions.Where(x => x.ReferenceId.HasValue).Select(x => (x.ConditionType, x.ReferenceId!.Value)).ToHashSet()
                      ?? new HashSet<(PriceConditionType, long)>();

        for (var i = 0; i < request.Conditions.Count; i++)
        {
            var condition = request.Conditions[i];
            if (!PriceConditionTypes.IsReference(condition.ConditionType) || condition.ReferenceId is null)
            {
                continue;
            }

            var id = condition.ReferenceId.Value;
            bool? isActive = condition.ConditionType switch
            {
                PriceConditionType.OriginZone or PriceConditionType.DestinationZone =>
                    await Db.Zones.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsActive).FirstOrDefaultAsync(cancellationToken),
                PriceConditionType.OriginCity or PriceConditionType.DestinationCity =>
                    await Db.Cities.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsActive).FirstOrDefaultAsync(cancellationToken),
                _ =>
                    await Db.ServiceTypes.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsActive).FirstOrDefaultAsync(cancellationToken)
            };

            var fieldName = $"conditions[{i}].referenceId";
            var name = PriceConditionTypes.Arabic(condition.ConditionType);
            if (isActive is null)
            {
                Add(errors, fieldName, $"{name}: السجل رقم {id} غير موجود.");
            }
            else if (isActive == false && !current.Contains((condition.ConditionType, id)))
            {
                Add(errors, fieldName, $"{name}: السجل رقم {id} غير مفعّل.");
            }
        }

        if (request.TaxRateId.HasValue)
        {
            var taxRateId = request.TaxRateId.Value;
            var tax = await Db.TaxRates.AsNoTracking().Where(x => x.Id == taxRateId).Select(x => new { x.IsActive }).FirstOrDefaultAsync(cancellationToken);
            if (tax is null)
            {
                Add(errors, "taxRateId", "نسبة الضريبة غير موجودة.");
            }
            else if (!tax.IsActive && taxRateId != existing?.TaxRateId)
            {
                Add(errors, "taxRateId", "نسبة الضريبة غير مفعّلة.");
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
        }

        if (existing is null || existing.IsActive)
        {
            await EnsureNotAmbiguousAsync(planId, existing?.Id ?? 0, request.Priority, request.ValidFromUtc, request.ValidToUtc, request.Conditions, cancellationToken);
        }
    }

    /// <summary>
    /// 409 when another active rule of the plan has the same priority, the same non-weight conditions, an
    /// overlapping validity and an overlapping weight range ([min, max), empty = [0, unlimited)).
    /// </summary>
    private async Task EnsureNotAmbiguousAsync(
        long planId,
        long ruleId,
        int priority,
        DateTime? validFrom,
        DateTime? validTo,
        IReadOnlyCollection<SavePriceRuleConditionRequest> conditions,
        CancellationToken cancellationToken)
    {
        var signature = Signature(conditions.Select(c => (c.ConditionType, c.ReferenceId, c.MinValue, c.MaxValue)));
        var (weightFrom, weightTo) = WeightRange(conditions.Select(c => (c.ConditionType, c.MinValue, c.MaxValue)));

        var candidates = await Db.PriceRules
            .AsNoTracking()
            .Include(x => x.Conditions)
            .Where(x => x.PricingPlanId == planId && x.Id != ruleId && x.IsActive && x.Priority == priority)
            .ToListAsync(cancellationToken);

        foreach (var other in candidates)
        {
            var otherConditions = other.Conditions.Select(c => (c.ConditionType, c.ReferenceId, c.MinValue, c.MaxValue)).ToList();
            if (Signature(otherConditions) != signature)
            {
                continue;
            }

            var validityOverlaps = (other.ValidFromUtc ?? DateTime.MinValue) < (validTo ?? DateTime.MaxValue) &&
                                   (validFrom ?? DateTime.MinValue) < (other.ValidToUtc ?? DateTime.MaxValue);
            if (!validityOverlaps)
            {
                continue;
            }

            var (otherFrom, otherTo) = WeightRange(otherConditions.Select(c => (c.ConditionType, c.MinValue, c.MaxValue)));
            if (otherFrom < weightTo && weightFrom < otherTo)
            {
                throw new ConflictException(
                    $"نطاق الوزن يتداخل مع قاعدة أخرى لها نفس الشروط والأولوية (رقم {other.Id}: «{other.ArabicName}»). " +
                    "غيّر الأولوية أو نطاق الوزن أو الشروط.");
            }
        }
    }

    /// <summary>Every condition except the weight range, in a stable order.</summary>
    private static string Signature(IEnumerable<(PriceConditionType Type, long? ReferenceId, decimal? Min, decimal? Max)> conditions)
    {
        return string.Join(
            "|",
            conditions
                .Where(c => c.Type != PriceConditionType.ChargeableWeight)
                .Select(c => string.Create(CultureInfo.InvariantCulture, $"{(int)c.Type}:{c.ReferenceId}:{c.Min}:{c.Max}"))
                .OrderBy(x => x, StringComparer.Ordinal));
    }

    private static (decimal From, decimal To) WeightRange(IEnumerable<(PriceConditionType Type, decimal? Min, decimal? Max)> conditions)
    {
        var weight = conditions.FirstOrDefault(c => c.Type == PriceConditionType.ChargeableWeight);
        return weight.Type == PriceConditionType.ChargeableWeight
            ? (weight.Min ?? 0m, weight.Max ?? decimal.MaxValue)
            : (0m, decimal.MaxValue);
    }

    private static List<SavePriceRuleConditionRequest> ToConditionRequests(PriceRule rule) =>
        rule.Conditions
            .Select(c => new SavePriceRuleConditionRequest { ConditionType = c.ConditionType, ReferenceId = c.ReferenceId, MinValue = c.MinValue, MaxValue = c.MaxValue })
            .ToList();

    private static void Add(Dictionary<string, List<string>> errors, string fieldName, string message)
    {
        if (!errors.TryGetValue(fieldName, out var list))
        {
            list = new List<string>();
            errors[fieldName] = list;
        }

        list.Add(message);
    }

    private static void Apply(PriceRule rule, SavePricingRuleRequest request)
    {
        rule.ArabicName = request.ArabicName.Trim();
        rule.EnglishName = string.IsNullOrWhiteSpace(request.EnglishName) ? null : request.EnglishName.Trim();
        rule.Priority = request.Priority;
        rule.ValidFromUtc = request.ValidFromUtc;
        rule.ValidToUtc = request.ValidToUtc;
        rule.BasePrice = request.BasePrice;
        rule.IncludedWeightKg = request.IncludedWeightKg;
        rule.PricePerCubicMeter = request.PricePerCubicMeter;
        rule.AppliesToBasis = request.AppliesToBasis;
        rule.IncludedPackageCount = request.IncludedPackageCount;
        rule.PerPackageFee = request.PerPackageFee;
        rule.CodFeeFixed = request.CodFeeFixed;
        rule.CodFeePercentage = request.CodFeePercentage;
        rule.CodFeeMinimum = request.CodFeeMinimum;
        rule.InsuranceFeePercentage = request.InsuranceFeePercentage;
        rule.InsuranceFeeMinimum = request.InsuranceFeeMinimum;
        rule.PickupFee = request.PickupFee;
        rule.PackagingFee = request.PackagingFee;
        rule.RemoteAreaFee = request.RemoteAreaFee;
        rule.ReturnFee = request.ReturnFee;
        rule.DiscountPercentage = request.DiscountPercentage;
        rule.TaxRateId = request.TaxRateId;
        rule.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        foreach (var condition in request.Conditions)
        {
            var isReference = PriceConditionTypes.IsReference(condition.ConditionType);
            rule.Conditions.Add(new PriceRuleCondition
            {
                ConditionType = condition.ConditionType,
                ReferenceId = isReference ? condition.ReferenceId : null,
                MinValue = isReference ? null : condition.MinValue,
                MaxValue = isReference ? null : condition.MaxValue
            });
        }

        foreach (var tier in request.WeightTiers.OrderBy(x => x.FromWeightKg))
        {
            rule.WeightTiers.Add(new PriceRuleWeightTier
            {
                FromWeightKg = tier.FromWeightKg,
                ToWeightKg = tier.ToWeightKg,
                PricePerKg = tier.PricePerKg
            });
        }
    }

    private async Task<IReadOnlyList<PricingRuleDto>> ToDtosAsync(IReadOnlyCollection<PriceRule> rules, CancellationToken cancellationToken)
    {
        var conditions = rules.SelectMany(x => x.Conditions).Where(x => x.ReferenceId.HasValue).ToList();
        var zoneIds = conditions.Where(x => x.ConditionType is PriceConditionType.OriginZone or PriceConditionType.DestinationZone).Select(x => x.ReferenceId!.Value).Distinct().ToList();
        var cityIds = conditions.Where(x => x.ConditionType is PriceConditionType.OriginCity or PriceConditionType.DestinationCity).Select(x => x.ReferenceId!.Value).Distinct().ToList();
        var serviceIds = conditions.Where(x => x.ConditionType == PriceConditionType.ServiceType).Select(x => x.ReferenceId!.Value).Distinct().ToList();

        var zones = zoneIds.Count == 0
            ? new Dictionary<long, string>()
            : await Db.Zones.AsNoTracking().IgnoreQueryFilters().Where(x => zoneIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);
        var cities = cityIds.Count == 0
            ? new Dictionary<long, string>()
            : await Db.Cities.AsNoTracking().IgnoreQueryFilters().Where(x => cityIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);
        var services = serviceIds.Count == 0
            ? new Dictionary<long, string>()
            : await Db.ServiceTypes.AsNoTracking().IgnoreQueryFilters().Where(x => serviceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);

        string? NameOf(PriceRuleCondition condition)
        {
            if (condition.ReferenceId is not { } id)
            {
                return null;
            }

            var source = condition.ConditionType switch
            {
                PriceConditionType.OriginZone or PriceConditionType.DestinationZone => zones,
                PriceConditionType.OriginCity or PriceConditionType.DestinationCity => cities,
                _ => services
            };

            return source.TryGetValue(id, out var name) ? name : null;
        }

        return rules.Select(x => new PricingRuleDto
        {
            Id = x.Id,
            PricingPlanId = x.PricingPlanId,
            ArabicName = x.ArabicName,
            EnglishName = x.EnglishName,
            Priority = x.Priority,
            ValidFromUtc = x.ValidFromUtc,
            ValidToUtc = x.ValidToUtc,
            BasePrice = x.BasePrice,
            IncludedWeightKg = x.IncludedWeightKg,
            PricePerCubicMeter = x.PricePerCubicMeter,
            AppliesToBasis = x.AppliesToBasis,
            IncludedPackageCount = x.IncludedPackageCount,
            PerPackageFee = x.PerPackageFee,
            CodFeeFixed = x.CodFeeFixed,
            CodFeePercentage = x.CodFeePercentage,
            CodFeeMinimum = x.CodFeeMinimum,
            InsuranceFeePercentage = x.InsuranceFeePercentage,
            InsuranceFeeMinimum = x.InsuranceFeeMinimum,
            PickupFee = x.PickupFee,
            PackagingFee = x.PackagingFee,
            RemoteAreaFee = x.RemoteAreaFee,
            ReturnFee = x.ReturnFee,
            DiscountPercentage = x.DiscountPercentage,
            TaxRateId = x.TaxRateId,
            TaxRateName = x.TaxRate?.ArabicName,
            Notes = x.Notes,
            IsActive = x.IsActive,
            Conditions = x.Conditions
                .OrderBy(c => c.ConditionType)
                .ThenBy(c => c.Id)
                .Select(c => new PriceRuleConditionDto
                {
                    Id = c.Id,
                    ConditionType = c.ConditionType,
                    ConditionTypeName = PriceConditionTypes.Arabic(c.ConditionType),
                    ReferenceId = c.ReferenceId,
                    ReferenceName = NameOf(c),
                    MinValue = c.MinValue,
                    MaxValue = c.MaxValue
                })
                .ToList(),
            WeightTiers = x.WeightTiers
                .OrderBy(t => t.FromWeightKg)
                .Select(t => new PriceRuleWeightTierDto { Id = t.Id, FromWeightKg = t.FromWeightKg, ToWeightKg = t.ToWeightKg, PricePerKg = t.PricePerKg })
                .ToList(),
            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc
        }).ToList();
    }
}
