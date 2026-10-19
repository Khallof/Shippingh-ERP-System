using System.Globalization;
using ClosedXML.Excel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Entities.Settings;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Files;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments.Import;

/// <summary>
/// Excel import of shipments (ClosedXML, MIT license).
///
/// - GET template: the expected columns, an example row and Arabic instructions.
/// - Preview: every row is read and validated with exactly the same rules as POST /shipments
///   (FluentValidation + ShipmentBuilder + pricing); nothing is saved. The client shows the row errors.
/// - Import: validates again and creates ALL shipments in ONE transaction. One invalid row = nothing imported.
///
/// Limits: .xlsx only, 5 MB, 500 data rows. Codes are matched case-insensitively.
/// </summary>
public sealed class ShipmentImportService : IShipmentImportService
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const int MaxRows = 500;
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] TrueWords = { "1", "true", "yes", "y", "نعم", "صح" };
    private static readonly string[] FalseWords = { "", "0", "false", "no", "n", "لا", "خطأ" };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentBuilder _builder;
    private readonly ShippingSettingsService _settings;
    private readonly IValidator<SaveShipmentRequest> _validator;

    public ShipmentImportService(
        MasterDataServiceDependencies dependencies,
        ShipmentBuilder builder,
        ShippingSettingsService settings,
        IValidator<SaveShipmentRequest> validator,
        ShipmentAutoBilling autoBilling)
    {
        _dependencies = dependencies;
        _builder = builder;
        _settings = settings;
        _validator = validator;
        _autoBilling = autoBilling;
    }

    private readonly ShipmentAutoBilling _autoBilling;

    private AppDbContext Db => _dependencies.DbContext;

    public FileDownload GetTemplate()
    {
        using var workbook = new XLWorkbook();

        var sheet = workbook.AddWorksheet("Shipments");
        var columns = ShipmentImportColumns.All;
        for (var i = 0; i < columns.Count; i++)
        {
            var header = sheet.Cell(1, i + 1);
            header.Value = columns[i].Key;
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = columns[i].Required ? XLColor.LightGreen : XLColor.LightGray;
            header.CreateComment().AddText(columns[i].ArabicDescription);

            sheet.Cell(2, i + 1).Value = columns[i].Example;
            sheet.Column(i + 1).Width = Math.Max(14, columns[i].Key.Length + 4);
        }

        sheet.SheetView.FreezeRows(1);

        var help = workbook.AddWorksheet("Instructions");
        help.RightToLeft = true;
        help.Cell(1, 1).Value = "العمود";
        help.Cell(1, 2).Value = "إلزامي";
        help.Cell(1, 3).Value = "الوصف";
        help.Cell(1, 4).Value = "مثال";
        help.Row(1).Style.Font.Bold = true;

        for (var i = 0; i < columns.Count; i++)
        {
            help.Cell(i + 2, 1).Value = columns[i].Key;
            help.Cell(i + 2, 2).Value = columns[i].Required ? "نعم" : "لا";
            help.Cell(i + 2, 3).Value = columns[i].ArabicDescription;
            help.Cell(i + 2, 4).Value = columns[i].Example;
        }

        var notesRow = columns.Count + 3;
        help.Cell(notesRow, 1).Value = "ملاحظات";
        help.Cell(notesRow, 1).Style.Font.Bold = true;
        help.Cell(notesRow + 1, 1).Value = "1. احذف صف المثال (الصف 2) من ورقة Shipments قبل الاستيراد.";
        help.Cell(notesRow + 2, 1).Value = $"2. الحد الأقصى {MaxRows} صف في الملف، وحجم الملف حتى 5 ميغابايت.";
        help.Cell(notesRow + 3, 1).Value = "3. يُحسب سعر الشحن من خطة تسعير المرسل تلقائياً.";
        help.Cell(notesRow + 4, 1).Value = "4. الاستيراد كامل أو لا شيء: إذا كان أي صف خاطئاً لا يُستورد أي صف. استخدم المعاينة أولاً.";
        help.Column(1).Width = 24;
        help.Column(2).Width = 10;
        help.Column(3).Width = 70;
        help.Column(4).Width = 18;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return FileDownload.FromBytes(stream.ToArray(), XlsxContentType, "shipments-import-template.xlsx");
    }

    public async Task<ShipmentImportPreviewDto> PreviewAsync(UploadedFile file, ImportShipmentsRequest request, CancellationToken cancellationToken)
    {
        var batch = await ValidateFileAsync(file, request, cancellationToken);

        return new ShipmentImportPreviewDto
        {
            FileName = LocalFileStorage.SafeFileName(file.FileName, ".xlsx"),
            TotalRows = batch.Rows.Count,
            ValidRows = batch.Rows.Count(x => x.IsValid),
            InvalidRows = batch.Rows.Count(x => !x.IsValid),
            CanImport = batch.Rows.Count > 0 && batch.Rows.All(x => x.IsValid),
            Rows = batch.Rows.Select(x => x.ToDto()).ToList()
        };
    }

    public async Task<ShipmentImportResultDto> ImportAsync(UploadedFile file, ImportShipmentsRequest request, CancellationToken cancellationToken)
    {
        var batch = await ValidateFileAsync(file, request, cancellationToken);

        if (batch.Rows.Count == 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["file"] = new[] { "الملف لا يحتوي على صفوف بيانات." }
            });
        }

        var invalid = batch.Rows.Where(x => !x.IsValid).ToList();
        if (invalid.Count > 0)
        {
            // Nothing is imported: the client gets every row error at once.
            throw new RequestValidationException(invalid.ToDictionary(
                x => $"row {x.RowNumber}",
                x => x.Errors.ToArray()));
        }

        var fileName = LocalFileStorage.SafeFileName(file.FileName, ".xlsx");

        var imported = await Db.ExecuteInTransactionAsync(async ct =>
        {
            var reserved = new HashSet<string>(StringComparer.Ordinal);
            var created = new List<(int RowNumber, Domain.Entities.Shipments.Shipment Shipment)>();

            foreach (var row in batch.Rows)
            {
                var shipment = await _builder.AddNewAsync(row.Draft!, batch.Settings, ShipmentStatusChangeSource.Import, "Imported", reserved, ct);
                created.Add((row.RowNumber, shipment));
            }

            await Db.SaveChangesAsync(ct);

            _dependencies.AuditLog.Add(
                AuditAction.ShipmentsImported,
                entityName: "Shipment",
                details: new
                {
                    fileName,
                    count = created.Count,
                    originBranchId = batch.OriginBranchId,
                    shipmentNumbers = created.Select(x => x.Shipment.ShipmentNumber).Take(50).ToList()
                });
            await Db.SaveChangesAsync(ct);

            return created
                .Select(x => new ImportedShipmentDto
                {
                    RowNumber = x.RowNumber,
                    ShipmentId = x.Shipment.Id,
                    ShipmentNumber = x.Shipment.ShipmentNumber,
                    TrackingNumber = x.Shipment.TrackingNumber
                })
                .ToList();
        }, cancellationToken);

        // Step 9: every imported shipment becomes the debt of its payer, like a shipment registered on screen.
        foreach (var row in imported)
        {
            await _autoBilling.InvoiceAsync(row.ShipmentId, cancellationToken);
        }

        return new ShipmentImportResultDto { ImportedCount = imported.Count, Shipments = imported };
    }

    // ------------------------------------------------------------------ reading and validation

    private async Task<ImportBatch> ValidateFileAsync(UploadedFile file, ImportShipmentsRequest request, CancellationToken cancellationToken)
    {
        var originBranchId = request.OriginBranchId ?? _dependencies.BranchAccess.CurrentBranchId
                             ?? throw FileError("originBranchId", "حدد الفرع الذي يسجّل الشحنات (originBranchId).");

        _dependencies.BranchAccess.EnsureCanAccess(originBranchId);

        var rawRows = ReadWorkbook(file);
        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var lookups = await LoadLookupsAsync(rawRows, cancellationToken);

        var rows = new List<ImportRow>();
        var seenReferences = new HashSet<(long, string)>();

        foreach (var raw in rawRows)
        {
            var row = new ImportRow(raw);
            var request2 = BuildRequest(raw, originBranchId, lookups, row.Errors);

            if (request2 is not null)
            {
                var validation = await _validator.ValidateAsync(request2, cancellationToken);
                foreach (var failure in validation.Errors)
                {
                    row.Errors.Add(failure.ErrorMessage);
                }

                if (request2.ExternalReference is not null &&
                    !seenReferences.Add((request2.SenderCustomerId, request2.ExternalReference.ToUpperInvariant())))
                {
                    row.Errors.Add($"المرجع '{request2.ExternalReference}' مكرر في الملف لنفس المرسل.");
                }

                if (row.Errors.Count == 0)
                {
                    try
                    {
                        row.Draft = await _builder.PrepareAsync(request2, null, settings, cancellationToken);
                    }
                    catch (RequestValidationException exception)
                    {
                        row.Errors.AddRange(exception.Errors.SelectMany(x => x.Value));
                    }
                    catch (AppException exception)
                    {
                        row.Errors.Add(exception.Message);
                    }
                }
            }

            rows.Add(row);
        }

        return new ImportBatch(originBranchId, settings, rows);
    }

    private static List<RawRow> ReadWorkbook(UploadedFile file)
    {
        if (file.Length <= 0)
        {
            throw FileError("file", "الملف فارغ.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw FileError("file", "حجم الملف أكبر من 5 ميغابايت.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw FileError("file", "الملف يجب أن يكون بصيغة Excel ‏(.xlsx).");
        }

        using var buffer = new MemoryStream();
        using (var input = file.OpenReadStream())
        {
            input.CopyTo(buffer);
        }

        if (buffer.Length > MaxFileSizeBytes || !FileSignatures.IsZip(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 8))))
        {
            throw FileError("file", "الملف ليس ملف Excel صالحاً (.xlsx).");
        }

        buffer.Position = 0;

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(buffer);
        }
        catch (Exception)
        {
            throw FileError("file", "تعذّرت قراءة ملف Excel. احفظه من Excel بصيغة .xlsx وحاول مجدداً.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(x => x.Name.Equals("Shipments", StringComparison.OrdinalIgnoreCase))
                        ?? workbook.Worksheets.First();

            var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var column = 1; column <= lastColumn; column++)
            {
                var header = CellText(sheet.Cell(1, column));
                if (!string.IsNullOrWhiteSpace(header))
                {
                    headers[header.Trim()] = column;
                }
            }

            var missing = ShipmentImportColumns.All.Where(x => x.Required && !headers.ContainsKey(x.Key)).Select(x => x.Key).ToList();
            if (missing.Count > 0)
            {
                throw FileError("file", $"أعمدة مطلوبة غير موجودة في الصف الأول: {string.Join(", ", missing)}. حمّل القالب من /shipments/import/template.");
            }

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            var rows = new List<RawRow>();

            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, column) in headers)
                {
                    values[key] = CellText(sheet.Cell(rowNumber, column));
                }

                if (values.Values.All(string.IsNullOrWhiteSpace))
                {
                    continue; // empty line
                }

                rows.Add(new RawRow(rowNumber, values));
                if (rows.Count > MaxRows)
                {
                    throw FileError("file", $"الحد الأقصى {MaxRows} صف في الملف الواحد. قسّم الملف.");
                }
            }

            return rows;
        }
    }

    private static string CellText(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank)
        {
            return string.Empty;
        }

        if (value.IsNumber)
        {
            return value.GetNumber().ToString("0.############", CultureInfo.InvariantCulture);
        }

        if (value.IsBoolean)
        {
            return value.GetBoolean() ? "1" : "0";
        }

        if (value.IsText)
        {
            return value.GetText().Trim();
        }

        return cell.GetFormattedString().Trim();
    }

    private async Task<Lookups> LoadLookupsAsync(IReadOnlyList<RawRow> rows, CancellationToken cancellationToken)
    {
        var customerTokens = rows
            .SelectMany(x => new[] { x.Get(ShipmentImportColumns.SenderCustomer), x.Get(ShipmentImportColumns.RecipientCustomer) })
            .Where(x => x.Length > 0)
            .Select(x => x.ToUpperInvariant())
            .Distinct()
            .ToList();

        var customers = await Db.Customers.AsNoTracking()
            .Where(x => customerTokens.Contains(x.Code) || customerTokens.Contains(x.Phone))
            .Select(x => new { x.Id, x.Code, x.Phone })
            .ToListAsync(cancellationToken);

        var cityCodes = rows.Select(x => x.Get(ShipmentImportColumns.DestinationCityCode).ToUpperInvariant()).Where(x => x.Length > 0).Distinct().ToList();
        var cities = await Db.Cities.AsNoTracking()
            .Where(x => cityCodes.Contains(x.Code))
            .Select(x => new { x.Id, x.Code })
            .ToListAsync(cancellationToken);

        var zoneCodes = rows.Select(x => x.Get(ShipmentImportColumns.DestinationZoneCode).ToUpperInvariant()).Where(x => x.Length > 0).Distinct().ToList();
        var zones = await Db.Zones.AsNoTracking()
            .Where(x => zoneCodes.Contains(x.Code))
            .Select(x => new { x.Id, x.CityId, x.Code })
            .ToListAsync(cancellationToken);

        var serviceCodes = rows.Select(x => x.Get(ShipmentImportColumns.ServiceTypeCode).ToUpperInvariant()).Where(x => x.Length > 0).Distinct().ToList();
        var serviceTypes = await Db.ServiceTypes.AsNoTracking()
            .Where(x => serviceCodes.Contains(x.Code))
            .Select(x => new { x.Id, x.Code })
            .ToListAsync(cancellationToken);

        return new Lookups(
            customers.Select(x => (x.Id, x.Code, x.Phone)).ToList(),
            cities.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList(), StringComparer.OrdinalIgnoreCase),
            zones.Select(x => (x.Id, x.CityId, x.Code)).ToList(),
            serviceTypes.ToDictionary(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Turns a raw row into a request; adds an error and returns null when a code cannot be resolved.</summary>
    private static SaveShipmentRequest? BuildRequest(RawRow raw, long originBranchId, Lookups lookups, List<string> errors)
    {
        var senderId = ResolveCustomer(raw.Get(ShipmentImportColumns.SenderCustomer), "المرسل", lookups, errors);
        var recipientId = ResolveCustomer(raw.Get(ShipmentImportColumns.RecipientCustomer), "المستلم", lookups, errors);

        long? cityId = null;
        var cityCode = raw.Get(ShipmentImportColumns.DestinationCityCode);
        if (cityCode.Length > 0)
        {
            if (!lookups.Cities.TryGetValue(cityCode, out var cityIds))
            {
                errors.Add($"رمز المدينة '{cityCode}' غير موجود.");
            }
            else if (cityIds.Count > 1)
            {
                errors.Add($"رمز المدينة '{cityCode}' موجود في أكثر من دولة؛ اترك المدينة فارغة ليُستخدم عنوان المستلم.");
            }
            else
            {
                cityId = cityIds[0];
            }
        }

        long? zoneId = null;
        var zoneCode = raw.Get(ShipmentImportColumns.DestinationZoneCode);
        if (zoneCode.Length > 0)
        {
            var candidates = lookups.Zones.Where(z => z.Code.Equals(zoneCode, StringComparison.OrdinalIgnoreCase)).ToList();
            var zone = cityId.HasValue ? candidates.FirstOrDefault(z => z.CityId == cityId) : (candidates.Count == 1 ? candidates[0] : default);
            if (zone == default)
            {
                errors.Add(cityId.HasValue || candidates.Count == 0
                    ? $"رمز المنطقة '{zoneCode}' غير موجود في مدينة الوجهة."
                    : $"رمز المنطقة '{zoneCode}' يحتاج رمز المدينة.");
            }
            else
            {
                zoneId = zone.Id;
                cityId ??= zone.CityId;
            }
        }

        long serviceTypeId = 0;
        var serviceCode = raw.Get(ShipmentImportColumns.ServiceTypeCode);
        if (serviceCode.Length == 0)
        {
            errors.Add("رمز نوع الخدمة مطلوب.");
        }
        else if (!lookups.ServiceTypes.TryGetValue(serviceCode, out serviceTypeId))
        {
            errors.Add($"رمز نوع الخدمة '{serviceCode}' غير موجود.");
        }

        var packageCount = ParseDecimal(raw, ShipmentImportColumns.PackageCount, "عدد الطرود", errors, required: true);
        var totalWeight = ParseDecimal(raw, ShipmentImportColumns.TotalWeightKg, "الوزن الكلي", errors, required: true);
        var length = ParseDecimal(raw, ShipmentImportColumns.LengthCm, "الطول", errors, required: false);
        var width = ParseDecimal(raw, ShipmentImportColumns.WidthCm, "العرض", errors, required: false);
        var height = ParseDecimal(raw, ShipmentImportColumns.HeightCm, "الارتفاع", errors, required: false);
        var declaredValue = ParseDecimal(raw, ShipmentImportColumns.DeclaredValue, "القيمة المصرّح بها", errors, required: false) ?? 0m;
        var insuranceValue = ParseDecimal(raw, ShipmentImportColumns.InsuranceValue, "قيمة التأمين", errors, required: false) ?? 0m;
        var codAmount = ParseDecimal(raw, ShipmentImportColumns.CodAmount, "مبلغ التحصيل", errors, required: false) ?? 0m;
        var isCod = ParseBool(raw.Get(ShipmentImportColumns.IsCod), errors);

        if (packageCount.HasValue && (packageCount.Value != Math.Floor(packageCount.Value) || packageCount < 1 || packageCount > 100))
        {
            errors.Add("عدد الطرود يجب أن يكون رقماً صحيحاً من 1 إلى 100.");
        }

        if (errors.Count > 0 || senderId is null || recipientId is null || packageCount is null || totalWeight is null)
        {
            return null;
        }

        var count = (int)packageCount.Value;
        var perPackage = Math.Round(totalWeight.Value / count, 3, MidpointRounding.ToZero);
        var packages = Enumerable.Range(0, count)
            .Select(index => new ShipmentPackageRequest
            {
                // The last package takes the rounding remainder so the total stays exact.
                WeightKg = index == count - 1 ? totalWeight.Value - (perPackage * (count - 1)) : perPackage,
                LengthCm = length,
                WidthCm = width,
                HeightCm = height
            })
            .ToList();

        var externalReference = raw.Get(ShipmentImportColumns.ExternalReference);
        var notes = raw.Get(ShipmentImportColumns.SenderNotes);

        return new SaveShipmentRequest
        {
            SenderCustomerId = senderId.Value,
            RecipientCustomerId = recipientId.Value,
            OriginBranchId = originBranchId,
            DestinationCityId = cityId,
            DestinationZoneId = zoneId,
            ServiceTypeId = serviceTypeId,
            PackageDescription = raw.Get(ShipmentImportColumns.PackageDescription),
            Packages = packages,
            DeclaredValue = declaredValue,
            InsuranceValue = insuranceValue,
            IsCOD = isCod,
            CODAmount = isCod ? codAmount : 0m,
            SenderNotes = notes.Length == 0 ? null : notes,
            ExternalReference = externalReference.Length == 0 ? null : externalReference
        };
    }

    private static long? ResolveCustomer(string token, string role, Lookups lookups, List<string> errors)
    {
        if (token.Length == 0)
        {
            errors.Add($"{role} مطلوب (رمز العميل أو هاتفه).");
            return null;
        }

        var byCode = lookups.Customers.Where(x => x.Code.Equals(token, StringComparison.OrdinalIgnoreCase)).ToList();
        if (byCode.Count == 1)
        {
            return byCode[0].Id;
        }

        var byPhone = lookups.Customers.Where(x => x.Phone == token).ToList();
        if (byPhone.Count == 1)
        {
            return byPhone[0].Id;
        }

        errors.Add(byPhone.Count > 1
            ? $"رقم الهاتف '{token}' مسجّل لأكثر من عميل؛ استخدم رمز العميل."
            : $"{role} '{token}' غير موجود.");
        return null;
    }

    private static decimal? ParseDecimal(RawRow raw, string column, string name, List<string> errors, bool required)
    {
        var text = NormalizeDigits(raw.Get(column));
        if (text.Length == 0)
        {
            if (required)
            {
                errors.Add($"{name} مطلوب.");
            }

            return null;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{name}: القيمة '{raw.Get(column)}' ليست رقماً.");
        return null;
    }

    private static bool ParseBool(string text, List<string> errors)
    {
        var value = text.Trim().ToLowerInvariant();
        if (TrueWords.Contains(value))
        {
            return true;
        }

        if (FalseWords.Contains(value))
        {
            return false;
        }

        errors.Add($"قيمة COD '{text}' غير مفهومة (استخدم نعم أو لا).");
        return false;
    }

    /// <summary>Arabic-Indic digits and separators -> ASCII ("١٢٫٥" -> "12.5"); thousands separators removed.</summary>
    private static string NormalizeDigits(string text)
    {
        var chars = text.Trim().Select(c => c switch
        {
            >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
            >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
            '٫' => '.',
            _ => c
        });

        return new string(chars.Where(c => c is not (',' or '٬' or ' ')).ToArray());
    }

    private static RequestValidationException FileError(string fieldName, string message) =>
        new(new Dictionary<string, string[]> { [fieldName] = new[] { message } });

    // ------------------------------------------------------------------ private types

    private sealed record RawRow(int RowNumber, IReadOnlyDictionary<string, string> Values)
    {
        public string Get(string column) => Values.TryGetValue(column, out var value) ? value.Trim() : string.Empty;
    }

    private sealed class ImportRow
    {
        public ImportRow(RawRow raw)
        {
            Raw = raw;
        }

        public RawRow Raw { get; }

        public int RowNumber => Raw.RowNumber;

        public List<string> Errors { get; } = new();

        public ShipmentDraft? Draft { get; set; }

        public bool IsValid => Errors.Count == 0 && Draft is not null;

        public ShipmentImportRowDto ToDto()
        {
            _ = decimal.TryParse(NormalizeDigits(Raw.Get(ShipmentImportColumns.PackageCount)), NumberStyles.Number, CultureInfo.InvariantCulture, out var packageCount);
            _ = decimal.TryParse(NormalizeDigits(Raw.Get(ShipmentImportColumns.TotalWeightKg)), NumberStyles.Number, CultureInfo.InvariantCulture, out var weight);

            return new ShipmentImportRowDto
            {
                RowNumber = RowNumber,
                IsValid = IsValid,
                Errors = Errors.Distinct().ToList(),
                ExternalReference = NullIfEmpty(Raw.Get(ShipmentImportColumns.ExternalReference)),
                SenderCustomerCode = NullIfEmpty(Raw.Get(ShipmentImportColumns.SenderCustomer)),
                RecipientCustomerCode = NullIfEmpty(Raw.Get(ShipmentImportColumns.RecipientCustomer)),
                DestinationCityCode = NullIfEmpty(Raw.Get(ShipmentImportColumns.DestinationCityCode)),
                ServiceTypeCode = NullIfEmpty(Raw.Get(ShipmentImportColumns.ServiceTypeCode)),
                PackageCount = packageCount is >= 0 and <= 1000 ? (int)packageCount : 0,
                WeightKg = weight,
                IsCOD = Draft?.IsCod ?? false,
                CODAmount = Draft?.CodAmount ?? 0m,
                TotalCharge = Draft?.Pricing.TotalCharge,
                CurrencyCode = Draft?.Pricing.CurrencyCode
            };
        }

        private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
    }

    private sealed record ImportBatch(long OriginBranchId, ShippingSettings Settings, IReadOnlyList<ImportRow> Rows);

    private sealed record Lookups(
        IReadOnlyList<(long Id, string Code, string Phone)> Customers,
        IReadOnlyDictionary<string, List<long>> Cities,
        IReadOnlyList<(long Id, long CityId, string Code)> Zones,
        IReadOnlyDictionary<string, long> ServiceTypes);
}
