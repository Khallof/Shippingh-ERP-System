using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Accounting;
using ShippingERP.Infrastructure.Identity;

namespace ShippingERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// Demo accounting data (step 7), only with Seed:DemoData = true, the chart of accounts present and no manual journal
/// entry yet:
///   1. posts every operational event waiting in the outbox (demo cashboxes, invoices, COD...) so the reports show
///      real figures at once;
///   2. creates and posts, through the real workflow (create -> submit -> approve -> post), four manual entries:
///      the owner's capital (bank + vehicles), the office rent paid by bank, a fuel supplier bill (supplier aging) and
///      the month's salaries (accrued);
///   3. leaves one bank-charges entry Submitted, to try the approval in Swagger.
/// </summary>
public sealed class DemoAccountingSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly IJournalEntryService _journal;
    private readonly IAccountingPostingService _postings;
    private readonly AccountingAccess _access;
    private readonly SeedOptions _options;
    private readonly ILogger<DemoAccountingSeeder> _logger;

    public DemoAccountingSeeder(
        AppDbContext dbContext,
        IJournalEntryService journal,
        IAccountingPostingService postings,
        AccountingAccess access,
        IOptions<SeedOptions> options,
        ILogger<DemoAccountingSeeder> logger)
    {
        _dbContext = dbContext;
        _journal = journal;
        _postings = postings;
        _access = access;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> SeedAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && (!_options.Enabled || !_options.DemoData || !_options.DemoFinancials))
        {
            return 0;
        }

        if (_dbContext.Database.IsRelational() &&
            (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            _logger.LogWarning("Demo journal entries skipped: there are pending migrations");
            return 0;
        }

        if (!await _dbContext.Accounts.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Demo journal entries skipped: no chart of accounts");
            return 0;
        }

        if (await _dbContext.JournalEntries.AnyAsync(x => !x.IsSystemGenerated, cancellationToken))
        {
            _logger.LogDebug("Demo journal entries skipped: manual entries already exist");
            return 0;
        }

        var processed = await _postings.ProcessPendingAsync(1000, cancellationToken);
        _logger.LogInformation(
            "Pending operational events posted: {Posted} posted, {Skipped} without accounting effect, {Failed} failed",
            processed.Posted, processed.Skipped, processed.Failed);

        var accounts = await _dbContext.Accounts.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
        string[] needed = { "1103", "1201", "3100", "6202", "6101", "2101", "6201", "2105", "6205" };
        if (needed.Any(code => !accounts.ContainsKey(code)))
        {
            _logger.LogDebug("Demo journal entries skipped: the default chart of accounts was changed");
            return 0;
        }

        var bankId = await _dbContext.BankAccounts.AsNoTracking().Where(x => x.Code == "BNK-CBS-SYP").Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        var supplierId = await _dbContext.Suppliers.AsNoTracking().OrderBy(x => x.Code).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        var costCenters = await _dbContext.AccountingDimensions.AsNoTracking()
            .Where(x => x.DimensionType == DimensionType.CostCenter)
            .ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
        var branchId = await _dbContext.Branches.AsNoTracking().Where(x => x.Code == "DAM").Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken)
                       ?? await _access.DefaultBranchIdAsync(cancellationToken);

        var today = _access.Today;
        var yearStart = await _access.FiscalYearStartAsync(today, cancellationToken);
        long? Center(string code) => costCenters.TryGetValue(code, out var id) ? id : null;

        var entries = new List<SaveJournalEntryRequest>
        {
            new()
            {
                EntryDate = yearStart,
                JournalType = JournalType.Opening,
                BranchId = branchId,
                Description = "قيد افتتاحي: رأس مال المالك (رصيد البنك والمركبات)",
                Reference = "OPEN-DEMO",
                Lines = new List<JournalLineRequest>
                {
                    new() { AccountId = accounts["1103"], Debit = 10_000_000m, BankAccountId = bankId, Description = "رصيد الحساب البنكي" },
                    new() { AccountId = accounts["1201"], Debit = 25_000_000m, Description = "سيارات التوزيع" },
                    new() { AccountId = accounts["3100"], Credit = 35_000_000m, Description = "رأس مال المالك" }
                }
            },
            new()
            {
                EntryDate = today,
                BranchId = branchId,
                Description = "إيجار مكتب دمشق لهذا الشهر - تحويل بنكي",
                Reference = "RENT-DEMO",
                Lines = new List<JournalLineRequest>
                {
                    new() { AccountId = accounts["6202"], Debit = 1_200_000m, CostCenterId = Center("ADMIN") },
                    new() { AccountId = accounts["1103"], Credit = 1_200_000m, BankAccountId = bankId }
                }
            },
            new()
            {
                EntryDate = today,
                BranchId = branchId,
                Description = "فاتورة وقود آجلة من المورد",
                Reference = "FUEL-INV-DEMO",
                Lines = new List<JournalLineRequest>
                {
                    new() { AccountId = accounts["6101"], Debit = 450_000m, CostCenterId = Center("FLEET") },
                    new() { AccountId = accounts["2101"], Credit = 450_000m, SupplierId = supplierId }
                }
            },
            new()
            {
                EntryDate = today,
                BranchId = branchId,
                JournalType = JournalType.Adjustment,
                Description = "استحقاق رواتب الشهر",
                Reference = "SAL-DEMO",
                Lines = new List<JournalLineRequest>
                {
                    new() { AccountId = accounts["6201"], Debit = 3_000_000m, CostCenterId = Center("OPS") },
                    new() { AccountId = accounts["2105"], Credit = 3_000_000m }
                }
            }
        };

        var count = 0;
        foreach (var request in entries)
        {
            request.Submit = true;
            var created = await _journal.CreateAsync(request, null, cancellationToken);
            await _journal.ApproveAsync(created.Entry.Id, new JournalDecisionRequest { Notes = "بيانات تجريبية" }, cancellationToken);
            await _journal.PostAsync(created.Entry.Id, cancellationToken);
            count++;
        }

        await _journal.CreateAsync(new SaveJournalEntryRequest
        {
            EntryDate = today,
            BranchId = branchId,
            JournalType = JournalType.Bank,
            Description = "عمولة تحويل بنكي (بانتظار الاعتماد)",
            Reference = "BANK-FEE-DEMO",
            Submit = true,
            Lines = new List<JournalLineRequest>
            {
                new() { AccountId = accounts["6205"], Debit = 15_000m, CostCenterId = Center("ADMIN") },
                new() { AccountId = accounts["1103"], Credit = 15_000m, BankAccountId = bankId }
            }
        }, null, cancellationToken);
        count++;

        _logger.LogInformation("Demo journal entries created ({Count}): 4 posted, 1 waiting for approval", count);
        return count;
    }
}
