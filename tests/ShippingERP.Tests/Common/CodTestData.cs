using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Infrastructure.Persistence.Seeding;

namespace ShippingERP.Tests.Common;

/// <summary>
/// Helpers of the COD and cash tests (step 6). Demo prices: a 2.5 kg standard shipment with COD 100,000 costs
/// 15,000 + COD commission (2,000 + 1% = 3,000) = 18,000, tax 0 %, so the sender's net is 82,000.
/// </summary>
public static class CodTestData
{
    public const decimal Cod = 100_000m;
    public const decimal Charges = 18_000m;
    public const decimal Net = Cod - Charges;

    /// <summary>Demo master data + price rules + the demo cashboxes (DAM-SYP-01 500,000; DAM-USD-01; ALP-SYP-01 250,000) and bank account.</summary>
    public static async Task<DemoIds> SeedAsync(IdentityTestContext context)
    {
        await context.SeedDemoDataAsync();
        await context.UseAsync<DemoCashSeeder, int>(s => s.SeedAsync(force: true));
        return await ShipmentTestData.LoadIdsAsync(context);
    }

    public static Task<long> CashboxIdAsync(IdentityTestContext context, string code = "DAM-SYP-01") =>
        context.QueryAsync(db => db.Cashboxes.Where(x => x.Code == code).Select(x => x.Id).SingleAsync());

    public static Task<decimal> CashboxBalanceAsync(IdentityTestContext context, string code = "DAM-SYP-01") =>
        context.QueryAsync(db => db.Cashboxes.Where(x => x.Code == code).Select(x => x.CurrentBalance).SingleAsync());

    public static Task<long> BankAccountIdAsync(IdentityTestContext context) =>
        context.QueryAsync(db => db.BankAccounts.Where(x => x.Code == "BNK-CBS-SYP").Select(x => x.Id).SingleAsync());

    public static Task<decimal> DriverBalanceAsync(IdentityTestContext context, long driverId) =>
        context.QueryAsync(async db => await db.DriverCashAccounts.Where(x => x.DriverId == driverId).Select(x => (decimal?)x.Balance).SingleOrDefaultAsync() ?? 0m);

    public static Task<long> CodIdAsync(IdentityTestContext context, long shipmentId) =>
        context.QueryAsync(db => db.CODTransactions.Where(x => x.ShipmentId == shipmentId).Select(x => x.Id).SingleAsync());

    public static Task<CodTransactionDto> GetCodAsync(IdentityTestContext context, long codId) =>
        context.UseAsync<ICodService, CodTransactionDto>(s => s.GetByIdAsync(codId, CancellationToken.None));

    /// <summary>A COD shipment received at the Damascus branch (ready for a driver manifest).</summary>
    public static async Task<ShipmentDto> CodShipmentAtBranchAsync(IdentityTestContext context, DemoIds ids, decimal cod = Cod)
    {
        var shipment = await ShipmentTestData.CreateAsync(context, ShipmentTestData.NewRequest(ids, codAmount: cod));
        return await ShipmentTestData.ReceiveAtBranchAsync(context, shipment.Id);
    }

    /// <summary>Puts the shipments on a dispatched manifest of the driver (they become OutForDelivery).</summary>
    public static async Task<long> DispatchAsync(IdentityTestContext context, long driverId, params ShipmentDto[] shipments)
    {
        var manifest = await context.UseAsync<IDriverManifestService, DriverManifestDto>(s => s.CreateAsync(
            new CreateDriverManifestRequest { DriverId = driverId, Codes = shipments.Select(x => x.TrackingNumber).ToList() },
            CancellationToken.None));

        await context.UseAsync<IDriverManifestService, DriverManifestDto>(s => s.DispatchAsync(manifest.Id, CancellationToken.None));
        return manifest.Id;
    }

    public static Task<ProofOfDeliveryDto> DeliverAsync(IdentityTestContext context, long shipmentId) =>
        context.UseAsync<IDeliveryService, ProofOfDeliveryDto>(s => s.DeliverAsync(
            shipmentId, new DeliverShipmentRequest { RecipientName = "سارة" }, null, null, CancellationToken.None));

    /// <summary>A COD shipment delivered by the driver (COD status: AssignedToDriver, not collected yet).</summary>
    public static async Task<ShipmentDto> DeliveredCodShipmentAsync(IdentityTestContext context, DemoIds ids, decimal cod = Cod, long? driverId = null)
    {
        var shipment = await CodShipmentAtBranchAsync(context, ids, cod);
        await DispatchAsync(context, driverId ?? ids.DamascusDriver, shipment);
        await DeliverAsync(context, shipment.Id);
        return await ShipmentTestData.GetAsync(context, shipment.Id);
    }

    public static Task<CodTransactionDto> CollectAsync(
        IdentityTestContext context,
        long shipmentId,
        decimal amount = Cod,
        long? cashboxId = null,
        string? differenceReason = null) =>
        context.UseAsync<ICodService, CodTransactionDto>(s => s.CollectAsync(
            new CollectCodRequest { ShipmentId = shipmentId, Amount = amount, CashboxId = cashboxId, DifferenceReason = differenceReason },
            CancellationToken.None));

    /// <summary>A delivered COD shipment whose money the driver collected (COD status: CollectedByDriver).</summary>
    public static async Task<CodTransactionDto> CollectedCodAsync(IdentityTestContext context, DemoIds ids, decimal cod = Cod, long? driverId = null)
    {
        var shipment = await DeliveredCodShipmentAsync(context, ids, cod, driverId);
        return await CollectAsync(context, shipment.Id, cod);
    }

    public static async Task<CashVoucherCreationResult<CashReceiptVoucherDto>> HandoverAsync(
        IdentityTestContext context,
        long driverId,
        decimal amount,
        string cashboxCode = "DAM-SYP-01",
        IEnumerable<long>? codIds = null,
        string? idempotencyKey = null)
    {
        var cashboxId = await CashboxIdAsync(context, cashboxCode);
        return await context.UseAsync<ICodService, CashVoucherCreationResult<CashReceiptVoucherDto>>(s => s.HandoverAsync(
            new DriverHandoverRequest { DriverId = driverId, CashboxId = cashboxId, Amount = amount, CodTransactionIds = codIds?.ToList() ?? new List<long>() },
            idempotencyKey,
            CancellationToken.None));
    }

    /// <summary>A COD collected, handed over to DAM-SYP-01 and marked ready (net 82,000 for the default amount).</summary>
    public static async Task<CodTransactionDto> ReadyCodAsync(IdentityTestContext context, DemoIds ids, decimal cod = Cod)
    {
        var collected = await CollectedCodAsync(context, ids, cod);
        await HandoverAsync(context, ids.DamascusDriver, cod, codIds: new[] { collected.Id });
        await MarkReadyAsync(context, collected.Id);
        return await GetCodAsync(context, collected.Id);
    }

    public static Task<IReadOnlyList<CodTransactionListItemDto>> MarkReadyAsync(IdentityTestContext context, params long[] codIds) =>
        context.UseAsync<ICodService, IReadOnlyList<CodTransactionListItemDto>>(s => s.MarkReadyAsync(
            new MarkCodReadyRequest { CodTransactionIds = codIds.ToList() }, CancellationToken.None));

    public static Task<SenderSettlementDto> CreateSettlementAsync(IdentityTestContext context, long senderId, params SettlementLineRequest[] lines) =>
        context.UseAsync<ISenderSettlementService, SenderSettlementDto>(s => s.CreateAsync(
            new CreateSenderSettlementRequest { SenderCustomerId = senderId, Lines = lines.ToList() }, CancellationToken.None));

    public static Task<SenderSettlementDto> ApproveSettlementAsync(IdentityTestContext context, long settlementId) =>
        context.UseAsync<ISenderSettlementService, SenderSettlementDto>(s => s.ApproveAsync(settlementId, new ApproveSenderSettlementRequest(), CancellationToken.None));

    public static async Task<SenderSettlementDto> PayCashAsync(IdentityTestContext context, long settlementId, string cashboxCode = "DAM-SYP-01")
    {
        var methodId = await BillingTestData.PaymentMethodIdAsync(context, "CASH");
        var cashboxId = await CashboxIdAsync(context, cashboxCode);
        return await context.UseAsync<ISenderSettlementService, SenderSettlementDto>(s => s.PayAsync(
            settlementId, new PaySenderSettlementRequest { PaymentMethodId = methodId, CashboxId = cashboxId }, CancellationToken.None));
    }

    public static Task<CashVoucherCreationResult<CashPaymentVoucherDto>> PayExpenseAsync(IdentityTestContext context, long cashboxId, decimal amount, string? key = null) =>
        context.UseAsync<ICashVoucherService, CashVoucherCreationResult<CashPaymentVoucherDto>>(async s =>
        {
            var categoryId = await context.QueryAsync(db => db.ExpenseCategories.Where(x => x.Code == "FUEL").Select(x => x.Id).SingleAsync());
            return await s.CreatePaymentAsync(new CreateCashPaymentRequest
            {
                CashboxId = cashboxId,
                PaymentType = Domain.Enums.CashPaymentType.Expense,
                Amount = amount,
                PaidTo = "محطة الوقود",
                Description = "وقود الشاحنة",
                ExpenseCategoryId = categoryId
            }, key, CancellationToken.None);
        });

    /// <summary>A second active driver of the Damascus branch (the demo has one per branch).</summary>
    public static async Task<long> CreateDamascusDriverAsync(IdentityTestContext context, DemoIds ids, string code = "EMP-900")
    {
        return await context.QueryAsync(async db =>
        {
            var employee = new Employee
            {
                Code = code, ArabicName = "سائق إضافي " + code, BranchId = ids.Damascus, JobTitle = "سائق",
                Phone = "+963944999900", HireDateUtc = DateTime.UtcNow.AddYears(-1), IsActive = true
            };
            db.Employees.Add(employee);
            await db.SaveChangesAsync();

            var driver = new DriverProfile
            {
                EmployeeId = employee.Id, LicenseNumber = "DL-" + code, LicenseCategory = "B",
                LicenseExpiryDateUtc = DateTime.UtcNow.AddYears(2), IsActive = true
            };
            db.DriverProfiles.Add(driver);
            await db.SaveChangesAsync();
            return driver.Id;
        });
    }

    /// <summary>Every outbox posting request must be balanced (sum of debits = sum of credits).</summary>
    public static Task<List<string>> UnbalancedPostingsAsync(IdentityTestContext context) =>
        context.QueryAsync(async db =>
        {
            var messages = await db.OutboxMessages.ToListAsync();
            var problems = new List<string>();
            foreach (var message in messages)
            {
                using var json = System.Text.Json.JsonDocument.Parse(message.PayloadJson);
                if (!json.RootElement.TryGetProperty("posting", out var posting) || !posting.TryGetProperty("lines", out var lines))
                {
                    continue;
                }

                var debit = lines.EnumerateArray().Sum(l => l.GetProperty("debit").GetDecimal());
                var credit = lines.EnumerateArray().Sum(l => l.GetProperty("credit").GetDecimal());
                if (debit != credit || debit == 0m)
                {
                    problems.Add($"{message.EventType}: {debit} <> {credit}");
                }
            }

            return problems;
        });
}
