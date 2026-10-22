namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// "Seed" section of the configuration. Controls creation of roles, permissions and the FIRST administrator.
///
/// The administrator's credentials are NEVER stored in source code or appsettings.json. Set them with
/// environment variables before the first start:
///   Seed__AdminUserName, Seed__AdminEmail, Seed__AdminPassword
/// They are used only while no SystemAdmin user exists. After the first start you can (and should)
/// remove Seed__AdminPassword from the environment.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Set to false to skip seeding (e.g. a read-only replica).</summary>
    public bool Enabled { get; set; } = true;

    public string? AdminUserName { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public string AdminFullName { get; set; } = "System Administrator";

    public string AdminArabicFullName { get; set; } = "مدير النظام";

    /// <summary>
    /// Step 3: insert demo master data (company, 2 branches, cities, zones, 10 customers...) when the
    /// database has no branches yet. True in appsettings.Development.json, false in production.
    /// </summary>
    public bool DemoData { get; set; }

    /// <summary>
    /// Only used with <see cref="DemoData"/> = true: false seeds every demo record EXCEPT the 10 demo customers
    /// (so no demo shipments, invoices or pricing contract either, because those need the customers).
    /// Set Seed__DemoCustomers=false to start with the demo master data and your own customers.
    /// </summary>
    public bool DemoCustomers { get; set; } = true;

    /// <summary>
    /// Only used with <see cref="DemoData"/> = true: false = no demo money. The demo cashboxes are created with a zero
    /// opening balance and the demo journal entries (capital, rent, salaries, fuel bill) are not posted, so the
    /// financial reports start empty. Set Seed__DemoFinancials=false.
    /// </summary>
    public bool DemoFinancials { get; set; } = true;
}
