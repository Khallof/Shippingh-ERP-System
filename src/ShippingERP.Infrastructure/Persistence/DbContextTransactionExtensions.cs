using Microsoft.EntityFrameworkCore;

namespace ShippingERP.Infrastructure.Persistence;

/// <summary>
/// Runs several SaveChanges calls (for example UserManager.CreateAsync + AddToRolesAsync + the audit row)
/// as ONE database transaction: either everything is saved or nothing is.
///
/// Because the DbContext uses EnableRetryOnFailure, an explicit transaction must run inside the
/// execution strategy; otherwise EF Core throws. This helper does both.
/// If the operation throws, the transaction is rolled back automatically when it is disposed.
/// </summary>
public static class DbContextTransactionExtensions
{
    public static Task<TResult> ExecuteInTransactionAsync<TResult>(
        this AppDbContext dbContext,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var result = await operation(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
