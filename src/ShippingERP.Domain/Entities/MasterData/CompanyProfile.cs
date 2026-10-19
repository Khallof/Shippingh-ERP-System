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

    /// <summary>The currency used for accounting and reports.</summary>
    public long BaseCurrencyId { get; set; }

    public Currency BaseCurrency { get; set; } = null!;
}
