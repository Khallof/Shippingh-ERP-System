using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;

namespace ShippingERP.Tests.Common;

/// <summary>Ids of the demo master data (step 3 demo seeder), loaded once per test class.</summary>
public sealed class DemoIds
{
    public long Damascus { get; init; }
    public long Aleppo { get; init; }
    public long DamascusWarehouse { get; init; }
    public long AleppoWarehouse { get; init; }
    public long Standard { get; init; }
    public long Express { get; init; }
    public long SameDay { get; init; }
    public long DamascusCity { get; init; }
    public long HomsCity { get; init; }
    public long DamascusDriver { get; init; }
    public long AleppoDriver { get; init; }
    public long Van { get; init; }
    public long Motorcycle { get; init; }
    public IReadOnlyDictionary<string, long> Customers { get; init; } = new Dictionary<string, long>();

    public long Customer(string code) => Customers[code];
}

/// <summary>Helpers shared by the shipment tests.</summary>
public static class ShipmentTestData
{
    /// <summary>A valid 1 x 1 PNG image.</summary>
    public static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public static async Task<DemoIds> LoadIdsAsync(IdentityTestContext context)
    {
        return await context.QueryAsync(async db => new DemoIds
        {
            Damascus = await db.Branches.Where(x => x.Code == "DAM").Select(x => x.Id).SingleAsync(),
            Aleppo = await db.Branches.Where(x => x.Code == "ALP").Select(x => x.Id).SingleAsync(),
            DamascusWarehouse = await db.Warehouses.Where(x => x.Code == "DAM-WH1").Select(x => x.Id).SingleAsync(),
            AleppoWarehouse = await db.Warehouses.Where(x => x.Code == "ALP-WH1").Select(x => x.Id).SingleAsync(),
            Standard = await db.ServiceTypes.Where(x => x.Code == "STD").Select(x => x.Id).SingleAsync(),
            Express = await db.ServiceTypes.Where(x => x.Code == "EXP").Select(x => x.Id).SingleAsync(),
            SameDay = await db.ServiceTypes.Where(x => x.Code == "SDD").Select(x => x.Id).SingleAsync(),
            DamascusCity = await db.Cities.Where(x => x.Code == "DAM").Select(x => x.Id).SingleAsync(),
            HomsCity = await db.Cities.Where(x => x.Code == "HMS").Select(x => x.Id).SingleAsync(),
            DamascusDriver = await db.DriverProfiles.Where(x => x.LicenseNumber == "DL-100200").Select(x => x.Id).SingleAsync(),
            AleppoDriver = await db.DriverProfiles.Where(x => x.LicenseNumber == "DL-100300").Select(x => x.Id).SingleAsync(),
            Van = await db.Vehicles.Where(x => x.PlateNumber == "DAM 123-456").Select(x => x.Id).SingleAsync(),
            Motorcycle = await db.Vehicles.Where(x => x.PlateNumber == "ALP 654-321").Select(x => x.Id).SingleAsync(),
            Customers = await db.Customers.ToDictionaryAsync(x => x.Code, x => x.Id)
        });
    }

    /// <summary>
    /// Damascus -> Damascus standard shipment (sender CUS-00003, recipient CUS-00002 in Damascus / Malki).
    /// Demo price: 15,000 for 0-5 kg; COD fee 2,000 + 1%.
    /// </summary>
    public static SaveShipmentRequest NewRequest(
        DemoIds ids,
        string sender = "CUS-00003",
        string recipient = "CUS-00002",
        long? originBranchId = null,
        decimal weightKg = 2.5m,
        decimal codAmount = 0m,
        long? serviceTypeId = null,
        string? externalReference = null)
    {
        return new SaveShipmentRequest
        {
            SenderCustomerId = ids.Customer(sender),
            RecipientCustomerId = ids.Customer(recipient),
            OriginBranchId = originBranchId ?? ids.Damascus,
            ServiceTypeId = serviceTypeId ?? ids.Standard,
            PackageDescription = "ملابس",
            Packages = new List<ShipmentPackageRequest> { new() { WeightKg = weightKg } },
            DeclaredValue = codAmount,
            IsCOD = codAmount > 0m,
            CODAmount = codAmount,
            ExternalReference = externalReference
        };
    }

    public static Task<ShipmentDto> CreateAsync(IdentityTestContext context, SaveShipmentRequest request) =>
        context.UseAsync<IShipmentService, ShipmentDto>(service => service.CreateAsync(request, CancellationToken.None));

    public static Task<ShipmentDto> GetAsync(IdentityTestContext context, long id) =>
        context.UseAsync<IShipmentService, ShipmentDto>(service => service.GetByIdAsync(id, CancellationToken.None));

    /// <summary>Moves a shipment to ReceivedAtBranch with a manual scan (needed before transfers / driver manifests).</summary>
    public static Task<ShipmentDto> ReceiveAtBranchAsync(IdentityTestContext context, long id) =>
        context.UseAsync<IShipmentService, ShipmentDto>(service => service.ChangeStatusAsync(
            id,
            new ChangeShipmentStatusRequest { Status = Domain.Enums.ShipmentStatus.ReceivedAtBranch },
            CancellationToken.None));

    /// <summary>Creates a user in the Driver role and links it to the driver profile (needs the roles seeded).</summary>
    public static async Task<long> LinkDriverUserAsync(IdentityTestContext context, long driverId, string userName, long branchId)
    {
        var userId = await context.CreateUserAsync(userName, ShippingERP.Domain.Authorization.Roles.Driver, branchId: branchId);
        await context.QueryAsync(async db =>
        {
            var profile = await db.DriverProfiles.FirstAsync(x => x.Id == driverId);
            profile.UserId = userId;
            return await db.SaveChangesAsync();
        });

        return userId;
    }

    public static UploadedFile File(byte[] content, string fileName) =>
        new(fileName, "application/octet-stream", content.Length, () => new MemoryStream(content));

    public static UploadedFile Png(string fileName = "signature.png") => File(PngBytes, fileName);
}
