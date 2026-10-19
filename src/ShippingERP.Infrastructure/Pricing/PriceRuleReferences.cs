using Microsoft.EntityFrameworkCore;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Pricing;

/// <summary>
/// Price rule conditions point to zones, cities and service types by id WITHOUT a foreign key (the condition type
/// decides the table). The master-data services call this before deleting such a record.
/// Only conditions of rules that are not deleted count.
/// </summary>
public static class PriceRuleReferences
{
    public static Task<bool> IsUsedAsync(AppDbContext dbContext, long referenceId, CancellationToken cancellationToken, params PriceConditionType[] types)
    {
        return dbContext.PriceRuleConditions
            .AsNoTracking()
            .AnyAsync(
                x => x.ReferenceId == referenceId &&
                     types.Contains(x.ConditionType) &&
                     !x.PriceRule.IsDeleted,
                cancellationToken);
    }
}
