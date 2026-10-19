namespace ShippingERP.Infrastructure.Shipments.Import;

/// <summary>One column of the import sheet. Header = Key (English, row 1); Arabic help is in the Instructions sheet.</summary>
public sealed record ShipmentImportColumn(string Key, bool Required, string ArabicDescription, string Example);

/// <summary>The columns of the shipment import template, in order.</summary>
public static class ShipmentImportColumns
{
    public const string ExternalReference = "ExternalReference";
    public const string SenderCustomer = "SenderCustomer";
    public const string RecipientCustomer = "RecipientCustomer";
    public const string DestinationCityCode = "DestinationCityCode";
    public const string DestinationZoneCode = "DestinationZoneCode";
    public const string ServiceTypeCode = "ServiceTypeCode";
    public const string PackageCount = "PackageCount";
    public const string TotalWeightKg = "TotalWeightKg";
    public const string LengthCm = "LengthCm";
    public const string WidthCm = "WidthCm";
    public const string HeightCm = "HeightCm";
    public const string PackageDescription = "PackageDescription";
    public const string DeclaredValue = "DeclaredValue";
    public const string InsuranceValue = "InsuranceValue";
    public const string IsCod = "IsCOD";
    public const string CodAmount = "CODAmount";
    public const string SenderNotes = "SenderNotes";

    public static IReadOnlyList<ShipmentImportColumn> All { get; } = new List<ShipmentImportColumn>
    {
        new(ExternalReference, false, "رقم الطلب لدى المرسل (اختياري، لا يتكرر لنفس المرسل).", "ORD-1001"),
        new(SenderCustomer, true, "رمز العميل المرسل (CUS-00003) أو رقم هاتفه كما هو مسجّل.", "CUS-00003"),
        new(RecipientCustomer, true, "رمز العميل المستلم أو رقم هاتفه. يُستخدم عنوانه الافتراضي.", "CUS-00002"),
        new(DestinationCityCode, false, "رمز مدينة الوجهة (مثل DAM). فارغ = مدينة عنوان المستلم.", "DAM"),
        new(DestinationZoneCode, false, "رمز المنطقة داخل مدينة الوجهة (اختياري).", "MALKI"),
        new(ServiceTypeCode, true, "رمز نوع الخدمة (STD عادي، EXP سريع، SDD نفس اليوم).", "STD"),
        new(PackageCount, true, "عدد الطرود (1 إلى 100). يُقسم الوزن الكلي عليها بالتساوي.", "1"),
        new(TotalWeightKg, true, "الوزن الكلي بالكيلوغرام (حتى 3 منازل عشرية).", "2.5"),
        new(LengthCm, false, "الطول بالسنتيمتر لكل طرد (أدخل الأبعاد الثلاثة أو اتركها فارغة).", "30"),
        new(WidthCm, false, "العرض بالسنتيمتر لكل طرد.", "20"),
        new(HeightCm, false, "الارتفاع بالسنتيمتر لكل طرد.", "15"),
        new(PackageDescription, true, "وصف المحتوى.", "ملابس"),
        new(DeclaredValue, false, "القيمة المصرّح بها (0 إذا فارغ).", "50000"),
        new(InsuranceValue, false, "قيمة التأمين (لا تزيد عن القيمة المصرّح بها).", "0"),
        new(IsCod, false, "الدفع عند الاستلام: نعم / لا (أو 1 / 0، Yes / No).", "نعم"),
        new(CodAmount, false, "مبلغ التحصيل عند الاستلام (مطلوب إذا كانت COD = نعم).", "125000"),
        new(SenderNotes, false, "ملاحظات تُطبع على الملصق (اختياري).", "اتصل قبل التوصيل")
    };
}
