using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Pricing;

namespace ShippingERP.Infrastructure.MasterData;

/// <summary>Countries (codes are global and unique).</summary>
public sealed class CountryService : CrudServiceBase<Country, CountryDto, SaveCountryRequest, CountryListQuery>, ICountryService
{
    private static readonly Dictionary<string, Expression<Func<Country, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = x => x.Code,
        ["arabicName"] = x => x.ArabicName,
        ["englishName"] = x => x.EnglishName
    };

    public CountryService(MasterDataServiceDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override string EntityName => "Country";

    protected override string DisplayName => "الدولة";

    protected override IReadOnlyDictionary<string, Expression<Func<Country, object?>>> SortFields => Sorts;

    protected override Expression<Func<Country, object?>> DefaultSort => x => x.ArabicName;

    protected override Expression<Func<Country, CountryDto>> ToDto => x => new CountryDto
    {
        Id = x.Id,
        Code = x.Code,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        PhoneCode = x.PhoneCode,
        CityCount = x.Cities.Count(),
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        UpdatedAtUtc = x.UpdatedAtUtc,
        UpdatedByUserId = x.UpdatedByUserId
    };

    protected override IQueryable<Country> ApplyFilters(IQueryable<Country> query, CountryListQuery filter)
    {
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

    protected override async Task ValidateAsync(SaveCountryRequest request, Country? existing, CancellationToken cancellationToken)
    {
        var code = NormalizeCode(request.Code);
        await EnsureUniqueAsync(x => x.Code == code, existing, $"رمز الدولة '{code}' مستخدم من قبل.", cancellationToken);
    }

    protected override void Apply(Country entity, SaveCountryRequest request)
    {
        entity.Code = NormalizeCode(request.Code);
        entity.ArabicName = request.ArabicName.Trim();
        entity.EnglishName = Optional(request.EnglishName);
        entity.PhoneCode = Optional(request.PhoneCode);
    }

    protected override async Task EnsureCanDeleteAsync(Country entity, CancellationToken cancellationToken)
    {
        var id = entity.Id;
        if (await Db.Cities.AnyAsync(x => x.CountryId == id, cancellationToken))
        {
            throw InUse("مدن");
        }
    }
}

/// <summary>Cities (codes are unique inside their country).</summary>
public sealed class CityService : CrudServiceBase<City, CityDto, SaveCityRequest, CityListQuery>, ICityService
{
    private static readonly Dictionary<string, Expression<Func<City, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = x => x.Code,
        ["arabicName"] = x => x.ArabicName,
        ["englishName"] = x => x.EnglishName
    };

    public CityService(MasterDataServiceDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override string EntityName => "City";

    protected override string DisplayName => "المدينة";

    protected override IReadOnlyDictionary<string, Expression<Func<City, object?>>> SortFields => Sorts;

    protected override Expression<Func<City, object?>> DefaultSort => x => x.ArabicName;

    protected override Expression<Func<City, CityDto>> ToDto => x => new CityDto
    {
        Id = x.Id,
        CountryId = x.CountryId,
        CountryName = x.Country.ArabicName,
        Code = x.Code,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        ZoneCount = x.Zones.Count(),
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        UpdatedAtUtc = x.UpdatedAtUtc,
        UpdatedByUserId = x.UpdatedByUserId
    };

    protected override IQueryable<City> ApplyFilters(IQueryable<City> query, CityListQuery filter)
    {
        if (filter.CountryId.HasValue)
        {
            var countryId = filter.CountryId.Value;
            query = query.Where(x => x.CountryId == countryId);
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

    protected override async Task ValidateAsync(SaveCityRequest request, City? existing, CancellationToken cancellationToken)
    {
        await EnsureReferenceAsync<Country>(request.CountryId, existing?.CountryId, "countryId", "الدولة", cancellationToken);

        var code = NormalizeCode(request.Code);
        var countryId = request.CountryId;
        await EnsureUniqueAsync(
            x => x.CountryId == countryId && x.Code == code,
            existing,
            $"رمز المدينة '{code}' مستخدم من قبل في هذه الدولة.",
            cancellationToken);
    }

    protected override void Apply(City entity, SaveCityRequest request)
    {
        entity.CountryId = request.CountryId;
        entity.Code = NormalizeCode(request.Code);
        entity.ArabicName = request.ArabicName.Trim();
        entity.EnglishName = Optional(request.EnglishName);
    }

    protected override async Task EnsureCanDeleteAsync(City entity, CancellationToken cancellationToken)
    {
        var id = entity.Id;

        if (await Db.Zones.AnyAsync(x => x.CityId == id, cancellationToken))
        {
            throw InUse("مناطق");
        }

        if (await Db.Branches.AnyAsync(x => x.CityId == id, cancellationToken))
        {
            throw InUse("فروع");
        }

        if (await Db.CustomerAddresses.AnyAsync(x => x.CityId == id, cancellationToken))
        {
            throw InUse("عناوين عملاء");
        }

        if (await Db.Shipments.AnyAsync(x => x.OriginCityId == id || x.DestinationCityId == id, cancellationToken))
        {
            throw InUse("شحنات");
        }

        if (await PriceRuleReferences.IsUsedAsync(Db, id, cancellationToken, PriceConditionType.OriginCity, PriceConditionType.DestinationCity))
        {
            throw InUse("قواعد تسعير");
        }
    }
}

/// <summary>Zones / districts (codes are unique inside their city).</summary>
public sealed class ZoneService : CrudServiceBase<Zone, ZoneDto, SaveZoneRequest, ZoneListQuery>, IZoneService
{
    private static readonly Dictionary<string, Expression<Func<Zone, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = x => x.Code,
        ["arabicName"] = x => x.ArabicName,
        ["englishName"] = x => x.EnglishName
    };

    public ZoneService(MasterDataServiceDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override string EntityName => "Zone";

    protected override string DisplayName => "المنطقة";

    protected override IReadOnlyDictionary<string, Expression<Func<Zone, object?>>> SortFields => Sorts;

    protected override Expression<Func<Zone, object?>> DefaultSort => x => x.ArabicName;

    protected override Expression<Func<Zone, ZoneDto>> ToDto => x => new ZoneDto
    {
        Id = x.Id,
        CityId = x.CityId,
        CityName = x.City.ArabicName,
        CountryId = x.City.CountryId,
        Code = x.Code,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        IsRemoteArea = x.IsRemoteArea,
        Notes = x.Notes,
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        UpdatedAtUtc = x.UpdatedAtUtc,
        UpdatedByUserId = x.UpdatedByUserId
    };

    protected override IQueryable<Zone> ApplyFilters(IQueryable<Zone> query, ZoneListQuery filter)
    {
        if (filter.CityId.HasValue)
        {
            var cityId = filter.CityId.Value;
            query = query.Where(x => x.CityId == cityId);
        }

        if (filter.IsRemoteArea.HasValue)
        {
            var isRemoteArea = filter.IsRemoteArea.Value;
            query = query.Where(x => x.IsRemoteArea == isRemoteArea);
        }

        if (filter.CountryId.HasValue)
        {
            var countryId = filter.CountryId.Value;
            query = query.Where(x => x.City.CountryId == countryId);
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

    protected override async Task ValidateAsync(SaveZoneRequest request, Zone? existing, CancellationToken cancellationToken)
    {
        await EnsureReferenceAsync<City>(request.CityId, existing?.CityId, "cityId", "المدينة", cancellationToken);

        var code = NormalizeCode(request.Code);
        var cityId = request.CityId;
        await EnsureUniqueAsync(
            x => x.CityId == cityId && x.Code == code,
            existing,
            $"رمز المنطقة '{code}' مستخدم من قبل في هذه المدينة.",
            cancellationToken);
    }

    protected override void Apply(Zone entity, SaveZoneRequest request)
    {
        entity.CityId = request.CityId;
        entity.Code = NormalizeCode(request.Code);
        entity.ArabicName = request.ArabicName.Trim();
        entity.EnglishName = Optional(request.EnglishName);
        entity.IsRemoteArea = request.IsRemoteArea;
        entity.Notes = Optional(request.Notes);
    }

    protected override async Task EnsureCanDeleteAsync(Zone entity, CancellationToken cancellationToken)
    {
        var id = entity.Id;

        if (await Db.CustomerAddresses.AnyAsync(x => x.ZoneId == id, cancellationToken))
        {
            throw InUse("عناوين عملاء");
        }

        if (await PriceRuleReferences.IsUsedAsync(Db, id, cancellationToken, PriceConditionType.OriginZone, PriceConditionType.DestinationZone))
        {
            throw InUse("قواعد تسعير");
        }

        if (await Db.Shipments.AnyAsync(x => x.DestinationZoneId == id || x.OriginZoneId == id, cancellationToken))
        {
            throw InUse("شحنات");
        }
    }
}
