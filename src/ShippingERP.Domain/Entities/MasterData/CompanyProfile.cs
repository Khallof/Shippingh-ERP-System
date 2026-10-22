using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>
/// The company that owns the system (one row). Printed on invoices, waybills and reports.
/// </summary>
public class CompanyProfile : BaseEntity
{
    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    /// <summary>السجل التجاري</summary>
    public string? CommercialRegistrationNumber { get; set; }

    /// <summary>الرقم الضريبي</summary>
    public string? TaxNumber { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? Address { get; set; }

    public string? LogoUrl { get; set; }

    /// <summary>Step 9: the company logo (PNG or JPEG, max 1 MB), printed on every document and shown in the app.</summary>
    public byte[]? LogoImage { get; set; }

    /// <summary>image/png or image/jpeg.</summary>
    public string? LogoContentType { get; set; }

    /// <summary>When the logo was last changed (cache-busting for the browser).</summary>
    public DateTime? LogoUpdatedAtUtc { get; set; }

    /// <summary>The currency used for accounting and reports.</summary>
    public long BaseCurrencyId { get; set; }

    public Currency BaseCurrency { get; set; } = null!;
}
