using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Infrastructure.MasterData;

/// <summary>Warehouses. Restricted to the caller's branch unless Branches.ViewAll.</summary>
public sealed class WarehouseService
    : CrudServiceBase<Warehouse, WarehouseDto, SaveWarehouseRequest, WarehouseListQuery>, IWarehouseService
{
    private static readonly Dictionary<string, Expression<Func<Warehouse, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = x => x.Code,
        ["arabicName"] = x => x.ArabicName,
        ["branch"] = x => x.Branch.ArabicName,
        ["createdAtUtc"] = x => x.CreatedAtUtc
    };

    public WarehouseService(MasterDataServiceDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override string EntityName => "Warehouse";

    protected override string DisplayName => "المستودع";

    protected override IReadOnlyDictionary<string, Expression<Func<Warehouse, object?>>> SortFields => Sorts;

    protected override Expression<Func<Warehouse, object?>> DefaultSort => x => x.Code;

    protected override Expression<Func<Warehouse, long?>>? BranchSelector => x => x.BranchId;

    protected override Expression<Func<Warehouse, WarehouseDto>> ToDto => x => new WarehouseDto
    {
        Id = x.Id,
        Code = x.Code,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        Address = x.Address,
        Phone = x.Phone,
        CapacityCubicMeters = x.CapacityCubicMeters,
        Notes = x.Notes,
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        UpdatedAtUtc = x.UpdatedAtUtc,
        UpdatedByUserId = x.UpdatedByUserId
    };

    protected override IQueryable<Warehouse> ApplyFilters(IQueryable<Warehouse> query, WarehouseListQuery filter)
    {
        if (filter.BranchId.HasValue)
        {
            var branchId = filter.BranchId.Value;
            query = query.Where(x => x.BranchId == branchId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                x.Code.Contains(search) ||
                x.ArabicName.Contains(search) ||
                (x.EnglishName != null && x.EnglishName.Contains(search)));
        }

        return query;
    }

    protected override async Task ValidateAsync(SaveWarehouseRequest request, Warehouse? existing, CancellationToken cancellationToken)
    {
        BranchAccess.EnsureCanAccess(request.BranchId);

        var code = NormalizeCode(request.Code);
        await EnsureUniqueAsync(x => x.Code == code, existing, $"رمز المستودع '{code}' مستخدم من قبل.", cancellationToken);
        await EnsureReferenceAsync<Branch>(request.BranchId, existing?.BranchId, "branchId", "الفرع", cancellationToken);
    }

    protected override void Apply(Warehouse entity, SaveWarehouseRequest request)
    {
        entity.Code = NormalizeCode(request.Code);
        entity.ArabicName = request.ArabicName.Trim();
        entity.EnglishName = Optional(request.EnglishName);
        entity.BranchId = request.BranchId;
        entity.Address = Optional(request.Address);
        entity.Phone = Optional(request.Phone);
        entity.CapacityCubicMeters = request.CapacityCubicMeters;
        entity.Notes = Optional(request.Notes);
    }

    protected override async Task EnsureCanDeleteAsync(Warehouse entity, CancellationToken cancellationToken)
    {
        var id = entity.Id;

        if (await Db.Shipments.AnyAsync(x => x.CurrentWarehouseId == id, cancellationToken) ||
            await Db.ShipmentManifests.AnyAsync(x => x.WarehouseId == id, cancellationToken))
        {
            throw InUse("شحنات أو بيانات حركة مستودع");
        }
    }
}
