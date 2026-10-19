using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Infrastructure.Files;
using ShippingERP.Infrastructure.Shipments.Import;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Shipments;

/// <summary>Labels (PDF / HTML), Excel import, shipping settings and secure file storage.</summary>
public sealed class LabelImportAndFileTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        _ids = await ShipmentTestData.LoadIdsAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private static async Task<byte[]> ReadAllAsync(FileDownload file)
    {
        await using var stream = file.Content;
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }

    // ------------------------------------------------------------------ labels

    [Fact]
    public async Task PdfLabel_IsAPdfDocument()
    {
        var request = ShipmentTestData.NewRequest(_ids, codAmount: 125000m);
        request.Packages.Add(new ShipmentPackageRequest { WeightKg = 1m });
        var shipment = await ShipmentTestData.CreateAsync(_context, request);

        var file = await _context.UseAsync<IShipmentLabelService, FileDownload>(s => s.GenerateAsync(shipment.Id, LabelFormat.Pdf, CancellationToken.None));
        var bytes = await ReadAllAsync(file);

        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal($"label-{shipment.ShipmentNumber}.pdf", file.FileName);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.True(bytes.Length > 1000);
    }

    [Fact]
    public async Task HtmlLabel_EncodesUserTextAndContainsBarcodes()
    {
        var request = ShipmentTestData.NewRequest(_ids, codAmount: 125000m);
        request.SenderNotes = "<script>alert('x')</script>";
        var shipment = await ShipmentTestData.CreateAsync(_context, request);

        var file = await _context.UseAsync<IShipmentLabelService, FileDownload>(s => s.GenerateAsync(shipment.Id, LabelFormat.Html, CancellationToken.None));
        var html = Encoding.UTF8.GetString(await ReadAllAsync(file));

        Assert.StartsWith("text/html", file.ContentType);
        Assert.Contains("dir=\"rtl\"", html);
        Assert.Contains(shipment.TrackingNumber, html);
        Assert.Contains("<svg", html);
        Assert.Contains("125,000 SYP", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("size:100mm 150mm", html);
    }

    [Fact]
    public async Task Label_OfACancelledShipment_IsRefused()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        await _context.UseAsync<IShipmentService, ShipmentDto>(s => s.CancelAsync(shipment.Id, new CancelShipmentRequest { Reason = "x" }, CancellationToken.None));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.UseAsync<IShipmentLabelService, FileDownload>(s => s.GenerateAsync(shipment.Id, LabelFormat.Pdf, CancellationToken.None)));
    }

    [Fact]
    public async Task BulkLabels_UseTheConfiguredLabelSize()
    {
        await _context.UseAsync<IShippingSettingsService, ShippingSettingsDto>(s => s.SaveAsync(
            new SaveShippingSettingsRequest { VolumetricDivisor = 5000, WeightRoundingStepKg = 0.5m, TrackingNumberPrefix = "AB", LabelWidthMm = 100, LabelHeightMm = 100 },
            CancellationToken.None));
        var first = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        var second = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        var file = await _context.UseAsync<IShipmentLabelService, FileDownload>(s => s.GenerateBulkAsync(
            new BulkLabelRequest { ShipmentIds = new List<long> { second.Id, first.Id }, Format = LabelFormat.Html },
            CancellationToken.None));
        var html = Encoding.UTF8.GetString(await ReadAllAsync(file));

        Assert.StartsWith("AB", first.TrackingNumber);
        Assert.Contains("size:100mm 100mm", html);
        Assert.True(html.IndexOf(second.TrackingNumber, StringComparison.Ordinal) < html.IndexOf(first.TrackingNumber, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ settings

    [Fact]
    public async Task Settings_VolumetricDivisor_AppliesToNewShipments()
    {
        var defaults = await _context.UseAsync<IShippingSettingsService, ShippingSettingsDto>(s => s.GetAsync(CancellationToken.None));
        Assert.True(defaults.IsDefault);
        Assert.Equal(5000, defaults.VolumetricDivisor);

        await _context.UseAsync<IShippingSettingsService, ShippingSettingsDto>(s => s.SaveAsync(
            new SaveShippingSettingsRequest { VolumetricDivisor = 4000, WeightRoundingStepKg = 0m, TrackingNumberPrefix = "SE", LabelWidthMm = 100, LabelHeightMm = 150 },
            CancellationToken.None));

        var request = ShipmentTestData.NewRequest(_ids, weightKg: 1m);
        request.Packages[0].LengthCm = 30m;
        request.Packages[0].WidthCm = 20m;
        request.Packages[0].HeightCm = 10m;
        var shipment = await ShipmentTestData.CreateAsync(_context, request);

        // 30 x 20 x 10 / 4000 = 1.5 (no rounding step)
        Assert.Equal(1.5m, shipment.VolumetricWeight);
        Assert.Equal(1.5m, shipment.ChargeableWeight);
    }

    // ------------------------------------------------------------------ Excel import

    private static UploadedFile Workbook(params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Shipments");
        var columns = ShipmentImportColumns.All;
        for (var c = 0; c < columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = columns[c].Key;
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = rows[r][c] switch
                {
                    null => Blank.Value,
                    string text => text,
                    int number => number,
                    decimal number => number,
                    _ => rows[r][c]!.ToString()
                };
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return ShipmentTestData.File(stream.ToArray(), "shipments.xlsx");
    }

    // ExternalReference, Sender, Recipient, City, Zone, Service, Count, Weight, L, W, H, Description, Declared, Insurance, COD?, CODAmount, Notes
    private static object?[] Row(string reference, string sender, string recipient, decimal weight, int packages = 1, string cod = "لا", decimal codAmount = 0m, string service = "STD") =>
        new object?[] { reference, sender, recipient, null, null, service, packages, weight, null, null, null, "ملابس", 0m, 0m, cod, codAmount, null };

    [Fact]
    public async Task ImportTemplate_HasEveryColumn()
    {
        var file = await _context.UseAsync<IShipmentImportService, FileDownload>(s => Task.FromResult(s.GetTemplate()));

        using var workbook = new XLWorkbook(file.Content);
        var sheet = workbook.Worksheet("Shipments");
        var headers = Enumerable.Range(1, ShipmentImportColumns.All.Count).Select(c => sheet.Cell(1, c).GetString()).ToList();

        Assert.Equal(ShipmentImportColumns.All.Select(c => c.Key), headers);
        Assert.True(workbook.Worksheet("Instructions").RightToLeft);
    }

    [Fact]
    public async Task ImportPreview_ReportsRowErrors_WithoutSaving()
    {
        var file = Workbook(
            Row("A-1", "CUS-00003", "CUS-00002", 2.5m),
            Row("A-2", "+963113000103", "CUS-00006", 7m, cod: "نعم", codAmount: 50000m),
            Row("A-3", "CUS-99999", "CUS-00002", 1m),
            Row("A-1", "CUS-00003", "CUS-00002", 1m));

        var preview = await _context.UseAsync<IShipmentImportService, ShipmentImportPreviewDto>(s =>
            s.PreviewAsync(file, new ImportShipmentsRequest { OriginBranchId = _ids.Damascus }, CancellationToken.None));

        Assert.Equal(4, preview.TotalRows);
        Assert.Equal(2, preview.ValidRows);
        Assert.False(preview.CanImport);
        Assert.Contains(preview.Rows[2].Errors, e => e.Contains("CUS-99999"));
        Assert.Contains(preview.Rows[3].Errors, e => e.Contains("A-1"));
        Assert.Equal(2, preview.Rows[0].RowNumber);
        Assert.Equal(5, preview.Rows[3].RowNumber);
        Assert.Equal(50000m, preview.Rows[1].CODAmount);
        Assert.Equal(0, await _context.QueryAsync(db => db.Shipments.CountAsync()));
    }

    [Fact]
    public async Task Import_WithAnInvalidRow_ImportsNothing()
    {
        var file = Workbook(Row("B-1", "CUS-00003", "CUS-00002", 2m), Row("B-2", "CUS-00003", "", 2m));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => _context.UseAsync<IShipmentImportService, ShipmentImportResultDto>(s =>
            s.ImportAsync(file, new ImportShipmentsRequest { OriginBranchId = _ids.Damascus }, CancellationToken.None)));

        Assert.True(exception.Errors.ContainsKey("row 3"));
        Assert.Equal(0, await _context.QueryAsync(db => db.Shipments.CountAsync()));
    }

    [Fact]
    public async Task Import_ValidFile_CreatesEveryShipmentAndSplitsTheWeight()
    {
        var file = Workbook(Row("C-1", "CUS-00003", "CUS-00002", 10m, packages: 3), Row("C-2", "CUS-00007", "CUS-00006", 1m));

        var result = await _context.UseAsync<IShipmentImportService, ShipmentImportResultDto>(s =>
            s.ImportAsync(file, new ImportShipmentsRequest { OriginBranchId = _ids.Damascus }, CancellationToken.None));

        Assert.Equal(2, result.ImportedCount);
        var packages = await _context.QueryAsync(db => db.ShipmentPackages
            .Where(x => x.ShipmentId == result.Shipments[0].ShipmentId)
            .OrderBy(x => x.SequenceNumber)
            .Select(x => x.WeightKg)
            .ToListAsync());
        Assert.Equal(new[] { 3.333m, 3.333m, 3.334m }, packages);

        var history = await _context.UseAsync<IShipmentService, IReadOnlyList<ShipmentStatusHistoryDto>>(s =>
            s.GetHistoryAsync(result.Shipments[1].ShipmentId, CancellationToken.None));
        Assert.Equal(Domain.Enums.ShipmentStatusChangeSource.Import, Assert.Single(history).Source);
    }

    [Fact]
    public async Task Import_NonExcelFile_IsRejected()
    {
        var file = ShipmentTestData.File("not excel"u8.ToArray(), "shipments.xlsx");

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => _context.UseAsync<IShipmentImportService, ShipmentImportPreviewDto>(s =>
            s.PreviewAsync(file, new ImportShipmentsRequest { OriginBranchId = _ids.Damascus }, CancellationToken.None)));

        Assert.True(exception.Errors.ContainsKey("file"));
    }

    // ------------------------------------------------------------------ file storage

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, "application/pdf")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, null)]
    public void FileSignatures_DetectTheRealType(byte[] header, string? expected)
    {
        Assert.Equal(expected, FileSignatures.Detect(header)?.ContentType);
    }

    [Fact]
    public async Task FileStorage_RejectsAMismatchedExtension_AndPathTraversalKeys()
    {
        var storage = new LocalFileStorage(
            Options.Create(new FileStorageOptions { RootPath = _context.UploadsRoot, MaxFileSizeBytes = 1024 * 1024 }),
            _context.Clock);

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            storage.SaveAsync(ShipmentTestData.File(ShipmentTestData.PngBytes, "photo.pdf"), "shipments", "file", CancellationToken.None));

        var stored = await storage.SaveAsync(ShipmentTestData.Png("..\\..\\x.png"), "shipments", "file", CancellationToken.None);
        Assert.StartsWith("shipments/", stored.StorageKey);
        Assert.Equal("x.png", stored.SafeFileName);
        Assert.Equal(64, stored.Sha256.Length);

        await using (var stream = await storage.OpenReadAsync(stored.StorageKey, CancellationToken.None))
        {
            Assert.Equal(ShipmentTestData.PngBytes.Length, stream.Length);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.OpenReadAsync("../../etc/passwd", CancellationToken.None));
    }

    [Fact]
    public async Task FileStorage_RejectsFilesOverTheLimit()
    {
        var storage = new LocalFileStorage(
            Options.Create(new FileStorageOptions { RootPath = _context.UploadsRoot, MaxFileSizeBytes = 100 }),
            _context.Clock);
        var big = ShipmentTestData.PngBytes.Concat(new byte[500]).ToArray();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            storage.SaveAsync(ShipmentTestData.File(big, "big.png"), "shipments", "file", CancellationToken.None));
    }
}
