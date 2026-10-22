namespace ShippingERP.Application.DTOs.MasterData;

public sealed class CompanyProfileDto
{
    public long Id { get; init; }
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public string? CommercialRegistrationNumber { get; init; }
    public string? TaxNumber { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? Address { get; init; }
    public string? LogoUrl { get; init; }

    /// <summary>Step 9: a logo was uploaded (GET /api/v1/company-profile/logo returns it).</summary>
    public bool HasLogo { get; init; }

    /// <summary>Changes when the logo changes; append it to the logo URL so the browser reloads it.</summary>
    public long LogoVersion { get; init; }
    public long BaseCurrencyId { get; init; }
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

/// <summary>Body of PUT /api/v1/company-profile (creates the profile the first time, updates it afterwards).</summary>
public sealed class SaveCompanyProfileRequest
{
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public string? CommercialRegistrationNumber { get; set; }
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? LogoUrl { get; set; }
    public long BaseCurrencyId { get; set; }
}
