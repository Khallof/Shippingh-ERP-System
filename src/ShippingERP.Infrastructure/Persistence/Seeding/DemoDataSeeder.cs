using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Identity;

namespace ShippingERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// Inserts realistic DEMO master data so the system can be tried immediately:
/// 1 company, 2 countries, 5 cities, 13 zones, 2 branches, 3 warehouses, 3 service types, 5 payment methods,
/// 3 currencies + 2 exchange rates, 3 tax rates, 7 expense categories, 1 pricing plan (its price rules are added by
/// DemoPricingSeeder, step 5),
/// 10 customers (each with an address), 2 suppliers, 3 employees, 2 drivers and 2 vehicles.
///
/// Runs only when Seed:DemoData = true (Development) AND the database has no branch yet, so it never
/// touches real data. All names, phone numbers and rates are fictional examples.
/// </summary>
public sealed class DemoDataSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SeedOptions _options;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        AppDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IOptions<SeedOptions> options,
        ILogger<DemoDataSeeder> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <param name="force">Ignore Seed:DemoData (used by tests).</param>
    public async Task SeedAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && (!_options.Enabled || !_options.DemoData))
        {
            return;
        }

        if (_dbContext.Database.IsRelational() &&
            (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            _logger.LogWarning("Demo data skipped: there are pending migrations");
            return;
        }

        if (await _dbContext.Branches.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Demo data skipped: branches already exist");
            return;
        }

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            await SeedAllAsync(ct);
            return true;
        }, cancellationToken);

        _logger.LogInformation("Demo master data created (2 branches, {Customers} customers, 3 employees, 2 drivers, 2 vehicles)", _options.DemoCustomers ? 10 : 0);
    }

    private async Task SeedAllAsync(CancellationToken ct)
    {
        var now = _dateTimeProvider.UtcNow;

        // ---------------- Currencies, rates, taxes ----------------
        var syp = new Currency { Code = "SYP", ArabicName = "ليرة سورية", EnglishName = "Syrian Pound", Symbol = "ل.س", DecimalPlaces = 0, IsBaseCurrency = true, IsActive = true };
        var usd = new Currency { Code = "USD", ArabicName = "دولار أمريكي", EnglishName = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true };
        var eur = new Currency { Code = "EUR", ArabicName = "يورو", EnglishName = "Euro", Symbol = "€", DecimalPlaces = 2, IsActive = true };
        _dbContext.Currencies.AddRange(syp, usd, eur);
        await _dbContext.SaveChangesAsync(ct);

        var rateDate = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.ExchangeRates.AddRange(
            new ExchangeRate { FromCurrencyId = usd.Id, ToCurrencyId = syp.Id, Rate = 13000m, EffectiveFromUtc = rateDate, Notes = "سعر تجريبي للعرض فقط", IsActive = true },
            new ExchangeRate { FromCurrencyId = eur.Id, ToCurrencyId = syp.Id, Rate = 14100m, EffectiveFromUtc = rateDate, Notes = "سعر تجريبي للعرض فقط", IsActive = true });

        _dbContext.TaxRates.AddRange(
            new TaxRate { Code = "EXEMPT", ArabicName = "معفى من الضريبة", EnglishName = "Exempt", Percentage = 0m, IsDefault = true, IsActive = true },
            new TaxRate { Code = "VAT5", ArabicName = "ضريبة 5%", EnglishName = "VAT 5%", Percentage = 5m, IsActive = true },
            new TaxRate { Code = "VAT10", ArabicName = "ضريبة 10%", EnglishName = "VAT 10%", Percentage = 10m, IsActive = true });

        // ---------------- Company ----------------
        _dbContext.CompanyProfiles.Add(new CompanyProfile
        {
            ArabicName = "شركة البرق للشحن السريع (بيانات تجريبية)",
            EnglishName = "Al-Barq Express Shipping (demo)",
            CommercialRegistrationNumber = "CR-000000",
            TaxNumber = "TX-000000",
            Phone = "+963 11 000 0000",
            Email = "info@albarq-express.example",
            Website = "https://albarq-express.example",
            Address = "دمشق - المزة - أوتوستراد المزة",
            BaseCurrencyId = syp.Id
        });

        // ---------------- Geography ----------------
        var syria = new Country { Code = "SY", ArabicName = "سوريا", EnglishName = "Syria", PhoneCode = "+963", IsActive = true };
        var lebanon = new Country { Code = "LB", ArabicName = "لبنان", EnglishName = "Lebanon", PhoneCode = "+961", IsActive = true };
        _dbContext.Countries.AddRange(syria, lebanon);
        await _dbContext.SaveChangesAsync(ct);

        var damascus = NewCity(syria, "DAM", "دمشق", "Damascus");
        var aleppo = NewCity(syria, "ALP", "حلب", "Aleppo");
        var homs = NewCity(syria, "HMS", "حمص", "Homs");
        var latakia = NewCity(syria, "LTK", "اللاذقية", "Latakia");
        var beirut = NewCity(lebanon, "BEY", "بيروت", "Beirut");
        _dbContext.Cities.AddRange(damascus, aleppo, homs, latakia, beirut);
        await _dbContext.SaveChangesAsync(ct);

        var mazzeh = NewZone(damascus, "MAZZEH", "المزة", "Mazzeh");
        var malki = NewZone(damascus, "MALKI", "المالكي", "Malki");
        var babTouma = NewZone(damascus, "BABTOUMA", "باب توما", "Bab Touma");
        var midan = NewZone(damascus, "MIDAN", "الميدان", "Midan");
        var kafrSousa = NewZone(damascus, "KAFRSOUSA", "كفرسوسة", "Kafr Sousa");
        var aziziyeh = NewZone(aleppo, "AZIZIYEH", "العزيزية", "Aziziyeh");
        var jamiliyeh = NewZone(aleppo, "JAMILIYEH", "الجميلية", "Jamiliyeh");
        var hamdaniyeh = NewZone(aleppo, "HAMDANIYEH", "الحمدانية", "Hamdaniyeh");
        var inshaat = NewZone(homs, "INSHAAT", "الإنشاءات", "Inshaat");
        var waer = NewZone(homs, "WAER", "الوعر", "Waer");
        var ziraa = NewZone(latakia, "ZIRAA", "الزراعة", "Ziraa");
        var sheikhDaher = NewZone(latakia, "SHEIKHDAHER", "الشيخ ضاهر", "Sheikh Daher");
        var hamra = NewZone(beirut, "HAMRA", "الحمرا", "Hamra");
        _dbContext.Zones.AddRange(mazzeh, malki, babTouma, midan, kafrSousa, aziziyeh, jamiliyeh, hamdaniyeh, inshaat, waer, ziraa, sheikhDaher, hamra);

        // ---------------- Branches & warehouses ----------------
        var damascusBranch = new Branch
        {
            Code = "DAM", ArabicName = "فرع دمشق", EnglishName = "Damascus Branch", CityId = damascus.Id,
            Address = "دمشق - المزة - شارع الجلاء", Phone = "+963 11 000 0001", IsHeadOffice = true, IsActive = true
        };
        var aleppoBranch = new Branch
        {
            Code = "ALP", ArabicName = "فرع حلب", EnglishName = "Aleppo Branch", CityId = aleppo.Id,
            Address = "حلب - العزيزية - شارع القوتلي", Phone = "+963 21 000 0002", IsActive = true
        };
        _dbContext.Branches.AddRange(damascusBranch, aleppoBranch);
        await _dbContext.SaveChangesAsync(ct);

        _dbContext.Warehouses.AddRange(
            new Warehouse { Code = "DAM-WH1", ArabicName = "مستودع دمشق الرئيسي", EnglishName = "Damascus Main Warehouse", BranchId = damascusBranch.Id, Address = "دمشق - القدم - المنطقة الصناعية", CapacityCubicMeters = 1200m, IsActive = true },
            new Warehouse { Code = "DAM-WH2", ArabicName = "مستودع المزة", EnglishName = "Mazzeh Warehouse", BranchId = damascusBranch.Id, Address = "دمشق - المزة جبل", CapacityCubicMeters = 300m, IsActive = true },
            new Warehouse { Code = "ALP-WH1", ArabicName = "مستودع حلب", EnglishName = "Aleppo Warehouse", BranchId = aleppoBranch.Id, Address = "حلب - الشيخ نجار - المدينة الصناعية", CapacityCubicMeters = 800m, IsActive = true });

        // ---------------- Lookups ----------------
        var standard = new ServiceType { Code = "STD", ArabicName = "توصيل عادي", EnglishName = "Standard", EstimatedDeliveryDays = 3, SortOrder = 1, IsActive = true };
        var express = new ServiceType { Code = "EXP", ArabicName = "توصيل سريع", EnglishName = "Express", EstimatedDeliveryDays = 1, SortOrder = 2, IsActive = true };
        var sameDay = new ServiceType { Code = "SDD", ArabicName = "توصيل في نفس اليوم", EnglishName = "Same day", EstimatedDeliveryDays = 0, SortOrder = 3, IsActive = true };
        _dbContext.ServiceTypes.AddRange(standard, express, sameDay);

        _dbContext.PaymentMethods.AddRange(
            new PaymentMethod { Code = "CASH", ArabicName = "نقداً", EnglishName = "Cash", Kind = PaymentMethodKind.Cash, SortOrder = 1, IsActive = true },
            new PaymentMethod { Code = "COD", ArabicName = "الدفع عند الاستلام", EnglishName = "Cash on delivery", Kind = PaymentMethodKind.CashOnDelivery, SortOrder = 2, IsActive = true },
            new PaymentMethod { Code = "BANK", ArabicName = "تحويل بنكي", EnglishName = "Bank transfer", Kind = PaymentMethodKind.BankTransfer, RequiresReference = true, SortOrder = 3, IsActive = true },
            new PaymentMethod { Code = "CARD", ArabicName = "بطاقة", EnglishName = "Card", Kind = PaymentMethodKind.Card, RequiresReference = true, SortOrder = 4, IsActive = true },
            new PaymentMethod { Code = "ACCOUNT", ArabicName = "على حساب العميل", EnglishName = "Customer account", Kind = PaymentMethodKind.CustomerAccount, SortOrder = 5, IsActive = true });

        _dbContext.ExpenseCategories.AddRange(
            new ExpenseCategory { Code = "FUEL", ArabicName = "وقود", EnglishName = "Fuel", IsActive = true },
            new ExpenseCategory { Code = "MAINT", ArabicName = "صيانة مركبات", EnglishName = "Vehicle maintenance", IsActive = true },
            new ExpenseCategory { Code = "SALARY", ArabicName = "رواتب وأجور", EnglishName = "Salaries", IsActive = true },
            new ExpenseCategory { Code = "RENT", ArabicName = "إيجارات", EnglishName = "Rent", IsActive = true },
            new ExpenseCategory { Code = "UTIL", ArabicName = "كهرباء ومياه واتصالات", EnglishName = "Utilities", IsActive = true },
            new ExpenseCategory { Code = "OFFICE", ArabicName = "قرطاسية ومستلزمات مكتبية", EnglishName = "Office supplies", IsActive = true },
            new ExpenseCategory { Code = "TOLLS", ArabicName = "رسوم طرق ومواقف", EnglishName = "Tolls and parking", IsActive = true });

        _dbContext.Suppliers.AddRange(
            new Supplier { Code = "SUP-001", ArabicName = "محطة وقود الربوة (تجريبي)", Phone = "+963 11 000 0101", ContactPerson = "أبو سليم", IsActive = true },
            new Supplier { Code = "SUP-002", ArabicName = "مركز الأمانة لصيانة السيارات (تجريبي)", Phone = "+963 11 000 0102", ContactPerson = "م. فادي", IsActive = true });
        await _dbContext.SaveChangesAsync(ct);

        // ---------------- Pricing ----------------
        var generalPlan = new CustomerPricingPlan
        {
            Code = "GENERAL", ArabicName = "الأسعار العامة", EnglishName = "General prices",
            CurrencyId = syp.Id, IsDefault = true, ValidFromUtc = rateDate, IsActive = true
        };
        _dbContext.CustomerPricingPlans.Add(generalPlan);
        await _dbContext.SaveChangesAsync(ct);

        // Seed:DemoCustomers = false skips the demo customers; the shipment and contract demo seeders then skip too.
        if (_options.DemoCustomers)
        {
            // ---------------- Customers (each with one default address) ----------------
            var customers = new (string Name, CustomerType Type, CustomerRole Role, Branch Branch, string Phone, decimal Credit, City City, Zone Zone, string Address)[]
            {
                ("أحمد محمود الخطيب", CustomerType.Individual, CustomerRole.Both, damascusBranch, "+963944000101", 0m, damascus, mazzeh, "شارع الشيخ سعد، بناء 12"),
                ("سارة يوسف العلي", CustomerType.Individual, CustomerRole.Recipient, damascusBranch, "+963944000102", 0m, damascus, malki, "جادة الرئيس، بناء 4"),
                ("شركة الياسمين للتجارة", CustomerType.Business, CustomerRole.Sender, damascusBranch, "+963113000103", 500000m, damascus, babTouma, "شارع باب توما الرئيسي، محل 7"),
                ("محمد خالد الحلبي", CustomerType.Individual, CustomerRole.Both, aleppoBranch, "+963933000104", 0m, aleppo, aziziyeh, "شارع فيصل، بناء 20"),
                ("مؤسسة الشهباء للألبسة", CustomerType.Business, CustomerRole.Sender, aleppoBranch, "+963212000105", 750000m, aleppo, jamiliyeh, "سوق الجميلية، محل 15"),
                ("ريم عبد الله حداد", CustomerType.Individual, CustomerRole.Recipient, aleppoBranch, "+963933000106", 0m, aleppo, hamdaniyeh, "الحمدانية، الحي الثالث، بناء 9"),
                ("متجر النور الإلكتروني", CustomerType.Business, CustomerRole.Both, damascusBranch, "+963944000107", 1000000m, damascus, kafrSousa, "كفرسوسة، مجمع الأمويين، مكتب 3"),
                ("خالد إبراهيم السوري", CustomerType.Individual, CustomerRole.Recipient, damascusBranch, "+963955000108", 0m, homs, inshaat, "الإنشاءات، شارع البرازيل، بناء 5"),
                ("ليلى حسن مراد", CustomerType.Individual, CustomerRole.Recipient, aleppoBranch, "+963955000109", 0m, latakia, ziraa, "حي الزراعة، قرب الجامعة، بناء 11"),
                ("شركة المتوسط للصيدليات", CustomerType.Business, CustomerRole.Both, damascusBranch, "+963113000110", 2000000m, damascus, midan, "الميدان، شارع الحقلة، بناء 2")
            };

            for (var i = 0; i < customers.Length; i++)
            {
                var data = customers[i];
                var customer = new Customer
                {
                    Code = "CUS-" + (i + 1).ToString("D5", CultureInfo.InvariantCulture),
                    CustomerType = data.Type,
                    Roles = data.Role,
                    ArabicName = data.Name,
                    Phone = data.Phone,
                    CreditLimit = data.Credit,
                    DefaultBranchId = data.Branch.Id,
                    Notes = "عميل تجريبي",
                    IsActive = true
                };

                customer.Addresses.Add(new CustomerAddress
                {
                    Label = data.Type == CustomerType.Business ? "مقر الشركة" : "المنزل",
                    CityId = data.City.Id,
                    ZoneId = data.Zone.Id,
                    AddressLine = data.Address,
                    ContactName = data.Name,
                    Phone = data.Phone,
                    IsDefault = true,
                    IsActive = true
                });

                _dbContext.Customers.Add(customer);
            }
        }

        // ---------------- Employees, vehicles, drivers ----------------
        var manager = new Employee { Code = "EMP-001", ArabicName = "سامر نعيم", EnglishName = "Samer Naeem", BranchId = damascusBranch.Id, JobTitle = "مدير فرع", Phone = "+963944100001", HireDateUtc = rateDate.AddYears(-5), IsActive = true };
        var driverDamascus = new Employee { Code = "EMP-002", ArabicName = "علي حسين", EnglishName = "Ali Hussein", BranchId = damascusBranch.Id, JobTitle = "سائق", Phone = "+963944100002", HireDateUtc = rateDate.AddYears(-2), IsActive = true };
        var driverAleppo = new Employee { Code = "EMP-003", ArabicName = "عمر يوسف", EnglishName = "Omar Youssef", BranchId = aleppoBranch.Id, JobTitle = "سائق", Phone = "+963933100003", HireDateUtc = rateDate.AddYears(-1), IsActive = true };
        _dbContext.Employees.AddRange(manager, driverDamascus, driverAleppo);

        var van = new Vehicle { PlateNumber = "DAM 123-456", Model = "Hyundai H100", VehicleType = VehicleType.Van, CapacityKg = 1000m, BranchId = damascusBranch.Id, IsActive = true };
        var motorcycle = new Vehicle { PlateNumber = "ALP 654-321", Model = "Honda CG 150", VehicleType = VehicleType.Motorcycle, CapacityKg = 150m, BranchId = aleppoBranch.Id, IsActive = true };
        _dbContext.Vehicles.AddRange(van, motorcycle);
        await _dbContext.SaveChangesAsync(ct);

        _dbContext.DriverProfiles.AddRange(
            new DriverProfile { EmployeeId = driverDamascus.Id, LicenseNumber = "DL-100200", LicenseCategory = "C", LicenseExpiryDateUtc = rateDate.AddYears(2), DefaultVehicleId = van.Id, IsActive = true },
            new DriverProfile { EmployeeId = driverAleppo.Id, LicenseNumber = "DL-100300", LicenseCategory = "A", LicenseExpiryDateUtc = rateDate.AddDays(20), DefaultVehicleId = motorcycle.Id, Notes = "الرخصة تنتهي قريباً (لتجربة التنبيه)", IsActive = true });
        await _dbContext.SaveChangesAsync(ct);
    }

    private static City NewCity(Country country, string code, string arabicName, string englishName) =>
        new() { CountryId = country.Id, Code = code, ArabicName = arabicName, EnglishName = englishName, IsActive = true };

    private static Zone NewZone(City city, string code, string arabicName, string englishName) =>
        new() { CityId = city.Id, Code = code, ArabicName = arabicName, EnglishName = englishName, IsActive = true };
}
