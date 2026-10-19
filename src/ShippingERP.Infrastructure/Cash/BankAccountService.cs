using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Cash;

/// <summary>
/// Company bank accounts (step 6): the destinations of cash deposits and the source of bank settlements.
/// Step 7: each one can be linked to its own general ledger account and is reconciled with the bank statement.
/// </summary>
public sealed class BankAccountService : IBankAccountService
{
    private readonly MasterDataServiceDependencies _dependencies;

    public BankAccountService(MasterDataServiceDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    private AppDbContext Db => _dependencies.DbContext;

    public async Task<PagedResult<BankAccountDto>> GetPagedAsync(BankAccountListQuery query, CancellationToken cancellationToken)
    {
        var rows = Db.BankAccounts.AsNoTracking();

        if (query.CurrencyId.HasValue)
        {
            var currencyId = query.CurrencyId.Value;
            rows = rows.Where(x => x.CurrencyId == currencyId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == null || x.BranchId == branchId);
        }

        if (query.IsActive.HasValue)
        {
            var isActive = query.IsActive.Value;
            rows = rows.Where(x => x.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Code.Contains(search) || x.ArabicName.Contains(search) || x.BankName.Contains(search) || x.AccountNumber.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderBy(x => x.Code)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<BankAccountDto>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<BankAccountDto> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        await Db.BankAccounts.AsNoTracking().Where(x => x.Id == id).Select(Projection).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("الحساب البنكي", id);

    public async Task<BankAccountDto> CreateAsync(SaveBankAccountRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await Db.BankAccounts.IgnoreQueryFilters().AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new ConflictException($"رمز الحساب البنكي {code} مستخدم من قبل.");
        }

        await ValidateAsync(request, cancellationToken);

        var account = new BankAccount { Code = code };
        Apply(account, request);
        Db.BankAccounts.Add(account);
        await Db.SaveChangesAsync(cancellationToken);

        Audit(account);
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(account.Id, cancellationToken);
    }

    public async Task<BankAccountDto> UpdateAsync(long id, SaveBankAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await Db.BankAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                      ?? throw new NotFoundException("الحساب البنكي", id);

        var code = request.Code.Trim().ToUpperInvariant();
        if (code != account.Code && await Db.BankAccounts.IgnoreQueryFilters().AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new ConflictException($"رمز الحساب البنكي {code} مستخدم من قبل.");
        }

        await ValidateAsync(request, cancellationToken);

        if (request.CurrencyId != account.CurrencyId)
        {
            var used = await Db.CashTransfers.AnyAsync(x => x.ToBankAccountId == id, cancellationToken) ||
                       await Db.SenderSettlements.AnyAsync(x => x.BankAccountId == id, cancellationToken);
            if (used)
            {
                throw new BusinessRuleException("لا يمكن تغيير عملة حساب بنكي استُخدم في تحويلات أو تسويات.");
            }
        }

        if (request.AccountId != account.AccountId && account.AccountId.HasValue &&
            await Db.JournalEntryLines.AnyAsync(x => x.BankAccountId == id, cancellationToken))
        {
            throw new BusinessRuleException("لا يمكن تغيير حساب الأستاذ لحساب بنكي له قيود مرحّلة؛ أنشئ قيد تسوية لنقل الرصيد أولاً.");
        }

        account.Code = code;
        Apply(account, request);
        Audit(account);
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var account = await Db.BankAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                      ?? throw new NotFoundException("الحساب البنكي", id);

        var used = await Db.CashTransfers.AnyAsync(x => x.ToBankAccountId == id, cancellationToken) ||
                   await Db.SenderSettlements.AnyAsync(x => x.BankAccountId == id, cancellationToken) ||
                   await Db.JournalEntryLines.AnyAsync(x => x.BankAccountId == id, cancellationToken) ||
                   await Db.BankTransactions.AnyAsync(x => x.BankAccountId == id, cancellationToken) ||
                   await Db.AccountingPostingRules.AnyAsync(x => x.BankAccountId == id, cancellationToken);
        if (used)
        {
            throw new BusinessRuleException("لا يمكن حذف حساب بنكي له حركات أو قيود أو قواعد ترحيل؛ أوقف تفعيله بدلاً من ذلك.");
        }

        Db.BankAccounts.Remove(account);
        Audit(account);
        await Db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateAsync(SaveBankAccountRequest request, CancellationToken cancellationToken)
    {
        if (!await Db.Currencies.AsNoTracking().AnyAsync(x => x.Id == request.CurrencyId && x.IsActive, cancellationToken))
        {
            throw CashAccess.Invalid("currencyId", "العملة غير موجودة أو غير مفعّلة.");
        }

        if (request.BranchId.HasValue && !await Db.Branches.AsNoTracking().AnyAsync(x => x.Id == request.BranchId.Value, cancellationToken))
        {
            throw CashAccess.Invalid("branchId", "الفرع غير موجود.");
        }

        if (request.AccountId.HasValue)
        {
            var accountId = request.AccountId.Value;
            var glAccount = await Db.Accounts.AsNoTracking()
                .Where(x => x.Id == accountId)
                .Select(x => new { x.IsPostable, x.IsActive, x.AccountType, x.CurrencyId })
                .FirstOrDefaultAsync(cancellationToken);

            if (glAccount is null || !glAccount.IsActive || !glAccount.IsPostable || glAccount.AccountType != AccountType.Assets)
            {
                throw CashAccess.Invalid("accountId", "حساب الأستاذ يجب أن يكون حساب أصول فرعياً مفعّلاً.");
            }

            if (glAccount.CurrencyId.HasValue && glAccount.CurrencyId != request.CurrencyId)
            {
                throw CashAccess.Invalid("accountId", "حساب الأستاذ مقيّد بعملة أخرى.");
            }
        }
    }

    private static void Apply(BankAccount account, SaveBankAccountRequest request)
    {
        account.ArabicName = request.ArabicName.Trim();
        account.BankName = request.BankName.Trim();
        account.AccountNumber = request.AccountNumber.Trim();
        account.Iban = string.IsNullOrWhiteSpace(request.Iban) ? null : request.Iban.Trim().ToUpperInvariant();
        account.CurrencyId = request.CurrencyId;
        account.BranchId = request.BranchId;
        account.IsActive = request.IsActive;
        account.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        account.AccountId = request.AccountId;
    }

    private void Audit(BankAccount account) =>
        _dependencies.AuditLog.Add(AuditAction.BankAccountSaved, entityName: "BankAccount",
            entityId: account.Id.ToString(CultureInfo.InvariantCulture),
            details: new { account.Code, account.BankName, account.CurrencyId, account.IsActive });

    private static readonly Expression<Func<BankAccount, BankAccountDto>> Projection = x => new BankAccountDto
    {
        Id = x.Id,
        Code = x.Code,
        ArabicName = x.ArabicName,
        BankName = x.BankName,
        AccountNumber = x.AccountNumber,
        Iban = x.Iban,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        BranchId = x.BranchId,
        BranchName = x.Branch != null ? x.Branch.ArabicName : null,
        IsActive = x.IsActive,
        Notes = x.Notes,
        CreatedAtUtc = x.CreatedAtUtc,
        AccountId = x.AccountId,
        AccountCode = x.Account != null ? x.Account.Code : null,
        AccountName = x.Account != null ? x.Account.ArabicName : null
    };
}
