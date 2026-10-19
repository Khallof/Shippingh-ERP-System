using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// Creates 6 DEMO shipments in different statuses (Created, PickedUp, ReceivedAtBranch, InWarehouse) on top of
/// the demo master data of step 3, using the real ShipmentBuilder (same validation, numbers and pricing as the API).
///
/// Runs only when Seed:DemoData = true, the demo customers (CUS-00001...) exist and there is no shipment yet.
/// </summary>
public sealed class DemoShipmentSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly ShipmentBuilder _builder;
    private readonly ShipmentStatusChanger _statusChanger;
    private readonly ShippingSettingsService _settings;
    private readonly SeedOptions _options;
    private readonly ILogger<DemoShipmentSeeder> _logger;

    public DemoShipmentSeeder(
        AppDbContext dbContext,
        ShipmentBuilder builder,
        ShipmentStatusChanger statusChanger,
        ShippingSettingsService settings,
        IOptions<SeedOptions> options,
        ILogger<DemoShipmentSeeder> logger)
    {
        _dbContext = dbContext;
        _builder = builder;
        _statusChanger = statusChanger;
        _settings = settings;
        _options = options.Value;
        _logger = logger;
    }

    /// <param name="force">Ignore Seed:DemoData (used by tests).</param>
    /// <returns>Number of shipments created.</returns>
    public async Task<int> SeedAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && (!_options.Enabled || !_options.DemoData))
        {
            return 0;
        }

        if (_dbContext.Database.IsRelational() &&
            (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            return 0;
        }

        if (await _dbContext.Shipments.AnyAsync(cancellationToken))
        {
            return 0;
        }

        var customers = await _dbContext.Customers.AsNoTracking()
            .Where(x => x.Code.StartsWith("CUS-0000") || x.Code == "CUS-00010")
            .ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
        var serviceTypes = await _dbContext.ServiceTypes.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
        var branches = await _dbContext.Branches.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
        var warehouseId = await _dbContext.Warehouses.AsNoTracking()
            .Where(x => x.Code == "DAM-WH1")
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var required = new[] { "CUS-00001", "CUS-00002", "CUS-00003", "CUS-00004", "CUS-00005", "CUS-00006", "CUS-00007", "CUS-00008", "CUS-00009", "CUS-00010" };
        if (required.Any(code => !customers.ContainsKey(code)) ||
            !serviceTypes.ContainsKey("STD") || !serviceTypes.ContainsKey("EXP") || !serviceTypes.ContainsKey("SDD") ||
            !branches.ContainsKey("DAM") || !branches.ContainsKey("ALP") || warehouseId is null)
        {
            _logger.LogDebug("Demo shipments skipped: the demo master data of step 3 was not found");
            return 0;
        }

        var settings = await _settings.GetEffectiveAsync(cancellationToken);

        var demo = new (string Sender, string Recipient, string Branch, string Service, decimal[] Weights, bool Dimensions, decimal Cod, string Description, ShipmentStatus Status, string? Reference)[]
        {
            ("CUS-00003", "CUS-00002", "DAM", "STD", new[] { 2.5m }, false, 125000m, "ملابس نسائية", ShipmentStatus.Created, "YAS-1001"),
            ("CUS-00007", "CUS-00006", "DAM", "EXP", new[] { 3m, 4m }, true, 300000m, "أجهزة إلكترونية صغيرة", ShipmentStatus.ReceivedAtBranch, "NOOR-5521"),
            ("CUS-00010", "CUS-00008", "DAM", "STD", new[] { 1m }, false, 0m, "أدوية (مغلفة)", ShipmentStatus.InWarehouse, null),
            ("CUS-00005", "CUS-00001", "ALP", "STD", new[] { 6m }, false, 90000m, "ألبسة رجالية", ShipmentStatus.Created, "SHB-77"),
            ("CUS-00004", "CUS-00009", "ALP", "SDD", new[] { 0.8m }, false, 0m, "مستندات", ShipmentStatus.Created, null),
            ("CUS-00001", "CUS-00002", "DAM", "EXP", new[] { 0.5m }, false, 0m, "هدية", ShipmentStatus.PickedUp, null)
        };

        var count = await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            var created = 0;
            foreach (var item in demo)
            {
                var request = new SaveShipmentRequest
                {
                    SenderCustomerId = customers[item.Sender],
                    RecipientCustomerId = customers[item.Recipient],
                    OriginBranchId = branches[item.Branch],
                    ServiceTypeId = serviceTypes[item.Service],
                    PackageDescription = item.Description,
                    Packages = item.Weights.Select(weight => new ShipmentPackageRequest
                    {
                        WeightKg = weight,
                        LengthCm = item.Dimensions ? 40m : null,
                        WidthCm = item.Dimensions ? 30m : null,
                        HeightCm = item.Dimensions ? 20m : null
                    }).ToList(),
                    DeclaredValue = item.Cod,
                    IsCOD = item.Cod > 0m,
                    CODAmount = item.Cod,
                    SenderNotes = item.Cod > 0m ? "اتصل بالمستلم قبل التوصيل" : null,
                    ExternalReference = item.Reference
                };

                var draft = await _builder.PrepareAsync(request, null, settings, ct);
                var shipment = await _builder.AddNewAsync(draft, settings, ShipmentStatusChangeSource.Creation, "Created", null, ct);
                await _dbContext.SaveChangesAsync(ct);

                if (item.Status is ShipmentStatus.PickedUp or ShipmentStatus.ReceivedAtBranch or ShipmentStatus.InWarehouse)
                {
                    _statusChanger.Change(shipment, item.Status, new StatusChange(ShipmentStatusChangeSource.Manual)
                    {
                        WarehouseId = item.Status == ShipmentStatus.InWarehouse ? warehouseId : null,
                        Notes = "بيانات تجريبية"
                    });
                    await _dbContext.SaveChangesAsync(ct);
                }

                created++;
            }

            return created;
        }, cancellationToken);

        _logger.LogInformation("Demo shipments created ({Count})", count);
        return count;
    }
}
