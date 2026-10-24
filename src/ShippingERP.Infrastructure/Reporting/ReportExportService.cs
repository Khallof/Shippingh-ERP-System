using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Application.Interfaces.Reports;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Reporting;

/// <summary>
/// Excel / PDF exports of the reports (step 8). Each export calls the SAME service as its screen (same filters, same
/// permissions, same branch visibility) and turns the result into a <see cref="ReportDocument"/>.
/// </summary>
public sealed class ReportExportService : IReportExportService
{
    /// <summary>Largest list exported in one file.</summary>
    public const int MaxRows = 10_000;

    private readonly AppDbContext _dbContext;
    private readonly IShipmentService _shipments;
    private readonly IDriverManifestService _manifests;
    private readonly IFinancialReportService _financial;
    private readonly OperationalReportService _operations;
    private readonly ReportFileRenderer _renderer;
    private readonly BusinessCalendar _calendar;
    private readonly IDateTimeProvider _clock;
    private readonly BillingAccess _billing;

    public ReportExportService(
        AppDbContext dbContext,
        IShipmentService shipments,
        IDriverManifestService manifests,
        IFinancialReportService financial,
        OperationalReportService operations,
        ReportFileRenderer renderer,
        BusinessCalendar calendar,
        IDateTimeProvider clock,
        BillingAccess billing,
        IInvoiceService invoices)
    {
        _invoices = invoices;
        _dbContext = dbContext;
        _shipments = shipments;
        _manifests = manifests;
        _financial = financial;
        _operations = operations;
        _renderer = renderer;
        _calendar = calendar;
        _clock = clock;
        _billing = billing;
    }

    private static readonly ReportColumn[] OperationalColumns =
    {
        new("البند", ReportColumnKind.Text, 3),
        new("الإجمالي", ReportColumnKind.Integer),
        new("مُسلّمة", ReportColumnKind.Integer),
        new("فاشلة / مرتجعة", ReportColumnKind.Integer),
        new("قيد التنفيذ", ReportColumnKind.Integer),
        new("ملغاة", ReportColumnKind.Integer),
        new("نسبة التسليم", ReportColumnKind.Percent),
        new("الأجور", ReportColumnKind.Money, 1.4f),
        new("الدفع عند الاستلام", ReportColumnKind.Money, 1.4f)
    };

    // ------------------------------------------------------------------ shipments

    public async Task<FileDownload> ShipmentsAsync(ShipmentListQuery query, ExportFormat format, CancellationToken cancellationToken)
    {
        var rows = new List<ShipmentListItemDto>();
        query.PageSize = 100;
        for (query.PageNumber = 1; rows.Count < MaxRows; query.PageNumber++)
        {
            var page = await _shipments.GetPagedAsync(query, cancellationToken);
            rows.AddRange(page.Items);
            if (page.Items.Count < query.PageSize || query.PageNumber >= page.TotalPages)
            {
                break;
            }
        }

        var info = new List<(string, string)> { ("عدد الشحنات", rows.Count.ToString("N0", CultureInfo.InvariantCulture)) };
        if (query.Status.HasValue)
        {
            info.Add(("الحالة", rows.FirstOrDefault()?.StatusName ?? query.Status.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(query.ConsignmentNumber))
        {
            info.Add(("الإرسالية", query.ConsignmentNumber.Trim()));
        }

        if (query.CreatedFromUtc.HasValue || query.CreatedToUtc.HasValue)
        {
            info.Add(("الفترة", $"{Date(query.CreatedFromUtc)} - {Date(query.CreatedToUtc)}"));
        }

        // Step 9: amounts in different currencies are never added together. One column per currency for the
        // charges ("الأجور USD", "الأجور LYD"...) and for the COD amounts (only currencies that have COD).
        var currencies = rows.Select(x => x.CurrencyCode).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        var codCurrencies = currencies.Where(c => rows.Any(x => x.IsCOD && x.CurrencyCode == c)).ToList();

        var columns = new List<ReportColumn>
        {
            new("رقم الشحنة", ReportColumnKind.Text, 1.5f),
            new("رقم التتبع", ReportColumnKind.Text, 1.5f),
            new("الحالة"),
            new("المرسل", ReportColumnKind.Text, 1.6f),
            new("المستلم", ReportColumnKind.Text, 1.6f),
            new("هاتف المستلم", ReportColumnKind.Text, 1.2f),
            new("الفرع الحالي"),
            new("مدينة الوجهة"),
            new("نوع البضاعة"),
            new("الإرسالية", ReportColumnKind.Text, 0.8f),
            new("الطرود", ReportColumnKind.Integer, 0.6f),
            // Step 9: the quantity that was priced: kg for weight pricing, m³ for volume pricing.
            new("الوزن (كغ)", ReportColumnKind.Number, 0.7f),
            new("الحجم (م³)", ReportColumnKind.Number, 0.7f)
        };
        columns.AddRange(codCurrencies.Select(c => new ReportColumn("الدفع عند الاستلام " + c, ReportColumnKind.Money)));
        columns.AddRange(currencies.Select(c => new ReportColumn("الأجور " + c, ReportColumnKind.Money)));
        columns.Add(new ReportColumn("تاريخ الإنشاء", ReportColumnKind.DateTime, 1.3f));
        columns.Add(new ReportColumn("تاريخ التسليم", ReportColumnKind.DateTime, 1.3f));

        var tableRows = rows.Select(x =>
        {
            var row = new List<object?>
            {
                x.ShipmentNumber, x.TrackingNumber, x.StatusName, x.SenderName, x.RecipientName, x.RecipientPhone,
                x.CurrentBranchName, x.DestinationCityName, x.ServiceTypeName, x.ConsignmentNumber, x.PackageCount,
                x.PricingBasis == PricingBasis.Volume ? null : x.ChargeableWeight,
                x.PricingBasis == PricingBasis.Volume ? x.TotalVolumeCbm : null
            };
            row.AddRange(codCurrencies.Select(c => x.IsCOD && x.CurrencyCode == c ? (object?)x.CODAmount : null));
            row.AddRange(currencies.Select(c => x.CurrencyCode == c ? (object?)x.TotalCharge : null));
            row.Add(Local(x.CreatedAtUtc));
            row.Add(x.DeliveredAtUtc.HasValue ? Local(x.DeliveredAtUtc.Value) : null);
            return row.ToArray();
        }).ToList();

        var totals = new List<object?>
        {
            "الإجمالي", null, null, null, null, null, null, null, null, null, rows.Sum(x => x.PackageCount),
            rows.Where(x => x.PricingBasis != PricingBasis.Volume).Sum(x => x.ChargeableWeight),
            rows.Where(x => x.PricingBasis == PricingBasis.Volume).Sum(x => x.TotalVolumeCbm)
        };
        totals.AddRange(codCurrencies.Select(c => (object?)rows.Where(x => x.IsCOD && x.CurrencyCode == c).Sum(x => x.CODAmount)));
        totals.AddRange(currencies.Select(c => (object?)rows.Where(x => x.CurrencyCode == c).Sum(x => x.TotalCharge)));
        totals.Add(null);
        totals.Add(null);

        foreach (var currency in currencies)
        {
            var total = rows.Where(x => x.CurrencyCode == currency).Sum(x => x.TotalCharge);
            info.Add(("إجمالي الأجور " + currency, total.ToString("#,##0.00", CultureInfo.InvariantCulture)));
        }

        var report = new ReportDocument
        {
            Title = "قائمة الشحنات",
            FileName = "shipments-" + Stamp(),
            Landscape = true,
            Info = info,
            Sections = new[]
            {
                new ReportSection
                {
                    Columns = columns,
                    Rows = tableRows,
                    Totals = totals.ToArray()
                }
            }
        };

        return await RenderAsync(report, format, cancellationToken);
    }

    // ------------------------------------------------------------------ invoices (step 9)

    public async Task<FileDownload> InvoicesAsync(InvoiceListQuery query, ExportFormat format, CancellationToken cancellationToken)
    {
        var rows = new List<InvoiceListItemDto>();
        query.PageSize = 100;
        for (query.PageNumber = 1; rows.Count < MaxRows; query.PageNumber++)
        {
            var page = await _invoices.GetPagedAsync(query, cancellationToken);
            rows.AddRange(page.Items);
            if (page.Items.Count < query.PageSize || query.PageNumber >= page.TotalPages)
            {
                break;
            }
        }

        // Amounts in different currencies are never added together: totals only when there is one currency.
        var oneCurrency = rows.Select(x => x.CurrencyCode).Distinct().Count() <= 1;
        var report = new ReportDocument
        {
            Title = "قائمة الفواتير",
            FileName = "invoices-" + Stamp(),
            Landscape = true,
            Info = new List<(string, string)> { ("عدد الفواتير", rows.Count.ToString("N0", CultureInfo.InvariantCulture)) },
            Sections = new[]
            {
                new ReportSection
                {
                    Columns = new ReportColumn[]
                    {
                        new("رقم الفاتورة", ReportColumnKind.Text, 1.3f),
                        new("الحالة"),
                        new("العميل", ReportColumnKind.Text, 2f),
                        new("الفرع"),
                        new("التاريخ", ReportColumnKind.Text, 1f),
                        new("الاستحقاق", ReportColumnKind.Text, 1f),
                        new("الشحنات", ReportColumnKind.Integer, 0.6f),
                        new("الإجمالي", ReportColumnKind.Money),
                        new("المدفوع", ReportColumnKind.Money),
                        new("المتبقي", ReportColumnKind.Money),
                        new("العملة", ReportColumnKind.Text, 0.6f)
                    },
                    Rows = rows.Select(x => new object?[]
                    {
                        x.InvoiceNumber ?? "مسودة", x.StatusName, $"{x.CustomerName} ({x.CustomerCode})", x.BranchName,
                        Date(x.InvoiceDateUtc), Date(x.DueDateUtc), x.LineCount, x.TotalAmount, x.PaidAmount, x.BalanceDue, x.CurrencyCode
                    }).ToList(),
                    Totals = oneCurrency && rows.Count > 0
                        ? new object?[]
                        {
                            "الإجمالي", null, null, null, null, null, rows.Sum(x => x.LineCount), rows.Sum(x => x.TotalAmount),
                            rows.Sum(x => x.PaidAmount), rows.Sum(x => x.BalanceDue), rows[0].CurrencyCode
                        }
                        : null
                }
            }
        };

        return await RenderAsync(report, format, cancellationToken);
    }

    private readonly IInvoiceService _invoices;

    public async Task<FileDownload> OperationalReportAsync(OperationalReportQuery query, ExportFormat format, CancellationToken cancellationToken)
    {
        var result = await _operations.GetAsync(query, cancellationToken);

        object?[] Row(OperationalGroupRowDto x) =>
            new object?[] { x.Name, x.Total, x.Delivered, x.Failed, x.InProgress, x.Cancelled, x.DeliveredPercent, x.TotalCharges, x.CodAmount };

        ReportSection Section(string title, IReadOnlyList<OperationalGroupRowDto> rows) => new()
        {
            Title = title,
            Columns = OperationalColumns,
            Rows = rows.Select(Row).ToList()
        };

        var report = new ReportDocument
        {
            Title = "التقرير التشغيلي للشحنات",
            FileName = "operations-report-" + Stamp(),
            Landscape = true,
            Info = Combine(await PeriodInfoAsync(result.FromDate, result.ToDate, query.BranchId, cancellationToken), ("العملة", result.CurrencyCode)),
            Sections = new[]
            {
                new ReportSection { Title = "الإجمالي", Columns = OperationalColumns, Rows = new[] { Row(result.Totals) } },
                new ReportSection
                {
                    Title = "حسب الحالة الحالية",
                    Columns = new ReportColumn[] { new("الحالة", ReportColumnKind.Text, 3), new("العدد", ReportColumnKind.Integer) },
                    Rows = result.ByStatus.Select(x => new object?[] { x.StatusName, x.Count }).ToList(),
                    Totals = new object?[] { "الإجمالي", result.ByStatus.Sum(x => x.Count) }
                },
                Section("حسب الفرع (فرع الانطلاق)", result.ByBranch),
                Section("حسب السائق", result.ByDriver),
                Section("حسب نوع الخدمة", result.ByServiceType),
                Section("حسب المرسل (أعلى 50)", result.ByCustomer),
                new ReportSection
                {
                    Title = "حسب اليوم",
                    Columns = new ReportColumn[] { new("التاريخ", ReportColumnKind.Date, 2), new("أُنشئت", ReportColumnKind.Integer), new("سُلّمت", ReportColumnKind.Integer) },
                    Rows = result.Daily.Select(x => new object?[] { x.Date, x.Created, x.Delivered }).ToList(),
                    Totals = new object?[] { "الإجمالي", result.Daily.Sum(x => x.Created), result.Daily.Sum(x => x.Delivered) }
                }
            }
        };

        return await RenderAsync(report, format, cancellationToken);
    }

    // ------------------------------------------------------------------ accounting

    public async Task<FileDownload> TrialBalanceAsync(TrialBalanceQuery query, ExportFormat format, CancellationToken cancellationToken)
    {
        var result = await _financial.GetTrialBalanceAsync(query, cancellationToken);

        var report = new ReportDocument
        {
            Title = "ميزان المراجعة",
            FileName = "trial-balance-" + Stamp(),
            Landscape = true,
            MoneyDecimals = await BaseDecimalsAsync(cancellationToken),
            Info = Combine(await PeriodInfoAsync(result.FromDate, result.ToDate, query.BranchId, cancellationToken),
                ("العملة", result.BaseCurrencyCode), ("متوازن", result.IsBalanced ? "نعم" : "لا")),
            Sections = new[]
            {
                new ReportSection
                {
                    Columns = new ReportColumn[]
                    {
                        new("رقم الحساب", ReportColumnKind.Text, 0.9f),
                        new("اسم الحساب", ReportColumnKind.Text, 2.6f),
                        new("افتتاحي مدين", ReportColumnKind.Money),
                        new("افتتاحي دائن", ReportColumnKind.Money),
                        new("حركة مدين", ReportColumnKind.Money),
                        new("حركة دائن", ReportColumnKind.Money),
                        new("ختامي مدين", ReportColumnKind.Money),
                        new("ختامي دائن", ReportColumnKind.Money)
                    },
                    Rows = result.Rows.Select(x => new object?[]
                    {
                        x.Code, x.ArabicName, x.OpeningDebit, x.OpeningCredit, x.PeriodDebit, x.PeriodCredit, x.ClosingDebit, x.ClosingCredit
                    }).ToList(),
                    Totals = new object?[]
                    {
                        null, "الإجمالي", result.TotalOpeningDebit, result.TotalOpeningCredit, result.TotalPeriodDebit,
                        result.TotalPeriodCredit, result.TotalClosingDebit, result.TotalClosingCredit
                    }
                }
            },
            Signatures = new[] { "المحاسب", "المدير المالي" }
        };

        return await RenderAsync(report, format, cancellationToken);
    }

    public async Task<FileDownload> GeneralLedgerAsync(GeneralLedgerQuery query, ExportFormat format, CancellationToken cancellationToken)
    {
        var all = new GeneralLedgerQuery
        {
            AccountId = query.AccountId,
            FromDate = query.FromDate,
            ToDate = query.ToDate,
            BranchId = query.BranchId,
            CostCenterId = query.CostCenterId,
            PageNumber = 1,
            PageSize = MaxRows
        };
        var result = await _financial.GetGeneralLedgerAsync(all, cancellationToken);
        var lines = result.Lines.Items;

        var report = new ReportDocument
        {
            Title = $"دفتر الأستاذ: {result.AccountCode} - {result.AccountName}",
            FileName = $"general-ledger-{result.AccountCode}-{Stamp()}",
            Landscape = true,
            MoneyDecimals = await BaseDecimalsAsync(cancellationToken),
            Info = Combine(await PeriodInfoAsync(result.FromDate, result.ToDate, query.BranchId, cancellationToken),
                ("العملة", result.BaseCurrencyCode), ("يشمل الحسابات الفرعية", result.IncludesSubAccounts ? "نعم" : "لا")),
            Sections = new[]
            {
                new ReportSection
                {
                    Columns = new ReportColumn[]
                    {
                        new("التاريخ", ReportColumnKind.Date, 0.9f),
                        new("رقم القيد", ReportColumnKind.Text, 1.1f),
                        new("الحساب", ReportColumnKind.Text, 0.7f),
                        new("البيان", ReportColumnKind.Text, 3f),
                        new("المستند", ReportColumnKind.Text, 1.1f),
                        new("الفرع", ReportColumnKind.Text, 1f),
                        new("مدين", ReportColumnKind.Money),
                        new("دائن", ReportColumnKind.Money),
                        new("الرصيد", ReportColumnKind.Money)
                    },
                    Rows = new[] { new object?[] { null, null, null, "الرصيد الافتتاحي", null, null, null, null, result.OpeningBalance } }
                        .Concat(lines.Select(x => new object?[]
                        {
                            x.EntryDate, x.EntryNumber, x.AccountCode, x.LineDescription ?? x.Description, x.SourceNumber ?? x.Reference,
                            x.BranchName, x.Debit == 0m ? null : x.Debit, x.Credit == 0m ? null : x.Credit, x.RunningBalance
                        }))
                        .ToList(),
                    Totals = new object?[] { null, null, null, "الإجمالي / الرصيد الختامي", null, null, result.PeriodDebit, result.PeriodCredit, result.ClosingBalance }
                }
            }
        };

        return await RenderAsync(report, format, cancellationToken);
    }

    public async Task<FileDownload> ProfitLossAsync(ProfitLossQuery query, ExportFormat format, CancellationToken cancellationToken)
    {
        var result = await _financial.GetProfitLossAsync(query, cancellationToken);
        var columns = new ReportColumn[]
        {
            new("التصنيف", ReportColumnKind.Text, 2f), new("رقم الحساب", ReportColumnKind.Text, 0.9f), new("اسم الحساب", ReportColumnKind.Text, 2.4f), new("المبلغ", ReportColumnKind.Money, 1.2f)
        };

        ReportSection Section(string title, IReadOnlyList<StatementGroupDto> groups, decimal total) => new()
        {
            Title = title,
            Columns = columns,
            Rows = groups.SelectMany(g => g.Accounts.Select(a => new object?[] { g.ArabicName, a.Code, a.ArabicName, a.Amount })).ToList(),
            Totals = new object?[] { "إجمالي " + title, null, null, total }
        };

        var report = new ReportDocument
        {
            Title = "قائمة الدخل",
            FileName = "profit-loss-" + Stamp(),
            MoneyDecimals = await BaseDecimalsAsync(cancellationToken),
            Info = Combine(await PeriodInfoAsync(result.FromDate, result.ToDate, query.BranchId, cancellationToken), ("العملة", result.BaseCurrencyCode)),
            Sections = new[]
            {
                Section("الإيرادات", result.Revenue, result.TotalRevenue),
                Section("تكلفة الإيرادات", result.CostOfRevenue, result.TotalCostOfRevenue),
                Section("المصاريف", result.Expenses, result.TotalExpenses)
            },
            Summary = new[]
            {
                ("مجمل الربح", Money(result.GrossProfit)),
                ("صافي الربح (الخسارة)", Money(result.NetProfit)),
                ("هامش صافي الربح", result.NetProfitMarginPercent.HasValue ? result.NetProfitMarginPercent.Value.ToString("0.##", CultureInfo.InvariantCulture) + "%" : "-")
            }
        };

        return await RenderAsync(report, format, cancellationToken);
    }

    public async Task<FileDownload> BalanceSheetAsync(BalanceSheetQuery query, ExportFormat format, CancellationToken cancellationToken)
    {
        var result = await _financial.GetBalanceSheetAsync(query, cancellationToken);
        var columns = new ReportColumn[]
        {
            new("التصنيف", ReportColumnKind.Text, 2f), new("رقم الحساب", ReportColumnKind.Text, 0.9f), new("اسم الحساب", ReportColumnKind.Text, 2.4f), new("الرصيد", ReportColumnKind.Money, 1.2f)
        };

        ReportSection Section(string title, IEnumerable<object?[]> rows, decimal total) => new()
        {
            Title = title,
            Columns = columns,
            Rows = rows.ToList(),
            Totals = new object?[] { "إجمالي " + title, null, null, total }
        };

        IEnumerable<object?[]> Rows(IReadOnlyList<StatementGroupDto> groups) =>
            groups.SelectMany(g => g.Accounts.Select(a => new object?[] { g.ArabicName, a.Code, a.ArabicName, a.Amount }));

        var equityRows = Rows(result.Equity)
            .Append(new object?[] { "الأرباح", null, "أرباح سنوات سابقة (غير مقفلة)", result.PriorYearsProfit })
            .Append(new object?[] { "الأرباح", null, "ربح السنة الحالية", result.CurrentYearProfit });

        var info = new List<(string, string)> { ("كما في", Date(result.AsOfDate)), ("العملة", result.BaseCurrencyCode), ("متوازنة", result.IsBalanced ? "نعم" : "لا") };
        if (query.BranchId.HasValue)
        {
            info.Add(("الفرع", await BranchNameAsync(query.BranchId.Value, cancellationToken)));
        }

        var report = new ReportDocument
        {
            Title = "الميزانية العمومية",
            FileName = "balance-sheet-" + Stamp(),
            MoneyDecimals = await BaseDecimalsAsync(cancellationToken),
            Info = info,
            Sections = new[]
            {
                Section("الأصول", Rows(result.Assets), result.TotalAssets),
                Section("الخصوم", Rows(result.Liabilities), result.TotalLiabilities),
                Section("حقوق الملكية", equityRows, result.TotalEquity)
            },
            Summary = new[]
            {
                ("إجمالي الأصول", Money(result.TotalAssets)),
                ("إجمالي الخصوم وحقوق الملكية", Money(result.TotalLiabilitiesAndEquity))
            },
            Signatures = new[] { "المحاسب", "المدير المالي" }
        };

        return await RenderAsync(report, format, cancellationToken);
    }

    // ------------------------------------------------------------------ delivery manifest

    public async Task<FileDownload> DriverManifestPdfAsync(long manifestId, CancellationToken cancellationToken)
    {
        var manifest = await _manifests.GetByIdAsync(manifestId, cancellationToken);
        var items = (manifest.Items ?? Array.Empty<DriverManifestItemDto>()).OrderBy(x => x.Sequence).ToList();

        var report = new ReportDocument
        {
            Title = "كشف توزيع السائق",
            FileName = "manifest-" + SafeName(manifest.ManifestNumber),
            Landscape = true,
            QrText = manifest.ManifestNumber,
            Info = new List<(string, string)>
            {
                ("رقم الكشف", manifest.ManifestNumber),
                ("التاريخ", Date(Local(manifest.ManifestDateUtc))),
                ("الفرع", manifest.BranchName),
                ("السائق", manifest.DriverName),
                ("المركبة", manifest.VehiclePlateNumber ?? "-"),
                ("الحالة", ManifestStatus(manifest.Status))
            },
            Sections = new[]
            {
                new ReportSection
                {
                    Columns = new ReportColumn[]
                    {
                        new("#", ReportColumnKind.Integer, 0.4f),
                        new("رقم التتبع", ReportColumnKind.Text, 1.4f),
                        new("المستلم", ReportColumnKind.Text, 1.6f),
                        new("الهاتف", ReportColumnKind.Text, 1.2f),
                        new("العنوان", ReportColumnKind.Text, 2.6f),
                        new("الطرود", ReportColumnKind.Integer, 0.6f),
                        new("المبلغ المطلوب", ReportColumnKind.Money, 1.1f),
                        new("النتيجة", ReportColumnKind.Text, 0.9f),
                        new("توقيع المستلم", ReportColumnKind.Text, 1.4f)
                    },
                    Rows = items.Select(x => new object?[]
                    {
                        x.Sequence, x.TrackingNumber, x.RecipientName, x.RecipientPhone,
                        string.Join(" - ", new[] { x.DestinationCityName, x.DestinationZoneName, x.RecipientAddressLine }.Where(s => !string.IsNullOrWhiteSpace(s))),
                        x.PackageCount, x.IsCOD ? x.CODAmount : null, Result(x.Result), null
                    }).ToList(),
                    Totals = new object?[]
                    {
                        null, $"{items.Count} شحنة", null, null, null, items.Sum(x => x.PackageCount), items.Where(x => x.IsCOD).Sum(x => x.CODAmount), null, null
                    }
                }
            },
            Summary = new[]
            {
                ("مُسلّمة", manifest.DeliveredCount.ToString(CultureInfo.InvariantCulture)),
                ("فاشلة", manifest.FailedCount.ToString(CultureInfo.InvariantCulture)),
                ("قيد التوزيع", manifest.PendingCount.ToString(CultureInfo.InvariantCulture)),
                ("إجمالي مبالغ التحصيل", Money(manifest.TotalCodAmount))
            },
            Signatures = new[] { "السائق (استلام الشحنات)", "مسؤول الفرع", "أمين الصندوق (تسليم المبالغ)" }
        };

        return await RenderAsync(report, ExportFormat.Pdf, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<FileDownload> RenderAsync(ReportDocument report, ExportFormat format, CancellationToken cancellationToken)
    {
        var company = await CashDocumentPdfRenderer.LoadCompanyAsync(_dbContext, cancellationToken);
        return _renderer.Render(report, format, company, Local(_clock.UtcNow));
    }

    private async Task<List<(string Label, string Value)>> PeriodInfoAsync(DateTime from, DateTime to, long? branchId, CancellationToken cancellationToken)
    {
        var info = new List<(string, string)> { ("الفترة", $"{Date(from)} - {Date(to)}") };
        info.Add(("الفرع", branchId.HasValue ? await BranchNameAsync(branchId.Value, cancellationToken) : "كل الفروع"));
        return info;
    }

    private static List<(string Label, string Value)> Combine(List<(string Label, string Value)> info, params (string Label, string Value)[] more)
    {
        info.AddRange(more);
        return info;
    }

    private async Task<string> BranchNameAsync(long branchId, CancellationToken cancellationToken) =>
        await _dbContext.Branches.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == branchId).Select(x => x.ArabicName).FirstOrDefaultAsync(cancellationToken)
        ?? branchId.ToString(CultureInfo.InvariantCulture);

    private async Task<int> BaseDecimalsAsync(CancellationToken cancellationToken) =>
        (await _billing.GetBaseCurrencyAsync(cancellationToken)).DecimalPlaces;

    private DateTime Local(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _calendar.Zone);

    private string Stamp() => Local(_clock.UtcNow).ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);

    private static string Date(DateTime? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "-";

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    private static string SafeName(string value) =>
        new(value.Select(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-' ? ch : '-').ToArray());

    private static string Result(DeliveryResult result) => result switch
    {
        DeliveryResult.Delivered => "مُسلّمة",
        DeliveryResult.Failed => "فاشلة",
        _ => string.Empty
    };

    private static string ManifestStatus(DriverManifestStatus status) => status switch
    {
        DriverManifestStatus.Draft => "مسودة",
        DriverManifestStatus.Dispatched => "مع السائق",
        DriverManifestStatus.Closed => "مغلق",
        DriverManifestStatus.Cancelled => "ملغى",
        _ => status.ToString()
    };
}
