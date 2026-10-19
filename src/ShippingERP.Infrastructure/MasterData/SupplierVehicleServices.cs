using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Infrastructure.MasterData;

/// <summary>Suppliers (company-wide, not branch-restricted).</summary>
public sealed class SupplierService : CrudServiceBase<Supplier, SupplierDto, SaveSupplierRequest, SupplierListQuery>, ISupplierService
{
    private static readonly Dictionary<string, Expression<Func<Supplier, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = x => x.Code,
        ["arabicName"] = x => x.ArabicName,
        ["createdAtUtc"] = x => x.CreatedAtUtc
    };

    public SupplierService(MasterDataServiceDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override string EntityName => "Supplier";

    protected override string DisplayName => "المورد";

    protected override IReadOnlyDictionary<string, Expression<Func<Supplier, object?>>> SortFields => Sorts;

    protected override Expression<Func<Supplier, object?>> DefaultSort => x => x.Code;

    protected override Expression<Func<Supplier, SupplierDto>> ToDto => x => new SupplierDto
    {
        Id = x.Id,
        Code = x.Code,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        ContactPerson = x.ContactPerson,
        Phone = x.Phone,
        AlternatePhone = x.AlternatePhone,
        Email = x.Email,
        TaxNumber = x.TaxNumber,
        Address = x.Address,
        OpeningBalance = x.OpeningBalance,
        Notes = x.Notes,
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        UpdatedAtUtc = x.UpdatedAtUtc,
        UpdatedByUserId = x.UpdatedByUserId
    };

    protected override IQueryable<Supplier> ApplyFilters(IQueryable<Supplier> query, SupplierListQuery filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                x.Code.Contains(search) ||
                x.ArabicName.Contains(search) ||
                (x.EnglishName != null && x.EnglishName.Contains(search)) ||
                x.Phone.Contains(search) ||
                (x.AlternatePhone != null && x.AlternatePhone.Contains(search)) ||
                (x.ContactPerson != null && x.ContactPerson.Contains(search)));
        }

        return query;
    }

    protected override async Task ValidateAsync(SaveSupplierRequest request, Supplier? existing, CancellationToken cancellationToken)
    {
        var code = NormalizeCode(request.Code);
        await EnsureUniqueAsync(x => x.Code == code, existing, $"رمز المورد '{code}' مستخدم من قبل.", cancellationToken);
    }

    protected override void Apply(Supplier entity, SaveSupplierRequest request)
    {
        entity.Code = NormalizeCode(request.Code);
        entity.ArabicName = request.ArabicName.Trim();
        entity.EnglishName = Optional(request.EnglishName);
        entity.ContactPerson = Optional(request.ContactPerson);
        entity.Phone = request.Phone.Trim();
        entity.AlternatePhone = Optional(request.AlternatePhone);
        entity.Email = Optional(request.Email);
        entity.TaxNumber = Optional(request.TaxNumber);
        entity.Address = Optional(request.Address);
        entity.OpeningBalance = request.OpeningBalance;
        entity.Notes = Optional(request.Notes);
    }

    protected override async Task EnsureCanDeleteAsync(Supplier entity, CancellationToken cancellationToken)
    {
        if (entity.OpeningBalance != 0m)
        {
            throw new BusinessRuleException("لا يمكن حذف مورد له رصيد افتتاحي. يمكنك إيقافه بدلاً من الحذف.");
        }

        var id = entity.Id;
        if (await Db.CashPaymentVouchers.AnyAsync(x => x.SupplierId == id, cancellationToken))
        {
            throw InUse("سندات صرف نقدية");
        }

        // Step 7: ledger lines of the supplier (supplier payables).
        if (await Db.JournalEntryLines.AnyAsync(x => x.SupplierId == id, cancellationToken))
        {
            throw InUse("قيود محاسبية");
        }
    }
}

/// <summary>Vehicles. Restricted to the caller's branch unless Branches.ViewAll. Plate numbers are unique.</summary>
public sealed class VehicleService : CrudServiceBase<Vehicle, VehicleDto, SaveVehicleRequest, VehicleListQuery>, IVehicleService
{
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    private static readonly Dictionary<string, Expression<Func<Vehicle, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["plateNumber"] = x => x.PlateNumber,
        ["model"] = x => x.Model,
        ["capacityKg"] = x => x.CapacityKg,
        ["createdAtUtc"] = x => x.CreatedAtUtc
    };

    public VehicleService(MasterDataServiceDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override string EntityName => "Vehicle";

    protected override string DisplayName => "المركبة";

    protected override IReadOnlyDictionary<string, Expression<Func<Vehicle, object?>>> SortFields => Sorts;

    protected override Expression<Func<Vehicle, object?>> DefaultSort => x => x.PlateNumber;

    protected override Expression<Func<Vehicle, long?>>? BranchSelector => x => x.BranchId;

    protected override Expression<Func<Vehicle, VehicleDto>> ToDto => x => new VehicleDto
    {
        Id = x.Id,
        PlateNumber = x.PlateNumber,
        Model = x.Model,
        VehicleType = x.VehicleType,
        CapacityKg = x.CapacityKg,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        Notes = x.Notes,
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        UpdatedAtUtc = x.UpdatedAtUtc,
        UpdatedByUserId = x.UpdatedByUserId
    };

    /// <summary>"  dam  123-456 " -> "DAM 123-456".</summary>
    public static string NormalizePlate(string plateNumber) =>
        Spaces.Replace(plateNumber.Trim(), " ").ToUpperInvariant();

    protected override IQueryable<Vehicle> ApplyFilters(IQueryable<Vehicle> query, VehicleListQuery filter)
    {
        if (filter.BranchId.HasValue)
        {
            var branchId = filter.BranchId.Value;
            query = query.Where(x => x.BranchId == branchId);
        }

        if (filter.VehicleType.HasValue)
        {
            var vehicleType = filter.VehicleType.Value;
            query = query.Where(x => x.VehicleType == vehicleType);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x => x.PlateNumber.Contains(search) || x.Model.Contains(search));
        }

        return query;
    }

    protected override async Task ValidateAsync(SaveVehicleRequest request, Vehicle? existing, CancellationToken cancellationToken)
    {
        BranchAccess.EnsureCanAccess(request.BranchId);

        var plate = NormalizePlate(request.PlateNumber);
        await EnsureUniqueAsync(x => x.PlateNumber == plate, existing, $"رقم اللوحة '{plate}' مسجل لمركبة أخرى.", cancellationToken);
        await EnsureReferenceAsync<Branch>(request.BranchId, existing?.BranchId, "branchId", "الفرع", cancellationToken);

        if (existing is not null && existing.BranchId != request.BranchId)
        {
            var id = existing.Id;
            if (await Db.DriverProfiles.AnyAsync(x => x.DefaultVehicleId == id, cancellationToken))
            {
                throw Invalid("branchId", "لا يمكن نقل المركبة إلى فرع آخر وهي مركبة افتراضية لسائق. غيّر مركبة السائق أولاً.");
            }
        }
    }

    protected override void Apply(Vehicle entity, SaveVehicleRequest request)
    {
        entity.PlateNumber = NormalizePlate(request.PlateNumber);
        entity.Model = request.Model.Trim();
        entity.VehicleType = request.VehicleType;
        entity.CapacityKg = request.CapacityKg;
        entity.BranchId = request.BranchId;
        entity.Notes = Optional(request.Notes);
    }

    protected override async Task EnsureCanDeleteAsync(Vehicle entity, CancellationToken cancellationToken)
    {
        var id = entity.Id;
        if (await Db.DriverProfiles.AnyAsync(x => x.DefaultVehicleId == id, cancellationToken))
        {
            throw InUse("ملفات السائقين");
        }

        if (await Db.Shipments.AnyAsync(x => x.VehicleId == id, cancellationToken) ||
            await Db.BranchTransfers.AnyAsync(x => x.VehicleId == id, cancellationToken) ||
            await Db.DriverDeliveryManifests.AnyAsync(x => x.VehicleId == id, cancellationToken))
        {
            throw InUse("شحنات أو بيانات نقل");
        }
    }
}
