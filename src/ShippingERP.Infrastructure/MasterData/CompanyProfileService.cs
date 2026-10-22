using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
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
        HasLogo = x.LogoImage != null,
        LogoVersion = x.LogoUpdatedAtUtc.HasValue ? x.LogoUpdatedAtUtc.Value.Ticks : 0,
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

    // ------------------------------------------------------------------ logo (step 9)

    public const long MaxLogoBytes = 1024 * 1024;

    public async Task<CompanyProfileDto> SetLogoAsync(UploadedFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > MaxLogoBytes)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["file"] = new[] { "حجم الشعار يجب أن يكون بين 1 بايت و 1 ميغابايت. صغّر الصورة ثم أعد المحاولة." }
            });
        }

        byte[] bytes;
        await using (var stream = file.OpenReadStream())
        using (var memory = new MemoryStream())
        {
            await stream.CopyToAsync(memory, cancellationToken);
            bytes = memory.ToArray();
        }

        // The real type comes from the first bytes, never from the name / content type sent by the client.
        var contentType = DetectImageType(bytes)
                          ?? throw new RequestValidationException(new Dictionary<string, string[]>
                          {
                              ["file"] = new[] { "الشعار يجب أن يكون صورة PNG أو JPG." }
                          });

        var profile = await Db.CompanyProfiles.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken)
                      ?? throw new BusinessRuleException("احفظ بيانات الشركة أولاً، ثم ارفع الشعار.");

        profile.LogoImage = bytes;
        profile.LogoContentType = contentType;
        profile.LogoUpdatedAtUtc = _dependencies.DateTimeProvider.UtcNow;

        _dependencies.AuditLog.Add(
            AuditAction.MasterDataUpdated,
            entityName: EntityName,
            entityId: profile.Id.ToString(),
            details: new { logo = "uploaded", contentType, bytes.Length });
        await Db.SaveChangesAsync(cancellationToken);

        return await GetAsync(cancellationToken);
    }

    public async Task<CompanyProfileDto> RemoveLogoAsync(CancellationToken cancellationToken)
    {
        var profile = await Db.CompanyProfiles.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken)
                      ?? throw new NotFoundException("لم يتم إدخال بيانات الشركة بعد.");

        profile.LogoImage = null;
        profile.LogoContentType = null;
        profile.LogoUpdatedAtUtc = _dependencies.DateTimeProvider.UtcNow;

        _dependencies.AuditLog.Add(
            AuditAction.MasterDataUpdated,
            entityName: EntityName,
            entityId: profile.Id.ToString(),
            details: new { logo = "removed" });
        await Db.SaveChangesAsync(cancellationToken);

        return await GetAsync(cancellationToken);
    }

    public async Task<FileDownload?> GetLogoAsync(CancellationToken cancellationToken)
    {
        var logo = await Db.CompanyProfiles.AsNoTracking()
            .OrderBy(x => x.Id)
            .Where(x => x.LogoImage != null)
            .Select(x => new { x.LogoImage, x.LogoContentType })
            .FirstOrDefaultAsync(cancellationToken);

        if (logo?.LogoImage is null)
        {
            return null;
        }

        var contentType = logo.LogoContentType ?? "image/png";
        return FileDownload.FromBytes(logo.LogoImage, contentType, contentType == "image/jpeg" ? "logo.jpg" : "logo.png");
    }

    /// <summary>PNG or JPEG by their signatures; null for anything else.</summary>
    internal static string? DetectImageType(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        return null;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
