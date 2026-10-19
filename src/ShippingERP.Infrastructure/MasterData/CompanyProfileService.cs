using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.MasterData;

/// <summary>The single company profile row (created by the first PUT).</summary>
public sealed class CompanyProfileService : ICompanyProfileService
{
    private const string EntityName = "CompanyProfile";

    private readonly MasterDataServiceDependencies _dependencies;

    public CompanyProfileService(MasterDataServiceDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    private AppDbContext Db => _dependencies.DbContext;

    private static Expression<Func<CompanyProfile, CompanyProfileDto>> ToDto => x => new CompanyProfileDto
    {
        Id = x.Id,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        CommercialRegistrationNumber = x.CommercialRegistrationNumber,
        TaxNumber = x.TaxNumber,
        Phone = x.Phone,
        Email = x.Email,
        Website = x.Website,
        Address = x.Address,
        LogoUrl = x.LogoUrl,
        BaseCurrencyId = x.BaseCurrencyId,
        BaseCurrencyCode = x.BaseCurrency.Code,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc
    };

    public async Task<CompanyProfileDto> GetAsync(CancellationToken cancellationToken)
    {
        return await Db.CompanyProfiles
                   .AsNoTracking()
                   .OrderBy(x => x.Id)
                   .Select(ToDto)
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("لم يتم إدخال بيانات الشركة بعد. استخدم PUT /api/v1/company-profile.");
    }

    public async Task<CompanyProfileDto> SaveAsync(SaveCompanyProfileRequest request, CancellationToken cancellationToken)
    {
        var currencyId = request.BaseCurrencyId;
        var currencyIsActive = await Db.Currencies
            .AnyAsync(x => x.Id == currencyId && x.IsActive, cancellationToken);

        if (!currencyIsActive)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["baseCurrencyId"] = new[] { "العملة غير موجودة أو غير مفعّلة." }
            });
        }

        var profile = await Db.CompanyProfiles.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        var isNew = profile is null;
        profile ??= new CompanyProfile();

        profile.ArabicName = request.ArabicName.Trim();
        profile.EnglishName = Optional(request.EnglishName);
        profile.CommercialRegistrationNumber = Optional(request.CommercialRegistrationNumber);
        profile.TaxNumber = Optional(request.TaxNumber);
        profile.Phone = Optional(request.Phone);
        profile.Email = Optional(request.Email);
        profile.Website = Optional(request.Website);
        profile.Address = Optional(request.Address);
        profile.LogoUrl = Optional(request.LogoUrl);
        profile.BaseCurrencyId = request.BaseCurrencyId;

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            if (isNew)
            {
                Db.CompanyProfiles.Add(profile);
            }

            await Db.SaveChangesAsync(ct);

            _dependencies.AuditLog.Add(
                isNew ? AuditAction.MasterDataCreated : AuditAction.MasterDataUpdated,
                entityName: EntityName,
                entityId: profile.Id.ToString(),
                details: request);
            await Db.SaveChangesAsync(ct);

            return true;
        }, cancellationToken);

        return await GetAsync(cancellationToken);
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
