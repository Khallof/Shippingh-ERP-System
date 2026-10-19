using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Common;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.MasterData;

/// <summary>
/// Shared implementation of list / get / create / update / activate / deactivate / delete for master data.
///
/// A concrete service (BranchService, CustomerService...) only describes what is specific:
/// - how to turn the entity into a DTO (<see cref="ToDto"/>, an expression so EF selects only needed columns),
/// - the list filters and search (<see cref="ApplyFilters"/>) and sortable columns (<see cref="SortFields"/>),
/// - business validation (<see cref="ValidateAsync"/>: unique codes, existing references, branch access),
/// - how the request is copied into the entity (<see cref="Apply"/>),
/// - when deletion must be refused (<see cref="EnsureCanDeleteAsync"/>).
///
/// Everything else is identical for every table: branch filtering, paging, audit log, soft delete.
/// </summary>
public abstract class CrudServiceBase<TEntity, TDto, TSave, TQuery> : ICrudService<TDto, TSave, TQuery>
    where TEntity : SoftDeletableEntity, new()
    where TDto : class
    where TQuery : MasterDataListQuery
{
    private static readonly HashSet<string> NotAudited = new(StringComparer.Ordinal)
    {
        nameof(BaseEntity.CreatedAtUtc), nameof(BaseEntity.CreatedByUserId),
        nameof(BaseEntity.UpdatedAtUtc), nameof(BaseEntity.UpdatedByUserId),
        nameof(BaseEntity.RowVersion)
    };

    protected CrudServiceBase(MasterDataServiceDependencies dependencies)
    {
        Dependencies = dependencies;
    }

    protected MasterDataServiceDependencies Dependencies { get; }

    protected AppDbContext Db => Dependencies.DbContext;

    protected IBranchAccessService BranchAccess => Dependencies.BranchAccess;

    protected DateTime UtcNow => Dependencies.DateTimeProvider.UtcNow;

    /// <summary>Name written in the audit log, e.g. "Branch".</summary>
    protected abstract string EntityName { get; }

    /// <summary>Arabic name used in messages, e.g. "الفرع".</summary>
    protected abstract string DisplayName { get; }

    /// <summary>Entity -> DTO projection, translated to SQL.</summary>
    protected abstract Expression<Func<TEntity, TDto>> ToDto { get; }

    /// <summary>Sortable columns. Keys are the API names (case-insensitive), e.g. "code".</summary>
    protected abstract IReadOnlyDictionary<string, Expression<Func<TEntity, object?>>> SortFields { get; }

    /// <summary>Order used when sortBy is empty or unknown.</summary>
    protected abstract Expression<Func<TEntity, object?>> DefaultSort { get; }

    /// <summary>
    /// The branch a row belongs to, for branch-restricted tables (e.g. x => x.BranchId).
    /// Null = the table is shared by all branches.
    /// </summary>
    protected virtual Expression<Func<TEntity, long?>>? BranchSelector => null;

    protected abstract IQueryable<TEntity> ApplyFilters(IQueryable<TEntity> query, TQuery filter);

    /// <summary>Throws RequestValidationException / ConflictException / ForbiddenException. existing is null on create.</summary>
    protected abstract Task ValidateAsync(TSave request, TEntity? existing, CancellationToken cancellationToken);

    protected abstract void Apply(TEntity entity, TSave request);

    /// <summary>Runs before saving a create or update (e.g. clear the "default" flag of other rows).</summary>
    protected virtual Task BeforeSaveAsync(TEntity entity, bool isNew, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Throw BusinessRuleException when the record is still used by other records.</summary>
    protected virtual Task EnsureCanDeleteAsync(TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Throw BusinessRuleException when the record may not be deactivated.</summary>
    protected virtual Task EnsureCanDeactivateAsync(TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<PagedResult<TDto>> GetPagedAsync(TQuery query, CancellationToken cancellationToken)
    {
        var rows = ApplyFilters(ScopedQuery().AsNoTracking(), query);

        if (query.IsActive.HasValue)
        {
            var isActive = query.IsActive.Value;
            rows = rows.Where(entity => entity.IsActive == isActive);
        }

        var totalCount = await rows.CountAsync(cancellationToken);

        var sort = !string.IsNullOrWhiteSpace(query.SortBy) && SortFields.TryGetValue(query.SortBy, out var selected)
            ? selected
            : DefaultSort;

        var ordered = query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort);

        var items = await ordered
            .ThenBy(entity => entity.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<TDto>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<TDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await ScopedQuery()
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

        return dto ?? throw new NotFoundException(DisplayName, id);
    }

    public async Task<TDto> CreateAsync(TSave request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request, null, cancellationToken);

        var entity = new TEntity { IsActive = true };
        Apply(entity, request);

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            await BeforeSaveAsync(entity, true, ct);
            Db.Set<TEntity>().Add(entity);
            await Db.SaveChangesAsync(ct);

            // Second save in the same transaction: the audit row needs the new id.
            Dependencies.AuditLog.Add(
                AuditAction.MasterDataCreated,
                entityName: EntityName,
                entityId: entity.Id.ToString(),
                details: request);
            await Db.SaveChangesAsync(ct);

            return entity.Id;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<TDto> UpdateAsync(long id, TSave request, CancellationToken cancellationToken)
    {
        var entity = await FindForUpdateAsync(id, cancellationToken);

        await ValidateAsync(request, entity, cancellationToken);
        Apply(entity, request);

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            await BeforeSaveAsync(entity, false, ct);

            var changes = CollectChanges(entity);
            if (changes.Count > 0)
            {
                Dependencies.AuditLog.Add(
                    AuditAction.MasterDataUpdated,
                    entityName: EntityName,
                    entityId: entity.Id.ToString(),
                    details: changes);
            }

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<TDto> SetActiveAsync(long id, bool isActive, CancellationToken cancellationToken)
    {
        var entity = await FindForUpdateAsync(id, cancellationToken);

        if (entity.IsActive != isActive)
        {
            if (!isActive)
            {
                await EnsureCanDeactivateAsync(entity, cancellationToken);
            }

            entity.IsActive = isActive;

            Dependencies.AuditLog.Add(
                isActive ? AuditAction.MasterDataActivated : AuditAction.MasterDataDeactivated,
                entityName: EntityName,
                entityId: entity.Id.ToString());

            // One SaveChanges = one transaction (row + audit).
            await Db.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var entity = await FindForUpdateAsync(id, cancellationToken);

        await EnsureCanDeleteAsync(entity, cancellationToken);

        // The AuditableEntityInterceptor turns this into a soft delete (IsDeleted = 1): nothing is physically removed.
        Db.Set<TEntity>().Remove(entity);

        Dependencies.AuditLog.Add(
            AuditAction.MasterDataDeleted,
            entityName: EntityName,
            entityId: entity.Id.ToString());

        await Db.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- helpers for derived services

    /// <summary>Rows the caller may see (branch restriction applied, soft-deleted rows hidden).</summary>
    protected IQueryable<TEntity> ScopedQuery()
    {
        IQueryable<TEntity> query = Db.Set<TEntity>();
        var selector = BranchSelector;

        if (selector is null || BranchAccess.CanAccessAllBranches)
        {
            return query;
        }

        // Builds: entity => selector(entity) == callerBranchId
        var callerBranchId = BranchAccess.CurrentBranchId;
        var body = Expression.Equal(selector.Body, Expression.Constant(callerBranchId, typeof(long?)));
        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, selector.Parameters));
    }

    protected async Task<TEntity> FindForUpdateAsync(long id, CancellationToken cancellationToken)
    {
        return await ScopedQuery().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken)
               ?? throw new NotFoundException(DisplayName, id);
    }

    /// <summary>
    /// 409 when another row (including soft-deleted rows: codes are never reused) matches <paramref name="duplicate"/>.
    /// </summary>
    protected async Task EnsureUniqueAsync(
        Expression<Func<TEntity, bool>> duplicate,
        TEntity? existing,
        string message,
        CancellationToken cancellationToken)
    {
        var query = Db.Set<TEntity>().IgnoreQueryFilters().Where(duplicate);

        if (existing is not null)
        {
            var existingId = existing.Id;
            query = query.Where(entity => entity.Id != existingId);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new ConflictException(message);
        }
    }

    /// <summary>
    /// 400 when the referenced row does not exist, or is inactive and is being newly assigned
    /// (keeping an existing link to a now-inactive row is allowed).
    /// </summary>
    protected async Task EnsureReferenceAsync<TReference>(
        long? referenceId,
        long? currentReferenceId,
        string fieldName,
        string displayName,
        CancellationToken cancellationToken)
        where TReference : SoftDeletableEntity
    {
        if (referenceId is null)
        {
            return;
        }

        var id = referenceId.Value;
        var reference = await Db.Set<TReference>()
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => new { entity.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (reference is null)
        {
            throw Invalid(fieldName, $"{displayName} غير موجود.");
        }

        if (!reference.IsActive && referenceId != currentReferenceId)
        {
            throw Invalid(fieldName, $"{displayName} غير مفعّل.");
        }
    }

    protected static RequestValidationException Invalid(string fieldName, string message) =>
        new(new Dictionary<string, string[]> { [fieldName] = new[] { message } });

    protected BusinessRuleException InUse(string usedBy) =>
        new($"لا يمكن حذف {DisplayName} لأنه مستخدم في {usedBy}. يمكنك إيقافه بدلاً من الحذف.");

    protected static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    protected static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>"from/to" values of the modified columns, for the audit log.</summary>
    private Dictionary<string, object?> CollectChanges(TEntity entity)
    {
        Db.ChangeTracker.DetectChanges();

        return Db.Entry(entity).Properties
            .Where(property => property.IsModified && !NotAudited.Contains(property.Metadata.Name))
            .Where(property => !Equals(property.OriginalValue, property.CurrentValue))
            .ToDictionary(
                property => property.Metadata.Name,
                property => (object?)new { from = property.OriginalValue, to = property.CurrentValue });
    }
}
