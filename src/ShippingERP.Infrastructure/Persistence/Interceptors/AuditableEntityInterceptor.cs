using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Common;

namespace ShippingERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Runs automatically just before every SaveChanges / SaveChangesAsync and:
/// 1. Blocks updates/deletes of append-only entities (audit logs, journal entries, COD transactions...).
/// 2. Converts deletes of ISoftDeletable entities into soft deletes.
/// 3. Fills CreatedAtUtc/CreatedByUserId and UpdatedAtUtc/UpdatedByUserId on every IAuditableEntity
///    (all BaseEntity classes plus ApplicationUser and ApplicationRole).
///
/// Because this runs in one place, no developer can forget to set audit fields.
/// </summary>
public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;

    public AuditableEntityInterceptor(IDateTimeProvider dateTimeProvider, ICurrentUserService currentUserService)
    {
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditRules(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditRules(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditRules(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var userId = _currentUserService.UserId;

        // ToList() because we change entry states inside the loop.
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            GuardAppendOnly(entry);
            ApplySoftDelete(entry, utcNow, userId);
            ApplyAuditFields(entry, utcNow, userId);
        }
    }

    private static void GuardAppendOnly(EntityEntry entry)
    {
        if (entry.Entity is IAppendOnlyEntity &&
            entry.State is EntityState.Modified or EntityState.Deleted)
        {
            throw new InvalidOperationException(
                $"Entity '{entry.Metadata.ClrType.Name}' is append-only. " +
                "It cannot be updated or deleted; insert a reversing record instead.");
        }
    }

    private static void ApplySoftDelete(EntityEntry entry, DateTime utcNow, long? userId)
    {
        if (entry.State != EntityState.Deleted || entry.Entity is not ISoftDeletable softDeletable)
        {
            return;
        }

        // Turn the DELETE into an UPDATE.
        entry.State = EntityState.Modified;
        softDeletable.IsDeleted = true;
        softDeletable.DeletedAtUtc = utcNow;
        softDeletable.DeletedByUserId = userId;

        if (entry.Entity is SoftDeletableEntity softDeletableEntity)
        {
            softDeletableEntity.IsActive = false;
        }
    }

    private static void ApplyAuditFields(EntityEntry entry, DateTime utcNow, long? userId)
    {
        if (entry.Entity is not IAuditableEntity entity)
        {
            return;
        }

        switch (entry.State)
        {
            case EntityState.Added:
                entity.CreatedAtUtc = utcNow;
                // ??= keeps a value set on purpose (e.g. the user id on a sign-in audit record,
                // where nobody is authenticated yet).
                entity.CreatedByUserId ??= userId;
                entity.UpdatedAtUtc = null;
                entity.UpdatedByUserId = null;
                break;

            case EntityState.Modified:
                entity.UpdatedAtUtc = utcNow;
                entity.UpdatedByUserId = userId;

                // Creation info must never change after insert.
                entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).IsModified = false;
                entry.Property(nameof(IAuditableEntity.CreatedByUserId)).IsModified = false;
                break;
        }
    }
}
