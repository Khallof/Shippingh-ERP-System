using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.HR;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Infrastructure.MasterData;

/// <summary>Employees. Restricted to the caller's branch unless Branches.ViewAll. One system user per employee.</summary>
public sealed class EmployeeService : CrudServiceBase<Employee, EmployeeDto, SaveEmployeeRequest, EmployeeListQuery>, IEmployeeService
{
    private static readonly Dictionary<string, Expression<Func<Employee, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = x => x.Code,
        ["arabicName"] = x => x.ArabicName,
        ["hireDateUtc"] = x => x.HireDateUtc,
        ["createdAtUtc"] = x => x.CreatedAtUtc
    };

    public EmployeeService(MasterDataServiceDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override string EntityName => "Employee";

    protected override string DisplayName => "الموظف";

    protected override IReadOnlyDictionary<string, Expression<Func<Employee, object?>>> SortFields => Sorts;

    protected override Expression<Func<Employee, object?>> DefaultSort => x => x.Code;

    protected override Expression<Func<Employee, long?>>? BranchSelector => x => x.BranchId;

    protected override Expression<Func<Employee, EmployeeDto>> ToDto => x => new EmployeeDto
    {
        Id = x.Id,
        Code = x.Code,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        JobTitle = x.JobTitle,
        Phone = x.Phone,
        Email = x.Email,
        NationalId = x.NationalId,
        HireDateUtc = x.HireDateUtc,
        UserId = x.UserId,
        UserName = x.User != null ? x.User.UserName : null,
        IsDriver = x.DriverProfile != null,
        Notes = x.Notes,
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        UpdatedAtUtc = x.UpdatedAtUtc,
        UpdatedByUserId = x.UpdatedByUserId
    };

    protected override IQueryable<Employee> ApplyFilters(IQueryable<Employee> query, EmployeeListQuery filter)
    {
        if (filter.BranchId.HasValue)
        {
            var branchId = filter.BranchId.Value;
            query = query.Where(x => x.BranchId == branchId);
        }

        if (filter.HasUser.HasValue)
        {
            query = filter.HasUser.Value
                ? query.Where(x => x.UserId != null)
                : query.Where(x => x.UserId == null);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                x.Code.Contains(search) ||
                x.ArabicName.Contains(search) ||
                (x.EnglishName != null && x.EnglishName.Contains(search)) ||
                (x.Phone != null && x.Phone.Contains(search)) ||
                (x.JobTitle != null && x.JobTitle.Contains(search)));
        }

        return query;
    }

    protected override async Task ValidateAsync(SaveEmployeeRequest request, Employee? existing, CancellationToken cancellationToken)
    {
        BranchAccess.EnsureCanAccess(request.BranchId);

        var code = NormalizeCode(request.Code);
        await EnsureUniqueAsync(x => x.Code == code, existing, $"الرقم الوظيفي '{code}' مستخدم من قبل.", cancellationToken);
        await EnsureReferenceAsync<Branch>(request.BranchId, existing?.BranchId, "branchId", "الفرع", cancellationToken);

        if (request.UserId.HasValue)
        {
            var userId = request.UserId.Value;
            if (!await Db.Users.AnyAsync(x => x.Id == userId, cancellationToken))
            {
                throw Invalid("userId", "المستخدم غير موجود.");
            }

            await EnsureUniqueAsync(x => x.UserId == userId, existing, "هذا المستخدم مرتبط بموظف آخر.", cancellationToken);
        }

        if (existing is not null && existing.BranchId != request.BranchId)
        {
            var employeeId = existing.Id;
            var vehicleBranchMismatch = await Db.DriverProfiles.AnyAsync(
                x => x.EmployeeId == employeeId && x.DefaultVehicleId != null && x.DefaultVehicle!.BranchId != request.BranchId,
                cancellationToken);

            if (vehicleBranchMismatch)
            {
                throw Invalid("branchId", "للموظف ملف سائق بمركبة من فرعه الحالي. غيّر مركبة السائق أولاً.");
            }
        }
    }

    protected override void Apply(Employee entity, SaveEmployeeRequest request)
    {
        entity.Code = NormalizeCode(request.Code);
        entity.ArabicName = request.ArabicName.Trim();
        entity.EnglishName = Optional(request.EnglishName);
        entity.BranchId = request.BranchId;
        entity.JobTitle = Optional(request.JobTitle);
        entity.Phone = Optional(request.Phone);
        entity.Email = Optional(request.Email);
        entity.NationalId = Optional(request.NationalId);
        entity.HireDateUtc = request.HireDateUtc;
        entity.UserId = request.UserId;
        entity.Notes = Optional(request.Notes);
    }

    protected override async Task EnsureCanDeleteAsync(Employee entity, CancellationToken cancellationToken)
    {
        var id = entity.Id;
        if (await Db.DriverProfiles.AnyAsync(x => x.EmployeeId == id, cancellationToken))
        {
            throw InUse("ملف سائق");
        }
    }
}

/// <summary>
/// Driver profiles. A driver is always an employee (one profile per employee); the branch is the employee's branch.
/// </summary>
public sealed class DriverService : CrudServiceBase<DriverProfile, DriverDto, SaveDriverRequest, DriverListQuery>, IDriverService
{
    private static readonly Dictionary<string, Expression<Func<DriverProfile, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["employeeName"] = x => x.Employee.ArabicName,
        ["licenseExpiryDateUtc"] = x => x.LicenseExpiryDateUtc
    };

    public DriverService(MasterDataServiceDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override string EntityName => "DriverProfile";

    protected override string DisplayName => "ملف السائق";

    protected override IReadOnlyDictionary<string, Expression<Func<DriverProfile, object?>>> SortFields => Sorts;

    protected override Expression<Func<DriverProfile, object?>> DefaultSort => x => x.Employee.ArabicName;

    protected override Expression<Func<DriverProfile, long?>>? BranchSelector => x => x.Employee.BranchId;

    protected override Expression<Func<DriverProfile, DriverDto>> ToDto
    {
        get
        {
            var now = UtcNow;
            return x => new DriverDto
            {
                Id = x.Id,
                EmployeeId = x.EmployeeId,
                EmployeeCode = x.Employee.Code,
                EmployeeName = x.Employee.ArabicName,
                BranchId = x.Employee.BranchId,
                BranchName = x.Employee.Branch.ArabicName,
                Phone = x.Employee.Phone,
                UserId = x.UserId,
                UserName = x.User != null ? x.User.UserName : null,
                LicenseNumber = x.LicenseNumber,
                LicenseCategory = x.LicenseCategory,
                LicenseExpiryDateUtc = x.LicenseExpiryDateUtc,
                IsLicenseExpired = x.LicenseExpiryDateUtc < now,
                DefaultVehicleId = x.DefaultVehicleId,
                DefaultVehiclePlate = x.DefaultVehicle != null ? x.DefaultVehicle.PlateNumber : null,
                Notes = x.Notes,
                IsActive = x.IsActive,
                CreatedAtUtc = x.CreatedAtUtc,
                CreatedByUserId = x.CreatedByUserId,
                UpdatedAtUtc = x.UpdatedAtUtc,
                UpdatedByUserId = x.UpdatedByUserId
            };
        }
    }

    protected override IQueryable<DriverProfile> ApplyFilters(IQueryable<DriverProfile> query, DriverListQuery filter)
    {
        if (filter.BranchId.HasValue)
        {
            var branchId = filter.BranchId.Value;
            query = query.Where(x => x.Employee.BranchId == branchId);
        }

        if (filter.LicenseExpiringSoon.HasValue)
        {
            var limit = UtcNow.AddDays(30);
            query = filter.LicenseExpiringSoon.Value
                ? query.Where(x => x.LicenseExpiryDateUtc < limit)
                : query.Where(x => x.LicenseExpiryDateUtc >= limit);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                x.LicenseNumber.Contains(search) ||
                x.Employee.Code.Contains(search) ||
                x.Employee.ArabicName.Contains(search) ||
                (x.Employee.EnglishName != null && x.Employee.EnglishName.Contains(search)) ||
                (x.Employee.Phone != null && x.Employee.Phone.Contains(search)));
        }

        return query;
    }

    protected override async Task ValidateAsync(SaveDriverRequest request, DriverProfile? existing, CancellationToken cancellationToken)
    {
        var employee = await Db.Employees
            .AsNoTracking()
            .Where(x => x.Id == request.EmployeeId)
            .Select(x => new { x.Id, x.BranchId, x.IsActive, x.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            throw Invalid("employeeId", "الموظف غير موجود.");
        }

        if (!employee.IsActive && existing?.EmployeeId != employee.Id)
        {
            throw Invalid("employeeId", "الموظف غير مفعّل.");
        }

        BranchAccess.EnsureCanAccess(employee.BranchId);

        var employeeId = employee.Id;
        await EnsureUniqueAsync(x => x.EmployeeId == employeeId, existing, "لهذا الموظف ملف سائق مسبقاً.", cancellationToken);

        var licenseNumber = request.LicenseNumber.Trim().ToUpperInvariant();
        await EnsureUniqueAsync(x => x.LicenseNumber == licenseNumber, existing, $"رقم الرخصة '{licenseNumber}' مسجل لسائق آخر.", cancellationToken);

        var userId = request.UserId ?? employee.UserId;
        if (userId.HasValue)
        {
            var id = userId.Value;
            if (!await Db.Users.AnyAsync(x => x.Id == id, cancellationToken))
            {
                throw Invalid("userId", "المستخدم غير موجود.");
            }

            await EnsureUniqueAsync(x => x.UserId == id, existing, "هذا المستخدم مرتبط بسائق آخر.", cancellationToken);
        }

        if (request.DefaultVehicleId.HasValue)
        {
            var vehicleId = request.DefaultVehicleId.Value;
            var vehicle = await Db.Vehicles
                .AsNoTracking()
                .Where(x => x.Id == vehicleId)
                .Select(x => new { x.BranchId, x.IsActive })
                .FirstOrDefaultAsync(cancellationToken);

            if (vehicle is null)
            {
                throw Invalid("defaultVehicleId", "المركبة غير موجودة.");
            }

            if (!vehicle.IsActive && existing?.DefaultVehicleId != vehicleId)
            {
                throw Invalid("defaultVehicleId", "المركبة غير مفعّلة.");
            }

            if (vehicle.BranchId != employee.BranchId)
            {
                throw Invalid("defaultVehicleId", "المركبة يجب أن تتبع نفس فرع السائق.");
            }
        }
    }

    protected override void Apply(DriverProfile entity, SaveDriverRequest request)
    {
        entity.EmployeeId = request.EmployeeId;
        entity.UserId = request.UserId;
        entity.LicenseNumber = request.LicenseNumber.Trim().ToUpperInvariant();
        entity.LicenseCategory = Optional(request.LicenseCategory);
        entity.LicenseExpiryDateUtc = request.LicenseExpiryDateUtc;
        entity.DefaultVehicleId = request.DefaultVehicleId;
        entity.Notes = Optional(request.Notes);
    }

    protected override async Task BeforeSaveAsync(DriverProfile entity, bool isNew, CancellationToken cancellationToken)
    {
        // No user given: use the employee's own user account (if any) for the driver app.
        if (entity.UserId is null)
        {
            var employeeId = entity.EmployeeId;
            entity.UserId = await Db.Employees
                .Where(x => x.Id == employeeId)
                .Select(x => x.UserId)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    protected override async Task EnsureCanDeleteAsync(DriverProfile entity, CancellationToken cancellationToken)
    {
        var id = entity.Id;

        if (await Db.Shipments.AnyAsync(x => x.AssignedDriverId == id, cancellationToken) ||
            await Db.DriverDeliveryManifests.AnyAsync(x => x.DriverId == id, cancellationToken) ||
            await Db.BranchTransfers.AnyAsync(x => x.DriverId == id, cancellationToken))
        {
            throw InUse("شحنات أو بيانات توزيع");
        }

        // Step 6: a driver with a cash account (even settled) keeps his history.
        if (await Db.DriverCashAccounts.AnyAsync(x => x.DriverId == id, cancellationToken) ||
            await Db.CODTransactions.AnyAsync(x => x.DriverId == id, cancellationToken))
        {
            throw InUse("عهدة نقدية أو مبالغ تحصيل");
        }

        // Step 7: ledger lines of the driver.
        if (await Db.JournalEntryLines.AnyAsync(x => x.DriverId == id, cancellationToken))
        {
            throw InUse("قيود محاسبية");
        }
    }
}
