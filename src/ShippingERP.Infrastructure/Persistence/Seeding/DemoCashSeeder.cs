using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Infrastructure.Identity;

namespace ShippingERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// Demo cash setup (step 6), only with Seed:DemoData = true, the demo branches present and no cashbox yet:
///   DAM-SYP-01  صندوق دمشق الرئيسي   (SYP, opening 500,000)
///   DAM-USD-01  صندوق دمشق بالدولار  (USD, opening 0)
///   ALP-SYP-01  صندوق حلب الرئيسي    (SYP, opening 250,000)
///   BNK-CBS-SYP المصرف التجاري السوري (bank account, SYP)
/// It uses the real services, so the opening balances have their cashbox transactions and accounting events.
/// </summary>
public sealed class DemoCashSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly ICashboxService _cashboxes;
    private readonly IBankAccountService _bankAccounts;
    private readonly SeedOptions _options;
    private readonly ILogger<DemoCashSeeder> _logger;

    public DemoCashSeeder(
        AppDbContext dbContext,
        ICashboxService cashboxes,
        IBankAccountService bankAccounts,
        IOptions<SeedOptions> options,
        ILogger<DemoCashSeeder> logger)
    {
        _dbContext = dbContext;
        _cashboxes = cashboxes;
        _bankAccounts = bankAccounts;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> SeedAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && (!_options.Enabled || !_options.DemoData))
        {
            return 0;
        }

        if (_dbContext.Database.IsRelational() &&
            (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            _logger.LogWarning("Demo cashboxes skipped: there are pending migrations");
            return 0;
        }

        if (await _dbContext.Cashboxes.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Demo cashboxes skipped: cashboxes already exist");
            return 0;
        }

        var damascus = await _dbContext.Branches.AsNoTracking().Where(x => x.Code == "DAM").Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        var aleppo = await _dbContext.Branches.AsNoTracking().Where(x => x.Code == "ALP").Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        var syp = await _dbContext.Currencies.AsNoTracking().Where(x => x.Code == "SYP").Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        var usd = await _dbContext.Currencies.AsNoTracking().Where(x => x.Code == "USD").Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);

        if (damascus is null || aleppo is null || syp is null || usd is null)
        {
            _logger.LogDebug("Demo cashboxes skipped: the demo branches / currencies are missing");
            return 0;
        }

        await _cashboxes.CreateAsync(new CreateCashboxRequest
        {
            Code = "DAM-SYP-01", ArabicName = "صندوق دمشق الرئيسي", EnglishName = "Damascus main cashbox",
            BranchId = damascus.Value, CurrencyId = syp.Value, OpeningBalance = 500_000m
        }, cancellationToken);

        await _cashboxes.CreateAsync(new CreateCashboxRequest
        {
            Code = "DAM-USD-01", ArabicName = "صندوق دمشق بالدولار", EnglishName = "Damascus USD cashbox",
            BranchId = damascus.Value, CurrencyId = usd.Value, OpeningBalance = 0m
        }, cancellationToken);

        await _cashboxes.CreateAsync(new CreateCashboxRequest
        {
            Code = "ALP-SYP-01", ArabicName = "صندوق حلب الرئيسي", EnglishName = "Aleppo main cashbox",
            BranchId = aleppo.Value, CurrencyId = syp.Value, OpeningBalance = 250_000m
        }, cancellationToken);

        await _bankAccounts.CreateAsync(new SaveBankAccountRequest
        {
            Code = "BNK-CBS-SYP", ArabicName = "حساب الشركة - المصرف التجاري السوري", BankName = "المصرف التجاري السوري",
            AccountNumber = "0101-000123456", CurrencyId = syp.Value, IsActive = true, Notes = "حساب تجريبي"
        }, cancellationToken);

        _logger.LogInformation("Demo cashboxes created (3) and 1 bank account");
        return 4;
    }
}
