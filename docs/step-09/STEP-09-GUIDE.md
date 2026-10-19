# الخطوة 9: التسعير بالوزن أو بالحجم، عدد الطرود، والأجور على المستلم

هذه الخطوة تجعل البرنامج يعمل بطريقة الشحن عندك:
- بضاعة تُسعَّر **بالمتر المكعب** (مثلاً 95$ للمتر).
- وبضاعة تُسعَّر **بالكيلو** (مثلاً الألبسة 4 دينار والبضاعة الأخرى 8 دينار).
- **عدد الطرود** يُكتب رقماً واحداً.
- **أجور الشحن على المستلم** هي الاختيار الافتراضي.

---

## 1. ما الذي تغيّر؟

| الطلب | ما تم |
|---|---|
| أسعّر مرة بالوزن ومرة بالحجم | حقل جديد في الشحنة: **طريقة التسعير: بالوزن / بالحجم**. قاعدة السعر فيها **سعر المتر المكعب**، ويمكن أن تُطبَّق على الوزن فقط أو الحجم فقط. |
| عدد الطرود رقم وليس طرداً طرداً | في نموذج الشحنة: **عدد الطرود + الوزن الكلي + الحجم الكلي (م³)**. البرنامج ينشئ الطرود (لكل طرد باركود وملصق) ويوزّع الوزن عليها بالتساوي. |
| الدفع عند المستلم | حقل جديد: **أجور الشحن يدفعها: المستلم / المرسل**، والافتراضي **المستلم**. فاتورة الأجور تخرج **باسم المستلم**، ولا تُخصم من مبلغ تحصيل المرسل، وتظهر على الملصق وشاشة السائق «أجور الشحن على المستلم». |
| إلغاء نوع الخدمة | الحقل صار اسمه **«نوع البضاعة»** (ألبسة، بضاعة عامة، أثاث...). ما زلت تحتاجه لأن سعر الكيلو يختلف حسب نوع البضاعة (4 أو 8). |
| إلغاء المرجع الخارجي | حُذف من شاشة الشحنة. الشحنات القديمة التي لها مرجع تحتفظ به. |

### كيف يحسب البرنامج السعر الآن

- **بالوزن:** الوزن المحتسب = الوزن الكلي مقرّباً للأعلى حسب «تقريب الوزن» في الإعدادات، ثم **× سعر الكيلو** من شرائح الوزن في القاعدة. مثال: 12.5 كغ ألبسة × 4 = **50 دينار**.
- **بالحجم:** **الحجم (م³) × سعر المتر المكعب** في القاعدة، والوزن هنا لا يدخل في السعر. مثال: 1.75 م³ × 95 = **166.25$**.
- يُضاف إلى ذلك (إن وُجد في القاعدة) السعر الأساسي والرسوم الإضافية والضريبة.

### كيف يختار البرنامج قائمة الأسعار (وعملتها)

يجرّب الخطط بالترتيب التالي، وأول خطة فيها قاعدة مطابقة هي التي تسعّر:
1. عقد الزبون الخاص.
2. الخطة المعيّنة للزبون.
3. الخطة **الافتراضية**.
4. **جديد:** باقي الخطط العامة النشطة (غير المعيّنة لأي زبون)، بترتيب إنشائها.

لذلك يمكنك أن تجعل خطة **«أسعار الكيلو» بالدينار** افتراضية، وتضيف بجانبها خطة **«أسعار المتر» بالدولار**.
- شحنة بالحجم لا تطابق قواعد الكيلو، فتنتقل تلقائياً إلى خطة الدولار.
- عملة الشحنة هي عملة الخطة التي سعّرتها.
- فواتير الدولار وفواتير الدينار منفصلة دائماً ولا تُجمع معاً.

---

## 2. تطبيق الخطوة (PowerShell)

> أوقف الـ API و`npm run dev` أولاً (Ctrl+C).

```powershell
# 1) فك الملف في مجلد جديد
$zip = Get-ChildItem "$HOME\Downloads\ShippingERP-Step09-full*.zip" | Select-Object -First 1
Unblock-File $zip.FullName
Expand-Archive $zip.FullName -DestinationPath "$HOME\Downloads\ShippingERP-Step09" -Force

# 2) انسخ مجلد Migrations من الخطوة السابقة
Copy-Item -Recurse -Force "$HOME\Downloads\ShippingERP-Step08\src\ShippingERP.Infrastructure\Persistence\Migrations" `
  "$HOME\Downloads\ShippingERP-Step09\src\ShippingERP.Infrastructure\Persistence\"

cd "$HOME\Downloads\ShippingERP-Step09"

# 3) البناء والاختبارات (الاختبارات زادت 4)
dotnet build --no-incremental
dotnet test --no-build

# 4) Migration جديد لأعمدة الخطوة 9 ثم تحديث قاعدة البيانات (البيانات الحالية تبقى كما هي)
dotnet ef migrations add Step09_CargoPricing --project src/ShippingERP.Infrastructure --startup-project src/ShippingERP.API
dotnet ef database update --project src/ShippingERP.Infrastructure --startup-project src/ShippingERP.API

# 5) التشغيل
dotnet run --project src/ShippingERP.API --launch-profile http
```

ثم في نافذة ثانية:

```powershell
cd "$HOME\Downloads\ShippingERP-Step09\frontend"
npm install
npm run dev
```

**ملاحظات:**
- الـ Migration يضيف أعمدة فقط:
  - `Shipments`: TotalVolumeCbm، PricingBasis، ChargesPaidBy.
  - `PriceRules`: PricePerCubicMeter، AppliesToBasis.
  - `ShipmentPricingSnapshots`: أربعة أعمدة للحجم.
- الشحنات القديمة تصبح «بالوزن» و«على المرسل»، فلا يتغيّر سعر أي شحنة سابقة.
- إعدادات user-secrets (المدير، وبيانات العرض بدون عملاء) محفوظة على جهازك وتعمل في المجلد الجديد كما هي.

---

## 3. إعداد الأسعار لطريقتك (مرة واحدة)

### 3.1 العملات
من **البيانات الأساسية ← العملات**:
- أضف **الدينار الليبي**: الرمز `LYD`، الاسم «دينار ليبي»، الرمز المختصر «د.ل»، عدد الكسور 3 (أو 2).
- **الدولار `USD`** موجود.

> **العملة الأساسية** (التي تُكتب بها الحسابات) هي حالياً الليرة السورية من بيانات العرض. إن كنت تريد الدينار أساسياً، أخبرني **قبل** إدخال فواتير حقيقية، لأن تغيير العملة الأساسية بعد ذلك يغيّر الأرصدة.

### 3.2 أنواع البضاعة
من **البيانات الأساسية ← أنواع البضاعة** عدّل الأنواع الموجودة أو أضف أنواعاً جديدة، مثلاً:

| الرمز | الاسم |
|---|---|
| CLOTH | ألبسة |
| GOODS | بضاعة عامة |
| CBM | بضاعة بالمتر (أثاث، كراتين كبيرة...) |

(الأنواع القديمة «توصيل عادي / سريع / نفس اليوم» يمكنك إعادة تسميتها أو إيقافها.)

### 3.3 خطة «أسعار الكيلو» بالدينار
من **خطط وقواعد التسعير**:
1. أنشئ خطة جديدة:
   - الرمز `KG-LYD`، الاسم «أسعار الكيلو».
   - العملة **LYD**.
   - ضع علامة **افتراضية**.
2. أضف قاعدة «ألبسة 4 د.ل»:
   - **تُطبَّق على:** بالوزن فقط.
   - **شرط:** نوع البضاعة = ألبسة.
   - السعر الأساسي **0**، الوزن المشمول **0**.
   - شريحة وزن: من **0** إلى (فارغ)، سعر الكغ **4**.
3. أضف قاعدة «بضاعة عامة 8 د.ل»:
   - **تُطبَّق على:** بالوزن فقط.
   - **شرط:** نوع البضاعة = بضاعة عامة.
   - السعر الأساسي **0**، الوزن المشمول **0**.
   - شريحة وزن: من **0**، سعر الكغ **8**.

### 3.4 خطة «أسعار المتر» بالدولار
1. أنشئ خطة جديدة:
   - الرمز `CBM-USD`، الاسم «أسعار المتر المكعب».
   - العملة **USD**.
   - **غير** افتراضية، و**لا تعيّنها** لأي زبون.
2. أضف قاعدة «95$ للمتر»:
   - **تُطبَّق على:** بالحجم فقط.
   - **سعر المتر المكعب:** **95**.
   - السعر الأساسي **0**، الوزن المشمول **0**.
   - بلا شروط (أو أضف شرط نوع البضاعة إن أردت).

> اترك حقل **«خطة التسعير»** فارغاً في بطاقة الزبون (إلا لزبون له أسعار خاصة)، فيأخذ البرنامج الخطة الافتراضية ثم خطة المتر تلقائياً.

### 3.5 أوقف خطة العرض القديمة
خطة **GENERAL «الأسعار العامة»** من بيانات العرض فيها قواعد تطابق كل الشحنات. **أوقفها** (إلغاء التفعيل)، وإلا سعّرت شحنات الحجم بالليرة قبل أن يصل البرنامج إلى خطة الدولار.

---

## 4. العمل اليومي

### تسجيل شحنة
**الشحنات ← شحنة جديدة**:
1. **المرسل والمستلم** والعنوان.
2. **المسار:** فرع الإنشاء ومدينة الوجهة.
3. **البضاعة والتسعير:**
   - وصف المحتوى، ونوع البضاعة.
   - **عدد الطرود** (مثلاً 12)، و**الوزن الكلي** بالكيلو.
   - **الحجم الكلي (م³)**: إجباري عند التسعير بالحجم.
   - **طريقة التسعير:** «بالوزن (سعر الكيلو)» أو «بالحجم (سعر المتر المكعب)».
4. **الدفع والتحصيل:**
   - **أجور الشحن يدفعها:** «المستلم» جاهز افتراضياً. اختر «المرسل» إذا دفع المرسل الأجور مقدماً.
   - **تحصيل ثمن البضاعة للمرسل:** فقط إذا كان المستلم سيدفع ثمن البضاعة أيضاً لتسلّمه للمرسل.
5. اضغط **احسب السعر**: ترى طريقة التسعير والحجم أو الوزن وسعر المتر أو الكيلو والإجمالي وعملته. ثم **تسجيل الشحنة**.

### عند التسليم: المستلم يدفع الأجور
- الملصق وشاشة السائق ونافذة تأكيد التسليم تُظهر **«أجور الشحن على المستلم: المبلغ»**.
- بعد أن يدفع المستلم:
  1. افتح الشحنة ← **إنشاء فاتورة**. الفاتورة تخرج **باسم المستلم** تلقائياً.
  2. أصدر الفاتورة.
  3. سجّل الدفعة بـ **تسجيل دفعة** من صفحة الفاتورة.
- يمكنك أيضاً عمل **فاتورة مجمّعة** للمستلم (كل شحناته التي أجورها عليه) من شاشة الفواتير باختيار المستلم كعميل.
- كشف حساب المستلم وأعمار ديونه يظهران من **العملاء ← كشف الحساب** كأي زبون.

> شحنة أجورها على المستلم **لا تظهر** في فواتير المرسل، ولا تُخصم أجورها من تسوية مبلغ التحصيل للمرسل.

---

## 5. قائمة فحص سريعة

1. [ ] `dotnet test` كله ناجح.
2. [ ] `database update` نجح دون أخطاء.
3. [ ] أنشئ عملة LYD، ونوعي بضاعة (ألبسة، بضاعة عامة)، والخطتين، وأوقف خطة GENERAL.
4. [ ] شحنة ألبسة: 3 طرود، 12.5 كغ، بالوزن. «احسب السعر» يجب أن يعطي **50 LYD**.
5. [ ] شحنة بضاعة عامة: 12.5 كغ بالوزن = **100 LYD**.
6. [ ] شحنة بالحجم: 1.75 م³ = **166.25 USD**.
7. [ ] بعد التسجيل: تبويب «الطرود» فيه 3 طرود لكل منها باركود، ومجموع أوزانها 12.5.
8. [ ] طباعة الملصق: يظهر «أجور الشحن على المستلم» والمبلغ.
9. [ ] إنشاء فاتورة من الشحنة: اسم العميل في الفاتورة هو **المستلم**.
10. [ ] محاولة فاتورة مجمّعة للمرسل تضم هذه الشحنة: تُرفض بسبب «أجورها على المستلم».
11. [ ] شحنة قديمة (من قبل الخطوة 9): تفتح وتظهر «بالوزن / على المرسل» وسعرها لم يتغيّر.

---

## 6. الملفات الكاملة (43 ملفاً جديداً أو معدّلاً)

كل ملف كامل كما هو في المشروع. المسارات نسبية من المجلد الجذر للحل. `frontend/package-lock.json` (قفل الإصدارات، يولّده npm) موجود في ZIP فقط.

### 6.1 `src/ShippingERP.Domain/Entities/Pricing/PriceRule.cs`

```csharp
using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Pricing;

/// <summary>
/// One price rule of a pricing plan (قاعدة تسعير). Replaces the step-3 CustomerPricingPlanRule.
///
/// WHEN it applies: its <see cref="Conditions"/> (zones, cities, service type, weight range, package count).
/// When several active rules match a shipment, the highest Priority wins, then the most specific rule
/// (the one with more condition types), then the oldest rule (lowest Id).
///
/// WHAT it charges (all amounts in the plan currency):
///   Base charge      = BasePrice (covers the first IncludedWeightKg and IncludedPackageCount)
///   Weight charge    = sum of the weight tiers above IncludedWeightKg (progressive, like tax brackets)
///   Package fee      = (packages - IncludedPackageCount) x PerPackageFee
///   Insurance fee    = max(InsuranceFeeMinimum, insurance value x InsuranceFeePercentage %)   (insured shipments)
///   Pickup fee       = PickupFee        (shipments with RequiresPickup)
///   Packaging fee    = PackagingFee     (shipments with RequiresPackaging)
///   Remote area fee  = RemoteAreaFee    (destination zone marked IsRemoteArea)
///   Return fee       = ReturnFee        (shipments being returned to the sender)
///   Contract discount= DiscountPercentage % of the shipping subtotal
///   COD commission   = max(CodFeeMinimum, CodFeeFixed + COD amount x CodFeePercentage %)   (COD shipments)
///   Tax              = TaxRate (or the default tax rate when empty) on (subtotal - discount + COD commission)
///
/// Shipments keep the values used at pricing time in ShipmentPricingSnapshot, so editing a rule never changes the
/// price of an existing shipment.
/// </summary>
public class PriceRule : SoftDeletableEntity
{
    public long PricingPlanId { get; set; }

    public CustomerPricingPlan PricingPlan { get; set; } = null!;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    /// <summary>0 to 1000. Higher wins.</summary>
    public int Priority { get; set; }

    /// <summary>Optional validity inside the plan validity (e.g. a seasonal surcharge).</summary>
    public DateTime? ValidFromUtc { get; set; }

    public DateTime? ValidToUtc { get; set; }

    // ---------------- Base and weight ----------------

    public decimal BasePrice { get; set; }

    /// <summary>Weight covered by the base price (kg, 3 decimals). Tiers charge the weight above it.</summary>
    public decimal IncludedWeightKg { get; set; }

    // ---------------- Volume (step 9) ----------------

    /// <summary>Price of one cubic metre; used for shipments priced by volume (charge = m³ x price, plus the base price).</summary>
    public decimal PricePerCubicMeter { get; set; }

    /// <summary>Null = the rule prices both kinds; otherwise only shipments priced by weight, or only by volume.</summary>
    public PricingBasis? AppliesToBasis { get; set; }

    // ---------------- Packages ----------------

    /// <summary>Packages covered by the base price (default 1).</summary>
    public int IncludedPackageCount { get; set; } = 1;

    public decimal PerPackageFee { get; set; }

    // ---------------- COD commission ----------------

    public decimal CodFeeFixed { get; set; }

    /// <summary>0 to 100.</summary>
    public decimal CodFeePercentage { get; set; }

    public decimal CodFeeMinimum { get; set; }

    // ---------------- Insurance ----------------

    /// <summary>0 to 100, of Shipment.InsuranceValue.</summary>
    public decimal InsuranceFeePercentage { get; set; }

    public decimal InsuranceFeeMinimum { get; set; }

    // ---------------- Optional service fees ----------------

    public decimal PickupFee { get; set; }

    public decimal PackagingFee { get; set; }

    public decimal RemoteAreaFee { get; set; }

    public decimal ReturnFee { get; set; }

    // ---------------- Discount and tax ----------------

    /// <summary>Contract discount 0 to 100 % on the shipping subtotal (base + weight + service fees).</summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>Tax rate of this rule. Null = the default tax rate.</summary>
    public long? TaxRateId { get; set; }

    public TaxRate? TaxRate { get; set; }

    public string? Notes { get; set; }

    public ICollection<PriceRuleCondition> Conditions { get; set; } = new List<PriceRuleCondition>();

    public ICollection<PriceRuleWeightTier> WeightTiers { get; set; } = new List<PriceRuleWeightTier>();
}
```

### 6.2 `src/ShippingERP.Domain/Entities/Shipments/Shipment.cs`

```csharp
using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Billing;
using ShippingERP.Domain.Entities.Cod;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// A shipment (شحنة): one consignment from a sender to a recipient, made of one or more packages.
///
/// - ShipmentNumber (SHP-2026-000001) is the internal document number; TrackingNumber is the random, unique
///   number printed on the label and given to customers.
/// - Status only changes through the state machine (ShipmentStatusRules); every change adds an immutable
///   ShipmentStatusHistory row.
/// - Shipments are never deleted: they are cancelled.
/// - Money fields are stored only (no accounting posting). The COD money of a COD shipment is followed by its
///   CODTransaction (step 6), created and kept in sync automatically.
/// </summary>
public class Shipment : BaseEntity
{
    public string ShipmentNumber { get; set; } = string.Empty;

    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>Optional reference from the sender (order number). Unique per sender when given.</summary>
    public string? ExternalReference { get; set; }

    // ---------------- Parties ----------------

    public long SenderCustomerId { get; set; }

    public Customer SenderCustomer { get; set; } = null!;

    public long RecipientCustomerId { get; set; }

    public Customer RecipientCustomer { get; set; } = null!;

    public long? SenderAddressId { get; set; }

    public CustomerAddress? SenderAddress { get; set; }

    public long? RecipientAddressId { get; set; }

    public CustomerAddress? RecipientAddress { get; set; }

    // ---------------- Route ----------------

    /// <summary>The branch that registered the shipment.</summary>
    public long OriginBranchId { get; set; }

    public Branch OriginBranch { get; set; } = null!;

    /// <summary>Where the shipment physically is now (changes with branch transfers).</summary>
    public long CurrentBranchId { get; set; }

    public Branch CurrentBranch { get; set; } = null!;

    /// <summary>The warehouse of the current branch while Status = InWarehouse; null otherwise.</summary>
    public long? CurrentWarehouseId { get; set; }

    public Warehouse? CurrentWarehouse { get; set; }

    /// <summary>The branch that will deliver it. Null until known (filled automatically when the city has one branch).</summary>
    public long? DestinationBranchId { get; set; }

    public Branch? DestinationBranch { get; set; }

    public long OriginCityId { get; set; }

    public City OriginCity { get; set; } = null!;

    public long DestinationCityId { get; set; }

    public City DestinationCity { get; set; } = null!;

    /// <summary>Zone of the sender address (used by the pricing engine). Step 5.</summary>
    public long? OriginZoneId { get; set; }

    public Zone? OriginZone { get; set; }

    public long? DestinationZoneId { get; set; }

    public Zone? DestinationZone { get; set; }

    public long ServiceTypeId { get; set; }

    public ServiceType ServiceType { get; set; } = null!;

    // ---------------- Operation ----------------

    /// <summary>Driver profile currently responsible (pickup or delivery).</summary>
    public long? AssignedDriverId { get; set; }

    public DriverProfile? AssignedDriver { get; set; }

    public long? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Created;

    /// <summary>Number of times the shipment went out for delivery.</summary>
    public int DeliveryAttemptCount { get; set; }

    /// <summary>The branch that hands a returned shipment back to the sender (set when a return is opened).</summary>
    public long? ReturnBranchId { get; set; }

    public Branch? ReturnBranch { get; set; }

    // ---------------- Contents and weights ----------------

    public int PackageCount { get; set; }

    /// <summary>Sum of the package weights (kg, 3 decimals).</summary>
    public decimal ActualWeight { get; set; }

    /// <summary>Sum of the package volumetric weights (kg, 3 decimals).</summary>
    public decimal VolumetricWeight { get; set; }

    /// <summary>max(actual, volumetric) rounded up to the configured step: the weight used for pricing.</summary>
    public decimal ChargeableWeight { get; set; }

    /// <summary>Step 9: total volume in cubic metres (3 decimals), used when PricingBasis = Volume.</summary>
    public decimal TotalVolumeCbm { get; set; }

    /// <summary>Step 9: by weight (kg) or by volume (m³).</summary>
    public PricingBasis PricingBasis { get; set; } = PricingBasis.Weight;

    /// <summary>Step 9: who is invoiced for the shipping charge.</summary>
    public ChargesPayer ChargesPaidBy { get; set; } = ChargesPayer.Sender;

    /// <summary>The customer invoiced for the shipping charge.</summary>
    public long PayerCustomerId => ChargesPaidBy == ChargesPayer.Recipient ? RecipientCustomerId : SenderCustomerId;

    public string PackageDescription { get; set; } = string.Empty;

    // ---------------- Money (stored only) ----------------

    /// <summary>Currency of every amount of this shipment (from the pricing plan, or the base currency).</summary>
    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public decimal DeclaredValue { get; set; }

    public decimal InsuranceValue { get; set; }

    public bool IsCOD { get; set; }

    /// <summary>Amount to collect from the recipient (0 when IsCOD = false).</summary>
    public decimal CODAmount { get; set; }

    /// <summary>A driver collects the shipment from the sender (adds the rule's pickup fee). Step 5.</summary>
    public bool RequiresPickup { get; set; }

    /// <summary>The company packs the shipment (adds the rule's packaging fee). Step 5.</summary>
    public bool RequiresPackaging { get; set; }

    /// <summary>
    /// Shipping subtotal (base + weight + service fees) + COD commission, before discount and tax.
    /// The full breakdown is in the latest ShipmentPricingSnapshot.
    /// </summary>
    public decimal ShippingCharge { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>ShippingCharge - DiscountAmount + TaxAmount.</summary>
    public decimal TotalCharge { get; set; }

    // ---------------- Notes ----------------

    /// <summary>Notes from the sender, printed on the label.</summary>
    public string? SenderNotes { get; set; }

    /// <summary>Staff-only notes, never printed.</summary>
    public string? InternalNotes { get; set; }

    public string? CancellationReason { get; set; }

    // ---------------- Milestones (UTC) ----------------

    public DateTime? PickedUpAtUtc { get; set; }

    public DateTime? DeliveredAtUtc { get; set; }

    public DateTime? ReturnedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    // ---------------- Children ----------------

    public ICollection<ShipmentPackage> Packages { get; set; } = new List<ShipmentPackage>();

    public ICollection<ShipmentStatusHistory> StatusHistory { get; set; } = new List<ShipmentStatusHistory>();

    public ICollection<ShipmentAttachment> Attachments { get; set; } = new List<ShipmentAttachment>();

    public ICollection<ShipmentPricingSnapshot> PricingSnapshots { get; set; } = new List<ShipmentPricingSnapshot>();

    /// <summary>Invoice lines that bill this shipment (step 5). At most one is active per charge type.</summary>
    public ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();

    /// <summary>Manual price changes requested for this shipment (step 5).</summary>
    public ICollection<ShipmentPriceOverride> PriceOverrides { get; set; } = new List<ShipmentPriceOverride>();

    /// <summary>The COD money of this shipment (step 6). Null when the shipment was never a COD shipment.</summary>
    public CODTransaction? CodTransaction { get; set; }
}
```

### 6.3 `src/ShippingERP.Domain/Entities/Shipments/ShipmentPricingSnapshot.cs`

```csharp
using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// A frozen, complete copy of how the price of a shipment was calculated: the plan and rule, every input
/// (weights, packages, COD, insurance, optional services), every component of the breakdown and the tax.
/// Later changes to plans, rules, tax rates or settings never change the price of an existing shipment.
///
/// Append-only: a new snapshot is added every time the shipment is priced (Created, Updated, Imported, Repriced,
/// ManualOverride). The latest one is the current price; invoice lines point to the snapshot they billed.
///
/// Breakdown:
///   ShippingSubtotal     = BasePrice + AdditionalWeightCharge + ServiceFeesTotal      (or the manual charge)
///   DiscountAmount       = ContractDiscountAmount + ManualDiscountAmount
///   TotalShippingCharge  = ShippingSubtotal - DiscountAmount
///   TaxableAmount        = TotalShippingCharge + CodFee (COD commission)
///   TotalCharge          = TaxableAmount + TaxAmount                                  (net total)
///   ShippingCharge       = ShippingSubtotal + CodFee                                  (gross, as on the shipment)
/// </summary>
public class ShipmentPricingSnapshot : BaseEntity, IAppendOnlyEntity
{
    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    /// <summary>"Created", "Updated", "Imported", "Repriced" or "ManualOverride".</summary>
    public string Reason { get; set; } = string.Empty;

    public PricingSource PricingSource { get; set; }

    public long? PricingPlanId { get; set; }

    public string? PricingPlanCode { get; set; }

    /// <summary>The PriceRule used (no foreign key: snapshots made in step 4 refer to the old rule table).</summary>
    public long? PricingRuleId { get; set; }

    public string? PricingRuleName { get; set; }

    public long CurrencyId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    // ---------------- Inputs ----------------

    public decimal ActualWeight { get; set; }

    public decimal VolumetricWeight { get; set; }

    public decimal ChargeableWeight { get; set; }

    public int VolumetricDivisor { get; set; }

    public decimal WeightRoundingStepKg { get; set; }

    public int PackageCount { get; set; }

    /// <summary>Step 9: by weight or by volume.</summary>
    public PricingBasis PricingBasis { get; set; } = PricingBasis.Weight;

    /// <summary>Step 9: the volume priced (m³, 3 decimals).</summary>
    public decimal VolumeCbm { get; set; }

    public decimal PricePerCubicMeter { get; set; }

    /// <summary>VolumeCbm x PricePerCubicMeter (0 for weight pricing).</summary>
    public decimal VolumeCharge { get; set; }

    public decimal InsuranceValue { get; set; }

    public bool RequiresPickup { get; set; }

    public bool RequiresPackaging { get; set; }

    public bool IsRemoteArea { get; set; }

    public bool IsReturn { get; set; }

    // ---------------- Base and weight ----------------

    public decimal BasePrice { get; set; }

    /// <summary>Weight covered by the base price.</summary>
    public decimal IncludedWeightKg { get; set; }

    /// <summary>Kilograms charged by the weight tiers.</summary>
    public decimal AdditionalWeightKg { get; set; }

    /// <summary>Weight charge (sum of the tiers).</summary>
    public decimal AdditionalWeightCharge { get; set; }

    /// <summary>The tiers applied, as JSON: [{"fromKg":5,"toKg":20,"kg":3,"pricePerKg":2000,"charge":6000}].</summary>
    public string? WeightTiersJson { get; set; }

    // ---------------- Service fees ----------------

    public decimal PackageFee { get; set; }

    public decimal InsuranceFee { get; set; }

    public decimal PickupFee { get; set; }

    public decimal PackagingFee { get; set; }

    public decimal RemoteAreaFee { get; set; }

    public decimal ReturnFee { get; set; }

    public decimal ServiceFeesTotal { get; set; }

    // ---------------- COD commission ----------------

    public decimal CodFeeFixed { get; set; }

    public decimal CodFeePercentage { get; set; }

    public decimal CodFeeMinimum { get; set; }

    public decimal CodAmount { get; set; }

    /// <summary>COD commission.</summary>
    public decimal CodFee { get; set; }

    // ---------------- Totals ----------------

    /// <summary>Base + weight + service fees, or the manual charge.</summary>
    public decimal ShippingSubtotal { get; set; }

    /// <summary>The rule price when a manual charge was used (null when no rule matched or no manual charge).</summary>
    public decimal? CalculatedShippingSubtotal { get; set; }

    public decimal ContractDiscountPercentage { get; set; }

    public decimal ContractDiscountAmount { get; set; }

    public decimal ManualDiscountAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalShippingCharge { get; set; }

    public decimal TaxableAmount { get; set; }

    /// <summary>Gross charge stored on the shipment: ShippingSubtotal + COD commission.</summary>
    public decimal ShippingCharge { get; set; }

    public long? TaxRateId { get; set; }

    public decimal TaxPercentage { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>Net total: TaxableAmount + TaxAmount.</summary>
    public decimal TotalCharge { get; set; }

    // ---------------- Manual override ----------------

    public string? OverrideReason { get; set; }

    public long? PriceOverrideId { get; set; }
}
```

### 6.4 `src/ShippingERP.Domain/Enums/ShipmentEnums.cs`

```csharp
namespace ShippingERP.Domain.Enums;

// Enums used by shipments and their documents (step 4). All are stored as text, so never rename a value.

/// <summary>What caused a status change. Some statuses can only be reached from a specific workflow.</summary>
public enum ShipmentStatusChangeSource
{
    /// <summary>The shipment was registered (POST /shipments).</summary>
    Creation = 1,

    /// <summary>Imported from an Excel file.</summary>
    Import = 2,

    /// <summary>A user scanned / changed the status directly (PickedUp, ReceivedAtBranch, InWarehouse).</summary>
    Manual = 3,

    /// <summary>Warehouse check-in / check-out manifest.</summary>
    WarehouseManifest = 4,

    /// <summary>Branch transfer dispatched / received.</summary>
    BranchTransfer = 5,

    /// <summary>Driver delivery manifest dispatched / closed.</summary>
    DriverManifest = 6,

    /// <summary>Delivery confirmed (proof of delivery) or delivery attempt failed.</summary>
    Delivery = 7,

    /// <summary>Return-to-sender workflow.</summary>
    Return = 8,

    /// <summary>Damage / loss case opened or resolved.</summary>
    DamageCase = 9,

    /// <summary>Shipment cancelled.</summary>
    Cancellation = 10,

    /// <summary>Explicit status correction by an authorized user (bypasses the normal rules, reason required).</summary>
    Correction = 11
}

/// <summary>Kind of file attached to a shipment.</summary>
public enum ShipmentAttachmentType
{
    Document = 1,
    PackagePhoto = 2,
    CommercialInvoice = 3,
    /// <summary>Recipient signature (proof of delivery).</summary>
    Signature = 4,
    /// <summary>Photo taken at delivery (proof of delivery).</summary>
    DeliveryPhoto = 5,
    /// <summary>Evidence for a damage / loss case.</summary>
    DamageEvidence = 6,
    Other = 99
}

/// <summary>How the shipping charge was obtained.</summary>
public enum PricingSource
{
    /// <summary>Calculated from a pricing rule of the customer's plan (or the default plan).</summary>
    PricingRule = 1,

    /// <summary>Entered manually by a user (no rule matched, or a special price).</summary>
    Manual = 2,

    /// <summary>Calculated from a rule of the sender's own contract (a pricing plan linked to that customer).</summary>
    CustomerContract = 3
}

/// <summary>Direction of a warehouse movement manifest.</summary>
public enum WarehouseMovementType
{
    /// <summary>إدخال: shipments enter the warehouse (status becomes InWarehouse).</summary>
    CheckIn = 1,

    /// <summary>إخراج: shipments leave the warehouse back to the branch floor (status becomes ReceivedAtBranch).</summary>
    CheckOut = 2
}

public enum WarehouseManifestStatus
{
    /// <summary>Items can still be added or removed.</summary>
    Draft = 1,

    /// <summary>Applied to the shipments; cannot change anymore.</summary>
    Confirmed = 2,

    Cancelled = 3
}

public enum BranchTransferStatus
{
    Draft = 1,

    /// <summary>Left the origin branch; every shipment is InTransit.</summary>
    Dispatched = 2,

    /// <summary>Every shipment was received by the destination branch.</summary>
    Received = 3,

    /// <summary>Received, but some shipments were missing.</summary>
    PartiallyReceived = 4,

    Cancelled = 5
}

public enum BranchTransferItemStatus
{
    Pending = 1,
    InTransit = 2,
    Received = 3,
    /// <summary>Not found when the transfer was received. Open a damage / loss case to follow it up.</summary>
    Missing = 4
}

public enum DriverManifestStatus
{
    Draft = 1,

    /// <summary>Handed to the driver; every shipment is OutForDelivery.</summary>
    Dispatched = 2,

    /// <summary>The driver came back; every item has a result.</summary>
    Closed = 3,

    Cancelled = 4
}

public enum DeliveryResult
{
    Pending = 1,
    Delivered = 2,
    Failed = 3
}

public enum ShipmentReturnType
{
    /// <summary>The delivery failed (absent, wrong address...).</summary>
    FailedDelivery = 1,

    /// <summary>The recipient refused the shipment.</summary>
    RecipientRefused = 2,

    /// <summary>The sender asked to get the shipment back.</summary>
    SenderRequest = 3,

    /// <summary>The recipient sends a delivered shipment back (the only way to reopen a delivered shipment).</summary>
    AfterDelivery = 4
}

public enum ShipmentReturnStatus
{
    Open = 1,
    Completed = 2,
    Cancelled = 3
}

public enum DamageCaseType
{
    Damaged = 1,
    PartiallyDamaged = 2,
    Lost = 3
}

public enum DamageCaseStatus
{
    Open = 1,
    UnderInvestigation = 2,
    Resolved = 3
}

public enum DamageCaseResolution
{
    /// <summary>The customer will be compensated (the amount is stored; accounting comes in a later step).</summary>
    Compensated = 1,

    /// <summary>The claim was rejected.</summary>
    Rejected = 2,

    /// <summary>The lost shipment was found; it goes back to ReceivedAtBranch.</summary>
    FoundAndRecovered = 3,

    /// <summary>Recorded only, no further action.</summary>
    NoAction = 4
}

/// <summary>
/// Step 9: how the shipping charge of a shipment is calculated. Chosen on every shipment.
/// Weight = by kilogram (the weight tiers of the price rule); Volume = by cubic metre (PriceRule.PricePerCubicMeter).
/// </summary>
public enum PricingBasis
{
    Weight = 1,
    Volume = 2
}

/// <summary>Step 9: who pays the shipping charge (who is invoiced for it).</summary>
public enum ChargesPayer
{
    /// <summary>The sender is invoiced (prepaid, the default before step 9).</summary>
    Sender = 1,

    /// <summary>The recipient pays on delivery (freight collect): the recipient is invoiced.</summary>
    Recipient = 2
}

public static class ShipmentChargeEnums
{
    public static string Arabic(PricingBasis basis) => basis switch
    {
        PricingBasis.Weight => "بالوزن",
        PricingBasis.Volume => "بالحجم",
        _ => basis.ToString()
    };

    public static string Arabic(ChargesPayer payer) => payer switch
    {
        ChargesPayer.Sender => "على المرسل",
        ChargesPayer.Recipient => "على المستلم",
        _ => payer.ToString()
    };
}
```

### 6.5 `src/ShippingERP.Domain/Pricing/PricingEngine.cs`

```csharp
using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Pricing;

/// <summary>The shipment facts a price rule is matched against.</summary>
public sealed class PricingContext
{
    public long? OriginZoneId { get; init; }
    public long? DestinationZoneId { get; init; }
    public long? OriginCityId { get; init; }
    public long? DestinationCityId { get; init; }
    public long ServiceTypeId { get; init; }
    public decimal ChargeableWeight { get; init; }
    public int PackageCount { get; init; }

    /// <summary>Step 9: rules with AppliesToBasis only match shipments priced the same way.</summary>
    public PricingBasis PricingBasis { get; init; } = PricingBasis.Weight;
}

/// <summary>
/// Chooses the price rule of a plan for a shipment. Pure logic (no database), so it is unit-tested directly.
///
/// A rule matches when it is active, valid at the pricing time and, for every condition TYPE it has, at least one
/// condition of that type matches (same type = OR, different types = AND). Winner: highest Priority, then the most
/// specific rule (more condition types), then the lowest Id (the oldest rule).
/// </summary>
public static class PriceRuleMatcher
{
    public static bool Matches(PriceRule rule, PricingContext context, DateTime nowUtc)
    {
        if (!rule.IsActive || rule.IsDeleted)
        {
            return false;
        }

        if ((rule.ValidFromUtc.HasValue && rule.ValidFromUtc.Value > nowUtc) ||
            (rule.ValidToUtc.HasValue && rule.ValidToUtc.Value <= nowUtc))
        {
            return false;
        }

        if (rule.AppliesToBasis.HasValue && rule.AppliesToBasis.Value != context.PricingBasis)
        {
            return false;
        }

        foreach (var group in rule.Conditions.GroupBy(x => x.ConditionType))
        {
            if (!group.Any(condition => ConditionMatches(condition, context)))
            {
                return false;
            }
        }

        return true;
    }

    public static bool ConditionMatches(PriceRuleCondition condition, PricingContext context)
    {
        return condition.ConditionType switch
        {
            PriceConditionType.OriginZone => context.OriginZoneId.HasValue && condition.ReferenceId == context.OriginZoneId,
            PriceConditionType.DestinationZone => context.DestinationZoneId.HasValue && condition.ReferenceId == context.DestinationZoneId,
            PriceConditionType.OriginCity => context.OriginCityId.HasValue && condition.ReferenceId == context.OriginCityId,
            PriceConditionType.DestinationCity => context.DestinationCityId.HasValue && condition.ReferenceId == context.DestinationCityId,
            PriceConditionType.ServiceType => condition.ReferenceId == context.ServiceTypeId,
            PriceConditionType.ChargeableWeight => InRange(context.ChargeableWeight, condition),
            PriceConditionType.PackageCount => InRange(context.PackageCount, condition),
            _ => false
        };
    }

    /// <summary>Number of different condition types (more = more specific).</summary>
    public static int Specificity(PriceRule rule) =>
        rule.Conditions.Select(x => x.ConditionType).Distinct().Count() + (rule.AppliesToBasis.HasValue ? 1 : 0);

    public static PriceRule? SelectBest(IEnumerable<PriceRule> rules, PricingContext context, DateTime nowUtc)
    {
        return rules
            .Where(rule => Matches(rule, context, nowUtc))
            .OrderByDescending(rule => rule.Priority)
            .ThenByDescending(Specificity)
            .ThenBy(rule => rule.Id)
            .FirstOrDefault();
    }

    private static bool InRange(decimal value, PriceRuleCondition condition) =>
        (condition.MinValue is null || value >= condition.MinValue.Value) &&
        (condition.MaxValue is null || value < condition.MaxValue.Value);
}

/// <summary>Everything besides the rule that the price depends on.</summary>
public sealed class PriceBreakdownInput
{
    public decimal ChargeableWeight { get; init; }

    /// <summary>Step 9: Weight = base + weight tiers; Volume = base + VolumeCbm x PricePerCubicMeter (no weight tiers).</summary>
    public PricingBasis PricingBasis { get; init; } = PricingBasis.Weight;

    /// <summary>Volume in m³ (3 decimals), charged when PricingBasis = Volume.</summary>
    public decimal VolumeCbm { get; init; }

    public int PackageCount { get; init; }
    public bool IsCod { get; init; }
    public decimal CodAmount { get; init; }
    public decimal InsuranceValue { get; init; }
    public bool RequiresPickup { get; init; }
    public bool RequiresPackaging { get; init; }
    public bool IsRemoteArea { get; init; }
    public bool IsReturn { get; init; }

    /// <summary>Manual shipping subtotal (replaces base + weight + service fees and the contract discount).</summary>
    public decimal? ManualShippingCharge { get; init; }

    public decimal ManualDiscountAmount { get; init; }
}

/// <summary>One applied weight tier.</summary>
public sealed record WeightTierCharge(decimal FromKg, decimal? ToKg, decimal Kg, decimal PricePerKg, decimal Charge);

/// <summary>The full price breakdown (all amounts rounded to the currency decimals).</summary>
public sealed class PriceBreakdown
{
    public decimal BasePrice { get; init; }
    public decimal IncludedWeightKg { get; init; }
    public decimal AdditionalWeightKg { get; init; }
    public decimal WeightCharge { get; init; }
    public IReadOnlyList<WeightTierCharge> WeightTiers { get; init; } = Array.Empty<WeightTierCharge>();

    public PricingBasis PricingBasis { get; init; } = PricingBasis.Weight;
    public decimal VolumeCbm { get; init; }
    public decimal PricePerCubicMeter { get; init; }

    /// <summary>VolumeCbm x PricePerCubicMeter (0 for weight pricing).</summary>
    public decimal VolumeCharge { get; init; }

    public decimal PackageFee { get; init; }
    public decimal InsuranceFee { get; init; }
    public decimal PickupFee { get; init; }
    public decimal PackagingFee { get; init; }
    public decimal RemoteAreaFee { get; init; }
    public decimal ReturnFee { get; init; }
    public decimal ServiceFeesTotal => PackageFee + InsuranceFee + PickupFee + PackagingFee + RemoteAreaFee + ReturnFee;

    public decimal CodFeeFixed { get; init; }
    public decimal CodFeePercentage { get; init; }
    public decimal CodFeeMinimum { get; init; }
    public decimal CodAmount { get; init; }
    public decimal CodCommission { get; init; }

    /// <summary>Base + weight (or volume) + service fees, or the manual charge.</summary>
    public decimal ShippingSubtotal { get; init; }

    /// <summary>The rule subtotal when a manual charge replaced it.</summary>
    public decimal? CalculatedShippingSubtotal { get; init; }

    public decimal ContractDiscountPercentage { get; init; }
    public decimal ContractDiscountAmount { get; init; }
    public decimal ManualDiscountAmount { get; init; }
    public decimal DiscountAmount => ContractDiscountAmount + ManualDiscountAmount;

    /// <summary>ShippingSubtotal - DiscountAmount.</summary>
    public decimal TotalShippingCharge => ShippingSubtotal - DiscountAmount;

    /// <summary>TotalShippingCharge + COD commission.</summary>
    public decimal TaxableAmount => TotalShippingCharge + CodCommission;

    public decimal TaxPercentage { get; init; }
    public decimal TaxAmount { get; init; }

    /// <summary>Net total = TaxableAmount + TaxAmount.</summary>
    public decimal NetTotal => TaxableAmount + TaxAmount;

    /// <summary>Gross charge stored on the shipment: ShippingSubtotal + COD commission.</summary>
    public decimal GrossCharge => ShippingSubtotal + CodCommission;
}

/// <summary>
/// Calculates the price breakdown of a shipment from one rule (pure logic, see PriceRule for the formulas).
///
/// - With a manual charge, base / weight / service fees are not used (the manual charge is the shipping subtotal)
///   and no contract discount applies; the COD commission still comes from the rule when there is one.
/// - The tax applies to (subtotal - discount + COD commission).
/// - Every component is rounded to the currency decimals (midpoint away from zero) before it is added, so the
///   printed components always add up to the totals.
/// </summary>
public static class PriceBreakdownCalculator
{
    public static PriceBreakdown Calculate(PriceRule? rule, PriceBreakdownInput input, decimal taxPercentage, int decimals)
    {
        if (rule is null && input.ManualShippingCharge is null)
        {
            throw new InvalidOperationException("A price needs a rule or a manual shipping charge.");
        }

        decimals = Math.Clamp(decimals, 0, 4);

        // ---------------- COD commission (from the rule, also with a manual charge)
        decimal codCommission = 0m;
        if (rule is not null && input.IsCod && input.CodAmount > 0m)
        {
            var commission = rule.CodFeeFixed + (input.CodAmount * rule.CodFeePercentage / 100m);
            codCommission = Round(Math.Max(commission, rule.CodFeeMinimum), decimals);
        }

        decimal basePrice = 0m, weightCharge = 0m, additionalKg = 0m, volumeCharge = 0m, pricePerCbm = 0m;
        var volume = input.PricingBasis == PricingBasis.Volume ? Math.Max(0m, input.VolumeCbm) : 0m;
        decimal packageFee = 0m, insuranceFee = 0m, pickupFee = 0m, packagingFee = 0m, remoteAreaFee = 0m, returnFee = 0m;
        decimal contractDiscount = 0m, contractDiscountPercentage = 0m;
        IReadOnlyList<WeightTierCharge> tiers = Array.Empty<WeightTierCharge>();
        decimal subtotal;
        decimal? calculatedSubtotal = null;

        if (rule is not null)
        {
            basePrice = Round(rule.BasePrice, decimals);
            if (input.PricingBasis == PricingBasis.Volume)
            {
                pricePerCbm = rule.PricePerCubicMeter;
                volumeCharge = Round(volume * pricePerCbm, decimals);
            }
            else
            {
                (tiers, additionalKg) = WeightTiers(rule, input.ChargeableWeight, decimals);
                weightCharge = tiers.Sum(x => x.Charge);
            }

            var extraPackages = Math.Max(0, input.PackageCount - Math.Max(0, rule.IncludedPackageCount));
            packageFee = Round(extraPackages * rule.PerPackageFee, decimals);

            if (input.InsuranceValue > 0m && (rule.InsuranceFeePercentage > 0m || rule.InsuranceFeeMinimum > 0m))
            {
                var fee = input.InsuranceValue * rule.InsuranceFeePercentage / 100m;
                insuranceFee = Round(Math.Max(fee, rule.InsuranceFeeMinimum), decimals);
            }

            pickupFee = input.RequiresPickup ? Round(rule.PickupFee, decimals) : 0m;
            packagingFee = input.RequiresPackaging ? Round(rule.PackagingFee, decimals) : 0m;
            remoteAreaFee = input.IsRemoteArea ? Round(rule.RemoteAreaFee, decimals) : 0m;
            returnFee = input.IsReturn ? Round(rule.ReturnFee, decimals) : 0m;

            calculatedSubtotal = basePrice + weightCharge + volumeCharge + packageFee + insuranceFee + pickupFee + packagingFee + remoteAreaFee + returnFee;
        }

        if (input.ManualShippingCharge.HasValue)
        {
            subtotal = Round(input.ManualShippingCharge.Value, decimals);
            basePrice = weightCharge = additionalKg = volumeCharge = pricePerCbm = 0m;
            packageFee = insuranceFee = pickupFee = packagingFee = remoteAreaFee = returnFee = 0m;
            tiers = Array.Empty<WeightTierCharge>();
        }
        else
        {
            subtotal = calculatedSubtotal!.Value;
            calculatedSubtotal = null;
            contractDiscountPercentage = rule!.DiscountPercentage;
            contractDiscount = Round(subtotal * contractDiscountPercentage / 100m, decimals);
        }

        var manualDiscount = Round(input.ManualDiscountAmount, decimals);
        var taxable = subtotal - contractDiscount - manualDiscount + codCommission;
        var tax = Round(Math.Max(0m, taxable) * taxPercentage / 100m, decimals);

        return new PriceBreakdown
        {
            BasePrice = basePrice,
            IncludedWeightKg = input.ManualShippingCharge.HasValue || input.PricingBasis == PricingBasis.Volume ? 0m : rule?.IncludedWeightKg ?? 0m,
            AdditionalWeightKg = additionalKg,
            WeightCharge = weightCharge,
            WeightTiers = tiers,
            PricingBasis = input.PricingBasis,
            VolumeCbm = volume,
            PricePerCubicMeter = pricePerCbm,
            VolumeCharge = volumeCharge,
            PackageFee = packageFee,
            InsuranceFee = insuranceFee,
            PickupFee = pickupFee,
            PackagingFee = packagingFee,
            RemoteAreaFee = remoteAreaFee,
            ReturnFee = returnFee,
            CodFeeFixed = rule?.CodFeeFixed ?? 0m,
            CodFeePercentage = rule?.CodFeePercentage ?? 0m,
            CodFeeMinimum = rule?.CodFeeMinimum ?? 0m,
            CodAmount = input.IsCod ? input.CodAmount : 0m,
            CodCommission = codCommission,
            ShippingSubtotal = subtotal,
            CalculatedShippingSubtotal = calculatedSubtotal,
            ContractDiscountPercentage = contractDiscountPercentage,
            ContractDiscountAmount = contractDiscount,
            ManualDiscountAmount = manualDiscount,
            TaxPercentage = taxPercentage,
            TaxAmount = tax
        };
    }

    /// <summary>Progressive tiers: each tier charges the kg inside [from, to) above the included weight.</summary>
    public static (IReadOnlyList<WeightTierCharge> Tiers, decimal Kg) WeightTiers(PriceRule rule, decimal chargeableWeight, int decimals)
    {
        var result = new List<WeightTierCharge>();
        var included = Math.Max(0m, rule.IncludedWeightKg);
        decimal totalKg = 0m;

        foreach (var tier in rule.WeightTiers.OrderBy(x => x.FromWeightKg))
        {
            var start = Math.Max(tier.FromWeightKg, included);
            var end = Math.Min(chargeableWeight, tier.ToWeightKg ?? decimal.MaxValue);
            var kg = end - start;
            if (kg <= 0m)
            {
                continue;
            }

            var charge = Round(kg * tier.PricePerKg, decimals);
            result.Add(new WeightTierCharge(tier.FromWeightKg, tier.ToWeightKg, kg, tier.PricePerKg, charge));
            totalKg += kg;
        }

        return (result, totalKg);
    }

    public static decimal Round(decimal value, int decimals) =>
        Math.Round(value, Math.Clamp(decimals, 0, 4), MidpointRounding.AwayFromZero);
}
```

### 6.6 `src/ShippingERP.Application/DTOs/Customers/PricingDtos.cs`

```csharp
using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Customers;

// ------------------------------------------------------------------ pricing plans

public sealed class PricingPlanDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    /// <summary>Set when the plan is a customer-specific contract.</summary>
    public long? ContractCustomerId { get; init; }
    public string? ContractCustomerName { get; init; }
    public string? ContractReference { get; init; }
    public DateTime? ValidFromUtc { get; init; }
    public DateTime? ValidToUtc { get; init; }
    public string? Notes { get; init; }
    public int RuleCount { get; init; }
    public int CustomerCount { get; init; }
}

/// <summary>
/// Body of POST/PUT /api/v1/pricing-plans.
/// ContractCustomerId makes the plan a customer contract: it prices that customer's shipments before the plan
/// assigned to the customer and before the default plan (a contract plan cannot be the default plan).
/// </summary>
public sealed class SavePricingPlanRequest
{
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public long CurrencyId { get; set; }
    public bool IsDefault { get; set; }
    public long? ContractCustomerId { get; set; }
    public string? ContractReference { get; set; }
    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Search: code, names, contract reference. SortBy: code (default), arabicName, createdAtUtc.</summary>
public sealed class PricingPlanListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "createdAtUtc" };

    public long? CurrencyId { get; set; }

    /// <summary>true = only customer contracts, false = only general plans.</summary>
    public bool? IsContract { get; set; }

    public long? ContractCustomerId { get; set; }
}

// ------------------------------------------------------------------ price rules

public sealed class PriceRuleConditionDto
{
    public long Id { get; init; }
    public PriceConditionType ConditionType { get; init; }
    public string ConditionTypeName { get; init; } = string.Empty;
    public long? ReferenceId { get; init; }
    /// <summary>Name of the zone / city / service type.</summary>
    public string? ReferenceName { get; init; }
    public decimal? MinValue { get; init; }
    public decimal? MaxValue { get; init; }
}

public sealed class PriceRuleWeightTierDto
{
    public long Id { get; init; }
    public decimal FromWeightKg { get; init; }
    public decimal? ToWeightKg { get; init; }
    public decimal PricePerKg { get; init; }
}

public sealed class PricingRuleDto
{
    public long Id { get; init; }
    public long PricingPlanId { get; init; }
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public int Priority { get; init; }
    public DateTime? ValidFromUtc { get; init; }
    public DateTime? ValidToUtc { get; init; }
    public decimal BasePrice { get; init; }
    public decimal IncludedWeightKg { get; init; }
    public decimal PricePerCubicMeter { get; init; }
    public PricingBasis? AppliesToBasis { get; init; }
    public int IncludedPackageCount { get; init; }
    public decimal PerPackageFee { get; init; }
    public decimal CodFeeFixed { get; init; }
    public decimal CodFeePercentage { get; init; }
    public decimal CodFeeMinimum { get; init; }
    public decimal InsuranceFeePercentage { get; init; }
    public decimal InsuranceFeeMinimum { get; init; }
    public decimal PickupFee { get; init; }
    public decimal PackagingFee { get; init; }
    public decimal RemoteAreaFee { get; init; }
    public decimal ReturnFee { get; init; }
    public decimal DiscountPercentage { get; init; }
    public long? TaxRateId { get; init; }
    public string? TaxRateName { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<PriceRuleConditionDto> Conditions { get; init; } = Array.Empty<PriceRuleConditionDto>();
    public IReadOnlyList<PriceRuleWeightTierDto> WeightTiers { get; init; } = Array.Empty<PriceRuleWeightTierDto>();
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public sealed class SavePriceRuleConditionRequest
{
    public PriceConditionType ConditionType { get; set; }

    /// <summary>Zone / city / service type id (reference types only).</summary>
    public long? ReferenceId { get; set; }

    /// <summary>Inclusive (weight in kg, or package count). Range types only.</summary>
    public decimal? MinValue { get; set; }

    /// <summary>Exclusive. Empty = no limit. Range types only.</summary>
    public decimal? MaxValue { get; set; }
}

public sealed class SavePriceRuleWeightTierRequest
{
    public decimal FromWeightKg { get; set; }

    /// <summary>Empty = no upper limit (last tier only).</summary>
    public decimal? ToWeightKg { get; set; }

    public decimal PricePerKg { get; set; }
}

/// <summary>
/// Body of POST/PUT /api/v1/pricing-plans/{planId}/rules.
///
/// Conditions decide WHEN the rule applies (empty = every shipment); the amounts decide WHAT it charges.
/// Example "Damascus standard, up to 30 kg":
///   conditions: [{ conditionType: "ServiceType", referenceId: 1 }, { conditionType: "DestinationCity", referenceId: 1 },
///                { conditionType: "ChargeableWeight", minValue: 0, maxValue: 30 }]
///   basePrice: 15000, includedWeightKg: 5, weightTiers: [{ fromWeightKg: 5, toWeightKg: null, pricePerKg: 2000 }]
/// </summary>
public sealed class SavePricingRuleRequest
{
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }

    /// <summary>0 to 1000. Higher wins when several rules match.</summary>
    public int Priority { get; set; }

    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }

    public List<SavePriceRuleConditionRequest> Conditions { get; set; } = new();

    public decimal BasePrice { get; set; }

    /// <summary>Weight covered by the base price.</summary>
    public decimal IncludedWeightKg { get; set; }

    /// <summary>Progressive per-kg prices above IncludedWeightKg. Contiguous; only the last may be open-ended.</summary>
    public List<SavePriceRuleWeightTierRequest> WeightTiers { get; set; } = new();

    /// <summary>Step 9: price of one m³ for shipments priced by volume.</summary>
    public decimal PricePerCubicMeter { get; set; }

    /// <summary>Step 9: null = both; Weight / Volume = only shipments priced that way.</summary>
    public PricingBasis? AppliesToBasis { get; set; }

    public int IncludedPackageCount { get; set; } = 1;
    public decimal PerPackageFee { get; set; }

    public decimal CodFeeFixed { get; set; }
    /// <summary>0 to 100.</summary>
    public decimal CodFeePercentage { get; set; }
    public decimal CodFeeMinimum { get; set; }

    /// <summary>0 to 100 (of the insurance value).</summary>
    public decimal InsuranceFeePercentage { get; set; }
    public decimal InsuranceFeeMinimum { get; set; }

    public decimal PickupFee { get; set; }
    public decimal PackagingFee { get; set; }
    public decimal RemoteAreaFee { get; set; }
    public decimal ReturnFee { get; set; }

    /// <summary>Contract discount 0 to 100 % on the shipping subtotal.</summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>Empty = the default tax rate.</summary>
    public long? TaxRateId { get; set; }

    public string? Notes { get; set; }
}
```

### 6.7 `src/ShippingERP.Application/DTOs/Shipments/ManifestDtos.cs`

```csharp
using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Shipments;

/// <summary>
/// Shipments to add to a manifest / transfer, by tracking number or shipment number (barcode scanner input).
/// Up to 200 codes per call.
/// </summary>
public sealed class ShipmentCodesRequest
{
    public List<string> Codes { get; set; } = new();
}

/// <summary>Base query for document lists (manifests, transfers, returns, damage cases).</summary>
public abstract class DocumentListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "createdAtUtc", "number" };

    /// <summary>Created on or after (UTC).</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>Created before (UTC, exclusive).</summary>
    public DateTime? ToUtc { get; set; }
}

/// <summary>A shipment as shown on a manifest line.</summary>
public class ManifestShipmentDto
{
    public long ItemId { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public ShipmentStatus ShipmentStatus { get; init; }
    public string ShipmentStatusName { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string RecipientPhone { get; init; } = string.Empty;
    public string DestinationCityName { get; init; } = string.Empty;
    public string? DestinationZoneName { get; init; }
    public int PackageCount { get; init; }
    public decimal ChargeableWeight { get; init; }
    public bool IsCOD { get; init; }
    public decimal CODAmount { get; init; }
    public decimal TotalVolumeCbm { get; init; }
    public PricingBasis PricingBasis { get; init; }
    public ChargesPayer ChargesPaidBy { get; init; }
}

// ------------------------------------------------------------------ warehouse movement manifests

public sealed class CreateWarehouseManifestRequest
{
    public WarehouseMovementType MovementType { get; set; } = WarehouseMovementType.CheckIn;

    /// <summary>The warehouse (its branch becomes the manifest branch).</summary>
    public long WarehouseId { get; set; }

    public string? Notes { get; set; }

    /// <summary>Optional shipments to add immediately.</summary>
    public List<string> Codes { get; set; } = new();
}

public sealed class WarehouseManifestDto
{
    public long Id { get; init; }
    public string ManifestNumber { get; init; } = string.Empty;
    public WarehouseMovementType MovementType { get; init; }
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;
    public WarehouseManifestStatus Status { get; init; }
    public int ItemCount { get; init; }
    public DateTime? ConfirmedAtUtc { get; init; }
    public long? ConfirmedByUserId { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    /// <summary>Only in GET by id.</summary>
    public IReadOnlyList<ManifestShipmentDto>? Items { get; init; }
}

/// <summary>SortBy: createdAtUtc (default, newest first), number.</summary>
public sealed class WarehouseManifestListQuery : DocumentListQuery
{
    public long? BranchId { get; set; }
    public long? WarehouseId { get; set; }
    public WarehouseManifestStatus? Status { get; set; }
    public WarehouseMovementType? MovementType { get; set; }
}

// ------------------------------------------------------------------ branch transfers

public sealed class CreateBranchTransferRequest
{
    public long FromBranchId { get; set; }
    public long ToBranchId { get; set; }
    public long? VehicleId { get; set; }
    /// <summary>Line-haul driver profile id.</summary>
    public long? DriverId { get; set; }
    public string? SealNumber { get; set; }
    public string? Notes { get; set; }
    /// <summary>Optional shipments to add immediately.</summary>
    public List<string> Codes { get; set; } = new();
}

/// <summary>Body of POST /api/v1/branch-transfers/{id}/dispatch (vehicle / driver / seal can be set or changed).</summary>
public sealed class DispatchBranchTransferRequest
{
    public long? VehicleId { get; set; }
    public long? DriverId { get; set; }
    public string? SealNumber { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Body of POST /api/v1/branch-transfers/{id}/receive.
/// ReceivedCodes empty = everything arrived. Otherwise only the listed shipments are received and the
/// others are marked Missing (the transfer becomes PartiallyReceived).
/// </summary>
public sealed class ReceiveBranchTransferRequest
{
    public List<string> ReceivedCodes { get; set; } = new();
    public string? Notes { get; set; }
}

public sealed class BranchTransferItemDto : ManifestShipmentDto
{
    public BranchTransferItemStatus ItemStatus { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }
    public string? Notes { get; init; }
}

public sealed class BranchTransferDto
{
    public long Id { get; init; }
    public string TransferNumber { get; init; } = string.Empty;
    public long FromBranchId { get; init; }
    public string FromBranchName { get; init; } = string.Empty;
    public long ToBranchId { get; init; }
    public string ToBranchName { get; init; } = string.Empty;
    public long? VehicleId { get; init; }
    public string? VehiclePlateNumber { get; init; }
    public long? DriverId { get; init; }
    public string? DriverName { get; init; }
    public string? SealNumber { get; init; }
    public BranchTransferStatus Status { get; init; }
    public int ItemCount { get; init; }
    public int ReceivedCount { get; init; }
    public int MissingCount { get; init; }
    public DateTime? DispatchedAtUtc { get; init; }
    public long? DispatchedByUserId { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }
    public long? ReceivedByUserId { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    /// <summary>Only in GET by id.</summary>
    public IReadOnlyList<BranchTransferItemDto>? Items { get; init; }
}

/// <summary>SortBy: createdAtUtc (default, newest first), number.</summary>
public sealed class BranchTransferListQuery : DocumentListQuery
{
    public long? FromBranchId { get; set; }
    public long? ToBranchId { get; set; }
    public BranchTransferStatus? Status { get; set; }
}

// ------------------------------------------------------------------ driver delivery manifests

public sealed class CreateDriverManifestRequest
{
    /// <summary>Driver profile id. The manifest branch is the driver's branch.</summary>
    public long DriverId { get; set; }
    /// <summary>Default: the driver's default vehicle.</summary>
    public long? VehicleId { get; set; }
    /// <summary>Planned delivery day (default today).</summary>
    public DateTime? ManifestDateUtc { get; set; }
    public string? Notes { get; set; }
    /// <summary>Optional shipments to add immediately (in this stop order).</summary>
    public List<string> Codes { get; set; } = new();
}

public sealed class CloseDriverManifestRequest
{
    /// <summary>Reason written on shipments that are still pending (they become DeliveryFailed).</summary>
    public string? PendingReason { get; set; }
    public string? Notes { get; set; }
}

public sealed class DriverManifestItemDto : ManifestShipmentDto
{
    /// <summary>Step 9: the shipping charges are collected from the recipient.</summary>
    public bool ChargesOnRecipient { get; init; }

    /// <summary>Shipping charge to collect from the recipient (0 when the sender pays).</summary>
    public decimal ChargesToCollect { get; init; }

    public int Sequence { get; init; }
    public DeliveryResult Result { get; init; }
    public DateTime? ResultAtUtc { get; init; }
    public string? FailureReason { get; init; }
    public long? ProofOfDeliveryId { get; init; }
    public string? RecipientAddressLine { get; init; }
}

public sealed class DriverManifestDto
{
    public long Id { get; init; }
    public string ManifestNumber { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long DriverId { get; init; }
    public string DriverName { get; init; } = string.Empty;
    public long? VehicleId { get; init; }
    public string? VehiclePlateNumber { get; init; }
    public DateTime ManifestDateUtc { get; init; }
    public DriverManifestStatus Status { get; init; }
    public int ItemCount { get; init; }
    public int DeliveredCount { get; init; }
    public int FailedCount { get; init; }
    public int PendingCount { get; init; }
    /// <summary>Total COD to collect on this manifest (information only; collection is a later step).</summary>
    public decimal TotalCodAmount { get; init; }
    public DateTime? DispatchedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    /// <summary>Only in GET by id.</summary>
    public IReadOnlyList<DriverManifestItemDto>? Items { get; init; }
}

/// <summary>SortBy: createdAtUtc (default, newest first), number. Drivers only get their own manifests.</summary>
public sealed class DriverManifestListQuery : DocumentListQuery
{
    public long? BranchId { get; set; }
    public long? DriverId { get; set; }
    public DriverManifestStatus? Status { get; set; }
}
```

### 6.8 `src/ShippingERP.Application/DTOs/Shipments/ShipmentDtos.cs`

```csharp
using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Shipments;

// ------------------------------------------------------------------ requests

/// <summary>One package of a shipment. Volumetric weight is calculated from the dimensions (all three or none).</summary>
public sealed class ShipmentPackageRequest
{
    public string? Description { get; set; }

    /// <summary>Actual weight in kg (up to 3 decimals), greater than 0.</summary>
    public decimal WeightKg { get; set; }

    public decimal? LengthCm { get; set; }

    public decimal? WidthCm { get; set; }

    public decimal? HeightCm { get; set; }
}

/// <summary>
/// Body of POST /api/v1/shipments, PUT /api/v1/shipments/{id} and POST /api/v1/shipments/quote.
///
/// Empty optional fields are filled automatically:
/// - OriginCityId: the sender address city, otherwise the origin branch city.
/// - DestinationCityId / DestinationZoneId: from the recipient address.
/// - Addresses: the default address of each customer.
/// - DestinationBranchId: the only active branch of the destination city (if there is exactly one).
/// The shipping charge is calculated from the pricing rules; ManualShippingCharge and DiscountAmount
/// require the permission Shipments.OverridePrice and a ManualPriceReason. A change bigger than the approval
/// threshold (shipping settings) also requires Pricing.ApproveOverride; other users create the shipment at the
/// rule price and request the change with POST /shipments/{id}/apply-price.
/// </summary>
public sealed class SaveShipmentRequest
{
    public long SenderCustomerId { get; set; }

    public long RecipientCustomerId { get; set; }

    public long? SenderAddressId { get; set; }

    public long? RecipientAddressId { get; set; }

    /// <summary>The branch registering the shipment. Ignored on update (the origin never changes).</summary>
    public long OriginBranchId { get; set; }

    public long? DestinationBranchId { get; set; }

    public long? OriginCityId { get; set; }

    public long? DestinationCityId { get; set; }

    public long? DestinationZoneId { get; set; }

    public long ServiceTypeId { get; set; }

    /// <summary>What is inside, e.g. "ملابس", "أدوية", "قطع إلكترونية".</summary>
    public string PackageDescription { get; set; } = string.Empty;

    /// <summary>At least one package.</summary>
    public List<ShipmentPackageRequest> Packages { get; set; } = new();

    /// <summary>Step 9: price by weight (kg, default) or by volume (m³).</summary>
    public PricingBasis PricingBasis { get; set; } = PricingBasis.Weight;

    /// <summary>
    /// Step 9: total volume in m³ (3 decimals). Required (> 0) for volume pricing unless every package has its
    /// dimensions (then the volume is calculated from them).
    /// </summary>
    public decimal TotalVolumeCbm { get; set; }

    /// <summary>Step 9: who pays (is invoiced for) the shipping charge. Default Sender.</summary>
    public ChargesPayer ChargesPaidBy { get; set; } = ChargesPayer.Sender;

    public decimal DeclaredValue { get; set; }

    /// <summary>Cannot be more than DeclaredValue.</summary>
    public decimal InsuranceValue { get; set; }

    public bool IsCOD { get; set; }

    /// <summary>Required (> 0) when IsCOD is true; must be 0 otherwise.</summary>
    public decimal CODAmount { get; set; }

    /// <summary>A driver collects the shipment from the sender (pickup fee of the price rule).</summary>
    public bool RequiresPickup { get; set; }

    /// <summary>The company packs the shipment (packaging fee of the price rule).</summary>
    public bool RequiresPackaging { get; set; }

    /// <summary>
    /// Optional manual shipping subtotal (replaces base + weight + service fees; the COD commission and the tax are
    /// still added). Requires Shipments.OverridePrice and ManualPriceReason.
    /// </summary>
    public decimal? ManualShippingCharge { get; set; }

    /// <summary>Manual discount. Requires Shipments.OverridePrice and ManualPriceReason when greater than 0.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Required with a manual charge or a discount.</summary>
    public string? ManualPriceReason { get; set; }

    public string? SenderNotes { get; set; }

    public string? InternalNotes { get; set; }

    /// <summary>The sender's own order number (unique per sender).</summary>
    public string? ExternalReference { get; set; }
}

/// <summary>
/// Search: tracking number, shipment number, external reference, sender/recipient name and phone.
/// SortBy: createdAtUtc (default, newest first), shipmentNumber, trackingNumber, status, totalCharge,
/// codAmount, chargeableWeight, deliveredAtUtc.
/// </summary>
public sealed class ShipmentListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields =
    {
        "createdAtUtc", "shipmentNumber", "trackingNumber", "status", "totalCharge", "codAmount", "chargeableWeight", "deliveredAtUtc"
    };

    /// <summary>Exact tracking number.</summary>
    public string? TrackingNumber { get; set; }

    /// <summary>Exact shipment number (SHP-2026-000001).</summary>
    public string? ShipmentNumber { get; set; }

    /// <summary>Sender name contains / phone starts with this text.</summary>
    public string? Sender { get; set; }

    /// <summary>Recipient name contains / phone starts with this text.</summary>
    public string? Recipient { get; set; }

    public ShipmentStatus? Status { get; set; }

    /// <summary>Shipments whose origin, current or destination branch is this branch.</summary>
    public long? BranchId { get; set; }

    public long? OriginBranchId { get; set; }

    public long? CurrentBranchId { get; set; }

    public long? DestinationBranchId { get; set; }

    /// <summary>Assigned driver profile id.</summary>
    public long? DriverId { get; set; }

    public long? SenderCustomerId { get; set; }

    public long? RecipientCustomerId { get; set; }

    public long? OriginCityId { get; set; }

    public long? DestinationCityId { get; set; }

    public long? ServiceTypeId { get; set; }

    public bool? IsCOD { get; set; }

    /// <summary>Created on or after (UTC).</summary>
    public DateTime? CreatedFromUtc { get; set; }

    /// <summary>Created before (UTC, exclusive).</summary>
    public DateTime? CreatedToUtc { get; set; }
}

/// <summary>Body of PATCH /api/v1/shipments/{id}/status (scan-style changes only).</summary>
public sealed class ChangeShipmentStatusRequest
{
    /// <summary>PickedUp, ReceivedAtBranch or InWarehouse. Other statuses have their own workflow endpoint.</summary>
    public ShipmentStatus Status { get; set; }

    /// <summary>Required for InWarehouse: a warehouse of the shipment's current branch.</summary>
    public long? WarehouseId { get; set; }

    public string? Notes { get; set; }

    /// <summary>When it happened (default now). Cannot be in the future.</summary>
    public DateTime? OccurredAtUtc { get; set; }
}

public sealed class CancelShipmentRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Body of PATCH /api/v1/shipments/{id}/driver. DriverId = null removes the assignment.</summary>
public sealed class AssignDriverRequest
{
    public long? DriverId { get; set; }

    /// <summary>Default: the driver's default vehicle.</summary>
    public long? VehicleId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Body of POST /api/v1/shipments/{id}/status-corrections. Sets any status (except Created) outside the normal
/// rules. Requires Shipments.CorrectStatus and a reason; recorded with IsCorrection = true.
/// </summary>
public sealed class CorrectShipmentStatusRequest
{
    public ShipmentStatus Status { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Optional: also correct where the shipment physically is.</summary>
    public long? CurrentBranchId { get; set; }

    /// <summary>Required when the corrected status is InWarehouse.</summary>
    public long? WarehouseId { get; set; }
}

// ------------------------------------------------------------------ responses

public sealed class ShipmentPackageDto
{
    public long Id { get; init; }
    public int SequenceNumber { get; init; }
    public string PackageBarcode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal WeightKg { get; init; }
    public decimal? LengthCm { get; init; }
    public decimal? WidthCm { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal VolumetricWeightKg { get; init; }
}

/// <summary>One row of the shipment list.</summary>
public sealed class ShipmentListItemDto
{
    public long Id { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public ShipmentStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public long SenderCustomerId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string SenderPhone { get; init; } = string.Empty;
    public long RecipientCustomerId { get; init; }
    public string RecipientName { get; init; } = string.Empty;
    public string RecipientPhone { get; init; } = string.Empty;
    public long OriginBranchId { get; init; }
    public string OriginBranchName { get; init; } = string.Empty;
    public long CurrentBranchId { get; init; }
    public string CurrentBranchName { get; init; } = string.Empty;
    public long? DestinationBranchId { get; init; }
    public string? DestinationBranchName { get; init; }
    public long DestinationCityId { get; init; }
    public string DestinationCityName { get; init; } = string.Empty;
    public long ServiceTypeId { get; init; }
    public string ServiceTypeName { get; init; } = string.Empty;
    public long? AssignedDriverId { get; init; }
    public string? AssignedDriverName { get; init; }
    public int PackageCount { get; init; }
    public decimal ChargeableWeight { get; init; }
    public decimal TotalVolumeCbm { get; init; }
    public PricingBasis PricingBasis { get; init; }
    public ChargesPayer ChargesPaidBy { get; init; }
    public bool IsCOD { get; init; }
    public decimal CODAmount { get; init; }
    public decimal TotalCharge { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? DeliveredAtUtc { get; init; }
}

/// <summary>Full shipment details.</summary>
public sealed class ShipmentDto
{
    public long Id { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public ShipmentStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;

    public long SenderCustomerId { get; init; }
    public string SenderCode { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string SenderPhone { get; init; } = string.Empty;
    public long? SenderAddressId { get; init; }
    public string? SenderAddressLine { get; init; }

    public long RecipientCustomerId { get; init; }
    public string RecipientCode { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string RecipientPhone { get; init; } = string.Empty;
    public long? RecipientAddressId { get; init; }
    public string? RecipientAddressLine { get; init; }
    public string? RecipientContactPhone { get; init; }

    public long OriginBranchId { get; init; }
    public string OriginBranchName { get; init; } = string.Empty;
    public long CurrentBranchId { get; init; }
    public string CurrentBranchName { get; init; } = string.Empty;
    public long? CurrentWarehouseId { get; init; }
    public string? CurrentWarehouseName { get; init; }
    public long? DestinationBranchId { get; init; }
    public string? DestinationBranchName { get; init; }
    public long OriginCityId { get; init; }
    public string OriginCityName { get; init; } = string.Empty;
    public long DestinationCityId { get; init; }
    public string DestinationCityName { get; init; } = string.Empty;
    public long? OriginZoneId { get; init; }
    public string? OriginZoneName { get; init; }
    public long? DestinationZoneId { get; init; }
    public string? DestinationZoneName { get; init; }
    public long ServiceTypeId { get; init; }
    public string ServiceTypeName { get; init; } = string.Empty;

    public long? AssignedDriverId { get; init; }
    public string? AssignedDriverName { get; init; }
    public long? VehicleId { get; init; }
    public string? VehiclePlateNumber { get; init; }
    public int DeliveryAttemptCount { get; init; }
    public long? ReturnBranchId { get; init; }

    public int PackageCount { get; init; }
    public decimal ActualWeight { get; init; }
    public decimal VolumetricWeight { get; init; }
    public decimal ChargeableWeight { get; init; }
    public decimal TotalVolumeCbm { get; init; }
    public PricingBasis PricingBasis { get; init; }
    public ChargesPayer ChargesPaidBy { get; init; }
    public string PackageDescription { get; init; } = string.Empty;

    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal DeclaredValue { get; init; }
    public decimal InsuranceValue { get; init; }
    public bool IsCOD { get; init; }
    public decimal CODAmount { get; init; }
    public bool RequiresPickup { get; init; }
    public bool RequiresPackaging { get; init; }
    public decimal ShippingCharge { get; init; }
    /// <summary>Total discount (contract + manual).</summary>
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalCharge { get; init; }

    /// <summary>Current manual subtotal (null = rule price). Send it back unchanged in PUT to keep it.</summary>
    public decimal? ManualShippingCharge { get; init; }
    /// <summary>Current manual discount (send it back as DiscountAmount in PUT to keep it).</summary>
    public decimal ManualDiscountAmount { get; init; }

    /// <summary>Number of the active invoice of the shipping charge (draft invoices have no number yet).</summary>
    public long? InvoiceId { get; init; }
    public string? InvoiceNumber { get; init; }
    /// <summary>True while a manual price change waits for approval.</summary>
    public bool HasPendingPriceOverride { get; init; }

    /// <summary>Step 6: the COD record of the shipment (null when it was never a COD shipment).</summary>
    public long? CodTransactionId { get; init; }
    public CODStatus? CodStatus { get; init; }
    public decimal? CodCollectedAmount { get; init; }

    public string? SenderNotes { get; init; }
    public string? InternalNotes { get; init; }
    public string? CancellationReason { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public long? UpdatedByUserId { get; init; }
    public DateTime? PickedUpAtUtc { get; init; }
    public DateTime? DeliveredAtUtc { get; init; }
    public DateTime? ReturnedAtUtc { get; init; }
    public DateTime? CancelledAtUtc { get; init; }

    public IReadOnlyList<ShipmentPackageDto> Packages { get; init; } = Array.Empty<ShipmentPackageDto>();

    /// <summary>Statuses reachable from the current one by the normal workflows.</summary>
    public IReadOnlyList<ShipmentStatus> NextStatuses { get; init; } = Array.Empty<ShipmentStatus>();
}

public sealed class ShipmentStatusHistoryDto
{
    public long Id { get; init; }
    public ShipmentStatus? FromStatus { get; init; }
    public string? FromStatusName { get; init; }
    public ShipmentStatus ToStatus { get; init; }
    public string ToStatusName { get; init; } = string.Empty;
    public ShipmentStatusChangeSource Source { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public long? BranchId { get; init; }
    public string? BranchName { get; init; }
    public long? WarehouseId { get; init; }
    public long? DriverId { get; init; }
    public string? Notes { get; init; }
    public string? ReferenceType { get; init; }
    public long? ReferenceId { get; init; }
    public string? ReferenceNumber { get; init; }
    public bool IsCorrection { get; init; }
    public long? ChangedByUserId { get; init; }
    public string? ChangedByUserName { get; init; }
    public DateTime RecordedAtUtc { get; init; }
}

/// <summary>An applied weight tier.</summary>
public sealed class PricingWeightTierDto
{
    public decimal FromKg { get; init; }
    public decimal? ToKg { get; init; }
    public decimal Kg { get; init; }
    public decimal PricePerKg { get; init; }
    public decimal Charge { get; init; }
}

/// <summary>
/// A price breakdown: a pricing snapshot, or the result of POST /shipments/quote and
/// POST /shipments/{id}/calculate-price (then Id is null).
///
///   ShippingSubtotal    = BaseCharge + WeightCharge + ServiceFeesTotal   (or the manual charge)
///   TotalShippingCharge = ShippingSubtotal - DiscountAmount
///   TaxableAmount       = TotalShippingCharge + CodCommission
///   NetTotal            = TaxableAmount + TaxAmount                      (= the shipment TotalCharge)
/// </summary>
public sealed class ShipmentPricingDto
{
    public long? Id { get; init; }
    public string Reason { get; init; } = string.Empty;
    public PricingSource PricingSource { get; init; }
    public long? PricingPlanId { get; init; }
    public string? PricingPlanCode { get; init; }
    public long? PricingRuleId { get; init; }
    public string? PricingRuleName { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;

    // Inputs
    public decimal ActualWeight { get; init; }
    public decimal VolumetricWeight { get; init; }
    public decimal ChargeableWeight { get; init; }
    public int VolumetricDivisor { get; init; }
    public decimal WeightRoundingStepKg { get; init; }
    public int PackageCount { get; init; }
    public PricingBasis PricingBasis { get; init; }
    public decimal VolumeCbm { get; init; }
    public decimal PricePerCubicMeter { get; init; }

    /// <summary>VolumeCbm x PricePerCubicMeter (shipments priced by volume).</summary>
    public decimal VolumeCharge { get; init; }
    public decimal InsuranceValue { get; init; }
    public bool RequiresPickup { get; init; }
    public bool RequiresPackaging { get; init; }
    public bool IsRemoteArea { get; init; }
    public bool IsReturn { get; init; }

    // Base and weight
    public decimal BaseCharge { get; init; }
    public decimal IncludedWeightKg { get; init; }
    public decimal AdditionalWeightKg { get; init; }
    public decimal WeightCharge { get; init; }
    public IReadOnlyList<PricingWeightTierDto> WeightTiers { get; init; } = Array.Empty<PricingWeightTierDto>();

    // Service fees
    public decimal PackageFee { get; init; }
    public decimal InsuranceFee { get; init; }
    public decimal PickupFee { get; init; }
    public decimal PackagingFee { get; init; }
    public decimal RemoteAreaFee { get; init; }
    public decimal ReturnFee { get; init; }
    public decimal ServiceFeesTotal { get; init; }

    // Subtotal and discount
    public decimal ShippingSubtotal { get; init; }
    /// <summary>The rule subtotal that a manual charge replaced (null otherwise).</summary>
    public decimal? CalculatedShippingSubtotal { get; init; }
    public decimal ContractDiscountPercentage { get; init; }
    public decimal ContractDiscountAmount { get; init; }
    public decimal ManualDiscountAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TotalShippingCharge { get; init; }

    // COD commission
    public decimal CodAmount { get; init; }
    public decimal CodFeeFixed { get; init; }
    public decimal CodFeePercentage { get; init; }
    public decimal CodFeeMinimum { get; init; }
    public decimal CodCommission { get; init; }

    // Tax and totals
    public decimal TaxableAmount { get; init; }
    public long? TaxRateId { get; init; }
    public decimal TaxPercentage { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal NetTotal { get; init; }

    /// <summary>Gross charge stored on the shipment (ShippingSubtotal + CodCommission).</summary>
    public decimal ShippingCharge { get; init; }

    /// <summary>Same as NetTotal (kept for step-4 clients).</summary>
    public decimal TotalCharge { get; init; }

    public string? OverrideReason { get; init; }
    public long? PriceOverrideId { get; init; }
    public DateTime? CalculatedAtUtc { get; init; }
}

/// <summary>Tracking view: GET /api/v1/shipments/track/{trackingNumber}.</summary>
public sealed class ShipmentTrackingDto
{
    public long ShipmentId { get; init; }
    public string TrackingNumber { get; init; } = string.Empty;
    public string ShipmentNumber { get; init; } = string.Empty;
    public ShipmentStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string OriginCityName { get; init; } = string.Empty;
    public string DestinationCityName { get; init; } = string.Empty;
    public string CurrentBranchName { get; init; } = string.Empty;
    public int PackageCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? DeliveredAtUtc { get; init; }
    public IReadOnlyList<ShipmentTrackingEventDto> Events { get; init; } = Array.Empty<ShipmentTrackingEventDto>();
}

public sealed class ShipmentTrackingEventDto
{
    public DateTime OccurredAtUtc { get; init; }
    public ShipmentStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string? BranchName { get; init; }
    public string? Notes { get; init; }
}
```

### 6.9 `src/ShippingERP.Application/Validators/Customers/CustomerValidators.cs`

```csharp
using FluentValidation;
using ShippingERP.Application.DTOs.Customers;
using ShippingERP.Application.Validators.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.Validators.Customers;

public sealed class SaveCustomerRequestValidator : AbstractValidator<SaveCustomerRequest>
{
    public SaveCustomerRequestValidator()
    {
        RuleFor(x => x.Code!)
            .ValidCode(20)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));
        RuleFor(x => x.CustomerType).IsInEnum().WithMessage("نوع العميل غير صالح.");
        RuleFor(x => x.Roles)
            .Must(role => role is CustomerRole.Sender or CustomerRole.Recipient or CustomerRole.Both)
            .WithMessage("دور العميل يجب أن يكون Sender أو Recipient أو Both.");
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.Phone).RequiredPhone();
        RuleFor(x => x.AlternatePhone).OptionalPhone();
        RuleFor(x => x.Email).OptionalEmail();
        RuleFor(x => x.NationalOrCompanyNumber).OptionalText(50);
        RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0m).WithMessage("حد الائتمان لا يمكن أن يكون سالباً.")
            .PrecisionScale(18, 2, true).WithMessage("حد الائتمان يقبل منزلتين عشريتين على الأكثر.");
        RuleFor(x => x.OpeningBalance)
            .PrecisionScale(18, 2, true).WithMessage("الرصيد الافتتاحي يقبل منزلتين عشريتين على الأكثر.");
        RuleFor(x => x.Notes).OptionalText(1000);
        RuleFor(x => x.DefaultBranchId).ValidId("الفرع الافتراضي مطلوب.");
        RuleFor(x => x.PricingPlanId).ValidOptionalId("خطة التسعير غير صالحة.").When(x => x.PricingPlanId.HasValue);
    }
}

public sealed class CustomerListQueryValidator : AbstractValidator<CustomerListQuery>
{
    public CustomerListQueryValidator()
    {
        Include(new PagedQueryRules<CustomerListQuery>(CustomerListQuery.AllowedSortFields));
    }
}

public sealed class SaveCustomerAddressRequestValidator : AbstractValidator<SaveCustomerAddressRequest>
{
    public SaveCustomerAddressRequestValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("اسم العنوان مطلوب (مثل: المنزل، المكتب).")
            .MaximumLength(50).WithMessage("اسم العنوان طويل جداً.");
        RuleFor(x => x.CityId).ValidId("المدينة مطلوبة.");
        RuleFor(x => x.ZoneId).ValidOptionalId("المنطقة غير صالحة.").When(x => x.ZoneId.HasValue);
        RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage("العنوان التفصيلي مطلوب.")
            .MaximumLength(300).WithMessage("العنوان طويل جداً.");
        RuleFor(x => x.Landmark).OptionalText(200);
        RuleFor(x => x.BuildingNumber).OptionalText(20);
        RuleFor(x => x.PostalCode).OptionalText(20);
        RuleFor(x => x.ContactName).OptionalText(150);
        RuleFor(x => x.Phone).OptionalPhone();
        RuleFor(x => x.AlternatePhone).OptionalPhone();
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m).WithMessage("خط العرض يجب أن يكون بين -90 و 90.")
            .When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m).WithMessage("خط الطول يجب أن يكون بين -180 و 180.")
            .When(x => x.Longitude.HasValue);
        RuleFor(x => x.Longitude)
            .NotNull().WithMessage("أدخل خط الطول مع خط العرض، أو اتركهما فارغين.")
            .When(x => x.Latitude.HasValue);
        RuleFor(x => x.Latitude)
            .NotNull().WithMessage("أدخل خط العرض مع خط الطول، أو اتركهما فارغين.")
            .When(x => x.Longitude.HasValue);
    }
}

public sealed class SavePricingPlanRequestValidator : AbstractValidator<SavePricingPlanRequest>
{
    public SavePricingPlanRequestValidator()
    {
        RuleFor(x => x.Code).ValidCode(20);
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.CurrencyId).ValidId("العملة مطلوبة.");
        RuleFor(x => x.ValidToUtc)
            .GreaterThan(x => x.ValidFromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.ValidFromUtc.HasValue && x.ValidToUtc.HasValue);
        RuleFor(x => x.ContractCustomerId).ValidOptionalId("العميل غير صالح.").When(x => x.ContractCustomerId.HasValue);
        RuleFor(x => x.IsDefault)
            .Equal(false).WithMessage("عقد العميل لا يمكن أن يكون خطة التسعير الافتراضية.")
            .When(x => x.ContractCustomerId.HasValue);
        RuleFor(x => x.ContractReference).OptionalText(50);
        RuleFor(x => x.ContractReference)
            .Empty().WithMessage("مرجع العقد يُستخدم مع عقود العملاء فقط.")
            .When(x => !x.ContractCustomerId.HasValue);
        RuleFor(x => x.Notes).OptionalText(1000);
    }
}

public sealed class PricingPlanListQueryValidator : AbstractValidator<PricingPlanListQuery>
{
    public PricingPlanListQueryValidator()
    {
        Include(new PagedQueryRules<PricingPlanListQuery>(PricingPlanListQuery.AllowedSortFields));
        RuleFor(x => x.CurrencyId).ValidOptionalId("العملة غير صالحة.").When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.ContractCustomerId).ValidOptionalId("العميل غير صالح.").When(x => x.ContractCustomerId.HasValue);
    }
}

public sealed class SavePriceRuleConditionRequestValidator : AbstractValidator<SavePriceRuleConditionRequest>
{
    public SavePriceRuleConditionRequestValidator()
    {
        RuleFor(x => x.ConditionType).IsInEnum().WithMessage("نوع الشرط غير صالح.");

        // Reference conditions: an id, no range.
        RuleFor(x => x.ReferenceId)
            .NotNull().WithMessage("حدد المنطقة / المدينة / نوع الخدمة لهذا الشرط.")
            .GreaterThan(0).WithMessage("المعرّف غير صالح.")
            .When(x => PriceConditionTypes.IsReference(x.ConditionType));
        RuleFor(x => x)
            .Must(x => x.MinValue is null && x.MaxValue is null)
            .OverridePropertyName("minValue")
            .WithMessage("شروط المناطق والمدن ونوع الخدمة لا تقبل حداً أدنى أو أعلى.")
            .When(x => PriceConditionTypes.IsReference(x.ConditionType));

        // Range conditions: min and/or max, no id.
        RuleFor(x => x.ReferenceId)
            .Null().WithMessage("شروط الوزن وعدد الطرود لا تقبل معرّفاً.")
            .When(x => PriceConditionTypes.IsRange(x.ConditionType));
        RuleFor(x => x)
            .Must(x => x.MinValue.HasValue || x.MaxValue.HasValue)
            .OverridePropertyName("minValue")
            .WithMessage("أدخل الحد الأدنى أو الحد الأعلى (أو كليهما).")
            .When(x => PriceConditionTypes.IsRange(x.ConditionType));
        RuleFor(x => x.MinValue)
            .GreaterThanOrEqualTo(0m).WithMessage("الحد الأدنى لا يمكن أن يكون سالباً.")
            .PrecisionScale(10, 3, true).WithMessage("القيمة تقبل 3 منازل عشرية على الأكثر.")
            .When(x => x.MinValue.HasValue);
        RuleFor(x => x.MaxValue)
            .GreaterThan(x => x.MinValue ?? 0m).WithMessage("الحد الأعلى يجب أن يكون أكبر من الحد الأدنى.")
            .PrecisionScale(10, 3, true).WithMessage("القيمة تقبل 3 منازل عشرية على الأكثر.")
            .When(x => x.MaxValue.HasValue);
        RuleFor(x => x)
            .Must(x => (x.MinValue is null || x.MinValue == decimal.Truncate(x.MinValue.Value)) &&
                       (x.MaxValue is null || x.MaxValue == decimal.Truncate(x.MaxValue.Value)))
            .OverridePropertyName("minValue")
            .WithMessage("عدد الطرود رقم صحيح.")
            .When(x => x.ConditionType == PriceConditionType.PackageCount);
    }
}

public sealed class SavePriceRuleWeightTierRequestValidator : AbstractValidator<SavePriceRuleWeightTierRequest>
{
    public SavePriceRuleWeightTierRequestValidator()
    {
        RuleFor(x => x.FromWeightKg)
            .GreaterThanOrEqualTo(0m).WithMessage("بداية الشريحة لا يمكن أن تكون سالبة.")
            .PrecisionScale(10, 3, true).WithMessage("الوزن يقبل 3 منازل عشرية على الأكثر.");
        RuleFor(x => x.ToWeightKg)
            .GreaterThan(x => x.FromWeightKg).WithMessage("نهاية الشريحة يجب أن تكون أكبر من بدايتها.")
            .PrecisionScale(10, 3, true).WithMessage("الوزن يقبل 3 منازل عشرية على الأكثر.")
            .When(x => x.ToWeightKg.HasValue);
        RuleFor(x => x.PricePerKg).PriceAmount("سعر الكيلو");
    }
}

public sealed class SavePricingRuleRequestValidator : AbstractValidator<SavePricingRuleRequest>
{
    public const int MaxConditions = 50;
    public const int MaxTiers = 20;

    public SavePricingRuleRequestValidator()
    {
        RuleFor(x => x.ArabicName).ArabicName();
        RuleFor(x => x.EnglishName).OptionalText(150);
        RuleFor(x => x.Priority).InclusiveBetween(0, 1000).WithMessage("الأولوية يجب أن تكون بين 0 و 1000.");
        RuleFor(x => x.ValidToUtc)
            .GreaterThan(x => x.ValidFromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.ValidFromUtc.HasValue && x.ValidToUtc.HasValue);

        RuleFor(x => x.Conditions)
            .Must(list => list.Count <= MaxConditions).WithMessage($"الحد الأقصى {MaxConditions} شرطاً في القاعدة.")
            .Must(list => list.Where(c => PriceConditionTypes.IsRange(c.ConditionType)).GroupBy(c => c.ConditionType).All(g => g.Count() == 1))
            .WithMessage("شرط الوزن وشرط عدد الطرود يُدخل كل منهما مرة واحدة على الأكثر.")
            .Must(list => list.Where(c => c.ReferenceId.HasValue).GroupBy(c => new { c.ConditionType, c.ReferenceId }).All(g => g.Count() == 1))
            .WithMessage("يوجد شرط مكرر.");
        RuleForEach(x => x.Conditions).Cascade(CascadeMode.Continue).SetValidator(new SavePriceRuleConditionRequestValidator());

        RuleFor(x => x.BasePrice).PriceAmount("السعر الأساسي");
        RuleFor(x => x.PricePerCubicMeter).PriceAmount("سعر المتر المكعب");
        RuleFor(x => x.AppliesToBasis).IsInEnum().WithMessage("طريقة التسعير غير صالحة.").When(x => x.AppliesToBasis.HasValue);
        RuleFor(x => x.IncludedWeightKg)
            .GreaterThanOrEqualTo(0m).WithMessage("الوزن المشمول لا يمكن أن يكون سالباً.")
            .LessThanOrEqualTo(10000m).WithMessage("الوزن المشمول كبير جداً.")
            .PrecisionScale(10, 3, true).WithMessage("الوزن يقبل 3 منازل عشرية على الأكثر.");

        RuleFor(x => x.WeightTiers)
            .Must(list => list.Count <= MaxTiers).WithMessage($"الحد الأقصى {MaxTiers} شريحة وزن.")
            .Must(TiersAreContiguous)
            .WithMessage("شرائح الوزن يجب أن تكون متتالية دون تداخل أو فراغ، ولا تبدأ قبل الوزن المشمول، والأخيرة فقط بلا نهاية.");
        RuleFor(x => x)
            .Must(x => x.WeightTiers.Count == 0 || x.WeightTiers.Min(t => t.FromWeightKg) <= x.IncludedWeightKg)
            .OverridePropertyName("weightTiers")
            .WithMessage("الشريحة الأولى يجب أن تبدأ من الوزن المشمول بالسعر الأساسي أو قبله.");
        RuleForEach(x => x.WeightTiers).Cascade(CascadeMode.Continue).SetValidator(new SavePriceRuleWeightTierRequestValidator());

        RuleFor(x => x.IncludedPackageCount)
            .InclusiveBetween(0, 1000).WithMessage("عدد الطرود المشمولة يجب أن يكون بين 0 و 1000.");
        RuleFor(x => x.PerPackageFee).PriceAmount("رسم الطرد الإضافي");

        RuleFor(x => x.CodFeeFixed).PriceAmount("رسوم التحصيل الثابتة");
        RuleFor(x => x.CodFeePercentage).Percentage("نسبة رسوم التحصيل");
        RuleFor(x => x.CodFeeMinimum).PriceAmount("الحد الأدنى لعمولة التحصيل");
        RuleFor(x => x.InsuranceFeePercentage).Percentage("نسبة رسوم التأمين");
        RuleFor(x => x.InsuranceFeeMinimum).PriceAmount("الحد الأدنى لرسوم التأمين");
        RuleFor(x => x.PickupFee).PriceAmount("رسم الاستلام من المرسل");
        RuleFor(x => x.PackagingFee).PriceAmount("رسم التغليف");
        RuleFor(x => x.RemoteAreaFee).PriceAmount("رسم المناطق النائية");
        RuleFor(x => x.ReturnFee).PriceAmount("رسم الإرجاع");
        RuleFor(x => x.DiscountPercentage).Percentage("نسبة خصم العقد");
        RuleFor(x => x.TaxRateId).ValidOptionalId("نسبة الضريبة غير صالحة.").When(x => x.TaxRateId.HasValue);
        RuleFor(x => x.Notes).OptionalText(500);
    }

    /// <summary>Sorted by start: each tier starts where the previous ends; only the last may be open-ended.</summary>
    private static bool TiersAreContiguous(SavePricingRuleRequest request, List<SavePriceRuleWeightTierRequest> tiers)
    {
        if (tiers.Count == 0)
        {
            return true;
        }

        var ordered = tiers.OrderBy(t => t.FromWeightKg).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var isLast = i == ordered.Count - 1;
            if (!isLast && ordered[i].ToWeightKg is null)
            {
                return false;
            }

            if (!isLast && ordered[i].ToWeightKg != ordered[i + 1].FromWeightKg)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Money and percentage rules of the pricing validators.</summary>
public static class PriceRules
{
    public static IRuleBuilderOptions<T, decimal> PriceAmount<T>(this IRuleBuilder<T, decimal> rule, string name)
    {
        return rule
            .GreaterThanOrEqualTo(0m).WithMessage($"{name} لا يمكن أن يكون سالباً.")
            .LessThanOrEqualTo(999_999_999_999m).WithMessage($"{name} كبير جداً.")
            .PrecisionScale(18, 2, true).WithMessage($"{name} يقبل منزلتين عشريتين على الأكثر.");
    }

    public static IRuleBuilderOptions<T, decimal> Percentage<T>(this IRuleBuilder<T, decimal> rule, string name)
    {
        return rule
            .InclusiveBetween(0m, 100m).WithMessage($"{name} يجب أن تكون بين 0 و 100.")
            .PrecisionScale(5, 2, true).WithMessage($"{name} تقبل منزلتين عشريتين على الأكثر.");
    }
}
```

### 6.10 `src/ShippingERP.Application/Validators/Shipments/ShipmentValidators.cs`

```csharp
using FluentValidation;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Validators.Common;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;

namespace ShippingERP.Application.Validators.Shipments;

public sealed class ShipmentPackageRequestValidator : AbstractValidator<ShipmentPackageRequest>
{
    public const decimal MaxWeightKg = 10000m;
    public const decimal MaxDimensionCm = 1000m;

    public ShipmentPackageRequestValidator()
    {
        RuleFor(x => x.Description).OptionalText(200);

        RuleFor(x => x.WeightKg)
            .GreaterThan(0m).WithMessage("وزن الطرد يجب أن يكون أكبر من صفر.")
            .LessThanOrEqualTo(MaxWeightKg).WithMessage($"وزن الطرد يجب ألا يزيد عن {MaxWeightKg} كغ.")
            .PrecisionScale(10, 3, true).WithMessage("الوزن يقبل 3 منازل عشرية على الأكثر.");

        RuleFor(x => x.LengthCm).ValidDimension("الطول").When(x => x.LengthCm.HasValue);
        RuleFor(x => x.WidthCm).ValidDimension("العرض").When(x => x.WidthCm.HasValue);
        RuleFor(x => x.HeightCm).ValidDimension("الارتفاع").When(x => x.HeightCm.HasValue);

        RuleFor(x => x)
            .Must(x => (x.LengthCm.HasValue && x.WidthCm.HasValue && x.HeightCm.HasValue) ||
                       (!x.LengthCm.HasValue && !x.WidthCm.HasValue && !x.HeightCm.HasValue))
            .OverridePropertyName("dimensions")
            .WithMessage("أدخل الطول والعرض والارتفاع معاً، أو اتركها كلها فارغة.");
    }
}

internal static class DimensionRules
{
    public static IRuleBuilderOptions<T, decimal?> ValidDimension<T>(this IRuleBuilder<T, decimal?> rule, string name)
    {
        return rule
            .GreaterThan(0m).WithMessage($"{name} يجب أن يكون أكبر من صفر.")
            .LessThanOrEqualTo(ShipmentPackageRequestValidator.MaxDimensionCm)
            .WithMessage($"{name} يجب ألا يزيد عن {ShipmentPackageRequestValidator.MaxDimensionCm} سم.")
            .PrecisionScale(10, 2, true).WithMessage($"{name} يقبل منزلتين عشريتين على الأكثر.");
    }

    public static IRuleBuilderOptions<T, decimal> Money<T>(this IRuleBuilder<T, decimal> rule, string name)
    {
        return rule
            .GreaterThanOrEqualTo(0m).WithMessage($"{name} لا يمكن أن يكون سالباً.")
            .LessThanOrEqualTo(999_999_999_999m).WithMessage($"{name} كبير جداً.")
            .PrecisionScale(18, 2, true).WithMessage($"{name} يقبل منزلتين عشريتين على الأكثر.");
    }

    public static IRuleBuilderOptions<T, string?> RequiredReason<T>(this IRuleBuilder<T, string?> rule, int maxLength = 500)
    {
        return rule
            .NotEmpty().WithMessage("السبب مطلوب.")
            .MaximumLength(maxLength).WithMessage($"يجب ألا يزيد السبب عن {maxLength} حرفاً.");
    }
}

public sealed class SaveShipmentRequestValidator : AbstractValidator<SaveShipmentRequest>
{
    public const int MaxPackages = 500;
    public const decimal MaxVolumeCbm = 10000m;

    public SaveShipmentRequestValidator()
    {
        RuleFor(x => x.SenderCustomerId).ValidId("المرسل مطلوب.");
        RuleFor(x => x.RecipientCustomerId).ValidId("المستلم مطلوب.");
        RuleFor(x => x.SenderAddressId).ValidOptionalId("عنوان المرسل غير صالح.").When(x => x.SenderAddressId.HasValue);
        RuleFor(x => x.RecipientAddressId).ValidOptionalId("عنوان المستلم غير صالح.").When(x => x.RecipientAddressId.HasValue);
        RuleFor(x => x.OriginBranchId).ValidId("فرع الإنشاء مطلوب.");
        RuleFor(x => x.DestinationBranchId).ValidOptionalId("فرع الوجهة غير صالح.").When(x => x.DestinationBranchId.HasValue);
        RuleFor(x => x.OriginCityId).ValidOptionalId("مدينة الانطلاق غير صالحة.").When(x => x.OriginCityId.HasValue);
        RuleFor(x => x.DestinationCityId).ValidOptionalId("مدينة الوجهة غير صالحة.").When(x => x.DestinationCityId.HasValue);
        RuleFor(x => x.DestinationZoneId).ValidOptionalId("منطقة الوجهة غير صالحة.").When(x => x.DestinationZoneId.HasValue);
        RuleFor(x => x.ServiceTypeId).ValidId("نوع الخدمة مطلوب.");

        RuleFor(x => x.PackageDescription)
            .NotEmpty().WithMessage("وصف المحتوى مطلوب.")
            .MaximumLength(300).WithMessage("وصف المحتوى طويل جداً.");

        RuleFor(x => x.Packages)
            .NotEmpty().WithMessage("يجب إدخال طرد واحد على الأقل.")
            .Must(packages => packages.Count <= MaxPackages).WithMessage($"الحد الأقصى {MaxPackages} طرداً في الشحنة الواحدة.");
        // Continue: report the errors of EVERY package, not only of the first invalid one
        // (the global rule-level cascade mode is Stop).
        RuleForEach(x => x.Packages).Cascade(CascadeMode.Continue).SetValidator(new ShipmentPackageRequestValidator());

        // Step 9: pricing basis, volume and payer.
        RuleFor(x => x.PricingBasis).IsInEnum().WithMessage("طريقة التسعير غير صالحة.");
        RuleFor(x => x.ChargesPaidBy).IsInEnum().WithMessage("الجهة الدافعة لأجور الشحن غير صالحة.");
        RuleFor(x => x.TotalVolumeCbm)
            .GreaterThanOrEqualTo(0m).WithMessage("الحجم لا يمكن أن يكون سالباً.")
            .LessThanOrEqualTo(MaxVolumeCbm).WithMessage($"الحجم يجب ألا يزيد عن {MaxVolumeCbm} م³.")
            .PrecisionScale(10, 3, true).WithMessage("الحجم يقبل 3 منازل عشرية على الأكثر.");
        RuleFor(x => x.TotalVolumeCbm)
            .GreaterThan(0m).WithMessage("الحجم بالمتر المكعب مطلوب عند التسعير بالحجم.")
            .When(x => x.PricingBasis == PricingBasis.Volume &&
                       (x.Packages.Count == 0 || x.Packages.Any(p => !(p.LengthCm.HasValue && p.WidthCm.HasValue && p.HeightCm.HasValue))));

        RuleFor(x => x.DeclaredValue).Money("القيمة المصرّح بها");
        RuleFor(x => x.InsuranceValue)
            .Money("قيمة التأمين")
            .LessThanOrEqualTo(x => x.DeclaredValue).WithMessage("قيمة التأمين لا يمكن أن تزيد عن القيمة المصرّح بها.");

        RuleFor(x => x.CODAmount)
            .GreaterThan(0m).WithMessage("مبلغ الدفع عند الاستلام مطلوب عندما تكون الشحنة COD.")
            .When(x => x.IsCOD);
        RuleFor(x => x.CODAmount)
            .Equal(0m).WithMessage("مبلغ الدفع عند الاستلام يجب أن يكون صفراً عندما لا تكون الشحنة COD.")
            .When(x => !x.IsCOD);
        RuleFor(x => x.CODAmount).Money("مبلغ الدفع عند الاستلام");

        RuleFor(x => x.ManualShippingCharge!.Value)
            .Money("سعر الشحن اليدوي")
            .OverridePropertyName("manualShippingCharge")
            .When(x => x.ManualShippingCharge.HasValue);

        RuleFor(x => x.DiscountAmount).Money("الخصم");
        RuleFor(x => x.DiscountAmount)
            .LessThanOrEqualTo(x => x.ManualShippingCharge!.Value)
            .WithMessage("الخصم لا يمكن أن يزيد عن سعر الشحن.")
            .When(x => x.ManualShippingCharge.HasValue);
        RuleFor(x => x.ManualPriceReason)
            .NotEmpty().WithMessage("سبب السعر اليدوي أو الخصم مطلوب.")
            .When(x => x.ManualShippingCharge.HasValue || x.DiscountAmount > 0m);
        RuleFor(x => x.ManualPriceReason).OptionalText(500);

        RuleFor(x => x.SenderNotes).OptionalText(500);
        RuleFor(x => x.InternalNotes).OptionalText(1000);
        RuleFor(x => x.ExternalReference).OptionalText(50);
    }
}

public sealed class ShipmentListQueryValidator : AbstractValidator<ShipmentListQuery>
{
    public ShipmentListQueryValidator()
    {
        Include(new PagedQueryRules<ShipmentListQuery>(ShipmentListQuery.AllowedSortFields));

        RuleFor(x => x.TrackingNumber).OptionalText(40);
        RuleFor(x => x.ShipmentNumber).OptionalText(40);
        RuleFor(x => x.Sender).OptionalText(100);
        RuleFor(x => x.Recipient).OptionalText(100);
        RuleFor(x => x.Status).IsInEnum().WithMessage("حالة الشحنة غير صالحة.").When(x => x.Status.HasValue);
        RuleFor(x => x.CreatedToUtc)
            .GreaterThan(x => x.CreatedFromUtc!.Value).WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.")
            .When(x => x.CreatedFromUtc.HasValue && x.CreatedToUtc.HasValue);
    }
}

public sealed class ChangeShipmentStatusRequestValidator : AbstractValidator<ChangeShipmentStatusRequest>
{
    public ChangeShipmentStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => ShipmentStatusRules.ManualTargets.Contains(status))
            .WithMessage("يمكن تغيير الحالة يدوياً إلى PickedUp أو ReceivedAtBranch أو InWarehouse فقط. " +
                         "باقي الحالات لها مسارها الخاص (التحويلات، بيانات التوزيع، التسليم، المرتجعات، الإلغاء).");
        RuleFor(x => x.WarehouseId)
            .NotNull().WithMessage("المستودع مطلوب عند إدخال الشحنة إلى المستودع.")
            .When(x => x.Status == ShipmentStatus.InWarehouse);
        RuleFor(x => x.WarehouseId).ValidOptionalId("المستودع غير صالح.").When(x => x.WarehouseId.HasValue);
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class CancelShipmentRequestValidator : AbstractValidator<CancelShipmentRequest>
{
    public CancelShipmentRequestValidator()
    {
        RuleFor(x => x.Reason).RequiredReason();
    }
}

public sealed class AssignDriverRequestValidator : AbstractValidator<AssignDriverRequest>
{
    public AssignDriverRequestValidator()
    {
        RuleFor(x => x.DriverId).ValidOptionalId("السائق غير صالح.").When(x => x.DriverId.HasValue);
        RuleFor(x => x.VehicleId).ValidOptionalId("المركبة غير صالحة.").When(x => x.VehicleId.HasValue);
        RuleFor(x => x.VehicleId)
            .Null().WithMessage("لا يمكن تحديد مركبة بدون سائق.")
            .When(x => !x.DriverId.HasValue);
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class CorrectShipmentStatusRequestValidator : AbstractValidator<CorrectShipmentStatusRequest>
{
    public CorrectShipmentStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("حالة الشحنة غير صالحة.")
            .NotEqual(ShipmentStatus.Created).WithMessage("لا يمكن إعادة الشحنة إلى حالة «تم الإنشاء».");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("سبب التصحيح مطلوب.")
            .MinimumLength(10).WithMessage("اكتب سبب التصحيح بوضوح (10 أحرف على الأقل).")
            .MaximumLength(500).WithMessage("سبب التصحيح طويل جداً.");
        RuleFor(x => x.CurrentBranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.CurrentBranchId.HasValue);
        RuleFor(x => x.WarehouseId)
            .NotNull().WithMessage("المستودع مطلوب عند التصحيح إلى «في المستودع».")
            .When(x => x.Status == ShipmentStatus.InWarehouse);
        RuleFor(x => x.WarehouseId).ValidOptionalId("المستودع غير صالح.").When(x => x.WarehouseId.HasValue);
    }
}

public sealed class UploadShipmentAttachmentRequestValidator : AbstractValidator<UploadShipmentAttachmentRequest>
{
    public UploadShipmentAttachmentRequestValidator()
    {
        RuleFor(x => x.AttachmentType)
            .IsInEnum().WithMessage("نوع المرفق غير صالح.")
            .Must(type => type is not (ShipmentAttachmentType.Signature or ShipmentAttachmentType.DeliveryPhoto))
            .WithMessage("التوقيع وصورة التسليم تُرفع مع تأكيد التسليم فقط.");
        RuleFor(x => x.Description).OptionalText(300);
        RuleFor(x => x.DamageCaseId).ValidOptionalId("حالة التلف غير صالحة.").When(x => x.DamageCaseId.HasValue);
    }
}

public sealed class DeliverShipmentRequestValidator : AbstractValidator<DeliverShipmentRequest>
{
    public DeliverShipmentRequestValidator()
    {
        RuleFor(x => x.RecipientName)
            .NotEmpty().WithMessage("اسم المستلم مطلوب.")
            .MaximumLength(150).WithMessage("اسم المستلم طويل جداً.");
        RuleFor(x => x.RecipientRelation).OptionalText(50);
        RuleFor(x => x.Notes).OptionalText(500);
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m).WithMessage("خط العرض يجب أن يكون بين -90 و 90.")
            .When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m).WithMessage("خط الطول يجب أن يكون بين -180 و 180.")
            .When(x => x.Longitude.HasValue);
        RuleFor(x => x.Longitude)
            .NotNull().WithMessage("أدخل خط الطول مع خط العرض، أو اتركهما فارغين.")
            .When(x => x.Latitude.HasValue);
        RuleFor(x => x.Latitude)
            .NotNull().WithMessage("أدخل خط العرض مع خط الطول، أو اتركهما فارغين.")
            .When(x => x.Longitude.HasValue);
    }
}

public sealed class RecordDeliveryFailureRequestValidator : AbstractValidator<RecordDeliveryFailureRequest>
{
    public RecordDeliveryFailureRequestValidator()
    {
        RuleFor(x => x.Reason).RequiredReason(300);
        RuleFor(x => x.Notes).OptionalText(500);
    }
}

public sealed class BulkLabelRequestValidator : AbstractValidator<BulkLabelRequest>
{
    public const int MaxShipments = 100;

    public BulkLabelRequestValidator()
    {
        RuleFor(x => x.ShipmentIds)
            .NotEmpty().WithMessage("اختر شحنة واحدة على الأقل.")
            .Must(ids => ids.Count <= MaxShipments).WithMessage($"الحد الأقصى {MaxShipments} شحنة في الملف الواحد.")
            .Must(ids => ids.All(id => id > 0)).WithMessage("أرقام الشحنات غير صالحة.");
        RuleFor(x => x.Format).IsInEnum().WithMessage("صيغة الملصق يجب أن تكون Pdf أو Html.");
    }
}

public sealed class ImportShipmentsRequestValidator : AbstractValidator<ImportShipmentsRequest>
{
    public ImportShipmentsRequestValidator()
    {
        RuleFor(x => x.OriginBranchId).ValidOptionalId("الفرع غير صالح.").When(x => x.OriginBranchId.HasValue);
    }
}

public sealed class SaveShippingSettingsRequestValidator : AbstractValidator<SaveShippingSettingsRequest>
{
    public SaveShippingSettingsRequestValidator()
    {
        RuleFor(x => x.VolumetricDivisor)
            .InclusiveBetween(1000, 10000).WithMessage("معامل الوزن الحجمي يجب أن يكون بين 1000 و 10000 (الشائع 5000).");
        RuleFor(x => x.WeightRoundingStepKg)
            .InclusiveBetween(0m, 5m).WithMessage("خطوة تقريب الوزن يجب أن تكون بين 0 و 5 كغ.")
            .PrecisionScale(6, 3, true).WithMessage("خطوة التقريب تقبل 3 منازل عشرية على الأكثر.");
        RuleFor(x => x.TrackingNumberPrefix)
            .NotEmpty().WithMessage("بادئة رقم التتبع مطلوبة.")
            .Matches("^[A-Z]{2,4}$").WithMessage("بادئة رقم التتبع من 2 إلى 4 أحرف إنجليزية كبيرة (مثل SE).");
        RuleFor(x => x.LabelWidthMm)
            .InclusiveBetween(50, 210).WithMessage("عرض الملصق يجب أن يكون بين 50 و 210 مم.");
        RuleFor(x => x.LabelHeightMm)
            .InclusiveBetween(50, 300).WithMessage("ارتفاع الملصق يجب أن يكون بين 50 و 300 مم.");
        RuleFor(x => x.PriceOverrideApprovalThresholdPercent)
            .InclusiveBetween(0m, 100m).WithMessage("حد اعتماد تعديل الأسعار يجب أن يكون بين 0 و 100 %.")
            .PrecisionScale(5, 2, true).WithMessage("النسبة تقبل منزلتين عشريتين على الأكثر.");
        RuleFor(x => x.DefaultPaymentTermsDays)
            .InclusiveBetween(0, 365).WithMessage("مدة السداد يجب أن تكون بين 0 و 365 يوماً.");
        RuleFor(x => x.TrackingUrlTemplate)
            .MaximumLength(300).WithMessage("رابط التتبع طويل جداً.")
            .Must(BeValidTemplate)
            .WithMessage("رابط التتبع يجب أن يبدأ بـ https:// أو http:// ويحتوي على {trackingNumber}.")
            .When(x => !string.IsNullOrWhiteSpace(x.TrackingUrlTemplate));
    }

    private static bool BeValidTemplate(string? template)
    {
        if (template is null || !template.Contains("{trackingNumber}", StringComparison.Ordinal))
        {
            return false;
        }

        var sample = template.Replace("{trackingNumber}", "SE0000000000", StringComparison.Ordinal);
        return Uri.TryCreate(sample, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }
}
```

### 6.11 `src/ShippingERP.Infrastructure/Billing/InvoiceService.cs`

```csharp
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Billing;
using ShippingERP.Domain.Entities.Billing;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Billing;

/// <summary>
/// Customer invoices and credit notes (step 5).
///
/// Double billing is blocked twice: the service refuses shipments that already have an active invoice line, and
/// the unique index UX_InvoiceLines_ActiveCharge refuses the second of two simultaneous requests (409).
/// Every change of an issued invoice writes, in ONE transaction: the document, its customer sub-ledger rows, the
/// audit row and the outbox event for accounting.
/// </summary>
public sealed class InvoiceService : IInvoiceService
{
    private const string EntityName = "Invoice";
    public const int MaxExportRows = 10_000;

    private static readonly Dictionary<string, Expression<Func<Invoice, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["createdAtUtc"] = x => x.CreatedAtUtc,
        ["invoiceDateUtc"] = x => x.InvoiceDateUtc,
        ["invoiceNumber"] = x => x.InvoiceNumber,
        ["dueDateUtc"] = x => x.DueDateUtc,
        ["totalAmount"] = x => x.TotalAmount,
        ["balanceDue"] = x => x.BalanceDue,
        ["status"] = x => x.Status
    };

    private static readonly Dictionary<string, Expression<Func<Shipment, object?>>> ShipmentSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["createdAtUtc"] = x => x.CreatedAtUtc,
        ["shipmentNumber"] = x => x.ShipmentNumber,
        ["totalCharge"] = x => x.TotalCharge
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly BillingAccess _access;
    private readonly ShipmentAccessService _shipmentAccess;
    private readonly ShippingSettingsService _settings;
    private readonly DocumentNumberGenerator _numbers;
    private readonly CustomerLedger _ledger;
    private readonly IIntegrationEventPublisher _events;
    private readonly InvoicePdfRenderer _pdf;

    public InvoiceService(
        MasterDataServiceDependencies dependencies,
        BillingAccess access,
        ShipmentAccessService shipmentAccess,
        ShippingSettingsService settings,
        DocumentNumberGenerator numbers,
        CustomerLedger ledger,
        IIntegrationEventPublisher events,
        InvoicePdfRenderer pdf)
    {
        _dependencies = dependencies;
        _access = access;
        _shipmentAccess = shipmentAccess;
        _settings = settings;
        _numbers = numbers;
        _ledger = ledger;
        _events = events;
        _pdf = pdf;
    }

    private AppDbContext Db => _dependencies.DbContext;

    private ICurrentUserService CurrentUser => _dependencies.CurrentUser;

    private DateTime UtcNow => _dependencies.DateTimeProvider.UtcNow;

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<InvoiceListItemDto>> GetPagedAsync(InvoiceListQuery query, CancellationToken cancellationToken)
    {
        var rows = ApplyFilters(_access.Invoices(), query);
        var totalCount = await rows.CountAsync(cancellationToken);

        var items = await Sort(rows, query)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ListItem)
            .ToListAsync(cancellationToken);

        return new PagedResult<InvoiceListItemDto>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<InvoiceDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var invoice = await _access.Invoices()
                          .Where(x => x.Id == id)
                          .Select(x => new
                          {
                              Invoice = x,
                              CustomerCode = x.Customer.Code,
                              CustomerName = x.Customer.ArabicName,
                              CustomerPhone = x.Customer.Phone,
                              CustomerTaxNumber = x.Customer.NationalOrCompanyNumber,
                              BranchName = x.Branch.ArabicName,
                              CurrencyCode = x.Currency.Code,
                              CurrencyDecimals = x.Currency.DecimalPlaces
                          })
                          .FirstOrDefaultAsync(cancellationToken)
                      ?? throw new NotFoundException("الفاتورة", id);

        var lines = await Db.InvoiceLines.AsNoTracking()
            .Where(x => x.InvoiceId == id)
            .OrderBy(x => x.LineNumber)
            .Select(x => new InvoiceLineDto
            {
                Id = x.Id,
                LineNumber = x.LineNumber,
                ShipmentId = x.ShipmentId,
                ShipmentNumber = x.Shipment.ShipmentNumber,
                TrackingNumber = x.Shipment.TrackingNumber,
                PricingSnapshotId = x.PricingSnapshotId,
                ChargeType = x.ChargeType,
                Description = x.Description,
                ShippingCharge = x.ShippingCharge,
                LineDiscountAmount = x.LineDiscountAmount,
                InvoiceDiscountAmount = x.InvoiceDiscountAmount,
                TaxableAmount = x.TaxableAmount,
                TaxPercentage = x.TaxPercentage,
                TaxAmount = x.TaxAmount,
                LineTotal = x.LineTotal,
                CreditedTaxableAmount = x.CreditedTaxableAmount,
                CreditedTaxAmount = x.CreditedTaxAmount,
                CreditableAmount = x.TaxableAmount - x.CreditedTaxableAmount
            })
            .ToListAsync(cancellationToken);

        var payments = await Db.CustomerPaymentAllocations.AsNoTracking()
            .Where(x => x.InvoiceId == id)
            .OrderBy(x => x.AllocatedAtUtc)
            .Select(x => new InvoicePaymentDto
            {
                PaymentId = x.PaymentId,
                PaymentNumber = x.Payment.PaymentNumber,
                PaymentMethodName = x.Payment.PaymentMethod.ArabicName,
                Amount = x.Amount,
                AllocatedAtUtc = x.AllocatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var creditNoteIds = await Db.CreditNotes.AsNoTracking().Where(x => x.InvoiceId == id).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(cancellationToken);
        var creditNotes = new List<CreditNoteDto>();
        foreach (var creditNoteId in creditNoteIds)
        {
            creditNotes.Add(await LoadCreditNoteAsync(Db.CreditNotes.AsNoTracking(), creditNoteId, cancellationToken));
        }

        var inv = invoice.Invoice;
        return new InvoiceDto
        {
            Id = inv.Id,
            InvoiceNumber = inv.InvoiceNumber,
            Status = inv.Status,
            StatusName = BillingNames.Arabic(inv.Status),
            NextStatuses = InvoiceStatusRules.NextStatuses(inv.Status),
            IsConsolidated = inv.IsConsolidated,
            CustomerId = inv.CustomerId,
            CustomerCode = invoice.CustomerCode,
            CustomerName = invoice.CustomerName,
            CustomerPhone = invoice.CustomerPhone,
            CustomerTaxNumber = invoice.CustomerTaxNumber,
            BranchId = inv.BranchId,
            BranchName = invoice.BranchName,
            CurrencyId = inv.CurrencyId,
            CurrencyCode = invoice.CurrencyCode,
            CurrencyDecimals = invoice.CurrencyDecimals,
            InvoiceDateUtc = inv.InvoiceDateUtc,
            PaymentTermsDays = inv.PaymentTermsDays,
            DueDateUtc = inv.DueDateUtc,
            SubTotal = inv.SubTotal,
            LineDiscountAmount = inv.LineDiscountAmount,
            InvoiceDiscountAmount = inv.InvoiceDiscountAmount,
            DiscountAmount = inv.DiscountAmount,
            TaxableAmount = inv.TaxableAmount,
            TaxAmount = inv.TaxAmount,
            TotalAmount = inv.TotalAmount,
            PaidAmount = inv.PaidAmount,
            CreditedAmount = inv.CreditedAmount,
            CreditAppliedAmount = inv.CreditAppliedAmount,
            BalanceDue = inv.BalanceDue,
            Notes = inv.Notes,
            CreatedAtUtc = inv.CreatedAtUtc,
            CreatedByUserId = inv.CreatedByUserId,
            IssuedAtUtc = inv.IssuedAtUtc,
            IssuedByUserId = inv.IssuedByUserId,
            PaidAtUtc = inv.PaidAtUtc,
            CancelledAtUtc = inv.CancelledAtUtc,
            CancellationReason = inv.CancellationReason,
            Lines = lines,
            Payments = payments,
            CreditNotes = creditNotes
        };
    }

    public async Task<PagedResult<EligibleShipmentDto>> GetEligibleShipmentsAsync(EligibleShipmentsQuery query, CancellationToken cancellationToken)
    {
        var shipments = await EligibleShipmentsAsync(query.CustomerId, query.CurrencyId, query.FromUtc, query.ToUtc, cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            shipments = shipments.Where(x =>
                x.ShipmentNumber.Contains(search) ||
                x.TrackingNumber.Contains(search) ||
                x.RecipientCustomer.ArabicName.Contains(search));
        }

        var totalCount = await shipments.CountAsync(cancellationToken);
        IOrderedQueryable<Shipment> ordered = !string.IsNullOrWhiteSpace(query.SortBy) && ShipmentSorts.TryGetValue(query.SortBy, out var sort)
            ? (query.IsDescending() ? shipments.OrderByDescending(sort) : shipments.OrderBy(sort))
            : shipments.OrderBy(x => x.CreatedAtUtc);

        var items = await ordered
            .ThenBy(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new EligibleShipmentDto
            {
                ShipmentId = x.Id,
                ShipmentNumber = x.ShipmentNumber,
                TrackingNumber = x.TrackingNumber,
                StatusName = ShipmentStatusNames.Arabic(x.Status),
                RecipientName = x.RecipientCustomer.ArabicName,
                DestinationCityName = x.DestinationCity.ArabicName,
                CurrencyCode = x.Currency.Code,
                ShippingCharge = x.ShippingCharge,
                DiscountAmount = x.DiscountAmount,
                TaxAmount = x.TaxAmount,
                TotalCharge = x.TotalCharge,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<EligibleShipmentDto>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<CreditNoteDto> GetCreditNoteAsync(long creditNoteId, CancellationToken cancellationToken)
    {
        return await LoadCreditNoteAsync(_access.CreditNotes(), creditNoteId, cancellationToken);
    }

    // ------------------------------------------------------------------ create

    public async Task<InvoiceDto> CreateFromShipmentAsync(long shipmentId, CreateInvoiceFromShipmentRequest request, CancellationToken cancellationToken)
    {
        var shipments = await _shipmentAccess.ScopeAsync(Db.Shipments.AsNoTracking(), cancellationToken);
        // Step 9: the invoice goes to whoever pays the charge (the recipient for "charges on the recipient").
        var payerId = await shipments.Where(x => x.Id == shipmentId)
                          .Select(x => (long?)(x.ChargesPaidBy == ChargesPayer.Recipient ? x.RecipientCustomerId : x.SenderCustomerId))
                          .FirstOrDefaultAsync(cancellationToken)
                      ?? throw new NotFoundException("الشحنة", shipmentId);

        return await CreateAsync(payerId, new[] { shipmentId }, request, isConsolidated: false, cancellationToken);
    }

    public async Task<InvoiceDto> CreateConsolidatedAsync(CreateConsolidatedInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (!await Db.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken))
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["customerId"] = new[] { "العميل غير موجود." } });
        }

        IReadOnlyList<long> shipmentIds = request.ShipmentIds;
        if (shipmentIds.Count == 0)
        {
            var currency = await _access.GetCurrencyAsync(request.CurrencyId, cancellationToken);
            var eligible = await EligibleShipmentsAsync(request.CustomerId, currency.Id, request.FromUtc, request.ToUtc, cancellationToken);
            shipmentIds = await eligible.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
                .Select(x => x.Id)
                .Take(Application.Validators.Billing.CreateConsolidatedInvoiceRequestValidator.MaxShipments + 1)
                .ToListAsync(cancellationToken);

            if (shipmentIds.Count == 0)
            {
                throw new BusinessRuleException("لا توجد شحنات قابلة للفوترة لهذا العميل في الفترة المحددة.");
            }

            if (shipmentIds.Count > Application.Validators.Billing.CreateConsolidatedInvoiceRequestValidator.MaxShipments)
            {
                throw new BusinessRuleException(
                    $"عدد الشحنات المؤهلة أكبر من {Application.Validators.Billing.CreateConsolidatedInvoiceRequestValidator.MaxShipments}. قسّم الفترة على أكثر من فاتورة.");
            }
        }

        return await CreateAsync(request.CustomerId, shipmentIds, request, isConsolidated: true, cancellationToken);
    }

    private async Task<InvoiceDto> CreateAsync(
        long customerId,
        IReadOnlyList<long> shipmentIds,
        CreateInvoiceOptions options,
        bool isConsolidated,
        CancellationToken cancellationToken)
    {
        if (options.IssueImmediately && CurrentUser.IsAuthenticated && !CurrentUser.HasPermission(Permissions.Invoices.Issue))
        {
            throw new ForbiddenException("الإصدار المباشر يحتاج صلاحية Invoices.Issue. أنشئ الفاتورة كمسودة.");
        }

        var visible = await _shipmentAccess.ScopeAsync(Db.Shipments.AsNoTracking(), cancellationToken);
        var shipments = await visible
            .Where(x => shipmentIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.ShipmentNumber,
                x.TrackingNumber,
                x.SenderCustomerId,
                PayerCustomerId = x.ChargesPaidBy == ChargesPayer.Recipient ? x.RecipientCustomerId : x.SenderCustomerId,
                x.ChargesPaidBy,
                x.Status,
                x.CurrencyId,
                x.OriginBranchId,
                x.TotalCharge,
                DestinationCityName = x.DestinationCity.ArabicName,
                IsInvoiced = x.InvoiceLines.Any(l => l.IsActive),
                HasPendingOverride = x.PriceOverrides.Any(o => o.Status == PriceOverrideStatus.Pending),
                ChargeDeductedFromCod = x.CodTransaction != null && x.CodTransaction.ShippingChargeDeducted,
                Snapshot = x.PricingSnapshots.OrderByDescending(p => p.Id).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var missing = shipmentIds.Except(shipments.Select(x => x.Id)).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException($"الشحنات غير موجودة: {string.Join(", ", missing)}.");
        }

        // Each problem is reported with its shipment number, so the user can fix the selection at once.
        var problems = new List<string>();
        foreach (var shipment in shipments)
        {
            if (shipment.PayerCustomerId != customerId)
            {
                problems.Add(shipment.ChargesPaidBy == ChargesPayer.Recipient
                    ? $"{shipment.ShipmentNumber}: أجورها على المستلم، وتُفوتر للمستلم"
                    : $"{shipment.ShipmentNumber}: تخص مرسلاً آخر");
            }
            else if (shipment.Status == ShipmentStatus.Cancelled)
            {
                problems.Add($"{shipment.ShipmentNumber}: ملغاة");
            }
            else if (shipment.HasPendingOverride)
            {
                problems.Add($"{shipment.ShipmentNumber}: لها تعديل سعر بانتظار الموافقة");
            }
            else if (shipment.ChargeDeductedFromCod)
            {
                problems.Add($"{shipment.ShipmentNumber}: أجورها تُخصم من مبلغ الدفع عند الاستلام في تسوية المرسل");
            }
            else if (shipment.Snapshot is null || shipment.TotalCharge <= 0m)
            {
                problems.Add($"{shipment.ShipmentNumber}: لا يوجد مبلغ للفوترة");
            }
        }

        if (problems.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن فوترة بعض الشحنات: " + string.Join("؛ ", problems) + ".");
        }

        var invoiced = shipments.Where(x => x.IsInvoiced).Select(x => x.ShipmentNumber).ToList();
        if (invoiced.Count > 0)
        {
            throw new ConflictException($"الشحنات التالية مفوترة من قبل ولا يمكن فوترتها مرة ثانية: {string.Join("، ", invoiced)}.");
        }

        var currencies = shipments.Select(x => x.CurrencyId).Distinct().ToList();
        if (currencies.Count > 1)
        {
            throw new BusinessRuleException("لا يمكن جمع شحنات بعملات مختلفة في فاتورة واحدة.");
        }

        var originBranches = shipments.Select(x => x.OriginBranchId).Distinct().ToList();
        var branchId = await _access.ResolveBranchAsync(options.BranchId, originBranches.Count == 1 ? originBranches[0] : null, cancellationToken);
        var settings = await _settings.GetEffectiveAsync(cancellationToken);

        var invoice = new Invoice
        {
            CustomerId = customerId,
            BranchId = branchId,
            CurrencyId = currencies[0],
            Status = InvoiceStatus.Draft,
            IsConsolidated = isConsolidated,
            PaymentTermsDays = options.PaymentTermsDays ?? settings.DefaultPaymentTermsDays,
            Notes = string.IsNullOrWhiteSpace(options.Notes) ? null : options.Notes.Trim()
        };

        var lineNumber = 1;
        foreach (var shipment in shipments.OrderBy(x => x.ShipmentNumber, StringComparer.Ordinal))
        {
            var snapshot = shipment.Snapshot!;
            invoice.Lines.Add(new InvoiceLine
            {
                LineNumber = lineNumber++,
                ShipmentId = shipment.Id,
                PricingSnapshotId = snapshot.Id,
                ChargeType = InvoiceChargeType.ShippingCharge,
                Description = $"أجور شحن {shipment.ShipmentNumber} ({shipment.TrackingNumber}) إلى {shipment.DestinationCityName}",
                ShippingCharge = snapshot.ShippingCharge,
                LineDiscountAmount = snapshot.DiscountAmount,
                InvoiceDiscountAmount = 0m,
                TaxableAmount = snapshot.TaxableAmount,
                TaxRateId = snapshot.TaxRateId,
                TaxPercentage = snapshot.TaxPercentage,
                TaxAmount = snapshot.TaxAmount,
                LineTotal = snapshot.TotalCharge,
                IsActive = true
            });
        }

        RecalculateTotals(invoice);

        long id;
        try
        {
            id = await Db.ExecuteInTransactionAsync(async ct =>
            {
                Db.Invoices.Add(invoice);
                await Db.SaveChangesAsync(ct);

                _dependencies.AuditLog.Add(
                    AuditAction.InvoiceCreated,
                    entityName: EntityName,
                    entityId: invoice.Id.ToString(CultureInfo.InvariantCulture),
                    details: new { invoice.CustomerId, invoice.BranchId, invoice.TotalAmount, shipmentIds });

                if (options.IssueImmediately)
                {
                    await IssueCoreAsync(invoice, null, ct);
                }

                await Db.SaveChangesAsync(ct);
                return invoice.Id;
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (BillingAccess.IsUniqueViolation(exception))
        {
            // Another user invoiced one of these shipments at the same moment.
            throw new ConflictException("إحدى الشحنات فُوترت للتو بواسطة مستخدم آخر. حدّث القائمة وحاول مجدداً.");
        }

        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ draft / issue / cancel

    public async Task<InvoiceDto> UpdateDraftAsync(long id, UpdateDraftInvoiceRequest request, CancellationToken cancellationToken)
    {
        var invoice = await LoadForUpdateAsync(id, includeLines: true, cancellationToken);
        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new BusinessRuleException("لا يمكن تعديل فاتورة صادرة. استخدم الإشعار الدائن، أو ألغِ الفاتورة إن لم تُدفع.");
        }

        var decimals = await CurrencyDecimalsAsync(invoice.CurrencyId, cancellationToken);
        var lines = invoice.Lines.Where(x => x.IsActive).OrderBy(x => x.LineNumber).ToList();
        var discountBase = lines.Sum(x => x.ShippingCharge - x.LineDiscountAmount);
        var discount = BillingAccess.Round(request.InvoiceDiscountAmount, decimals);

        if (discount > discountBase)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["invoiceDiscountAmount"] = new[] { $"خصم الفاتورة لا يمكن أن يزيد عن صافي السطور ({discountBase})." }
            });
        }

        AllocateInvoiceDiscount(lines, discount, decimals);

        invoice.PaymentTermsDays = request.PaymentTermsDays;
        invoice.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        RecalculateTotals(invoice);

        _dependencies.AuditLog.Add(
            AuditAction.InvoiceUpdated,
            entityName: EntityName,
            entityId: invoice.Id.ToString(CultureInfo.InvariantCulture),
            details: new { request.PaymentTermsDays, request.InvoiceDiscountAmount, invoice.TotalAmount });

        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<InvoiceDto> IssueAsync(long id, IssueInvoiceRequest request, CancellationToken cancellationToken)
    {
        var invoice = await LoadForUpdateAsync(id, includeLines: true, cancellationToken);
        if (!InvoiceStatusRules.CanIssue(invoice.Status))
        {
            throw new BusinessRuleException($"لا يمكن إصدار فاتورة في حالة «{BillingNames.Arabic(invoice.Status)}». يمكن إصدار المسودات فقط.");
        }

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            await IssueCoreAsync(invoice, request.InvoiceDateUtc, ct);
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>Number, dates, status, sub-ledger debit, audit and outbox event. The caller saves.</summary>
    private async Task IssueCoreAsync(Invoice invoice, DateTime? invoiceDateUtc, CancellationToken cancellationToken)
    {
        var now = UtcNow;
        var invoiceDate = invoiceDateUtc ?? now;
        if (invoiceDate > now.AddMinutes(5))
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["invoiceDateUtc"] = new[] { "تاريخ الفاتورة لا يمكن أن يكون في المستقبل." } });
        }

        if (invoice.TotalAmount <= 0m)
        {
            throw new BusinessRuleException("إجمالي الفاتورة صفر؛ لا يمكن إصدارها.");
        }

        var activeLines = invoice.Lines.Where(x => x.IsActive).ToList();
        var shipmentIds = activeLines.Select(x => x.ShipmentId).ToList();
        if (await Db.Shipments.AnyAsync(x => shipmentIds.Contains(x.Id) && x.Status == ShipmentStatus.Cancelled, cancellationToken))
        {
            throw new BusinessRuleException("إحدى شحنات الفاتورة ملغاة. ألغِ المسودة وأنشئ فاتورة جديدة.");
        }

        invoice.InvoiceNumber = await _numbers.NextAsync(DocumentPrefixes.Invoice, cancellationToken);
        invoice.InvoiceDateUtc = invoiceDate;
        invoice.DueDateUtc = invoiceDate.AddDays(invoice.PaymentTermsDays);
        invoice.Status = InvoiceStatus.Issued;
        invoice.IssuedAtUtc = now;
        invoice.IssuedByUserId = CurrentUser.UserId;
        InvoiceStatusRules.Refresh(invoice, now);

        _ledger.InvoiceIssued(invoice);

        var currencyCode = await Db.Currencies.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.Id == invoice.CurrencyId).Select(x => x.Code).FirstAsync(cancellationToken);

        _events.Publish(new InvoiceIssuedEvent
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CustomerId = invoice.CustomerId,
            BranchId = invoice.BranchId,
            CurrencyId = invoice.CurrencyId,
            CurrencyCode = currencyCode,
            InvoiceDateUtc = invoice.InvoiceDateUtc.Value,
            DueDateUtc = invoice.DueDateUtc.Value,
            SubTotal = invoice.SubTotal,
            DiscountAmount = invoice.DiscountAmount,
            TaxableAmount = invoice.TaxableAmount,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            TaxLines = TaxLines(activeLines.Select(x => (x.TaxRateId, x.TaxPercentage, x.TaxableAmount, x.TaxAmount))),
            ShipmentIds = shipmentIds
        });

        _dependencies.AuditLog.Add(
            AuditAction.InvoiceIssued,
            entityName: EntityName,
            entityId: invoice.Id.ToString(CultureInfo.InvariantCulture),
            details: new { invoice.InvoiceNumber, invoice.TotalAmount, invoice.DueDateUtc });
    }

    public async Task<InvoiceDto> CancelAsync(long id, CancelInvoiceRequest request, CancellationToken cancellationToken)
    {
        var invoice = await LoadForUpdateAsync(id, includeLines: true, cancellationToken);
        if (!InvoiceStatusRules.CanCancel(invoice))
        {
            throw new BusinessRuleException(invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Credited
                ? $"الفاتورة في حالة «{BillingNames.Arabic(invoice.Status)}» ولا يمكن إلغاؤها."
                : "لا يمكن إلغاء فاتورة عليها دفعات أو إشعارات دائنة. أصدر إشعاراً دائناً بالمبلغ المطلوب بدلاً من الإلغاء.");
        }

        var now = UtcNow;
        var reason = request.Reason.Trim();
        var wasIssued = invoice.Status != InvoiceStatus.Draft;

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            invoice.Status = InvoiceStatus.Cancelled;
            invoice.CancelledAtUtc = now;
            invoice.CancelledByUserId = CurrentUser.UserId;
            invoice.CancellationReason = reason;
            invoice.BalanceDue = 0m;

            // Free the shipments: they can be invoiced again.
            foreach (var line in invoice.Lines)
            {
                line.IsActive = false;
            }

            if (wasIssued)
            {
                _ledger.InvoiceCancelled(invoice, now);
                _events.Publish(new InvoiceCancelledEvent
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber!,
                    CustomerId = invoice.CustomerId,
                    BranchId = invoice.BranchId,
                    CurrencyId = invoice.CurrencyId,
                    TaxableAmount = invoice.TaxableAmount,
                    TaxAmount = invoice.TaxAmount,
                    TotalAmount = invoice.TotalAmount,
                    Reason = reason
                });
            }

            _dependencies.AuditLog.Add(
                AuditAction.InvoiceCancelled,
                entityName: EntityName,
                entityId: invoice.Id.ToString(CultureInfo.InvariantCulture),
                details: new { invoice.InvoiceNumber, wasIssued, reason });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ credit notes

    public async Task<CreditNoteDto> CreateCreditNoteAsync(long invoiceId, CreateCreditNoteRequest request, CancellationToken cancellationToken)
    {
        var invoice = await LoadForUpdateAsync(invoiceId, includeLines: true, cancellationToken);
        if (!InvoiceStatusRules.CanCredit(invoice.Status))
        {
            throw new BusinessRuleException($"لا يمكن إصدار إشعار دائن لفاتورة في حالة «{BillingNames.Arabic(invoice.Status)}». الإشعار الدائن للفواتير الصادرة فقط.");
        }

        var decimals = await CurrencyDecimalsAsync(invoice.CurrencyId, cancellationToken);
        var activeLines = invoice.Lines.Where(x => x.IsActive).ToDictionary(x => x.Id);
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var selections = new List<(InvoiceLine Line, decimal Taxable)>();

        if (request.CreditAll)
        {
            selections.AddRange(activeLines.Values
                .OrderBy(x => x.LineNumber)
                .Select(line => (line, line.TaxableAmount - line.CreditedTaxableAmount))
                .Where(x => x.Item2 > 0m));
        }
        else
        {
            for (var i = 0; i < request.Lines.Count; i++)
            {
                var item = request.Lines[i];
                if (!activeLines.TryGetValue(item.InvoiceLineId, out var line))
                {
                    errors[$"lines[{i}].invoiceLineId"] = new[] { "السطر غير موجود في هذه الفاتورة أو أُشعر بالكامل." };
                    continue;
                }

                var remaining = line.TaxableAmount - line.CreditedTaxableAmount;
                var amount = item.TaxableAmount.HasValue ? BillingAccess.Round(item.TaxableAmount.Value, decimals) : remaining;
                if (amount <= 0m || amount > remaining)
                {
                    errors[$"lines[{i}].taxableAmount"] = new[] { $"المبلغ المتاح للإشعار في هذا السطر هو {remaining}." };
                    continue;
                }

                selections.Add((line, amount));
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        if (selections.Count == 0)
        {
            throw new BusinessRuleException("لا يوجد مبلغ متبقٍ للإشعار في هذه الفاتورة.");
        }

        var now = UtcNow;
        var creditNote = new CreditNote
        {
            InvoiceId = invoice.Id,
            CustomerId = invoice.CustomerId,
            BranchId = invoice.BranchId,
            CurrencyId = invoice.CurrencyId,
            CreditNoteDateUtc = now,
            Reason = request.Reason.Trim()
        };

        foreach (var (line, taxable) in selections)
        {
            var remainingTax = line.TaxAmount - line.CreditedTaxAmount;
            var fullLine = taxable == line.TaxableAmount - line.CreditedTaxableAmount;

            // Crediting the rest of a line reverses exactly the rest of its tax (no rounding difference left).
            var tax = fullLine ? remainingTax : Math.Min(remainingTax, BillingAccess.Round(taxable * line.TaxPercentage / 100m, decimals));

            creditNote.Lines.Add(new CreditNoteLine
            {
                InvoiceLineId = line.Id,
                ShipmentId = line.ShipmentId,
                Description = line.Description,
                TaxableAmount = taxable,
                TaxPercentage = line.TaxPercentage,
                TaxAmount = tax,
                LineTotal = taxable + tax
            });

            line.CreditedTaxableAmount += taxable;
            line.CreditedTaxAmount += tax;

            // A fully credited line no longer bills the shipment: it can be repriced and invoiced again.
            if (line.CreditedTaxableAmount >= line.TaxableAmount)
            {
                line.IsActive = false;
            }
        }

        creditNote.TaxableAmount = creditNote.Lines.Sum(x => x.TaxableAmount);
        creditNote.TaxAmount = creditNote.Lines.Sum(x => x.TaxAmount);
        creditNote.TotalAmount = creditNote.Lines.Sum(x => x.LineTotal);
        creditNote.AppliedToInvoiceAmount = Math.Min(creditNote.TotalAmount, InvoiceStatusRules.Balance(invoice));
        creditNote.UnappliedAmount = creditNote.TotalAmount - creditNote.AppliedToInvoiceAmount;

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            creditNote.CreditNoteNumber = await _numbers.NextAsync(DocumentPrefixes.CreditNote, ct);

            invoice.CreditedAmount += creditNote.TotalAmount;
            invoice.CreditAppliedAmount += creditNote.AppliedToInvoiceAmount;
            InvoiceStatusRules.Refresh(invoice, now);

            Db.CreditNotes.Add(creditNote);
            await Db.SaveChangesAsync(ct);

            _ledger.CreditNoteIssued(creditNote, invoice.InvoiceNumber);
            _events.Publish(new CreditNoteIssuedEvent
            {
                CreditNoteId = creditNote.Id,
                CreditNoteNumber = creditNote.CreditNoteNumber,
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber!,
                CustomerId = creditNote.CustomerId,
                BranchId = creditNote.BranchId,
                CurrencyId = creditNote.CurrencyId,
                TaxableAmount = creditNote.TaxableAmount,
                TaxAmount = creditNote.TaxAmount,
                TotalAmount = creditNote.TotalAmount,
                AppliedToInvoiceAmount = creditNote.AppliedToInvoiceAmount,
                UnappliedAmount = creditNote.UnappliedAmount,
                TaxLines = TaxLines(creditNote.Lines.Select(x => ((long?)null, x.TaxPercentage, x.TaxableAmount, x.TaxAmount)))
            });
            _dependencies.AuditLog.Add(
                AuditAction.CreditNoteIssued,
                entityName: "CreditNote",
                entityId: creditNote.Id.ToString(CultureInfo.InvariantCulture),
                details: new { creditNote.CreditNoteNumber, invoice.InvoiceNumber, creditNote.TotalAmount, creditNote.Reason });

            await Db.SaveChangesAsync(ct);
            return creditNote.Id;
        }, cancellationToken);

        return await GetCreditNoteAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ files and jobs

    public async Task<FileDownload> GetPdfAsync(long id, CancellationToken cancellationToken)
    {
        var invoice = await GetByIdAsync(id, cancellationToken);
        var company = await Db.CompanyProfiles.AsNoTracking()
            .Select(x => new InvoiceCompanyInfo(x.ArabicName, x.EnglishName, x.TaxNumber, x.CommercialRegistrationNumber, x.Phone, x.Email, x.Address))
            .FirstOrDefaultAsync(cancellationToken)
            ?? new InvoiceCompanyInfo("شركة الشحن", null, null, null, null, null, null);

        var bytes = _pdf.Render(invoice, company);
        var name = invoice.InvoiceNumber ?? $"DRAFT-{invoice.Id}";
        return FileDownload.FromBytes(bytes, "application/pdf", $"{name}.pdf");
    }

    public async Task<FileDownload> ExportAsync(InvoiceListQuery query, CancellationToken cancellationToken)
    {
        var rows = ApplyFilters(_access.Invoices(), query);
        var items = await Sort(rows, query)
            .Take(MaxExportRows)
            .Select(ListItem)
            .ToListAsync(cancellationToken);

        var bytes = BillingExcelExporter.Invoices(items);
        var stamp = UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        return FileDownload.FromBytes(bytes, BillingExcelExporter.ContentType, $"invoices-{stamp}.xlsx");
    }

    public async Task<int> RefreshOverdueAsync(CancellationToken cancellationToken)
    {
        var now = UtcNow;
        var invoices = await Db.Invoices
            .Where(x => (x.Status == InvoiceStatus.Issued || x.Status == InvoiceStatus.PartiallyPaid) && x.DueDateUtc < now)
            .Take(1000)
            .ToListAsync(cancellationToken);

        var changed = invoices.Count(invoice => InvoiceStatusRules.Refresh(invoice, now));
        if (changed > 0)
        {
            await Db.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }

    // ------------------------------------------------------------------ helpers

    private static readonly Expression<Func<Invoice, InvoiceListItemDto>> ListItem = x => new InvoiceListItemDto
    {
        Id = x.Id,
        InvoiceNumber = x.InvoiceNumber,
        Status = x.Status,
        StatusName = BillingNames.Arabic(x.Status),
        CustomerId = x.CustomerId,
        CustomerCode = x.Customer.Code,
        CustomerName = x.Customer.ArabicName,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        CurrencyCode = x.Currency.Code,
        IsConsolidated = x.IsConsolidated,
        LineCount = x.Lines.Count(),
        InvoiceDateUtc = x.InvoiceDateUtc,
        DueDateUtc = x.DueDateUtc,
        TotalAmount = x.TotalAmount,
        PaidAmount = x.PaidAmount,
        CreditedAmount = x.CreditedAmount,
        BalanceDue = x.BalanceDue,
        CreatedAtUtc = x.CreatedAtUtc
    };

    private static IQueryable<Invoice> ApplyFilters(IQueryable<Invoice> query, InvoiceListQuery filter)
    {
        if (filter.Status.HasValue)
        {
            var status = filter.Status.Value;
            query = query.Where(x => x.Status == status);
        }

        if (filter.CustomerId.HasValue)
        {
            var customerId = filter.CustomerId.Value;
            query = query.Where(x => x.CustomerId == customerId);
        }

        if (filter.BranchId.HasValue)
        {
            var branchId = filter.BranchId.Value;
            query = query.Where(x => x.BranchId == branchId);
        }

        if (filter.CurrencyId.HasValue)
        {
            var currencyId = filter.CurrencyId.Value;
            query = query.Where(x => x.CurrencyId == currencyId);
        }

        if (filter.ShipmentId.HasValue)
        {
            var shipmentId = filter.ShipmentId.Value;
            query = query.Where(x => x.Lines.Any(l => l.ShipmentId == shipmentId));
        }

        if (filter.FromUtc.HasValue)
        {
            var from = filter.FromUtc.Value;
            query = query.Where(x => (x.InvoiceDateUtc ?? x.CreatedAtUtc) >= from);
        }

        if (filter.ToUtc.HasValue)
        {
            var to = filter.ToUtc.Value;
            query = query.Where(x => (x.InvoiceDateUtc ?? x.CreatedAtUtc) < to);
        }

        if (filter.HasBalance.HasValue)
        {
            query = filter.HasBalance.Value
                ? query.Where(x => x.BalanceDue > 0m && x.Status != InvoiceStatus.Draft && x.Status != InvoiceStatus.Cancelled)
                : query.Where(x => x.BalanceDue == 0m || x.Status == InvoiceStatus.Draft || x.Status == InvoiceStatus.Cancelled);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                (x.InvoiceNumber != null && x.InvoiceNumber.Contains(search)) ||
                x.Customer.Code.Contains(search) ||
                x.Customer.ArabicName.Contains(search) ||
                x.Customer.Phone.StartsWith(search));
        }

        return query;
    }

    private static IOrderedQueryable<Invoice> Sort(IQueryable<Invoice> query, InvoiceListQuery filter)
    {
        IOrderedQueryable<Invoice> ordered = !string.IsNullOrWhiteSpace(filter.SortBy) && Sorts.TryGetValue(filter.SortBy, out var sort)
            ? (filter.IsDescending() ? query.OrderByDescending(sort) : query.OrderBy(sort))
            : query.OrderByDescending(x => x.CreatedAtUtc);

        return ordered.ThenByDescending(x => x.Id);
    }

    /// <summary>
    /// Shipments the customer pays for (as sender, or as recipient when the charges are on the recipient, step 9) that
    /// are visible, priced, not cancelled, not invoiced and whose charge is not deducted from their COD (step 6).
    /// </summary>
    private async Task<IQueryable<Shipment>> EligibleShipmentsAsync(
        long customerId,
        long? currencyId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var query = (await _shipmentAccess.ScopeAsync(Db.Shipments.AsNoTracking(), cancellationToken))
            .Where(x => ((x.ChargesPaidBy == ChargesPayer.Sender && x.SenderCustomerId == customerId) ||
                         (x.ChargesPaidBy == ChargesPayer.Recipient && x.RecipientCustomerId == customerId)) &&
                        x.Status != ShipmentStatus.Cancelled &&
                        x.TotalCharge > 0m &&
                        !x.InvoiceLines.Any(l => l.IsActive) &&
                        !x.PriceOverrides.Any(o => o.Status == PriceOverrideStatus.Pending) &&
                        !(x.CodTransaction != null && x.CodTransaction.ShippingChargeDeducted));

        if (currencyId.HasValue)
        {
            var id = currencyId.Value;
            query = query.Where(x => x.CurrencyId == id);
        }

        if (fromUtc.HasValue)
        {
            var from = fromUtc.Value;
            query = query.Where(x => x.CreatedAtUtc >= from);
        }

        if (toUtc.HasValue)
        {
            var to = toUtc.Value;
            query = query.Where(x => x.CreatedAtUtc < to);
        }

        return query;
    }

    private async Task<Invoice> LoadForUpdateAsync(long id, bool includeLines, CancellationToken cancellationToken)
    {
        IQueryable<Invoice> query = _access.Invoices(tracked: true);
        if (includeLines)
        {
            query = query.Include(x => x.Lines);
        }

        return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
               ?? throw new NotFoundException("الفاتورة", id);
    }

    private async Task<int> CurrencyDecimalsAsync(long currencyId, CancellationToken cancellationToken) =>
        await Db.Currencies.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == currencyId).Select(x => x.DecimalPlaces).FirstOrDefaultAsync(cancellationToken);

    /// <summary>Totals = sums of the active lines.</summary>
    internal static void RecalculateTotals(Invoice invoice)
    {
        var lines = invoice.Lines.Where(x => x.IsActive).ToList();
        invoice.SubTotal = lines.Sum(x => x.ShippingCharge);
        invoice.LineDiscountAmount = lines.Sum(x => x.LineDiscountAmount);
        invoice.InvoiceDiscountAmount = lines.Sum(x => x.InvoiceDiscountAmount);
        invoice.DiscountAmount = invoice.LineDiscountAmount + invoice.InvoiceDiscountAmount;
        invoice.TaxableAmount = lines.Sum(x => x.TaxableAmount);
        invoice.TaxAmount = lines.Sum(x => x.TaxAmount);
        invoice.TotalAmount = lines.Sum(x => x.LineTotal);
        invoice.BalanceDue = invoice.Status == InvoiceStatus.Draft ? invoice.TotalAmount : InvoiceStatusRules.Balance(invoice);
    }

    /// <summary>
    /// Spreads the invoice discount over the lines in proportion to their net amounts (largest line takes the
    /// rounding difference), then recalculates each line's tax at its own rate.
    /// </summary>
    internal static void AllocateInvoiceDiscount(IReadOnlyList<InvoiceLine> lines, decimal discount, int decimals)
    {
        var total = lines.Sum(x => x.ShippingCharge - x.LineDiscountAmount);
        decimal allocated = 0m;

        foreach (var line in lines)
        {
            var net = line.ShippingCharge - line.LineDiscountAmount;
            line.InvoiceDiscountAmount = total == 0m ? 0m : BillingAccess.Round(discount * net / total, decimals);
            allocated += line.InvoiceDiscountAmount;
        }

        var difference = discount - allocated;
        if (difference != 0m && lines.Count > 0)
        {
            var largest = lines.OrderByDescending(x => x.ShippingCharge - x.LineDiscountAmount).ThenBy(x => x.LineNumber).First();
            largest.InvoiceDiscountAmount += difference;
        }

        foreach (var line in lines)
        {
            line.TaxableAmount = line.ShippingCharge - line.LineDiscountAmount - line.InvoiceDiscountAmount;
            line.TaxAmount = BillingAccess.Round(Math.Max(0m, line.TaxableAmount) * line.TaxPercentage / 100m, decimals);
            line.LineTotal = line.TaxableAmount + line.TaxAmount;
        }
    }

    private static IReadOnlyList<InvoiceTaxLine> TaxLines(IEnumerable<(long? TaxRateId, decimal TaxPercentage, decimal TaxableAmount, decimal TaxAmount)> lines) =>
        lines.GroupBy(x => new { x.TaxRateId, x.TaxPercentage })
            .Select(g => new InvoiceTaxLine(g.Key.TaxRateId, g.Key.TaxPercentage, g.Sum(x => x.TaxableAmount), g.Sum(x => x.TaxAmount)))
            .ToList();

    private static async Task<CreditNoteDto> LoadCreditNoteAsync(IQueryable<CreditNote> source, long id, CancellationToken cancellationToken)
    {
        return await source
                   .Where(x => x.Id == id)
                   .Select(x => new CreditNoteDto
                   {
                       Id = x.Id,
                       CreditNoteNumber = x.CreditNoteNumber,
                       InvoiceId = x.InvoiceId,
                       InvoiceNumber = x.Invoice.InvoiceNumber,
                       CustomerId = x.CustomerId,
                       CustomerName = x.Customer.ArabicName,
                       CurrencyCode = x.Currency.Code,
                       CreditNoteDateUtc = x.CreditNoteDateUtc,
                       Reason = x.Reason,
                       TaxableAmount = x.TaxableAmount,
                       TaxAmount = x.TaxAmount,
                       TotalAmount = x.TotalAmount,
                       AppliedToInvoiceAmount = x.AppliedToInvoiceAmount,
                       UnappliedAmount = x.UnappliedAmount,
                       CreatedByUserId = x.CreatedByUserId,
                       Lines = x.Lines
                           .OrderBy(l => l.Id)
                           .Select(l => new CreditNoteLineDto
                           {
                               Id = l.Id,
                               InvoiceLineId = l.InvoiceLineId,
                               ShipmentId = l.ShipmentId,
                               Description = l.Description,
                               TaxableAmount = l.TaxableAmount,
                               TaxPercentage = l.TaxPercentage,
                               TaxAmount = l.TaxAmount,
                               LineTotal = l.LineTotal
                           })
                           .ToList()
                   })
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("الإشعار الدائن", id);
    }
}
```

### 6.12 `src/ShippingERP.Infrastructure/Cod/CodService.cs`

```csharp
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Entities.Cod;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Cod;

/// <summary>
/// The COD workflow (step 6): collection, driver handover, review (ready for settlement) and disputes.
///
/// Money rule: the COD amount is the SENDER's money. Collecting it creates a debt of the driver (driver cash
/// account) and a liability to the sender (posting CodPayableToSenders); the handover moves the cash into a
/// cashbox; only the settlement turns the deductions into company revenue.
/// Every operation is one database transaction: COD change + history + sub-ledgers + voucher + audit + outbox event.
/// </summary>
public sealed class CodService : ICodService
{
    private const string EntityName = "CODTransaction";
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<string, Expression<Func<CODTransaction, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["createdAtUtc"] = x => x.CreatedAtUtc,
        ["collectedAtUtc"] = x => x.CollectedAtUtc,
        ["expectedAmount"] = x => x.ExpectedAmount,
        ["collectedAmount"] = x => x.CollectedAmount,
        ["netPayableAmount"] = x => x.NetPayableAmount,
        ["status"] = x => x.Status
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly CashAccess _access;
    private readonly CashLedger _ledger;
    private readonly ShipmentAccessService _shipments;
    private readonly DocumentNumberGenerator _numbers;
    private readonly IIntegrationEventPublisher _events;
    private readonly ICashVoucherService _vouchers;

    public CodService(
        MasterDataServiceDependencies dependencies,
        CashAccess access,
        CashLedger ledger,
        ShipmentAccessService shipments,
        DocumentNumberGenerator numbers,
        IIntegrationEventPublisher events,
        ICashVoucherService vouchers)
    {
        _dependencies = dependencies;
        _access = access;
        _ledger = ledger;
        _shipments = shipments;
        _numbers = numbers;
        _events = events;
        _vouchers = vouchers;
    }

    private AppDbContext Db => _dependencies.DbContext;

    private DateTime UtcNow => _dependencies.DateTimeProvider.UtcNow;

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<CodTransactionListItemDto>> GetPagedAsync(CodTransactionListQuery query, CancellationToken cancellationToken)
    {
        var rows = await _access.CodsAsync(tracked: false, cancellationToken);

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.SenderCustomerId.HasValue)
        {
            var senderId = query.SenderCustomerId.Value;
            rows = rows.Where(x => x.SenderCustomerId == senderId);
        }

        if (query.DriverId.HasValue)
        {
            var driverId = query.DriverId.Value;
            rows = rows.Where(x => x.DriverId == driverId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.OriginBranchId == branchId);
        }

        if (query.CurrencyId.HasValue)
        {
            var currencyId = query.CurrencyId.Value;
            rows = rows.Where(x => x.CurrencyId == currencyId);
        }

        if (query.CashboxId.HasValue)
        {
            var cashboxId = query.CashboxId.Value;
            rows = rows.Where(x => x.ReceivedCashboxId == cashboxId);
        }

        if (query.CollectedFromUtc.HasValue)
        {
            var from = query.CollectedFromUtc.Value;
            rows = rows.Where(x => x.CollectedAtUtc >= from);
        }

        if (query.CollectedToUtc.HasValue)
        {
            var to = query.CollectedToUtc.Value;
            rows = rows.Where(x => x.CollectedAtUtc < to);
        }

        if (query.HasRemainingPayable.HasValue)
        {
            rows = query.HasRemainingPayable.Value
                ? rows.Where(x => (x.Status == CODStatus.ReadyForSenderSettlement || x.Status == CODStatus.PartiallySettled) && x.NetPayableAmount != x.SettledAmount)
                : rows.Where(x => !((x.Status == CODStatus.ReadyForSenderSettlement || x.Status == CODStatus.PartiallySettled) && x.NetPayableAmount != x.SettledAmount));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                x.Shipment.ShipmentNumber.Contains(search) ||
                x.Shipment.TrackingNumber.Contains(search) ||
                x.SenderCustomer.Code.Contains(search) ||
                x.SenderCustomer.ArabicName.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<CODTransaction> ordered = !string.IsNullOrWhiteSpace(query.SortBy) && Sorts.TryGetValue(query.SortBy, out var sort)
            ? (query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort))
            : rows.OrderByDescending(x => x.CreatedAtUtc);

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(CodProjections.ListItem)
            .ToListAsync(cancellationToken);

        return new PagedResult<CodTransactionListItemDto>(items.Select(CodProjections.WithNames).ToList(), query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<CodTransactionDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var rows = await _access.CodsAsync(tracked: false, cancellationToken);
        var dto = await rows.Where(x => x.Id == id).Select(CodProjections.Detail).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("مبلغ التحصيل", id);
        return CodProjections.WithNames(dto);
    }

    public async Task<CodTransactionDto> GetByShipmentAsync(long shipmentId, CancellationToken cancellationToken)
    {
        await _shipments.EnsureVisibleAsync(shipmentId, cancellationToken);

        var id = await Db.CODTransactions.AsNoTracking()
                     .Where(x => x.ShipmentId == shipmentId)
                     .Select(x => (long?)x.Id)
                     .FirstOrDefaultAsync(cancellationToken)
                 ?? throw new NotFoundException("هذه الشحنة ليست شحنة دفع عند الاستلام.");

        var dto = await Db.CODTransactions.AsNoTracking().Where(x => x.Id == id).Select(CodProjections.Detail).FirstAsync(cancellationToken);
        return CodProjections.WithNames(dto);
    }

    // ------------------------------------------------------------------ collect

    public async Task<CodTransactionDto> CollectAsync(CollectCodRequest request, CancellationToken cancellationToken)
    {
        var visible = await _shipments.ScopeAsync(Db.Shipments.AsNoTracking(), cancellationToken);
        if (request.ShipmentId.HasValue)
        {
            var shipmentId = request.ShipmentId.Value;
            visible = visible.Where(x => x.Id == shipmentId);
        }
        else
        {
            var trackingNumber = request.TrackingNumber!.Trim();
            visible = visible.Where(x => x.TrackingNumber == trackingNumber);
        }

        var shipment = await visible
                           .Select(x => new
                           {
                               x.Id,
                               x.ShipmentNumber,
                               x.IsCOD,
                               x.CODAmount,
                               x.Status,
                               x.CurrentBranchId,
                               x.OriginBranchId,
                               x.SenderCustomerId,
                               x.RecipientCustomerId,
                               x.CurrencyId,
                               x.AssignedDriverId,
                               x.DeliveredAtUtc,
                               x.CreatedAtUtc,
                               RecipientName = x.RecipientCustomer.ArabicName
                           })
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw new NotFoundException("الشحنة غير موجودة.");

        if (!shipment.IsCOD || shipment.CODAmount <= 0m)
        {
            throw new BusinessRuleException($"الشحنة {shipment.ShipmentNumber} ليست شحنة دفع عند الاستلام.");
        }

        if (shipment.Status != ShipmentStatus.Delivered)
        {
            throw new BusinessRuleException(
                $"لا يمكن تسجيل التحصيل قبل تأكيد تسليم الشحنة {shipment.ShipmentNumber}. سجّل إثبات التسليم أولاً.");
        }

        var cod = await Db.CODTransactions.FirstOrDefaultAsync(x => x.ShipmentId == shipment.Id, cancellationToken);
        if (cod is null)
        {
            // A COD shipment from before step 6 that the startup backfill did not reach yet.
            cod = new CODTransaction
            {
                ShipmentId = shipment.Id,
                SenderCustomerId = shipment.SenderCustomerId,
                OriginBranchId = shipment.OriginBranchId,
                CurrencyId = shipment.CurrencyId,
                ExpectedAmount = shipment.CODAmount,
                Status = CODStatus.Pending
            };
            Db.CODTransactions.Add(cod);
            _ledger.AddCodHistory(cod, CODTransactionType.Registered, null, CODStatus.Pending, shipment.CODAmount,
                new CodReference("Shipment", shipment.Id, shipment.ShipmentNumber));
        }

        if (cod.Status == CODStatus.Cancelled)
        {
            throw new BusinessRuleException($"مبلغ التحصيل للشحنة {shipment.ShipmentNumber} ملغى.");
        }

        if (!CodStatusRules.IsNotCollected(cod.Status))
        {
            throw new ConflictException(
                $"تم تسجيل تحصيل الشحنة {shipment.ShipmentNumber} من قبل (الحالة: {CodNames.Arabic(cod.Status)}). " +
                "لتصحيح المبلغ اطلب تسوية «تصحيح مبلغ محصّل».");
        }

        var currency = await _access.Billing.GetCurrencyAsync(cod.CurrencyId, cancellationToken);
        var amount = BillingAccess.Round(request.Amount, currency.DecimalPlaces);
        var hasDifference = amount != cod.ExpectedAmount;
        if (hasDifference && string.IsNullOrWhiteSpace(request.DifferenceReason))
        {
            throw CashAccess.Invalid("differenceReason",
                $"المبلغ المحصّل ({amount}) يختلف عن مبلغ التحصيل في الشحنة ({cod.ExpectedAmount}). اكتب سبب الفرق.");
        }

        var now = UtcNow;
        var collectedAt = request.CollectedAtUtc ?? now;
        var earliest = (shipment.DeliveredAtUtc ?? shipment.CreatedAtUtc) - ClockTolerance;
        if (collectedAt > now + ClockTolerance || collectedAt < earliest)
        {
            throw CashAccess.Invalid("collectedAtUtc", "وقت التحصيل يجب أن يكون بين وقت التسليم والآن.");
        }

        if (request.CashboxId.HasValue)
        {
            if (_access.IsDriverOnly)
            {
                throw new ForbiddenException("السائق يسجّل التحصيل في عهدته فقط؛ الاستلام في الصندوق يتم في الفرع.");
            }

            _access.EnsurePermission(Permissions.Cod.Handover, "التحصيل في الفرع مباشرة إلى الصندوق يحتاج صلاحية COD.Handover.");
            var cashbox = await _access.CashboxForPostingAsync(request.CashboxId.Value, cod.CurrencyId, "cashboxId", cancellationToken);

            await CollectAtCounterAsync(cod, shipment.ShipmentNumber, shipment.RecipientCustomerId, shipment.RecipientName, cashbox, amount, collectedAt,
                hasDifference, request, cancellationToken);
        }
        else
        {
            var driverId = await ResolveCollectingDriverAsync(cod, shipment.Id, shipment.AssignedDriverId, shipment.CurrentBranchId, request.DriverId, cancellationToken);
            await CollectByDriverAsync(cod, shipment.ShipmentNumber, driverId, amount, collectedAt, hasDifference, request, cancellationToken);
        }

        return await GetByIdAsync(cod.Id, cancellationToken);
    }

    private async Task<long> ResolveCollectingDriverAsync(
        CODTransaction cod,
        long shipmentId,
        long? assignedDriverId,
        long currentBranchId,
        long? requestedDriverId,
        CancellationToken cancellationToken)
    {
        if (_access.IsDriverOnly)
        {
            var myDriverId = await _access.CurrentDriverIdAsync(cancellationToken)
                             ?? throw new ForbiddenException("حسابك غير مرتبط بملف سائق فعّال.");

            if (requestedDriverId.HasValue && requestedDriverId.Value != myDriverId)
            {
                throw new ForbiddenException("السائق يسجّل التحصيل باسمه فقط.");
            }

            return myDriverId;
        }

        _access.BranchAccess.EnsureCanAccess(currentBranchId);

        var proofDriverId = await Db.ProofsOfDelivery.AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId)
            .OrderByDescending(x => x.Id)
            .Select(x => x.DriverId)
            .FirstOrDefaultAsync(cancellationToken);

        var driverId = requestedDriverId ?? proofDriverId ?? cod.DriverId ?? assignedDriverId
                       ?? throw CashAccess.Invalid("driverId",
                           "لا يوجد سائق لهذه الشحنة (سُلّمت في الفرع). حدد السائق، أو حدد الصندوق لتسجيل التحصيل في الفرع.");

        var exists = await Db.DriverProfiles.AsNoTracking().AnyAsync(x => x.Id == driverId && x.IsActive, cancellationToken);
        if (!exists)
        {
            throw CashAccess.Invalid("driverId", "السائق غير موجود أو غير مفعّل.");
        }

        return driverId;
    }

    private async Task CollectByDriverAsync(
        CODTransaction cod,
        string shipmentNumber,
        long driverId,
        decimal amount,
        DateTime collectedAt,
        bool hasDifference,
        CollectCodRequest request,
        CancellationToken cancellationToken)
    {
        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var account = await _ledger.DriverAccountAsync(driverId, cod.CurrencyId, ct);

            cod.CollectedAmount = amount;
            cod.CollectedAtUtc = collectedAt;
            cod.DriverId = driverId;
            cod.CollectedByUserId = _dependencies.CurrentUser.UserId;
            cod.CollectionDifferenceReason = hasDifference ? request.DifferenceReason!.Trim() : null;

            var reference = new CodReference(CashLedger.CodDocument, cod.Id == 0 ? null : cod.Id, shipmentNumber, DriverId: driverId);
            _ledger.MoveCod(cod, CODStatus.CollectedByDriver, CODTransactionType.Collected, amount, reference, request.Notes);
            await Db.SaveChangesAsync(ct);

            _ledger.PostDriver(account, DriverCashMovementType.CodCollected, debit: amount, credit: 0m, cod.Id,
                CashLedger.CodDocument, cod.Id, shipmentNumber, $"تحصيل الشحنة {shipmentNumber}");

            if (hasDifference)
            {
                OpenDispute(cod, $"فرق في المبلغ المحصّل: {request.DifferenceReason!.Trim()}", reference);
            }

            _events.Publish(new CodCollectedEvent
            {
                CodTransactionId = cod.Id,
                ShipmentId = cod.ShipmentId,
                ShipmentNumber = shipmentNumber,
                SenderCustomerId = cod.SenderCustomerId,
                DriverId = driverId,
                CurrencyId = cod.CurrencyId,
                ExpectedAmount = cod.ExpectedAmount,
                CollectedAmount = amount,
                CollectedAtUtc = collectedAt,
                Posting = CashPostings.CodCollected(cod.Id, shipmentNumber, cod.CurrencyId, collectedAt, amount,
                    cod.SenderCustomerId, cod.OriginBranchId, driverId, null)
            });
            Audit(AuditAction.CodCollected, cod.Id, new { shipmentNumber, driverId, amount, cod.ExpectedAmount, hasDifference });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    private async Task CollectAtCounterAsync(
        CODTransaction cod,
        string shipmentNumber,
        long recipientCustomerId,
        string recipientName,
        Cashbox cashbox,
        decimal amount,
        DateTime collectedAt,
        bool hasDifference,
        CollectCodRequest request,
        CancellationToken cancellationToken)
    {
        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var voucher = new CashReceiptVoucher
            {
                VoucherNumber = await _numbers.NextAsync(DocumentPrefixes.CashReceipt, ct),
                CashboxId = cashbox.Id,
                BranchId = cashbox.BranchId,
                CurrencyId = cashbox.CurrencyId,
                ReceiptType = CashReceiptType.CounterCodCollection,
                Status = CashVoucherStatus.Posted,
                VoucherDateUtc = collectedAt,
                Amount = amount,
                ReceivedFrom = Clip(recipientName, 150),
                Description = $"تحصيل الدفع عند الاستلام للشحنة {shipmentNumber} في الفرع",
                CustomerId = recipientCustomerId,
                ExpectedAmount = cod.ExpectedAmount
            };
            Db.CashReceiptVouchers.Add(voucher);
            await Db.SaveChangesAsync(ct);

            await _ledger.PostCashboxAsync(cashbox, CashboxTransactionType.CounterCodCollection, amount, 0m,
                CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, voucher.Description, voucher.ReceivedFrom, ct);

            cod.CollectedAmount = amount;
            cod.CollectedAtUtc = collectedAt;
            cod.DriverId = null;
            cod.CollectedByUserId = _dependencies.CurrentUser.UserId;
            cod.CollectionDifferenceReason = hasDifference ? request.DifferenceReason!.Trim() : null;
            cod.ReceivedCashboxId = cashbox.Id;
            cod.ReceiptVoucherId = voucher.Id;
            cod.ReceivedAtUtc = UtcNow;

            var reference = new CodReference(CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, CashboxId: cashbox.Id);
            _ledger.MoveCod(cod, CODStatus.ReceivedAtBranchCashbox, CODTransactionType.CollectedAtCounter, amount, reference, request.Notes);

            if (hasDifference)
            {
                OpenDispute(cod, $"فرق في المبلغ المحصّل: {request.DifferenceReason!.Trim()}", reference);
            }

            await Db.SaveChangesAsync(ct);

            _events.Publish(new CodCollectedEvent
            {
                CodTransactionId = cod.Id,
                ShipmentId = cod.ShipmentId,
                ShipmentNumber = shipmentNumber,
                SenderCustomerId = cod.SenderCustomerId,
                CashboxId = cashbox.Id,
                ReceiptVoucherId = voucher.Id,
                CurrencyId = cod.CurrencyId,
                ExpectedAmount = cod.ExpectedAmount,
                CollectedAmount = amount,
                CollectedAtUtc = collectedAt,
                Posting = CashPostings.CodCollected(cod.Id, shipmentNumber, cod.CurrencyId, collectedAt, amount,
                    cod.SenderCustomerId, cashbox.BranchId, null, cashbox.Id)
            });
            Audit(AuditAction.CodCollected, cod.Id, new { shipmentNumber, cashbox = cashbox.Code, voucher.VoucherNumber, amount, cod.ExpectedAmount, hasDifference });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    // ------------------------------------------------------------------ handover

    public async Task<CashVoucherCreationResult<CashReceiptVoucherDto>> HandoverAsync(
        DriverHandoverRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var key = Idempotency.NormalizeKey(idempotencyKey);
        var hash = Idempotency.Hash(request);
        if (key is not null)
        {
            var replay = await FindReplayAsync(key, hash, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        var driver = await Db.DriverProfiles.AsNoTracking()
                         .Where(x => x.Id == request.DriverId)
                         .Select(x => new { x.Id, x.IsActive, Name = x.Employee.ArabicName })
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? throw CashAccess.Invalid("driverId", "السائق غير موجود.");

        var cashbox = await _access.CashboxForPostingAsync(request.CashboxId, null, "cashboxId", cancellationToken);
        var decimals = cashbox.Currency.DecimalPlaces;
        var amount = BillingAccess.Round(request.Amount, decimals);

        var account = await Db.DriverCashAccounts.FirstOrDefaultAsync(x => x.DriverId == driver.Id && x.CurrencyId == cashbox.CurrencyId, cancellationToken);
        var balance = account?.Balance ?? 0m;
        if (amount > balance)
        {
            throw new BusinessRuleException(
                $"المبلغ المسلّم ({amount}) أكبر من رصيد عهدة السائق {driver.Name} ({balance} {cashbox.Currency.Code}). " +
                "إذا كانت الزيادة صحيحة فسجّل تسوية «زيادة نقدية لدى السائق» واعتمدها أولاً.");
        }

        // The COD shipments handed over (default: everything the driver collected in this currency).
        List<CODTransaction> cods;
        if (request.CodTransactionIds.Count > 0)
        {
            cods = await Db.CODTransactions.Include(x => x.Shipment)
                .Where(x => request.CodTransactionIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            var missing = request.CodTransactionIds.Except(cods.Select(x => x.Id)).ToList();
            if (missing.Count > 0)
            {
                throw new NotFoundException($"شحنات التحصيل غير موجودة: {string.Join(", ", missing)}.");
            }

            var problems = cods
                .Where(x => x.Status != CODStatus.CollectedByDriver || x.DriverId != driver.Id || x.CurrencyId != cashbox.CurrencyId)
                .Select(x => x.Status != CODStatus.CollectedByDriver
                    ? $"{x.Shipment.ShipmentNumber}: الحالة «{CodNames.Arabic(x.Status)}»"
                    : x.DriverId != driver.Id ? $"{x.Shipment.ShipmentNumber}: محصّلة بواسطة سائق آخر" : $"{x.Shipment.ShipmentNumber}: بعملة أخرى")
                .ToList();

            if (problems.Count > 0)
            {
                throw new BusinessRuleException("لا يمكن تسليم بعض الشحنات: " + string.Join("؛ ", problems) + ".");
            }
        }
        else
        {
            cods = await Db.CODTransactions.Include(x => x.Shipment)
                .Where(x => x.DriverId == driver.Id && x.Status == CODStatus.CollectedByDriver && x.CurrencyId == cashbox.CurrencyId)
                .OrderBy(x => x.CollectedAtUtc)
                .ToListAsync(cancellationToken);
        }

        var expected = cods.Sum(x => x.CollectedAmount);
        var shortage = Math.Max(expected - amount, 0m);
        var excess = Math.Max(amount - expected, 0m);

        var voucher = new CashReceiptVoucher
        {
            CashboxId = cashbox.Id,
            BranchId = cashbox.BranchId,
            CurrencyId = cashbox.CurrencyId,
            ReceiptType = CashReceiptType.DriverHandover,
            Status = CashVoucherStatus.Posted,
            VoucherDateUtc = UtcNow,
            Amount = amount,
            ReceivedFrom = Clip(driver.Name, 150),
            Description = cods.Count == 0
                ? $"استلام نقدية من السائق {driver.Name} (تسديد رصيد سابق)"
                : $"استلام عهدة السائق {driver.Name}: {cods.Count} شحنة دفع عند الاستلام",
            ReferenceNumber = null,
            DriverId = driver.Id,
            ExpectedAmount = expected,
            ShortageAmount = shortage,
            ExcessAmount = excess,
            IdempotencyKey = key,
            RequestHash = hash
        };
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            voucher.Description = Clip($"{voucher.Description} - {request.Notes.Trim()}", 500);
        }

        long id;
        try
        {
            id = await Db.ExecuteInTransactionAsync(async ct =>
            {
                voucher.VoucherNumber = await _numbers.NextAsync(DocumentPrefixes.CashReceipt, ct);
                Db.CashReceiptVouchers.Add(voucher);
                await Db.SaveChangesAsync(ct);

                await _ledger.PostCashboxAsync(cashbox, CashboxTransactionType.DriverHandover, amount, 0m,
                    CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, voucher.Description, driver.Name, ct);

                var driverAccount = await _ledger.DriverAccountAsync(driver.Id, cashbox.CurrencyId, ct);
                _ledger.PostDriver(driverAccount, DriverCashMovementType.Handover, debit: 0m, credit: amount, null,
                    CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, $"تسليم نقدية للصندوق {cashbox.Code} بالسند {voucher.VoucherNumber}");

                var now = UtcNow;
                var reference = new CodReference(CashLedger.ReceiptDocument, voucher.Id, voucher.VoucherNumber, driver.Id, cashbox.Id);
                foreach (var cod in cods)
                {
                    cod.ReceivedCashboxId = cashbox.Id;
                    cod.ReceiptVoucherId = voucher.Id;
                    cod.ReceivedAtUtc = now;
                    _ledger.MoveCod(cod, CODStatus.ReceivedAtBranchCashbox, CODTransactionType.HandedOver, cod.CollectedAmount, reference);
                }

                _events.Publish(new DriverCashHandedOverEvent
                {
                    ReceiptVoucherId = voucher.Id,
                    VoucherNumber = voucher.VoucherNumber,
                    DriverId = driver.Id,
                    CashboxId = cashbox.Id,
                    BranchId = cashbox.BranchId,
                    CurrencyId = cashbox.CurrencyId,
                    Amount = amount,
                    ExpectedAmount = expected,
                    ShortageAmount = shortage,
                    ExcessAmount = excess,
                    CodTransactionIds = cods.Select(x => x.Id).ToList(),
                    Posting = CashPostings.DriverHandover(voucher.Id, voucher.VoucherNumber, cashbox.CurrencyId, now, amount, cashbox.BranchId, cashbox.Id, driver.Id)
                });
                _dependencies.AuditLog.Add(
                    AuditAction.DriverCashHandedOver,
                    entityName: CashLedger.ReceiptDocument,
                    entityId: voucher.Id.ToString(CultureInfo.InvariantCulture),
                    details: new
                    {
                        voucher.VoucherNumber,
                        driverId = driver.Id,
                        cashbox = cashbox.Code,
                        amount,
                        expected,
                        shortage,
                        excess,
                        shipments = cods.Select(x => x.Shipment.ShipmentNumber)
                    });

                await Db.SaveChangesAsync(ct);
                return voucher.Id;
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (key is not null && BillingAccess.IsUniqueViolation(exception))
        {
            Db.ChangeTracker.Clear();
            return await FindReplayAsync(key, hash, cancellationToken)
                   ?? throw new ConflictException("تعذر تسجيل الاستلام بسبب طلب متزامن. أعد المحاولة.");
        }

        return new CashVoucherCreationResult<CashReceiptVoucherDto> { Voucher = await _vouchers.GetReceiptAsync(id, cancellationToken), IsReplay = false };
    }

    private async Task<CashVoucherCreationResult<CashReceiptVoucherDto>?> FindReplayAsync(string key, string hash, CancellationToken cancellationToken)
    {
        var existing = await Db.CashReceiptVouchers.AsNoTracking()
            .Where(x => x.IdempotencyKey == key)
            .Select(x => new { x.Id, x.RequestHash, x.CreatedByUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return null;
        }

        Idempotency.EnsureSameRequest(existing.RequestHash ?? string.Empty, existing.CreatedByUserId, hash, _dependencies.CurrentUser.UserId);
        return new CashVoucherCreationResult<CashReceiptVoucherDto> { Voucher = await _vouchers.GetReceiptAsync(existing.Id, cancellationToken), IsReplay = true };
    }

    // ------------------------------------------------------------------ ready for settlement

    public async Task<IReadOnlyList<CodTransactionListItemDto>> MarkReadyAsync(MarkCodReadyRequest request, CancellationToken cancellationToken)
    {
        var scope = await _access.CodsAsync(tracked: true, cancellationToken);
        List<CODTransaction> cods;

        if (request.CodTransactionIds.Count > 0)
        {
            cods = await scope.Where(x => request.CodTransactionIds.Contains(x.Id)).ToListAsync(cancellationToken);
            var missing = request.CodTransactionIds.Except(cods.Select(x => x.Id)).ToList();
            if (missing.Count > 0)
            {
                throw new NotFoundException($"شحنات التحصيل غير موجودة: {string.Join(", ", missing)}.");
            }

            if (request.SenderCustomerId.HasValue && cods.Any(x => x.SenderCustomerId != request.SenderCustomerId.Value))
            {
                throw new BusinessRuleException("بعض الشحنات المحددة تخص مرسلاً آخر.");
            }
        }
        else
        {
            var senderId = request.SenderCustomerId!.Value;
            cods = await scope.Where(x => x.SenderCustomerId == senderId && x.Status == CODStatus.ReceivedAtBranchCashbox).ToListAsync(cancellationToken);
            if (cods.Count == 0)
            {
                throw new BusinessRuleException("لا توجد شحنات دفع عند الاستلام مستلمة في الصناديق لهذا المرسل.");
            }
        }

        var shipmentIds = cods.Select(x => x.ShipmentId).ToList();
        var shipments = await Db.Shipments.AsNoTracking()
            .Where(x => shipmentIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.ShipmentNumber,
                x.Status,
                IsInvoiced = x.InvoiceLines.Any(l => l.IsActive),
                x.ChargesPaidBy,
                HasPendingOverride = x.PriceOverrides.Any(o => o.Status == PriceOverrideStatus.Pending),
                Snapshot = x.PricingSnapshots
                    .OrderByDescending(p => p.Id)
                    .Select(p => new { p.TotalShippingCharge, p.CodFee, p.TaxAmount })
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var problems = new List<string>();
        foreach (var cod in cods)
        {
            var shipment = shipments[cod.ShipmentId];
            if (cod.Status != CODStatus.ReceivedAtBranchCashbox)
            {
                problems.Add($"{shipment.ShipmentNumber}: الحالة «{CodNames.Arabic(cod.Status)}» (المطلوب: مستلم في صندوق الفرع)");
            }
            else if (shipment.Status != ShipmentStatus.Delivered)
            {
                problems.Add($"{shipment.ShipmentNumber}: الشحنة لم تعد مسلّمة (مرتجع أو تصحيح حالة)؛ افتح نزاعاً لمعالجتها");
            }
            else if (shipment.HasPendingOverride)
            {
                problems.Add($"{shipment.ShipmentNumber}: لها تعديل سعر بانتظار الموافقة");
            }
        }

        if (problems.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن تجهيز بعض الشحنات للتسوية: " + string.Join("؛ ", problems) + ".");
        }

        var currencies = await Db.Currencies.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.DecimalPlaces, cancellationToken);

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            var now = UtcNow;
            foreach (var cod in cods)
            {
                var shipment = shipments[cod.ShipmentId];
                var decimals = currencies.TryGetValue(cod.CurrencyId, out var d) ? d : 2;

                // An invoiced shipment pays its charge through the invoice: nothing is deducted here.
                // Step 9: neither when the charges are on the recipient (the recipient is invoiced, not the sender).
                var recipientPays = shipment.ChargesPaidBy == ChargesPayer.Recipient;
                var deducted = !shipment.IsInvoiced && !recipientPays && shipment.Snapshot is not null;
                cod.ShippingChargeDeduction = deducted ? BillingAccess.Round(shipment.Snapshot!.TotalShippingCharge, decimals) : 0m;
                cod.CodCommissionDeduction = deducted ? BillingAccess.Round(shipment.Snapshot!.CodFee, decimals) : 0m;
                cod.TaxDeduction = deducted ? BillingAccess.Round(shipment.Snapshot!.TaxAmount, decimals) : 0m;
                cod.ShippingChargeDeducted = deducted && cod.ShippingChargeDeduction + cod.CodCommissionDeduction + cod.TaxDeduction > 0m;
                cod.NetPayableAmount = CodSettlementMath.NetPayable(cod.CollectedAmount, Deductions(cod));
                cod.ReadyAtUtc = now;

                _ledger.MoveCod(cod, CODStatus.ReadyForSenderSettlement, CODTransactionType.MarkedReady, cod.NetPayableAmount,
                    new CodReference(CashLedger.CodDocument, cod.Id, shipment.ShipmentNumber),
                    shipment.IsInvoiced ? "أجور الشحن مفوترة على فاتورة؛ لا تُخصم من مبلغ التحصيل."
                    : recipientPays ? "أجور الشحن على المستلم؛ لا تُخصم من مبلغ التحصيل." : null);
            }

            _dependencies.AuditLog.Add(
                AuditAction.CodMarkedReady,
                entityName: EntityName,
                entityId: cods.Count == 1 ? cods[0].Id.ToString(CultureInfo.InvariantCulture) : null,
                details: cods.Select(x => new { x.Id, x.CollectedAmount, x.NetPayableAmount, x.ShippingChargeDeducted }));

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        var ids = cods.Select(x => x.Id).ToList();
        var items = await Db.CODTransactions.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(CodProjections.ListItem)
            .ToListAsync(cancellationToken);
        return items.Select(CodProjections.WithNames).ToList();
    }

    // ------------------------------------------------------------------ disputes

    public async Task<CodTransactionDto> OpenDisputeAsync(long id, OpenCodDisputeRequest request, CancellationToken cancellationToken)
    {
        var cod = await FindForUpdateAsync(id, cancellationToken);

        if (!CodStatusRules.CanDispute(cod.Status))
        {
            throw new BusinessRuleException($"لا يمكن فتح نزاع على مبلغ في حالة «{CodNames.Arabic(cod.Status)}».");
        }

        if (await Db.SenderSettlementLines.AnyAsync(x => x.CODTransactionId == cod.Id && x.IsOpen, cancellationToken))
        {
            throw new BusinessRuleException("المبلغ محجوز في تسوية مفتوحة. ألغِ التسوية أولاً ثم افتح النزاع.");
        }

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            OpenDispute(cod, request.Reason.Trim(), new CodReference(CashLedger.CodDocument, cod.Id, null));
            Audit(AuditAction.CodDisputeOpened, cod.Id, new { reason = request.Reason.Trim(), cod.StatusBeforeDispute });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<CodTransactionDto> ResolveDisputeAsync(long id, ResolveCodDisputeRequest request, CancellationToken cancellationToken)
    {
        var cod = await FindForUpdateAsync(id, cancellationToken);

        if (cod.Status != CODStatus.Disputed)
        {
            throw new BusinessRuleException("المبلغ ليس في حالة نزاع.");
        }

        var openedBy = await Db.CODTransactionHistory.AsNoTracking()
            .Where(x => x.CODTransactionId == cod.Id && x.TransactionType == CODTransactionType.DisputeOpened)
            .OrderByDescending(x => x.Id)
            .Select(x => x.CreatedByUserId)
            .FirstOrDefaultAsync(cancellationToken);
        _access.EnsureOtherApprover(openedBy, "حل النزاع");

        var target = cod.StatusBeforeDispute ?? CODStatus.ReceivedAtBranchCashbox;

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            _ledger.MoveCod(cod, target, CODTransactionType.DisputeResolved, 0m, new CodReference(CashLedger.CodDocument, cod.Id, null), request.Notes.Trim());
            cod.StatusBeforeDispute = null;
            Audit(AuditAction.CodDisputeResolved, cod.Id, new { notes = request.Notes.Trim(), status = target });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    public static CodDeductions Deductions(CODTransaction cod) =>
        new(cod.ShippingChargeDeduction, cod.CodCommissionDeduction, cod.TaxDeduction, cod.OtherFeesAmount, cod.CompensationAmount);

    private void OpenDispute(CODTransaction cod, string reason, CodReference reference)
    {
        cod.StatusBeforeDispute = cod.Status;
        cod.DisputeReason = Clip(reason, 500);
        cod.DisputedAtUtc = UtcNow;
        _ledger.MoveCod(cod, CODStatus.Disputed, CODTransactionType.DisputeOpened, 0m, reference, reason);
    }

    private async Task<CODTransaction> FindForUpdateAsync(long id, CancellationToken cancellationToken)
    {
        var scope = await _access.CodsAsync(tracked: true, cancellationToken);
        return await scope.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
               ?? throw new NotFoundException("مبلغ التحصيل", id);
    }

    private void Audit(AuditAction action, long codId, object details) =>
        _dependencies.AuditLog.Add(action, entityName: EntityName, entityId: codId.ToString(CultureInfo.InvariantCulture), details: details);

    private static string Clip(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
```

### 6.13 `src/ShippingERP.Infrastructure/Identity/SeedOptions.cs`

```csharp
namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// "Seed" section of the configuration. Controls creation of roles, permissions and the FIRST administrator.
///
/// The administrator's credentials are NEVER stored in source code or appsettings.json. Set them with
/// environment variables before the first start:
///   Seed__AdminUserName, Seed__AdminEmail, Seed__AdminPassword
/// They are used only while no SystemAdmin user exists. After the first start you can (and should)
/// remove Seed__AdminPassword from the environment.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Set to false to skip seeding (e.g. a read-only replica).</summary>
    public bool Enabled { get; set; } = true;

    public string? AdminUserName { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public string AdminFullName { get; set; } = "System Administrator";

    public string AdminArabicFullName { get; set; } = "مدير النظام";

    /// <summary>
    /// Step 3: insert demo master data (company, 2 branches, cities, zones, 10 customers...) when the
    /// database has no branches yet. True in appsettings.Development.json, false in production.
    /// </summary>
    public bool DemoData { get; set; }

    /// <summary>
    /// Only used with <see cref="DemoData"/> = true: false seeds every demo record EXCEPT the 10 demo customers
    /// (so no demo shipments, invoices or pricing contract either, because those need the customers).
    /// Set Seed__DemoCustomers=false to start with the demo master data and your own customers.
    /// </summary>
    public bool DemoCustomers { get; set; } = true;
}
```

### 6.14 `src/ShippingERP.Infrastructure/MasterData/PricingServices.cs`

```csharp
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Customers;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.MasterData;

/// <summary>
/// Pricing plans (price lists) and customer contracts. Only one plan can be the default; a contract plan
/// (ContractCustomerId set) can never be the default, and two active contracts of the same customer may not
/// overlap in time.
/// </summary>
public sealed class PricingPlanService
    : CrudServiceBase<CustomerPricingPlan, PricingPlanDto, SavePricingPlanRequest, PricingPlanListQuery>, IPricingPlanService
{
    private static readonly Dictionary<string, Expression<Func<CustomerPricingPlan, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = x => x.Code,
        ["arabicName"] = x => x.ArabicName,
        ["createdAtUtc"] = x => x.CreatedAtUtc
    };

    public PricingPlanService(MasterDataServiceDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override string EntityName => "CustomerPricingPlan";

    protected override string DisplayName => "خطة التسعير";

    protected override IReadOnlyDictionary<string, Expression<Func<CustomerPricingPlan, object?>>> SortFields => Sorts;

    protected override Expression<Func<CustomerPricingPlan, object?>> DefaultSort => x => x.Code;

    protected override Expression<Func<CustomerPricingPlan, PricingPlanDto>> ToDto => x => new PricingPlanDto
    {
        Id = x.Id,
        Code = x.Code,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        IsDefault = x.IsDefault,
        ContractCustomerId = x.ContractCustomerId,
        ContractCustomerName = x.ContractCustomer != null ? x.ContractCustomer.ArabicName : null,
        ContractReference = x.ContractReference,
        ValidFromUtc = x.ValidFromUtc,
        ValidToUtc = x.ValidToUtc,
        Notes = x.Notes,
        RuleCount = x.Rules.Count(),
        CustomerCount = Db.Customers.Count(customer => customer.PricingPlanId == x.Id),
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        UpdatedAtUtc = x.UpdatedAtUtc,
        UpdatedByUserId = x.UpdatedByUserId
    };

    protected override IQueryable<CustomerPricingPlan> ApplyFilters(IQueryable<CustomerPricingPlan> query, PricingPlanListQuery filter)
    {
        if (filter.CurrencyId.HasValue)
        {
            var currencyId = filter.CurrencyId.Value;
            query = query.Where(x => x.CurrencyId == currencyId);
        }

        if (filter.IsContract.HasValue)
        {
            query = filter.IsContract.Value
                ? query.Where(x => x.ContractCustomerId != null)
                : query.Where(x => x.ContractCustomerId == null);
        }

        if (filter.ContractCustomerId.HasValue)
        {
            var customerId = filter.ContractCustomerId.Value;
            query = query.Where(x => x.ContractCustomerId == customerId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                x.Code.Contains(search) ||
                x.ArabicName.Contains(search) ||
                (x.EnglishName != null && x.EnglishName.Contains(search)) ||
                (x.ContractReference != null && x.ContractReference.Contains(search)));
        }

        return query;
    }

    protected override async Task ValidateAsync(SavePricingPlanRequest request, CustomerPricingPlan? existing, CancellationToken cancellationToken)
    {
        var code = NormalizeCode(request.Code);
        await EnsureUniqueAsync(x => x.Code == code, existing, $"رمز خطة التسعير '{code}' مستخدم من قبل.", cancellationToken);
        await EnsureReferenceAsync<Currency>(request.CurrencyId, existing?.CurrencyId, "currencyId", "العملة", cancellationToken);
        await EnsureReferenceAsync<Customer>(request.ContractCustomerId, existing?.ContractCustomerId, "contractCustomerId", "العميل", cancellationToken);

        if (existing is { IsDefault: true } && !request.IsDefault)
        {
            throw new BusinessRuleException("يجب أن تبقى خطة تسعير افتراضية. اجعل خطة أخرى افتراضية بدلاً من إلغاء هذه.");
        }

        if (existing is not null && existing.CurrencyId != request.CurrencyId && await Db.PriceRules.AnyAsync(x => x.PricingPlanId == existing.Id, cancellationToken))
        {
            throw Invalid("currencyId", "لا يمكن تغيير عملة خطة تحتوي على قواعد أسعار.");
        }

        if (request.ContractCustomerId.HasValue)
        {
            await EnsureNoOverlappingContractAsync(request, existing?.Id ?? 0, cancellationToken);
        }
    }

    /// <summary>Two active contracts of the same customer may not be valid at the same moment.</summary>
    private async Task EnsureNoOverlappingContractAsync(SavePricingPlanRequest request, long existingId, CancellationToken cancellationToken)
    {
        var customerId = request.ContractCustomerId!.Value;
        var others = await Db.CustomerPricingPlans
            .AsNoTracking()
            .Where(x => x.ContractCustomerId == customerId && x.IsActive && x.Id != existingId)
            .Select(x => new { x.Code, x.ValidFromUtc, x.ValidToUtc })
            .ToListAsync(cancellationToken);

        var newFrom = request.ValidFromUtc ?? DateTime.MinValue;
        var newTo = request.ValidToUtc ?? DateTime.MaxValue;

        var overlapping = others.FirstOrDefault(x => (x.ValidFromUtc ?? DateTime.MinValue) < newTo && newFrom < (x.ValidToUtc ?? DateTime.MaxValue));
        if (overlapping is not null)
        {
            throw new ConflictException($"للعميل عقد تسعير آخر فعّال في نفس الفترة ({overlapping.Code}). عدّل تواريخ العقدين أو أوقف أحدهما.");
        }
    }

    protected override void Apply(CustomerPricingPlan entity, SavePricingPlanRequest request)
    {
        entity.Code = NormalizeCode(request.Code);
        entity.ArabicName = request.ArabicName.Trim();
        entity.EnglishName = Optional(request.EnglishName);
        entity.CurrencyId = request.CurrencyId;
        entity.IsDefault = request.IsDefault && request.ContractCustomerId is null;
        entity.ContractCustomerId = request.ContractCustomerId;
        entity.ContractReference = request.ContractCustomerId.HasValue ? Optional(request.ContractReference) : null;
        entity.ValidFromUtc = request.ValidFromUtc;
        entity.ValidToUtc = request.ValidToUtc;
        entity.Notes = Optional(request.Notes);
    }

    protected override async Task BeforeSaveAsync(CustomerPricingPlan entity, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew && !entity.IsDefault && entity.ContractCustomerId is null && !await Db.CustomerPricingPlans.AnyAsync(x => x.IsDefault, cancellationToken))
        {
            // The first general plan is the default plan.
            entity.IsDefault = true;
        }

        if (!entity.IsDefault)
        {
            return;
        }

        entity.IsActive = true;

        var others = await Db.CustomerPricingPlans.Where(x => x.IsDefault && x.Id != entity.Id).ToListAsync(cancellationToken);
        foreach (var other in others)
        {
            other.IsDefault = false;
        }

        if (others.Count > 0)
        {
            // Clear the old default first so the "single default plan" unique index is never violated.
            entity.IsDefault = false;
            await Db.SaveChangesAsync(cancellationToken);
            entity.IsDefault = true;
        }
    }

    protected override Task EnsureCanDeactivateAsync(CustomerPricingPlan entity, CancellationToken cancellationToken)
    {
        if (entity.IsDefault)
        {
            throw new BusinessRuleException("لا يمكن إيقاف خطة التسعير الافتراضية.");
        }

        return Task.CompletedTask;
    }

    protected override async Task EnsureCanDeleteAsync(CustomerPricingPlan entity, CancellationToken cancellationToken)
    {
        if (entity.IsDefault)
        {
            throw new BusinessRuleException("لا يمكن حذف خطة التسعير الافتراضية.");
        }

        var id = entity.Id;
        if (await Db.Customers.AnyAsync(x => x.PricingPlanId == id, cancellationToken))
        {
            throw InUse("عملاء");
        }

        if (await Db.PriceRules.AnyAsync(x => x.PricingPlanId == id, cancellationToken))
        {
            throw InUse("قواعد تسعير (احذف القواعد أولاً)");
        }
    }
}

/// <summary>
/// Price rules of one plan, with their conditions and weight tiers (step 5).
///
/// Ambiguity guard: two active rules of the same plan with the same priority and the same conditions (apart from the
/// weight range) may not have overlapping weight ranges; otherwise the engine could not tell which one to use
/// (it would silently take the older one).
/// </summary>
public sealed class PricingRuleService : IPricingRuleService
{
    private const string EntityName = "PriceRule";

    private readonly MasterDataServiceDependencies _dependencies;

    public PricingRuleService(MasterDataServiceDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    private AppDbContext Db => _dependencies.DbContext;

    public async Task<IReadOnlyList<PricingRuleDto>> GetAllAsync(long planId, CancellationToken cancellationToken)
    {
        await EnsurePlanExistsAsync(planId, cancellationToken);

        var rules = await Db.PriceRules
            .AsNoTracking()
            .Include(x => x.Conditions)
            .Include(x => x.WeightTiers)
            .Include(x => x.TaxRate)
            .Where(x => x.PricingPlanId == planId)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(rules, cancellationToken);
    }

    public async Task<PricingRuleDto> GetByIdAsync(long planId, long ruleId, CancellationToken cancellationToken)
    {
        var rule = await Db.PriceRules
                       .AsNoTracking()
                       .Include(x => x.Conditions)
                       .Include(x => x.WeightTiers)
                       .Include(x => x.TaxRate)
                       .FirstOrDefaultAsync(x => x.PricingPlanId == planId && x.Id == ruleId, cancellationToken)
                   ?? throw new NotFoundException("قاعدة التسعير", ruleId);

        return (await ToDtosAsync(new[] { rule }, cancellationToken))[0];
    }

    public async Task<PricingRuleDto> CreateAsync(long planId, SavePricingRuleRequest request, CancellationToken cancellationToken)
    {
        await EnsurePlanExistsAsync(planId, cancellationToken);
        await ValidateAsync(planId, request, null, cancellationToken);

        var rule = new PriceRule { PricingPlanId = planId, IsActive = true };
        Apply(rule, request);

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            Db.PriceRules.Add(rule);
            await Db.SaveChangesAsync(ct);

            _dependencies.AuditLog.Add(
                AuditAction.MasterDataCreated,
                entityName: EntityName,
                entityId: rule.Id.ToString(CultureInfo.InvariantCulture),
                details: new { planId, request });
            await Db.SaveChangesAsync(ct);

            return rule.Id;
        }, cancellationToken);

        return await GetByIdAsync(planId, id, cancellationToken);
    }

    public async Task<PricingRuleDto> UpdateAsync(long planId, long ruleId, SavePricingRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await Db.PriceRules
                       .Include(x => x.Conditions)
                       .Include(x => x.WeightTiers)
                       .FirstOrDefaultAsync(x => x.PricingPlanId == planId && x.Id == ruleId, cancellationToken)
                   ?? throw new NotFoundException("قاعدة التسعير", ruleId);

        await ValidateAsync(planId, request, rule, cancellationToken);

        // Conditions and tiers are replaced as a whole (shipments keep what was applied in their snapshots).
        Db.PriceRuleConditions.RemoveRange(rule.Conditions);
        Db.PriceRuleWeightTiers.RemoveRange(rule.WeightTiers);
        rule.Conditions.Clear();
        rule.WeightTiers.Clear();
        Apply(rule, request);

        _dependencies.AuditLog.Add(
            AuditAction.MasterDataUpdated,
            entityName: EntityName,
            entityId: rule.Id.ToString(CultureInfo.InvariantCulture),
            details: new { planId, request });

        // One SaveChanges = one transaction (rule, children and audit row).
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(planId, ruleId, cancellationToken);
    }

    public async Task<PricingRuleDto> SetActiveAsync(long planId, long ruleId, bool isActive, CancellationToken cancellationToken)
    {
        var rule = await FindAsync(planId, ruleId, cancellationToken);

        if (rule.IsActive != isActive)
        {
            if (isActive)
            {
                // Reactivating must not create an ambiguity with the active rules.
                var full = await Db.PriceRules.AsNoTracking()
                    .Include(x => x.Conditions)
                    .Include(x => x.WeightTiers)
                    .FirstAsync(x => x.Id == ruleId, cancellationToken);
                await EnsureNotAmbiguousAsync(planId, ruleId, full.Priority, full.ValidFromUtc, full.ValidToUtc, ToConditionRequests(full), cancellationToken);
            }

            rule.IsActive = isActive;
            _dependencies.AuditLog.Add(
                isActive ? AuditAction.MasterDataActivated : AuditAction.MasterDataDeactivated,
                entityName: EntityName,
                entityId: rule.Id.ToString(CultureInfo.InvariantCulture));
            await Db.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdAsync(planId, ruleId, cancellationToken);
    }

    public async Task DeleteAsync(long planId, long ruleId, CancellationToken cancellationToken)
    {
        var rule = await FindAsync(planId, ruleId, cancellationToken);

        Db.PriceRules.Remove(rule); // soft delete: shipments priced with it keep their snapshots
        _dependencies.AuditLog.Add(
            AuditAction.MasterDataDeleted,
            entityName: EntityName,
            entityId: rule.Id.ToString(CultureInfo.InvariantCulture),
            details: new { planId });

        await Db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<PriceRule> FindAsync(long planId, long ruleId, CancellationToken cancellationToken)
    {
        return await Db.PriceRules.FirstOrDefaultAsync(x => x.PricingPlanId == planId && x.Id == ruleId, cancellationToken)
               ?? throw new NotFoundException("قاعدة التسعير", ruleId);
    }

    private async Task EnsurePlanExistsAsync(long planId, CancellationToken cancellationToken)
    {
        if (!await Db.CustomerPricingPlans.AnyAsync(x => x.Id == planId, cancellationToken))
        {
            throw new NotFoundException("خطة التسعير", planId);
        }
    }

    private async Task ValidateAsync(long planId, SavePricingRuleRequest request, PriceRule? existing, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var current = existing?.Conditions.Where(x => x.ReferenceId.HasValue).Select(x => (x.ConditionType, x.ReferenceId!.Value)).ToHashSet()
                      ?? new HashSet<(PriceConditionType, long)>();

        for (var i = 0; i < request.Conditions.Count; i++)
        {
            var condition = request.Conditions[i];
            if (!PriceConditionTypes.IsReference(condition.ConditionType) || condition.ReferenceId is null)
            {
                continue;
            }

            var id = condition.ReferenceId.Value;
            bool? isActive = condition.ConditionType switch
            {
                PriceConditionType.OriginZone or PriceConditionType.DestinationZone =>
                    await Db.Zones.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsActive).FirstOrDefaultAsync(cancellationToken),
                PriceConditionType.OriginCity or PriceConditionType.DestinationCity =>
                    await Db.Cities.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsActive).FirstOrDefaultAsync(cancellationToken),
                _ =>
                    await Db.ServiceTypes.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsActive).FirstOrDefaultAsync(cancellationToken)
            };

            var fieldName = $"conditions[{i}].referenceId";
            var name = PriceConditionTypes.Arabic(condition.ConditionType);
            if (isActive is null)
            {
                Add(errors, fieldName, $"{name}: السجل رقم {id} غير موجود.");
            }
            else if (isActive == false && !current.Contains((condition.ConditionType, id)))
            {
                Add(errors, fieldName, $"{name}: السجل رقم {id} غير مفعّل.");
            }
        }

        if (request.TaxRateId.HasValue)
        {
            var taxRateId = request.TaxRateId.Value;
            var tax = await Db.TaxRates.AsNoTracking().Where(x => x.Id == taxRateId).Select(x => new { x.IsActive }).FirstOrDefaultAsync(cancellationToken);
            if (tax is null)
            {
                Add(errors, "taxRateId", "نسبة الضريبة غير موجودة.");
            }
            else if (!tax.IsActive && taxRateId != existing?.TaxRateId)
            {
                Add(errors, "taxRateId", "نسبة الضريبة غير مفعّلة.");
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
        }

        if (existing is null || existing.IsActive)
        {
            await EnsureNotAmbiguousAsync(planId, existing?.Id ?? 0, request.Priority, request.ValidFromUtc, request.ValidToUtc, request.Conditions, cancellationToken);
        }
    }

    /// <summary>
    /// 409 when another active rule of the plan has the same priority, the same non-weight conditions, an
    /// overlapping validity and an overlapping weight range ([min, max), empty = [0, unlimited)).
    /// </summary>
    private async Task EnsureNotAmbiguousAsync(
        long planId,
        long ruleId,
        int priority,
        DateTime? validFrom,
        DateTime? validTo,
        IReadOnlyCollection<SavePriceRuleConditionRequest> conditions,
        CancellationToken cancellationToken)
    {
        var signature = Signature(conditions.Select(c => (c.ConditionType, c.ReferenceId, c.MinValue, c.MaxValue)));
        var (weightFrom, weightTo) = WeightRange(conditions.Select(c => (c.ConditionType, c.MinValue, c.MaxValue)));

        var candidates = await Db.PriceRules
            .AsNoTracking()
            .Include(x => x.Conditions)
            .Where(x => x.PricingPlanId == planId && x.Id != ruleId && x.IsActive && x.Priority == priority)
            .ToListAsync(cancellationToken);

        foreach (var other in candidates)
        {
            var otherConditions = other.Conditions.Select(c => (c.ConditionType, c.ReferenceId, c.MinValue, c.MaxValue)).ToList();
            if (Signature(otherConditions) != signature)
            {
                continue;
            }

            var validityOverlaps = (other.ValidFromUtc ?? DateTime.MinValue) < (validTo ?? DateTime.MaxValue) &&
                                   (validFrom ?? DateTime.MinValue) < (other.ValidToUtc ?? DateTime.MaxValue);
            if (!validityOverlaps)
            {
                continue;
            }

            var (otherFrom, otherTo) = WeightRange(otherConditions.Select(c => (c.ConditionType, c.MinValue, c.MaxValue)));
            if (otherFrom < weightTo && weightFrom < otherTo)
            {
                throw new ConflictException(
                    $"نطاق الوزن يتداخل مع قاعدة أخرى لها نفس الشروط والأولوية (رقم {other.Id}: «{other.ArabicName}»). " +
                    "غيّر الأولوية أو نطاق الوزن أو الشروط.");
            }
        }
    }

    /// <summary>Every condition except the weight range, in a stable order.</summary>
    private static string Signature(IEnumerable<(PriceConditionType Type, long? ReferenceId, decimal? Min, decimal? Max)> conditions)
    {
        return string.Join(
            "|",
            conditions
                .Where(c => c.Type != PriceConditionType.ChargeableWeight)
                .Select(c => string.Create(CultureInfo.InvariantCulture, $"{(int)c.Type}:{c.ReferenceId}:{c.Min}:{c.Max}"))
                .OrderBy(x => x, StringComparer.Ordinal));
    }

    private static (decimal From, decimal To) WeightRange(IEnumerable<(PriceConditionType Type, decimal? Min, decimal? Max)> conditions)
    {
        var weight = conditions.FirstOrDefault(c => c.Type == PriceConditionType.ChargeableWeight);
        return weight.Type == PriceConditionType.ChargeableWeight
            ? (weight.Min ?? 0m, weight.Max ?? decimal.MaxValue)
            : (0m, decimal.MaxValue);
    }

    private static List<SavePriceRuleConditionRequest> ToConditionRequests(PriceRule rule) =>
        rule.Conditions
            .Select(c => new SavePriceRuleConditionRequest { ConditionType = c.ConditionType, ReferenceId = c.ReferenceId, MinValue = c.MinValue, MaxValue = c.MaxValue })
            .ToList();

    private static void Add(Dictionary<string, List<string>> errors, string fieldName, string message)
    {
        if (!errors.TryGetValue(fieldName, out var list))
        {
            list = new List<string>();
            errors[fieldName] = list;
        }

        list.Add(message);
    }

    private static void Apply(PriceRule rule, SavePricingRuleRequest request)
    {
        rule.ArabicName = request.ArabicName.Trim();
        rule.EnglishName = string.IsNullOrWhiteSpace(request.EnglishName) ? null : request.EnglishName.Trim();
        rule.Priority = request.Priority;
        rule.ValidFromUtc = request.ValidFromUtc;
        rule.ValidToUtc = request.ValidToUtc;
        rule.BasePrice = request.BasePrice;
        rule.IncludedWeightKg = request.IncludedWeightKg;
        rule.PricePerCubicMeter = request.PricePerCubicMeter;
        rule.AppliesToBasis = request.AppliesToBasis;
        rule.IncludedPackageCount = request.IncludedPackageCount;
        rule.PerPackageFee = request.PerPackageFee;
        rule.CodFeeFixed = request.CodFeeFixed;
        rule.CodFeePercentage = request.CodFeePercentage;
        rule.CodFeeMinimum = request.CodFeeMinimum;
        rule.InsuranceFeePercentage = request.InsuranceFeePercentage;
        rule.InsuranceFeeMinimum = request.InsuranceFeeMinimum;
        rule.PickupFee = request.PickupFee;
        rule.PackagingFee = request.PackagingFee;
        rule.RemoteAreaFee = request.RemoteAreaFee;
        rule.ReturnFee = request.ReturnFee;
        rule.DiscountPercentage = request.DiscountPercentage;
        rule.TaxRateId = request.TaxRateId;
        rule.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        foreach (var condition in request.Conditions)
        {
            var isReference = PriceConditionTypes.IsReference(condition.ConditionType);
            rule.Conditions.Add(new PriceRuleCondition
            {
                ConditionType = condition.ConditionType,
                ReferenceId = isReference ? condition.ReferenceId : null,
                MinValue = isReference ? null : condition.MinValue,
                MaxValue = isReference ? null : condition.MaxValue
            });
        }

        foreach (var tier in request.WeightTiers.OrderBy(x => x.FromWeightKg))
        {
            rule.WeightTiers.Add(new PriceRuleWeightTier
            {
                FromWeightKg = tier.FromWeightKg,
                ToWeightKg = tier.ToWeightKg,
                PricePerKg = tier.PricePerKg
            });
        }
    }

    private async Task<IReadOnlyList<PricingRuleDto>> ToDtosAsync(IReadOnlyCollection<PriceRule> rules, CancellationToken cancellationToken)
    {
        var conditions = rules.SelectMany(x => x.Conditions).Where(x => x.ReferenceId.HasValue).ToList();
        var zoneIds = conditions.Where(x => x.ConditionType is PriceConditionType.OriginZone or PriceConditionType.DestinationZone).Select(x => x.ReferenceId!.Value).Distinct().ToList();
        var cityIds = conditions.Where(x => x.ConditionType is PriceConditionType.OriginCity or PriceConditionType.DestinationCity).Select(x => x.ReferenceId!.Value).Distinct().ToList();
        var serviceIds = conditions.Where(x => x.ConditionType == PriceConditionType.ServiceType).Select(x => x.ReferenceId!.Value).Distinct().ToList();

        var zones = zoneIds.Count == 0
            ? new Dictionary<long, string>()
            : await Db.Zones.AsNoTracking().IgnoreQueryFilters().Where(x => zoneIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);
        var cities = cityIds.Count == 0
            ? new Dictionary<long, string>()
            : await Db.Cities.AsNoTracking().IgnoreQueryFilters().Where(x => cityIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);
        var services = serviceIds.Count == 0
            ? new Dictionary<long, string>()
            : await Db.ServiceTypes.AsNoTracking().IgnoreQueryFilters().Where(x => serviceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);

        string? NameOf(PriceRuleCondition condition)
        {
            if (condition.ReferenceId is not { } id)
            {
                return null;
            }

            var source = condition.ConditionType switch
            {
                PriceConditionType.OriginZone or PriceConditionType.DestinationZone => zones,
                PriceConditionType.OriginCity or PriceConditionType.DestinationCity => cities,
                _ => services
            };

            return source.TryGetValue(id, out var name) ? name : null;
        }

        return rules.Select(x => new PricingRuleDto
        {
            Id = x.Id,
            PricingPlanId = x.PricingPlanId,
            ArabicName = x.ArabicName,
            EnglishName = x.EnglishName,
            Priority = x.Priority,
            ValidFromUtc = x.ValidFromUtc,
            ValidToUtc = x.ValidToUtc,
            BasePrice = x.BasePrice,
            IncludedWeightKg = x.IncludedWeightKg,
            PricePerCubicMeter = x.PricePerCubicMeter,
            AppliesToBasis = x.AppliesToBasis,
            IncludedPackageCount = x.IncludedPackageCount,
            PerPackageFee = x.PerPackageFee,
            CodFeeFixed = x.CodFeeFixed,
            CodFeePercentage = x.CodFeePercentage,
            CodFeeMinimum = x.CodFeeMinimum,
            InsuranceFeePercentage = x.InsuranceFeePercentage,
            InsuranceFeeMinimum = x.InsuranceFeeMinimum,
            PickupFee = x.PickupFee,
            PackagingFee = x.PackagingFee,
            RemoteAreaFee = x.RemoteAreaFee,
            ReturnFee = x.ReturnFee,
            DiscountPercentage = x.DiscountPercentage,
            TaxRateId = x.TaxRateId,
            TaxRateName = x.TaxRate?.ArabicName,
            Notes = x.Notes,
            IsActive = x.IsActive,
            Conditions = x.Conditions
                .OrderBy(c => c.ConditionType)
                .ThenBy(c => c.Id)
                .Select(c => new PriceRuleConditionDto
                {
                    Id = c.Id,
                    ConditionType = c.ConditionType,
                    ConditionTypeName = PriceConditionTypes.Arabic(c.ConditionType),
                    ReferenceId = c.ReferenceId,
                    ReferenceName = NameOf(c),
                    MinValue = c.MinValue,
                    MaxValue = c.MaxValue
                })
                .ToList(),
            WeightTiers = x.WeightTiers
                .OrderBy(t => t.FromWeightKg)
                .Select(t => new PriceRuleWeightTierDto { Id = t.Id, FromWeightKg = t.FromWeightKg, ToWeightKg = t.ToWeightKg, PricePerKg = t.PricePerKg })
                .ToList(),
            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc
        }).ToList();
    }
}
```

### 6.15 `src/ShippingERP.Infrastructure/Persistence/Configurations/Pricing/PricingConfigurations.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Entities.Pricing;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PriceRuleConfiguration : IEntityTypeConfiguration<PriceRule>
{
    public void Configure(EntityTypeBuilder<PriceRule> builder)
    {
        builder.ToTable("PriceRules");

        builder.Property(x => x.ArabicName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.EnglishName).HasMaxLength(150);
        builder.Property(x => x.IncludedWeightKg).HasPrecision(10, 3);
        builder.Property(x => x.AppliesToBasis).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.CodFeePercentage).HasPrecision(5, 2);
        builder.Property(x => x.InsuranceFeePercentage).HasPrecision(5, 2);
        builder.Property(x => x.DiscountPercentage).HasPrecision(5, 2);
        builder.Property(x => x.Notes).HasMaxLength(500);

        // The engine loads the active rules of one plan.
        builder.HasIndex(x => new { x.PricingPlanId, x.IsActive, x.Priority });

        builder.HasOne(x => x.TaxRate).WithMany().HasForeignKey(x => x.TaxRateId).OnDelete(DeleteBehavior.Restrict);

        // Conditions and tiers belong to the rule; they are replaced when the rule is saved.
        builder.HasMany(x => x.Conditions).WithOne(x => x.PriceRule).HasForeignKey(x => x.PriceRuleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.WeightTiers).WithOne(x => x.PriceRule).HasForeignKey(x => x.PriceRuleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PriceRuleConditionConfiguration : IEntityTypeConfiguration<PriceRuleCondition>
{
    public void Configure(EntityTypeBuilder<PriceRuleCondition> builder)
    {

        builder.Property(x => x.ConditionType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.MinValue).HasPrecision(10, 3);
        builder.Property(x => x.MaxValue).HasPrecision(10, 3);

        builder.HasIndex(x => x.PriceRuleId);

        // "Is this zone / city / service type used by a rule?" (delete guards of the master data).
        builder.HasIndex(x => new { x.ConditionType, x.ReferenceId });

        // ReferenceId has no foreign key on purpose: depending on ConditionType it points to Zones, Cities or ServiceTypes.
        builder.ToTable("PriceRuleConditions", table => table.HasCheckConstraint(
            "CK_PriceRuleConditions_Shape",
            "([ReferenceId] IS NOT NULL AND [MinValue] IS NULL AND [MaxValue] IS NULL) OR " +
            "([ReferenceId] IS NULL AND ([MinValue] IS NOT NULL OR [MaxValue] IS NOT NULL))"));
    }
}

public sealed class PriceRuleWeightTierConfiguration : IEntityTypeConfiguration<PriceRuleWeightTier>
{
    public void Configure(EntityTypeBuilder<PriceRuleWeightTier> builder)
    {

        builder.Property(x => x.FromWeightKg).HasPrecision(10, 3);
        builder.Property(x => x.ToWeightKg).HasPrecision(10, 3);

        builder.HasIndex(x => new { x.PriceRuleId, x.FromWeightKg }).IsUnique();

        builder.ToTable("PriceRuleWeightTiers", table => table.HasCheckConstraint(
            "CK_PriceRuleWeightTiers_Range",
            "[ToWeightKg] IS NULL OR [ToWeightKg] > [FromWeightKg]"));
    }
}

public sealed class ShipmentPriceOverrideConfiguration : IEntityTypeConfiguration<ShipmentPriceOverride>
{
    public void Configure(EntityTypeBuilder<ShipmentPriceOverride> builder)
    {
        builder.ToTable("ShipmentPriceOverrides");

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DecisionNotes).HasMaxLength(500);
        builder.Property(x => x.DeviationPercentage).HasPrecision(9, 2);

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => x.ShipmentId);

        // At most one pending request per shipment.
        builder.HasIndex(x => x.ShipmentId, "UX_ShipmentPriceOverrides_OnePending")
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");

        builder.HasOne(x => x.Shipment).WithMany(x => x.PriceOverrides).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

### 6.16 `src/ShippingERP.Infrastructure/Persistence/Configurations/Shipments/ShipmentConfigurations.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Infrastructure.Persistence.Configurations.Shipments;

/// <summary>
/// Shipments table. Indexes follow the list filters: status, branches (origin / current / destination),
/// driver, customers, cities, service type, COD flag and creation date.
/// Every foreign key is RESTRICT: nothing that a shipment uses can be deleted physically.
/// </summary>
public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");

        builder.Property(x => x.ShipmentNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.TrackingNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.ExternalReference).HasMaxLength(50);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.ActualWeight).HasPrecision(10, 3);
        builder.Property(x => x.VolumetricWeight).HasPrecision(10, 3);
        builder.Property(x => x.ChargeableWeight).HasPrecision(10, 3);
        builder.Property(x => x.TotalVolumeCbm).HasPrecision(10, 3);
        builder.Property(x => x.PricingBasis).HasConversion<string>().HasMaxLength(20).IsUnicode(false).HasDefaultValue(PricingBasis.Weight);
        builder.Property(x => x.ChargesPaidBy).HasConversion<string>().HasMaxLength(20).IsUnicode(false).HasDefaultValue(ChargesPayer.Sender);
        builder.Ignore(x => x.PayerCustomerId);
        builder.Property(x => x.PackageDescription).HasMaxLength(300).IsRequired();
        builder.Property(x => x.SenderNotes).HasMaxLength(500);
        builder.Property(x => x.InternalNotes).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);

        builder.HasIndex(x => x.ShipmentNumber).IsUnique();
        builder.HasIndex(x => x.TrackingNumber).IsUnique();
        builder.HasIndex(x => new { x.SenderCustomerId, x.ExternalReference })
            .IsUnique()
            .HasFilter("[ExternalReference] IS NOT NULL")
            .HasDatabaseName("UX_Shipments_Sender_ExternalReference");

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => new { x.OriginBranchId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.CurrentBranchId, x.Status });
        builder.HasIndex(x => new { x.DestinationBranchId, x.Status });
        builder.HasIndex(x => new { x.AssignedDriverId, x.Status });
        builder.HasIndex(x => x.SenderCustomerId);
        builder.HasIndex(x => x.RecipientCustomerId);
        builder.HasIndex(x => x.DestinationCityId);
        builder.HasIndex(x => x.ServiceTypeId);
        builder.HasIndex(x => x.IsCOD).HasFilter("[IsCOD] = 1");

        builder.HasOne(x => x.SenderCustomer).WithMany().HasForeignKey(x => x.SenderCustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecipientCustomer).WithMany().HasForeignKey(x => x.RecipientCustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SenderAddress).WithMany().HasForeignKey(x => x.SenderAddressId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecipientAddress).WithMany().HasForeignKey(x => x.RecipientAddressId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginBranch).WithMany().HasForeignKey(x => x.OriginBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CurrentBranch).WithMany().HasForeignKey(x => x.CurrentBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CurrentWarehouse).WithMany().HasForeignKey(x => x.CurrentWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DestinationBranch).WithMany().HasForeignKey(x => x.DestinationBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReturnBranch).WithMany().HasForeignKey(x => x.ReturnBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginCity).WithMany().HasForeignKey(x => x.OriginCityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DestinationCity).WithMany().HasForeignKey(x => x.DestinationCityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginZone).WithMany().HasForeignKey(x => x.OriginZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DestinationZone).WithMany().HasForeignKey(x => x.DestinationZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ServiceType).WithMany().HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedDriver).WithMany().HasForeignKey(x => x.AssignedDriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);

        // Packages are replaced while the shipment is editable, so they are the only cascading child.
        builder.HasMany(x => x.Packages).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.StatusHistory).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Attachments).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.PricingSnapshots).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShipmentPackageConfiguration : IEntityTypeConfiguration<ShipmentPackage>
{
    public void Configure(EntityTypeBuilder<ShipmentPackage> builder)
    {
        builder.ToTable("ShipmentPackages");

        builder.Property(x => x.PackageBarcode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.WeightKg).HasPrecision(10, 3);
        builder.Property(x => x.VolumetricWeightKg).HasPrecision(10, 3);
        builder.Property(x => x.LengthCm).HasPrecision(10, 2);
        builder.Property(x => x.WidthCm).HasPrecision(10, 2);
        builder.Property(x => x.HeightCm).HasPrecision(10, 2);

        builder.HasIndex(x => x.PackageBarcode).IsUnique();
        builder.HasIndex(x => new { x.ShipmentId, x.SequenceNumber }).IsUnique();
    }
}

public sealed class ShipmentStatusHistoryConfiguration : IEntityTypeConfiguration<ShipmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<ShipmentStatusHistory> builder)
    {
        builder.ToTable("ShipmentStatusHistory");

        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.ReferenceType).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(30).IsUnicode(false);

        builder.HasIndex(x => new { x.ShipmentId, x.OccurredAtUtc });

        // Foreign keys without navigation properties: integrity only.
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShipmentAttachmentConfiguration : IEntityTypeConfiguration<ShipmentAttachment>
{
    public void Configure(EntityTypeBuilder<ShipmentAttachment> builder)
    {
        builder.ToTable("ShipmentAttachments");

        builder.Property(x => x.AttachmentType).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(300).IsUnicode(false).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300);

        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => x.ShipmentId);

        builder.HasOne(x => x.DamageCase).WithMany().HasForeignKey(x => x.DamageCaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShipmentPricingSnapshotConfiguration : IEntityTypeConfiguration<ShipmentPricingSnapshot>
{
    public void Configure(EntityTypeBuilder<ShipmentPricingSnapshot> builder)
    {
        builder.ToTable("ShipmentPricingSnapshots");

        builder.Property(x => x.Reason).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.PricingSource).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.PricingPlanCode).HasMaxLength(20);
        builder.Property(x => x.PricingRuleName).HasMaxLength(150);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10).IsUnicode(false).IsRequired();
        builder.Property(x => x.ActualWeight).HasPrecision(10, 3);
        builder.Property(x => x.VolumetricWeight).HasPrecision(10, 3);
        builder.Property(x => x.ChargeableWeight).HasPrecision(10, 3);
        builder.Property(x => x.WeightRoundingStepKg).HasPrecision(6, 3);
        builder.Property(x => x.IncludedWeightKg).HasPrecision(10, 3);
        builder.Property(x => x.AdditionalWeightKg).HasPrecision(10, 3);
        builder.Property(x => x.PricingBasis).HasConversion<string>().HasMaxLength(20).IsUnicode(false).HasDefaultValue(PricingBasis.Weight);
        builder.Property(x => x.VolumeCbm).HasPrecision(10, 3);
        builder.Property(x => x.WeightTiersJson).HasMaxLength(4000);
        builder.Property(x => x.CodFeePercentage).HasPrecision(5, 2);
        builder.Property(x => x.ContractDiscountPercentage).HasPrecision(5, 2);
        builder.Property(x => x.TaxPercentage).HasPrecision(5, 2);
        builder.Property(x => x.OverrideReason).HasMaxLength(500);

        builder.HasIndex(x => x.ShipmentId);

        // No foreign key to the price rule: snapshots from step 4 point to the old CustomerPricingPlanRules table.
        builder.HasOne<CustomerPricingPlan>().WithMany().HasForeignKey(x => x.PricingPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxRate>().WithMany().HasForeignKey(x => x.TaxRateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProofOfDeliveryConfiguration : IEntityTypeConfiguration<ProofOfDelivery>
{
    public void Configure(EntityTypeBuilder<ProofOfDelivery> builder)
    {
        builder.ToTable("ProofsOfDelivery");

        builder.Property(x => x.RecipientName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.RecipientRelation).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);

        builder.HasIndex(x => x.ShipmentId);

        builder.HasOne(x => x.Shipment).WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SignatureAttachment).WithMany().HasForeignKey(x => x.SignatureAttachmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PhotoAttachment).WithMany().HasForeignKey(x => x.PhotoAttachmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DriverDeliveryManifest>().WithMany().HasForeignKey(x => x.DriverManifestId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

### 6.17 `src/ShippingERP.Infrastructure/Persistence/Seeding/DemoDataSeeder.cs`

```csharp
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Identity;

namespace ShippingERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// Inserts realistic DEMO master data so the system can be tried immediately:
/// 1 company, 2 countries, 5 cities, 13 zones, 2 branches, 3 warehouses, 3 service types, 5 payment methods,
/// 3 currencies + 2 exchange rates, 3 tax rates, 7 expense categories, 1 pricing plan (its price rules are added by
/// DemoPricingSeeder, step 5),
/// 10 customers (each with an address), 2 suppliers, 3 employees, 2 drivers and 2 vehicles.
///
/// Runs only when Seed:DemoData = true (Development) AND the database has no branch yet, so it never
/// touches real data. All names, phone numbers and rates are fictional examples.
/// </summary>
public sealed class DemoDataSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SeedOptions _options;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        AppDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IOptions<SeedOptions> options,
        ILogger<DemoDataSeeder> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <param name="force">Ignore Seed:DemoData (used by tests).</param>
    public async Task SeedAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && (!_options.Enabled || !_options.DemoData))
        {
            return;
        }

        if (_dbContext.Database.IsRelational() &&
            (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            _logger.LogWarning("Demo data skipped: there are pending migrations");
            return;
        }

        if (await _dbContext.Branches.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Demo data skipped: branches already exist");
            return;
        }

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            await SeedAllAsync(ct);
            return true;
        }, cancellationToken);

        _logger.LogInformation("Demo master data created (2 branches, {Customers} customers, 3 employees, 2 drivers, 2 vehicles)", _options.DemoCustomers ? 10 : 0);
    }

    private async Task SeedAllAsync(CancellationToken ct)
    {
        var now = _dateTimeProvider.UtcNow;

        // ---------------- Currencies, rates, taxes ----------------
        var syp = new Currency { Code = "SYP", ArabicName = "ليرة سورية", EnglishName = "Syrian Pound", Symbol = "ل.س", DecimalPlaces = 0, IsBaseCurrency = true, IsActive = true };
        var usd = new Currency { Code = "USD", ArabicName = "دولار أمريكي", EnglishName = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true };
        var eur = new Currency { Code = "EUR", ArabicName = "يورو", EnglishName = "Euro", Symbol = "€", DecimalPlaces = 2, IsActive = true };
        _dbContext.Currencies.AddRange(syp, usd, eur);
        await _dbContext.SaveChangesAsync(ct);

        var rateDate = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.ExchangeRates.AddRange(
            new ExchangeRate { FromCurrencyId = usd.Id, ToCurrencyId = syp.Id, Rate = 13000m, EffectiveFromUtc = rateDate, Notes = "سعر تجريبي للعرض فقط", IsActive = true },
            new ExchangeRate { FromCurrencyId = eur.Id, ToCurrencyId = syp.Id, Rate = 14100m, EffectiveFromUtc = rateDate, Notes = "سعر تجريبي للعرض فقط", IsActive = true });

        _dbContext.TaxRates.AddRange(
            new TaxRate { Code = "EXEMPT", ArabicName = "معفى من الضريبة", EnglishName = "Exempt", Percentage = 0m, IsDefault = true, IsActive = true },
            new TaxRate { Code = "VAT5", ArabicName = "ضريبة 5%", EnglishName = "VAT 5%", Percentage = 5m, IsActive = true },
            new TaxRate { Code = "VAT10", ArabicName = "ضريبة 10%", EnglishName = "VAT 10%", Percentage = 10m, IsActive = true });

        // ---------------- Company ----------------
        _dbContext.CompanyProfiles.Add(new CompanyProfile
        {
            ArabicName = "شركة البرق للشحن السريع (بيانات تجريبية)",
            EnglishName = "Al-Barq Express Shipping (demo)",
            CommercialRegistrationNumber = "CR-000000",
            TaxNumber = "TX-000000",
            Phone = "+963 11 000 0000",
            Email = "info@albarq-express.example",
            Website = "https://albarq-express.example",
            Address = "دمشق - المزة - أوتوستراد المزة",
            BaseCurrencyId = syp.Id
        });

        // ---------------- Geography ----------------
        var syria = new Country { Code = "SY", ArabicName = "سوريا", EnglishName = "Syria", PhoneCode = "+963", IsActive = true };
        var lebanon = new Country { Code = "LB", ArabicName = "لبنان", EnglishName = "Lebanon", PhoneCode = "+961", IsActive = true };
        _dbContext.Countries.AddRange(syria, lebanon);
        await _dbContext.SaveChangesAsync(ct);

        var damascus = NewCity(syria, "DAM", "دمشق", "Damascus");
        var aleppo = NewCity(syria, "ALP", "حلب", "Aleppo");
        var homs = NewCity(syria, "HMS", "حمص", "Homs");
        var latakia = NewCity(syria, "LTK", "اللاذقية", "Latakia");
        var beirut = NewCity(lebanon, "BEY", "بيروت", "Beirut");
        _dbContext.Cities.AddRange(damascus, aleppo, homs, latakia, beirut);
        await _dbContext.SaveChangesAsync(ct);

        var mazzeh = NewZone(damascus, "MAZZEH", "المزة", "Mazzeh");
        var malki = NewZone(damascus, "MALKI", "المالكي", "Malki");
        var babTouma = NewZone(damascus, "BABTOUMA", "باب توما", "Bab Touma");
        var midan = NewZone(damascus, "MIDAN", "الميدان", "Midan");
        var kafrSousa = NewZone(damascus, "KAFRSOUSA", "كفرسوسة", "Kafr Sousa");
        var aziziyeh = NewZone(aleppo, "AZIZIYEH", "العزيزية", "Aziziyeh");
        var jamiliyeh = NewZone(aleppo, "JAMILIYEH", "الجميلية", "Jamiliyeh");
        var hamdaniyeh = NewZone(aleppo, "HAMDANIYEH", "الحمدانية", "Hamdaniyeh");
        var inshaat = NewZone(homs, "INSHAAT", "الإنشاءات", "Inshaat");
        var waer = NewZone(homs, "WAER", "الوعر", "Waer");
        var ziraa = NewZone(latakia, "ZIRAA", "الزراعة", "Ziraa");
        var sheikhDaher = NewZone(latakia, "SHEIKHDAHER", "الشيخ ضاهر", "Sheikh Daher");
        var hamra = NewZone(beirut, "HAMRA", "الحمرا", "Hamra");
        _dbContext.Zones.AddRange(mazzeh, malki, babTouma, midan, kafrSousa, aziziyeh, jamiliyeh, hamdaniyeh, inshaat, waer, ziraa, sheikhDaher, hamra);

        // ---------------- Branches & warehouses ----------------
        var damascusBranch = new Branch
        {
            Code = "DAM", ArabicName = "فرع دمشق", EnglishName = "Damascus Branch", CityId = damascus.Id,
            Address = "دمشق - المزة - شارع الجلاء", Phone = "+963 11 000 0001", IsHeadOffice = true, IsActive = true
        };
        var aleppoBranch = new Branch
        {
            Code = "ALP", ArabicName = "فرع حلب", EnglishName = "Aleppo Branch", CityId = aleppo.Id,
            Address = "حلب - العزيزية - شارع القوتلي", Phone = "+963 21 000 0002", IsActive = true
        };
        _dbContext.Branches.AddRange(damascusBranch, aleppoBranch);
        await _dbContext.SaveChangesAsync(ct);

        _dbContext.Warehouses.AddRange(
            new Warehouse { Code = "DAM-WH1", ArabicName = "مستودع دمشق الرئيسي", EnglishName = "Damascus Main Warehouse", BranchId = damascusBranch.Id, Address = "دمشق - القدم - المنطقة الصناعية", CapacityCubicMeters = 1200m, IsActive = true },
            new Warehouse { Code = "DAM-WH2", ArabicName = "مستودع المزة", EnglishName = "Mazzeh Warehouse", BranchId = damascusBranch.Id, Address = "دمشق - المزة جبل", CapacityCubicMeters = 300m, IsActive = true },
            new Warehouse { Code = "ALP-WH1", ArabicName = "مستودع حلب", EnglishName = "Aleppo Warehouse", BranchId = aleppoBranch.Id, Address = "حلب - الشيخ نجار - المدينة الصناعية", CapacityCubicMeters = 800m, IsActive = true });

        // ---------------- Lookups ----------------
        var standard = new ServiceType { Code = "STD", ArabicName = "توصيل عادي", EnglishName = "Standard", EstimatedDeliveryDays = 3, SortOrder = 1, IsActive = true };
        var express = new ServiceType { Code = "EXP", ArabicName = "توصيل سريع", EnglishName = "Express", EstimatedDeliveryDays = 1, SortOrder = 2, IsActive = true };
        var sameDay = new ServiceType { Code = "SDD", ArabicName = "توصيل في نفس اليوم", EnglishName = "Same day", EstimatedDeliveryDays = 0, SortOrder = 3, IsActive = true };
        _dbContext.ServiceTypes.AddRange(standard, express, sameDay);

        _dbContext.PaymentMethods.AddRange(
            new PaymentMethod { Code = "CASH", ArabicName = "نقداً", EnglishName = "Cash", Kind = PaymentMethodKind.Cash, SortOrder = 1, IsActive = true },
            new PaymentMethod { Code = "COD", ArabicName = "الدفع عند الاستلام", EnglishName = "Cash on delivery", Kind = PaymentMethodKind.CashOnDelivery, SortOrder = 2, IsActive = true },
            new PaymentMethod { Code = "BANK", ArabicName = "تحويل بنكي", EnglishName = "Bank transfer", Kind = PaymentMethodKind.BankTransfer, RequiresReference = true, SortOrder = 3, IsActive = true },
            new PaymentMethod { Code = "CARD", ArabicName = "بطاقة", EnglishName = "Card", Kind = PaymentMethodKind.Card, RequiresReference = true, SortOrder = 4, IsActive = true },
            new PaymentMethod { Code = "ACCOUNT", ArabicName = "على حساب العميل", EnglishName = "Customer account", Kind = PaymentMethodKind.CustomerAccount, SortOrder = 5, IsActive = true });

        _dbContext.ExpenseCategories.AddRange(
            new ExpenseCategory { Code = "FUEL", ArabicName = "وقود", EnglishName = "Fuel", IsActive = true },
            new ExpenseCategory { Code = "MAINT", ArabicName = "صيانة مركبات", EnglishName = "Vehicle maintenance", IsActive = true },
            new ExpenseCategory { Code = "SALARY", ArabicName = "رواتب وأجور", EnglishName = "Salaries", IsActive = true },
            new ExpenseCategory { Code = "RENT", ArabicName = "إيجارات", EnglishName = "Rent", IsActive = true },
            new ExpenseCategory { Code = "UTIL", ArabicName = "كهرباء ومياه واتصالات", EnglishName = "Utilities", IsActive = true },
            new ExpenseCategory { Code = "OFFICE", ArabicName = "قرطاسية ومستلزمات مكتبية", EnglishName = "Office supplies", IsActive = true },
            new ExpenseCategory { Code = "TOLLS", ArabicName = "رسوم طرق ومواقف", EnglishName = "Tolls and parking", IsActive = true });

        _dbContext.Suppliers.AddRange(
            new Supplier { Code = "SUP-001", ArabicName = "محطة وقود الربوة (تجريبي)", Phone = "+963 11 000 0101", ContactPerson = "أبو سليم", IsActive = true },
            new Supplier { Code = "SUP-002", ArabicName = "مركز الأمانة لصيانة السيارات (تجريبي)", Phone = "+963 11 000 0102", ContactPerson = "م. فادي", IsActive = true });
        await _dbContext.SaveChangesAsync(ct);

        // ---------------- Pricing ----------------
        var generalPlan = new CustomerPricingPlan
        {
            Code = "GENERAL", ArabicName = "الأسعار العامة", EnglishName = "General prices",
            CurrencyId = syp.Id, IsDefault = true, ValidFromUtc = rateDate, IsActive = true
        };
        _dbContext.CustomerPricingPlans.Add(generalPlan);
        await _dbContext.SaveChangesAsync(ct);

        // Seed:DemoCustomers = false skips the demo customers; the shipment and contract demo seeders then skip too.
        if (_options.DemoCustomers)
        {
            // ---------------- Customers (each with one default address) ----------------
            var customers = new (string Name, CustomerType Type, CustomerRole Role, Branch Branch, string Phone, decimal Credit, City City, Zone Zone, string Address)[]
            {
                ("أحمد محمود الخطيب", CustomerType.Individual, CustomerRole.Both, damascusBranch, "+963944000101", 0m, damascus, mazzeh, "شارع الشيخ سعد، بناء 12"),
                ("سارة يوسف العلي", CustomerType.Individual, CustomerRole.Recipient, damascusBranch, "+963944000102", 0m, damascus, malki, "جادة الرئيس، بناء 4"),
                ("شركة الياسمين للتجارة", CustomerType.Business, CustomerRole.Sender, damascusBranch, "+963113000103", 500000m, damascus, babTouma, "شارع باب توما الرئيسي، محل 7"),
                ("محمد خالد الحلبي", CustomerType.Individual, CustomerRole.Both, aleppoBranch, "+963933000104", 0m, aleppo, aziziyeh, "شارع فيصل، بناء 20"),
                ("مؤسسة الشهباء للألبسة", CustomerType.Business, CustomerRole.Sender, aleppoBranch, "+963212000105", 750000m, aleppo, jamiliyeh, "سوق الجميلية، محل 15"),
                ("ريم عبد الله حداد", CustomerType.Individual, CustomerRole.Recipient, aleppoBranch, "+963933000106", 0m, aleppo, hamdaniyeh, "الحمدانية، الحي الثالث، بناء 9"),
                ("متجر النور الإلكتروني", CustomerType.Business, CustomerRole.Both, damascusBranch, "+963944000107", 1000000m, damascus, kafrSousa, "كفرسوسة، مجمع الأمويين، مكتب 3"),
                ("خالد إبراهيم السوري", CustomerType.Individual, CustomerRole.Recipient, damascusBranch, "+963955000108", 0m, homs, inshaat, "الإنشاءات، شارع البرازيل، بناء 5"),
                ("ليلى حسن مراد", CustomerType.Individual, CustomerRole.Recipient, aleppoBranch, "+963955000109", 0m, latakia, ziraa, "حي الزراعة، قرب الجامعة، بناء 11"),
                ("شركة المتوسط للصيدليات", CustomerType.Business, CustomerRole.Both, damascusBranch, "+963113000110", 2000000m, damascus, midan, "الميدان، شارع الحقلة، بناء 2")
            };

            for (var i = 0; i < customers.Length; i++)
            {
                var data = customers[i];
                var customer = new Customer
                {
                    Code = "CUS-" + (i + 1).ToString("D5", CultureInfo.InvariantCulture),
                    CustomerType = data.Type,
                    Roles = data.Role,
                    ArabicName = data.Name,
                    Phone = data.Phone,
                    CreditLimit = data.Credit,
                    DefaultBranchId = data.Branch.Id,
                    Notes = "عميل تجريبي",
                    IsActive = true
                };

                customer.Addresses.Add(new CustomerAddress
                {
                    Label = data.Type == CustomerType.Business ? "مقر الشركة" : "المنزل",
                    CityId = data.City.Id,
                    ZoneId = data.Zone.Id,
                    AddressLine = data.Address,
                    ContactName = data.Name,
                    Phone = data.Phone,
                    IsDefault = true,
                    IsActive = true
                });

                _dbContext.Customers.Add(customer);
            }
        }

        // ---------------- Employees, vehicles, drivers ----------------
        var manager = new Employee { Code = "EMP-001", ArabicName = "سامر نعيم", EnglishName = "Samer Naeem", BranchId = damascusBranch.Id, JobTitle = "مدير فرع", Phone = "+963944100001", HireDateUtc = rateDate.AddYears(-5), IsActive = true };
        var driverDamascus = new Employee { Code = "EMP-002", ArabicName = "علي حسين", EnglishName = "Ali Hussein", BranchId = damascusBranch.Id, JobTitle = "سائق", Phone = "+963944100002", HireDateUtc = rateDate.AddYears(-2), IsActive = true };
        var driverAleppo = new Employee { Code = "EMP-003", ArabicName = "عمر يوسف", EnglishName = "Omar Youssef", BranchId = aleppoBranch.Id, JobTitle = "سائق", Phone = "+963933100003", HireDateUtc = rateDate.AddYears(-1), IsActive = true };
        _dbContext.Employees.AddRange(manager, driverDamascus, driverAleppo);

        var van = new Vehicle { PlateNumber = "DAM 123-456", Model = "Hyundai H100", VehicleType = VehicleType.Van, CapacityKg = 1000m, BranchId = damascusBranch.Id, IsActive = true };
        var motorcycle = new Vehicle { PlateNumber = "ALP 654-321", Model = "Honda CG 150", VehicleType = VehicleType.Motorcycle, CapacityKg = 150m, BranchId = aleppoBranch.Id, IsActive = true };
        _dbContext.Vehicles.AddRange(van, motorcycle);
        await _dbContext.SaveChangesAsync(ct);

        _dbContext.DriverProfiles.AddRange(
            new DriverProfile { EmployeeId = driverDamascus.Id, LicenseNumber = "DL-100200", LicenseCategory = "C", LicenseExpiryDateUtc = rateDate.AddYears(2), DefaultVehicleId = van.Id, IsActive = true },
            new DriverProfile { EmployeeId = driverAleppo.Id, LicenseNumber = "DL-100300", LicenseCategory = "A", LicenseExpiryDateUtc = rateDate.AddDays(20), DefaultVehicleId = motorcycle.Id, Notes = "الرخصة تنتهي قريباً (لتجربة التنبيه)", IsActive = true });
        await _dbContext.SaveChangesAsync(ct);
    }

    private static City NewCity(Country country, string code, string arabicName, string englishName) =>
        new() { CountryId = country.Id, Code = code, ArabicName = arabicName, EnglishName = englishName, IsActive = true };

    private static Zone NewZone(City city, string code, string arabicName, string englishName) =>
        new() { CityId = city.Id, Code = code, ArabicName = arabicName, EnglishName = englishName, IsActive = true };
}
```

### 6.18 `src/ShippingERP.Infrastructure/Pricing/ShipmentPricingService.cs`

```csharp
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Pricing;
using ShippingERP.Application.Interfaces.Pricing;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Billing;
using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Entities.Settings;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Pricing;

/// <summary>
/// Repricing of existing shipments and the approval of manual price changes (step 5).
///
/// - calculate-price: preview only.
/// - apply-price without manual values: recalculates from the current rules (snapshot "Repriced").
/// - apply-price with manual values (Shipments.OverridePrice + reason):
///     * small change (deviation &lt;= threshold) or caller holds Pricing.ApproveOverride -> applied at once
///       (snapshot "ManualOverride", override row Approved);
///     * otherwise -> override row Pending, the shipment price does not change until an approver approves.
/// A new apply-price replaces (cancels) the shipment's pending request. Cancelled or invoiced shipments cannot be
/// repriced. Every write is one transaction with its audit row.
/// </summary>
public sealed class ShipmentPricingService : IShipmentPricingService
{
    private const string EntityName = "Shipment";

    private static readonly Dictionary<string, Func<IQueryable<ShipmentPriceOverride>, bool, IOrderedQueryable<ShipmentPriceOverride>>> Sorts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["createdAtUtc"] = (q, desc) => desc ? q.OrderByDescending(x => x.CreatedAtUtc) : q.OrderBy(x => x.CreatedAtUtc),
            ["deviationPercentage"] = (q, desc) => desc ? q.OrderByDescending(x => x.DeviationPercentage) : q.OrderBy(x => x.DeviationPercentage),
            ["status"] = (q, desc) => desc ? q.OrderByDescending(x => x.Status) : q.OrderBy(x => x.Status)
        };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentAccessService _access;
    private readonly ShipmentPricingCalculator _calculator;
    private readonly ShippingSettingsService _settings;

    public ShipmentPricingService(
        MasterDataServiceDependencies dependencies,
        ShipmentAccessService access,
        ShipmentPricingCalculator calculator,
        ShippingSettingsService settings)
    {
        _dependencies = dependencies;
        _access = access;
        _calculator = calculator;
        _settings = settings;
    }

    private AppDbContext Db => _dependencies.DbContext;

    // ------------------------------------------------------------------ calculate / apply

    public async Task<ShipmentPriceResultDto> CalculateAsync(long shipmentId, ShipmentPriceRequest request, CancellationToken cancellationToken)
    {
        var query = await _access.ScopeAsync(Db.Shipments.AsNoTracking(), cancellationToken);
        var shipment = await query.FirstOrDefaultAsync(x => x.Id == shipmentId, cancellationToken)
                       ?? throw new NotFoundException("الشحنة", shipmentId);

        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var result = await PriceAsync(shipment, request.ManualShippingCharge, request.ManualDiscountAmount, settings, cancellationToken);
        var evaluation = PriceOverrideEvaluation.From(result, settings.PriceOverrideApprovalThresholdPercent);

        return ToResult(shipment.Id, result, evaluation, settings, applied: false, overrideDto: null, reason: "Preview");
    }

    public async Task<ShipmentPriceResultDto> ApplyAsync(long shipmentId, ShipmentPriceRequest request, CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(shipmentId, cancellationToken);
        await EnsureCanRepriceAsync(shipment, cancellationToken);

        var isManual = request.ManualShippingCharge.HasValue || request.ManualDiscountAmount > 0m;
        // Code without a signed-in user (seeding, jobs) is trusted like everywhere else.
        var user = _dependencies.CurrentUser;
        if (isManual && user.IsAuthenticated && !user.HasPermission(Permissions.Shipments.OverridePrice))
        {
            throw new ForbiddenException("إدخال سعر يدوي أو خصم يحتاج صلاحية Shipments.OverridePrice.");
        }

        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var result = await PriceAsync(shipment, request.ManualShippingCharge, request.ManualDiscountAmount, settings, cancellationToken);
        var evaluation = PriceOverrideEvaluation.From(result, settings.PriceOverrideApprovalThresholdPercent);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var now = _dependencies.DateTimeProvider.UtcNow;
        var userId = _dependencies.CurrentUser.UserId;

        if (!isManual)
        {
            await Db.ExecuteInTransactionAsync(async ct =>
            {
                await ShipmentBillingGuards.CancelPendingOverridesAsync(Db, shipment.Id, userId, now, "أُلغي: أُعيد تسعير الشحنة بالقواعد.", ct);
                var before = new { shipment.ShippingCharge, shipment.DiscountAmount, shipment.TaxAmount, shipment.TotalCharge };

                ApplyToShipment(shipment, result);
                Db.ShipmentPricingSnapshots.Add(result.ToSnapshot(shipment, "Repriced"));
                _dependencies.AuditLog.Add(
                    AuditAction.ShipmentRepriced,
                    entityName: EntityName,
                    entityId: shipment.Id.ToString(CultureInfo.InvariantCulture),
                    details: new { before, after = new { shipment.ShippingCharge, shipment.DiscountAmount, shipment.TaxAmount, shipment.TotalCharge } });

                await Db.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);

            return ToResult(shipment.Id, result, evaluation, settings, applied: true, overrideDto: null, reason: "Repriced");
        }

        var canApprove = !user.IsAuthenticated || user.HasPermission(Permissions.Pricing.ApproveOverride);
        var applyNow = !evaluation.RequiresApproval || canApprove;

        var overrideId = await Db.ExecuteInTransactionAsync(async ct =>
        {
            await ShipmentBillingGuards.CancelPendingOverridesAsync(Db, shipment.Id, userId, now, "أُلغي: حلّ محله طلب أحدث.", ct);

            // Save the cancellation first: the unique index allows only one pending request per shipment.
            await Db.SaveChangesAsync(ct);

            var priceOverride = new ShipmentPriceOverride
            {
                ShipmentId = shipment.Id,
                Status = applyNow ? PriceOverrideStatus.Approved : PriceOverrideStatus.Pending,
                ManualShippingCharge = request.ManualShippingCharge,
                ManualDiscountAmount = request.ManualDiscountAmount,
                Reason = reason ?? string.Empty,
                CurrencyId = result.CurrencyId,
                CalculatedTotalShippingCharge = evaluation.RuleTotalShippingCharge,
                ProposedTotalShippingCharge = evaluation.ProposedTotalShippingCharge,
                DeviationPercentage = evaluation.DeviationPercentage,
                RequiresApproval = evaluation.RequiresApproval,
                DecidedByUserId = applyNow ? userId : null,
                DecidedAtUtc = applyNow ? now : null,
                DecisionNotes = applyNow
                    ? (evaluation.RequiresApproval ? "طُبّق مباشرة: المستخدم يملك صلاحية الاعتماد." : "طُبّق مباشرة: ضمن الحد المسموح.")
                    : null
            };
            Db.ShipmentPriceOverrides.Add(priceOverride);
            await Db.SaveChangesAsync(ct);

            if (applyNow)
            {
                await ApplyOverrideAsync(shipment, priceOverride, result, ct);
            }

            _dependencies.AuditLog.Add(
                applyNow ? AuditAction.PriceOverrideApproved : AuditAction.PriceOverrideRequested,
                entityName: EntityName,
                entityId: shipment.Id.ToString(CultureInfo.InvariantCulture),
                details: new
                {
                    overrideId = priceOverride.Id,
                    request.ManualShippingCharge,
                    request.ManualDiscountAmount,
                    reason,
                    evaluation.RuleTotalShippingCharge,
                    evaluation.ProposedTotalShippingCharge,
                    evaluation.DeviationPercentage
                });
            await Db.SaveChangesAsync(ct);

            return priceOverride.Id;
        }, cancellationToken);

        var dto = await GetOverrideAsync(overrideId, cancellationToken);
        return ToResult(shipment.Id, result, evaluation, settings, applied: applyNow, overrideDto: dto, reason: applyNow ? "ManualOverride" : "Pending");
    }

    // ------------------------------------------------------------------ approval

    public async Task<PagedResult<PriceOverrideDto>> GetOverridesAsync(PriceOverrideListQuery query, CancellationToken cancellationToken)
    {
        var rows = await VisibleOverridesAsync(cancellationToken);

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (query.ShipmentId.HasValue)
        {
            var shipmentId = query.ShipmentId.Value;
            rows = rows.Where(x => x.ShipmentId == shipmentId);
        }

        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value;
            rows = rows.Where(x => x.CreatedAtUtc >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = query.ToUtc.Value;
            rows = rows.Where(x => x.CreatedAtUtc < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                x.Shipment.ShipmentNumber.Contains(search) ||
                x.Shipment.TrackingNumber.Contains(search) ||
                x.Shipment.SenderCustomer.ArabicName.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = !string.IsNullOrWhiteSpace(query.SortBy) && Sorts.TryGetValue(query.SortBy, out var sort)
            ? sort(rows, query.IsDescending())
            : rows.OrderByDescending(x => x.CreatedAtUtc);

        var items = await Project(ordered.ThenByDescending(x => x.Id)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<PriceOverrideDto>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<PriceOverrideDto> GetOverrideAsync(long id, CancellationToken cancellationToken)
    {
        var rows = await VisibleOverridesAsync(cancellationToken);
        return await Project(rows.Where(x => x.Id == id)).FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("طلب تعديل السعر", id);
    }

    public async Task<ShipmentPriceResultDto> ApproveOverrideAsync(long id, DecidePriceOverrideRequest request, CancellationToken cancellationToken)
    {
        var priceOverride = await FindPendingAsync(id, cancellationToken);
        var shipment = await _access.FindForUpdateAsync(priceOverride.ShipmentId, cancellationToken);
        await EnsureCanRepriceAsync(shipment, cancellationToken);

        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var result = await PriceAsync(shipment, priceOverride.ManualShippingCharge, priceOverride.ManualDiscountAmount, settings, cancellationToken);
        var evaluation = PriceOverrideEvaluation.From(result, settings.PriceOverrideApprovalThresholdPercent);
        var now = _dependencies.DateTimeProvider.UtcNow;

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            priceOverride.Status = PriceOverrideStatus.Approved;
            priceOverride.DecidedByUserId = _dependencies.CurrentUser.UserId;
            priceOverride.DecidedAtUtc = now;
            priceOverride.DecisionNotes = Optional(request.Notes);
            priceOverride.CalculatedTotalShippingCharge = evaluation.RuleTotalShippingCharge;
            priceOverride.ProposedTotalShippingCharge = evaluation.ProposedTotalShippingCharge;
            priceOverride.DeviationPercentage = evaluation.DeviationPercentage;

            await ApplyOverrideAsync(shipment, priceOverride, result, ct);

            _dependencies.AuditLog.Add(
                AuditAction.PriceOverrideApproved,
                entityName: EntityName,
                entityId: shipment.Id.ToString(CultureInfo.InvariantCulture),
                details: new { overrideId = priceOverride.Id, request.Notes, shipment.TotalCharge });
            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        var dto = await GetOverrideAsync(id, cancellationToken);
        return ToResult(shipment.Id, result, evaluation, settings, applied: true, overrideDto: dto, reason: "ManualOverride");
    }

    public async Task<PriceOverrideDto> RejectOverrideAsync(long id, DecidePriceOverrideRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Notes))
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["notes"] = new[] { "سبب الرفض مطلوب." } });
        }

        var priceOverride = await FindPendingAsync(id, cancellationToken);

        priceOverride.Status = PriceOverrideStatus.Rejected;
        priceOverride.DecidedByUserId = _dependencies.CurrentUser.UserId;
        priceOverride.DecidedAtUtc = _dependencies.DateTimeProvider.UtcNow;
        priceOverride.DecisionNotes = request.Notes.Trim();

        _dependencies.AuditLog.Add(
            AuditAction.PriceOverrideRejected,
            entityName: EntityName,
            entityId: priceOverride.ShipmentId.ToString(CultureInfo.InvariantCulture),
            details: new { overrideId = priceOverride.Id, request.Notes });

        await Db.SaveChangesAsync(cancellationToken);
        return await GetOverrideAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<PricingResult> PriceAsync(
        Shipment shipment,
        decimal? manualShippingCharge,
        decimal manualDiscountAmount,
        ShippingSettings settings,
        CancellationToken cancellationToken)
    {
        var shipmentId = shipment.Id;
        var senderId = shipment.SenderCustomerId;

        var senderPlanId = await Db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.Id == senderId)
            .Select(x => x.PricingPlanId)
            .FirstOrDefaultAsync(cancellationToken);

        var isReturn = await Db.ShipmentReturns.AsNoTracking()
            .AnyAsync(x => x.ShipmentId == shipmentId && x.Status != ShipmentReturnStatus.Cancelled, cancellationToken);

        return await _calculator.CalculateAsync(
            new PricingInput
            {
                SenderCustomerId = senderId,
                CustomerPricingPlanId = senderPlanId,
                OriginZoneId = shipment.OriginZoneId,
                DestinationZoneId = shipment.DestinationZoneId,
                OriginCityId = shipment.OriginCityId,
                DestinationCityId = shipment.DestinationCityId,
                ServiceTypeId = shipment.ServiceTypeId,
                ActualWeight = shipment.ActualWeight,
                VolumetricWeight = shipment.VolumetricWeight,
                ChargeableWeight = shipment.ChargeableWeight,
                PackageCount = shipment.PackageCount,
                PricingBasis = shipment.PricingBasis,
                VolumeCbm = shipment.TotalVolumeCbm,
                IsCod = shipment.IsCOD,
                CodAmount = shipment.CODAmount,
                InsuranceValue = shipment.InsuranceValue,
                RequiresPickup = shipment.RequiresPickup,
                RequiresPackaging = shipment.RequiresPackaging,
                IsReturn = isReturn,
                ManualShippingCharge = manualShippingCharge,
                ManualDiscountAmount = manualDiscountAmount
            },
            settings,
            cancellationToken);
    }

    /// <summary>Cancelled and invoiced shipments keep their price.</summary>
    private async Task EnsureCanRepriceAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        if (shipment.Status == ShipmentStatus.Cancelled)
        {
            throw new BusinessRuleException("لا يمكن تسعير شحنة ملغاة.");
        }

        await ShipmentBillingGuards.EnsureNotInvoicedAsync(Db, shipment.Id, "تغيير سعر الشحنة", cancellationToken);
    }

    /// <summary>Writes the new price on the shipment and its snapshot; links the snapshot to the override.</summary>
    private async Task ApplyOverrideAsync(Shipment shipment, ShipmentPriceOverride priceOverride, PricingResult result, CancellationToken cancellationToken)
    {
        ApplyToShipment(shipment, result);

        var snapshot = result.ToSnapshot(shipment, "ManualOverride", priceOverride.Reason, priceOverride.Id);
        Db.ShipmentPricingSnapshots.Add(snapshot);
        await Db.SaveChangesAsync(cancellationToken);

        priceOverride.AppliedPricingSnapshotId = snapshot.Id;
    }

    private static void ApplyToShipment(Shipment shipment, PricingResult result)
    {
        shipment.CurrencyId = result.CurrencyId;
        shipment.ShippingCharge = result.ShippingCharge;
        shipment.DiscountAmount = result.DiscountAmount;
        shipment.TaxAmount = result.TaxAmount;
        shipment.TotalCharge = result.TotalCharge;
    }

    private async Task<ShipmentPriceOverride> FindPendingAsync(long id, CancellationToken cancellationToken)
    {
        var rows = await VisibleOverridesAsync(cancellationToken, tracked: true);
        var priceOverride = await rows.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                            ?? throw new NotFoundException("طلب تعديل السعر", id);

        if (priceOverride.Status != PriceOverrideStatus.Pending)
        {
            throw new BusinessRuleException($"الطلب ليس بانتظار الموافقة (الحالة: {BillingNames.Arabic(priceOverride.Status)}).");
        }

        var userId = _dependencies.CurrentUser.UserId;
        if (userId.HasValue && priceOverride.CreatedByUserId == userId)
        {
            throw new BusinessRuleException("لا يمكن اعتماد أو رفض طلب تعديل السعر الذي قدّمته بنفسك؛ يجب أن يقرره مستخدم آخر.");
        }

        return priceOverride;
    }

    /// <summary>Overrides of the shipments the caller can see.</summary>
    private async Task<IQueryable<ShipmentPriceOverride>> VisibleOverridesAsync(CancellationToken cancellationToken, bool tracked = false)
    {
        // AsNoTracking anywhere in an EF query makes the WHOLE query untracked, so the shipment sub-query must not
        // use it when the overrides are loaded for an update.
        var shipments = await _access.ScopeAsync(tracked ? Db.Shipments : Db.Shipments.AsNoTracking(), cancellationToken);
        var overrides = tracked ? Db.ShipmentPriceOverrides : Db.ShipmentPriceOverrides.AsNoTracking();
        return overrides.Where(x => shipments.Any(s => s.Id == x.ShipmentId));
    }

    private IQueryable<PriceOverrideDto> Project(IQueryable<ShipmentPriceOverride> query) => query.Select(x => new PriceOverrideDto
    {
        Id = x.Id,
        ShipmentId = x.ShipmentId,
        ShipmentNumber = x.Shipment.ShipmentNumber,
        TrackingNumber = x.Shipment.TrackingNumber,
        SenderName = x.Shipment.SenderCustomer.ArabicName,
        Status = x.Status,
        StatusName = BillingNames.Arabic(x.Status),
        ManualShippingCharge = x.ManualShippingCharge,
        ManualDiscountAmount = x.ManualDiscountAmount,
        Reason = x.Reason,
        CurrencyCode = Db.Currencies.IgnoreQueryFilters().Where(c => c.Id == x.CurrencyId).Select(c => c.Code).FirstOrDefault() ?? string.Empty,
        CalculatedTotalShippingCharge = x.CalculatedTotalShippingCharge,
        ProposedTotalShippingCharge = x.ProposedTotalShippingCharge,
        DeviationPercentage = x.DeviationPercentage,
        RequiresApproval = x.RequiresApproval,
        RequestedByUserId = x.CreatedByUserId,
        RequestedByUserName = Db.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault(),
        RequestedAtUtc = x.CreatedAtUtc,
        DecidedByUserId = x.DecidedByUserId,
        DecidedByUserName = Db.Users.Where(u => u.Id == x.DecidedByUserId).Select(u => u.FullName).FirstOrDefault(),
        DecidedAtUtc = x.DecidedAtUtc,
        DecisionNotes = x.DecisionNotes,
        AppliedPricingSnapshotId = x.AppliedPricingSnapshotId
    });

    private static ShipmentPriceResultDto ToResult(
        long shipmentId,
        PricingResult result,
        PriceOverrideEvaluation evaluation,
        ShippingSettings settings,
        bool applied,
        PriceOverrideDto? overrideDto,
        string reason) => new()
    {
        ShipmentId = shipmentId,
        Pricing = result.ToDto(reason),
        RulePricing = result.ToRuleDto(),
        IsManual = evaluation.IsManual,
        DeviationPercentage = evaluation.DeviationPercentage,
        ApprovalThresholdPercent = settings.PriceOverrideApprovalThresholdPercent,
        RequiresApproval = evaluation.RequiresApproval,
        Applied = applied,
        Override = overrideDto
    };

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

### 6.19 `src/ShippingERP.Infrastructure/Shipments/DriverManifestService.cs`

```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>
/// Driver delivery manifests. Dispatch makes every shipment OutForDelivery and assigns it to the driver;
/// delivery results come from the DeliveryService (proof of delivery / failed attempt); Close turns the
/// remaining pending shipments into DeliveryFailed.
///
/// Visibility: a driver-only user sees only his own manifests; other users see the manifests of their branch.
/// </summary>
public sealed class DriverManifestService : IDriverManifestService
{
    private const string ReferenceType = "DriverDeliveryManifest";
    private const string DefaultPendingReason = "لم يتم التسليم - أُغلق بيان التوزيع";

    private static readonly Expression<Func<DriverDeliveryManifest, DriverManifestDto>> ToListDto = x => new DriverManifestDto
    {
        Id = x.Id,
        ManifestNumber = x.ManifestNumber,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        DriverId = x.DriverId,
        DriverName = x.Driver.Employee.ArabicName,
        VehicleId = x.VehicleId,
        VehiclePlateNumber = x.Vehicle != null ? x.Vehicle.PlateNumber : null,
        ManifestDateUtc = x.ManifestDateUtc,
        Status = x.Status,
        ItemCount = x.Items.Count(),
        DeliveredCount = x.Items.Count(i => i.Result == DeliveryResult.Delivered),
        FailedCount = x.Items.Count(i => i.Result == DeliveryResult.Failed),
        PendingCount = x.Items.Count(i => i.Result == DeliveryResult.Pending),
        TotalCodAmount = x.Items.Where(i => i.Shipment.IsCOD).Sum(i => i.Shipment.CODAmount),
        DispatchedAtUtc = x.DispatchedAtUtc,
        ClosedAtUtc = x.ClosedAtUtc,
        Notes = x.Notes,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentAccessService _access;
    private readonly ShipmentStatusChanger _statusChanger;
    private readonly DocumentNumberGenerator _numbers;

    public DriverManifestService(
        MasterDataServiceDependencies dependencies,
        ShipmentAccessService access,
        ShipmentStatusChanger statusChanger,
        DocumentNumberGenerator numbers)
    {
        _dependencies = dependencies;
        _access = access;
        _statusChanger = statusChanger;
        _numbers = numbers;
    }

    private AppDbContext Db => _dependencies.DbContext;

    public async Task<PagedResult<DriverManifestDto>> GetPagedAsync(DriverManifestListQuery query, CancellationToken cancellationToken)
    {
        var rows = DocumentQueryHelpers.ApplyDateRange((await ScopedAsync(cancellationToken)).AsNoTracking(), query);

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId);
        }

        if (query.DriverId.HasValue)
        {
            var driverId = query.DriverId.Value;
            rows = rows.Where(x => x.DriverId == driverId);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.ManifestNumber.Contains(search) || x.Items.Any(i => i.Shipment.TrackingNumber == search));
        }

        return await DocumentQueryHelpers.ToPagedAsync(rows, query, x => x.ManifestNumber, ToListDto, cancellationToken);
    }

    public async Task<DriverManifestDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var manifest = await (await ScopedAsync(cancellationToken)).AsNoTracking()
                           .Where(x => x.Id == id)
                           .Select(ToListDto)
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw new NotFoundException("بيان التوزيع", id);

        var items = await Db.DriverDeliveryManifestItems.AsNoTracking()
            .Where(x => x.ManifestId == id)
            .OrderBy(x => x.Sequence)
            .Select(x => new DriverManifestItemDto
            {
                ItemId = x.Id,
                ShipmentId = x.ShipmentId,
                ShipmentNumber = x.Shipment.ShipmentNumber,
                TrackingNumber = x.Shipment.TrackingNumber,
                ShipmentStatus = x.Shipment.Status,
                ShipmentStatusName = ShipmentStatusNames.Arabic(x.Shipment.Status),
                RecipientName = x.Shipment.RecipientCustomer.ArabicName,
                RecipientPhone = x.Shipment.RecipientAddress != null && x.Shipment.RecipientAddress.Phone != null
                    ? x.Shipment.RecipientAddress.Phone
                    : x.Shipment.RecipientCustomer.Phone,
                RecipientAddressLine = x.Shipment.RecipientAddress != null ? x.Shipment.RecipientAddress.AddressLine : null,
                DestinationCityName = x.Shipment.DestinationCity.ArabicName,
                DestinationZoneName = x.Shipment.DestinationZone != null ? x.Shipment.DestinationZone.ArabicName : null,
                PackageCount = x.Shipment.PackageCount,
                ChargeableWeight = x.Shipment.ChargeableWeight,
                IsCOD = x.Shipment.IsCOD,
                CODAmount = x.Shipment.CODAmount,
                ChargesOnRecipient = x.Shipment.ChargesPaidBy == ChargesPayer.Recipient,
                ChargesToCollect = x.Shipment.ChargesPaidBy == ChargesPayer.Recipient ? x.Shipment.TotalCharge : 0m,
                Sequence = x.Sequence,
                Result = x.Result,
                ResultAtUtc = x.ResultAtUtc,
                FailureReason = x.FailureReason,
                ProofOfDeliveryId = x.ProofOfDeliveryId
            })
            .ToListAsync(cancellationToken);

        return WithItems(manifest, items);
    }

    public async Task<DriverManifestDto> CreateAsync(CreateDriverManifestRequest request, CancellationToken cancellationToken)
    {
        var now = _dependencies.DateTimeProvider.UtcNow;
        var manifestDate = DateTime.SpecifyKind((request.ManifestDateUtc ?? now).Date, DateTimeKind.Utc);

        var driver = await DriverRules.GetUsableDriverAsync(Db, request.DriverId, null, manifestDate, cancellationToken);
        _dependencies.BranchAccess.EnsureCanAccess(driver.BranchId);

        var vehicleId = request.VehicleId ?? driver.DefaultVehicleId;
        if (vehicleId.HasValue)
        {
            await DriverRules.EnsureUsableVehicleAsync(Db, vehicleId.Value, driver.BranchId, cancellationToken);
        }

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            var manifest = new DriverDeliveryManifest
            {
                ManifestNumber = await _numbers.NextAsync(DocumentPrefixes.DriverManifest, ct),
                BranchId = driver.BranchId,
                DriverId = driver.Id,
                VehicleId = vehicleId,
                ManifestDateUtc = manifestDate,
                Status = DriverManifestStatus.Draft,
                Notes = Optional(request.Notes)
            };

            Db.DriverDeliveryManifests.Add(manifest);

            if (request.Codes.Count > 0)
            {
                await AddItemsAsync(manifest, request.Codes, ct);
            }

            await Db.SaveChangesAsync(ct);
            return manifest.Id;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DriverManifestDto> AddShipmentsAsync(long id, ShipmentCodesRequest request, CancellationToken cancellationToken)
    {
        var manifest = await FindForUpdateAsync(id, DriverManifestStatus.Draft, cancellationToken);
        await AddItemsAsync(manifest, request.Codes, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DriverManifestDto> RemoveShipmentAsync(long id, long shipmentId, CancellationToken cancellationToken)
    {
        var manifest = await FindForUpdateAsync(id, DriverManifestStatus.Draft, cancellationToken);
        var item = manifest.Items.FirstOrDefault(x => x.ShipmentId == shipmentId)
                   ?? throw new NotFoundException("الشحنة غير موجودة في هذا البيان.");

        manifest.Items.Remove(item);
        Db.DriverDeliveryManifestItems.Remove(item);

        // Keep the stop order without gaps.
        var sequence = 1;
        foreach (var remaining in manifest.Items.OrderBy(x => x.Sequence))
        {
            remaining.Sequence = sequence++;
        }

        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DriverManifestDto> DispatchAsync(long id, CancellationToken cancellationToken)
    {
        var manifest = await FindForUpdateAsync(id, DriverManifestStatus.Draft, cancellationToken);
        if (manifest.Items.Count == 0)
        {
            throw new BusinessRuleException("لا يمكن تسليم بيان فارغ للسائق.");
        }

        var now = _dependencies.DateTimeProvider.UtcNow;
        await DriverRules.GetUsableDriverAsync(Db, manifest.DriverId, manifest.BranchId, now, cancellationToken);

        var shipments = await LoadShipmentsAsync(manifest, cancellationToken);
        var errors = manifest.Items
            .Select(item => ValidateLine(manifest, shipments[item.ShipmentId]))
            .Where(error => error is not null)
            .Select(error => error!)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["items"] = errors });
        }

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var item in manifest.Items.OrderBy(x => x.Sequence))
            {
                var shipment = shipments[item.ShipmentId];
                shipment.AssignedDriverId = manifest.DriverId;
                shipment.VehicleId = manifest.VehicleId;
                shipment.DeliveryAttemptCount++;

                _statusChanger.Change(shipment, ShipmentStatus.OutForDelivery, new StatusChange(ShipmentStatusChangeSource.DriverManifest)
                {
                    OccurredAtUtc = now,
                    DriverId = manifest.DriverId,
                    ReferenceType = ReferenceType,
                    ReferenceId = manifest.Id,
                    ReferenceNumber = manifest.ManifestNumber
                });
            }

            manifest.Status = DriverManifestStatus.Dispatched;
            manifest.DispatchedAtUtc = now;

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DriverManifestDto> CloseAsync(long id, CloseDriverManifestRequest request, CancellationToken cancellationToken)
    {
        var manifest = await FindForUpdateAsync(id, DriverManifestStatus.Dispatched, cancellationToken);
        var shipments = await LoadShipmentsAsync(manifest, cancellationToken);
        var now = _dependencies.DateTimeProvider.UtcNow;
        var reason = string.IsNullOrWhiteSpace(request.PendingReason) ? DefaultPendingReason : request.PendingReason.Trim();

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var item in manifest.Items.Where(x => x.Result == DeliveryResult.Pending))
            {
                var shipment = shipments[item.ShipmentId];

                if (shipment.Status == ShipmentStatus.OutForDelivery)
                {
                    _statusChanger.Change(shipment, ShipmentStatus.DeliveryFailed, new StatusChange(ShipmentStatusChangeSource.DriverManifest)
                    {
                        OccurredAtUtc = now,
                        DriverId = manifest.DriverId,
                        Notes = reason,
                        ReferenceType = ReferenceType,
                        ReferenceId = manifest.Id,
                        ReferenceNumber = manifest.ManifestNumber
                    });
                }

                item.Result = shipment.Status == ShipmentStatus.Delivered ? DeliveryResult.Delivered : DeliveryResult.Failed;
                item.ResultAtUtc = now;
                item.FailureReason = item.Result == DeliveryResult.Failed ? (reason.Length > 300 ? reason[..300] : reason) : null;
            }

            manifest.Status = DriverManifestStatus.Closed;
            manifest.ClosedAtUtc = now;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                var notes = string.IsNullOrWhiteSpace(manifest.Notes) ? request.Notes.Trim() : $"{manifest.Notes} | {request.Notes.Trim()}";
                manifest.Notes = notes.Length > 500 ? notes[..500] : notes;
            }

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<DriverManifestDto> CancelAsync(long id, CancellationToken cancellationToken)
    {
        var manifest = await FindForUpdateAsync(id, DriverManifestStatus.Draft, cancellationToken);
        manifest.Status = DriverManifestStatus.Cancelled;
        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<IQueryable<DriverDeliveryManifest>> ScopedAsync(CancellationToken cancellationToken)
    {
        if (_access.IsDriverOnly)
        {
            var driverId = await _access.GetCurrentDriverIdAsync(cancellationToken);
            return driverId is null
                ? Db.DriverDeliveryManifests.Where(_ => false)
                : Db.DriverDeliveryManifests.Where(x => x.DriverId == driverId);
        }

        if (_dependencies.BranchAccess.CanAccessAllBranches)
        {
            return Db.DriverDeliveryManifests;
        }

        var branchId = _dependencies.BranchAccess.CurrentBranchId;
        return Db.DriverDeliveryManifests.Where(x => x.BranchId == branchId);
    }

    private async Task<DriverDeliveryManifest> FindForUpdateAsync(long id, DriverManifestStatus requiredStatus, CancellationToken cancellationToken)
    {
        if (_access.IsDriverOnly)
        {
            throw new ForbiddenException("السائق لا يمكنه تعديل بيانات التوزيع.");
        }

        var manifest = await (await ScopedAsync(cancellationToken))
                           .Include(x => x.Items)
                           .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                       ?? throw new NotFoundException("بيان التوزيع", id);

        if (manifest.Status != requiredStatus)
        {
            throw new BusinessRuleException(requiredStatus == DriverManifestStatus.Draft
                ? $"البيان {manifest.ManifestNumber} لم يعد مسودة."
                : $"البيان {manifest.ManifestNumber} ليس مع السائق حالياً؛ لا يمكن إغلاقه.");
        }

        return manifest;
    }

    private async Task<Dictionary<long, Shipment>> LoadShipmentsAsync(DriverDeliveryManifest manifest, CancellationToken cancellationToken)
    {
        var ids = manifest.Items.Select(x => x.ShipmentId).ToList();
        return await Db.Shipments.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    private async Task AddItemsAsync(DriverDeliveryManifest manifest, IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
    {
        var resolved = await ShipmentCodeResolver.ResolveAsync(Db, codes, cancellationToken);
        var errors = new List<string>();
        var toAdd = new List<Shipment>();
        long? exceptId = manifest.Id == 0 ? null : manifest.Id;

        foreach (var (code, shipment) in resolved)
        {
            if (shipment is null)
            {
                errors.Add($"{code}: لم يتم العثور على الشحنة.");
                continue;
            }

            if (manifest.Items.Any(x => x.ShipmentId == shipment.Id) || toAdd.Contains(shipment))
            {
                continue;
            }

            var error = ValidateLine(manifest, shipment);
            if (error is not null)
            {
                errors.Add(error);
                continue;
            }

            var other = await ShipmentDocumentGuards.FindOpenDriverManifestAsync(Db, shipment.Id, exceptId, cancellationToken)
                        ?? await ShipmentDocumentGuards.FindOpenTransferAsync(Db, shipment.Id, null, cancellationToken);
            if (other is not null)
            {
                errors.Add($"{shipment.TrackingNumber}: موجودة على مستند مفتوح آخر ({other}).");
                continue;
            }

            toAdd.Add(shipment);
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["codes"] = errors.ToArray() });
        }

        var sequence = manifest.Items.Count == 0 ? 0 : manifest.Items.Max(x => x.Sequence);
        foreach (var shipment in toAdd)
        {
            manifest.Items.Add(new DriverDeliveryManifestItem
            {
                Manifest = manifest,
                ShipmentId = shipment.Id,
                Sequence = ++sequence,
                Result = DeliveryResult.Pending
            });
        }
    }

    private static string? ValidateLine(DriverDeliveryManifest manifest, Shipment shipment)
    {
        if (shipment.CurrentBranchId != manifest.BranchId)
        {
            return $"{shipment.TrackingNumber}: الشحنة ليست في فرع السائق.";
        }

        if (shipment.ReturnBranchId.HasValue)
        {
            return $"{shipment.TrackingNumber}: عليها مرتجع مفتوح.";
        }

        return ShipmentStatusChanger.CanChange(shipment, ShipmentStatus.OutForDelivery, ShipmentStatusChangeSource.DriverManifest)
            ? null
            : $"{shipment.TrackingNumber}: لا يمكن إخراجها للتوصيل وهي في حالة «{ShipmentStatusNames.Arabic(shipment.Status)}».";
    }

    private static DriverManifestDto WithItems(DriverManifestDto x, IReadOnlyList<DriverManifestItemDto> items) => new()
    {
        Id = x.Id,
        ManifestNumber = x.ManifestNumber,
        BranchId = x.BranchId,
        BranchName = x.BranchName,
        DriverId = x.DriverId,
        DriverName = x.DriverName,
        VehicleId = x.VehicleId,
        VehiclePlateNumber = x.VehiclePlateNumber,
        ManifestDateUtc = x.ManifestDateUtc,
        Status = x.Status,
        ItemCount = x.ItemCount,
        DeliveredCount = x.DeliveredCount,
        FailedCount = x.FailedCount,
        PendingCount = x.PendingCount,
        TotalCodAmount = x.TotalCodAmount,
        DispatchedAtUtc = x.DispatchedAtUtc,
        ClosedAtUtc = x.ClosedAtUtc,
        Notes = x.Notes,
        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        Items = items
    };

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

### 6.20 `src/ShippingERP.Infrastructure/Shipments/Labels/HtmlLabelRenderer.cs`

```csharp
using System.Globalization;
using System.Net;
using System.Text;

namespace ShippingERP.Infrastructure.Shipments.Labels;

/// <summary>
/// The same labels as a self-contained printable HTML page (open it and press Ctrl+P). Every user value is
/// HTML-encoded; barcodes are inline SVG; a strict Content-Security-Policy blocks scripts and external resources.
/// Set the printer to the label size and "no margins" for best results.
/// </summary>
public sealed class HtmlLabelRenderer
{
    public string Render(IReadOnlyList<ShipmentLabelData> labels, LabelOptions options)
    {
        var width = options.WidthMm.ToString(CultureInfo.InvariantCulture);
        var height = options.HeightMm.ToString(CultureInfo.InvariantCulture);
        var scale = options.Scale;

        string Pt(float size) => (size * scale).ToString("0.##", CultureInfo.InvariantCulture) + "pt";

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"ar\" dir=\"rtl\"><head><meta charset=\"utf-8\">");
        html.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:\">");
        html.Append("<title>").Append(Encode(labels.Count == 1 ? labels[0].ShipmentNumber : "ملصقات الشحنات")).Append("</title><style>");
        html.Append("@page{size:").Append(width).Append("mm ").Append(height).Append("mm;margin:0}");
        html.Append("*{box-sizing:border-box}body{margin:0;font-family:'Cairo','Tahoma','Arial',sans-serif;color:#000;background:#fff}");
        html.Append(".label{width:").Append(width).Append("mm;height:").Append(height).Append("mm;padding:3mm;overflow:hidden;page-break-after:always;break-after:page;display:flex;flex-direction:column;gap:1.2mm;line-height:1.25;font-size:").Append(Pt(8)).Append('}');
        html.Append(".row{display:flex;justify-content:space-between;align-items:center;gap:2mm}");
        html.Append(".box{border:1px solid #000;padding:1.5mm}.thin{border-width:.5px}");
        html.Append(".b{font-weight:700}.small{font-size:").Append(Pt(7)).Append('}');
        html.Append(".service{border:1px solid #000;padding:0 2mm;font-weight:700}");
        html.Append(".barcode{height:").Append((options.HeightMm * 0.11f).ToString("0.##", CultureInfo.InvariantCulture)).Append("mm;flex:none}.barcode svg{width:100%;height:100%}");
        html.Append(".tracking{text-align:center;font-weight:700;letter-spacing:.08em;direction:ltr;font-size:").Append(Pt(13)).Append('}');
        html.Append(".recipient{display:flex;gap:2mm}.recipient .info{flex:1}.name{font-weight:700;font-size:").Append(Pt(11)).Append('}');
        html.Append(".dest{font-weight:700;font-size:").Append(Pt(10)).Append('}');
        html.Append(".code{width:22%;background:#000;color:#fff;font-weight:700;display:flex;align-items:center;justify-content:center;font-size:").Append(Pt(16)).Append(";-webkit-print-color-adjust:exact;print-color-adjust:exact}");
        html.Append(".cod{background:#000;color:#fff;text-align:center;font-weight:700;padding:1mm;-webkit-print-color-adjust:exact;print-color-adjust:exact;font-size:").Append(Pt(12)).Append('}');
        html.Append(".nocod{text-align:center}.qr{width:").Append((options.HeightMm * 0.13f).ToString("0.##", CultureInfo.InvariantCulture)).Append("mm;flex:none}.qr svg{width:100%;height:auto;display:block}");
        html.Append(".ltr{direction:ltr;unicode-bidi:embed}.amount{font-size:").Append(Pt(14)).Append('}');
        // On screen: grey page with centred labels (printing uses the @page size, without margins).
        html.Append("@media screen{body{background:#e5e7eb;padding:4mm 0}.label{margin:0 auto 4mm;background:#fff;box-shadow:0 1px 4px rgba(0,0,0,.25)}}");
        html.Append("</style></head><body>");

        foreach (var label in labels)
        {
            var barcode = BarcodeGenerator.Code128Svg(label.TrackingNumber);
            var qr = BarcodeGenerator.QrSvg(options.QrContent(label.TrackingNumber));

            foreach (var package in label.Packages)
            {
                html.Append("<section class=\"label\">");

                html.Append("<div class=\"row\"><span class=\"b\">").Append(Encode(LabelText.Clip(label.CompanyName, 40)))
                    .Append("</span><span class=\"service\">").Append(Encode(LabelText.Clip(label.ServiceTypeName, 20))).Append("</span></div>");

                html.Append("<div class=\"barcode\">").Append(barcode).Append("</div>");
                html.Append("<div class=\"tracking\">").Append(Encode(label.TrackingNumber)).Append("</div>");

                html.Append("<div class=\"row\"><span>رقم الشحنة: <span class=\"b ltr\">").Append(Encode(label.ShipmentNumber))
                    .Append("</span></span><span class=\"ltr\">").Append(label.CreatedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("</span></div>");

                html.Append("<div class=\"box recipient\"><div class=\"info\"><div class=\"small\">إلى (المستلم)</div>");
                html.Append("<div class=\"name\">").Append(Encode(LabelText.Clip(label.RecipientName, 45))).Append("</div>");
                html.Append("<div class=\"ltr\" style=\"text-align:right\">").Append(Encode(label.RecipientPhone)).Append("</div>");
                if (!string.IsNullOrWhiteSpace(label.RecipientAddressLine))
                {
                    html.Append("<div>").Append(Encode(LabelText.Clip(label.RecipientAddressLine, 90))).Append("</div>");
                }

                if (!string.IsNullOrWhiteSpace(label.RecipientLandmark))
                {
                    html.Append("<div class=\"small\">قرب: ").Append(Encode(LabelText.Clip(label.RecipientLandmark, 50))).Append("</div>");
                }

                html.Append("<div class=\"dest\">").Append(Encode(LabelText.Clip(label.DestinationText, 60))).Append("</div></div>");
                html.Append("<div class=\"code\">").Append(Encode(label.DestinationBranchCode ?? "--")).Append("</div></div>");

                html.Append("<div class=\"box thin\"><div class=\"small\">من (المرسل)</div><div><span class=\"b\">")
                    .Append(Encode(LabelText.Clip(label.SenderName, 40))).Append("</span> <span class=\"ltr\">").Append(Encode(label.SenderPhone))
                    .Append("</span></div><div class=\"small\">").Append(Encode($"{label.OriginCityName} - {label.OriginBranchCode}")).Append("</div></div>");

                html.Append("<div class=\"row\"><span><span class=\"b\">").Append(Encode($"الطرد {package.SequenceNumber} من {label.PackageCount}"))
                    .Append("</span> <span class=\"small ltr\">").Append(Encode(package.PackageBarcode)).Append("</span></span><span>الوزن المحتسب: ")
                    .Append(Encode(LabelText.Weight(label.ChargeableWeight))).Append("</span></div>");

                if (label.HasCollection)
                {
                    html.Append("<div class=\"cod\"><div>").Append(Encode(label.CollectTitle)).Append("</div><div class=\"ltr amount\">").Append(Encode(label.CollectText)).Append("</div></div>");
                }
                else
                {
                    html.Append("<div class=\"box thin nocod\">لا يوجد مبلغ للتحصيل</div>");
                }

                html.Append("<div class=\"row\" style=\"align-items:flex-start\"><div class=\"qr\">").Append(qr).Append("</div><div style=\"flex:1\">");
                html.Append("<div class=\"small\">المحتوى: ").Append(Encode(LabelText.Clip(label.PackageDescription, 60))).Append("</div>");
                if (!string.IsNullOrWhiteSpace(label.SenderNotes))
                {
                    html.Append("<div class=\"small\">ملاحظات: ").Append(Encode(LabelText.Clip(label.SenderNotes, 100))).Append("</div>");
                }

                html.Append("</div></div></section>");
            }
        }

        html.Append("</body></html>");
        return html.ToString();
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
```

### 6.21 `src/ShippingERP.Infrastructure/Shipments/Labels/LabelModels.cs`

```csharp
using System.Globalization;

namespace ShippingERP.Infrastructure.Shipments.Labels;

/// <summary>Everything printed on the labels of one shipment (one label per package).</summary>
public sealed class ShipmentLabelData
{
    public string CompanyName { get; init; } = string.Empty;
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public string ServiceTypeName { get; init; } = string.Empty;
    public string OriginBranchCode { get; init; } = string.Empty;
    public string? DestinationBranchCode { get; init; }
    public string? DestinationBranchName { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string SenderPhone { get; init; } = string.Empty;
    public string OriginCityName { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string RecipientPhone { get; init; } = string.Empty;
    public string? RecipientAddressLine { get; init; }
    public string? RecipientLandmark { get; init; }
    public string DestinationCityName { get; init; } = string.Empty;
    public string? DestinationZoneName { get; init; }
    public int PackageCount { get; init; }
    public decimal ChargeableWeight { get; init; }
    public bool IsCod { get; init; }
    public decimal CodAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;

    /// <summary>Step 9: the shipping charges are collected from the recipient.</summary>
    public bool ChargesOnRecipient { get; init; }

    /// <summary>Total shipping charge (collected when ChargesOnRecipient).</summary>
    public decimal ChargesAmount { get; init; }

    public string PackageDescription { get; init; } = string.Empty;
    public string? SenderNotes { get; init; }
    public IReadOnlyList<ShipmentLabelPackage> Packages { get; init; } = Array.Empty<ShipmentLabelPackage>();

    /// <summary>"125,000 SYP"</summary>
    public string CodText => string.Create(CultureInfo.InvariantCulture, $"{CodAmount:#,0.##} {CurrencyCode}");

    /// <summary>Step 9: something to collect from the recipient (COD amount and/or the shipping charges).</summary>
    public bool HasCollection => IsCod || (ChargesOnRecipient && ChargesAmount > 0m);

    /// <summary>COD + the charges when they are on the recipient.</summary>
    public decimal CollectAmount => (IsCod ? CodAmount : 0m) + (ChargesOnRecipient ? ChargesAmount : 0m);

    public string CollectText => string.Create(CultureInfo.InvariantCulture, $"{CollectAmount:#,0.##} {CurrencyCode}");

    public string CollectTitle =>
        IsCod && ChargesOnRecipient && ChargesAmount > 0m ? "المطلوب من المستلم (المبلغ + أجور الشحن)"
        : IsCod ? "الدفع عند الاستلام - COD"
        : "أجور الشحن على المستلم";

    /// <summary>Address line, zone, city on one line.</summary>
    public string DestinationText =>
        string.Join(" - ", new[] { DestinationZoneName, DestinationCityName }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public sealed record ShipmentLabelPackage(int SequenceNumber, string PackageBarcode, decimal WeightKg);

/// <summary>Label size and QR content, from the shipping settings.</summary>
public sealed record LabelOptions(int WidthMm, int HeightMm, string? TrackingUrlTemplate)
{
    /// <summary>What the QR code contains: the tracking page URL when configured, otherwise the tracking number.</summary>
    public string QrContent(string trackingNumber) =>
        string.IsNullOrWhiteSpace(TrackingUrlTemplate)
            ? trackingNumber
            : TrackingUrlTemplate.Replace("{trackingNumber}", Uri.EscapeDataString(trackingNumber), StringComparison.Ordinal);

    /// <summary>Font scale relative to the 100 mm reference label.</summary>
    public float Scale => Math.Clamp(WidthMm / 100f, 0.6f, 2f);
}

/// <summary>Shortens long user text so a label never overflows.</summary>
internal static class LabelText
{
    public static string Clip(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim().Replace('\n', ' ').Replace('\r', ' ');
        return text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
    }

    public static string Weight(decimal kg) => string.Create(CultureInfo.InvariantCulture, $"{kg:0.###} كغ");
}
```

### 6.22 `src/ShippingERP.Infrastructure/Shipments/Labels/PdfLabelRenderer.cs`

```csharp
using System.Globalization;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ShippingERP.Infrastructure.Shipments.Labels;

/// <summary>
/// Thermal shipping labels as PDF (QuestPDF). One page per package, page size = configured label size
/// (default 100 x 150 mm), right-to-left layout with the embedded Cairo font (Arabic + Latin), so the output is
/// identical on Windows, Linux and Docker images without installed fonts.
///
/// QuestPDF license: the Community license is free for companies with less than USD 1M annual revenue;
/// larger companies need a commercial license (https://www.questpdf.com/license/).
/// </summary>
public sealed class PdfLabelRenderer
{
    public const string FontFamily = "Cairo";

    private static readonly object InitLock = new();
    private static bool _initialized;

    public PdfLabelRenderer()
    {
        EnsureInitialized();
    }

    public byte[] Render(IReadOnlyList<ShipmentLabelData> labels, LabelOptions options)
    {
        var scale = options.Scale;

        var document = Document.Create(container =>
        {
            foreach (var label in labels)
            {
                var barcode = BarcodeGenerator.Code128Svg(label.TrackingNumber);
                var qr = BarcodeGenerator.QrSvg(options.QrContent(label.TrackingNumber));

                foreach (var package in label.Packages)
                {
                    container.Page(page =>
                    {
                        page.Size(options.WidthMm, options.HeightMm, Unit.Millimetre);
                        page.Margin(3, Unit.Millimetre);
                        page.ContentFromRightToLeft();
                        page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(8 * scale).LineHeight(1.1f));
                        page.Content().ScaleToFit().Column(column => Compose(column, label, package, barcode, qr, options, scale));
                    });
                }
            }
        });

        return document.GeneratePdf();
    }

    private static void Compose(
        ColumnDescriptor column,
        ShipmentLabelData label,
        ShipmentLabelPackage package,
        string barcodeSvg,
        string qrSvg,
        LabelOptions options,
        float scale)
    {
        column.Spacing(1.5f, Unit.Millimetre);

        // Header: company + service type.
        column.Item().Row(row =>
        {
            row.RelativeItem().AlignMiddle().Text(LabelText.Clip(label.CompanyName, 40)).Bold().FontSize(9 * scale);
            row.AutoItem().Border(0.8f).PaddingHorizontal(2, Unit.Millimetre).AlignMiddle()
                .Text(LabelText.Clip(label.ServiceTypeName, 20)).Bold().FontSize(9 * scale);
        });

        // Tracking barcode + number.
        column.Item().Height(options.HeightMm * 0.11f, Unit.Millimetre).Svg(barcodeSvg).FitArea();
        column.Item().AlignCenter().Text(label.TrackingNumber).Bold().FontSize(13 * scale).LetterSpacing(0.08f);

        column.Item().Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span("رقم الشحنة: ");
                text.Span(label.ShipmentNumber).Bold();
            });
            row.AutoItem().Text(label.CreatedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        });

        // Recipient (the most important block) + destination branch code.
        column.Item().Border(1).Padding(1.5f, Unit.Millimetre).Row(row =>
        {
            row.RelativeItem().Column(recipient =>
            {
                recipient.Item().Text("إلى (المستلم)").FontSize(7 * scale);
                recipient.Item().Text(LabelText.Clip(label.RecipientName, 45)).Bold().FontSize(11 * scale);
                recipient.Item().Text(label.RecipientPhone).FontSize(10 * scale);
                if (!string.IsNullOrWhiteSpace(label.RecipientAddressLine))
                {
                    recipient.Item().Text(LabelText.Clip(label.RecipientAddressLine, 90));
                }

                if (!string.IsNullOrWhiteSpace(label.RecipientLandmark))
                {
                    recipient.Item().Text("قرب: " + LabelText.Clip(label.RecipientLandmark, 50)).FontSize(7 * scale);
                }

                recipient.Item().Text(LabelText.Clip(label.DestinationText, 60)).Bold().FontSize(10 * scale);
            });

            row.ConstantItem(options.WidthMm * 0.22f, Unit.Millimetre).Background(Colors.Black).AlignCenter().AlignMiddle()
                .Text(label.DestinationBranchCode ?? "--").FontColor(Colors.White).Bold().FontSize(16 * scale);
        });

        // Sender.
        column.Item().Border(0.5f).Padding(1.5f, Unit.Millimetre).Column(sender =>
        {
            sender.Item().Text("من (المرسل)").FontSize(7 * scale);
            sender.Item().Text(text =>
            {
                text.Span(LabelText.Clip(label.SenderName, 40)).SemiBold();
                text.Span("  " + label.SenderPhone);
            });
            sender.Item().Text($"{label.OriginCityName} - {label.OriginBranchCode}").FontSize(7 * scale);
        });

        // Package and weight.
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span($"الطرد {package.SequenceNumber} من {label.PackageCount}").Bold();
                text.Span("   " + package.PackageBarcode).FontSize(7 * scale);
            });
            row.AutoItem().Text("الوزن المحتسب: " + LabelText.Weight(label.ChargeableWeight));
        });

        // COD (inverted, very visible) or "no collection".
        if (label.HasCollection)
        {
            column.Item().Background(Colors.Black).Padding(1.5f, Unit.Millimetre).Column(cod =>
            {
                cod.Item().AlignCenter().Text(label.CollectTitle).FontColor(Colors.White).Bold().FontSize(10 * scale);
                cod.Item().AlignCenter().Text(label.CollectText).FontColor(Colors.White).Bold().FontSize(14 * scale);
            });
        }
        else
        {
            column.Item().Border(0.5f).Padding(1, Unit.Millimetre).AlignCenter()
                .Text("لا يوجد مبلغ للتحصيل").FontSize(8 * scale);
        }

        // QR + content / notes.
        column.Item().Row(row =>
        {
            row.ConstantItem(options.HeightMm * 0.13f, Unit.Millimetre)
                .Height(options.HeightMm * 0.13f, Unit.Millimetre)
                .Svg(qrSvg).FitArea();

            row.RelativeItem().PaddingRight(2, Unit.Millimetre).Column(notes =>
            {
                notes.Item().Text("المحتوى: " + LabelText.Clip(label.PackageDescription, 60)).FontSize(7 * scale);
                if (!string.IsNullOrWhiteSpace(label.SenderNotes))
                {
                    notes.Item().Text("ملاحظات: " + LabelText.Clip(label.SenderNotes, 100)).FontSize(7 * scale);
                }
            });
        });
    }

    /// <summary>License + embedded fonts, once per process (also used by the invoice PDF).</summary>
    internal static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;

            // User text may contain characters the font does not have (emoji...): print a box instead of failing.
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

            var assembly = typeof(PdfLabelRenderer).Assembly;
            foreach (var resourceName in assembly.GetManifestResourceNames().Where(x => x.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is not null)
                {
                    FontManager.RegisterFont(stream);
                }
            }

            _initialized = true;
        }
    }
}
```

### 6.23 `src/ShippingERP.Infrastructure/Shipments/Labels/ShipmentLabelService.cs`

```csharp
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments.Labels;

/// <summary>Builds shipment labels (PDF or HTML) for visible shipments. Cancelled shipments get no label.</summary>
public sealed class ShipmentLabelService : IShipmentLabelService
{
    public const string PdfContentType = "application/pdf";
    public const string HtmlContentType = "text/html; charset=utf-8";

    private readonly AppDbContext _dbContext;
    private readonly ShipmentAccessService _access;
    private readonly ShippingSettingsService _settings;
    private readonly PdfLabelRenderer _pdfRenderer;
    private readonly HtmlLabelRenderer _htmlRenderer;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ShipmentLabelService(
        AppDbContext dbContext,
        ShipmentAccessService access,
        ShippingSettingsService settings,
        PdfLabelRenderer pdfRenderer,
        HtmlLabelRenderer htmlRenderer,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _access = access;
        _settings = settings;
        _pdfRenderer = pdfRenderer;
        _htmlRenderer = htmlRenderer;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<FileDownload> GenerateAsync(long shipmentId, LabelFormat format, CancellationToken cancellationToken)
    {
        var labels = await LoadAsync(new[] { shipmentId }, cancellationToken);
        return await RenderAsync(labels, format, $"label-{labels[0].ShipmentNumber}", cancellationToken);
    }

    public async Task<FileDownload> GenerateBulkAsync(BulkLabelRequest request, CancellationToken cancellationToken)
    {
        var labels = await LoadAsync(request.ShipmentIds.Distinct().ToList(), cancellationToken);
        var name = string.Create(CultureInfo.InvariantCulture, $"labels-{_dateTimeProvider.UtcNow:yyyyMMdd-HHmm}");
        return await RenderAsync(labels, request.Format, name, cancellationToken);
    }

    private async Task<FileDownload> RenderAsync(IReadOnlyList<ShipmentLabelData> labels, LabelFormat format, string fileName, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var options = new LabelOptions(settings.LabelWidthMm, settings.LabelHeightMm, settings.TrackingUrlTemplate);

        if (format == LabelFormat.Html)
        {
            var html = _htmlRenderer.Render(labels, options);
            return FileDownload.FromBytes(Encoding.UTF8.GetBytes(html), HtmlContentType, fileName + ".html");
        }

        var pdf = _pdfRenderer.Render(labels, options);
        return FileDownload.FromBytes(pdf, PdfContentType, fileName + ".pdf");
    }

    private async Task<IReadOnlyList<ShipmentLabelData>> LoadAsync(IReadOnlyCollection<long> shipmentIds, CancellationToken cancellationToken)
    {
        var companyName = await _dbContext.CompanyProfiles.AsNoTracking()
                              .OrderBy(x => x.Id)
                              .Select(x => x.ArabicName)
                              .FirstOrDefaultAsync(cancellationToken)
                          ?? "ShippingERP";

        var query = await _access.ScopeAsync(_dbContext.Shipments.AsNoTracking(), cancellationToken);
        var rows = await query
            .Where(x => shipmentIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Status,
                Label = new ShipmentLabelData
                {
                    CompanyName = companyName,
                    ShipmentNumber = x.ShipmentNumber,
                    TrackingNumber = x.TrackingNumber,
                    CreatedAtUtc = x.CreatedAtUtc,
                    ServiceTypeName = x.ServiceType.ArabicName,
                    OriginBranchCode = x.OriginBranch.Code,
                    DestinationBranchCode = x.DestinationBranch != null ? x.DestinationBranch.Code : null,
                    DestinationBranchName = x.DestinationBranch != null ? x.DestinationBranch.ArabicName : null,
                    SenderName = x.SenderCustomer.ArabicName,
                    SenderPhone = x.SenderCustomer.Phone,
                    OriginCityName = x.OriginCity.ArabicName,
                    RecipientName = x.RecipientAddress != null && x.RecipientAddress.ContactName != null
                        ? x.RecipientAddress.ContactName
                        : x.RecipientCustomer.ArabicName,
                    RecipientPhone = x.RecipientAddress != null && x.RecipientAddress.Phone != null
                        ? x.RecipientAddress.Phone
                        : x.RecipientCustomer.Phone,
                    RecipientAddressLine = x.RecipientAddress != null ? x.RecipientAddress.AddressLine : null,
                    RecipientLandmark = x.RecipientAddress != null ? x.RecipientAddress.Landmark : null,
                    DestinationCityName = x.DestinationCity.ArabicName,
                    DestinationZoneName = x.DestinationZone != null ? x.DestinationZone.ArabicName : null,
                    PackageCount = x.PackageCount,
                    ChargeableWeight = x.ChargeableWeight,
                    IsCod = x.IsCOD,
                    CodAmount = x.CODAmount,
                    CurrencyCode = x.Currency.Code,
                    ChargesOnRecipient = x.ChargesPaidBy == ChargesPayer.Recipient,
                    ChargesAmount = x.TotalCharge,
                    PackageDescription = x.PackageDescription,
                    SenderNotes = x.SenderNotes,
                    Packages = x.Packages
                        .OrderBy(p => p.SequenceNumber)
                        .Select(p => new ShipmentLabelPackage(p.SequenceNumber, p.PackageBarcode, p.WeightKg))
                        .ToList()
                }
            })
            .ToListAsync(cancellationToken);

        var missing = shipmentIds.FirstOrDefault(id => rows.All(r => r.Id != id));
        if (missing != 0)
        {
            throw new NotFoundException("الشحنة", missing);
        }

        var cancelled = rows.FirstOrDefault(r => r.Status == ShipmentStatus.Cancelled);
        if (cancelled is not null)
        {
            throw new BusinessRuleException($"الشحنة {cancelled.Label.ShipmentNumber} ملغاة؛ لا يمكن طباعة ملصق لها.");
        }

        // Keep the order requested by the caller.
        return shipmentIds.Select(id => rows.First(r => r.Id == id).Label).ToList();
    }
}
```

### 6.24 `src/ShippingERP.Infrastructure/Shipments/ShipmentBuilder.cs`

```csharp
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Entities.Settings;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>A validated shipment request with every reference resolved and the price calculated.</summary>
public sealed class ShipmentDraft
{
    public long OriginBranchId { get; init; }
    public long SenderCustomerId { get; init; }
    public long RecipientCustomerId { get; init; }
    public long? SenderAddressId { get; init; }
    public long? RecipientAddressId { get; init; }
    public long? DestinationBranchId { get; init; }
    public long OriginCityId { get; init; }
    public long DestinationCityId { get; init; }
    public long? OriginZoneId { get; init; }
    public long? DestinationZoneId { get; init; }
    public long ServiceTypeId { get; init; }
    public string PackageDescription { get; init; } = string.Empty;
    public IReadOnlyList<PackageDraft> Packages { get; init; } = Array.Empty<PackageDraft>();
    public decimal ActualWeight { get; init; }
    public decimal VolumetricWeight { get; init; }
    public decimal ChargeableWeight { get; init; }
    public decimal TotalVolumeCbm { get; init; }
    public PricingBasis PricingBasis { get; init; } = PricingBasis.Weight;
    public ChargesPayer ChargesPaidBy { get; init; } = ChargesPayer.Sender;
    public decimal DeclaredValue { get; init; }
    public decimal InsuranceValue { get; init; }
    public bool IsCod { get; init; }
    public decimal CodAmount { get; init; }
    public bool RequiresPickup { get; init; }
    public bool RequiresPackaging { get; init; }
    public string? SenderNotes { get; init; }
    public string? InternalNotes { get; init; }
    public string? ExternalReference { get; init; }
    public PricingResult Pricing { get; init; } = null!;

    /// <summary>Reason of a manual charge / discount (stored in the pricing snapshot).</summary>
    public string? ManualPriceReason { get; init; }
}

public sealed record PackageDraft(
    int SequenceNumber,
    string? Description,
    decimal WeightKg,
    decimal? LengthCm,
    decimal? WidthCm,
    decimal? HeightCm,
    decimal VolumetricWeightKg);

/// <summary>
/// Turns a SaveShipmentRequest into a validated <see cref="ShipmentDraft"/> and applies it to a Shipment.
/// Used by create, update, quote and the Excel import, so every path follows exactly the same rules.
///
/// Reference data (customers, addresses, branches, cities, zones, service types) is cached for the lifetime of
/// the scope, so importing hundreds of rows does not repeat the same queries.
/// </summary>
public sealed class ShipmentBuilder
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IBranchAccessService _branchAccess;
    private readonly ShipmentPricingCalculator _pricing;
    private readonly DocumentNumberGenerator _numbers;
    private readonly TrackingNumberGenerator _trackingNumbers;
    private readonly ShipmentStatusChanger _statusChanger;

    private readonly Dictionary<long, BranchInfo?> _branches = new();
    private readonly Dictionary<long, CustomerInfo?> _customers = new();
    private readonly Dictionary<long, AddressInfo?> _addresses = new();
    private readonly Dictionary<long, AddressInfo?> _defaultAddresses = new();
    private readonly Dictionary<long, bool?> _cities = new();
    private readonly Dictionary<long, ZoneInfo?> _zones = new();
    private readonly Dictionary<long, bool?> _serviceTypes = new();
    private readonly Dictionary<long, long?> _singleBranchByCity = new();

    public ShipmentBuilder(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IBranchAccessService branchAccess,
        ShipmentPricingCalculator pricing,
        DocumentNumberGenerator numbers,
        TrackingNumberGenerator trackingNumbers,
        ShipmentStatusChanger statusChanger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _branchAccess = branchAccess;
        _pricing = pricing;
        _numbers = numbers;
        _trackingNumbers = trackingNumbers;
        _statusChanger = statusChanger;
    }

    /// <summary>
    /// Validates the references and business rules and calculates the price.
    /// Throws RequestValidationException (400), ForbiddenException (403) or BusinessRuleException (422).
    /// </summary>
    /// <param name="existing">The shipment being updated, or null for a new one.</param>
    public async Task<ShipmentDraft> PrepareAsync(
        SaveShipmentRequest request,
        Shipment? existing,
        ShippingSettings settings,
        CancellationToken cancellationToken)
    {
        var errors = new ErrorBag();

        // ---------------- origin branch (fixed after creation)
        var originBranchId = existing?.OriginBranchId ?? request.OriginBranchId;
        if (existing is null)
        {
            _branchAccess.EnsureCanAccess(originBranchId);
        }

        var originBranch = await GetBranchAsync(originBranchId, cancellationToken);
        if (originBranch is null)
        {
            errors.Add("originBranchId", "الفرع غير موجود.");
        }
        else if (existing is null && !originBranch.IsActive)
        {
            errors.Add("originBranchId", "الفرع غير مفعّل.");
        }

        // ---------------- customers
        var sender = await GetCustomerAsync(request.SenderCustomerId, cancellationToken);
        if (sender is null)
        {
            errors.Add("senderCustomerId", "المرسل غير موجود.");
        }
        else
        {
            if (!sender.IsActive && request.SenderCustomerId != existing?.SenderCustomerId)
            {
                errors.Add("senderCustomerId", "المرسل غير مفعّل.");
            }

            if ((sender.Roles & CustomerRole.Sender) != CustomerRole.Sender)
            {
                errors.Add("senderCustomerId", "العميل المحدد غير مسجّل كمرسل (دوره مستلم فقط).");
            }
        }

        var recipient = await GetCustomerAsync(request.RecipientCustomerId, cancellationToken);
        if (recipient is null)
        {
            errors.Add("recipientCustomerId", "المستلم غير موجود.");
        }
        else
        {
            if (!recipient.IsActive && request.RecipientCustomerId != existing?.RecipientCustomerId)
            {
                errors.Add("recipientCustomerId", "المستلم غير مفعّل.");
            }

            if ((recipient.Roles & CustomerRole.Recipient) != CustomerRole.Recipient)
            {
                errors.Add("recipientCustomerId", "العميل المحدد غير مسجّل كمستلم (دوره مرسل فقط).");
            }
        }

        // ---------------- addresses (default address when not given)
        var senderAddress = await ResolveAddressAsync(
            request.SenderAddressId, request.SenderCustomerId, existing?.SenderAddressId, "senderAddressId", "عنوان المرسل", errors, cancellationToken);
        var recipientAddress = await ResolveAddressAsync(
            request.RecipientAddressId, request.RecipientCustomerId, existing?.RecipientAddressId, "recipientAddressId", "عنوان المستلم", errors, cancellationToken);

        // ---------------- cities and zone
        var originCityId = request.OriginCityId ?? senderAddress?.CityId ?? originBranch?.CityId;
        if (originCityId is null)
        {
            errors.Add("originCityId", "حدد مدينة الانطلاق.");
        }
        else
        {
            await CheckCityAsync(originCityId.Value, existing?.OriginCityId, "originCityId", errors, cancellationToken);
        }

        var destinationCityId = request.DestinationCityId ?? recipientAddress?.CityId;
        if (destinationCityId is null)
        {
            errors.Add("destinationCityId", "حدد مدينة الوجهة، أو أضف عنواناً للمستلم.");
        }
        else
        {
            await CheckCityAsync(destinationCityId.Value, existing?.DestinationCityId, "destinationCityId", errors, cancellationToken);

            if (request.RecipientAddressId.HasValue && recipientAddress is not null && recipientAddress.CityId != destinationCityId)
            {
                errors.Add("recipientAddressId", "عنوان المستلم في مدينة غير مدينة الوجهة.");
            }
        }

        var destinationZoneId = request.DestinationZoneId ??
                                (recipientAddress is not null && recipientAddress.CityId == destinationCityId ? recipientAddress.ZoneId : null);
        if (destinationZoneId.HasValue)
        {
            var zone = await GetZoneAsync(destinationZoneId.Value, cancellationToken);
            if (zone is null)
            {
                errors.Add("destinationZoneId", "المنطقة غير موجودة.");
            }
            else
            {
                if (zone.CityId != destinationCityId)
                {
                    errors.Add("destinationZoneId", "المنطقة لا تتبع مدينة الوجهة.");
                }

                if (!zone.IsActive && destinationZoneId != existing?.DestinationZoneId)
                {
                    errors.Add("destinationZoneId", "المنطقة غير مفعّلة.");
                }
            }
        }

        // ---------------- destination branch (automatic when the city has exactly one branch)
        var destinationBranchId = request.DestinationBranchId;
        if (destinationBranchId.HasValue)
        {
            var destinationBranch = await GetBranchAsync(destinationBranchId.Value, cancellationToken);
            if (destinationBranch is null)
            {
                errors.Add("destinationBranchId", "فرع الوجهة غير موجود.");
            }
            else if (!destinationBranch.IsActive && destinationBranchId != existing?.DestinationBranchId)
            {
                errors.Add("destinationBranchId", "فرع الوجهة غير مفعّل.");
            }
        }
        else if (destinationCityId.HasValue)
        {
            destinationBranchId = await GetSingleBranchOfCityAsync(destinationCityId.Value, cancellationToken);
        }

        // ---------------- service type
        if (!_serviceTypes.TryGetValue(request.ServiceTypeId, out var serviceTypeActive))
        {
            var serviceTypeId = request.ServiceTypeId;
            serviceTypeActive = await _dbContext.ServiceTypes.AsNoTracking()
                .Where(x => x.Id == serviceTypeId)
                .Select(x => (bool?)x.IsActive)
                .FirstOrDefaultAsync(cancellationToken);
            _serviceTypes[request.ServiceTypeId] = serviceTypeActive;
        }

        if (serviceTypeActive is null)
        {
            errors.Add("serviceTypeId", "نوع الخدمة غير موجود.");
        }
        else if (serviceTypeActive == false && request.ServiceTypeId != existing?.ServiceTypeId)
        {
            errors.Add("serviceTypeId", "نوع الخدمة غير مفعّل.");
        }

        // ---------------- external reference (unique per sender)
        var externalReference = Optional(request.ExternalReference);
        if (externalReference is not null)
        {
            var senderId = request.SenderCustomerId;
            var existingId = existing?.Id ?? 0;
            if (await _dbContext.Shipments.AnyAsync(
                    x => x.SenderCustomerId == senderId && x.ExternalReference == externalReference && x.Id != existingId,
                    cancellationToken))
            {
                errors.Add("externalReference", $"المرجع '{externalReference}' مستخدم في شحنة أخرى لنفس المرسل.");
            }
        }

        errors.ThrowIfAny();

        // ---------------- price override permission
        var changesPrice = await EnsureCanOverridePriceAsync(request, existing, cancellationToken);

        // ---------------- packages and weights
        var divisor = settings.VolumetricDivisor;
        var packages = request.Packages
            .Select((package, index) => new PackageDraft(
                index + 1,
                Optional(package.Description),
                package.WeightKg,
                package.LengthCm,
                package.WidthCm,
                package.HeightCm,
                ShipmentWeightCalculator.VolumetricWeight(package.LengthCm, package.WidthCm, package.HeightCm, divisor)))
            .ToList();

        var actualWeight = Math.Round(packages.Sum(x => x.WeightKg), ShipmentWeightCalculator.WeightDecimals);
        var volumetricWeight = Math.Round(packages.Sum(x => x.VolumetricWeightKg), ShipmentWeightCalculator.WeightDecimals);
        var chargeableWeight = ShipmentWeightCalculator.ChargeableWeight(actualWeight, volumetricWeight, settings.WeightRoundingStepKg);

        // Step 9: the volume (m³) is the entered total, or the sum of the package dimensions when it is not entered.
        var volumeFromPackages = request.Packages.All(p => p.LengthCm.HasValue && p.WidthCm.HasValue && p.HeightCm.HasValue)
            ? request.Packages.Sum(p => p.LengthCm!.Value * p.WidthCm!.Value * p.HeightCm!.Value) / 1_000_000m
            : 0m;
        var totalVolumeCbm = Math.Round(request.TotalVolumeCbm > 0m ? request.TotalVolumeCbm : volumeFromPackages,
            ShipmentWeightCalculator.WeightDecimals, MidpointRounding.AwayFromZero);
        if (request.PricingBasis == PricingBasis.Volume && totalVolumeCbm <= 0m)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["totalVolumeCbm"] = new[] { "الحجم بالمتر المكعب مطلوب عند التسعير بالحجم." }
            });
        }

        var codAmount = request.IsCOD ? request.CODAmount : 0m;
        var originZoneId = senderAddress is not null && senderAddress.CityId == originCityId ? senderAddress.ZoneId : null;

        var pricing = await _pricing.CalculateAsync(
            new PricingInput
            {
                SenderCustomerId = sender!.Id,
                CustomerPricingPlanId = sender.PricingPlanId,
                OriginZoneId = originZoneId,
                DestinationZoneId = destinationZoneId,
                OriginCityId = originCityId,
                DestinationCityId = destinationCityId,
                ServiceTypeId = request.ServiceTypeId,
                ActualWeight = actualWeight,
                VolumetricWeight = volumetricWeight,
                ChargeableWeight = chargeableWeight,
                PackageCount = packages.Count,
                PricingBasis = request.PricingBasis,
                VolumeCbm = totalVolumeCbm,
                IsCod = request.IsCOD,
                CodAmount = codAmount,
                InsuranceValue = request.InsuranceValue,
                RequiresPickup = request.RequiresPickup,
                RequiresPackaging = request.RequiresPackaging,
                ManualShippingCharge = request.ManualShippingCharge,
                ManualDiscountAmount = request.DiscountAmount
            },
            settings,
            cancellationToken);

        // ---------------- big manual changes need an approver
        if (changesPrice && _currentUser.IsAuthenticated)
        {
            var evaluation = PriceOverrideEvaluation.From(pricing, settings.PriceOverrideApprovalThresholdPercent);
            if (evaluation.RequiresApproval && !_currentUser.HasPermission(Permissions.Pricing.ApproveOverride))
            {
                var size = evaluation.DeviationPercentage.HasValue
                    ? $"يغيّر السعر بنسبة {evaluation.DeviationPercentage.Value.ToString("0.##", CultureInfo.InvariantCulture)}%"
                    : "لا توجد قاعدة تسعير للمقارنة";
                throw new BusinessRuleException(
                    $"السعر اليدوي {size}، وهذا أكبر من الحد المسموح ({settings.PriceOverrideApprovalThresholdPercent.ToString("0.##", CultureInfo.InvariantCulture)}%) " +
                    "ويحتاج موافقة مستخدم يملك صلاحية Pricing.ApproveOverride. احفظ الشحنة بسعر القواعد ثم اطلب التعديل عبر " +
                    "POST /api/v1/shipments/{id}/apply-price ليُعتمد لاحقاً.");
            }
        }

        return new ShipmentDraft
        {
            OriginBranchId = originBranchId,
            SenderCustomerId = request.SenderCustomerId,
            RecipientCustomerId = request.RecipientCustomerId,
            SenderAddressId = senderAddress?.Id,
            RecipientAddressId = recipientAddress?.Id,
            DestinationBranchId = destinationBranchId,
            OriginCityId = originCityId!.Value,
            DestinationCityId = destinationCityId!.Value,
            OriginZoneId = originZoneId,
            DestinationZoneId = destinationZoneId,
            ServiceTypeId = request.ServiceTypeId,
            PackageDescription = request.PackageDescription.Trim(),
            Packages = packages,
            ActualWeight = actualWeight,
            VolumetricWeight = volumetricWeight,
            ChargeableWeight = chargeableWeight,
            TotalVolumeCbm = totalVolumeCbm,
            PricingBasis = request.PricingBasis,
            ChargesPaidBy = request.ChargesPaidBy,
            DeclaredValue = request.DeclaredValue,
            InsuranceValue = request.InsuranceValue,
            IsCod = request.IsCOD,
            CodAmount = codAmount,
            RequiresPickup = request.RequiresPickup,
            RequiresPackaging = request.RequiresPackaging,
            SenderNotes = Optional(request.SenderNotes),
            InternalNotes = Optional(request.InternalNotes),
            ExternalReference = externalReference,
            Pricing = pricing,
            ManualPriceReason = pricing.Input.IsManual ? Optional(request.ManualPriceReason) : null
        };
    }

    /// <summary>
    /// Creates a new shipment from a draft (numbers, packages, first history row, pricing snapshot) and adds it to
    /// the DbContext. The caller saves inside its transaction.
    /// </summary>
    public async Task<Shipment> AddNewAsync(
        ShipmentDraft draft,
        ShippingSettings settings,
        ShipmentStatusChangeSource source,
        string pricingReason,
        ISet<string>? reservedTrackingNumbers,
        CancellationToken cancellationToken)
    {
        // Numbers first: the generic sequence path may call SaveChanges.
        var shipmentNumber = await _numbers.NextAsync(DocumentPrefixes.Shipment, cancellationToken);
        var trackingNumber = await _trackingNumbers.NextAsync(settings.TrackingNumberPrefix, reservedTrackingNumbers, cancellationToken);

        var shipment = new Shipment
        {
            ShipmentNumber = shipmentNumber,
            TrackingNumber = trackingNumber,
            Status = ShipmentStatus.Created,
            OriginBranchId = draft.OriginBranchId,
            CurrentBranchId = draft.OriginBranchId
        };

        Apply(shipment, draft);

        _dbContext.Shipments.Add(shipment);
        _statusChanger.RecordCreation(shipment, source);
        _dbContext.ShipmentPricingSnapshots.Add(draft.Pricing.ToSnapshot(shipment, pricingReason, draft.ManualPriceReason));

        return shipment;
    }

    /// <summary>Copies the draft into the shipment. Packages are updated in place (same barcodes), added or removed.</summary>
    public void Apply(Shipment shipment, ShipmentDraft draft)
    {
        shipment.SenderCustomerId = draft.SenderCustomerId;
        shipment.RecipientCustomerId = draft.RecipientCustomerId;
        shipment.SenderAddressId = draft.SenderAddressId;
        shipment.RecipientAddressId = draft.RecipientAddressId;
        shipment.DestinationBranchId = draft.DestinationBranchId;
        shipment.OriginCityId = draft.OriginCityId;
        shipment.DestinationCityId = draft.DestinationCityId;
        shipment.OriginZoneId = draft.OriginZoneId;
        shipment.DestinationZoneId = draft.DestinationZoneId;
        shipment.ServiceTypeId = draft.ServiceTypeId;
        shipment.PackageDescription = draft.PackageDescription;
        shipment.PackageCount = draft.Packages.Count;
        shipment.ActualWeight = draft.ActualWeight;
        shipment.VolumetricWeight = draft.VolumetricWeight;
        shipment.ChargeableWeight = draft.ChargeableWeight;
        shipment.TotalVolumeCbm = draft.TotalVolumeCbm;
        shipment.PricingBasis = draft.PricingBasis;
        shipment.ChargesPaidBy = draft.ChargesPaidBy;
        shipment.DeclaredValue = draft.DeclaredValue;
        shipment.InsuranceValue = draft.InsuranceValue;
        shipment.IsCOD = draft.IsCod;
        shipment.CODAmount = draft.CodAmount;
        shipment.RequiresPickup = draft.RequiresPickup;
        shipment.RequiresPackaging = draft.RequiresPackaging;
        shipment.SenderNotes = draft.SenderNotes;
        shipment.InternalNotes = draft.InternalNotes;
        shipment.ExternalReference = draft.ExternalReference;
        shipment.CurrencyId = draft.Pricing.CurrencyId;
        shipment.ShippingCharge = draft.Pricing.ShippingCharge;
        shipment.DiscountAmount = draft.Pricing.DiscountAmount;
        shipment.TaxAmount = draft.Pricing.TaxAmount;
        shipment.TotalCharge = draft.Pricing.TotalCharge;

        var current = shipment.Packages.OrderBy(x => x.SequenceNumber).ToList();

        foreach (var packageDraft in draft.Packages)
        {
            var index = packageDraft.SequenceNumber - 1;
            ShipmentPackage package;

            if (index < current.Count)
            {
                package = current[index];
            }
            else
            {
                package = new ShipmentPackage { SequenceNumber = packageDraft.SequenceNumber };
                shipment.Packages.Add(package);
            }

            package.PackageBarcode = PackageBarcode(shipment.TrackingNumber, packageDraft.SequenceNumber);
            package.Description = packageDraft.Description;
            package.WeightKg = packageDraft.WeightKg;
            package.LengthCm = packageDraft.LengthCm;
            package.WidthCm = packageDraft.WidthCm;
            package.HeightCm = packageDraft.HeightCm;
            package.VolumetricWeightKg = packageDraft.VolumetricWeightKg;
        }

        foreach (var removed in current.Skip(draft.Packages.Count))
        {
            shipment.Packages.Remove(removed);
            _dbContext.ShipmentPackages.Remove(removed);
        }
    }

    public static string PackageBarcode(string trackingNumber, int sequenceNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"{trackingNumber}-{sequenceNumber:D3}");

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// A manual charge or a manual discount (new, or different from the shipment's current manual values) needs
    /// Shipments.OverridePrice. Returns true when the request changes the manual values.
    /// </summary>
    private async Task<bool> EnsureCanOverridePriceAsync(SaveShipmentRequest request, Shipment? existing, CancellationToken cancellationToken)
    {
        bool changesPrice;

        if (existing is null)
        {
            changesPrice = request.ManualShippingCharge.HasValue || request.DiscountAmount > 0m;
        }
        else
        {
            var existingId = existing.Id;
            var last = await _dbContext.ShipmentPricingSnapshots.AsNoTracking()
                .Where(x => x.ShipmentId == existingId)
                .OrderByDescending(x => x.Id)
                .Select(x => new { x.PricingSource, x.ShippingSubtotal, x.ManualDiscountAmount })
                .FirstOrDefaultAsync(cancellationToken);
            decimal? existingManual = last?.PricingSource == PricingSource.Manual ? last.ShippingSubtotal : null;
            var existingDiscount = last?.ManualDiscountAmount ?? 0m;

            changesPrice = request.ManualShippingCharge != existingManual || request.DiscountAmount != existingDiscount;
        }

        if (changesPrice && !_currentUser.HasPermission(Permissions.Shipments.OverridePrice) && _currentUser.IsAuthenticated)
        {
            throw new ForbiddenException("إدخال سعر يدوي أو خصم يحتاج صلاحية Shipments.OverridePrice.");
        }

        return changesPrice;
    }

    private async Task<AddressInfo?> ResolveAddressAsync(
        long? addressId,
        long customerId,
        long? currentAddressId,
        string fieldName,
        string displayName,
        ErrorBag errors,
        CancellationToken cancellationToken)
    {
        if (addressId is null)
        {
            if (!_defaultAddresses.TryGetValue(customerId, out var defaultAddress))
            {
                defaultAddress = await _dbContext.CustomerAddresses.AsNoTracking()
                    .Where(x => x.CustomerId == customerId && x.IsActive)
                    .OrderByDescending(x => x.IsDefault)
                    .ThenBy(x => x.Id)
                    .Select(x => new AddressInfo(x.Id, x.CustomerId, x.CityId, x.ZoneId, x.IsActive))
                    .FirstOrDefaultAsync(cancellationToken);
                _defaultAddresses[customerId] = defaultAddress;
            }

            return defaultAddress;
        }

        if (!_addresses.TryGetValue(addressId.Value, out var address))
        {
            var id = addressId.Value;
            address = await _dbContext.CustomerAddresses.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new AddressInfo(x.Id, x.CustomerId, x.CityId, x.ZoneId, x.IsActive))
                .FirstOrDefaultAsync(cancellationToken);
            _addresses[addressId.Value] = address;
        }

        if (address is null || address.CustomerId != customerId)
        {
            errors.Add(fieldName, $"{displayName} غير موجود أو لا يخص هذا العميل.");
            return null;
        }

        if (!address.IsActive && addressId != currentAddressId)
        {
            errors.Add(fieldName, $"{displayName} غير مفعّل.");
        }

        return address;
    }

    private async Task CheckCityAsync(long cityId, long? currentCityId, string fieldName, ErrorBag errors, CancellationToken cancellationToken)
    {
        if (!_cities.TryGetValue(cityId, out var isActive))
        {
            isActive = await _dbContext.Cities.AsNoTracking()
                .Where(x => x.Id == cityId)
                .Select(x => (bool?)x.IsActive)
                .FirstOrDefaultAsync(cancellationToken);
            _cities[cityId] = isActive;
        }

        if (isActive is null)
        {
            errors.Add(fieldName, "المدينة غير موجودة.");
        }
        else if (isActive == false && cityId != currentCityId)
        {
            errors.Add(fieldName, "المدينة غير مفعّلة.");
        }
    }

    private async Task<BranchInfo?> GetBranchAsync(long branchId, CancellationToken cancellationToken)
    {
        if (!_branches.TryGetValue(branchId, out var branch))
        {
            branch = await _dbContext.Branches.AsNoTracking()
                .Where(x => x.Id == branchId)
                .Select(x => new BranchInfo(x.Id, x.CityId, x.IsActive))
                .FirstOrDefaultAsync(cancellationToken);
            _branches[branchId] = branch;
        }

        return branch;
    }

    private async Task<CustomerInfo?> GetCustomerAsync(long customerId, CancellationToken cancellationToken)
    {
        if (!_customers.TryGetValue(customerId, out var customer))
        {
            customer = await _dbContext.Customers.AsNoTracking()
                .Where(x => x.Id == customerId)
                .Select(x => new CustomerInfo(x.Id, x.Roles, x.IsActive, x.PricingPlanId))
                .FirstOrDefaultAsync(cancellationToken);
            _customers[customerId] = customer;
        }

        return customer;
    }

    private async Task<ZoneInfo?> GetZoneAsync(long zoneId, CancellationToken cancellationToken)
    {
        if (!_zones.TryGetValue(zoneId, out var zone))
        {
            zone = await _dbContext.Zones.AsNoTracking()
                .Where(x => x.Id == zoneId)
                .Select(x => new ZoneInfo(x.Id, x.CityId, x.IsActive))
                .FirstOrDefaultAsync(cancellationToken);
            _zones[zoneId] = zone;
        }

        return zone;
    }

    private async Task<long?> GetSingleBranchOfCityAsync(long cityId, CancellationToken cancellationToken)
    {
        if (!_singleBranchByCity.TryGetValue(cityId, out var branchId))
        {
            var branchIds = await _dbContext.Branches.AsNoTracking()
                .Where(x => x.CityId == cityId && x.IsActive)
                .Select(x => x.Id)
                .Take(2)
                .ToListAsync(cancellationToken);
            branchId = branchIds.Count == 1 ? branchIds[0] : null;
            _singleBranchByCity[cityId] = branchId;
        }

        return branchId;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record BranchInfo(long Id, long CityId, bool IsActive);

    private sealed record CustomerInfo(long Id, CustomerRole Roles, bool IsActive, long? PricingPlanId);

    private sealed record AddressInfo(long Id, long CustomerId, long CityId, long? ZoneId, bool IsActive);

    private sealed record ZoneInfo(long Id, long CityId, bool IsActive);
}
```

### 6.25 `src/ShippingERP.Infrastructure/Shipments/ShipmentPricingCalculator.cs`

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Entities.Settings;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Pricing;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>Everything the price of a shipment depends on.</summary>
public sealed class PricingInput
{
    public long SenderCustomerId { get; init; }

    /// <summary>The plan assigned to the sender (Customer.PricingPlanId).</summary>
    public long? CustomerPricingPlanId { get; init; }

    public long? OriginZoneId { get; init; }
    public long? DestinationZoneId { get; init; }
    public long? OriginCityId { get; init; }
    public long? DestinationCityId { get; init; }
    public long ServiceTypeId { get; init; }
    public decimal ActualWeight { get; init; }
    public decimal VolumetricWeight { get; init; }
    public decimal ChargeableWeight { get; init; }
    public int PackageCount { get; init; }

    /// <summary>Step 9: by weight or by volume.</summary>
    public PricingBasis PricingBasis { get; init; } = PricingBasis.Weight;

    /// <summary>Step 9: total volume (m³, 3 decimals).</summary>
    public decimal VolumeCbm { get; init; }

    public bool IsCod { get; init; }
    public decimal CodAmount { get; init; }
    public decimal InsuranceValue { get; init; }
    public bool RequiresPickup { get; init; }
    public bool RequiresPackaging { get; init; }

    /// <summary>The shipment is being returned to the sender (return fee).</summary>
    public bool IsReturn { get; init; }

    public decimal? ManualShippingCharge { get; init; }
    public decimal ManualDiscountAmount { get; init; }

    public bool IsManual => ManualShippingCharge.HasValue || ManualDiscountAmount > 0m;
}

/// <summary>Result of a price calculation; becomes a ShipmentPricingSnapshot.</summary>
public sealed class PricingResult
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public PricingSource PricingSource { get; init; }
    public long? PricingPlanId { get; init; }
    public string? PricingPlanCode { get; init; }
    public long? PricingRuleId { get; init; }
    public string? PricingRuleName { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public int CurrencyDecimals { get; init; }
    public int VolumetricDivisor { get; init; }
    public decimal WeightRoundingStepKg { get; init; }
    public PricingInput Input { get; init; } = null!;
    public bool IsRemoteArea { get; init; }
    public long? TaxRateId { get; init; }
    public PriceBreakdown Breakdown { get; init; } = null!;

    /// <summary>The same shipment priced by the rule only (no manual values). Null when no rule matched.</summary>
    public PriceBreakdown? RuleBreakdown { get; init; }

    // Shortcuts used by the shipment.
    public decimal ShippingCharge => Breakdown.GrossCharge;
    public decimal DiscountAmount => Breakdown.DiscountAmount;
    public decimal TaxAmount => Breakdown.TaxAmount;
    public decimal TotalCharge => Breakdown.NetTotal;

    public ShipmentPricingSnapshot ToSnapshot(Shipment shipment, string reason, string? overrideReason = null, long? priceOverrideId = null)
    {
        var b = Breakdown;
        return new ShipmentPricingSnapshot
        {
            Shipment = shipment,
            Reason = reason,
            PricingSource = PricingSource,
            PricingPlanId = PricingPlanId,
            PricingPlanCode = PricingPlanCode,
            PricingRuleId = PricingRuleId,
            PricingRuleName = PricingRuleName,
            CurrencyId = CurrencyId,
            CurrencyCode = CurrencyCode,
            ActualWeight = Input.ActualWeight,
            VolumetricWeight = Input.VolumetricWeight,
            ChargeableWeight = Input.ChargeableWeight,
            VolumetricDivisor = VolumetricDivisor,
            WeightRoundingStepKg = WeightRoundingStepKg,
            PackageCount = Input.PackageCount,
            PricingBasis = b.PricingBasis,
            VolumeCbm = Input.VolumeCbm,
            PricePerCubicMeter = b.PricePerCubicMeter,
            VolumeCharge = b.VolumeCharge,
            InsuranceValue = Input.InsuranceValue,
            RequiresPickup = Input.RequiresPickup,
            RequiresPackaging = Input.RequiresPackaging,
            IsRemoteArea = IsRemoteArea,
            IsReturn = Input.IsReturn,
            BasePrice = b.BasePrice,
            IncludedWeightKg = b.IncludedWeightKg,
            AdditionalWeightKg = b.AdditionalWeightKg,
            AdditionalWeightCharge = b.WeightCharge,
            WeightTiersJson = b.WeightTiers.Count == 0 ? null : JsonSerializer.Serialize(b.WeightTiers, JsonOptions),
            PackageFee = b.PackageFee,
            InsuranceFee = b.InsuranceFee,
            PickupFee = b.PickupFee,
            PackagingFee = b.PackagingFee,
            RemoteAreaFee = b.RemoteAreaFee,
            ReturnFee = b.ReturnFee,
            ServiceFeesTotal = b.ServiceFeesTotal,
            CodFeeFixed = b.CodFeeFixed,
            CodFeePercentage = b.CodFeePercentage,
            CodFeeMinimum = b.CodFeeMinimum,
            CodAmount = b.CodAmount,
            CodFee = b.CodCommission,
            ShippingSubtotal = b.ShippingSubtotal,
            CalculatedShippingSubtotal = b.CalculatedShippingSubtotal,
            ContractDiscountPercentage = b.ContractDiscountPercentage,
            ContractDiscountAmount = b.ContractDiscountAmount,
            ManualDiscountAmount = b.ManualDiscountAmount,
            DiscountAmount = b.DiscountAmount,
            TotalShippingCharge = b.TotalShippingCharge,
            TaxableAmount = b.TaxableAmount,
            ShippingCharge = b.GrossCharge,
            TaxRateId = TaxRateId,
            TaxPercentage = b.TaxPercentage,
            TaxAmount = b.TaxAmount,
            TotalCharge = b.NetTotal,
            OverrideReason = overrideReason,
            PriceOverrideId = priceOverrideId
        };
    }

    public ShipmentPricingDto ToDto(string reason) => ToDto(Breakdown, PricingSource, reason);

    /// <summary>The rule-only breakdown as a DTO (null when no rule matched).</summary>
    public ShipmentPricingDto? ToRuleDto() =>
        RuleBreakdown is null
            ? null
            : ToDto(RuleBreakdown, PricingSource == PricingSource.Manual ? PricingSource.PricingRule : PricingSource, "Rule");

    private ShipmentPricingDto ToDto(PriceBreakdown b, PricingSource source, string reason) => new()
    {
        Reason = reason,
        PricingSource = source,
        PricingPlanId = PricingPlanId,
        PricingPlanCode = PricingPlanCode,
        PricingRuleId = PricingRuleId,
        PricingRuleName = PricingRuleName,
        CurrencyId = CurrencyId,
        CurrencyCode = CurrencyCode,
        ActualWeight = Input.ActualWeight,
        VolumetricWeight = Input.VolumetricWeight,
        ChargeableWeight = Input.ChargeableWeight,
        VolumetricDivisor = VolumetricDivisor,
        WeightRoundingStepKg = WeightRoundingStepKg,
        PackageCount = Input.PackageCount,
        PricingBasis = b.PricingBasis,
        VolumeCbm = Input.VolumeCbm,
        PricePerCubicMeter = b.PricePerCubicMeter,
        VolumeCharge = b.VolumeCharge,
        InsuranceValue = Input.InsuranceValue,
        RequiresPickup = Input.RequiresPickup,
        RequiresPackaging = Input.RequiresPackaging,
        IsRemoteArea = IsRemoteArea,
        IsReturn = Input.IsReturn,
        BaseCharge = b.BasePrice,
        IncludedWeightKg = b.IncludedWeightKg,
        AdditionalWeightKg = b.AdditionalWeightKg,
        WeightCharge = b.WeightCharge,
        WeightTiers = b.WeightTiers
            .Select(t => new PricingWeightTierDto { FromKg = t.FromKg, ToKg = t.ToKg, Kg = t.Kg, PricePerKg = t.PricePerKg, Charge = t.Charge })
            .ToList(),
        PackageFee = b.PackageFee,
        InsuranceFee = b.InsuranceFee,
        PickupFee = b.PickupFee,
        PackagingFee = b.PackagingFee,
        RemoteAreaFee = b.RemoteAreaFee,
        ReturnFee = b.ReturnFee,
        ServiceFeesTotal = b.ServiceFeesTotal,
        ShippingSubtotal = b.ShippingSubtotal,
        CalculatedShippingSubtotal = b.CalculatedShippingSubtotal,
        ContractDiscountPercentage = b.ContractDiscountPercentage,
        ContractDiscountAmount = b.ContractDiscountAmount,
        ManualDiscountAmount = b.ManualDiscountAmount,
        DiscountAmount = b.DiscountAmount,
        TotalShippingCharge = b.TotalShippingCharge,
        CodAmount = b.CodAmount,
        CodFeeFixed = b.CodFeeFixed,
        CodFeePercentage = b.CodFeePercentage,
        CodFeeMinimum = b.CodFeeMinimum,
        CodCommission = b.CodCommission,
        TaxableAmount = b.TaxableAmount,
        TaxRateId = TaxRateId,
        TaxPercentage = b.TaxPercentage,
        TaxAmount = b.TaxAmount,
        NetTotal = b.NetTotal,
        ShippingCharge = b.GrossCharge,
        TotalCharge = b.NetTotal
    };
}

/// <summary>How big a manual price change is, and whether it needs an approver.</summary>
public sealed record PriceOverrideEvaluation(bool IsManual, decimal? RuleTotalShippingCharge, decimal ProposedTotalShippingCharge, decimal? DeviationPercentage, bool RequiresApproval)
{
    /// <summary>
    /// Compares the total shipping charge (after discounts, before COD commission and tax) with and without the
    /// manual values. No rule (nothing to compare with) or a rule price of 0 always needs approval.
    /// </summary>
    public static PriceOverrideEvaluation From(PricingResult result, decimal thresholdPercent)
    {
        var proposed = result.Breakdown.TotalShippingCharge;
        if (!result.Input.IsManual)
        {
            return new PriceOverrideEvaluation(false, proposed, proposed, null, false);
        }

        var rule = result.RuleBreakdown?.TotalShippingCharge;
        decimal? deviation = rule is > 0m
            ? Math.Round(Math.Abs(proposed - rule.Value) / rule.Value * 100m, 2, MidpointRounding.AwayFromZero)
            : null;

        var requiresApproval = deviation is null || deviation.Value > thresholdPercent;
        return new PriceOverrideEvaluation(true, rule, proposed, deviation, requiresApproval);
    }
}

/// <summary>
/// The pricing engine (step 5). Loads the data, then delegates the arithmetic to the pure Domain classes
/// (PriceRuleMatcher, PriceBreakdownCalculator).
///
/// 1. Plans, in order: the sender's active and valid contract plan, the plan assigned to the sender, the default
///    plan, then (step 9) the other active general plans (not assigned to any customer) by id. The FIRST plan that has a matching rule prices the
///    shipment (a contract that does not cover this service / route falls back to the normal prices). So a "per kg"
///    plan in one currency and a "per m³" plan in another can live side by side.
/// 2. Rule: the best matching active rule of that plan (priority, specificity, oldest).
/// 3. Breakdown: base + weight tiers + service fees - discounts + COD commission + tax (see PriceRule).
/// 4. Manual values (a manual subtotal and/or a manual discount) replace / extend the rule price; the rule-only
///    price is kept in RuleBreakdown so the size of the change can be judged.
///
/// Plans, rules, zones, currencies and tax rates are cached for the lifetime of the scope (one HTTP request),
/// which keeps the Excel import fast.
/// </summary>
public sealed class ShipmentPricingCalculator
{
    private readonly AppDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    private readonly Dictionary<long, PlanInfo?> _plans = new();
    private readonly Dictionary<long, PlanInfo?> _contracts = new();
    private PlanInfo? _defaultPlan;
    private bool _defaultPlanLoaded;
    private List<PlanInfo>? _otherGeneralPlans;
    private CurrencyInfo? _baseCurrency;
    private readonly Dictionary<long, CurrencyInfo> _currencies = new();
    private readonly Dictionary<long, decimal?> _taxRates = new();
    private TaxInfo? _defaultTax;
    private bool _defaultTaxLoaded;
    private readonly Dictionary<long, bool> _remoteZones = new();

    public ShipmentPricingCalculator(AppDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PricingResult> CalculateAsync(PricingInput input, ShippingSettings settings, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.UtcNow;

        var context = new PricingContext
        {
            OriginZoneId = input.OriginZoneId,
            DestinationZoneId = input.DestinationZoneId,
            OriginCityId = input.OriginCityId,
            DestinationCityId = input.DestinationCityId,
            ServiceTypeId = input.ServiceTypeId,
            ChargeableWeight = input.ChargeableWeight,
            PackageCount = input.PackageCount,
            PricingBasis = input.PricingBasis
        };

        // The first plan (contract -> assigned -> default) that has a matching rule prices the shipment.
        // A contract that does not cover this kind of shipment therefore falls back to the normal prices.
        var candidates = await ResolvePlansAsync(input.SenderCustomerId, input.CustomerPricingPlanId, now, cancellationToken);
        PlanInfo? plan = null;
        PriceRule? rule = null;
        var source = PricingSource.PricingRule;

        foreach (var (candidate, candidateSource) in candidates)
        {
            var match = PriceRuleMatcher.SelectBest(candidate.Rules, context, now);
            if (match is not null)
            {
                (plan, rule, source) = (candidate, match, candidateSource);
                break;
            }
        }

        // No rule anywhere: a manual price uses the currency of the first applicable plan.
        if (plan is null && candidates.Count > 0)
        {
            (plan, source) = candidates[0];
        }

        var currency = plan is not null
            ? await GetCurrencyAsync(plan.CurrencyId, cancellationToken)
            : await GetBaseCurrencyAsync(cancellationToken);
        var decimals = Math.Clamp(currency.DecimalPlaces, 0, 4);

        if (rule is null && input.ManualShippingCharge is null)
        {
            throw new BusinessRuleException(
                $"لا توجد قاعدة تسعير مناسبة لهذه الشحنة ({ShipmentChargeEnums.Arabic(input.PricingBasis)}، نوع البضاعة، المناطق، الوزن، عدد الطرود). " +
                "أضف قاعدة في خطة التسعير، أو أدخل سعراً يدوياً بواسطة مستخدم يملك صلاحية Shipments.OverridePrice.");
        }

        var isRemoteArea = input.DestinationZoneId.HasValue && await IsRemoteZoneAsync(input.DestinationZoneId.Value, cancellationToken);
        var (taxRateId, taxPercentage) = await ResolveTaxAsync(rule?.TaxRateId, cancellationToken);

        var breakdownInput = new PriceBreakdownInput
        {
            ChargeableWeight = input.ChargeableWeight,
            PricingBasis = input.PricingBasis,
            VolumeCbm = input.VolumeCbm,
            PackageCount = input.PackageCount,
            IsCod = input.IsCod,
            CodAmount = input.IsCod ? input.CodAmount : 0m,
            InsuranceValue = input.InsuranceValue,
            RequiresPickup = input.RequiresPickup,
            RequiresPackaging = input.RequiresPackaging,
            IsRemoteArea = isRemoteArea,
            IsReturn = input.IsReturn,
            ManualShippingCharge = input.ManualShippingCharge,
            ManualDiscountAmount = input.ManualDiscountAmount
        };

        var breakdown = PriceBreakdownCalculator.Calculate(rule, breakdownInput, taxPercentage, decimals);

        if (breakdown.DiscountAmount > breakdown.ShippingSubtotal)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["discountAmount"] = new[] { $"الخصم لا يمكن أن يزيد عن سعر الشحن ({breakdown.ShippingSubtotal - breakdown.ContractDiscountAmount})." }
            });
        }

        var ruleBreakdown = rule is null
            ? null
            : input.IsManual
                ? PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
                {
                    ChargeableWeight = breakdownInput.ChargeableWeight,
                    PricingBasis = breakdownInput.PricingBasis,
                    VolumeCbm = breakdownInput.VolumeCbm,
                    PackageCount = breakdownInput.PackageCount,
                    IsCod = breakdownInput.IsCod,
                    CodAmount = breakdownInput.CodAmount,
                    InsuranceValue = breakdownInput.InsuranceValue,
                    RequiresPickup = breakdownInput.RequiresPickup,
                    RequiresPackaging = breakdownInput.RequiresPackaging,
                    IsRemoteArea = breakdownInput.IsRemoteArea,
                    IsReturn = breakdownInput.IsReturn
                }, taxPercentage, decimals)
                : breakdown;

        return new PricingResult
        {
            PricingSource = input.ManualShippingCharge.HasValue ? PricingSource.Manual : source,
            PricingPlanId = plan?.Id,
            PricingPlanCode = plan?.Code,
            PricingRuleId = rule?.Id,
            PricingRuleName = rule?.ArabicName,
            CurrencyId = currency.Id,
            CurrencyCode = currency.Code,
            CurrencyDecimals = decimals,
            VolumetricDivisor = settings.VolumetricDivisor,
            WeightRoundingStepKg = settings.WeightRoundingStepKg,
            Input = input,
            IsRemoteArea = isRemoteArea,
            TaxRateId = taxRateId,
            Breakdown = breakdown,
            RuleBreakdown = ruleBreakdown
        };
    }

    // ------------------------------------------------------------------ plan

    /// <summary>Applicable plans in priority order: the sender's contract, the assigned plan, the default plan.</summary>
    private async Task<List<(PlanInfo Plan, PricingSource Source)>> ResolvePlansAsync(
        long senderId,
        long? assignedPlanId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var result = new List<(PlanInfo Plan, PricingSource Source)>();

        if (!_contracts.TryGetValue(senderId, out var contract))
        {
            var contractId = await _dbContext.CustomerPricingPlans
                .AsNoTracking()
                .Where(x => x.ContractCustomerId == senderId && x.IsActive &&
                            (x.ValidFromUtc == null || x.ValidFromUtc <= now) &&
                            (x.ValidToUtc == null || x.ValidToUtc > now))
                .OrderByDescending(x => x.ValidFromUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            contract = contractId.HasValue ? await GetPlanAsync(contractId.Value, cancellationToken) : null;
            _contracts[senderId] = contract;
        }

        if (contract is not null && IsValid(contract, now))
        {
            result.Add((contract, PricingSource.CustomerContract));
        }

        if (assignedPlanId.HasValue && result.All(x => x.Plan.Id != assignedPlanId.Value))
        {
            var assigned = await GetPlanAsync(assignedPlanId.Value, cancellationToken);
            if (assigned is not null && IsValid(assigned, now) && (assigned.ContractCustomerId is null || assigned.ContractCustomerId == senderId))
            {
                result.Add((assigned, assigned.ContractCustomerId.HasValue ? PricingSource.CustomerContract : PricingSource.PricingRule));
            }
        }

        if (!_defaultPlanLoaded)
        {
            var defaultId = await _dbContext.CustomerPricingPlans
                .AsNoTracking()
                .Where(x => x.IsDefault && x.IsActive)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            _defaultPlan = defaultId.HasValue ? await GetPlanAsync(defaultId.Value, cancellationToken) : null;
            _defaultPlanLoaded = true;
        }

        if (_defaultPlan is not null && IsValid(_defaultPlan, now) && result.All(x => x.Plan.Id != _defaultPlan.Id))
        {
            result.Add((_defaultPlan, PricingSource.PricingRule));
        }

        // Step 9: the other general plans (no contract, not assigned to any customer, so special prices never leak),
        // e.g. a per-m³ plan in USD next to the default per-kg plan in LYD.
        if (_otherGeneralPlans is null)
        {
            var ids = await _dbContext.CustomerPricingPlans
                .AsNoTracking()
                .Where(x => x.ContractCustomerId == null && !x.IsDefault && x.IsActive &&
                            !_dbContext.Customers.Any(c => c.PricingPlanId == x.Id))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            _otherGeneralPlans = new List<PlanInfo>();
            foreach (var id in ids)
            {
                var other = await GetPlanAsync(id, cancellationToken);
                if (other is not null)
                {
                    _otherGeneralPlans.Add(other);
                }
            }
        }

        foreach (var other in _otherGeneralPlans)
        {
            if (IsValid(other, now) && result.All(x => x.Plan.Id != other.Id))
            {
                result.Add((other, PricingSource.PricingRule));
            }
        }

        return result;
    }

    private static bool IsValid(PlanInfo plan, DateTime now) =>
        plan.IsActive &&
        (plan.ValidFromUtc is null || plan.ValidFromUtc <= now) &&
        (plan.ValidToUtc is null || plan.ValidToUtc > now);

    private async Task<PlanInfo?> GetPlanAsync(long planId, CancellationToken cancellationToken)
    {
        if (_plans.TryGetValue(planId, out var cached))
        {
            return cached;
        }

        var plan = await _dbContext.CustomerPricingPlans
            .AsNoTracking()
            .Where(x => x.Id == planId)
            .Select(x => new PlanInfo(x.Id, x.Code, x.CurrencyId, x.IsActive, x.ValidFromUtc, x.ValidToUtc, x.ContractCustomerId))
            .FirstOrDefaultAsync(cancellationToken);

        if (plan is not null)
        {
            plan.Rules = await _dbContext.PriceRules
                .AsNoTracking()
                .Include(x => x.Conditions)
                .Include(x => x.WeightTiers)
                .Where(x => x.PricingPlanId == planId && x.IsActive)
                .ToListAsync(cancellationToken);
        }

        _plans[planId] = plan;
        return plan;
    }

    // ------------------------------------------------------------------ reference data

    private async Task<bool> IsRemoteZoneAsync(long zoneId, CancellationToken cancellationToken)
    {
        if (!_remoteZones.TryGetValue(zoneId, out var isRemote))
        {
            isRemote = await _dbContext.Zones
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(x => x.Id == zoneId)
                .Select(x => x.IsRemoteArea)
                .FirstOrDefaultAsync(cancellationToken);
            _remoteZones[zoneId] = isRemote;
        }

        return isRemote;
    }

    /// <summary>The rule's tax rate when set (and not deleted), otherwise the default tax rate (or 0 %).</summary>
    private async Task<(long? TaxRateId, decimal Percentage)> ResolveTaxAsync(long? ruleTaxRateId, CancellationToken cancellationToken)
    {
        if (ruleTaxRateId.HasValue)
        {
            if (!_taxRates.TryGetValue(ruleTaxRateId.Value, out var percentage))
            {
                var id = ruleTaxRateId.Value;
                percentage = await _dbContext.TaxRates
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => (decimal?)x.Percentage)
                    .FirstOrDefaultAsync(cancellationToken);
                _taxRates[ruleTaxRateId.Value] = percentage;
            }

            if (percentage.HasValue)
            {
                return (ruleTaxRateId, percentage.Value);
            }
        }

        if (!_defaultTaxLoaded)
        {
            _defaultTax = await _dbContext.TaxRates
                .AsNoTracking()
                .Where(x => x.IsDefault && x.IsActive)
                .Select(x => new TaxInfo(x.Id, x.Percentage))
                .FirstOrDefaultAsync(cancellationToken);
            _defaultTaxLoaded = true;
        }

        return (_defaultTax?.Id, _defaultTax?.Percentage ?? 0m);
    }

    private async Task<CurrencyInfo> GetCurrencyAsync(long currencyId, CancellationToken cancellationToken)
    {
        if (_currencies.TryGetValue(currencyId, out var cached))
        {
            return cached;
        }

        var currency = await _dbContext.Currencies
                           .AsNoTracking()
                           .IgnoreQueryFilters()
                           .Where(x => x.Id == currencyId)
                           .Select(x => new CurrencyInfo(x.Id, x.Code, x.DecimalPlaces))
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw new BusinessRuleException("عملة خطة التسعير غير موجودة.");

        _currencies[currencyId] = currency;
        return currency;
    }

    private async Task<CurrencyInfo> GetBaseCurrencyAsync(CancellationToken cancellationToken)
    {
        if (_baseCurrency is not null)
        {
            return _baseCurrency;
        }

        var baseCurrencyId = await _dbContext.Currencies
            .AsNoTracking()
            .Where(x => x.IsBaseCurrency && x.IsActive)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        baseCurrencyId ??= await _dbContext.CompanyProfiles
            .AsNoTracking()
            .Select(x => (long?)x.BaseCurrencyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (baseCurrencyId is null)
        {
            throw new BusinessRuleException("لا توجد عملة أساسية. عرّف العملة الأساسية في البيانات الأساسية أولاً.");
        }

        _baseCurrency = await GetCurrencyAsync(baseCurrencyId.Value, cancellationToken);
        return _baseCurrency;
    }

    private sealed record PlanInfo(long Id, string Code, long CurrencyId, bool IsActive, DateTime? ValidFromUtc, DateTime? ValidToUtc, long? ContractCustomerId)
    {
        public List<PriceRule> Rules { get; set; } = new();
    }

    private sealed record CurrencyInfo(long Id, string Code, int DecimalPlaces);

    private sealed record TaxInfo(long Id, decimal Percentage);
}
```

### 6.26 `src/ShippingERP.Infrastructure/Shipments/ShipmentProjections.cs`

```csharp
using System.Linq.Expressions;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>Entity -> DTO projections translated to SQL (only the needed columns are read).</summary>
public static class ShipmentProjections
{
    public static readonly Expression<Func<Shipment, ShipmentListItemDto>> ListItem = x => new ShipmentListItemDto
    {
        Id = x.Id,
        ShipmentNumber = x.ShipmentNumber,
        TrackingNumber = x.TrackingNumber,
        ExternalReference = x.ExternalReference,
        Status = x.Status,
        StatusName = ShipmentStatusNames.Arabic(x.Status),
        SenderCustomerId = x.SenderCustomerId,
        SenderName = x.SenderCustomer.ArabicName,
        SenderPhone = x.SenderCustomer.Phone,
        RecipientCustomerId = x.RecipientCustomerId,
        RecipientName = x.RecipientCustomer.ArabicName,
        RecipientPhone = x.RecipientCustomer.Phone,
        OriginBranchId = x.OriginBranchId,
        OriginBranchName = x.OriginBranch.ArabicName,
        CurrentBranchId = x.CurrentBranchId,
        CurrentBranchName = x.CurrentBranch.ArabicName,
        DestinationBranchId = x.DestinationBranchId,
        DestinationBranchName = x.DestinationBranch != null ? x.DestinationBranch.ArabicName : null,
        DestinationCityId = x.DestinationCityId,
        DestinationCityName = x.DestinationCity.ArabicName,
        ServiceTypeId = x.ServiceTypeId,
        ServiceTypeName = x.ServiceType.ArabicName,
        AssignedDriverId = x.AssignedDriverId,
        AssignedDriverName = x.AssignedDriver != null ? x.AssignedDriver.Employee.ArabicName : null,
        PackageCount = x.PackageCount,
        ChargeableWeight = x.ChargeableWeight,
        TotalVolumeCbm = x.TotalVolumeCbm,
        PricingBasis = x.PricingBasis,
        ChargesPaidBy = x.ChargesPaidBy,
        IsCOD = x.IsCOD,
        CODAmount = x.CODAmount,
        TotalCharge = x.TotalCharge,
        CurrencyCode = x.Currency.Code,
        CreatedAtUtc = x.CreatedAtUtc,
        DeliveredAtUtc = x.DeliveredAtUtc
    };

    public static readonly Expression<Func<Shipment, ShipmentDto>> Detail = x => new ShipmentDto
    {
        Id = x.Id,
        ShipmentNumber = x.ShipmentNumber,
        TrackingNumber = x.TrackingNumber,
        ExternalReference = x.ExternalReference,
        Status = x.Status,
        StatusName = ShipmentStatusNames.Arabic(x.Status),

        SenderCustomerId = x.SenderCustomerId,
        SenderCode = x.SenderCustomer.Code,
        SenderName = x.SenderCustomer.ArabicName,
        SenderPhone = x.SenderCustomer.Phone,
        SenderAddressId = x.SenderAddressId,
        SenderAddressLine = x.SenderAddress != null ? x.SenderAddress.AddressLine : null,

        RecipientCustomerId = x.RecipientCustomerId,
        RecipientCode = x.RecipientCustomer.Code,
        RecipientName = x.RecipientCustomer.ArabicName,
        RecipientPhone = x.RecipientCustomer.Phone,
        RecipientAddressId = x.RecipientAddressId,
        RecipientAddressLine = x.RecipientAddress != null ? x.RecipientAddress.AddressLine : null,
        RecipientContactPhone = x.RecipientAddress != null ? x.RecipientAddress.Phone : null,

        OriginBranchId = x.OriginBranchId,
        OriginBranchName = x.OriginBranch.ArabicName,
        CurrentBranchId = x.CurrentBranchId,
        CurrentBranchName = x.CurrentBranch.ArabicName,
        CurrentWarehouseId = x.CurrentWarehouseId,
        CurrentWarehouseName = x.CurrentWarehouse != null ? x.CurrentWarehouse.ArabicName : null,
        DestinationBranchId = x.DestinationBranchId,
        DestinationBranchName = x.DestinationBranch != null ? x.DestinationBranch.ArabicName : null,
        OriginCityId = x.OriginCityId,
        OriginCityName = x.OriginCity.ArabicName,
        DestinationCityId = x.DestinationCityId,
        DestinationCityName = x.DestinationCity.ArabicName,
        OriginZoneId = x.OriginZoneId,
        OriginZoneName = x.OriginZone != null ? x.OriginZone.ArabicName : null,
        DestinationZoneId = x.DestinationZoneId,
        DestinationZoneName = x.DestinationZone != null ? x.DestinationZone.ArabicName : null,
        ServiceTypeId = x.ServiceTypeId,
        ServiceTypeName = x.ServiceType.ArabicName,

        AssignedDriverId = x.AssignedDriverId,
        AssignedDriverName = x.AssignedDriver != null ? x.AssignedDriver.Employee.ArabicName : null,
        VehicleId = x.VehicleId,
        VehiclePlateNumber = x.Vehicle != null ? x.Vehicle.PlateNumber : null,
        DeliveryAttemptCount = x.DeliveryAttemptCount,
        ReturnBranchId = x.ReturnBranchId,

        PackageCount = x.PackageCount,
        ActualWeight = x.ActualWeight,
        VolumetricWeight = x.VolumetricWeight,
        ChargeableWeight = x.ChargeableWeight,
        TotalVolumeCbm = x.TotalVolumeCbm,
        PricingBasis = x.PricingBasis,
        ChargesPaidBy = x.ChargesPaidBy,
        PackageDescription = x.PackageDescription,

        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        DeclaredValue = x.DeclaredValue,
        InsuranceValue = x.InsuranceValue,
        IsCOD = x.IsCOD,
        CODAmount = x.CODAmount,
        RequiresPickup = x.RequiresPickup,
        RequiresPackaging = x.RequiresPackaging,
        ShippingCharge = x.ShippingCharge,
        DiscountAmount = x.DiscountAmount,
        TaxAmount = x.TaxAmount,
        TotalCharge = x.TotalCharge,
        ManualShippingCharge = x.PricingSnapshots
            .OrderByDescending(p => p.Id)
            .Select(p => p.PricingSource == PricingSource.Manual ? (decimal?)p.ShippingSubtotal : null)
            .FirstOrDefault(),
        ManualDiscountAmount = x.PricingSnapshots
            .OrderByDescending(p => p.Id)
            .Select(p => p.ManualDiscountAmount)
            .FirstOrDefault(),
        InvoiceId = x.InvoiceLines.Where(l => l.IsActive).Select(l => (long?)l.InvoiceId).FirstOrDefault(),
        InvoiceNumber = x.InvoiceLines.Where(l => l.IsActive).Select(l => l.Invoice.InvoiceNumber).FirstOrDefault(),
        HasPendingPriceOverride = x.PriceOverrides.Any(o => o.Status == PriceOverrideStatus.Pending),
        CodTransactionId = x.CodTransaction != null ? (long?)x.CodTransaction.Id : null,
        CodStatus = x.CodTransaction != null ? (CODStatus?)x.CodTransaction.Status : null,
        CodCollectedAmount = x.CodTransaction != null ? (decimal?)x.CodTransaction.CollectedAmount : null,

        SenderNotes = x.SenderNotes,
        InternalNotes = x.InternalNotes,
        CancellationReason = x.CancellationReason,

        CreatedAtUtc = x.CreatedAtUtc,
        CreatedByUserId = x.CreatedByUserId,
        UpdatedAtUtc = x.UpdatedAtUtc,
        UpdatedByUserId = x.UpdatedByUserId,
        PickedUpAtUtc = x.PickedUpAtUtc,
        DeliveredAtUtc = x.DeliveredAtUtc,
        ReturnedAtUtc = x.ReturnedAtUtc,
        CancelledAtUtc = x.CancelledAtUtc,

        Packages = x.Packages
            .OrderBy(p => p.SequenceNumber)
            .Select(p => new ShipmentPackageDto
            {
                Id = p.Id,
                SequenceNumber = p.SequenceNumber,
                PackageBarcode = p.PackageBarcode,
                Description = p.Description,
                WeightKg = p.WeightKg,
                LengthCm = p.LengthCm,
                WidthCm = p.WidthCm,
                HeightCm = p.HeightCm,
                VolumetricWeightKg = p.VolumetricWeightKg
            })
            .ToList(),

        NextStatuses = ShipmentStatusRules.NextStatuses(x.Status).ToList()
    };

    private static readonly System.Text.Json.JsonSerializerOptions TierJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Snapshot -> breakdown DTO (in memory: the weight tiers are stored as JSON).</summary>
    public static ShipmentPricingDto ToPricingDto(ShipmentPricingSnapshot x) => new()
    {
        Id = x.Id,
        Reason = x.Reason,
        PricingSource = x.PricingSource,
        PricingPlanId = x.PricingPlanId,
        PricingPlanCode = x.PricingPlanCode,
        PricingRuleId = x.PricingRuleId,
        PricingRuleName = x.PricingRuleName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.CurrencyCode,
        ActualWeight = x.ActualWeight,
        VolumetricWeight = x.VolumetricWeight,
        ChargeableWeight = x.ChargeableWeight,
        VolumetricDivisor = x.VolumetricDivisor,
        WeightRoundingStepKg = x.WeightRoundingStepKg,
        PackageCount = x.PackageCount,
        PricingBasis = x.PricingBasis,
        VolumeCbm = x.VolumeCbm,
        PricePerCubicMeter = x.PricePerCubicMeter,
        VolumeCharge = x.VolumeCharge,
        InsuranceValue = x.InsuranceValue,
        RequiresPickup = x.RequiresPickup,
        RequiresPackaging = x.RequiresPackaging,
        IsRemoteArea = x.IsRemoteArea,
        IsReturn = x.IsReturn,
        BaseCharge = x.BasePrice,
        IncludedWeightKg = x.IncludedWeightKg,
        AdditionalWeightKg = x.AdditionalWeightKg,
        WeightCharge = x.AdditionalWeightCharge,
        WeightTiers = ParseTiers(x.WeightTiersJson),
        PackageFee = x.PackageFee,
        InsuranceFee = x.InsuranceFee,
        PickupFee = x.PickupFee,
        PackagingFee = x.PackagingFee,
        RemoteAreaFee = x.RemoteAreaFee,
        ReturnFee = x.ReturnFee,
        ServiceFeesTotal = x.ServiceFeesTotal,
        ShippingSubtotal = x.ShippingSubtotal,
        CalculatedShippingSubtotal = x.CalculatedShippingSubtotal,
        ContractDiscountPercentage = x.ContractDiscountPercentage,
        ContractDiscountAmount = x.ContractDiscountAmount,
        ManualDiscountAmount = x.ManualDiscountAmount,
        DiscountAmount = x.DiscountAmount,
        TotalShippingCharge = x.TotalShippingCharge,
        CodAmount = x.CodAmount,
        CodFeeFixed = x.CodFeeFixed,
        CodFeePercentage = x.CodFeePercentage,
        CodFeeMinimum = x.CodFeeMinimum,
        CodCommission = x.CodFee,
        TaxableAmount = x.TaxableAmount,
        TaxRateId = x.TaxRateId,
        TaxPercentage = x.TaxPercentage,
        TaxAmount = x.TaxAmount,
        NetTotal = x.TotalCharge,
        ShippingCharge = x.ShippingCharge,
        TotalCharge = x.TotalCharge,
        OverrideReason = x.OverrideReason,
        PriceOverrideId = x.PriceOverrideId,
        CalculatedAtUtc = x.CreatedAtUtc
    };

    private static IReadOnlyList<PricingWeightTierDto> ParseTiers(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? Array.Empty<PricingWeightTierDto>()
            : System.Text.Json.JsonSerializer.Deserialize<List<PricingWeightTierDto>>(json, TierJsonOptions) ?? new List<PricingWeightTierDto>();

    /// <summary>A shipment line on a manifest (without the item fields).</summary>
    public static readonly Expression<Func<Shipment, ManifestShipmentDto>> ManifestLine = x => new ManifestShipmentDto
    {
        ShipmentId = x.Id,
        ShipmentNumber = x.ShipmentNumber,
        TrackingNumber = x.TrackingNumber,
        ShipmentStatus = x.Status,
        ShipmentStatusName = ShipmentStatusNames.Arabic(x.Status),
        RecipientName = x.RecipientCustomer.ArabicName,
        RecipientPhone = x.RecipientCustomer.Phone,
        DestinationCityName = x.DestinationCity.ArabicName,
        DestinationZoneName = x.DestinationZone != null ? x.DestinationZone.ArabicName : null,
        PackageCount = x.PackageCount,
        ChargeableWeight = x.ChargeableWeight,
        TotalVolumeCbm = x.TotalVolumeCbm,
        PricingBasis = x.PricingBasis,
        ChargesPaidBy = x.ChargesPaidBy,
        IsCOD = x.IsCOD,
        CODAmount = x.CODAmount
    };
}
```

### 6.27 `src/ShippingERP.Infrastructure/Shipments/ShipmentService.cs`

```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>
/// Shipment registration, search, tracking and the simple status operations (scan, cancel, driver assignment,
/// correction). Workflow statuses (transfers, delivery manifests, delivery, returns, damage) have their own services.
/// </summary>
public sealed class ShipmentService : IShipmentService
{
    private const string EntityName = "Shipment";

    private static readonly Dictionary<string, Expression<Func<Shipment, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["createdAtUtc"] = x => x.CreatedAtUtc,
        ["shipmentNumber"] = x => x.ShipmentNumber,
        ["trackingNumber"] = x => x.TrackingNumber,
        ["status"] = x => x.Status,
        ["totalCharge"] = x => x.TotalCharge,
        ["codAmount"] = x => x.CODAmount,
        ["chargeableWeight"] = x => x.ChargeableWeight,
        ["deliveredAtUtc"] = x => x.DeliveredAtUtc
    };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly ShipmentAccessService _access;
    private readonly ShipmentBuilder _builder;
    private readonly ShipmentStatusChanger _statusChanger;
    private readonly ShippingSettingsService _settings;

    public ShipmentService(
        MasterDataServiceDependencies dependencies,
        ShipmentAccessService access,
        ShipmentBuilder builder,
        ShipmentStatusChanger statusChanger,
        ShippingSettingsService settings)
    {
        _dependencies = dependencies;
        _access = access;
        _builder = builder;
        _statusChanger = statusChanger;
        _settings = settings;
    }

    private AppDbContext Db => _dependencies.DbContext;

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<ShipmentListItemDto>> GetPagedAsync(ShipmentListQuery query, CancellationToken cancellationToken)
    {
        var rows = ApplyFilters(await _access.ScopeAsync(Db.Shipments.AsNoTracking(), cancellationToken), query);

        var totalCount = await rows.CountAsync(cancellationToken);

        IOrderedQueryable<Shipment> ordered;
        if (string.IsNullOrWhiteSpace(query.SortBy) || !Sorts.TryGetValue(query.SortBy, out var sort))
        {
            // Default: newest first.
            ordered = rows.OrderByDescending(x => x.CreatedAtUtc);
        }
        else
        {
            ordered = query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort);
        }

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ShipmentProjections.ListItem)
            .ToListAsync(cancellationToken);

        return new PagedResult<ShipmentListItemDto>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<ShipmentDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var query = await _access.ScopeAsync(Db.Shipments.AsNoTracking(), cancellationToken);

        return await query
                   .Where(x => x.Id == id)
                   .Select(ShipmentProjections.Detail)
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("الشحنة", id);
    }

    public async Task<ShipmentTrackingDto> TrackAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        var query = await _access.ScopeAsync(Db.Shipments.AsNoTracking(), cancellationToken);

        var shipment = await query
            .Where(x => x.TrackingNumber == normalized ||
                        x.ShipmentNumber == normalized ||
                        x.Packages.Any(p => p.PackageBarcode == normalized))
            .Select(x => new
            {
                x.Id,
                x.TrackingNumber,
                x.ShipmentNumber,
                x.Status,
                OriginCityName = x.OriginCity.ArabicName,
                DestinationCityName = x.DestinationCity.ArabicName,
                CurrentBranchName = x.CurrentBranch.ArabicName,
                x.PackageCount,
                x.CreatedAtUtc,
                x.DeliveredAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"لم يتم العثور على شحنة برقم التتبع ({normalized}).");

        var shipmentId = shipment.Id;
        var events = await Db.ShipmentStatusHistory.AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId)
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new ShipmentTrackingEventDto
            {
                OccurredAtUtc = x.OccurredAtUtc,
                Status = x.ToStatus,
                StatusName = ShipmentStatusNames.Arabic(x.ToStatus),
                BranchName = Db.Branches.Where(b => b.Id == x.BranchId).Select(b => b.ArabicName).FirstOrDefault(),
                Notes = x.Notes
            })
            .ToListAsync(cancellationToken);

        return new ShipmentTrackingDto
        {
            ShipmentId = shipment.Id,
            TrackingNumber = shipment.TrackingNumber,
            ShipmentNumber = shipment.ShipmentNumber,
            Status = shipment.Status,
            StatusName = ShipmentStatusNames.Arabic(shipment.Status),
            OriginCityName = shipment.OriginCityName,
            DestinationCityName = shipment.DestinationCityName,
            CurrentBranchName = shipment.CurrentBranchName,
            PackageCount = shipment.PackageCount,
            CreatedAtUtc = shipment.CreatedAtUtc,
            DeliveredAtUtc = shipment.DeliveredAtUtc,
            Events = events
        };
    }

    public async Task<IReadOnlyList<ShipmentStatusHistoryDto>> GetHistoryAsync(long id, CancellationToken cancellationToken)
    {
        await _access.EnsureVisibleAsync(id, cancellationToken);

        return await Db.ShipmentStatusHistory.AsNoTracking()
            .Where(x => x.ShipmentId == id)
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new ShipmentStatusHistoryDto
            {
                Id = x.Id,
                FromStatus = x.FromStatus,
                FromStatusName = x.FromStatus.HasValue ? ShipmentStatusNames.Arabic(x.FromStatus.Value) : null,
                ToStatus = x.ToStatus,
                ToStatusName = ShipmentStatusNames.Arabic(x.ToStatus),
                Source = x.Source,
                OccurredAtUtc = x.OccurredAtUtc,
                BranchId = x.BranchId,
                BranchName = Db.Branches.Where(b => b.Id == x.BranchId).Select(b => b.ArabicName).FirstOrDefault(),
                WarehouseId = x.WarehouseId,
                DriverId = x.DriverId,
                Notes = x.Notes,
                ReferenceType = x.ReferenceType,
                ReferenceId = x.ReferenceId,
                ReferenceNumber = x.ReferenceNumber,
                IsCorrection = x.IsCorrection,
                ChangedByUserId = x.CreatedByUserId,
                ChangedByUserName = Db.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault(),
                RecordedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShipmentPricingDto>> GetPricingHistoryAsync(long id, CancellationToken cancellationToken)
    {
        await _access.EnsureVisibleAsync(id, cancellationToken);

        var snapshots = await Db.ShipmentPricingSnapshots.AsNoTracking()
            .Where(x => x.ShipmentId == id)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return snapshots.Select(ShipmentProjections.ToPricingDto).ToList();
    }

    // ------------------------------------------------------------------ create / update / quote

    public async Task<ShipmentPricingDto> QuoteAsync(SaveShipmentRequest request, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var draft = await _builder.PrepareAsync(request, null, settings, cancellationToken);
        return draft.Pricing.ToDto("Quote");
    }

    public async Task<ShipmentDto> CreateAsync(SaveShipmentRequest request, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var draft = await _builder.PrepareAsync(request, null, settings, cancellationToken);

        var id = await Db.ExecuteInTransactionAsync(async ct =>
        {
            var shipment = await _builder.AddNewAsync(draft, settings, ShipmentStatusChangeSource.Creation, "Created", null, ct);
            await Db.SaveChangesAsync(ct);

            _dependencies.AuditLog.Add(
                AuditAction.ShipmentCreated,
                entityName: EntityName,
                entityId: shipment.Id.ToString(),
                details: new { shipment.ShipmentNumber, shipment.TrackingNumber, shipment.TotalCharge, shipment.IsCOD, shipment.CODAmount });
            await Db.SaveChangesAsync(ct);

            return shipment.Id;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ShipmentDto> UpdateAsync(long id, SaveShipmentRequest request, CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(id, cancellationToken, includePackages: true);
        _access.EnsureCanOperate(shipment);

        if (!ShipmentStatusRules.EditableStatuses.Contains(shipment.Status))
        {
            throw new BusinessRuleException(
                $"لا يمكن تعديل الشحنة وهي في حالة «{ShipmentStatusNames.Arabic(shipment.Status)}». " +
                "التعديل مسموح فقط قبل خروجها من فرع الإنشاء.");
        }

        await ShipmentBillingGuards.EnsureNotInvoicedAsync(Db, shipment.Id, "تعديل الشحنة", cancellationToken);

        var settings = await _settings.GetEffectiveAsync(cancellationToken);
        var draft = await _builder.PrepareAsync(request, shipment, settings, cancellationToken);

        var before = new { shipment.TotalCharge, shipment.ChargeableWeight, shipment.RecipientCustomerId, shipment.DestinationCityId, shipment.CODAmount };
        var priceChanged =
            shipment.ShippingCharge != draft.Pricing.ShippingCharge ||
            shipment.DiscountAmount != draft.Pricing.DiscountAmount ||
            shipment.TaxAmount != draft.Pricing.TaxAmount ||
            shipment.ChargeableWeight != draft.ChargeableWeight ||
            shipment.TotalVolumeCbm != draft.TotalVolumeCbm ||
            shipment.PricingBasis != draft.PricingBasis ||
            shipment.CODAmount != draft.CodAmount ||
            shipment.PackageCount != draft.Packages.Count ||
            shipment.InsuranceValue != draft.InsuranceValue ||
            shipment.RequiresPickup != draft.RequiresPickup ||
            shipment.RequiresPackaging != draft.RequiresPackaging ||
            shipment.DestinationZoneId != draft.DestinationZoneId;

        await Db.ExecuteInTransactionAsync(async ct =>
        {
            _builder.Apply(shipment, draft);

            if (priceChanged)
            {
                Db.ShipmentPricingSnapshots.Add(draft.Pricing.ToSnapshot(shipment, "Updated", draft.ManualPriceReason));

                // A pending manual price was calculated for the old data.
                await ShipmentBillingGuards.CancelPendingOverridesAsync(
                    Db, shipment.Id, _dependencies.CurrentUser.UserId, _dependencies.DateTimeProvider.UtcNow, "أُلغي تلقائياً: تم تعديل بيانات الشحنة.", ct);
            }

            _dependencies.AuditLog.Add(
                AuditAction.ShipmentUpdated,
                entityName: EntityName,
                entityId: shipment.Id.ToString(),
                details: new
                {
                    before,
                    after = new { shipment.TotalCharge, shipment.ChargeableWeight, shipment.RecipientCustomerId, shipment.DestinationCityId, shipment.CODAmount },
                    priceChanged
                });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ status operations

    public async Task<ShipmentDto> ChangeStatusAsync(long id, ChangeShipmentStatusRequest request, CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(id, cancellationToken);
        _access.EnsureCanOperate(shipment);

        if (_access.IsDriverOnly && request.Status != ShipmentStatus.PickedUp)
        {
            throw new ForbiddenException("السائق يمكنه تسجيل الاستلام من المرسل (PickedUp) فقط.");
        }

        if (request.Status == ShipmentStatus.InWarehouse)
        {
            await EnsureWarehouseOfBranchAsync(request.WarehouseId!.Value, shipment.CurrentBranchId, cancellationToken);
        }

        _statusChanger.Change(shipment, request.Status, new StatusChange(ShipmentStatusChangeSource.Manual)
        {
            OccurredAtUtc = request.OccurredAtUtc,
            WarehouseId = request.Status == ShipmentStatus.InWarehouse ? request.WarehouseId : null,
            Notes = request.Notes
        });

        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ShipmentDto> CancelAsync(long id, CancelShipmentRequest request, CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(id, cancellationToken);
        _access.EnsureCanOperate(shipment);

        if (!ShipmentStatusRules.CancellableStatuses.Contains(shipment.Status))
        {
            throw new BusinessRuleException(
                $"لا يمكن إلغاء الشحنة وهي في حالة «{ShipmentStatusNames.Arabic(shipment.Status)}». " +
                "الإلغاء مسموح فقط قبل خروجها من فرع الإنشاء؛ استخدم المرتجعات بعد ذلك.");
        }

        await ShipmentDocumentGuards.EnsureNotOnOpenDocumentAsync(Db, shipment, cancellationToken);
        await ShipmentBillingGuards.EnsureNotInvoicedAsync(Db, shipment.Id, "إلغاء الشحنة", cancellationToken);
        await ShipmentBillingGuards.CancelPendingOverridesAsync(
            Db, shipment.Id, _dependencies.CurrentUser.UserId, _dependencies.DateTimeProvider.UtcNow, "أُلغي تلقائياً: أُلغيت الشحنة.", cancellationToken);

        var reason = request.Reason.Trim();
        shipment.CancellationReason = reason;
        _statusChanger.Change(shipment, ShipmentStatus.Cancelled, new StatusChange(ShipmentStatusChangeSource.Cancellation) { Notes = reason });

        _dependencies.AuditLog.Add(
            AuditAction.ShipmentCancelled,
            entityName: EntityName,
            entityId: shipment.Id.ToString(),
            details: new { shipment.ShipmentNumber, reason });

        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ShipmentDto> AssignDriverAsync(long id, AssignDriverRequest request, CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(id, cancellationToken);
        _access.EnsureCanOperate(shipment);

        if (ShipmentStatusRules.IsFinal(shipment.Status) || shipment.Status is ShipmentStatus.OutForDelivery or ShipmentStatus.InTransit)
        {
            throw new BusinessRuleException(
                $"لا يمكن تغيير السائق والشحنة في حالة «{ShipmentStatusNames.Arabic(shipment.Status)}». " +
                "الشحنات الخارجة للتوصيل تُدار من بيان التوزيع.");
        }

        var before = new { shipment.AssignedDriverId, shipment.VehicleId };

        if (request.DriverId is null)
        {
            shipment.AssignedDriverId = null;
            shipment.VehicleId = null;
        }
        else
        {
            var driver = await DriverRules.GetUsableDriverAsync(Db, request.DriverId.Value, shipment.CurrentBranchId, _dependencies.DateTimeProvider.UtcNow, cancellationToken);
            var vehicleId = request.VehicleId ?? driver.DefaultVehicleId;
            if (vehicleId.HasValue)
            {
                await DriverRules.EnsureUsableVehicleAsync(Db, vehicleId.Value, shipment.CurrentBranchId, cancellationToken);
            }

            shipment.AssignedDriverId = driver.Id;
            shipment.VehicleId = vehicleId;
        }

        _dependencies.AuditLog.Add(
            AuditAction.ShipmentDriverAssigned,
            entityName: EntityName,
            entityId: shipment.Id.ToString(),
            details: new { before, after = new { shipment.AssignedDriverId, shipment.VehicleId }, request.Notes });

        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ShipmentDto> CorrectStatusAsync(long id, CorrectShipmentStatusRequest request, CancellationToken cancellationToken)
    {
        var shipment = await _access.FindForUpdateAsync(id, cancellationToken);
        _access.EnsureCanOperate(shipment);

        if (shipment.Status == request.Status && request.CurrentBranchId is null && request.Status != ShipmentStatus.InWarehouse)
        {
            throw new BusinessRuleException("الشحنة في هذه الحالة أصلاً.");
        }

        await ShipmentDocumentGuards.EnsureNotOnDispatchedDocumentAsync(Db, shipment, cancellationToken);

        var before = new { shipment.Status, shipment.CurrentBranchId, shipment.CurrentWarehouseId };

        if (request.CurrentBranchId.HasValue && request.CurrentBranchId != shipment.CurrentBranchId)
        {
            var branchId = request.CurrentBranchId.Value;
            _access.BranchAccess.EnsureCanAccess(branchId);
            if (!await Db.Branches.AnyAsync(x => x.Id == branchId, cancellationToken))
            {
                throw new RequestValidationException(new Dictionary<string, string[]>
                {
                    ["currentBranchId"] = new[] { "الفرع غير موجود." }
                });
            }

            shipment.CurrentBranchId = branchId;
        }

        if (request.Status == ShipmentStatus.InWarehouse)
        {
            await EnsureWarehouseOfBranchAsync(request.WarehouseId!.Value, shipment.CurrentBranchId, cancellationToken);
        }

        if (shipment.Status != request.Status)
        {
            _statusChanger.Change(shipment, request.Status, new StatusChange(ShipmentStatusChangeSource.Correction)
            {
                WarehouseId = request.WarehouseId,
                Notes = request.Reason
            });
        }
        else
        {
            // Same status: only the branch and/or warehouse is corrected. Still recorded in the history.
            shipment.CurrentWarehouseId = request.Status == ShipmentStatus.InWarehouse ? request.WarehouseId : null;
            Db.ShipmentStatusHistory.Add(new ShipmentStatusHistory
            {
                Shipment = shipment,
                FromStatus = shipment.Status,
                ToStatus = shipment.Status,
                Source = ShipmentStatusChangeSource.Correction,
                OccurredAtUtc = _dependencies.DateTimeProvider.UtcNow,
                BranchId = shipment.CurrentBranchId,
                WarehouseId = shipment.CurrentWarehouseId,
                Notes = request.Reason.Trim(),
                IsCorrection = true
            });
        }

        _dependencies.AuditLog.Add(
            AuditAction.ShipmentStatusCorrected,
            entityName: EntityName,
            entityId: shipment.Id.ToString(),
            details: new { before, after = new { shipment.Status, shipment.CurrentBranchId, shipment.CurrentWarehouseId }, request.Reason });

        await Db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task EnsureWarehouseOfBranchAsync(long warehouseId, long branchId, CancellationToken cancellationToken)
    {
        var warehouse = await Db.Warehouses.AsNoTracking()
            .Where(x => x.Id == warehouseId)
            .Select(x => new { x.BranchId, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (warehouse is null || !warehouse.IsActive)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["warehouseId"] = new[] { "المستودع غير موجود أو غير مفعّل." }
            });
        }

        if (warehouse.BranchId != branchId)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["warehouseId"] = new[] { "المستودع لا يتبع الفرع الموجودة فيه الشحنة." }
            });
        }
    }

    private static IQueryable<Shipment> ApplyFilters(IQueryable<Shipment> query, ShipmentListQuery filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.TrackingNumber))
        {
            var trackingNumber = filter.TrackingNumber.Trim().ToUpperInvariant();
            query = query.Where(x => x.TrackingNumber == trackingNumber);
        }

        if (!string.IsNullOrWhiteSpace(filter.ShipmentNumber))
        {
            var shipmentNumber = filter.ShipmentNumber.Trim().ToUpperInvariant();
            query = query.Where(x => x.ShipmentNumber == shipmentNumber);
        }

        if (!string.IsNullOrWhiteSpace(filter.Sender))
        {
            var sender = filter.Sender.Trim();
            query = query.Where(x => x.SenderCustomer.ArabicName.Contains(sender) ||
                                     (x.SenderCustomer.EnglishName != null && x.SenderCustomer.EnglishName.Contains(sender)) ||
                                     x.SenderCustomer.Phone.Contains(sender));
        }

        if (!string.IsNullOrWhiteSpace(filter.Recipient))
        {
            var recipient = filter.Recipient.Trim();
            query = query.Where(x => x.RecipientCustomer.ArabicName.Contains(recipient) ||
                                     (x.RecipientCustomer.EnglishName != null && x.RecipientCustomer.EnglishName.Contains(recipient)) ||
                                     x.RecipientCustomer.Phone.Contains(recipient));
        }

        if (filter.Status.HasValue)
        {
            var status = filter.Status.Value;
            query = query.Where(x => x.Status == status);
        }

        if (filter.BranchId.HasValue)
        {
            var branchId = filter.BranchId.Value;
            query = query.Where(x => x.OriginBranchId == branchId || x.CurrentBranchId == branchId || x.DestinationBranchId == branchId);
        }

        if (filter.OriginBranchId.HasValue)
        {
            var branchId = filter.OriginBranchId.Value;
            query = query.Where(x => x.OriginBranchId == branchId);
        }

        if (filter.CurrentBranchId.HasValue)
        {
            var branchId = filter.CurrentBranchId.Value;
            query = query.Where(x => x.CurrentBranchId == branchId);
        }

        if (filter.DestinationBranchId.HasValue)
        {
            var branchId = filter.DestinationBranchId.Value;
            query = query.Where(x => x.DestinationBranchId == branchId);
        }

        if (filter.DriverId.HasValue)
        {
            var driverId = filter.DriverId.Value;
            query = query.Where(x => x.AssignedDriverId == driverId);
        }

        if (filter.SenderCustomerId.HasValue)
        {
            var customerId = filter.SenderCustomerId.Value;
            query = query.Where(x => x.SenderCustomerId == customerId);
        }

        if (filter.RecipientCustomerId.HasValue)
        {
            var customerId = filter.RecipientCustomerId.Value;
            query = query.Where(x => x.RecipientCustomerId == customerId);
        }

        if (filter.OriginCityId.HasValue)
        {
            var cityId = filter.OriginCityId.Value;
            query = query.Where(x => x.OriginCityId == cityId);
        }

        if (filter.DestinationCityId.HasValue)
        {
            var cityId = filter.DestinationCityId.Value;
            query = query.Where(x => x.DestinationCityId == cityId);
        }

        if (filter.ServiceTypeId.HasValue)
        {
            var serviceTypeId = filter.ServiceTypeId.Value;
            query = query.Where(x => x.ServiceTypeId == serviceTypeId);
        }

        if (filter.IsCOD.HasValue)
        {
            var isCod = filter.IsCOD.Value;
            query = query.Where(x => x.IsCOD == isCod);
        }

        if (filter.CreatedFromUtc.HasValue)
        {
            var from = filter.CreatedFromUtc.Value;
            query = query.Where(x => x.CreatedAtUtc >= from);
        }

        if (filter.CreatedToUtc.HasValue)
        {
            var to = filter.CreatedToUtc.Value;
            query = query.Where(x => x.CreatedAtUtc < to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            var upper = search.ToUpperInvariant();
            query = query.Where(x =>
                x.TrackingNumber == upper ||
                x.ShipmentNumber == upper ||
                x.ExternalReference == search ||
                x.SenderCustomer.ArabicName.Contains(search) ||
                x.SenderCustomer.Phone.Contains(search) ||
                x.RecipientCustomer.ArabicName.Contains(search) ||
                x.RecipientCustomer.Phone.Contains(search));
        }

        return query;
    }
}
```

### 6.28 `tests/ShippingERP.Tests/Unit/Billing/InvoiceServiceTests.cs`

```csharp
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Billing;

/// <summary>
/// Invoices and credit notes: drafts, issuing, duplicate prevention, consolidation, cancellation, credit notes,
/// overdue status, PDF and Excel. Demo standard shipment (2.5 kg) = 15,000 SYP before tax.
/// </summary>
public sealed class InvoiceServiceTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        _ids = await ShipmentTestData.LoadIdsAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<T> Use<T>(Func<IInvoiceService, Task<T>> action) => _context.UseAsync(action);

    private async Task<ShipmentDto> NewShipmentAsync(decimal weightKg = 2.5m, string sender = "CUS-00003") =>
        await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, sender: sender, weightKg: weightKg));

    [Fact]
    public async Task FromShipment_CreatesADraft_ThenIssueGivesTheNumber_DebitsTheAccount_AndPublishesTheEvent()
    {
        await BillingTestData.SetDefaultTaxAsync(_context, "VAT5");
        var shipment = await NewShipmentAsync();

        var draft = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);

        Assert.Equal(InvoiceStatus.Draft, draft.Status);
        Assert.Null(draft.InvoiceNumber);
        var line = Assert.Single(draft.Lines);
        Assert.Equal(shipment.Id, line.ShipmentId);
        Assert.Equal(15000m, line.TaxableAmount);
        Assert.Equal(750m, line.TaxAmount);
        Assert.Equal(15750m, draft.TotalAmount);
        Assert.Equal(15750m, draft.BalanceDue);
        Assert.Equal(0, await _context.QueryAsync(db => db.CustomerAccountTransactions.CountAsync()));

        var issued = await BillingTestData.IssueAsync(_context, draft.Id);

        Assert.Equal(InvoiceStatus.Issued, issued.Status);
        Assert.Equal($"INV-{_context.Clock.UtcNow.Year}-000001", issued.InvoiceNumber);
        Assert.Equal(issued.InvoiceDateUtc!.Value.AddDays(30), issued.DueDateUtc);

        var ledger = await _context.QueryAsync(db => db.CustomerAccountTransactions.ToListAsync());
        var debit = Assert.Single(ledger);
        Assert.Equal(CustomerAccountTransactionType.InvoiceIssued, debit.TransactionType);
        Assert.Equal(15750m, debit.Debit);
        Assert.Equal(issued.InvoiceNumber, debit.DocumentNumber);

        var outbox = Assert.Single(await _context.QueryAsync(db => db.OutboxMessages.ToListAsync()));
        Assert.Equal("InvoiceIssued", outbox.EventType);
        Assert.Contains(issued.InvoiceNumber!, outbox.PayloadJson);
        Assert.Null(outbox.ProcessedAtUtc);
    }

    [Fact]
    public async Task Shipment_CannotBeInvoicedTwice_UntilTheFirstInvoiceIsCancelled()
    {
        var shipment = await NewShipmentAsync();
        var first = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);

        await Assert.ThrowsAsync<ConflictException>(() => BillingTestData.InvoiceShipmentAsync(_context, shipment.Id));
        await Assert.ThrowsAsync<ConflictException>(() => Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = shipment.SenderCustomerId, ShipmentIds = new List<long> { shipment.Id } },
            CancellationToken.None)));

        await Use(s => s.CancelAsync(first.Id, new CancelInvoiceRequest { Reason = "أُنشئت بالخطأ" }, CancellationToken.None));

        var second = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(1, await _context.QueryAsync(db => db.InvoiceLines.CountAsync(x => x.ShipmentId == shipment.Id && x.IsActive)));
    }

    [Fact]
    public async Task Consolidated_BillsSeveralShipmentsOfTheSameSender_InOneInvoice()
    {
        var a = await NewShipmentAsync();
        var b = await NewShipmentAsync(weightKg: 9m);

        var invoice = await Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = a.SenderCustomerId, ShipmentIds = new List<long> { a.Id, b.Id }, IssueImmediately = true },
            CancellationToken.None));

        Assert.True(invoice.IsConsolidated);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(38000m, invoice.TotalAmount); // 15,000 + 23,000
    }

    [Fact]
    public async Task Consolidated_WithAShipmentOfAnotherSender_IsRefused()
    {
        var mine = await NewShipmentAsync();
        var other = await NewShipmentAsync(sender: "CUS-00007");

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = mine.SenderCustomerId, ShipmentIds = new List<long> { mine.Id, other.Id } },
            CancellationToken.None)));
    }

    [Fact]
    public async Task ChargesOnTheRecipient_AreInvoicedToTheRecipient_NotToTheSender()
    {
        var request = ShipmentTestData.NewRequest(_ids, sender: "CUS-00003", recipient: "CUS-00002");
        request.ChargesPaidBy = ChargesPayer.Recipient;
        var shipment = await ShipmentTestData.CreateAsync(_context, request);
        Assert.Equal(ChargesPayer.Recipient, shipment.ChargesPaidBy);

        // The sender cannot be invoiced for it...
        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = shipment.SenderCustomerId, ShipmentIds = new List<long> { shipment.Id } },
            CancellationToken.None)));

        // ...the invoice from the shipment goes to the recipient.
        var draft = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);
        Assert.Equal(shipment.RecipientCustomerId, draft.CustomerId);
        Assert.Equal(15000m, Assert.Single(draft.Lines).TaxableAmount);
    }

    [Fact]
    public async Task Consolidated_ByPeriod_TakesEveryShipmentThatIsNotInvoicedYet()
    {
        var from = _context.Clock.UtcNow.AddMinutes(-1);
        var invoiced = await NewShipmentAsync();
        await BillingTestData.InvoiceShipmentAsync(_context, invoiced.Id);
        await NewShipmentAsync();
        await NewShipmentAsync(weightKg: 7m);

        var eligible = await Use(s => s.GetEligibleShipmentsAsync(
            new EligibleShipmentsQuery { CustomerId = invoiced.SenderCustomerId }, CancellationToken.None));
        Assert.Equal(2, eligible.TotalCount);

        var invoice = await Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = invoiced.SenderCustomerId, FromUtc = from, ToUtc = _context.Clock.UtcNow.AddMinutes(1) },
            CancellationToken.None));

        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(34000m, invoice.TotalAmount); // 15,000 + 19,000
    }

    [Fact]
    public async Task DraftInvoiceDiscount_IsSpreadOverTheLines_AndTheTaxIsRecalculated()
    {
        await BillingTestData.SetDefaultTaxAsync(_context, "VAT5");
        var a = await NewShipmentAsync();            // 15,000 + 750 tax
        var b = await NewShipmentAsync(weightKg: 9m); // 23,000 + 1,150 tax
        var draft = await Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = a.SenderCustomerId, ShipmentIds = new List<long> { a.Id, b.Id } },
            CancellationToken.None));
        Assert.Equal(39900m, draft.TotalAmount);

        var updated = await Use(s => s.UpdateDraftAsync(
            draft.Id,
            new UpdateDraftInvoiceRequest { PaymentTermsDays = 15, InvoiceDiscountAmount = 3800m, Notes = "خصم نهاية الشهر" },
            CancellationToken.None));

        // 10 % of each line: 1,500 and 2,300; tax 5 % of 13,500 and 20,700.
        Assert.Equal(new[] { 1500m, 2300m }, updated.Lines.OrderBy(x => x.LineNumber).Select(x => x.InvoiceDiscountAmount).ToArray());
        Assert.Equal(3800m, updated.InvoiceDiscountAmount);
        Assert.Equal(34200m, updated.TaxableAmount);
        Assert.Equal(1710m, updated.TaxAmount);
        Assert.Equal(35910m, updated.TotalAmount);
        Assert.Equal(15, updated.PaymentTermsDays);
    }

    [Fact]
    public async Task IssuedInvoice_CannotBeEdited_OrIssuedAgain()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.UpdateDraftAsync(
            invoice.Id, new UpdateDraftInvoiceRequest { PaymentTermsDays = 10 }, CancellationToken.None)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => BillingTestData.IssueAsync(_context, invoice.Id));
    }

    [Fact]
    public async Task CancellingAnIssuedInvoice_ReversesTheDebit_AndFreesTheShipment()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);

        var cancelled = await Use(s => s.CancelAsync(invoice.Id, new CancelInvoiceRequest { Reason = "عنوان الفوترة خاطئ" }, CancellationToken.None));

        Assert.Equal(InvoiceStatus.Cancelled, cancelled.Status);
        Assert.Equal(0m, cancelled.BalanceDue);
        var ledger = await _context.QueryAsync(db => db.CustomerAccountTransactions.OrderBy(x => x.Id).ToListAsync());
        Assert.Equal(new[] { CustomerAccountTransactionType.InvoiceIssued, CustomerAccountTransactionType.InvoiceCancelled }, ledger.Select(x => x.TransactionType).ToArray());
        Assert.Equal(0m, ledger.Sum(x => x.Debit - x.Credit));
        Assert.Contains(await _context.QueryAsync(db => db.OutboxMessages.Select(x => x.EventType).ToListAsync()), x => x == "InvoiceCancelled");

        var again = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);
        Assert.Equal(InvoiceStatus.Draft, again.Status);
    }

    [Fact]
    public async Task CancellingAnInvoiceWithAPayment_IsRefused()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);
        await BillingTestData.PayAsync(_context, invoice.CustomerId, 1000m, allocations: new[] { (invoice.Id, 1000m) });

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CancelAsync(
            invoice.Id, new CancelInvoiceRequest { Reason = "محاولة إلغاء" }, CancellationToken.None)));
    }

    [Fact]
    public async Task CancellingAnInvoicedShipment_IsRefused()
    {
        var shipment = await NewShipmentAsync();
        await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<IShipmentService, ShipmentDto>(s =>
            s.CancelAsync(shipment.Id, new CancelShipmentRequest { Reason = "طلب العميل" }, CancellationToken.None)));
    }

    [Fact]
    public async Task PartialCreditNote_ReversesTheTaxAtTheLineRate_AndReducesTheBalance()
    {
        await BillingTestData.SetDefaultTaxAsync(_context, "VAT5");
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);
        var line = Assert.Single(invoice.Lines);

        var creditNote = await Use(s => s.CreateCreditNoteAsync(invoice.Id, new CreateCreditNoteRequest
        {
            Reason = "تأخر التسليم",
            Lines = new List<CreditNoteLineRequest> { new() { InvoiceLineId = line.Id, TaxableAmount = 5000m } }
        }, CancellationToken.None));

        Assert.Equal($"CRN-{_context.Clock.UtcNow.Year}-000001", creditNote.CreditNoteNumber);
        Assert.Equal(5000m, creditNote.TaxableAmount);
        Assert.Equal(250m, creditNote.TaxAmount);
        Assert.Equal(5250m, creditNote.TotalAmount);
        Assert.Equal(5250m, creditNote.AppliedToInvoiceAmount);

        var after = await BillingTestData.GetInvoiceAsync(_context, invoice.Id);
        Assert.Equal(InvoiceStatus.PartiallyPaid, after.Status);
        Assert.Equal(10500m, after.BalanceDue);
        Assert.Equal(10000m, Assert.Single(after.Lines).CreditableAmount);
        Assert.Single(after.CreditNotes);
    }

    [Fact]
    public async Task CreditAll_MakesTheInvoiceCredited_AndTheShipmentCanBeBilledAgain()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);

        await Use(s => s.CreateCreditNoteAsync(invoice.Id, new CreateCreditNoteRequest { Reason = "الشحنة لم تُنفذ", CreditAll = true }, CancellationToken.None));

        var after = await BillingTestData.GetInvoiceAsync(_context, invoice.Id);
        Assert.Equal(InvoiceStatus.Credited, after.Status);
        Assert.Equal(0m, after.BalanceDue);
        Assert.Equal(0m, await _context.QueryAsync(db => db.CustomerAccountTransactions.SumAsync(x => x.Debit - x.Credit)));

        // Nothing left to credit.
        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CreateCreditNoteAsync(
            invoice.Id, new CreateCreditNoteRequest { Reason = "مرة ثانية", CreditAll = true }, CancellationToken.None)));

        var rebilled = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);
        Assert.Equal(InvoiceStatus.Draft, rebilled.Status);
    }

    [Fact]
    public async Task CreditNote_LargerThanWhatIsLeftOnTheLine_IsRejected()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);
        var lineId = Assert.Single(invoice.Lines).Id;

        await Use(s => s.CreateCreditNoteAsync(invoice.Id, new CreateCreditNoteRequest
        {
            Reason = "خصم أول",
            Lines = new List<CreditNoteLineRequest> { new() { InvoiceLineId = lineId, TaxableAmount = 5000m } }
        }, CancellationToken.None));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => Use(s => s.CreateCreditNoteAsync(invoice.Id, new CreateCreditNoteRequest
        {
            Reason = "خصم ثانٍ",
            Lines = new List<CreditNoteLineRequest> { new() { InvoiceLineId = lineId, TaxableAmount = 10001m } }
        }, CancellationToken.None)));

        Assert.True(exception.Errors.ContainsKey("lines[0].taxableAmount"));
    }

    [Fact]
    public async Task CreditNote_OnAPaidInvoice_BecomesUnappliedCustomerCredit()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);
        await BillingTestData.PayAsync(_context, invoice.CustomerId, 15000m, allocations: new[] { (invoice.Id, 15000m) });

        var creditNote = await Use(s => s.CreateCreditNoteAsync(invoice.Id, new CreateCreditNoteRequest
        {
            Reason = "تعويض عن تأخير",
            Lines = new List<CreditNoteLineRequest> { new() { InvoiceLineId = invoice.Lines[0].Id, TaxableAmount = 2000m } }
        }, CancellationToken.None));

        Assert.Equal(0m, creditNote.AppliedToInvoiceAmount);
        Assert.Equal(2000m, creditNote.UnappliedAmount);
        Assert.Equal(InvoiceStatus.Paid, (await BillingTestData.GetInvoiceAsync(_context, invoice.Id)).Status);

        // The customer is now in credit by 2,000 (we owe him).
        Assert.Equal(-2000m, await _context.QueryAsync(db => db.CustomerAccountTransactions.SumAsync(x => x.Debit - x.Credit)));
    }

    [Fact]
    public async Task CreditNote_OnADraft_IsRefused()
    {
        var shipment = await NewShipmentAsync();
        var draft = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CreateCreditNoteAsync(
            draft.Id, new CreateCreditNoteRequest { Reason = "مسودة", CreditAll = true }, CancellationToken.None)));
    }

    [Fact]
    public async Task OverdueJob_MarksOpenInvoicesPastTheirDueDate()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true, paymentTermsDays: 10);

        Assert.Equal(0, await Use(s => s.RefreshOverdueAsync(CancellationToken.None)));

        _context.Clock.Advance(TimeSpan.FromDays(11));
        Assert.Equal(1, await Use(s => s.RefreshOverdueAsync(CancellationToken.None)));
        Assert.Equal(InvoiceStatus.Overdue, (await BillingTestData.GetInvoiceAsync(_context, invoice.Id)).Status);

        var overdue = await Use(s => s.GetPagedAsync(new InvoiceListQuery { Status = InvoiceStatus.Overdue }, CancellationToken.None));
        Assert.Equal(1, overdue.TotalCount);
    }

    [Fact]
    public async Task InvoiceList_SupportsFiltersSearchAndSorting()
    {
        var a = await NewShipmentAsync();
        var b = await NewShipmentAsync(weightKg: 9m);
        var issued = await BillingTestData.InvoiceShipmentAsync(_context, a.Id, issue: true);
        await BillingTestData.InvoiceShipmentAsync(_context, b.Id);

        var drafts = await Use(s => s.GetPagedAsync(new InvoiceListQuery { Status = InvoiceStatus.Draft }, CancellationToken.None));
        var bySearch = await Use(s => s.GetPagedAsync(new InvoiceListQuery { Search = issued.InvoiceNumber }, CancellationToken.None));
        var byAmount = await Use(s => s.GetPagedAsync(new InvoiceListQuery { SortBy = "totalAmount", SortDirection = "desc" }, CancellationToken.None));
        var withBalance = await Use(s => s.GetPagedAsync(new InvoiceListQuery { HasBalance = true }, CancellationToken.None));
        var byShipment = await Use(s => s.GetPagedAsync(new InvoiceListQuery { ShipmentId = b.Id }, CancellationToken.None));

        Assert.Equal(1, drafts.TotalCount);
        Assert.Equal(issued.Id, Assert.Single(bySearch.Items).Id);
        Assert.Equal(new[] { 23000m, 15000m }, byAmount.Items.Select(x => x.TotalAmount).ToArray());
        Assert.Equal(issued.Id, Assert.Single(withBalance.Items).Id);
        Assert.Equal(InvoiceStatus.Draft, Assert.Single(byShipment.Items).Status);
    }

    [Fact]
    public async Task Pdf_AndExcelExport_AreGenerated()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);

        var pdf = await Use(s => s.GetPdfAsync(invoice.Id, CancellationToken.None));
        var excel = await Use(s => s.ExportAsync(new InvoiceListQuery(), CancellationToken.None));

        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.Equal($"{invoice.InvoiceNumber}.pdf", pdf.FileName);
        Assert.Equal("%PDF", await Header(pdf, 4));
        Assert.EndsWith(".xlsx", excel.FileName);
        Assert.Equal("PK", await Header(excel, 2));
    }

    private static async Task<string> Header(FileDownload file, int length)
    {
        await using var stream = file.Content;
        var buffer = new byte[length];
        var read = await stream.ReadAsync(buffer);
        return Encoding.ASCII.GetString(buffer, 0, read);
    }
}
```

### 6.29 `tests/ShippingERP.Tests/Unit/Billing/PricingEngineTests.cs`

```csharp
using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Pricing;

namespace ShippingERP.Tests.Unit.Billing;

/// <summary>The pure pricing engine: rule matching and the price breakdown (no database).</summary>
public sealed class PricingEngineTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

    private static PriceRule Rule(long id, int priority = 10, params PriceRuleCondition[] conditions)
    {
        var rule = new PriceRule { Id = id, ArabicName = $"R{id}", Priority = priority, IsActive = true, IncludedPackageCount = 1 };
        foreach (var condition in conditions)
        {
            rule.Conditions.Add(condition);
        }

        return rule;
    }

    private static PriceRuleCondition Ref(PriceConditionType type, long id) => new() { ConditionType = type, ReferenceId = id };

    private static PriceRuleCondition Range(PriceConditionType type, decimal? min, decimal? max) => new() { ConditionType = type, MinValue = min, MaxValue = max };

    private static PricingContext Context(long serviceType = 1, long? destinationZone = 100, decimal weight = 3m, int packages = 1) => new()
    {
        ServiceTypeId = serviceType,
        DestinationZoneId = destinationZone,
        OriginZoneId = 200,
        OriginCityId = 10,
        DestinationCityId = 20,
        ChargeableWeight = weight,
        PackageCount = packages
    };

    // ------------------------------------------------------------------ matching

    [Fact]
    public void Conditions_OfTheSameType_AreAlternatives_DifferentTypes_MustAllMatch()
    {
        var rule = Rule(1, 10,
            Ref(PriceConditionType.DestinationZone, 100),
            Ref(PriceConditionType.DestinationZone, 101),
            Ref(PriceConditionType.ServiceType, 1));

        Assert.True(PriceRuleMatcher.Matches(rule, Context(destinationZone: 100), Now));
        Assert.True(PriceRuleMatcher.Matches(rule, Context(destinationZone: 101), Now));
        Assert.False(PriceRuleMatcher.Matches(rule, Context(destinationZone: 102), Now));
        Assert.False(PriceRuleMatcher.Matches(rule, Context(serviceType: 2, destinationZone: 100), Now));

        // A zone condition never matches a shipment without a zone.
        Assert.False(PriceRuleMatcher.Matches(rule, Context(destinationZone: null), Now));
    }

    [Fact]
    public void RangeConditions_AreInclusiveMinAndExclusiveMax()
    {
        var rule = Rule(1, 10, Range(PriceConditionType.ChargeableWeight, 5m, 10m), Range(PriceConditionType.PackageCount, 2m, null));

        Assert.True(PriceRuleMatcher.Matches(rule, Context(weight: 5m, packages: 2), Now));
        Assert.True(PriceRuleMatcher.Matches(rule, Context(weight: 9.999m, packages: 50), Now));
        Assert.False(PriceRuleMatcher.Matches(rule, Context(weight: 10m, packages: 2), Now));
        Assert.False(PriceRuleMatcher.Matches(rule, Context(weight: 6m, packages: 1), Now));
    }

    [Fact]
    public void SelectBest_PrefersPriority_ThenSpecificity_ThenTheOldestRule()
    {
        var general = Rule(1, 10);
        var specific = Rule(2, 10, Ref(PriceConditionType.ServiceType, 1), Ref(PriceConditionType.DestinationZone, 100));
        var otherSpecific = Rule(3, 10, Ref(PriceConditionType.ServiceType, 1), Ref(PriceConditionType.OriginZone, 200));
        var promotion = Rule(4, 50, Range(PriceConditionType.ChargeableWeight, 0m, 2m));

        var rules = new[] { general, specific, otherSpecific, promotion };

        // Weight 3: the promotion does not match; two rules with 2 condition types: the older one (Id 2) wins.
        Assert.Same(specific, PriceRuleMatcher.SelectBest(rules, Context(weight: 3m), Now));

        // Weight 1: the higher priority wins although it is less specific.
        Assert.Same(promotion, PriceRuleMatcher.SelectBest(rules, Context(weight: 1m), Now));
    }

    [Fact]
    public void InactiveRules_AndRulesOutsideTheirValidity_NeverMatch()
    {
        var inactive = Rule(1);
        inactive.IsActive = false;
        var future = Rule(2);
        future.ValidFromUtc = Now.AddDays(1);
        var expired = Rule(3);
        expired.ValidToUtc = Now;

        Assert.Null(PriceRuleMatcher.SelectBest(new[] { inactive, future, expired }, Context(), Now));
    }

    // ------------------------------------------------------------------ breakdown

    private static PriceRule DemoStandard()
    {
        var rule = Rule(1);
        rule.BasePrice = 15000m;
        rule.IncludedWeightKg = 5m;
        rule.WeightTiers.Add(new PriceRuleWeightTier { FromWeightKg = 5m, ToWeightKg = 20m, PricePerKg = 2000m });
        rule.WeightTiers.Add(new PriceRuleWeightTier { FromWeightKg = 20m, ToWeightKg = null, PricePerKg = 1500m });
        rule.CodFeeFixed = 2000m;
        rule.CodFeePercentage = 1m;
        return rule;
    }

    [Theory]
    [InlineData(2.5, 15000, 0)]
    [InlineData(5, 15000, 0)]
    [InlineData(9, 23000, 8000)]
    [InlineData(25, 52500, 37500)] // 15 x 2,000 + 5 x 1,500
    public void WeightTiers_AreProgressive_AboveTheIncludedWeight(decimal weight, decimal subtotal, decimal weightCharge)
    {
        var breakdown = PriceBreakdownCalculator.Calculate(DemoStandard(), new PriceBreakdownInput { ChargeableWeight = weight, PackageCount = 1 }, 0m, 0);

        Assert.Equal(15000m, breakdown.BasePrice);
        Assert.Equal(weightCharge, breakdown.WeightCharge);
        Assert.Equal(subtotal, breakdown.ShippingSubtotal);
        Assert.Equal(subtotal, breakdown.NetTotal);
    }

    [Fact]
    public void ServiceFees_DiscountCodCommissionAndTax_FollowTheDocumentedFormula()
    {
        var rule = DemoStandard();
        rule.IncludedPackageCount = 1;
        rule.PerPackageFee = 1000m;
        rule.InsuranceFeePercentage = 1m;
        rule.InsuranceFeeMinimum = 1000m;
        rule.PickupFee = 3000m;
        rule.PackagingFee = 2500m;
        rule.RemoteAreaFee = 5000m;
        rule.ReturnFee = 7500m;
        rule.DiscountPercentage = 10m;

        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
        {
            ChargeableWeight = 6m,
            PackageCount = 3,
            InsuranceValue = 300000m,
            RequiresPickup = true,
            RequiresPackaging = true,
            IsRemoteArea = true,
            IsReturn = false,
            IsCod = true,
            CodAmount = 100000m
        }, taxPercentage: 5m, decimals: 0);

        Assert.Equal(15000m, breakdown.BasePrice);
        Assert.Equal(2000m, breakdown.WeightCharge);          // 1 kg above 5
        Assert.Equal(2000m, breakdown.PackageFee);            // 2 extra packages
        Assert.Equal(3000m, breakdown.InsuranceFee);          // 1 % of 300,000
        Assert.Equal(3000m, breakdown.PickupFee);
        Assert.Equal(2500m, breakdown.PackagingFee);
        Assert.Equal(5000m, breakdown.RemoteAreaFee);
        Assert.Equal(0m, breakdown.ReturnFee);
        Assert.Equal(15500m, breakdown.ServiceFeesTotal);
        Assert.Equal(32500m, breakdown.ShippingSubtotal);
        Assert.Equal(3250m, breakdown.ContractDiscountAmount); // 10 %
        Assert.Equal(29250m, breakdown.TotalShippingCharge);
        Assert.Equal(3000m, breakdown.CodCommission);         // 2,000 + 1 % x 100,000
        Assert.Equal(32250m, breakdown.TaxableAmount);
        Assert.Equal(1613m, breakdown.TaxAmount);             // 5 % = 1,612.5 -> 1,613 (SYP, 0 decimals, away from zero)
        Assert.Equal(33863m, breakdown.NetTotal);
        Assert.Equal(35500m, breakdown.GrossCharge);          // subtotal + COD commission
    }

    [Fact]
    public void Minimums_ApplyToSmallInsuranceAndCodAmounts()
    {
        var rule = DemoStandard();
        rule.InsuranceFeePercentage = 1m;
        rule.InsuranceFeeMinimum = 1000m;
        rule.CodFeeFixed = 0m;
        rule.CodFeePercentage = 1m;
        rule.CodFeeMinimum = 2500m;

        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
        {
            ChargeableWeight = 1m,
            PackageCount = 1,
            InsuranceValue = 20000m,
            IsCod = true,
            CodAmount = 50000m
        }, 0m, 0);

        Assert.Equal(1000m, breakdown.InsuranceFee);
        Assert.Equal(2500m, breakdown.CodCommission);
    }

    [Fact]
    public void ManualCharge_ReplacesTheRuleSubtotal_ButKeepsTheCodCommission()
    {
        var rule = DemoStandard();
        rule.DiscountPercentage = 10m;

        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
        {
            ChargeableWeight = 9m,
            PackageCount = 1,
            IsCod = true,
            CodAmount = 10000m,
            ManualShippingCharge = 20000m,
            ManualDiscountAmount = 1000m
        }, 0m, 0);

        Assert.Equal(20000m, breakdown.ShippingSubtotal);
        Assert.Equal(23000m, breakdown.CalculatedShippingSubtotal);
        Assert.Equal(0m, breakdown.ContractDiscountAmount);   // no contract discount on a manual price
        Assert.Equal(1000m, breakdown.ManualDiscountAmount);
        Assert.Equal(2100m, breakdown.CodCommission);
        Assert.Equal(21100m, breakdown.NetTotal);
    }

    [Fact]
    public void Amounts_AreRoundedToTheCurrencyDecimals()
    {
        var rule = Rule(1);
        rule.BasePrice = 10m;
        rule.WeightTiers.Add(new PriceRuleWeightTier { FromWeightKg = 0m, PricePerKg = 0.333m });

        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput { ChargeableWeight = 2.5m, PackageCount = 1 }, 15m, 2);

        Assert.Equal(0.83m, breakdown.WeightCharge);          // 0.8325
        Assert.Equal(10.83m, breakdown.ShippingSubtotal);
        Assert.Equal(1.62m, breakdown.TaxAmount);             // 1.6245
        Assert.Equal(12.45m, breakdown.NetTotal);
    }

    [Fact]
    public void WithoutRuleAndWithoutManualCharge_TheEngineRefuses()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PriceBreakdownCalculator.Calculate(null, new PriceBreakdownInput { ChargeableWeight = 1m, PackageCount = 1 }, 0m, 0));
    }

    // ------------------------------------------------------------------ step 9: weight or volume

    [Fact]
    public void RuleWithAppliesToBasis_OnlyMatchesShipmentsPricedThatWay_AndIsMoreSpecific()
    {
        var any = Rule(1);
        var perCbm = Rule(2);
        perCbm.AppliesToBasis = PricingBasis.Volume;

        var weightContext = new PricingContext { ChargeableWeight = 3m, PackageCount = 1, PricingBasis = PricingBasis.Weight };
        var volumeContext = new PricingContext { ChargeableWeight = 3m, PackageCount = 1, PricingBasis = PricingBasis.Volume };

        Assert.False(PriceRuleMatcher.Matches(perCbm, weightContext, Now));
        Assert.True(PriceRuleMatcher.Matches(perCbm, volumeContext, Now));
        Assert.Same(any, PriceRuleMatcher.SelectBest(new[] { any, perCbm }, weightContext, Now));
        Assert.Same(perCbm, PriceRuleMatcher.SelectBest(new[] { any, perCbm }, volumeContext, Now));
    }

    [Fact]
    public void Volume_ChargesTheCubicMetres_AndIgnoresTheWeightTiers()
    {
        var rule = DemoStandard();
        rule.PricePerCubicMeter = 95m;

        // 2.5 m³ x 95 = 237.5, plus the base price; the 40 kg would cost tiers in weight pricing.
        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
        {
            ChargeableWeight = 40m,
            PackageCount = 1,
            PricingBasis = PricingBasis.Volume,
            VolumeCbm = 2.5m
        }, 0m, 2);

        Assert.Equal(237.5m, breakdown.VolumeCharge);
        Assert.Equal(0m, breakdown.WeightCharge);
        Assert.Empty(breakdown.WeightTiers);
        Assert.Equal(15000m + 237.5m, breakdown.ShippingSubtotal);

        var byWeight = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
        {
            ChargeableWeight = 40m,
            PackageCount = 1,
            PricingBasis = PricingBasis.Weight,
            VolumeCbm = 2.5m
        }, 0m, 2);

        Assert.Equal(0m, byWeight.VolumeCharge);
        Assert.True(byWeight.WeightCharge > 0m);
    }

    [Fact]
    public void PerKgOnly_FourDinarsAKilo()
    {
        var rule = Rule(1);
        rule.WeightTiers.Add(new PriceRuleWeightTier { FromWeightKg = 0m, PricePerKg = 4m });

        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput { ChargeableWeight = 12.5m, PackageCount = 3 }, 0m, 3);

        Assert.Equal(50m, breakdown.ShippingSubtotal);
    }
}
```

### 6.30 `frontend/src/api/generated.ts`

> ملف **مولّد** بالسكربت `frontend/scripts/generate-api-types.py` من DTOs الخادم — لا يُعدَّل يدوياً.

```typescript
/* eslint-disable */
// AUTO-GENERATED by scripts/generate-api-types.py from the C# DTOs and enums. Do not edit by hand.
import type { PagedResult } from './types';

export type AccountType = 'Assets' | 'Liabilities' | 'Equity' | 'Revenue' | 'CostOfRevenue' | 'Expenses';
export const AccountTypeValues = ['Assets', 'Liabilities', 'Equity', 'Revenue', 'CostOfRevenue', 'Expenses'] as const;

export type AccountingPeriodStatus = 'Open' | 'Closed';
export const AccountingPeriodStatusValues = ['Open', 'Closed'] as const;

export type AuditAction = 'Login' | 'LoginFailed' | 'RefreshTokenRevoked' | 'RefreshTokenReuseDetected' | 'PasswordChanged' | 'UserCreated' | 'UserUpdated' | 'UserActivated' | 'UserDeactivated' | 'RoleCreated' | 'RolePermissionsChanged' | 'MasterDataCreated' | 'MasterDataUpdated' | 'MasterDataActivated' | 'MasterDataDeactivated' | 'MasterDataDeleted' | 'ShipmentCreated' | 'ShipmentUpdated' | 'ShipmentCancelled' | 'ShipmentStatusCorrected' | 'ShipmentsImported' | 'ShipmentAttachmentUploaded' | 'ShipmentAttachmentDeleted' | 'ShipmentDriverAssigned' | 'ShippingSettingsChanged' | 'ShipmentRepriced' | 'PriceOverrideRequested' | 'PriceOverrideApproved' | 'PriceOverrideRejected' | 'InvoiceCreated' | 'InvoiceUpdated' | 'InvoiceIssued' | 'InvoiceCancelled' | 'CreditNoteIssued' | 'CustomerPaymentRecorded' | 'CustomerPaymentAllocated' | 'CodCollected' | 'DriverCashHandedOver' | 'CodMarkedReady' | 'CodDisputeOpened' | 'CodDisputeResolved' | 'CodAdjustmentRequested' | 'CodAdjustmentApproved' | 'CodAdjustmentRejected' | 'SenderSettlementCreated' | 'SenderSettlementApproved' | 'SenderSettlementPaid' | 'SenderSettlementCancelled' | 'SenderSettlementReversed' | 'CashboxCreated' | 'CashboxUpdated' | 'CashReceiptPosted' | 'CashPaymentPosted' | 'CashVoucherCancelled' | 'CashTransferRequested' | 'CashTransferApproved' | 'CashTransferRejected' | 'CashTransferReceived' | 'CashTransferCancelled' | 'CashboxDayClosed' | 'CashboxClosingApproved' | 'CashboxClosingRejected' | 'BankAccountSaved' | 'AccountSaved' | 'AccountDeleted' | 'AccountCategorySaved' | 'AccountingDimensionSaved' | 'FiscalYearCreated' | 'FiscalYearUpdated' | 'FiscalYearDeleted' | 'FiscalYearClosed' | 'AccountingPeriodClosed' | 'AccountingPeriodReopened' | 'JournalEntryCreated' | 'JournalEntryUpdated' | 'JournalEntryDeleted' | 'JournalEntrySubmitted' | 'JournalEntryApproved' | 'JournalEntryRejected' | 'JournalEntryPosted' | 'JournalEntryReversed' | 'PostingRuleSaved' | 'PostingRuleDeleted' | 'PostingEventRetried' | 'BankStatementImported' | 'BankReconciliationSaved' | 'BankReconciliationCompleted' | 'AccountingPeriodUpdated';
export const AuditActionValues = ['Login', 'LoginFailed', 'RefreshTokenRevoked', 'RefreshTokenReuseDetected', 'PasswordChanged', 'UserCreated', 'UserUpdated', 'UserActivated', 'UserDeactivated', 'RoleCreated', 'RolePermissionsChanged', 'MasterDataCreated', 'MasterDataUpdated', 'MasterDataActivated', 'MasterDataDeactivated', 'MasterDataDeleted', 'ShipmentCreated', 'ShipmentUpdated', 'ShipmentCancelled', 'ShipmentStatusCorrected', 'ShipmentsImported', 'ShipmentAttachmentUploaded', 'ShipmentAttachmentDeleted', 'ShipmentDriverAssigned', 'ShippingSettingsChanged', 'ShipmentRepriced', 'PriceOverrideRequested', 'PriceOverrideApproved', 'PriceOverrideRejected', 'InvoiceCreated', 'InvoiceUpdated', 'InvoiceIssued', 'InvoiceCancelled', 'CreditNoteIssued', 'CustomerPaymentRecorded', 'CustomerPaymentAllocated', 'CodCollected', 'DriverCashHandedOver', 'CodMarkedReady', 'CodDisputeOpened', 'CodDisputeResolved', 'CodAdjustmentRequested', 'CodAdjustmentApproved', 'CodAdjustmentRejected', 'SenderSettlementCreated', 'SenderSettlementApproved', 'SenderSettlementPaid', 'SenderSettlementCancelled', 'SenderSettlementReversed', 'CashboxCreated', 'CashboxUpdated', 'CashReceiptPosted', 'CashPaymentPosted', 'CashVoucherCancelled', 'CashTransferRequested', 'CashTransferApproved', 'CashTransferRejected', 'CashTransferReceived', 'CashTransferCancelled', 'CashboxDayClosed', 'CashboxClosingApproved', 'CashboxClosingRejected', 'BankAccountSaved', 'AccountSaved', 'AccountDeleted', 'AccountCategorySaved', 'AccountingDimensionSaved', 'FiscalYearCreated', 'FiscalYearUpdated', 'FiscalYearDeleted', 'FiscalYearClosed', 'AccountingPeriodClosed', 'AccountingPeriodReopened', 'JournalEntryCreated', 'JournalEntryUpdated', 'JournalEntryDeleted', 'JournalEntrySubmitted', 'JournalEntryApproved', 'JournalEntryRejected', 'JournalEntryPosted', 'JournalEntryReversed', 'PostingRuleSaved', 'PostingRuleDeleted', 'PostingEventRetried', 'BankStatementImported', 'BankReconciliationSaved', 'BankReconciliationCompleted', 'AccountingPeriodUpdated'] as const;

export type BankReconciliationLineType = 'Matched' | 'BankOnly' | 'BookOnly';
export const BankReconciliationLineTypeValues = ['Matched', 'BankOnly', 'BookOnly'] as const;

export type BankReconciliationStatus = 'Draft' | 'Completed';
export const BankReconciliationStatusValues = ['Draft', 'Completed'] as const;

export type BranchTransferItemStatus = 'Pending' | 'InTransit' | 'Received' | 'Missing';
export const BranchTransferItemStatusValues = ['Pending', 'InTransit', 'Received', 'Missing'] as const;

export type BranchTransferStatus = 'Draft' | 'Dispatched' | 'Received' | 'PartiallyReceived' | 'Cancelled';
export const BranchTransferStatusValues = ['Draft', 'Dispatched', 'Received', 'PartiallyReceived', 'Cancelled'] as const;

export type CODAdjustmentStatus = 'PendingApproval' | 'Approved' | 'Rejected';
export const CODAdjustmentStatusValues = ['PendingApproval', 'Approved', 'Rejected'] as const;

export type CODAdjustmentType = 'DriverShortageWriteOff' | 'DriverExcessCash' | 'CollectionCorrection' | 'CollectionReversal' | 'SenderFee' | 'SenderCompensation';
export const CODAdjustmentTypeValues = ['DriverShortageWriteOff', 'DriverExcessCash', 'CollectionCorrection', 'CollectionReversal', 'SenderFee', 'SenderCompensation'] as const;

export type CODStatus = 'Pending' | 'AssignedToDriver' | 'CollectedByDriver' | 'ReceivedAtBranchCashbox' | 'ReadyForSenderSettlement' | 'PartiallySettled' | 'SettledToSender' | 'Cancelled' | 'Disputed';
export const CODStatusValues = ['Pending', 'AssignedToDriver', 'CollectedByDriver', 'ReceivedAtBranchCashbox', 'ReadyForSenderSettlement', 'PartiallySettled', 'SettledToSender', 'Cancelled', 'Disputed'] as const;

export type CODTransactionType = 'Registered' | 'AssignedToDriver' | 'Unassigned' | 'ExpectedAmountChanged' | 'Cancelled' | 'Reopened' | 'Collected' | 'CollectedAtCounter' | 'HandedOver' | 'HandoverReversed' | 'MarkedReady' | 'SettlementPaid' | 'SettlementReversed' | 'CollectionCorrected' | 'CollectionReversed' | 'SenderFeeAdded' | 'SenderCompensationAdded' | 'DisputeOpened' | 'DisputeResolved';
export const CODTransactionTypeValues = ['Registered', 'AssignedToDriver', 'Unassigned', 'ExpectedAmountChanged', 'Cancelled', 'Reopened', 'Collected', 'CollectedAtCounter', 'HandedOver', 'HandoverReversed', 'MarkedReady', 'SettlementPaid', 'SettlementReversed', 'CollectionCorrected', 'CollectionReversed', 'SenderFeeAdded', 'SenderCompensationAdded', 'DisputeOpened', 'DisputeResolved'] as const;

export type CashFlowActivity = 'None' | 'Operating' | 'Investing' | 'Financing';
export const CashFlowActivityValues = ['None', 'Operating', 'Investing', 'Financing'] as const;

export type CashPaymentType = 'SenderSettlement' | 'Expense' | 'SupplierPayment' | 'EmployeeAdvance' | 'CustomerRefund' | 'Other';
export const CashPaymentTypeValues = ['SenderSettlement', 'Expense', 'SupplierPayment', 'EmployeeAdvance', 'CustomerRefund', 'Other'] as const;

export type CashReceiptType = 'DriverHandover' | 'CounterCodCollection' | 'CustomerPayment' | 'OtherIncome' | 'Other';
export const CashReceiptTypeValues = ['DriverHandover', 'CounterCodCollection', 'CustomerPayment', 'OtherIncome', 'Other'] as const;

export type CashTransferStatus = 'PendingApproval' | 'InTransit' | 'Completed' | 'Rejected' | 'Cancelled';
export const CashTransferStatusValues = ['PendingApproval', 'InTransit', 'Completed', 'Rejected', 'Cancelled'] as const;

export type CashTransferType = 'BetweenCashboxes' | 'BetweenBranches' | 'CashboxToBank';
export const CashTransferTypeValues = ['BetweenCashboxes', 'BetweenBranches', 'CashboxToBank'] as const;

export type CashVoucherStatus = 'Posted' | 'Cancelled';
export const CashVoucherStatusValues = ['Posted', 'Cancelled'] as const;

export type CashboxClosingStatus = 'Closed' | 'PendingApproval' | 'Approved' | 'Rejected';
export const CashboxClosingStatusValues = ['Closed', 'PendingApproval', 'Approved', 'Rejected'] as const;

export type CashboxTransactionType = 'OpeningBalance' | 'DriverHandover' | 'CounterCodCollection' | 'CashReceipt' | 'CashPayment' | 'SenderSettlementPayment' | 'TransferOut' | 'TransferIn' | 'ClosingShortage' | 'ClosingExcess' | 'Reversal';
export const CashboxTransactionTypeValues = ['OpeningBalance', 'DriverHandover', 'CounterCodCollection', 'CashReceipt', 'CashPayment', 'SenderSettlementPayment', 'TransferOut', 'TransferIn', 'ClosingShortage', 'ClosingExcess', 'Reversal'] as const;

export type ChargesPayer = 'Sender' | 'Recipient';
export const ChargesPayerValues = ['Sender', 'Recipient'] as const;

export type CustomerAccountTransactionType = 'InvoiceIssued' | 'InvoiceCancelled' | 'PaymentReceived' | 'CreditNoteIssued';
export const CustomerAccountTransactionTypeValues = ['InvoiceIssued', 'InvoiceCancelled', 'PaymentReceived', 'CreditNoteIssued'] as const;

export type CustomerRole = 'Sender' | 'Recipient' | 'Both';
export const CustomerRoleValues = ['Sender', 'Recipient', 'Both'] as const;

export type CustomerType = 'Individual' | 'Business';
export const CustomerTypeValues = ['Individual', 'Business'] as const;

export type DamageCaseResolution = 'Compensated' | 'Rejected' | 'FoundAndRecovered' | 'NoAction';
export const DamageCaseResolutionValues = ['Compensated', 'Rejected', 'FoundAndRecovered', 'NoAction'] as const;

export type DamageCaseStatus = 'Open' | 'UnderInvestigation' | 'Resolved';
export const DamageCaseStatusValues = ['Open', 'UnderInvestigation', 'Resolved'] as const;

export type DamageCaseType = 'Damaged' | 'PartiallyDamaged' | 'Lost';
export const DamageCaseTypeValues = ['Damaged', 'PartiallyDamaged', 'Lost'] as const;

export type DeliveryResult = 'Pending' | 'Delivered' | 'Failed';
export const DeliveryResultValues = ['Pending', 'Delivered', 'Failed'] as const;

export type DimensionType = 'CostCenter' | 'Project' | 'Vehicle' | 'Department';
export const DimensionTypeValues = ['CostCenter', 'Project', 'Vehicle', 'Department'] as const;

export type DriverCashMovementType = 'CodCollected' | 'Handover' | 'HandoverReversed' | 'CollectionCorrected' | 'CollectionReversed' | 'ShortageWrittenOff' | 'ExcessRecorded';
export const DriverCashMovementTypeValues = ['CodCollected', 'Handover', 'HandoverReversed', 'CollectionCorrected', 'CollectionReversed', 'ShortageWrittenOff', 'ExcessRecorded'] as const;

export type DriverManifestStatus = 'Draft' | 'Dispatched' | 'Closed' | 'Cancelled';
export const DriverManifestStatusValues = ['Draft', 'Dispatched', 'Closed', 'Cancelled'] as const;

export type ExportFormat = 'Excel' | 'Pdf';
export const ExportFormatValues = ['Excel', 'Pdf'] as const;

export type FiscalYearStatus = 'Open' | 'Closed';
export const FiscalYearStatusValues = ['Open', 'Closed'] as const;

export type InvoiceChargeType = 'ShippingCharge';
export const InvoiceChargeTypeValues = ['ShippingCharge'] as const;

export type InvoiceStatus = 'Draft' | 'Issued' | 'PartiallyPaid' | 'Paid' | 'Overdue' | 'Cancelled' | 'Credited';
export const InvoiceStatusValues = ['Draft', 'Issued', 'PartiallyPaid', 'Paid', 'Overdue', 'Cancelled', 'Credited'] as const;

export type JournalEntryStatus = 'Draft' | 'Submitted' | 'Approved' | 'Posted' | 'Reversed' | 'Rejected';
export const JournalEntryStatusValues = ['Draft', 'Submitted', 'Approved', 'Posted', 'Reversed', 'Rejected'] as const;

export type JournalType = 'General' | 'Opening' | 'Adjustment' | 'Reversal' | 'Closing' | 'Sales' | 'CustomerReceipts' | 'Cod' | 'Cash' | 'SenderSettlement' | 'Expense' | 'Bank';
export const JournalTypeValues = ['General', 'Opening', 'Adjustment', 'Reversal', 'Closing', 'Sales', 'CustomerReceipts', 'Cod', 'Cash', 'SenderSettlement', 'Expense', 'Bank'] as const;

export type LabelFormat = 'Pdf' | 'Html';
export const LabelFormatValues = ['Pdf', 'Html'] as const;

export type NormalBalance = 'Debit' | 'Credit';
export const NormalBalanceValues = ['Debit', 'Credit'] as const;

export type PaymentMethodKind = 'Cash' | 'CashOnDelivery' | 'BankTransfer' | 'Card' | 'CustomerAccount' | 'Wallet';
export const PaymentMethodKindValues = ['Cash', 'CashOnDelivery', 'BankTransfer', 'Card', 'CustomerAccount', 'Wallet'] as const;

export type PostingEventStatus = 'Posted' | 'Skipped' | 'Duplicate' | 'Failed';
export const PostingEventStatusValues = ['Posted', 'Skipped', 'Duplicate', 'Failed'] as const;

export type PriceConditionType = 'OriginZone' | 'DestinationZone' | 'OriginCity' | 'DestinationCity' | 'ServiceType' | 'ChargeableWeight' | 'PackageCount';
export const PriceConditionTypeValues = ['OriginZone', 'DestinationZone', 'OriginCity', 'DestinationCity', 'ServiceType', 'ChargeableWeight', 'PackageCount'] as const;

export type PriceOverrideStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
export const PriceOverrideStatusValues = ['Pending', 'Approved', 'Rejected', 'Cancelled'] as const;

export type PricingBasis = 'Weight' | 'Volume';
export const PricingBasisValues = ['Weight', 'Volume'] as const;

export type PricingSource = 'PricingRule' | 'Manual' | 'CustomerContract';
export const PricingSourceValues = ['PricingRule', 'Manual', 'CustomerContract'] as const;

export type SettlementStatus = 'Draft' | 'Approved' | 'Paid' | 'Cancelled' | 'Reversed';
export const SettlementStatusValues = ['Draft', 'Approved', 'Paid', 'Cancelled', 'Reversed'] as const;

export type ShipmentAttachmentType = 'Document' | 'PackagePhoto' | 'CommercialInvoice' | 'Signature' | 'DeliveryPhoto' | 'DamageEvidence' | 'Other';
export const ShipmentAttachmentTypeValues = ['Document', 'PackagePhoto', 'CommercialInvoice', 'Signature', 'DeliveryPhoto', 'DamageEvidence', 'Other'] as const;

export type ShipmentReturnStatus = 'Open' | 'Completed' | 'Cancelled';
export const ShipmentReturnStatusValues = ['Open', 'Completed', 'Cancelled'] as const;

export type ShipmentReturnType = 'FailedDelivery' | 'RecipientRefused' | 'SenderRequest' | 'AfterDelivery';
export const ShipmentReturnTypeValues = ['FailedDelivery', 'RecipientRefused', 'SenderRequest', 'AfterDelivery'] as const;

export type ShipmentStatus = 'Created' | 'PickedUp' | 'ReceivedAtBranch' | 'InWarehouse' | 'InTransit' | 'ArrivedAtDestination' | 'OutForDelivery' | 'Delivered' | 'DeliveryFailed' | 'Returned' | 'Cancelled' | 'LostOrDamaged';
export const ShipmentStatusValues = ['Created', 'PickedUp', 'ReceivedAtBranch', 'InWarehouse', 'InTransit', 'ArrivedAtDestination', 'OutForDelivery', 'Delivered', 'DeliveryFailed', 'Returned', 'Cancelled', 'LostOrDamaged'] as const;

export type ShipmentStatusChangeSource = 'Creation' | 'Import' | 'Manual' | 'WarehouseManifest' | 'BranchTransfer' | 'DriverManifest' | 'Delivery' | 'Return' | 'DamageCase' | 'Cancellation' | 'Correction';
export const ShipmentStatusChangeSourceValues = ['Creation', 'Import', 'Manual', 'WarehouseManifest', 'BranchTransfer', 'DriverManifest', 'Delivery', 'Return', 'DamageCase', 'Cancellation', 'Correction'] as const;

export type VehicleType = 'Motorcycle' | 'Car' | 'Van' | 'Truck' | 'Other';
export const VehicleTypeValues = ['Motorcycle', 'Car', 'Van', 'Truck', 'Other'] as const;

export type WarehouseManifestStatus = 'Draft' | 'Confirmed' | 'Cancelled';
export const WarehouseManifestStatusValues = ['Draft', 'Confirmed', 'Cancelled'] as const;

export type WarehouseMovementType = 'CheckIn' | 'CheckOut';
export const WarehouseMovementTypeValues = ['CheckIn', 'CheckOut'] as const;

export interface AccessTokenResult {
  token: string;
  expiresAtUtc: string;
}

export interface AccountCategoryDto {
  id: number;
  code: string;
  arabicName: string;
  englishName: string | null;
  accountType: AccountType;
  accountTypeName: string;
  cashFlowActivity: CashFlowActivity;
  cashFlowActivityName: string;
  sortOrder: number;
  isSystem: boolean;
  isActive: boolean;
  accountCount: number;
}

export interface AccountDto {
  id: number;
  code: string;
  arabicName: string;
  englishName: string | null;
  accountType: AccountType;
  accountTypeName: string;
  normalBalance: NormalBalance;
  normalBalanceName: string;
  accountCategoryId: number | null;
  accountCategoryName: string | null;
  parentAccountId: number | null;
  parentAccountCode: string | null;
  parentAccountName: string | null;
  level: number;
  isPostable: boolean;
  isSystem: boolean;
  isCashAccount: boolean;
  allowManualPosting: boolean;
  requiresCostCenter: boolean;
  currencyId: number | null;
  currencyCode: string | null;
  description: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface AccountListQuery extends PagedQuery {
  accountType?: AccountType | null;
  accountCategoryId?: number | null;
  parentAccountId?: number | null;
  isPostable?: boolean | null;
  isActive?: boolean | null;
}

export interface AccountRoleDto {
  role: string;
  arabicName: string;
}

export interface AccountTreeNodeDto {
  id: number;
  code: string;
  arabicName: string;
  accountType: AccountType;
  accountTypeName: string;
  level: number;
  isPostable: boolean;
  isActive: boolean;
  balance: number | null;
  children: AccountTreeNodeDto[];
}

export interface AccountTreeQuery {
  includeInactive?: boolean;
  includeBalances?: boolean;
  asOfDate?: string | null;
}

export interface AccountingDimensionDto {
  id: number;
  dimensionType: DimensionType;
  dimensionTypeName: string;
  code: string;
  arabicName: string;
  englishName: string | null;
  branchId: number | null;
  branchName: string | null;
  parentId: number | null;
  parentName: string | null;
  description: string | null;
  isActive: boolean;
}

export interface AccountingDimensionListQuery extends PagedQuery {
  dimensionType?: DimensionType | null;
  branchId?: number | null;
  isActive?: boolean | null;
}

export interface AccountingPeriodDto {
  id: number;
  fiscalYearId: number;
  fiscalYearCode: string;
  periodNumber: number;
  name: string;
  startDate: string;
  endDate: string;
  status: AccountingPeriodStatus;
  statusName: string;
  closedAtUtc: string | null;
  closedByUserId: number | null;
  closeNotes: string | null;
  reopenedAtUtc: string | null;
  reopenedByUserId: number | null;
  reopenReason: string | null;
}

export interface AccountingPeriodListQuery extends PagedQuery {
  fiscalYearId?: number | null;
  status?: AccountingPeriodStatus | null;
  date?: string | null;
}

export interface AllocatePaymentRequest {
  autoAllocate?: boolean;
  allocations?: PaymentAllocationRequest[];
}

export interface ApproveCashTransferRequest {
  bankReference?: string | null;
  notes?: string | null;
}

export interface ApproveSenderSettlementRequest {
  notes?: string | null;
}

export interface AssignDriverRequest {
  driverId?: number | null;
  vehicleId?: number | null;
  notes?: string | null;
}

export interface AuditLogDto {
  id: number;
  createdAtUtc: string;
  action: AuditAction;
  actionName: string;
  succeeded: boolean;
  userId: number | null;
  userName: string | null;
  entityName: string | null;
  entityId: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  details: string | null;
}

export interface AuditLogListQuery extends PagedQuery {
  action?: AuditAction | null;
  userId?: number | null;
  entityName?: string | null;
  entityId?: string | null;
  succeeded?: boolean | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface AuthResponse {
  tokenType: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  user: CurrentUserDto;
}

export interface BalanceSheetDto {
  asOfDate: string;
  baseCurrencyCode: string;
  assets: StatementGroupDto[];
  totalAssets: number;
  liabilities: StatementGroupDto[];
  totalLiabilities: number;
  equity: StatementGroupDto[];
  priorYearsProfit: number;
  currentYearProfit: number;
  totalEquity: number;
  totalLiabilitiesAndEquity: number;
  interBranchDifference: number;
  isBalanced: boolean;
}

export interface BalanceSheetQuery {
  asOfDate?: string | null;
  branchId?: number | null;
}

export interface BankAccountDto {
  id: number;
  code: string;
  arabicName: string;
  bankName: string;
  accountNumber: string;
  iban: string | null;
  currencyId: number;
  currencyCode: string;
  branchId: number | null;
  branchName: string | null;
  isActive: boolean;
  notes: string | null;
  createdAtUtc: string;
  accountId: number | null;
  accountCode: string | null;
  accountName: string | null;
}

export interface BankAccountListQuery extends PagedQuery {
  currencyId?: number | null;
  branchId?: number | null;
  isActive?: boolean | null;
}

export interface BankBalanceRowDto {
  bankAccountId: number;
  code: string;
  name: string;
  bankName: string;
  currencyCode: string;
  accountCode: string;
  ledgerBalance: number;
  ledgerBalanceBase: number;
  lastReconciledDate: string | null;
  lastStatementBalance: number | null;
  unreconciledStatementLines: number;
}

export interface BankBalancesDto {
  asOfDate: string;
  baseCurrencyCode: string;
  rows: BankBalanceRowDto[];
  totalBase: number;
}

export interface BankMatchRequest {
  bankTransactionId?: number;
  journalEntryLineId?: number;
}

export interface BankReconciliationDto {
  id: number;
  reconciliationNumber: string;
  bankAccountId: number;
  bankAccountName: string;
  currencyCode: string;
  accountId: number;
  accountCode: string;
  statementDate: string;
  statementEndingBalance: number;
  bookBalance: number;
  outstandingDeposits: number;
  outstandingWithdrawals: number;
  bankOnlyDeposits: number;
  bankOnlyWithdrawals: number;
  adjustedBankBalance: number;
  adjustedBookBalance: number;
  difference: number;
  status: BankReconciliationStatus;
  statusName: string;
  completedAtUtc: string | null;
  completedByUserId: number | null;
  notes: string | null;
  createdAtUtc: string;
  matchedCount: number;
  bankOnlyCount: number;
  bookOnlyCount: number;
  lines: BankReconciliationLineDto[];
}

export interface BankReconciliationLineDto {
  id: number;
  lineType: BankReconciliationLineType;
  lineTypeName: string;
  bankTransactionId: number | null;
  journalEntryLineId: number | null;
  journalEntryId: number | null;
  journalEntryNumber: string | null;
  transactionDate: string;
  description: string;
  amount: number;
  isCleared: boolean;
}

export interface BankReconciliationListQuery extends PagedQuery {
  bankAccountId?: number | null;
  status?: BankReconciliationStatus | null;
}

export interface BankStatementLineRequest {
  transactionDate?: string;
  description?: string;
  reference?: string | null;
  deposit?: number;
  withdrawal?: number;
}

export interface BankTransactionDto {
  id: number;
  bankAccountId: number;
  bankAccountName: string;
  transactionDate: string;
  description: string;
  reference: string | null;
  deposit: number;
  withdrawal: number;
  isReconciled: boolean;
  bankReconciliationId: number | null;
  matchedJournalEntryLineId: number | null;
}

export interface BankTransactionListQuery extends PagedQuery {
  bankAccountId?: number | null;
  isReconciled?: boolean | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface BranchDto extends MasterDataDto {
  code: string;
  arabicName: string;
  englishName: string | null;
  cityId: number;
  cityName: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  isHeadOffice: boolean;
  notes: string | null;
  warehouseCount: number;
}

export interface BranchListQuery extends MasterDataListQuery {
  cityId?: number | null;
}

export interface BranchTransferDto {
  id: number;
  transferNumber: string;
  fromBranchId: number;
  fromBranchName: string;
  toBranchId: number;
  toBranchName: string;
  vehicleId: number | null;
  vehiclePlateNumber: string | null;
  driverId: number | null;
  driverName: string | null;
  sealNumber: string | null;
  status: BranchTransferStatus;
  itemCount: number;
  receivedCount: number;
  missingCount: number;
  dispatchedAtUtc: string | null;
  dispatchedByUserId: number | null;
  receivedAtUtc: string | null;
  receivedByUserId: number | null;
  notes: string | null;
  createdAtUtc: string;
  createdByUserId: number | null;
  items: BranchTransferItemDto[] | null;
}

export interface BranchTransferItemDto extends ManifestShipmentDto {
  itemStatus: BranchTransferItemStatus;
  receivedAtUtc: string | null;
  notes: string | null;
}

export interface BranchTransferListQuery extends DocumentListQuery {
  fromBranchId?: number | null;
  toBranchId?: number | null;
  status?: BranchTransferStatus | null;
}

export interface BulkLabelRequest {
  shipmentIds?: number[];
  format?: LabelFormat;
}

export interface CancelCashVoucherRequest {
  reason?: string;
}

export interface CancelInvoiceRequest {
  reason?: string;
}

export interface CancelShipmentRequest {
  reason?: string;
}

export interface CancelShipmentReturnRequest {
  reason?: string;
}

export interface CashAccountBalanceDto {
  accountId: number;
  code: string;
  arabicName: string;
  opening: number;
  closing: number;
}

export interface CashFlowDto {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  openingCash: number;
  sections: CashFlowSectionDto[];
  netChange: number;
  closingCash: number;
  cashAccounts: CashAccountBalanceDto[];
  isReconciled: boolean;
}

export interface CashFlowLineDto {
  accountId: number;
  code: string;
  arabicName: string;
  inflow: number;
  outflow: number;
  net: number;
}

export interface CashFlowQuery {
  fromDate?: string | null;
  toDate?: string | null;
  branchId?: number | null;
}

export interface CashFlowSectionDto {
  activity: CashFlowActivity;
  activityName: string;
  lines: CashFlowLineDto[];
  totalInflow: number;
  totalOutflow: number;
  net: number;
}

export interface CashPaymentVoucherDto {
  id: number;
  voucherNumber: string;
  cashboxId: number;
  cashboxCode: string;
  cashboxName: string;
  branchId: number;
  branchName: string;
  currencyId: number;
  currencyCode: string;
  currencyName: string;
  currencyDecimals: number;
  paymentType: CashPaymentType;
  paymentTypeName: string;
  status: CashVoucherStatus;
  statusName: string;
  voucherDateUtc: string;
  amount: number;
  amountInWords: string;
  paidTo: string;
  description: string;
  referenceNumber: string | null;
  expenseCategoryId: number | null;
  expenseCategoryName: string | null;
  supplierId: number | null;
  supplierName: string | null;
  employeeId: number | null;
  employeeName: string | null;
  customerId: number | null;
  customerName: string | null;
  senderSettlementId: number | null;
  cancelledAtUtc: string | null;
  cancelledByUserId: number | null;
  cancellationReason: string | null;
  createdAtUtc: string;
  createdByUserId: number | null;
}

export interface CashReceiptVoucherDto {
  id: number;
  voucherNumber: string;
  cashboxId: number;
  cashboxCode: string;
  cashboxName: string;
  branchId: number;
  branchName: string;
  currencyId: number;
  currencyCode: string;
  currencyName: string;
  currencyDecimals: number;
  receiptType: CashReceiptType;
  receiptTypeName: string;
  status: CashVoucherStatus;
  statusName: string;
  voucherDateUtc: string;
  amount: number;
  amountInWords: string;
  receivedFrom: string;
  description: string;
  referenceNumber: string | null;
  driverId: number | null;
  driverName: string | null;
  customerId: number | null;
  customerName: string | null;
  customerPaymentId: number | null;
  customerPaymentNumber: string | null;
  expectedAmount: number | null;
  shortageAmount: number;
  excessAmount: number;
  driverBalanceAfter: number | null;
  cancelledAtUtc: string | null;
  cancelledByUserId: number | null;
  cancellationReason: string | null;
  createdAtUtc: string;
  createdByUserId: number | null;
  codLines: HandoverCodLineDto[];
}

export interface CashTransferDto {
  id: number;
  transferNumber: string;
  transferType: CashTransferType;
  transferTypeName: string;
  status: CashTransferStatus;
  statusName: string;
  fromCashboxId: number;
  fromCashboxName: string;
  fromBranchId: number;
  fromBranchName: string;
  toCashboxId: number | null;
  toCashboxName: string | null;
  toBranchId: number | null;
  toBranchName: string | null;
  toBankAccountId: number | null;
  toBankAccountName: string | null;
  currencyId: number;
  currencyCode: string;
  amount: number;
  reason: string;
  bankReference: string | null;
  requestedByUserId: number | null;
  createdAtUtc: string;
  decidedByUserId: number | null;
  decidedAtUtc: string | null;
  decisionNotes: string | null;
  sentAtUtc: string | null;
  receivedAtUtc: string | null;
  receivedByUserId: number | null;
}

export interface CashTransferListQuery extends PagedQuery {
  status?: CashTransferStatus | null;
  transferType?: CashTransferType | null;
  cashboxId?: number | null;
  branchId?: number | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface CashTransferReasonRequest {
  reason?: string;
}

export interface CashVoucherCreationResult<T> {
  voucher: T;
  isReplay: boolean;
}

export interface CashVoucherListQuery extends PagedQuery {
  cashboxId?: number | null;
  branchId?: number | null;
  status?: CashVoucherStatus | null;
  type?: string | null;
  driverId?: number | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface CashboxClosingQuery extends PagedQuery {
  cashboxId?: number | null;
  branchId?: number | null;
  status?: CashboxClosingStatus | null;
  fromDate?: string | null;
  toDate?: string | null;
  hasDiscrepancy?: boolean | null;
}

export interface CashboxDailyClosingDto {
  id: number;
  cashboxId: number;
  cashboxCode: string;
  cashboxName: string;
  branchName: string;
  currencyCode: string;
  businessDate: string;
  openingBalance: number;
  totalIn: number;
  totalOut: number;
  expectedBalance: number;
  countedBalance: number;
  discrepancy: number;
  status: CashboxClosingStatus;
  statusName: string;
  discrepancyReason: string | null;
  notes: string | null;
  closedByUserId: number | null;
  createdAtUtc: string;
  decidedByUserId: number | null;
  decidedAtUtc: string | null;
  decisionNotes: string | null;
  adjustmentTransactionId: number | null;
}

export interface CashboxDto {
  id: number;
  code: string;
  arabicName: string;
  englishName: string | null;
  branchId: number;
  branchName: string;
  currencyId: number;
  currencyCode: string;
  currencyDecimals: number;
  custodianUserId: number | null;
  custodianName: string | null;
  currentBalance: number;
  lastTransactionAtUtc: string | null;
  isActive: boolean;
  notes: string | null;
  createdAtUtc: string;
  createdByUserId: number | null;
  updatedAtUtc: string | null;
  updatedByUserId: number | null;
}

export interface CashboxListQuery extends PagedQuery {
  branchId?: number | null;
  currencyId?: number | null;
  isActive?: boolean | null;
}

export interface CashboxTransactionDto {
  id: number;
  transactionType: CashboxTransactionType;
  transactionTypeName: string;
  amountIn: number;
  amountOut: number;
  balanceAfter: number;
  transactionDateUtc: string;
  businessDate: string;
  documentType: string;
  documentId: number;
  documentNumber: string;
  description: string;
  counterpartyName: string | null;
  reversedTransactionId: number | null;
  createdByUserId: number | null;
}

export interface CashboxTransactionQuery extends PagedQuery {
  fromDate?: string | null;
  toDate?: string | null;
  transactionType?: CashboxTransactionType | null;
}

export interface ChangePasswordRequest {
  currentPassword?: string;
  newPassword?: string;
  confirmNewPassword?: string;
}

export interface ChangeShipmentStatusRequest {
  status?: ShipmentStatus;
  warehouseId?: number | null;
  notes?: string | null;
  occurredAtUtc?: string | null;
}

export interface CityDto extends MasterDataDto {
  countryId: number;
  countryName: string;
  code: string;
  arabicName: string;
  englishName: string | null;
  zoneCount: number;
}

export interface CityListQuery extends MasterDataListQuery {
  countryId?: number | null;
}

export interface CloseAccountingPeriodRequest {
  notes?: string | null;
}

export interface CloseCashboxDayRequest {
  businessDate?: string | null;
  countedBalance?: number;
  discrepancyReason?: string | null;
  notes?: string | null;
}

export interface CloseDriverManifestRequest {
  pendingReason?: string | null;
  notes?: string | null;
}

export interface CodAdjustmentDto {
  id: number;
  adjustmentNumber: string;
  adjustmentType: CODAdjustmentType;
  adjustmentTypeName: string;
  status: CODAdjustmentStatus;
  statusName: string;
  branchId: number;
  branchName: string;
  currencyId: number;
  currencyCode: string;
  driverId: number | null;
  driverName: string | null;
  codTransactionId: number | null;
  shipmentNumber: string | null;
  amount: number;
  previousCollectedAmount: number | null;
  newCollectedAmount: number | null;
  reason: string;
  requestedByUserId: number | null;
  createdAtUtc: string;
  decidedByUserId: number | null;
  decidedAtUtc: string | null;
  decisionNotes: string | null;
}

export interface CodAdjustmentListQuery extends PagedQuery {
  status?: CODAdjustmentStatus | null;
  adjustmentType?: CODAdjustmentType | null;
  driverId?: number | null;
  codTransactionId?: number | null;
  branchId?: number | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface CodAgingQuery {
  asOfUtc?: string | null;
  currencyId?: number | null;
  branchId?: number | null;
  senderCustomerId?: number | null;
}

export interface CodAgingReportDto {
  asOfUtc: string;
  currencyId: number;
  currencyCode: string;
  rows: CodAgingRowDto[];
  totals: CodAgingRowDto;
}

export interface CodAgingRowDto {
  senderCustomerId: number;
  senderCode: string;
  senderName: string;
  count: number;
  days0To7: number;
  days8To15: number;
  days16To30: number;
  days31To60: number;
  over60Days: number;
  total: number;
  deliveredNotCollected: number;
  withDrivers: number;
  inCashboxes: number;
  readyForSettlement: number;
  disputed: number;
  oldestDateUtc: string | null;
}

export interface CodDashboardDto {
  asOfUtc: string;
  currencyId: number;
  currencyCode: string;
  stages: CodStageSummaryDto[];
  deliveredNotCollectedCount: number;
  deliveredNotCollectedAmount: number;
  driversWithBalance: number;
  driversOutstandingTotal: number;
  cashboxCount: number;
  cashboxBalanceTotal: number;
  unsettledSenderNet: number;
  openSettlementsCount: number;
  openSettlementsAmount: number;
  pendingAdjustments: number;
  pendingTransfers: number;
  inTransitTransfersAmount: number;
  pendingClosingApprovals: number;
}

export interface CodDashboardQuery {
  branchId?: number | null;
  currencyId?: number | null;
}

export interface CodHistoryDto {
  id: number;
  transactionType: CODTransactionType;
  transactionTypeName: string;
  fromStatus: CODStatus | null;
  toStatus: CODStatus;
  amount: number;
  occurredAtUtc: string;
  driverId: number | null;
  cashboxId: number | null;
  referenceType: string | null;
  referenceId: number | null;
  referenceNumber: string | null;
  notes: string | null;
  createdByUserId: number | null;
}

export interface CodStageSummaryDto {
  status: CODStatus;
  statusName: string;
  count: number;
  amount: number;
}

export interface CodTransactionDto {
  id: number;
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
  shipmentStatus: ShipmentStatus;
  senderCustomerId: number;
  senderCode: string;
  senderName: string;
  originBranchId: number;
  originBranchName: string;
  currencyId: number;
  currencyCode: string;
  status: CODStatus;
  statusName: string;
  statusBeforeDispute: CODStatus | null;
  expectedAmount: number;
  collectedAmount: number;
  collectedAtUtc: string | null;
  driverId: number | null;
  driverName: string | null;
  collectedByUserId: number | null;
  collectionDifferenceReason: string | null;
  receivedCashboxId: number | null;
  receivedCashboxName: string | null;
  receiptVoucherId: number | null;
  receiptVoucherNumber: string | null;
  receivedAtUtc: string | null;
  shippingChargeDeduction: number;
  codCommissionDeduction: number;
  taxDeduction: number;
  otherFeesAmount: number;
  compensationAmount: number;
  shippingChargeDeducted: boolean;
  netPayableAmount: number;
  readyAtUtc: string | null;
  settledAmount: number;
  remainingPayable: number;
  deductionsRecognized: boolean;
  settledAtUtc: string | null;
  openSettlementId: number | null;
  openSettlementNumber: string | null;
  disputeReason: string | null;
  disputedAtUtc: string | null;
  cancelledAtUtc: string | null;
  createdAtUtc: string;
  history: CodHistoryDto[];
}

export interface CodTransactionListItemDto {
  id: number;
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
  shipmentStatus: ShipmentStatus;
  senderCustomerId: number;
  senderName: string;
  driverId: number | null;
  driverName: string | null;
  currencyId: number;
  currencyCode: string;
  status: CODStatus;
  statusName: string;
  expectedAmount: number;
  collectedAmount: number;
  netPayableAmount: number;
  settledAmount: number;
  remainingPayable: number;
  collectedAtUtc: string | null;
  receivedAtUtc: string | null;
  createdAtUtc: string;
}

export interface CodTransactionListQuery extends PagedQuery {
  status?: CODStatus | null;
  senderCustomerId?: number | null;
  driverId?: number | null;
  branchId?: number | null;
  currencyId?: number | null;
  cashboxId?: number | null;
  collectedFromUtc?: string | null;
  collectedToUtc?: string | null;
  hasRemainingPayable?: boolean | null;
}

export interface CollectCodRequest {
  shipmentId?: number | null;
  trackingNumber?: string | null;
  amount?: number;
  collectedAtUtc?: string | null;
  driverId?: number | null;
  cashboxId?: number | null;
  differenceReason?: string | null;
  notes?: string | null;
}

export interface CompanyProfileDto {
  id: number;
  arabicName: string;
  englishName: string | null;
  commercialRegistrationNumber: string | null;
  taxNumber: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  address: string | null;
  logoUrl: string | null;
  baseCurrencyId: number;
  baseCurrencyCode: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CompleteBankReconciliationRequest {
  notes?: string | null;
}

export interface CompleteShipmentReturnRequest {
  notes?: string | null;
}

export interface ControlAccountRowDto {
  control: string;
  arabicName: string;
  currencyCode: string;
  subledgerBalance: number;
  ledgerBalance: number;
  difference: number;
}

export interface ControlAccountsDto {
  generatedAtUtc: string;
  rows: ControlAccountRowDto[];
  pendingPostingEvents: number;
  failedPostingEvents: number;
  isReconciled: boolean;
}

export interface CorrectShipmentStatusRequest {
  status?: ShipmentStatus;
  reason?: string;
  currentBranchId?: number | null;
  warehouseId?: number | null;
}

export interface CountryDto extends MasterDataDto {
  code: string;
  arabicName: string;
  englishName: string | null;
  phoneCode: string | null;
  cityCount: number;
}

export interface CountryListQuery extends MasterDataListQuery {
}

export interface CreateBankReconciliationRequest {
  bankAccountId?: number;
  statementDate?: string;
  statementEndingBalance?: number;
  statementLines?: BankStatementLineRequest[];
  matches?: BankMatchRequest[];
  autoMatch?: boolean;
  complete?: boolean;
  notes?: string | null;
}

export interface CreateBranchTransferRequest {
  fromBranchId?: number;
  toBranchId?: number;
  vehicleId?: number | null;
  driverId?: number | null;
  sealNumber?: string | null;
  notes?: string | null;
  codes?: string[];
}

export interface CreateCashPaymentRequest {
  cashboxId?: number;
  paymentType?: CashPaymentType;
  amount?: number;
  paidTo?: string;
  description?: string;
  referenceNumber?: string | null;
  expenseCategoryId?: number | null;
  supplierId?: number | null;
  employeeId?: number | null;
  customerId?: number | null;
}

export interface CreateCashReceiptRequest {
  cashboxId?: number;
  receiptType?: CashReceiptType;
  amount?: number;
  receivedFrom?: string;
  description?: string;
  referenceNumber?: string | null;
  customerId?: number | null;
  customerPaymentId?: number | null;
}

export interface CreateCashTransferRequest {
  fromCashboxId?: number;
  toCashboxId?: number | null;
  toBankAccountId?: number | null;
  amount?: number;
  reason?: string;
  bankReference?: string | null;
}

export interface CreateCashboxRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  branchId?: number;
  currencyId?: number;
  custodianUserId?: number | null;
  openingBalance?: number;
  notes?: string | null;
}

export interface CreateCodAdjustmentRequest {
  adjustmentType?: CODAdjustmentType;
  driverId?: number | null;
  codTransactionId?: number | null;
  currencyId?: number | null;
  amount?: number | null;
  newCollectedAmount?: number | null;
  reason?: string;
}

export interface CreateConsolidatedInvoiceRequest extends CreateInvoiceOptions {
  customerId?: number;
  shipmentIds?: number[];
  fromUtc?: string | null;
  toUtc?: string | null;
  currencyId?: number | null;
}

export interface CreateCreditNoteRequest {
  reason?: string;
  creditAll?: boolean;
  lines?: CreditNoteLineRequest[];
}

export interface CreateCustomerPaymentRequest {
  customerId?: number;
  branchId?: number | null;
  paymentMethodId?: number;
  currencyId?: number | null;
  amount?: number;
  paymentDateUtc?: string | null;
  referenceNumber?: string | null;
  notes?: string | null;
  autoAllocate?: boolean;
  allocations?: PaymentAllocationRequest[];
}

export interface CreateDamageCaseRequest {
  shipmentId?: number;
  caseType?: DamageCaseType;
  description?: string;
  branchId?: number | null;
  estimatedLossAmount?: number;
  markShipmentAsLostOrDamaged?: boolean;
}

export interface CreateDriverManifestRequest {
  driverId?: number;
  vehicleId?: number | null;
  manifestDateUtc?: string | null;
  notes?: string | null;
  codes?: string[];
}

export interface CreateFiscalYearRequest {
  code?: string | null;
  arabicName?: string | null;
  startDate?: string;
  endDate?: string | null;
}

export interface CreateInvoiceFromShipmentRequest extends CreateInvoiceOptions {
}

export interface CreateInvoiceOptions {
  branchId: number | null;
  paymentTermsDays: number | null;
  notes: string | null;
  issueImmediately: boolean;
}

export interface CreateRoleRequest {
  name?: string;
  arabicName?: string;
  description?: string | null;
  permissions?: string[];
}

export interface CreateSenderSettlementRequest {
  senderCustomerId?: number;
  currencyId?: number | null;
  branchId?: number | null;
  codTransactionIds?: number[];
  lines?: SettlementLineRequest[];
  notes?: string | null;
}

export interface CreateShipmentReturnRequest {
  shipmentId?: number;
  returnType?: ShipmentReturnType;
  reason?: string;
  returnBranchId?: number | null;
  returnCharge?: number;
  notes?: string | null;
}

export interface CreateUserRequest {
  userName?: string;
  email?: string;
  phoneNumber?: string | null;
  password?: string;
  fullName?: string;
  arabicFullName?: string;
  branchId?: number | null;
  employeeCode?: string | null;
  roles?: string[];
}

export interface CreateWarehouseManifestRequest {
  movementType?: WarehouseMovementType;
  warehouseId?: number;
  notes?: string | null;
  codes?: string[];
}

export interface CreditNoteDto {
  id: number;
  creditNoteNumber: string;
  invoiceId: number;
  invoiceNumber: string | null;
  customerId: number;
  customerName: string;
  currencyCode: string;
  creditNoteDateUtc: string;
  reason: string;
  taxableAmount: number;
  taxAmount: number;
  totalAmount: number;
  appliedToInvoiceAmount: number;
  unappliedAmount: number;
  createdByUserId: number | null;
  lines: CreditNoteLineDto[];
}

export interface CreditNoteLineDto {
  id: number;
  invoiceLineId: number;
  shipmentId: number;
  description: string;
  taxableAmount: number;
  taxPercentage: number;
  taxAmount: number;
  lineTotal: number;
}

export interface CreditNoteLineRequest {
  invoiceLineId?: number;
  taxableAmount?: number | null;
}

export interface CurrencyDto extends MasterDataDto {
  code: string;
  arabicName: string;
  englishName: string | null;
  symbol: string;
  decimalPlaces: number;
  isBaseCurrency: boolean;
}

export interface CurrencyListQuery extends MasterDataListQuery {
}

export interface CurrencyTotalDto {
  currencyCode: string;
  subledgerBalance: number;
  ledgerBalance: number;
  difference: number;
}

export interface CurrentUserDto {
  id: number;
  userName: string;
  email: string | null;
  fullName: string;
  arabicFullName: string;
  branchId: number | null;
  employeeCode: string | null;
  lastLoginAtUtc: string | null;
  roles: string[];
  permissions: string[];
}

export interface CustomerAddressDto {
  id: number;
  customerId: number;
  label: string;
  cityId: number;
  cityName: string;
  zoneId: number | null;
  zoneName: string | null;
  addressLine: string;
  landmark: string | null;
  buildingNumber: string | null;
  postalCode: string | null;
  contactName: string | null;
  phone: string | null;
  alternatePhone: string | null;
  latitude: number | null;
  longitude: number | null;
  isDefault: boolean;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CustomerAgingQuery extends PagedQuery {
  asOfUtc?: string | null;
  currencyId?: number | null;
  branchId?: number | null;
  customerId?: number | null;
  onlyWithBalance?: boolean;
}

export interface CustomerAgingReportDto {
  asOfUtc: string;
  currencyId: number;
  currencyCode: string;
  branchId: number | null;
  totals: CustomerAgingRowDto;
  customers: PagedResult<CustomerAgingRowDto>;
}

export interface CustomerAgingRowDto {
  customerId: number;
  customerCode: string;
  customerName: string;
  customerPhone: string;
  creditLimit: number;
  current: number;
  days1To30: number;
  days31To60: number;
  days61To90: number;
  days90Plus: number;
  overdue: number;
  totalDue: number;
  unappliedCredits: number;
  netBalance: number;
  openInvoiceCount: number;
  oldestDaysPastDue: number;
}

export interface CustomerDto extends MasterDataDto {
  code: string;
  customerType: CustomerType;
  roles: CustomerRole;
  arabicName: string;
  englishName: string | null;
  phone: string;
  alternatePhone: string | null;
  email: string | null;
  nationalOrCompanyNumber: string | null;
  creditLimit: number;
  openingBalance: number;
  notes: string | null;
  defaultBranchId: number;
  defaultBranchName: string;
  pricingPlanId: number | null;
  pricingPlanName: string | null;
  addressCount: number;
}

export interface CustomerListQuery extends MasterDataListQuery {
  customerType?: CustomerType | null;
  role?: CustomerRole | null;
  defaultBranchId?: number | null;
  pricingPlanId?: number | null;
}

export interface CustomerPaymentCreationResult {
  payment: CustomerPaymentDto;
  isReplay: boolean;
}

export interface CustomerPaymentDto {
  id: number;
  paymentNumber: string;
  customerId: number;
  customerCode: string;
  customerName: string;
  branchId: number;
  branchName: string;
  paymentMethodId: number;
  paymentMethodName: string;
  currencyId: number;
  currencyCode: string;
  amount: number;
  paymentDateUtc: string;
  referenceNumber: string | null;
  notes: string | null;
  allocatedAmount: number;
  unallocatedAmount: number;
  createdAtUtc: string;
  createdByUserId: number | null;
  allocations: PaymentAllocationDto[];
}

export interface CustomerPaymentListItemDto {
  id: number;
  paymentNumber: string;
  customerId: number;
  customerName: string;
  branchName: string;
  paymentMethodName: string;
  currencyCode: string;
  amount: number;
  allocatedAmount: number;
  unallocatedAmount: number;
  paymentDateUtc: string;
  referenceNumber: string | null;
}

export interface CustomerPaymentListQuery extends PagedQuery {
  customerId?: number | null;
  branchId?: number | null;
  paymentMethodId?: number | null;
  currencyId?: number | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  hasUnallocatedAmount?: boolean | null;
}

export interface CustomerStatementDto {
  customerId: number;
  customerCode: string;
  customerName: string;
  customerPhone: string;
  creditLimit: number;
  currencyId: number;
  currencyCode: string;
  fromUtc: string | null;
  toUtc: string;
  openingBalance: number;
  totalDebit: number;
  totalCredit: number;
  closingBalance: number;
  openInvoiceCount: number;
  openInvoicesBalance: number;
  overdueBalance: number;
  unappliedCredits: number;
  branchId: number | null;
  lines: PagedResult<StatementLineDto>;
}

export interface CustomerStatementQuery extends PagedQuery {
  currencyId?: number | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface DailyCashCurrencyTotalDto {
  currencyCode: string;
  totalIn: number;
  totalOut: number;
  net: number;
}

export interface DailyCashMovementQuery {
  fromDate?: string | null;
  toDate?: string | null;
  cashboxId?: number | null;
  branchId?: number | null;
  currencyId?: number | null;
  includeEmptyDays?: boolean;
}

export interface DailyCashMovementReportDto {
  fromDate: string;
  toDate: string;
  timeZoneId: string;
  rows: DailyCashMovementRowDto[];
  totals: DailyCashCurrencyTotalDto[];
}

export interface DailyCashMovementRowDto {
  cashboxId: number;
  cashboxCode: string;
  cashboxName: string;
  branchName: string;
  currencyCode: string;
  businessDate: string;
  openingBalance: number;
  driverHandovers: number;
  counterCollections: number;
  receipts: number;
  transfersIn: number;
  otherIn: number;
  totalIn: number;
  settlementPayments: number;
  payments: number;
  transfersOut: number;
  otherOut: number;
  totalOut: number;
  closingBalance: number;
  transactionCount: number;
  closingStatus: CashboxClosingStatus | null;
  countedBalance: number | null;
  discrepancy: number | null;
}

export interface DailyShipmentCountDto {
  date: string;
  created: number;
  delivered: number;
}

export interface DamageCaseDto {
  id: number;
  caseNumber: string;
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
  shipmentStatus: ShipmentStatus;
  caseType: DamageCaseType;
  status: DamageCaseStatus;
  branchId: number;
  branchName: string;
  description: string;
  shipmentStatusBefore: ShipmentStatus;
  shipmentMarkedLostOrDamaged: boolean;
  estimatedLossAmount: number;
  resolution: DamageCaseResolution | null;
  compensationAmount: number | null;
  resolutionNotes: string | null;
  resolvedAtUtc: string | null;
  resolvedByUserId: number | null;
  evidenceCount: number;
  createdAtUtc: string;
  createdByUserId: number | null;
}

export interface DamageCaseListQuery extends DocumentListQuery {
  status?: DamageCaseStatus | null;
  caseType?: DamageCaseType | null;
  branchId?: number | null;
  shipmentId?: number | null;
}

export interface DashboardActivityDto {
  occurredAtUtc: string;
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
  status: ShipmentStatus;
  statusName: string;
  branchName: string | null;
  notes: string | null;
}

export interface DashboardCashboxDto {
  id: number;
  code: string;
  name: string;
  branchName: string;
  currencyCode: string;
  balance: number;
}

export interface DashboardDriverBalanceDto {
  driverId: number;
  driverName: string;
  branchName: string;
  currencyCode: string;
  balance: number;
  unhandedCodCount: number;
}

export interface DashboardDto {
  asOfUtc: string;
  today: string;
  periodDays: number;
  branchId: number | null;
  operations: DashboardOperationsDto | null;
  cod: CodDashboardDto | null;
  cashboxes: DashboardCashboxDto[] | null;
  finance: DashboardFinanceDto | null;
  driverBalances: DashboardDriverBalanceDto[] | null;
  topCustomers: DashboardTopCustomerDto[] | null;
  recentActivity: DashboardActivityDto[] | null;
}

export interface DashboardFinanceDto {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  totalRevenue: number;
  totalExpenses: number;
  netProfit: number;
  overdueInvoicesCount: number;
  overdueInvoicesAmount: number;
  receivablesBalance: number;
}

export interface DashboardOperationsDto {
  todayCreated: number;
  todayDelivered: number;
  todayFailed: number;
  openShipments: number;
  outForDelivery: number;
  periodShipments: number;
  periodDelivered: number;
  periodFailed: number;
  deliveredPercent: number;
  failedPercent: number;
  byStatus: StatusCountDto[];
  last7Days: DailyShipmentCountDto[];
}

export interface DashboardQuery {
  days?: number;
  branchId?: number | null;
}

export interface DashboardTopCustomerDto {
  customerId: number;
  code: string;
  name: string;
  shipmentCount: number;
  totalCharges: number;
  codAmount: number;
}

export interface DecideCashboxClosingRequest {
  notes?: string | null;
}

export interface DecideCodAdjustmentRequest {
  notes?: string | null;
}

export interface DecidePriceOverrideRequest {
  notes?: string | null;
}

export interface DeliverShipmentRequest {
  recipientName?: string;
  recipientRelation?: string | null;
  deliveredAtUtc?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  notes?: string | null;
}

export interface DispatchBranchTransferRequest {
  vehicleId?: number | null;
  driverId?: number | null;
  sealNumber?: string | null;
  notes?: string | null;
}

export interface DocumentListQuery extends PagedQuery {
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface DriverBalanceDto {
  driverId: number;
  driverName: string;
  employeeCode: string;
  branchId: number;
  branchName: string;
  currencyId: number;
  currencyCode: string;
  balance: number;
  unhandedCodCount: number;
  unhandedCodAmount: number;
  oldestUnhandedCollectionAtUtc: string | null;
  lastHandoverAtUtc: string | null;
  lastMovementAtUtc: string | null;
}

export interface DriverBalanceQuery extends PagedQuery {
  branchId?: number | null;
  currencyId?: number | null;
  driverId?: number | null;
  onlyOutstanding?: boolean;
}

export interface DriverCashMovementDto {
  id: number;
  movementType: DriverCashMovementType;
  movementTypeName: string;
  currencyCode: string;
  debit: number;
  credit: number;
  balanceAfter: number;
  movementDateUtc: string;
  codTransactionId: number | null;
  documentType: string;
  documentId: number;
  documentNumber: string;
  description: string;
  createdByUserId: number | null;
}

export interface DriverCollectionReportDto {
  fromUtc: string;
  toUtc: string;
  currencyId: number;
  currencyCode: string;
  rows: DriverCollectionRowDto[];
  totals: DriverCollectionRowDto;
}

export interface DriverCollectionReportQuery {
  fromUtc?: string | null;
  toUtc?: string | null;
  currencyId?: number | null;
  branchId?: number | null;
  driverId?: number | null;
}

export interface DriverCollectionRowDto {
  driverId: number;
  driverName: string;
  branchName: string;
  openingBalance: number;
  collectedCount: number;
  collectedAmount: number;
  handoverCount: number;
  handedOverAmount: number;
  shortageWrittenOff: number;
  excessRecorded: number;
  otherMovements: number;
  closingBalance: number;
}

export interface DriverDto extends MasterDataDto {
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  branchId: number;
  branchName: string;
  phone: string | null;
  userId: number | null;
  userName: string | null;
  licenseNumber: string;
  licenseCategory: string | null;
  licenseExpiryDateUtc: string;
  isLicenseExpired: boolean;
  defaultVehicleId: number | null;
  defaultVehiclePlate: string | null;
  notes: string | null;
}

export interface DriverHandoverRequest {
  driverId?: number;
  cashboxId?: number;
  amount?: number;
  codTransactionIds?: number[];
  notes?: string | null;
}

export interface DriverListQuery extends MasterDataListQuery {
  branchId?: number | null;
  licenseExpiringSoon?: boolean | null;
}

export interface DriverManifestDto {
  id: number;
  manifestNumber: string;
  branchId: number;
  branchName: string;
  driverId: number;
  driverName: string;
  vehicleId: number | null;
  vehiclePlateNumber: string | null;
  manifestDateUtc: string;
  status: DriverManifestStatus;
  itemCount: number;
  deliveredCount: number;
  failedCount: number;
  pendingCount: number;
  totalCodAmount: number;
  dispatchedAtUtc: string | null;
  closedAtUtc: string | null;
  notes: string | null;
  createdAtUtc: string;
  createdByUserId: number | null;
  items: DriverManifestItemDto[] | null;
}

export interface DriverManifestItemDto extends ManifestShipmentDto {
  chargesOnRecipient: boolean;
  chargesToCollect: number;
  sequence: number;
  result: DeliveryResult;
  resultAtUtc: string | null;
  failureReason: string | null;
  proofOfDeliveryId: number | null;
  recipientAddressLine: string | null;
}

export interface DriverManifestListQuery extends DocumentListQuery {
  branchId?: number | null;
  driverId?: number | null;
  status?: DriverManifestStatus | null;
}

export interface DriverMovementQuery extends PagedQuery {
  currencyId?: number | null;
  movementType?: DriverCashMovementType | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface EligibleShipmentDto {
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
  statusName: string;
  recipientName: string;
  destinationCityName: string;
  currencyCode: string;
  shippingCharge: number;
  discountAmount: number;
  taxAmount: number;
  totalCharge: number;
  createdAtUtc: string;
}

export interface EligibleShipmentsQuery extends PagedQuery {
  customerId?: number;
  currencyId?: number | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface EmployeeDto extends MasterDataDto {
  code: string;
  arabicName: string;
  englishName: string | null;
  branchId: number;
  branchName: string;
  jobTitle: string | null;
  phone: string | null;
  email: string | null;
  nationalId: string | null;
  hireDateUtc: string | null;
  userId: number | null;
  userName: string | null;
  isDriver: boolean;
  notes: string | null;
}

export interface EmployeeListQuery extends MasterDataListQuery {
  branchId?: number | null;
  hasUser?: boolean | null;
}

export interface ExchangeRateDto extends MasterDataDto {
  fromCurrencyId: number;
  fromCurrencyCode: string;
  toCurrencyId: number;
  toCurrencyCode: string;
  rate: number;
  effectiveFromUtc: string;
  notes: string | null;
}

export interface ExchangeRateListQuery extends MasterDataListQuery {
  fromCurrencyId?: number | null;
  toCurrencyId?: number | null;
}

export interface ExpenseAccountRowDto {
  accountId: number;
  code: string;
  arabicName: string;
  categoryName: string;
  accountType: AccountType;
  amount: number;
  percent: number;
}

export interface ExpenseByCategoryDto {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  byAccount: ExpenseAccountRowDto[];
  byExpenseCategory: ExpenseCategoryRowDto[];
  total: number;
}

export interface ExpenseByCategoryQuery {
  fromDate?: string | null;
  toDate?: string | null;
  branchId?: number | null;
  costCenterId?: number | null;
}

export interface ExpenseCategoryDto extends MasterDataDto {
  code: string;
  arabicName: string;
  englishName: string | null;
  description: string | null;
}

export interface ExpenseCategoryListQuery extends MasterDataListQuery {
}

export interface ExpenseCategoryRowDto {
  expenseCategoryId: number | null;
  arabicName: string;
  amount: number;
  percent: number;
}

export interface ExportQuery {
  format?: ExportFormat;
}

export interface FiscalYearDto {
  id: number;
  code: string;
  arabicName: string;
  startDate: string;
  endDate: string;
  status: FiscalYearStatus;
  statusName: string;
  closedAtUtc: string | null;
  closedByUserId: number | null;
  periodCount: number;
  openPeriodCount: number;
  periods: AccountingPeriodDto[];
}

export interface GeneralLedgerDto {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountType: AccountType;
  normalBalance: NormalBalance;
  includesSubAccounts: boolean;
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  openingBalance: number;
  periodDebit: number;
  periodCredit: number;
  closingBalance: number;
  lines: PagedResult<GeneralLedgerLineDto>;
}

export interface GeneralLedgerLineDto {
  journalEntryId: number;
  entryNumber: string;
  entryDate: string;
  journalType: JournalType;
  description: string;
  lineDescription: string | null;
  reference: string | null;
  sourceNumber: string | null;
  accountCode: string;
  branchName: string;
  currencyCode: string;
  amountInCurrency: number;
  debit: number;
  credit: number;
  runningBalance: number;
}

export interface GeneralLedgerQuery extends PagedQuery {
  accountId?: number;
  fromDate?: string | null;
  toDate?: string | null;
  branchId?: number | null;
  costCenterId?: number | null;
}

export interface HandoverCodLineDto {
  codTransactionId: number;
  shipmentNumber: string;
  trackingNumber: string;
  senderName: string;
  amount: number;
}

export interface ImportShipmentsRequest {
  originBranchId?: number | null;
}

export interface ImportedShipmentDto {
  rowNumber: number;
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
}

export interface InvestigateDamageCaseRequest {
  notes?: string | null;
}

export interface InvoiceDto {
  id: number;
  invoiceNumber: string | null;
  status: InvoiceStatus;
  statusName: string;
  nextStatuses: InvoiceStatus[];
  isConsolidated: boolean;
  customerId: number;
  customerCode: string;
  customerName: string;
  customerPhone: string;
  customerTaxNumber: string | null;
  branchId: number;
  branchName: string;
  currencyId: number;
  currencyCode: string;
  currencyDecimals: number;
  invoiceDateUtc: string | null;
  paymentTermsDays: number;
  dueDateUtc: string | null;
  subTotal: number;
  lineDiscountAmount: number;
  invoiceDiscountAmount: number;
  discountAmount: number;
  taxableAmount: number;
  taxAmount: number;
  totalAmount: number;
  paidAmount: number;
  creditedAmount: number;
  creditAppliedAmount: number;
  balanceDue: number;
  notes: string | null;
  createdAtUtc: string;
  createdByUserId: number | null;
  issuedAtUtc: string | null;
  issuedByUserId: number | null;
  paidAtUtc: string | null;
  cancelledAtUtc: string | null;
  cancellationReason: string | null;
  lines: InvoiceLineDto[];
  payments: InvoicePaymentDto[];
  creditNotes: CreditNoteDto[];
}

export interface InvoiceLineDto {
  id: number;
  lineNumber: number;
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
  pricingSnapshotId: number;
  chargeType: InvoiceChargeType;
  description: string;
  shippingCharge: number;
  lineDiscountAmount: number;
  invoiceDiscountAmount: number;
  taxableAmount: number;
  taxPercentage: number;
  taxAmount: number;
  lineTotal: number;
  creditedTaxableAmount: number;
  creditedTaxAmount: number;
  creditableAmount: number;
}

export interface InvoiceListItemDto {
  id: number;
  invoiceNumber: string | null;
  status: InvoiceStatus;
  statusName: string;
  customerId: number;
  customerCode: string;
  customerName: string;
  branchId: number;
  branchName: string;
  currencyCode: string;
  isConsolidated: boolean;
  lineCount: number;
  invoiceDateUtc: string | null;
  dueDateUtc: string | null;
  totalAmount: number;
  paidAmount: number;
  creditedAmount: number;
  balanceDue: number;
  createdAtUtc: string;
}

export interface InvoiceListQuery extends PagedQuery {
  status?: InvoiceStatus | null;
  customerId?: number | null;
  branchId?: number | null;
  currencyId?: number | null;
  shipmentId?: number | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  hasBalance?: boolean | null;
}

export interface InvoicePaymentDto {
  paymentId: number;
  paymentNumber: string;
  paymentMethodName: string;
  amount: number;
  allocatedAtUtc: string;
}

export interface IssueInvoiceRequest {
  invoiceDateUtc?: string | null;
}

export interface JournalCreationResult {
  entry: JournalEntryDto;
  replayed: boolean;
}

export interface JournalDecisionRequest {
  notes?: string | null;
}

export interface JournalEntryDto {
  id: number;
  entryNumber: string;
  journalType: JournalType;
  journalTypeName: string;
  status: JournalEntryStatus;
  statusName: string;
  entryDate: string;
  accountingPeriodId: number;
  periodName: string;
  periodStatus: AccountingPeriodStatus;
  branchId: number;
  branchName: string;
  currencyId: number;
  currencyCode: string;
  exchangeRate: number;
  description: string;
  reference: string | null;
  totalDebit: number;
  totalCredit: number;
  totalDebitBase: number;
  totalCreditBase: number;
  isBalanced: boolean;
  isSystemGenerated: boolean;
  sourceType: string | null;
  sourceId: number | null;
  sourceNumber: string | null;
  sourceEventType: string | null;
  createdByUserId: number | null;
  createdAtUtc: string;
  submittedAtUtc: string | null;
  submittedByUserId: number | null;
  approvedAtUtc: string | null;
  approvedByUserId: number | null;
  rejectedAtUtc: string | null;
  rejectedByUserId: number | null;
  rejectionReason: string | null;
  postedAtUtc: string | null;
  postedByUserId: number | null;
  reversedAtUtc: string | null;
  reversedByUserId: number | null;
  reversalOfEntryId: number | null;
  reversalOfEntryNumber: string | null;
  reversedByEntryId: number | null;
  reversedByEntryNumber: string | null;
  lines: JournalEntryLineDto[];
}

export interface JournalEntryLineDto {
  id: number;
  lineNumber: number;
  accountId: number;
  accountCode: string;
  accountName: string;
  description: string | null;
  debit: number;
  credit: number;
  debitBase: number;
  creditBase: number;
  branchId: number;
  branchName: string;
  accountRole: string | null;
  customerId: number | null;
  supplierId: number | null;
  driverId: number | null;
  employeeId: number | null;
  cashboxId: number | null;
  bankAccountId: number | null;
  expenseCategoryId: number | null;
  dimensions: JournalLineDimensionDto[];
}

export interface JournalEntryListItemDto {
  id: number;
  entryNumber: string;
  journalType: JournalType;
  journalTypeName: string;
  status: JournalEntryStatus;
  statusName: string;
  entryDate: string;
  periodName: string;
  branchId: number;
  branchName: string;
  currencyCode: string;
  description: string;
  reference: string | null;
  totalDebit: number;
  totalCredit: number;
  totalDebitBase: number;
  isSystemGenerated: boolean;
  sourceType: string | null;
  sourceNumber: string | null;
  createdAtUtc: string;
  postedAtUtc: string | null;
}

export interface JournalEntryListQuery extends PagedQuery {
  status?: JournalEntryStatus | null;
  journalType?: JournalType | null;
  fromDate?: string | null;
  toDate?: string | null;
  branchId?: number | null;
  accountId?: number | null;
  sourceType?: string | null;
  isSystemGenerated?: boolean | null;
  accountingPeriodId?: number | null;
}

export interface JournalLineDimensionDto {
  dimensionType: DimensionType;
  dimensionTypeName: string;
  dimensionId: number;
  code: string;
  name: string;
}

export interface JournalLineDimensionRequest {
  dimensionType?: DimensionType;
  dimensionId?: number;
}

export interface JournalLineRequest {
  accountId?: number;
  description?: string | null;
  debit?: number;
  credit?: number;
  branchId?: number | null;
  costCenterId?: number | null;
  dimensions?: JournalLineDimensionRequest[];
  customerId?: number | null;
  supplierId?: number | null;
  driverId?: number | null;
  employeeId?: number | null;
  cashboxId?: number | null;
  bankAccountId?: number | null;
  expenseCategoryId?: number | null;
}

export interface JournalReasonRequest {
  reason?: string;
}

export interface JournalReportQuery extends PagedQuery {
  fromDate?: string | null;
  toDate?: string | null;
  journalType?: JournalType | null;
  branchId?: number | null;
}

export interface LoginRequest {
  userNameOrEmail?: string;
  password?: string;
}

export interface ManifestShipmentDto {
  itemId: number;
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
  shipmentStatus: ShipmentStatus;
  shipmentStatusName: string;
  recipientName: string;
  recipientPhone: string;
  destinationCityName: string;
  destinationZoneName: string | null;
  packageCount: number;
  chargeableWeight: number;
  isCOD: boolean;
  codAmount: number;
  totalVolumeCbm: number;
  pricingBasis: PricingBasis;
  chargesPaidBy: ChargesPayer;
}

export interface MarkCodReadyRequest {
  codTransactionIds?: number[];
  senderCustomerId?: number | null;
}

export interface MasterDataDto {
  id: number;
  isActive: boolean;
  createdAtUtc: string;
  createdByUserId: number | null;
  updatedAtUtc: string | null;
  updatedByUserId: number | null;
}

export interface MasterDataListQuery extends PagedQuery {
  isActive?: boolean | null;
}

export interface OpenCodDisputeRequest {
  reason?: string;
}

export interface OperationalGroupRowDto {
  id: number | null;
  name: string;
  total: number;
  delivered: number;
  failed: number;
  inProgress: number;
  cancelled: number;
  deliveredPercent: number;
  totalCharges: number;
  codAmount: number;
}

export interface OperationalReportDto {
  fromDate: string;
  toDate: string;
  totals: OperationalGroupRowDto;
  byStatus: StatusCountDto[];
  byBranch: OperationalGroupRowDto[];
  byDriver: OperationalGroupRowDto[];
  byCustomer: OperationalGroupRowDto[];
  byServiceType: OperationalGroupRowDto[];
  daily: DailyShipmentCountDto[];
}

export interface OperationalReportQuery {
  fromDate?: string | null;
  toDate?: string | null;
  branchId?: number | null;
  senderCustomerId?: number | null;
  driverId?: number | null;
  status?: ShipmentStatus | null;
}

export interface PagedQuery {
  pageNumber?: number;
  pageSize?: number;
  search?: string | null;
  sortBy?: string | null;
  sortDirection?: string | null;
}

export interface PartyAgingDto {
  asOfDate: string;
  partyType: string;
  baseCurrencyCode: string;
  rows: PartyAgingRowDto[];
  totals: PartyAgingRowDto;
}

export interface PartyAgingQuery {
  asOfDate?: string | null;
  branchId?: number | null;
}

export interface PartyAgingRowDto {
  partyId: number;
  partyCode: string;
  partyName: string;
  days0To30: number;
  days31To60: number;
  days61To90: number;
  over90Days: number;
  totalOpen: number;
  unapplied: number;
  netBalance: number;
}

export interface PaySenderSettlementRequest {
  paymentMethodId?: number;
  cashboxId?: number | null;
  bankAccountId?: number | null;
  referenceNumber?: string | null;
  paidAtUtc?: string | null;
}

export interface PaymentAllocationDto {
  id: number;
  invoiceId: number;
  invoiceNumber: string | null;
  amount: number;
  allocatedAtUtc: string;
  invoiceBalanceDue: number;
}

export interface PaymentAllocationRequest {
  invoiceId?: number;
  amount?: number;
}

export interface PaymentMethodDto extends MasterDataDto {
  code: string;
  arabicName: string;
  englishName: string | null;
  kind: PaymentMethodKind;
  requiresReference: boolean;
  sortOrder: number;
}

export interface PaymentMethodListQuery extends MasterDataListQuery {
  kind?: PaymentMethodKind | null;
}

export interface PeriodCloseCheckDto {
  periodId: number;
  periodName: string;
  status: AccountingPeriodStatus;
  unpostedEntries: number;
  entriesAwaitingApproval: number;
  pendingPostingEvents: number;
  failedPostingEvents: number;
  canClose: boolean;
  blockers: string[];
}

export interface PermissionDto {
  code: string;
  module: string;
  description: string;
}

export interface PostingEventDto {
  id: number;
  eventId: string;
  outboxMessageId: number | null;
  eventType: string;
  sourceType: string | null;
  sourceId: number | null;
  sourceNumber: string | null;
  sourceKey: string | null;
  occurredAtUtc: string;
  postingDate: string | null;
  status: PostingEventStatus;
  statusName: string;
  journalEntryId: number | null;
  journalEntryNumber: string | null;
  attempts: number;
  lastError: string | null;
  processedAtUtc: string | null;
}

export interface PostingEventListQuery extends PagedQuery {
  status?: PostingEventStatus | null;
  eventType?: string | null;
  sourceType?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface PostingRuleDto {
  id: number;
  accountRole: string;
  accountId: number;
  accountCode: string;
  accountName: string;
  branchId: number | null;
  branchName: string | null;
  currencyId: number | null;
  currencyCode: string | null;
  cashboxId: number | null;
  cashboxName: string | null;
  bankAccountId: number | null;
  bankAccountName: string | null;
  expenseCategoryId: number | null;
  expenseCategoryName: string | null;
  priority: number;
  specificity: number;
  description: string | null;
  isActive: boolean;
}

export interface PostingRuleListQuery extends PagedQuery {
  accountRole?: string | null;
  accountId?: number | null;
  branchId?: number | null;
  isActive?: boolean | null;
}

export interface PostingSummaryDto {
  pendingOutboxMessages: number;
  oldestPendingAtUtc: string | null;
  postedEvents: number;
  skippedEvents: number;
  duplicateEvents: number;
  failedEvents: number;
  lastProcessedAtUtc: string | null;
}

export interface PriceOverrideDto {
  id: number;
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
  senderName: string;
  status: PriceOverrideStatus;
  statusName: string;
  manualShippingCharge: number | null;
  manualDiscountAmount: number;
  reason: string;
  currencyCode: string;
  calculatedTotalShippingCharge: number | null;
  proposedTotalShippingCharge: number;
  deviationPercentage: number | null;
  requiresApproval: boolean;
  requestedByUserId: number | null;
  requestedByUserName: string | null;
  requestedAtUtc: string;
  decidedByUserId: number | null;
  decidedByUserName: string | null;
  decidedAtUtc: string | null;
  decisionNotes: string | null;
  appliedPricingSnapshotId: number | null;
}

export interface PriceOverrideListQuery extends PagedQuery {
  status?: PriceOverrideStatus | null;
  shipmentId?: number | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface PriceRuleConditionDto {
  id: number;
  conditionType: PriceConditionType;
  conditionTypeName: string;
  referenceId: number | null;
  referenceName: string | null;
  minValue: number | null;
  maxValue: number | null;
}

export interface PriceRuleWeightTierDto {
  id: number;
  fromWeightKg: number;
  toWeightKg: number | null;
  pricePerKg: number;
}

export interface PricingPlanDto extends MasterDataDto {
  code: string;
  arabicName: string;
  englishName: string | null;
  currencyId: number;
  currencyCode: string;
  isDefault: boolean;
  contractCustomerId: number | null;
  contractCustomerName: string | null;
  contractReference: string | null;
  validFromUtc: string | null;
  validToUtc: string | null;
  notes: string | null;
  ruleCount: number;
  customerCount: number;
}

export interface PricingPlanListQuery extends MasterDataListQuery {
  currencyId?: number | null;
  isContract?: boolean | null;
  contractCustomerId?: number | null;
}

export interface PricingRuleDto {
  id: number;
  pricingPlanId: number;
  arabicName: string;
  englishName: string | null;
  priority: number;
  validFromUtc: string | null;
  validToUtc: string | null;
  basePrice: number;
  includedWeightKg: number;
  pricePerCubicMeter: number;
  appliesToBasis: PricingBasis | null;
  includedPackageCount: number;
  perPackageFee: number;
  codFeeFixed: number;
  codFeePercentage: number;
  codFeeMinimum: number;
  insuranceFeePercentage: number;
  insuranceFeeMinimum: number;
  pickupFee: number;
  packagingFee: number;
  remoteAreaFee: number;
  returnFee: number;
  discountPercentage: number;
  taxRateId: number | null;
  taxRateName: string | null;
  notes: string | null;
  isActive: boolean;
  conditions: PriceRuleConditionDto[];
  weightTiers: PriceRuleWeightTierDto[];
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface PricingWeightTierDto {
  fromKg: number;
  toKg: number | null;
  kg: number;
  pricePerKg: number;
  charge: number;
}

export interface ProcessPostingsRequest {
  maxCount?: number;
}

export interface ProcessPostingsResultDto {
  processed: number;
  posted: number;
  skipped: number;
  duplicates: number;
  failed: number;
  remaining: number;
}

export interface ProfitLossDto {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  revenue: StatementGroupDto[];
  totalRevenue: number;
  costOfRevenue: StatementGroupDto[];
  totalCostOfRevenue: number;
  grossProfit: number;
  expenses: StatementGroupDto[];
  totalExpenses: number;
  netProfit: number;
  netProfitMarginPercent: number | null;
}

export interface ProfitLossQuery {
  fromDate?: string | null;
  toDate?: string | null;
  branchId?: number | null;
  costCenterId?: number | null;
}

export interface ProofOfDeliveryDto {
  id: number;
  shipmentId: number;
  shipmentNumber: string;
  driverId: number | null;
  driverName: string | null;
  driverManifestId: number | null;
  recipientName: string;
  recipientRelation: string | null;
  deliveredAtUtc: string;
  signatureAttachmentId: number | null;
  signatureUrl: string | null;
  photoAttachmentId: number | null;
  photoUrl: string | null;
  latitude: number | null;
  longitude: number | null;
  notes: string | null;
  createdAtUtc: string;
  createdByUserId: number | null;
}

export interface ReceiveBranchTransferRequest {
  receivedCodes?: string[];
  notes?: string | null;
}

export interface ReceiveShipmentReturnRequest {
  branchId?: number | null;
  notes?: string | null;
}

export interface RecordDeliveryFailureRequest {
  reason?: string;
  notes?: string | null;
  occurredAtUtc?: string | null;
}

export interface RefreshTokenRequest {
  refreshToken?: string;
}

export interface RefreshTokenResult {
  rawToken: string;
  tokenHash: string;
  expiresAtUtc: string;
}

export interface ReopenAccountingPeriodRequest {
  reason?: string;
}

export interface ResolveCodDisputeRequest {
  notes?: string;
}

export interface ResolveDamageCaseRequest {
  resolution?: DamageCaseResolution;
  compensationAmount?: number | null;
  notes?: string;
  foundAtBranchId?: number | null;
}

export interface ResolvePostingAccountQuery {
  accountRole?: string;
  branchId?: number | null;
  currencyId?: number | null;
  cashboxId?: number | null;
  bankAccountId?: number | null;
  expenseCategoryId?: number | null;
}

export interface ResolvedPostingAccountDto {
  accountRole: string;
  accountId: number;
  accountCode: string;
  accountName: string;
  ruleId: number | null;
}

export interface RetryPostingEventRequest {
  postingDate?: string | null;
}

export interface RevenueByBranchDto {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  rows: RevenueByBranchRowDto[];
  totals: RevenueByBranchRowDto;
}

export interface RevenueByBranchQuery {
  fromDate?: string | null;
  toDate?: string | null;
}

export interface RevenueByBranchRowDto {
  branchId: number;
  branchName: string;
  shippingRevenue: number;
  codCommissionRevenue: number;
  additionalServiceRevenue: number;
  otherRevenue: number;
  totalRevenue: number;
  costOfRevenue: number;
  expenses: number;
  netProfit: number;
}

export interface ReverseJournalEntryRequest {
  reversalDate?: string | null;
  reason?: string;
}

export interface RevokeTokenRequest {
  refreshToken?: string;
}

export interface RoleDto {
  id: number;
  name: string;
  arabicName: string;
  description: string | null;
  isSystemRole: boolean;
  isActive: boolean;
  userCount: number;
  permissions: string[];
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface SaveAccountCategoryRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  accountType?: AccountType;
  cashFlowActivity?: CashFlowActivity;
  sortOrder?: number;
  isActive?: boolean;
}

export interface SaveAccountRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  accountType?: AccountType;
  accountCategoryId?: number | null;
  parentAccountId?: number | null;
  isPostable?: boolean;
  isCashAccount?: boolean;
  allowManualPosting?: boolean;
  requiresCostCenter?: boolean;
  currencyId?: number | null;
  description?: string | null;
  isActive?: boolean;
}

export interface SaveAccountingDimensionRequest {
  dimensionType?: DimensionType;
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  branchId?: number | null;
  parentId?: number | null;
  description?: string | null;
  isActive?: boolean;
}

export interface SaveBankAccountRequest {
  code?: string;
  arabicName?: string;
  bankName?: string;
  accountNumber?: string;
  iban?: string | null;
  currencyId?: number;
  branchId?: number | null;
  isActive?: boolean;
  notes?: string | null;
  accountId?: number | null;
}

export interface SaveBranchRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  cityId?: number;
  address?: string | null;
  phone?: string | null;
  email?: string | null;
  isHeadOffice?: boolean;
  notes?: string | null;
}

export interface SaveCityRequest {
  countryId?: number;
  code?: string;
  arabicName?: string;
  englishName?: string | null;
}

export interface SaveCompanyProfileRequest {
  arabicName?: string;
  englishName?: string | null;
  commercialRegistrationNumber?: string | null;
  taxNumber?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  address?: string | null;
  logoUrl?: string | null;
  baseCurrencyId?: number;
}

export interface SaveCountryRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  phoneCode?: string | null;
}

export interface SaveCurrencyRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  symbol?: string;
  decimalPlaces?: number;
  isBaseCurrency?: boolean;
}

export interface SaveCustomerAddressRequest {
  label?: string;
  cityId?: number;
  zoneId?: number | null;
  addressLine?: string;
  landmark?: string | null;
  buildingNumber?: string | null;
  postalCode?: string | null;
  contactName?: string | null;
  phone?: string | null;
  alternatePhone?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  isDefault?: boolean;
}

export interface SaveCustomerRequest {
  code?: string | null;
  customerType?: CustomerType;
  roles?: CustomerRole;
  arabicName?: string;
  englishName?: string | null;
  phone?: string;
  alternatePhone?: string | null;
  email?: string | null;
  nationalOrCompanyNumber?: string | null;
  creditLimit?: number;
  openingBalance?: number;
  notes?: string | null;
  defaultBranchId?: number;
  pricingPlanId?: number | null;
}

export interface SaveDriverRequest {
  employeeId?: number;
  userId?: number | null;
  licenseNumber?: string;
  licenseCategory?: string | null;
  licenseExpiryDateUtc?: string;
  defaultVehicleId?: number | null;
  notes?: string | null;
}

export interface SaveEmployeeRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  branchId?: number;
  jobTitle?: string | null;
  phone?: string | null;
  email?: string | null;
  nationalId?: string | null;
  hireDateUtc?: string | null;
  userId?: number | null;
  notes?: string | null;
}

export interface SaveExchangeRateRequest {
  fromCurrencyId?: number;
  toCurrencyId?: number;
  rate?: number;
  effectiveFromUtc?: string;
  notes?: string | null;
}

export interface SaveExpenseCategoryRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  description?: string | null;
}

export interface SaveJournalEntryRequest {
  entryDate?: string;
  journalType?: JournalType;
  currencyId?: number | null;
  exchangeRate?: number | null;
  branchId?: number | null;
  description?: string;
  reference?: string | null;
  lines?: JournalLineRequest[];
  submit?: boolean;
}

export interface SavePaymentMethodRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  kind?: PaymentMethodKind;
  requiresReference?: boolean;
  sortOrder?: number;
}

export interface SavePostingRuleRequest {
  accountRole?: string;
  accountId?: number;
  branchId?: number | null;
  currencyId?: number | null;
  cashboxId?: number | null;
  bankAccountId?: number | null;
  expenseCategoryId?: number | null;
  priority?: number;
  description?: string | null;
  isActive?: boolean;
}

export interface SavePriceRuleConditionRequest {
  conditionType?: PriceConditionType;
  referenceId?: number | null;
  minValue?: number | null;
  maxValue?: number | null;
}

export interface SavePriceRuleWeightTierRequest {
  fromWeightKg?: number;
  toWeightKg?: number | null;
  pricePerKg?: number;
}

export interface SavePricingPlanRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  currencyId?: number;
  isDefault?: boolean;
  contractCustomerId?: number | null;
  contractReference?: string | null;
  validFromUtc?: string | null;
  validToUtc?: string | null;
  notes?: string | null;
}

export interface SavePricingRuleRequest {
  arabicName?: string;
  englishName?: string | null;
  priority?: number;
  validFromUtc?: string | null;
  validToUtc?: string | null;
  conditions?: SavePriceRuleConditionRequest[];
  basePrice?: number;
  includedWeightKg?: number;
  weightTiers?: SavePriceRuleWeightTierRequest[];
  pricePerCubicMeter?: number;
  appliesToBasis?: PricingBasis | null;
  includedPackageCount?: number;
  perPackageFee?: number;
  codFeeFixed?: number;
  codFeePercentage?: number;
  codFeeMinimum?: number;
  insuranceFeePercentage?: number;
  insuranceFeeMinimum?: number;
  pickupFee?: number;
  packagingFee?: number;
  remoteAreaFee?: number;
  returnFee?: number;
  discountPercentage?: number;
  taxRateId?: number | null;
  notes?: string | null;
}

export interface SaveServiceTypeRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  description?: string | null;
  estimatedDeliveryDays?: number;
  sortOrder?: number;
}

export interface SaveShipmentRequest {
  senderCustomerId?: number;
  recipientCustomerId?: number;
  senderAddressId?: number | null;
  recipientAddressId?: number | null;
  originBranchId?: number;
  destinationBranchId?: number | null;
  originCityId?: number | null;
  destinationCityId?: number | null;
  destinationZoneId?: number | null;
  serviceTypeId?: number;
  packageDescription?: string;
  packages?: ShipmentPackageRequest[];
  pricingBasis?: PricingBasis;
  totalVolumeCbm?: number;
  chargesPaidBy?: ChargesPayer;
  declaredValue?: number;
  insuranceValue?: number;
  isCOD?: boolean;
  codAmount?: number;
  requiresPickup?: boolean;
  requiresPackaging?: boolean;
  manualShippingCharge?: number | null;
  discountAmount?: number;
  manualPriceReason?: string | null;
  senderNotes?: string | null;
  internalNotes?: string | null;
  externalReference?: string | null;
}

export interface SaveShippingSettingsRequest {
  volumetricDivisor?: number;
  weightRoundingStepKg?: number;
  trackingNumberPrefix?: string;
  labelWidthMm?: number;
  labelHeightMm?: number;
  trackingUrlTemplate?: string | null;
  priceOverrideApprovalThresholdPercent?: number;
  defaultPaymentTermsDays?: number;
}

export interface SaveSupplierRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  contactPerson?: string | null;
  phone?: string;
  alternatePhone?: string | null;
  email?: string | null;
  taxNumber?: string | null;
  address?: string | null;
  openingBalance?: number;
  notes?: string | null;
}

export interface SaveTaxRateRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  percentage?: number;
  isDefault?: boolean;
}

export interface SaveVehicleRequest {
  plateNumber?: string;
  model?: string;
  vehicleType?: VehicleType;
  capacityKg?: number;
  branchId?: number;
  notes?: string | null;
}

export interface SaveWarehouseRequest {
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  branchId?: number;
  address?: string | null;
  phone?: string | null;
  capacityCubicMeters?: number | null;
  notes?: string | null;
}

export interface SaveZoneRequest {
  cityId?: number;
  code?: string;
  arabicName?: string;
  englishName?: string | null;
  isRemoteArea?: boolean;
  notes?: string | null;
}

export interface SenderBalanceDto {
  senderCustomerId: number;
  senderCode: string;
  senderName: string;
  currencyId: number;
  currencyCode: string;
  pendingCount: number;
  pendingAmount: number;
  withDriversAmount: number;
  inCashboxAmount: number;
  readyNetAmount: number;
  disputedAmount: number;
  totalHeldAmount: number;
  openSettlementAmount: number;
}

export interface SenderBalanceQuery extends PagedQuery {
  senderCustomerId?: number | null;
  currencyId?: number | null;
  branchId?: number | null;
  onlyWithBalance?: boolean;
}

export interface SenderSettlementDto {
  id: number;
  settlementNumber: string;
  senderCustomerId: number;
  senderCode: string;
  senderName: string;
  senderPhone: string | null;
  branchId: number;
  branchName: string;
  currencyId: number;
  currencyCode: string;
  currencyName: string;
  currencyDecimals: number;
  status: SettlementStatus;
  statusName: string;
  settlementDateUtc: string;
  totalGrossReleased: number;
  totalShippingCharges: number;
  totalCodCommission: number;
  totalTax: number;
  totalOtherFees: number;
  totalCompensation: number;
  netPayable: number;
  notes: string | null;
  createdByUserId: number | null;
  approvedByUserId: number | null;
  approvedAtUtc: string | null;
  paymentMethodId: number | null;
  paymentMethodName: string | null;
  cashboxId: number | null;
  cashboxName: string | null;
  bankAccountId: number | null;
  bankAccountName: string | null;
  paymentReference: string | null;
  cashPaymentVoucherId: number | null;
  cashPaymentVoucherNumber: string | null;
  paidAtUtc: string | null;
  paidByUserId: number | null;
  cancelledAtUtc: string | null;
  cancellationReason: string | null;
  reversedAtUtc: string | null;
  reversalReason: string | null;
  lines: SenderSettlementLineDto[];
}

export interface SenderSettlementLineDto {
  id: number;
  codTransactionId: number;
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
  recipientName: string;
  deliveredAtUtc: string | null;
  collectedAmount: number;
  remainingBefore: number;
  shippingCharge: number;
  codCommission: number;
  tax: number;
  otherFees: number;
  compensation: number;
  recognizesDeductions: boolean;
  amount: number;
  grossReleased: number;
}

export interface SenderSettlementListItemDto {
  id: number;
  settlementNumber: string;
  senderCustomerId: number;
  senderName: string;
  branchName: string;
  currencyCode: string;
  status: SettlementStatus;
  statusName: string;
  settlementDateUtc: string;
  lineCount: number;
  totalGrossReleased: number;
  totalDeductions: number;
  netPayable: number;
  paidAtUtc: string | null;
}

export interface SenderSettlementListQuery extends PagedQuery {
  status?: SettlementStatus | null;
  senderCustomerId?: number | null;
  branchId?: number | null;
  currencyId?: number | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface SenderSettlementReportDto {
  fromUtc: string;
  toUtc: string;
  currencyCode: string;
  status: SettlementStatus;
  rows: SenderSettlementListItemDto[];
  settlementCount: number;
  shipmentCount: number;
  totalGrossReleased: number;
  totalShippingCharges: number;
  totalCodCommission: number;
  totalTax: number;
  totalOtherFees: number;
  totalCompensation: number;
  totalNetPaid: number;
}

export interface SenderSettlementReportQuery {
  fromUtc?: string | null;
  toUtc?: string | null;
  currencyId?: number | null;
  branchId?: number | null;
  senderCustomerId?: number | null;
  status?: SettlementStatus | null;
}

export interface ServiceTypeDto extends MasterDataDto {
  code: string;
  arabicName: string;
  englishName: string | null;
  description: string | null;
  estimatedDeliveryDays: number;
  sortOrder: number;
}

export interface ServiceTypeListQuery extends MasterDataListQuery {
}

export interface SettlementCandidateDto {
  codTransactionId: number;
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
  status: CODStatus;
  collectedAmount: number;
  shippingChargeDeduction: number;
  codCommissionDeduction: number;
  taxDeduction: number;
  otherFeesAmount: number;
  compensationAmount: number;
  netPayableAmount: number;
  settledAmount: number;
  remainingPayable: number;
  collectedAtUtc: string | null;
}

export interface SettlementCandidatesQuery {
  senderCustomerId?: number;
  currencyId?: number | null;
}

export interface SettlementLineRequest {
  codTransactionId?: number;
  amount?: number | null;
}

export interface SettlementReasonRequest {
  reason?: string;
}

export interface ShipmentAttachmentDto {
  id: number;
  shipmentId: number;
  attachmentType: ShipmentAttachmentType;
  damageCaseId: number | null;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  description: string | null;
  downloadUrl: string;
  createdAtUtc: string;
  createdByUserId: number | null;
}

export interface ShipmentCodesRequest {
  codes?: string[];
}

export interface ShipmentDto {
  id: number;
  shipmentNumber: string;
  trackingNumber: string;
  externalReference: string | null;
  status: ShipmentStatus;
  statusName: string;
  senderCustomerId: number;
  senderCode: string;
  senderName: string;
  senderPhone: string;
  senderAddressId: number | null;
  senderAddressLine: string | null;
  recipientCustomerId: number;
  recipientCode: string;
  recipientName: string;
  recipientPhone: string;
  recipientAddressId: number | null;
  recipientAddressLine: string | null;
  recipientContactPhone: string | null;
  originBranchId: number;
  originBranchName: string;
  currentBranchId: number;
  currentBranchName: string;
  currentWarehouseId: number | null;
  currentWarehouseName: string | null;
  destinationBranchId: number | null;
  destinationBranchName: string | null;
  originCityId: number;
  originCityName: string;
  destinationCityId: number;
  destinationCityName: string;
  originZoneId: number | null;
  originZoneName: string | null;
  destinationZoneId: number | null;
  destinationZoneName: string | null;
  serviceTypeId: number;
  serviceTypeName: string;
  assignedDriverId: number | null;
  assignedDriverName: string | null;
  vehicleId: number | null;
  vehiclePlateNumber: string | null;
  deliveryAttemptCount: number;
  returnBranchId: number | null;
  packageCount: number;
  actualWeight: number;
  volumetricWeight: number;
  chargeableWeight: number;
  totalVolumeCbm: number;
  pricingBasis: PricingBasis;
  chargesPaidBy: ChargesPayer;
  packageDescription: string;
  currencyId: number;
  currencyCode: string;
  declaredValue: number;
  insuranceValue: number;
  isCOD: boolean;
  codAmount: number;
  requiresPickup: boolean;
  requiresPackaging: boolean;
  shippingCharge: number;
  discountAmount: number;
  taxAmount: number;
  totalCharge: number;
  manualShippingCharge: number | null;
  manualDiscountAmount: number;
  invoiceId: number | null;
  invoiceNumber: string | null;
  hasPendingPriceOverride: boolean;
  codTransactionId: number | null;
  codStatus: CODStatus | null;
  codCollectedAmount: number | null;
  senderNotes: string | null;
  internalNotes: string | null;
  cancellationReason: string | null;
  createdAtUtc: string;
  createdByUserId: number | null;
  updatedAtUtc: string | null;
  updatedByUserId: number | null;
  pickedUpAtUtc: string | null;
  deliveredAtUtc: string | null;
  returnedAtUtc: string | null;
  cancelledAtUtc: string | null;
  packages: ShipmentPackageDto[];
  nextStatuses: ShipmentStatus[];
}

export interface ShipmentImportPreviewDto {
  fileName: string;
  totalRows: number;
  validRows: number;
  invalidRows: number;
  canImport: boolean;
  rows: ShipmentImportRowDto[];
}

export interface ShipmentImportResultDto {
  importedCount: number;
  shipments: ImportedShipmentDto[];
}

export interface ShipmentImportRowDto {
  rowNumber: number;
  isValid: boolean;
  errors: string[];
  externalReference: string | null;
  senderCustomerCode: string | null;
  recipientCustomerCode: string | null;
  destinationCityCode: string | null;
  serviceTypeCode: string | null;
  packageCount: number;
  weightKg: number;
  isCOD: boolean;
  codAmount: number;
  totalCharge: number | null;
  currencyCode: string | null;
}

export interface ShipmentListItemDto {
  id: number;
  shipmentNumber: string;
  trackingNumber: string;
  externalReference: string | null;
  status: ShipmentStatus;
  statusName: string;
  senderCustomerId: number;
  senderName: string;
  senderPhone: string;
  recipientCustomerId: number;
  recipientName: string;
  recipientPhone: string;
  originBranchId: number;
  originBranchName: string;
  currentBranchId: number;
  currentBranchName: string;
  destinationBranchId: number | null;
  destinationBranchName: string | null;
  destinationCityId: number;
  destinationCityName: string;
  serviceTypeId: number;
  serviceTypeName: string;
  assignedDriverId: number | null;
  assignedDriverName: string | null;
  packageCount: number;
  chargeableWeight: number;
  totalVolumeCbm: number;
  pricingBasis: PricingBasis;
  chargesPaidBy: ChargesPayer;
  isCOD: boolean;
  codAmount: number;
  totalCharge: number;
  currencyCode: string;
  createdAtUtc: string;
  deliveredAtUtc: string | null;
}

export interface ShipmentListQuery extends PagedQuery {
  trackingNumber?: string | null;
  shipmentNumber?: string | null;
  sender?: string | null;
  recipient?: string | null;
  status?: ShipmentStatus | null;
  branchId?: number | null;
  originBranchId?: number | null;
  currentBranchId?: number | null;
  destinationBranchId?: number | null;
  driverId?: number | null;
  senderCustomerId?: number | null;
  recipientCustomerId?: number | null;
  originCityId?: number | null;
  destinationCityId?: number | null;
  serviceTypeId?: number | null;
  isCOD?: boolean | null;
  createdFromUtc?: string | null;
  createdToUtc?: string | null;
}

export interface ShipmentPackageDto {
  id: number;
  sequenceNumber: number;
  packageBarcode: string;
  description: string | null;
  weightKg: number;
  lengthCm: number | null;
  widthCm: number | null;
  heightCm: number | null;
  volumetricWeightKg: number;
}

export interface ShipmentPackageRequest {
  description?: string | null;
  weightKg?: number;
  lengthCm?: number | null;
  widthCm?: number | null;
  heightCm?: number | null;
}

export interface ShipmentPriceRequest {
  manualShippingCharge?: number | null;
  manualDiscountAmount?: number;
  reason?: string | null;
}

export interface ShipmentPriceResultDto {
  shipmentId: number;
  pricing: ShipmentPricingDto;
  rulePricing: ShipmentPricingDto | null;
  isManual: boolean;
  deviationPercentage: number | null;
  approvalThresholdPercent: number;
  requiresApproval: boolean;
  applied: boolean;
  override: PriceOverrideDto | null;
}

export interface ShipmentPricingDto {
  id: number | null;
  reason: string;
  pricingSource: PricingSource;
  pricingPlanId: number | null;
  pricingPlanCode: string | null;
  pricingRuleId: number | null;
  pricingRuleName: string | null;
  currencyId: number;
  currencyCode: string;
  actualWeight: number;
  volumetricWeight: number;
  chargeableWeight: number;
  volumetricDivisor: number;
  weightRoundingStepKg: number;
  packageCount: number;
  pricingBasis: PricingBasis;
  volumeCbm: number;
  pricePerCubicMeter: number;
  volumeCharge: number;
  insuranceValue: number;
  requiresPickup: boolean;
  requiresPackaging: boolean;
  isRemoteArea: boolean;
  isReturn: boolean;
  baseCharge: number;
  includedWeightKg: number;
  additionalWeightKg: number;
  weightCharge: number;
  weightTiers: PricingWeightTierDto[];
  packageFee: number;
  insuranceFee: number;
  pickupFee: number;
  packagingFee: number;
  remoteAreaFee: number;
  returnFee: number;
  serviceFeesTotal: number;
  shippingSubtotal: number;
  calculatedShippingSubtotal: number | null;
  contractDiscountPercentage: number;
  contractDiscountAmount: number;
  manualDiscountAmount: number;
  discountAmount: number;
  totalShippingCharge: number;
  codAmount: number;
  codFeeFixed: number;
  codFeePercentage: number;
  codFeeMinimum: number;
  codCommission: number;
  taxableAmount: number;
  taxRateId: number | null;
  taxPercentage: number;
  taxAmount: number;
  netTotal: number;
  shippingCharge: number;
  totalCharge: number;
  overrideReason: string | null;
  priceOverrideId: number | null;
  calculatedAtUtc: string | null;
}

export interface ShipmentReturnDto {
  id: number;
  returnNumber: string;
  shipmentId: number;
  shipmentNumber: string;
  trackingNumber: string;
  shipmentStatus: ShipmentStatus;
  shipmentStatusName: string;
  shipmentCurrentBranchId: number;
  returnType: ShipmentReturnType;
  status: ShipmentReturnStatus;
  reason: string;
  returnBranchId: number;
  returnBranchName: string;
  returnCharge: number;
  receivedBackAtUtc: string | null;
  completedAtUtc: string | null;
  cancelledAtUtc: string | null;
  notes: string | null;
  createdAtUtc: string;
  createdByUserId: number | null;
}

export interface ShipmentReturnListQuery extends DocumentListQuery {
  status?: ShipmentReturnStatus | null;
  returnType?: ShipmentReturnType | null;
  returnBranchId?: number | null;
  shipmentId?: number | null;
}

export interface ShipmentStatusHistoryDto {
  id: number;
  fromStatus: ShipmentStatus | null;
  fromStatusName: string | null;
  toStatus: ShipmentStatus;
  toStatusName: string;
  source: ShipmentStatusChangeSource;
  occurredAtUtc: string;
  branchId: number | null;
  branchName: string | null;
  warehouseId: number | null;
  driverId: number | null;
  notes: string | null;
  referenceType: string | null;
  referenceId: number | null;
  referenceNumber: string | null;
  isCorrection: boolean;
  changedByUserId: number | null;
  changedByUserName: string | null;
  recordedAtUtc: string;
}

export interface ShipmentTrackingDto {
  shipmentId: number;
  trackingNumber: string;
  shipmentNumber: string;
  status: ShipmentStatus;
  statusName: string;
  originCityName: string;
  destinationCityName: string;
  currentBranchName: string;
  packageCount: number;
  createdAtUtc: string;
  deliveredAtUtc: string | null;
  events: ShipmentTrackingEventDto[];
}

export interface ShipmentTrackingEventDto {
  occurredAtUtc: string;
  status: ShipmentStatus;
  statusName: string;
  branchName: string | null;
  notes: string | null;
}

export interface ShippingSettingsDto {
  volumetricDivisor: number;
  weightRoundingStepKg: number;
  trackingNumberPrefix: string;
  labelWidthMm: number;
  labelHeightMm: number;
  trackingUrlTemplate: string | null;
  priceOverrideApprovalThresholdPercent: number;
  defaultPaymentTermsDays: number;
  isDefault: boolean;
  updatedAtUtc: string | null;
}

export interface StatementAccountDto {
  accountId: number;
  code: string;
  arabicName: string;
  amount: number;
}

export interface StatementGroupDto {
  categoryId: number | null;
  code: string;
  arabicName: string;
  amount: number;
  accounts: StatementAccountDto[];
}

export interface StatementLineDto {
  transactionId: number;
  transactionDateUtc: string;
  transactionType: CustomerAccountTransactionType;
  transactionTypeName: string;
  documentType: string;
  documentId: number;
  documentNumber: string;
  description: string;
  branchName: string;
  debit: number;
  credit: number;
  balance: number;
}

export interface StatusCountDto {
  status: ShipmentStatus;
  statusName: string;
  count: number;
}

export interface SubledgerBalanceRowDto {
  referenceId: number;
  code: string;
  name: string;
  branchName: string | null;
  currencyCode: string;
  subledgerBalance: number;
  ledgerBalance: number;
  difference: number;
  extra: number | null;
}

export interface SubledgerReconciliationDto {
  title: string;
  generatedAtUtc: string;
  rows: SubledgerBalanceRowDto[];
  totals: CurrencyTotalDto[];
  pendingPostingEvents: number;
  failedPostingEvents: number;
  isReconciled: boolean;
}

export interface SupplierDto extends MasterDataDto {
  code: string;
  arabicName: string;
  englishName: string | null;
  contactPerson: string | null;
  phone: string;
  alternatePhone: string | null;
  email: string | null;
  taxNumber: string | null;
  address: string | null;
  openingBalance: number;
  notes: string | null;
}

export interface SupplierListQuery extends MasterDataListQuery {
}

export interface SystemHealthDto {
  applicationName: string;
  version: string;
  environment: string;
  status: string;
  databaseStatus: string;
  databaseMessage: string | null;
  serverTimeUtc: string;
}

export interface TaxRateDto extends MasterDataDto {
  code: string;
  arabicName: string;
  englishName: string | null;
  percentage: number;
  isDefault: boolean;
}

export interface TaxRateListQuery extends MasterDataListQuery {
}

export interface TokenUserInfo {
  userId: number;
  userName: string;
  fullName: string;
  branchId: number | null;
  securityStamp: string;
  roles: string[];
  permissions: string[];
}

export interface TrialBalanceDto {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  rows: TrialBalanceRowDto[];
  totalOpeningDebit: number;
  totalOpeningCredit: number;
  totalPeriodDebit: number;
  totalPeriodCredit: number;
  totalClosingDebit: number;
  totalClosingCredit: number;
  isBalanced: boolean;
}

export interface TrialBalanceQuery {
  fromDate?: string | null;
  toDate?: string | null;
  branchId?: number | null;
  costCenterId?: number | null;
  includeZeroBalances?: boolean;
  level?: number | null;
}

export interface TrialBalanceRowDto {
  accountId: number;
  code: string;
  arabicName: string;
  accountType: AccountType;
  level: number;
  isPostable: boolean;
  openingDebit: number;
  openingCredit: number;
  periodDebit: number;
  periodCredit: number;
  closingDebit: number;
  closingCredit: number;
}

export interface UpdateAccountingPeriodRequest {
  name?: string;
}

export interface UpdateCashboxRequest {
  arabicName?: string;
  englishName?: string | null;
  custodianUserId?: number | null;
  isActive?: boolean;
  notes?: string | null;
}

export interface UpdateDraftInvoiceRequest {
  paymentTermsDays?: number;
  invoiceDiscountAmount?: number;
  notes?: string | null;
}

export interface UpdateFiscalYearRequest {
  arabicName?: string;
}

export interface UpdateRolePermissionsRequest {
  permissions?: string[];
}

export interface UpdateUserRequest {
  email?: string;
  phoneNumber?: string | null;
  fullName?: string;
  arabicFullName?: string;
  branchId?: number | null;
  employeeCode?: string | null;
  roles?: string[];
}

export interface UploadShipmentAttachmentRequest {
  attachmentType?: ShipmentAttachmentType;
  description?: string | null;
  damageCaseId?: number | null;
}

export interface UserDto {
  id: number;
  userName: string;
  email: string | null;
  phoneNumber: string | null;
  fullName: string;
  arabicFullName: string;
  branchId: number | null;
  employeeCode: string | null;
  isActive: boolean;
  isLockedOut: boolean;
  lockoutEndUtc: string | null;
  lastLoginAtUtc: string | null;
  roles: string[];
  createdAtUtc: string;
  createdByUserId: number | null;
  updatedAtUtc: string | null;
  updatedByUserId: number | null;
}

export interface UserListQuery extends PagedQuery {
  role?: string | null;
  branchId?: number | null;
  isActive?: boolean | null;
}

export interface UserSessionState {
  userId: number;
  isActive: boolean;
  securityStamp: string;
}

export interface VehicleDto extends MasterDataDto {
  plateNumber: string;
  model: string;
  vehicleType: VehicleType;
  capacityKg: number;
  branchId: number;
  branchName: string;
  notes: string | null;
}

export interface VehicleListQuery extends MasterDataListQuery {
  branchId?: number | null;
  vehicleType?: VehicleType | null;
}

export interface WarehouseDto extends MasterDataDto {
  code: string;
  arabicName: string;
  englishName: string | null;
  branchId: number;
  branchName: string;
  address: string | null;
  phone: string | null;
  capacityCubicMeters: number | null;
  notes: string | null;
}

export interface WarehouseListQuery extends MasterDataListQuery {
  branchId?: number | null;
}

export interface WarehouseManifestDto {
  id: number;
  manifestNumber: string;
  movementType: WarehouseMovementType;
  branchId: number;
  branchName: string;
  warehouseId: number;
  warehouseName: string;
  status: WarehouseManifestStatus;
  itemCount: number;
  confirmedAtUtc: string | null;
  confirmedByUserId: number | null;
  notes: string | null;
  createdAtUtc: string;
  createdByUserId: number | null;
  items: ManifestShipmentDto[] | null;
}

export interface WarehouseManifestListQuery extends DocumentListQuery {
  branchId?: number | null;
  warehouseId?: number | null;
  status?: WarehouseManifestStatus | null;
  movementType?: WarehouseMovementType | null;
}

export interface ZoneDto extends MasterDataDto {
  cityId: number;
  cityName: string;
  countryId: number;
  code: string;
  arabicName: string;
  englishName: string | null;
  isRemoteArea: boolean;
  notes: string | null;
}

export interface ZoneListQuery extends MasterDataListQuery {
  cityId?: number | null;
  countryId?: number | null;
  isRemoteArea?: boolean | null;
}
```

### 6.31 `frontend/src/i18n/enums.ts`

```typescript
import type {
  AccountType, AuditAction, BranchTransferStatus, CashPaymentType, CashReceiptType, CashTransferStatus, CashTransferType,
  CashVoucherStatus, CODAdjustmentStatus, CODAdjustmentType, CODStatus, CustomerRole, CustomerType, DriverManifestStatus,
  InvoiceStatus, JournalEntryStatus, JournalType, NormalBalance, PaymentMethodKind, PostingEventStatus, PriceOverrideStatus,
  SettlementStatus, ShipmentReturnStatus, ShipmentReturnType, ShipmentStatus, ShipmentStatusChangeSource, VehicleType,
  AccountingPeriodStatus, FiscalYearStatus, CashboxClosingStatus, DamageCaseStatus, PriceConditionType, ShipmentAttachmentType, BranchTransferItemStatus, DeliveryResult,
  ChargesPayer, PricingBasis
} from '@/api/generated';
import type { Language } from './index';

type Labels<T extends string> = Record<T, [arabic: string, english: string]>;

/** Arabic / English names of the API enums (the API sends the enum NAME, e.g. "OutForDelivery"). */
export const enumLabels = {
  ShipmentStatus: {
    Created: ['مُسجّلة', 'Created'], PickedUp: ['تم الاستلام من المرسل', 'Picked up'], ReceivedAtBranch: ['في الفرع', 'At branch'],
    InWarehouse: ['في المستودع', 'In warehouse'], InTransit: ['في الطريق بين الفروع', 'In transit'],
    ArrivedAtDestination: ['وصلت فرع الوجهة', 'At destination'], OutForDelivery: ['خارج للتوزيع', 'Out for delivery'],
    Delivered: ['تم التسليم', 'Delivered'], DeliveryFailed: ['فشل التسليم', 'Delivery failed'], Returned: ['مرتجعة', 'Returned'],
    Cancelled: ['ملغاة', 'Cancelled'], LostOrDamaged: ['مفقودة / تالفة', 'Lost or damaged']
  } satisfies Labels<ShipmentStatus>,
  ShipmentStatusChangeSource: {
    Creation: ['التسجيل', 'Creation'], Import: ['الاستيراد', 'Import'], Manual: ['يدوي', 'Manual'], WarehouseManifest: ['بيان المستودع', 'Warehouse manifest'],
    BranchTransfer: ['تحويل بين الفروع', 'Branch transfer'], DriverManifest: ['كشف توزيع', 'Driver manifest'], Delivery: ['التسليم', 'Delivery'],
    Return: ['مرتجع', 'Return'], DamageCase: ['حالة تلف', 'Damage case'], Cancellation: ['إلغاء', 'Cancellation'], Correction: ['تصحيح', 'Correction']
  } satisfies Labels<ShipmentStatusChangeSource>,
  ShipmentAttachmentType: {
    Document: ['مستند', 'Document'], PackagePhoto: ['صورة الطرد', 'Package photo'], CommercialInvoice: ['فاتورة تجارية', 'Commercial invoice'],
    Signature: ['توقيع', 'Signature'], DeliveryPhoto: ['صورة التسليم', 'Delivery photo'], DamageEvidence: ['دليل تلف', 'Damage evidence'], Other: ['أخرى', 'Other']
  } satisfies Labels<ShipmentAttachmentType>,
  CODStatus: {
    Pending: ['بانتظار التسليم', 'Pending'], AssignedToDriver: ['مع سائق للتحصيل', 'Assigned to driver'], CollectedByDriver: ['حصّله السائق', 'Collected by driver'],
    ReceivedAtBranchCashbox: ['في صندوق الفرع', 'In branch cashbox'], ReadyForSenderSettlement: ['جاهز للتسوية', 'Ready for settlement'],
    PartiallySettled: ['مسوّى جزئياً', 'Partially settled'], SettledToSender: ['مسوّى للمرسل', 'Settled to sender'], Cancelled: ['ملغى', 'Cancelled'],
    Disputed: ['متنازع عليه', 'Disputed']
  } satisfies Labels<CODStatus>,
  CODAdjustmentType: {
    DriverShortageWriteOff: ['شطب عجز سائق', 'Driver shortage write-off'], DriverExcessCash: ['زيادة نقدية لدى السائق', 'Driver excess cash'],
    CollectionCorrection: ['تصحيح تحصيل', 'Collection correction'], CollectionReversal: ['عكس تحصيل', 'Collection reversal'],
    SenderFee: ['رسم على المرسل', 'Sender fee'], SenderCompensation: ['تعويض للمرسل', 'Sender compensation']
  } satisfies Labels<CODAdjustmentType>,
  CODAdjustmentStatus: { PendingApproval: ['بانتظار الاعتماد', 'Pending approval'], Approved: ['معتمد', 'Approved'], Rejected: ['مرفوض', 'Rejected'] } satisfies Labels<CODAdjustmentStatus>,
  InvoiceStatus: {
    Draft: ['مسودة', 'Draft'], Issued: ['صادرة', 'Issued'], PartiallyPaid: ['مدفوعة جزئياً', 'Partially paid'], Paid: ['مدفوعة', 'Paid'],
    Overdue: ['متأخرة', 'Overdue'], Cancelled: ['ملغاة', 'Cancelled'], Credited: ['مُشعرة دائن', 'Credited']
  } satisfies Labels<InvoiceStatus>,
  JournalEntryStatus: {
    Draft: ['مسودة', 'Draft'], Submitted: ['مقدّم للاعتماد', 'Submitted'], Approved: ['معتمد', 'Approved'], Posted: ['مُرحّل', 'Posted'],
    Reversed: ['معكوس', 'Reversed'], Rejected: ['مرفوض', 'Rejected']
  } satisfies Labels<JournalEntryStatus>,
  JournalType: {
    General: ['عام', 'General'], Opening: ['افتتاحي', 'Opening'], Adjustment: ['تسوية', 'Adjustment'], Reversal: ['عكسي', 'Reversal'],
    Closing: ['إقفال', 'Closing'], Sales: ['مبيعات', 'Sales'], CustomerReceipts: ['مقبوضات العملاء', 'Customer receipts'], Cod: ['تحصيل عند الاستلام', 'COD'],
    Cash: ['نقدية', 'Cash'], SenderSettlement: ['تسوية مرسلين', 'Sender settlement'], Expense: ['مصروفات', 'Expense'], Bank: ['بنك', 'Bank']
  } satisfies Labels<JournalType>,
  SettlementStatus: {
    Draft: ['مسودة', 'Draft'], Approved: ['معتمدة', 'Approved'], Paid: ['مدفوعة', 'Paid'], Cancelled: ['ملغاة', 'Cancelled'], Reversed: ['معكوسة', 'Reversed']
  } satisfies Labels<SettlementStatus>,
  CashVoucherStatus: { Posted: ['مُرحّل', 'Posted'], Cancelled: ['ملغى', 'Cancelled'] } satisfies Labels<CashVoucherStatus>,
  CashTransferStatus: {
    PendingApproval: ['بانتظار الاعتماد', 'Pending approval'], InTransit: ['في الطريق', 'In transit'], Completed: ['مكتمل', 'Completed'],
    Rejected: ['مرفوض', 'Rejected'], Cancelled: ['ملغى', 'Cancelled']
  } satisfies Labels<CashTransferStatus>,
  CashTransferType: {
    BetweenCashboxes: ['بين صناديق', 'Between cashboxes'], BetweenBranches: ['بين فروع', 'Between branches'], CashboxToBank: ['إيداع بنكي', 'Cashbox to bank']
  } satisfies Labels<CashTransferType>,
  CashReceiptType: {
    DriverHandover: ['تسليم عهدة سائق', 'Driver handover'], CounterCodCollection: ['تحصيل في الفرع', 'Counter COD'], CustomerPayment: ['دفعة عميل', 'Customer payment'],
    OtherIncome: ['إيراد آخر', 'Other income'], Other: ['أخرى', 'Other']
  } satisfies Labels<CashReceiptType>,
  CashPaymentType: {
    SenderSettlement: ['تسوية مرسل', 'Sender settlement'], Expense: ['مصروف', 'Expense'], SupplierPayment: ['دفعة مورد', 'Supplier payment'],
    EmployeeAdvance: ['سلفة موظف', 'Employee advance'], CustomerRefund: ['رد مبلغ لعميل', 'Customer refund'], Other: ['أخرى', 'Other']
  } satisfies Labels<CashPaymentType>,
  CashboxClosingStatus: {
    Closed: ['مُقفل', 'Closed'], PendingApproval: ['بانتظار الاعتماد', 'Pending approval'], Approved: ['معتمد', 'Approved'], Rejected: ['مرفوض', 'Rejected']
  } satisfies Labels<CashboxClosingStatus>,
  DriverManifestStatus: { Draft: ['مسودة', 'Draft'], Dispatched: ['خرج للتوزيع', 'Dispatched'], Closed: ['مُغلق', 'Closed'], Cancelled: ['ملغى', 'Cancelled'] } satisfies Labels<DriverManifestStatus>,
  BranchTransferStatus: {
    Draft: ['مسودة', 'Draft'], Dispatched: ['مُرسل', 'Dispatched'], Received: ['مُستلم', 'Received'], PartiallyReceived: ['مستلم جزئياً', 'Partially received'],
    Cancelled: ['ملغى', 'Cancelled']
  } satisfies Labels<BranchTransferStatus>,
  BranchTransferItemStatus: { Pending: ['بانتظار الإرسال', 'Pending'], InTransit: ['في الطريق', 'In transit'], Received: ['مستلمة', 'Received'], Missing: ['مفقودة', 'Missing'] } satisfies Labels<BranchTransferItemStatus>,
  DeliveryResult: { Pending: ['قيد التوزيع', 'Pending'], Delivered: ['سُلّمت', 'Delivered'], Failed: ['فشلت', 'Failed'] } satisfies Labels<DeliveryResult>,
  ShipmentReturnStatus: { Open: ['مفتوح', 'Open'], Completed: ['مكتمل', 'Completed'], Cancelled: ['ملغى', 'Cancelled'] } satisfies Labels<ShipmentReturnStatus>,
  ShipmentReturnType: {
    FailedDelivery: ['فشل التسليم', 'Failed delivery'], RecipientRefused: ['رفض المستلم', 'Recipient refused'], SenderRequest: ['طلب المرسل', 'Sender request'],
    AfterDelivery: ['بعد التسليم', 'After delivery']
  } satisfies Labels<ShipmentReturnType>,
  DamageCaseStatus: { Open: ['مفتوحة', 'Open'], UnderInvestigation: ['قيد التحقيق', 'Under investigation'], Resolved: ['محلولة', 'Resolved'] } satisfies Labels<DamageCaseStatus>,
  PricingBasis: { Weight: ['بالوزن', 'By weight'], Volume: ['بالحجم', 'By volume'] } satisfies Labels<PricingBasis>,
  ChargesPayer: { Sender: ['على المرسل', 'Sender pays'], Recipient: ['على المستلم', 'Recipient pays'] } satisfies Labels<ChargesPayer>,
  PriceOverrideStatus: { Pending: ['بانتظار الاعتماد', 'Pending'], Approved: ['معتمد', 'Approved'], Rejected: ['مرفوض', 'Rejected'], Cancelled: ['ملغى', 'Cancelled'] } satisfies Labels<PriceOverrideStatus>,
  PriceConditionType: {
    OriginZone: ['منطقة المصدر', 'Origin zone'], DestinationZone: ['منطقة الوجهة', 'Destination zone'], OriginCity: ['مدينة المصدر', 'Origin city'],
    DestinationCity: ['مدينة الوجهة', 'Destination city'], ServiceType: ['نوع البضاعة', 'Goods type'], ChargeableWeight: ['الوزن المحتسب', 'Chargeable weight'],
    PackageCount: ['عدد الطرود', 'Package count']
  } satisfies Labels<PriceConditionType>,
  AccountType: {
    Assets: ['أصول', 'Assets'], Liabilities: ['خصوم', 'Liabilities'], Equity: ['حقوق ملكية', 'Equity'], Revenue: ['إيرادات', 'Revenue'],
    CostOfRevenue: ['تكلفة الإيرادات', 'Cost of revenue'], Expenses: ['مصروفات', 'Expenses']
  } satisfies Labels<AccountType>,
  NormalBalance: { Debit: ['مدين', 'Debit'], Credit: ['دائن', 'Credit'] } satisfies Labels<NormalBalance>,
  FiscalYearStatus: { Open: ['مفتوحة', 'Open'], Closed: ['مقفلة', 'Closed'] } satisfies Labels<FiscalYearStatus>,
  AccountingPeriodStatus: { Open: ['مفتوحة', 'Open'], Closed: ['مقفلة', 'Closed'] } satisfies Labels<AccountingPeriodStatus>,
  PostingEventStatus: { Posted: ['مُرحّل', 'Posted'], Skipped: ['متجاوز', 'Skipped'], Duplicate: ['مكرر', 'Duplicate'], Failed: ['فشل', 'Failed'] } satisfies Labels<PostingEventStatus>,
  CustomerType: { Individual: ['فرد', 'Individual'], Business: ['شركة', 'Business'] } satisfies Labels<CustomerType>,
  CustomerRole: { Sender: ['مرسل', 'Sender'], Recipient: ['مستلم', 'Recipient'], Both: ['مرسل ومستلم', 'Both'] } satisfies Labels<CustomerRole>,
  VehicleType: { Motorcycle: ['دراجة نارية', 'Motorcycle'], Car: ['سيارة', 'Car'], Van: ['فان', 'Van'], Truck: ['شاحنة', 'Truck'], Other: ['أخرى', 'Other'] } satisfies Labels<VehicleType>,
  PaymentMethodKind: {
    Cash: ['نقداً', 'Cash'], CashOnDelivery: ['عند الاستلام', 'Cash on delivery'], BankTransfer: ['تحويل بنكي', 'Bank transfer'], Card: ['بطاقة', 'Card'],
    CustomerAccount: ['على الحساب', 'Customer account'], Wallet: ['محفظة', 'Wallet']
  } satisfies Labels<PaymentMethodKind>,
  AuditAction: {
    Login: ['تسجيل دخول', 'Login'], LoginFailed: ['فشل دخول', 'Login failed'], RefreshTokenRevoked: ['إلغاء جلسة', 'Session revoked'],
    RefreshTokenReuseDetected: ['محاولة إعادة استخدام جلسة', 'Refresh token reuse'], PasswordChanged: ['تغيير كلمة المرور', 'Password changed'],
    UserCreated: ['إنشاء مستخدم', 'User created'], UserUpdated: ['تعديل مستخدم', 'User updated'], UserActivated: ['تفعيل مستخدم', 'User activated'],
    UserDeactivated: ['إيقاف مستخدم', 'User deactivated'], RoleCreated: ['إنشاء دور', 'Role created'], RolePermissionsChanged: ['تعديل صلاحيات دور', 'Role permissions changed'],
    MasterDataCreated: ['إضافة بيانات أساسية', 'Master data created'], MasterDataUpdated: ['تعديل بيانات أساسية', 'Master data updated'],
    MasterDataActivated: ['تفعيل بيانات أساسية', 'Master data activated'], MasterDataDeactivated: ['إيقاف بيانات أساسية', 'Master data deactivated'],
    MasterDataDeleted: ['حذف بيانات أساسية', 'Master data deleted'], ShipmentCreated: ['تسجيل شحنة', 'Shipment created'], ShipmentUpdated: ['تعديل شحنة', 'Shipment updated'],
    ShipmentCancelled: ['إلغاء شحنة', 'Shipment cancelled'], ShipmentStatusCorrected: ['تصحيح حالة شحنة', 'Status corrected'], ShipmentsImported: ['استيراد شحنات', 'Shipments imported'],
    ShipmentAttachmentUploaded: ['رفع مرفق', 'Attachment uploaded'], ShipmentAttachmentDeleted: ['حذف مرفق', 'Attachment deleted'],
    ShipmentDriverAssigned: ['إسناد سائق', 'Driver assigned'], ShippingSettingsChanged: ['تعديل إعدادات الشحن', 'Shipping settings changed'],
    ShipmentRepriced: ['إعادة تسعير', 'Shipment repriced'], PriceOverrideRequested: ['طلب سعر يدوي', 'Price override requested'],
    PriceOverrideApproved: ['اعتماد سعر يدوي', 'Price override approved'], PriceOverrideRejected: ['رفض سعر يدوي', 'Price override rejected'],
    InvoiceCreated: ['إنشاء فاتورة', 'Invoice created'], InvoiceUpdated: ['تعديل فاتورة', 'Invoice updated'], InvoiceIssued: ['إصدار فاتورة', 'Invoice issued'],
    InvoiceCancelled: ['إلغاء فاتورة', 'Invoice cancelled'], CreditNoteIssued: ['إشعار دائن', 'Credit note issued'],
    CustomerPaymentRecorded: ['تسجيل دفعة عميل', 'Customer payment recorded'], CustomerPaymentAllocated: ['توزيع دفعة', 'Payment allocated'],
    CodCollected: ['تحصيل نقدي', 'COD collected'], DriverCashHandedOver: ['تسليم عهدة سائق', 'Driver cash handed over'], CodMarkedReady: ['تجهيز للتسوية', 'COD marked ready'],
    CodDisputeOpened: ['فتح نزاع تحصيل', 'COD dispute opened'], CodDisputeResolved: ['حل نزاع تحصيل', 'COD dispute resolved'],
    CodAdjustmentRequested: ['طلب تسوية تحصيل', 'COD adjustment requested'], CodAdjustmentApproved: ['اعتماد تسوية تحصيل', 'COD adjustment approved'],
    CodAdjustmentRejected: ['رفض تسوية تحصيل', 'COD adjustment rejected'], SenderSettlementCreated: ['إنشاء تسوية مرسل', 'Settlement created'],
    SenderSettlementApproved: ['اعتماد تسوية مرسل', 'Settlement approved'], SenderSettlementPaid: ['دفع تسوية مرسل', 'Settlement paid'],
    SenderSettlementCancelled: ['إلغاء تسوية مرسل', 'Settlement cancelled'], SenderSettlementReversed: ['عكس تسوية مرسل', 'Settlement reversed'],
    CashboxCreated: ['إنشاء صندوق', 'Cashbox created'], CashboxUpdated: ['تعديل صندوق', 'Cashbox updated'], CashReceiptPosted: ['سند قبض', 'Cash receipt posted'],
    CashPaymentPosted: ['سند صرف', 'Cash payment posted'], CashVoucherCancelled: ['إلغاء سند', 'Voucher cancelled'],
    CashTransferRequested: ['طلب تحويل نقدي', 'Cash transfer requested'], CashTransferApproved: ['اعتماد تحويل نقدي', 'Cash transfer approved'],
    CashTransferRejected: ['رفض تحويل نقدي', 'Cash transfer rejected'], CashTransferReceived: ['استلام تحويل نقدي', 'Cash transfer received'],
    CashTransferCancelled: ['إلغاء تحويل نقدي', 'Cash transfer cancelled'], CashboxDayClosed: ['إقفال يومي', 'Cashbox day closed'],
    CashboxClosingApproved: ['اعتماد إقفال', 'Closing approved'], CashboxClosingRejected: ['رفض إقفال', 'Closing rejected'],
    BankAccountSaved: ['حفظ حساب بنكي', 'Bank account saved'], AccountSaved: ['حفظ حساب', 'Account saved'], AccountDeleted: ['حذف حساب', 'Account deleted'],
    AccountCategorySaved: ['حفظ تصنيف حساب', 'Account category saved'], AccountingDimensionSaved: ['حفظ بُعد محاسبي', 'Dimension saved'],
    FiscalYearCreated: ['إنشاء سنة مالية', 'Fiscal year created'], FiscalYearUpdated: ['تعديل سنة مالية', 'Fiscal year updated'],
    FiscalYearDeleted: ['حذف سنة مالية', 'Fiscal year deleted'], FiscalYearClosed: ['إقفال سنة مالية', 'Fiscal year closed'],
    AccountingPeriodClosed: ['إقفال فترة', 'Period closed'], AccountingPeriodReopened: ['إعادة فتح فترة', 'Period reopened'],
    JournalEntryCreated: ['إنشاء قيد', 'Journal created'], JournalEntryUpdated: ['تعديل قيد', 'Journal updated'], JournalEntryDeleted: ['حذف قيد', 'Journal deleted'],
    JournalEntrySubmitted: ['تقديم قيد', 'Journal submitted'], JournalEntryApproved: ['اعتماد قيد', 'Journal approved'], JournalEntryRejected: ['رفض قيد', 'Journal rejected'],
    JournalEntryPosted: ['ترحيل قيد', 'Journal posted'], JournalEntryReversed: ['عكس قيد', 'Journal reversed'], PostingRuleSaved: ['حفظ قاعدة ترحيل', 'Posting rule saved'],
    PostingRuleDeleted: ['حذف قاعدة ترحيل', 'Posting rule deleted'], PostingEventRetried: ['إعادة محاولة ترحيل', 'Posting retried'],
    BankStatementImported: ['استيراد كشف بنك', 'Bank statement imported'], BankReconciliationSaved: ['حفظ مطابقة بنكية', 'Reconciliation saved'],
    BankReconciliationCompleted: ['إكمال مطابقة بنكية', 'Reconciliation completed'], AccountingPeriodUpdated: ['تعديل فترة', 'Period updated']
  } satisfies Labels<AuditAction>
} as const;

export type EnumName = keyof typeof enumLabels;

export function enumLabel(name: EnumName, value: string | null | undefined, language: Language = 'ar'): string {
  if (!value) {
    return '-';
  }

  const pair = (enumLabels[name] as Record<string, readonly [string, string]>)[value];
  return pair ? pair[language === 'en' ? 1 : 0] : value;
}

/** Options for a select: [{ value, label }] in the declaration order. */
export function enumOptions(name: EnumName, language: Language = 'ar'): { value: string; label: string }[] {
  return Object.entries(enumLabels[name] as Record<string, readonly [string, string]>).map(([value, pair]) => ({
    value,
    label: pair[language === 'en' ? 1 : 0]
  }));
}
```

### 6.32 `frontend/src/components/layout/AppLayout.tsx`

```tsx
import { Suspense, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  AppBar, Avatar, Box, CircularProgress, Divider, Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText,
  ListSubheader, Menu, MenuItem, Toolbar, Tooltip, Typography, useMediaQuery, useTheme
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import TranslateIcon from '@mui/icons-material/Translate';
import DarkModeOutlinedIcon from '@mui/icons-material/DarkModeOutlined';
import LightModeOutlinedIcon from '@mui/icons-material/LightModeOutlined';
import LogoutIcon from '@mui/icons-material/Logout';
import PersonOutlineIcon from '@mui/icons-material/PersonOutline';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import type { CompanyProfileDto } from '@/api/generated';
import { navigation } from '@/app/navigation';
import { useColorMode } from '@/app/AppProviders';
import { useAuth } from '@/auth/AuthProvider';
import { setLanguage, useLanguage } from '@/i18n';

const DRAWER_WIDTH = 264;

function SideNav({ onNavigate }: { onNavigate?: () => void }) {
  const { t } = useTranslation();
  const { hasAny } = useAuth();
  const location = useLocation();

  // The most specific matching path is the active item (/shipments/new must not also light /shipments).
  const allPaths = navigation.flatMap((group) => group.items.map((item) => item.path));
  const active = allPaths
    .filter((path) => (path === '/' ? location.pathname === '/' : location.pathname === path || location.pathname.startsWith(`${path}/`)))
    .sort((a, b) => b.length - a.length)[0];

  return (
    <Box component="nav" sx={{ overflowY: 'auto', pb: 4 }}>
      {navigation.map((group) => {
        const items = group.items.filter((item) => !item.any || hasAny(...item.any));
        if (items.length === 0) {
          return null;
        }

        return (
          <List key={group.key} dense subheader={<ListSubheader sx={{ lineHeight: '32px', bgcolor: 'transparent' }}>{t(`nav.${group.key}`)}</ListSubheader>}>
            {items.map((item) => (
              <ListItemButton
                key={item.path}
                component={NavLink}
                to={item.path}
                end
                selected={active === item.path}
                onClick={onNavigate}
                sx={{ mx: 1, borderRadius: 1.5 }}
              >
                <ListItemIcon sx={{ minWidth: 36 }}>{item.icon}</ListItemIcon>
                <ListItemText primary={t(`nav.${item.key}`)} />
              </ListItemButton>
            ))}
          </List>
        );
      })}
    </Box>
  );
}

/** Shell: top bar (language, theme, user menu) + side menu (permanent on desktop, a drawer on phones). */
export default function AppLayout() {
  const theme = useTheme();
  const desktop = useMediaQuery(theme.breakpoints.up('md'));
  const [mobileOpen, setMobileOpen] = useState(false);
  const [userMenu, setUserMenu] = useState<HTMLElement | null>(null);
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const language = useLanguage();
  // Header shows the saved company name; before the profile exists (404) it falls back to the default text.
  const companyProfile = useQuery({ queryKey: ['/company-profile'], queryFn: () => api.get<CompanyProfileDto>('/company-profile'), staleTime: 5 * 60_000 });
  const colorMode = useColorMode();
  const navigate = useNavigate();

  const drawer = (
    <>
      <Toolbar sx={{ gap: 1 }}>
        <Box component="img" src="/favicon.svg" alt="" sx={{ width: 32, height: 32 }} />
        <Typography variant="h6" noWrap>
          {t('app.name')}
        </Typography>
      </Toolbar>
      <Divider />
      <SideNav onNavigate={desktop ? undefined : () => setMobileOpen(false)} />
    </>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar position="fixed" color="inherit" elevation={0} sx={{ borderBottom: 1, borderColor: 'divider', width: { md: `calc(100% - ${DRAWER_WIDTH}px)` }, marginInlineStart: { md: `${DRAWER_WIDTH}px` } }}>
        <Toolbar sx={{ gap: 1 }}>
          {!desktop && (
            <IconButton edge="start" onClick={() => setMobileOpen(true)} aria-label="menu">
              <MenuIcon />
            </IconButton>
          )}
          <Typography variant="subtitle1" sx={{ flexGrow: 1, fontWeight: 600 }} noWrap>
            {(language === 'en' ? companyProfile.data?.englishName || companyProfile.data?.arabicName : companyProfile.data?.arabicName) || t('app.company')}
          </Typography>
          <Tooltip title={language === 'ar' ? 'English' : 'العربية'}>
            <IconButton onClick={() => setLanguage(language === 'ar' ? 'en' : 'ar')} aria-label={t('common.language')}>
              <TranslateIcon />
            </IconButton>
          </Tooltip>
          <Tooltip title={t('common.darkMode')}>
            <IconButton onClick={colorMode.toggle}>{colorMode.mode === 'dark' ? <LightModeOutlinedIcon /> : <DarkModeOutlinedIcon />}</IconButton>
          </Tooltip>
          <IconButton onClick={(event) => setUserMenu(event.currentTarget)} sx={{ p: 0.5 }}>
            <Avatar sx={{ width: 34, height: 34, bgcolor: 'primary.main', fontSize: 15 }}>{(user?.arabicFullName || user?.userName || '?').slice(0, 1)}</Avatar>
          </IconButton>
          <Menu anchorEl={userMenu} open={!!userMenu} onClose={() => setUserMenu(null)}>
            <Box sx={{ px: 2, py: 1 }}>
              <Typography fontWeight={700}>{language === 'ar' ? user?.arabicFullName : user?.fullName}</Typography>
              <Typography variant="caption" color="text.secondary">
                {user?.userName} · {user?.roles.join('، ')}
              </Typography>
            </Box>
            <Divider />
            <MenuItem onClick={() => { setUserMenu(null); navigate('/profile'); }}>
              <ListItemIcon><PersonOutlineIcon fontSize="small" /></ListItemIcon>
              {t('auth.profile')}
            </MenuItem>
            <MenuItem onClick={() => { setUserMenu(null); void logout(); }}>
              <ListItemIcon><LogoutIcon fontSize="small" /></ListItemIcon>
              {t('auth.signOut')}
            </MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>

      <Box component="aside" sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}>
        {desktop ? (
          <Drawer variant="permanent" open sx={{ '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' } }}>
            {drawer}
          </Drawer>
        ) : (
          <Drawer variant="temporary" open={mobileOpen} onClose={() => setMobileOpen(false)} ModalProps={{ keepMounted: true }} sx={{ '& .MuiDrawer-paper': { width: DRAWER_WIDTH } }}>
            {drawer}
          </Drawer>
        )}
      </Box>

      <Box component="main" sx={{ flexGrow: 1, minWidth: 0, p: { xs: 1.5, md: 3 }, pt: { xs: 9, md: 10 } }}>
        <Suspense fallback={<Box sx={{ display: 'grid', placeItems: 'center', py: 10 }}><CircularProgress /></Box>}>
          <Outlet />
        </Suspense>
      </Box>
    </Box>
  );
}
```

### 6.33 `frontend/src/features/admin/SettingsPage.tsx`

```tsx
import { useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Grid2 as Grid, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import { AppError } from '@/api/errors';
import type { CompanyProfileDto, ShippingSettingsDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import Section from '@/components/common/Section';
import { useNotify } from '@/components/common/Notifier';
import FormFields, { type FieldDef } from '@/components/form/fields';
import { setLanguage, useLanguage, useTx } from '@/i18n';
import { useColorMode } from '@/app/AppProviders';
import { formatDateTime } from '@/utils/format';

function SettingsForm({ title, url, fields, data, loading, notice, canEdit }: { title: string; url: string; fields: FieldDef[]; data: object | undefined; loading: boolean; notice?: string; canEdit: boolean }) {
  const { t } = useTranslation();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const form = useForm<Record<string, unknown>>();
  useEffect(() => {
    if (data) form.reset(data as Record<string, unknown>);
  }, [data, form]);

  const submit = form.handleSubmit(async (values) => {
    try {
      await api.put(url, values);
      notify.success(t('common.saved'));
      await queryClient.invalidateQueries({ queryKey: [url] });
    } catch (error) {
      notify.error(error);
    }
  });

  return (
    <Section title={title} action={canEdit && <Button variant="contained" onClick={submit} disabled={form.formState.isSubmitting || loading}>{t('common.save')}</Button>}>
      {notice && <Alert severity="info" sx={{ mb: 2 }}>{notice}</Alert>}
      <fieldset disabled={!canEdit} style={{ border: 0, padding: 0, margin: 0 }}>
        <FormFields fields={fields.map((f) => ({ ...f, disabled: !canEdit }))} control={form.control} watch={form.watch} />
      </fieldset>
    </Section>
  );
}

/** Company profile (header of every PDF), shipping settings (labels, volumetric divisor, approvals) and UI preferences. */
export default function SettingsPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const colorMode = useColorMode();
  const { hasPermission } = useAuth();
  const canEdit = hasPermission(P.MasterData.Manage);
  const company = useQuery({ queryKey: ['/company-profile'], queryFn: () => api.get<CompanyProfileDto>('/company-profile') });
  const shipping = useQuery({ queryKey: ['/shipping-settings'], queryFn: () => api.get<ShippingSettingsDto>('/shipping-settings') });

  return (
    <>
      <PageHeader title={t('nav.settings')} />
      {!canEdit && <Alert severity="info" sx={{ mb: 2 }}>{tx('للعرض فقط: التعديل يحتاج صلاحية إدارة البيانات الأساسية.', 'Read only: editing needs MasterData.Manage.')}</Alert>}
      <SettingsForm title={tx('ملف الشركة (يظهر في رأس المستندات)', 'Company profile (document header)')} url="/company-profile" data={company.data} loading={company.isLoading}
        notice={company.error instanceof AppError && company.error.status === 404 ? tx('لم تُحفظ بيانات الشركة بعد: املأ الحقول ثم اضغط «حفظ».', 'No company profile yet: fill in the fields and press Save.') : undefined} canEdit={canEdit} fields={[
        { name: 'arabicName', label: tx('اسم الشركة بالعربية', 'Arabic name'), required: true },
        { name: 'englishName', label: tx('الاسم بالإنجليزية', 'English name') },
        { name: 'commercialRegistrationNumber', label: tx('السجل التجاري', 'Commercial registration'), span: 4 },
        { name: 'taxNumber', label: tx('الرقم الضريبي', 'Tax number'), span: 4 },
        { name: 'baseCurrencyId', label: tx('العملة الأساسية', 'Base currency'), type: 'lookup', lookup: 'currencies', required: true, span: 4 },
        { name: 'phone', label: tx('الهاتف', 'Phone'), span: 4 },
        { name: 'email', label: tx('البريد', 'E-mail'), type: 'email', span: 4 },
        { name: 'website', label: tx('الموقع', 'Website'), span: 4 },
        { name: 'address', label: tx('العنوان', 'Address'), span: 8 },
        { name: 'logoUrl', label: tx('رابط الشعار', 'Logo URL'), span: 4 }
      ]} />
      <SettingsForm title={tx('إعدادات الشحن والملصقات', 'Shipping and label settings')} url="/shipping-settings" data={shipping.data} loading={shipping.isLoading} canEdit={canEdit} fields={[
        { name: 'trackingNumberPrefix', label: tx('بادئة رقم التتبع', 'Tracking prefix'), required: true, span: 3 },
        { name: 'volumetricDivisor', label: tx('معامل الوزن الحجمي', 'Volumetric divisor'), type: 'integer', required: true, span: 3 },
        { name: 'weightRoundingStepKg', label: tx('تقريب الوزن (كغ)', 'Weight rounding (kg)'), type: 'number', required: true, span: 3 },
        { name: 'defaultPaymentTermsDays', label: tx('مدة السداد الافتراضية', 'Default payment terms'), type: 'integer', span: 3 },
        { name: 'labelWidthMm', label: tx('عرض الملصق مم', 'Label width mm'), type: 'integer', span: 3, helperText: tx('100 × 150 للطابعات الحرارية', '100 × 150 for thermal printers') },
        { name: 'labelHeightMm', label: tx('ارتفاع الملصق مم', 'Label height mm'), type: 'integer', span: 3 },
        { name: 'priceOverrideApprovalThresholdPercent', label: tx('حد اعتماد السعر اليدوي %', 'Manual price approval %'), type: 'number', span: 3 },
        { name: 'trackingUrlTemplate', label: tx('رابط التتبع (QR) مع {tracking}', 'Tracking URL (QR) with {tracking}'), span: 12 }
      ]} />
      {shipping.data?.updatedAtUtc && <Typography variant="caption" color="text.secondary">{tx('آخر تعديل', 'Last change')}: {formatDateTime(shipping.data.updatedAtUtc)}</Typography>}
      <Section title={tx('تفضيلات الواجهة (لهذا المتصفح)', 'Interface preferences (this browser)')}>
        <Grid container spacing={2}>
          <Grid><Button variant={language === 'ar' ? 'contained' : 'outlined'} onClick={() => setLanguage('ar')}>العربية</Button></Grid>
          <Grid><Button variant={language === 'en' ? 'contained' : 'outlined'} onClick={() => setLanguage('en')}>English</Button></Grid>
          <Grid><Button variant="outlined" onClick={colorMode.toggle}>{t('common.darkMode')}: {colorMode.mode === 'dark' ? t('common.yes') : t('common.no')}</Button></Grid>
        </Grid>
      </Section>
    </>
  );
}
```

### 6.34 `frontend/src/features/masterData/MasterDataPage.tsx`

```tsx
import { useMemo } from 'react';
import { Navigate, useNavigate, useParams } from 'react-router-dom';
import { Card, Tab, Tabs } from '@mui/material';
import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import ResourcePage, { type ResourceConfig } from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import type { FieldDef } from '@/components/form/fields';
import { useTx } from '@/i18n';

type Tx = (ar: string, en: string) => string;
type Row = GridValidRowModel & { id: number };

interface TabDef {
  key: string;
  label: string;
  view?: string[];
  config: ResourceConfig<Row>;
}

const names = (tx: Tx): FieldDef[] => [
  { name: 'code', label: tx('الرمز', 'Code'), required: true, maxLength: 20, span: 3 },
  { name: 'arabicName', label: tx('الاسم بالعربية', 'Arabic name'), required: true, maxLength: 200, span: 5 },
  { name: 'englishName', label: tx('الاسم بالإنجليزية', 'English name'), maxLength: 200, span: 4 }
];

const nameColumns = (tx: Tx) => [
  col.text<Row>('code', tx('الرمز', 'Code'), { ...S, flex: 0, width: 110 }),
  col.text<Row>('arabicName', tx('الاسم', 'Name'), S),
  col.text<Row>('englishName', tx('الاسم بالإنجليزية', 'English name'))
];

/** Master data screens (branches, warehouses, geography, services, currencies, people, vehicles...). */
function tabs(tx: Tx): TabDef[] {
  const active = col.active<Row>('isActive', tx('الحالة', 'Status'));
  const base = { canActivate: true, canDelete: true, searchPlaceholder: tx('الرمز أو الاسم', 'Code or name') };
  return [
    {
      key: 'branches', label: tx('الفروع', 'Branches'),
      config: { ...base, title: tx('الفروع', 'Branches'), url: '/branches', managePermission: P.Branches.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('cityName', tx('المدينة', 'City')), col.text('phone', tx('الهاتف', 'Phone')), col.bool('isHeadOffice', tx('رئيسي', 'Head office')), col.number('warehouseCount', tx('المستودعات', 'Warehouses')), active],
        filters: [{ name: 'cityId', label: tx('المدينة', 'City'), type: 'lookup', lookup: 'cities' }, { name: 'isActive', label: tx('فعّال', 'Active'), type: 'boolean' }],
        form: { fields: [...names(tx), { name: 'cityId', label: tx('المدينة', 'City'), type: 'lookup', lookup: 'cities', required: true }, { name: 'phone', label: tx('الهاتف', 'Phone'), span: 3 }, { name: 'email', label: tx('البريد', 'E-mail'), type: 'email', span: 3 }, { name: 'address', label: tx('العنوان', 'Address'), span: 12 }, { name: 'isHeadOffice', label: tx('المركز الرئيسي', 'Head office'), type: 'checkbox' }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'warehouses', label: tx('المستودعات', 'Warehouses'),
      config: { ...base, title: tx('المستودعات', 'Warehouses'), url: '/warehouses', managePermission: P.Branches.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('branchName', tx('الفرع', 'Branch')), col.number('capacityCubicMeters', tx('السعة م³', 'Capacity m³'), 2), active],
        filters: [{ name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches' }],
        form: { fields: [...names(tx), { name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches', required: true }, { name: 'capacityCubicMeters', label: tx('السعة م³', 'Capacity m³'), type: 'number', min: 0, span: 3 }, { name: 'phone', label: tx('الهاتف', 'Phone'), span: 3 }, { name: 'address', label: tx('العنوان', 'Address'), span: 12 }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'countries', label: tx('الدول', 'Countries'),
      config: { ...base, title: tx('الدول', 'Countries'), url: '/countries', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('phoneCode', tx('رمز الهاتف', 'Phone code')), col.number('cityCount', tx('المدن', 'Cities')), active],
        form: { fields: [...names(tx), { name: 'phoneCode', label: tx('رمز الهاتف', 'Phone code'), maxLength: 10 }] } }
    },
    {
      key: 'cities', label: tx('المدن', 'Cities'),
      config: { ...base, title: tx('المدن', 'Cities'), url: '/cities', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('countryName', tx('الدولة', 'Country')), col.number('zoneCount', tx('المناطق', 'Zones')), active],
        filters: [{ name: 'countryId', label: tx('الدولة', 'Country'), type: 'lookup', lookup: 'countries' }],
        form: { fields: [{ name: 'countryId', label: tx('الدولة', 'Country'), type: 'lookup', lookup: 'countries', required: true, span: 12 }, ...names(tx)] } }
    },
    {
      key: 'zones', label: tx('المناطق', 'Zones'),
      config: { ...base, title: tx('المناطق', 'Zones'), url: '/zones', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('cityName', tx('المدينة', 'City')), col.bool('isRemoteArea', tx('نائية', 'Remote')), active],
        filters: [{ name: 'cityId', label: tx('المدينة', 'City'), type: 'lookup', lookup: 'cities' }, { name: 'isRemoteArea', label: tx('نائية', 'Remote'), type: 'boolean' }],
        form: { fields: [{ name: 'cityId', label: tx('المدينة', 'City'), type: 'lookup', lookup: 'cities', required: true, span: 12 }, ...names(tx), { name: 'isRemoteArea', label: tx('منطقة نائية (رسوم إضافية)', 'Remote area (extra fee)'), type: 'checkbox' }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'services', label: tx('أنواع البضاعة', 'Goods types'),
      config: { ...base, title: tx('أنواع البضاعة (تُستخدم في قوائم الأسعار)', 'Goods types (used by the price lists)'), url: '/service-types', managePermission: P.MasterData.Manage, defaultSort: 'sortOrder', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.number('estimatedDeliveryDays', tx('أيام التوصيل', 'Delivery days')), col.number('sortOrder', tx('الترتيب', 'Order'), 0, S), active],
        form: { fields: [...names(tx), { name: 'estimatedDeliveryDays', label: tx('أيام التوصيل المتوقعة', 'Estimated days'), type: 'integer', required: true, min: 0, span: 3 }, { name: 'sortOrder', label: tx('الترتيب', 'Order'), type: 'integer', span: 3 }, { name: 'description', label: tx('الوصف', 'Description'), type: 'textarea' }] } }
    },
    {
      key: 'currencies', label: tx('العملات', 'Currencies'),
      config: { ...base, title: tx('العملات', 'Currencies'), url: '/currencies', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('symbol', tx('الرمز المختصر', 'Symbol')), col.number('decimalPlaces', tx('الكسور', 'Decimals')), col.bool('isBaseCurrency', tx('أساسية', 'Base')), active],
        form: { fields: [...names(tx), { name: 'symbol', label: tx('الرمز المختصر', 'Symbol'), required: true, span: 3 }, { name: 'decimalPlaces', label: tx('عدد الكسور', 'Decimals'), type: 'integer', min: 0, max: 4, span: 3 }, { name: 'isBaseCurrency', label: tx('العملة الأساسية', 'Base currency'), type: 'checkbox' }] } }
    },
    {
      key: 'rates', label: tx('أسعار الصرف', 'Exchange rates'),
      config: { ...base, title: tx('أسعار الصرف', 'Exchange rates'), url: '/exchange-rates', managePermission: P.MasterData.Manage, defaultSort: 'effectiveFromUtc',
        columns: [col.text('fromCurrencyCode', tx('من', 'From')), col.text('toCurrencyCode', tx('إلى', 'To')), col.number('rate', tx('السعر', 'Rate'), 6, S), col.date('effectiveFromUtc', tx('ساري من', 'Effective from'), S), col.text('notes', tx('ملاحظات', 'Notes')), active],
        form: { fields: [{ name: 'fromCurrencyId', label: tx('من عملة', 'From currency'), type: 'lookup', lookup: 'currencies', required: true }, { name: 'toCurrencyId', label: tx('إلى عملة', 'To currency'), type: 'lookup', lookup: 'currencies', required: true }, { name: 'rate', label: tx('السعر', 'Rate'), type: 'number', required: true, min: 0 }, { name: 'effectiveFromUtc', label: tx('ساري من', 'Effective from'), type: 'date', required: true }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'payment-methods', label: tx('طرق الدفع', 'Payment methods'),
      config: { ...base, title: tx('طرق الدفع', 'Payment methods'), url: '/payment-methods', managePermission: P.MasterData.Manage, defaultSort: 'sortOrder', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.enumText('kind', tx('النوع', 'Kind'), 'PaymentMethodKind'), col.bool('requiresReference', tx('يتطلب مرجعاً', 'Needs reference')), active],
        form: { fields: [...names(tx), { name: 'kind', label: tx('النوع', 'Kind'), type: 'enum', enumName: 'PaymentMethodKind', required: true, span: 4 }, { name: 'sortOrder', label: tx('الترتيب', 'Order'), type: 'integer', span: 2 }, { name: 'requiresReference', label: tx('يتطلب رقماً مرجعياً', 'Requires a reference'), type: 'checkbox' }] } }
    },
    {
      key: 'tax-rates', label: tx('الضرائب', 'Tax rates'),
      config: { ...base, title: tx('الضرائب', 'Tax rates'), url: '/tax-rates', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.number('percentage', '%', 2, S), col.bool('isDefault', tx('افتراضية', 'Default')), active],
        form: { fields: [...names(tx), { name: 'percentage', label: tx('النسبة %', 'Percentage %'), type: 'number', required: true, min: 0, max: 100 }, { name: 'isDefault', label: tx('افتراضية', 'Default'), type: 'checkbox' }] } }
    },
    {
      key: 'expense-categories', label: tx('بنود المصروفات', 'Expense categories'),
      config: { ...base, title: tx('بنود المصروفات', 'Expense categories'), url: '/expense-categories', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('description', tx('الوصف', 'Description')), active],
        form: { fields: [...names(tx), { name: 'description', label: tx('الوصف', 'Description'), type: 'textarea' }] } }
    },
    {
      key: 'employees', label: tx('الموظفون', 'Employees'), view: [P.Employees.View],
      config: { ...base, title: tx('الموظفون', 'Employees'), url: '/employees', managePermission: P.Employees.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx).slice(0, 2), col.text('branchName', tx('الفرع', 'Branch')), col.text('jobTitle', tx('الوظيفة', 'Job title')), col.text('phone', tx('الهاتف', 'Phone')), col.text('userName', tx('المستخدم', 'User')), col.bool('isDriver', tx('سائق', 'Driver')), active],
        filters: [{ name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches' }],
        form: { fields: [...names(tx), { name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches', required: true }, { name: 'jobTitle', label: tx('الوظيفة', 'Job title') }, { name: 'phone', label: tx('الهاتف', 'Phone'), span: 4 }, { name: 'email', label: tx('البريد', 'E-mail'), type: 'email', span: 4 }, { name: 'nationalId', label: tx('الرقم الوطني', 'National id'), span: 4 }, { name: 'hireDateUtc', label: tx('تاريخ التعيين', 'Hire date'), type: 'date' }, { name: 'userId', label: tx('حساب المستخدم', 'User account'), type: 'lookup', lookup: 'users' }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'drivers', label: tx('السائقون', 'Drivers'), view: [P.Employees.View],
      config: { ...base, title: tx('السائقون', 'Drivers'), url: '/drivers', managePermission: P.Employees.Manage, defaultSort: 'employeeName', defaultDirection: 'asc',
        columns: [col.text('employeeCode', tx('الرمز', 'Code'), { flex: 0, width: 100 }), col.text('employeeName', tx('الاسم', 'Name'), S), col.text('branchName', tx('الفرع', 'Branch')), col.text('licenseNumber', tx('الرخصة', 'License')), col.date('licenseExpiryDateUtc', tx('انتهاء الرخصة', 'License expiry'), S), col.text('defaultVehiclePlate', tx('المركبة', 'Vehicle')), col.text('userName', tx('المستخدم', 'User')), active],
        filters: [{ name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches' }, { name: 'licenseExpiringSoon', label: tx('رخصة تنتهي قريباً', 'License expiring'), type: 'boolean' }],
        form: { fields: [{ name: 'employeeId', label: tx('الموظف', 'Employee'), type: 'lookup', lookup: 'employees', required: true, createOnly: true }, { name: 'userId', label: tx('حساب المستخدم (لشاشة السائق)', 'User account (driver screen)'), type: 'lookup', lookup: 'users' }, { name: 'licenseNumber', label: tx('رقم الرخصة', 'License number'), required: true, span: 4 }, { name: 'licenseCategory', label: tx('الفئة', 'Category'), span: 4 }, { name: 'licenseExpiryDateUtc', label: tx('انتهاء الرخصة', 'Expiry'), type: 'date', required: true, span: 4 }, { name: 'defaultVehicleId', label: tx('المركبة الافتراضية', 'Default vehicle'), type: 'lookup', lookup: 'vehicles' }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'vehicles', label: tx('المركبات', 'Vehicles'), view: [P.Vehicles.View],
      config: { ...base, title: tx('المركبات', 'Vehicles'), url: '/vehicles', managePermission: P.Vehicles.Manage, defaultSort: 'plateNumber', defaultDirection: 'asc',
        columns: [col.text('plateNumber', tx('اللوحة', 'Plate'), S), col.text('model', tx('الطراز', 'Model'), S), col.enumText('vehicleType', tx('النوع', 'Type'), 'VehicleType'), col.number('capacityKg', tx('الحمولة كغ', 'Capacity kg'), 0, S), col.text('branchName', tx('الفرع', 'Branch')), active],
        filters: [{ name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches' }, { name: 'vehicleType', label: tx('النوع', 'Type'), type: 'enum', enumName: 'VehicleType' }],
        form: { fields: [{ name: 'plateNumber', label: tx('رقم اللوحة', 'Plate number'), required: true, span: 4 }, { name: 'model', label: tx('الطراز', 'Model'), required: true, span: 4 }, { name: 'vehicleType', label: tx('النوع', 'Type'), type: 'enum', enumName: 'VehicleType', required: true, span: 4 }, { name: 'capacityKg', label: tx('الحمولة كغ', 'Capacity kg'), type: 'number', min: 0 }, { name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches', required: true }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'suppliers', label: tx('الموردون', 'Suppliers'), view: [P.Suppliers.View],
      config: { ...base, title: tx('الموردون', 'Suppliers'), url: '/suppliers', managePermission: P.Suppliers.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx).slice(0, 2), col.text('contactPerson', tx('جهة الاتصال', 'Contact')), col.text('phone', tx('الهاتف', 'Phone')), col.text('taxNumber', tx('الرقم الضريبي', 'Tax no.')), col.money('openingBalance', tx('الرصيد الافتتاحي', 'Opening balance')), active],
        form: { fields: [...names(tx), { name: 'contactPerson', label: tx('جهة الاتصال', 'Contact person') }, { name: 'phone', label: tx('الهاتف', 'Phone'), required: true, span: 3 }, { name: 'alternatePhone', label: tx('هاتف بديل', 'Alt. phone'), span: 3 }, { name: 'email', label: tx('البريد', 'E-mail'), type: 'email' }, { name: 'taxNumber', label: tx('الرقم الضريبي', 'Tax number') }, { name: 'openingBalance', label: tx('الرصيد الافتتاحي', 'Opening balance'), type: 'money' }, { name: 'address', label: tx('العنوان', 'Address'), span: 12 }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'bank-accounts', label: tx('الحسابات البنكية', 'Bank accounts'), view: [P.Cashboxes.View],
      config: { ...base, canActivate: false, title: tx('الحسابات البنكية', 'Bank accounts'), url: '/bank-accounts', managePermission: P.Cashboxes.Manage,
        columns: [col.text('code', tx('الرمز', 'Code'), { flex: 0, width: 100 }), col.text('arabicName', tx('الاسم', 'Name')), col.text('bankName', tx('البنك', 'Bank')), col.text('accountNumber', tx('رقم الحساب', 'Account no.')), col.text('currencyCode', tx('العملة', 'Currency'), { flex: 0, width: 80 }), col.text('accountName', tx('الحساب المحاسبي', 'GL account')), active],
        form: { fields: [{ name: 'code', label: tx('الرمز', 'Code'), required: true, span: 3 }, { name: 'arabicName', label: tx('الاسم', 'Name'), required: true, span: 5 }, { name: 'bankName', label: tx('البنك', 'Bank'), required: true, span: 4 }, { name: 'accountNumber', label: tx('رقم الحساب', 'Account number'), required: true }, { name: 'iban', label: 'IBAN' }, { name: 'currencyId', label: tx('العملة', 'Currency'), type: 'lookup', lookup: 'currencies', required: true }, { name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches' }, { name: 'accountId', label: tx('الحساب المحاسبي', 'GL account'), type: 'lookup', lookup: 'accounts' }, { name: 'isActive', label: tx('فعّال', 'Active'), type: 'checkbox' }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    }
  ];
}

/** /master-data/:tab — one screen with a tab per master data list (tabs the user cannot view are hidden). */
export default function MasterDataPage() {
  const { tab } = useParams();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const tx = useTx();
  const { hasAny } = useAuth();
  const visible = useMemo(() => tabs(tx).filter((item) => !item.view || hasAny(...item.view)), [tx, hasAny]);
  const current = visible.find((item) => item.key === tab);

  if (!current) {
    return <Navigate to={`/master-data/${visible[0].key}`} replace />;
  }

  return (
    <>
      <PageHeader title={t('nav.masterData')} />
      <Card sx={{ mb: 2 }}>
        <Tabs value={current.key} onChange={(_, value: string) => navigate(`/master-data/${value}`)} variant="scrollable" scrollButtons="auto">
          {visible.map((item) => <Tab key={item.key} value={item.key} label={item.label} />)}
        </Tabs>
      </Card>
      {/* key: a new list state per tab */}
      <ResourcePage key={current.key} config={{ ...current.config, hideHeader: false, height: 'calc(100vh - 340px)' }} />
    </>
  );
}
```

### 6.35 `frontend/src/features/operations/DriverPage.tsx`

```tsx
import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, Card, CardActions, CardContent, Chip, CircularProgress, Grid2 as Grid, Stack, Tab, Tabs, Typography } from '@mui/material';
import PhoneOutlinedIcon from '@mui/icons-material/PhoneOutlined';
import PlaceOutlinedIcon from '@mui/icons-material/PlaceOutlined';
import TaskAltIcon from '@mui/icons-material/TaskAlt';
import ReportProblemOutlinedIcon from '@mui/icons-material/ReportProblemOutlined';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import { shipmentsApi } from '@/api/endpoints/shipments';
import type { DriverManifestDto, DriverManifestItemDto } from '@/api/generated';
import type { PagedResult } from '@/api/types';
import PageHeader from '@/components/common/PageHeader';
import StatusChip from '@/components/common/StatusChip';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import DeliverDialog from '@/features/shipments/DeliverDialog';
import { useTx } from '@/i18n';
import { formatDate, formatMoney } from '@/utils/format';

/**
 * Driver screen (phone first): the dispatched manifests of the signed-in driver (the API returns only his own when
 * he has the Driver role), one card per stop with call / map buttons, deliver with proof (signature + photo) or fail.
 */
export default function DriverPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [manifestIndex, setManifestIndex] = useState(0);
  const [delivering, setDelivering] = useState<DriverManifestItemDto | null>(null);
  const [failing, setFailing] = useState<DriverManifestItemDto | null>(null);

  const manifests = useQuery({
    queryKey: ['/driver-manifests', 'driver-screen'],
    queryFn: () => api.get<PagedResult<DriverManifestDto>>('/driver-manifests', { status: 'Dispatched', pageSize: 20 }),
    refetchInterval: 60_000
  });
  const current = manifests.data?.items[manifestIndex];
  const details = useQuery({
    queryKey: ['/driver-manifests', current?.id],
    queryFn: () => api.get<DriverManifestDto>(`/driver-manifests/${current!.id}`),
    enabled: !!current
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['/driver-manifests'] });
  const items = details.data?.items ?? [];
  const pending = items.filter((item) => item.result === 'Pending');
  const done = items.filter((item) => item.result !== 'Pending');
  const collected = items.filter((item) => item.result === 'Delivered' && item.isCOD).reduce((sum, item) => sum + item.codAmount, 0);

  return (
    <>
      <PageHeader title={t('nav.driver')} subtitle={current ? `${current.manifestNumber} · ${formatDate(current.manifestDateUtc)}` : undefined} />
      {manifests.isLoading && <CircularProgress />}
      {manifests.data?.items.length === 0 && <Alert severity="info">{tx('لا توجد كشوف توزيع خارجة باسمك حالياً.', 'You have no dispatched manifests right now.')}</Alert>}
      {(manifests.data?.items.length ?? 0) > 1 && (
        <Tabs value={manifestIndex} onChange={(_, v: number) => setManifestIndex(v)} variant="scrollable" sx={{ mb: 2 }}>
          {manifests.data!.items.map((m) => <Tab key={m.id} label={m.manifestNumber} />)}
        </Tabs>
      )}
      {details.data && (
        <>
          <Grid container spacing={1} sx={{ mb: 2 }}>
            <Grid size={4}><Card><CardContent sx={{ p: 1.5 }}><Typography variant="caption">{tx('متبقية', 'Pending')}</Typography><Typography variant="h5">{pending.length}</Typography></CardContent></Card></Grid>
            <Grid size={4}><Card><CardContent sx={{ p: 1.5 }}><Typography variant="caption">{tx('منجزة', 'Done')}</Typography><Typography variant="h5">{done.length}</Typography></CardContent></Card></Grid>
            <Grid size={4}><Card><CardContent sx={{ p: 1.5 }}><Typography variant="caption">{tx('التحصيل', 'Collected')}</Typography><Typography variant="h6" sx={{ fontVariantNumeric: 'tabular-nums' }}>{formatMoney(collected)}</Typography></CardContent></Card></Grid>
          </Grid>
          <Stack spacing={1.5}>
            {[...pending, ...done].map((item) => (
              <Card key={item.itemId} sx={{ opacity: item.result === 'Pending' ? 1 : 0.65 }}>
                <CardContent sx={{ pb: 1 }}>
                  <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', gap: 1 }}>
                    <Typography variant="subtitle1" fontWeight={700}>{item.sequence}. {item.recipientName}</Typography>
                    {item.result === 'Pending' ? <StatusChip enumName="ShipmentStatus" value={item.shipmentStatus} /> : <StatusChip enumName="DeliveryResult" value={item.result} />}
                  </Stack>
                  <Typography variant="body2" color="text.secondary" dir="ltr" sx={{ textAlign: 'start' }}>{item.trackingNumber}</Typography>
                  <Typography variant="body2" sx={{ mt: 0.5 }}>{[item.destinationCityName, item.destinationZoneName, item.recipientAddressLine].filter(Boolean).join(' - ')}</Typography>
                  <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                    <Chip label={`${item.packageCount} ${tx('طرد', 'pcs')}`} />
                    {item.isCOD && <Chip color="warning" label={`${tx('حصّل', 'Collect')} ${formatMoney(item.codAmount)}`} />}
                    {item.chargesOnRecipient && item.chargesToCollect > 0 && <Chip color="warning" variant="outlined" label={`${tx('أجور الشحن', 'Charges')} ${formatMoney(item.chargesToCollect)}`} />}
                  </Stack>
                </CardContent>
                <CardActions sx={{ flexWrap: 'wrap', gap: 1 }}>
                  <Button size="small" startIcon={<PhoneOutlinedIcon />} href={`tel:${item.recipientPhone}`}>{tx('اتصال', 'Call')}</Button>
                  <Button size="small" startIcon={<PlaceOutlinedIcon />} href={`https://maps.google.com/?q=${encodeURIComponent([item.recipientAddressLine, item.destinationCityName].filter(Boolean).join(' '))}`} target="_blank" rel="noreferrer">{tx('الخريطة', 'Map')}</Button>
                  <Box sx={{ flex: 1 }} />
                  {item.result === 'Pending' && item.shipmentStatus === 'OutForDelivery' && (
                    <>
                      <Button size="small" color="error" startIcon={<ReportProblemOutlinedIcon />} onClick={() => setFailing(item)}>{tx('فشل', 'Failed')}</Button>
                      <Button size="small" variant="contained" color="success" startIcon={<TaskAltIcon />} onClick={() => setDelivering(item)}>{tx('تسليم', 'Deliver')}</Button>
                    </>
                  )}
                </CardActions>
              </Card>
            ))}
          </Stack>
        </>
      )}
      {delivering && (
        <DeliverDialog open shipmentId={delivering.shipmentId} trackingNumber={delivering.trackingNumber} defaultRecipient={delivering.recipientName} codAmount={delivering.isCOD ? delivering.codAmount : 0} chargesAmount={delivering.chargesToCollect}
          onClose={() => setDelivering(null)} onDelivered={() => { setDelivering(null); notify.success(tx('تم تأكيد التسليم', 'Delivery confirmed')); void refresh(); }} />
      )}
      <ReasonDialog open={!!failing} title={tx('سبب فشل التسليم', 'Failure reason')} onClose={() => setFailing(null)}
        onSubmit={async (reason) => {
          try {
            await shipmentsApi.deliveryFailed(failing!.shipmentId, { reason });
            setFailing(null);
            notify.success(t('common.done'));
            await refresh();
          } catch (error) {
            notify.error(error);
          }
        }} />
    </>
  );
}
```

### 6.36 `frontend/src/features/pricing/PricingPlanDetailsPage.tsx`

```tsx
import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useFieldArray, useForm, Controller } from 'react-hook-form';
import {
  Alert, Box, Button, Card, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Divider, Grid2 as Grid, IconButton, MenuItem,
  Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import ToggleOnOutlinedIcon from '@mui/icons-material/ToggleOnOutlined';
import ToggleOffOutlinedIcon from '@mui/icons-material/ToggleOffOutlined';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import { AppError, toAppError } from '@/api/errors';
import type { PriceConditionType, PricingPlanDto, PricingRuleDto, SavePricingRuleRequest } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import { ActiveChip } from '@/components/common/StatusChip';
import { useNotify } from '@/components/common/Notifier';
import LookupSelect from '@/components/form/LookupSelect';
import type { LookupName } from '@/components/form/lookups';
import { enumLabel, enumOptions } from '@/i18n/enums';
import { useLanguage, useTx } from '@/i18n';
import { formatMoney, formatNumber } from '@/utils/format';

const referenceLookup: Partial<Record<PriceConditionType, LookupName>> = {
  OriginZone: 'zones', DestinationZone: 'zones', OriginCity: 'cities', DestinationCity: 'cities', ServiceType: 'serviceTypes'
};

const emptyRule: SavePricingRuleRequest = {
  arabicName: '', englishName: null, priority: 100, validFromUtc: null, validToUtc: null, conditions: [], basePrice: 0, includedWeightKg: 0,
  pricePerCubicMeter: 0, appliesToBasis: null,
  weightTiers: [], includedPackageCount: 1, perPackageFee: 0, codFeeFixed: 0, codFeePercentage: 0, codFeeMinimum: 0, insuranceFeePercentage: 0,
  insuranceFeeMinimum: 0, pickupFee: 0, packagingFee: 0, remoteAreaFee: 0, returnFee: 0, discountPercentage: 0, taxRateId: null, notes: null
};

function toRequest(rule: PricingRuleDto): SavePricingRuleRequest {
  return {
    ...emptyRule,
    ...Object.fromEntries(Object.keys(emptyRule).map((key) => [key, (rule as unknown as Record<string, unknown>)[key]])),
    conditions: rule.conditions.map((c) => ({ conditionType: c.conditionType, referenceId: c.referenceId, minValue: c.minValue, maxValue: c.maxValue })),
    weightTiers: rule.weightTiers.map((w) => ({ fromWeightKg: w.fromWeightKg, toWeightKg: w.toWeightKg, pricePerKg: w.pricePerKg }))
  } as SavePricingRuleRequest;
}

/** Rule editor: matching conditions (most specific + priority wins on the API), base price, weight tiers and fees. */
function RuleDialog({ planId, rule, onClose, onSaved }: { planId: number; rule: PricingRuleDto | null | undefined; onClose(): void; onSaved(): void }) {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const [error, setError] = useState<AppError | null>(null);
  const form = useForm<SavePricingRuleRequest>({ defaultValues: rule ? toRequest(rule) : emptyRule });
  const conditions = useFieldArray({ control: form.control, name: 'conditions' });
  const tiers = useFieldArray({ control: form.control, name: 'weightTiers' });
  useEffect(() => form.reset(rule ? toRequest(rule) : emptyRule), [rule, form]);

  const num = (name: Parameters<typeof form.register>[0], label: string, span = 3) => (
    <Grid size={{ xs: 6, md: span }}>
      <TextField type="number" label={label} inputProps={{ step: 'any', dir: 'ltr' }} {...form.register(name, { setValueAs: (v) => (v === '' || v === null ? null : Number(v)) })} error={!!error?.fieldErrors[name]} helperText={error?.fieldErrors[name]?.[0]} />
    </Grid>
  );

  const submit = form.handleSubmit(async (values) => {
    setError(null);
    try {
      if (rule) await api.put(`/pricing-plans/${planId}/rules/${rule.id}`, values);
      else await api.post(`/pricing-plans/${planId}/rules`, values);
      onSaved();
    } catch (caught) {
      setError(await toAppError(caught));
    }
  });

  return (
    <Dialog open onClose={onClose} maxWidth="lg" fullWidth>
      <form onSubmit={submit} noValidate>
        <DialogTitle>{rule ? tx('تعديل قاعدة', 'Edit rule') : tx('قاعدة جديدة', 'New rule')}</DialogTitle>
        <DialogContent dividers>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error.message}{Object.values(error.fieldErrors).flat().map((m) => ` · ${m}`)}</Alert>}
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, md: 4 }}><TextField label={tx('الاسم بالعربية', 'Arabic name')} required {...form.register('arabicName')} /></Grid>
            <Grid size={{ xs: 12, md: 4 }}><TextField label={tx('الاسم بالإنجليزية', 'English name')} {...form.register('englishName')} /></Grid>
            {num('priority', tx('الأولوية (الأصغر أولاً)', 'Priority (lower first)'), 2)}
            <Grid size={{ xs: 12, md: 2 }}>
              <Controller control={form.control} name="taxRateId" render={({ field }) => <LookupSelect source="taxRates" label={tx('الضريبة', 'Tax')} value={field.value ?? null} onChange={(v) => field.onChange(v)} />} />
            </Grid>
            <Grid size={{ xs: 6, md: 3 }}><TextField type="date" label={tx('ساري من', 'Valid from')} InputLabelProps={{ shrink: true }} {...form.register('validFromUtc', { setValueAs: (v) => v || null })} /></Grid>
            <Grid size={{ xs: 6, md: 3 }}><TextField type="date" label={tx('ساري إلى', 'Valid to')} InputLabelProps={{ shrink: true }} {...form.register('validToUtc', { setValueAs: (v) => v || null })} /></Grid>
          </Grid>

          <Divider sx={{ my: 2 }}><Chip label={tx('شروط المطابقة', 'Conditions')} /></Divider>
          {conditions.fields.map((item, index) => {
            const type = form.watch(`conditions.${index}.conditionType`) as PriceConditionType;
            const lookup = referenceLookup[type];
            return (
              <Grid container spacing={1} key={item.id} sx={{ mb: 1 }}>
                <Grid size={{ xs: 12, md: 3 }}>
                  <TextField select label={tx('النوع', 'Type')} value={type ?? ''} onChange={(e) => form.setValue(`conditions.${index}`, { conditionType: e.target.value as PriceConditionType, referenceId: null, minValue: null, maxValue: null })}>
                    {enumOptions('PriceConditionType', language).map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
                  </TextField>
                </Grid>
                {lookup ? (
                  <Grid size={{ xs: 12, md: 6 }}>
                    <Controller control={form.control} name={`conditions.${index}.referenceId`} render={({ field }) => <LookupSelect source={lookup} label={tx('القيمة', 'Value')} value={field.value ?? null} onChange={(v) => field.onChange(v)} />} />
                  </Grid>
                ) : (
                  <>
                    {num(`conditions.${index}.minValue`, tx('من', 'Min'))}
                    {num(`conditions.${index}.maxValue`, tx('إلى', 'Max'))}
                  </>
                )}
                <Grid size={1}><IconButton color="error" onClick={() => conditions.remove(index)}><DeleteOutlineIcon /></IconButton></Grid>
              </Grid>
            );
          })}
          <Button size="small" startIcon={<AddIcon />} onClick={() => conditions.append({ conditionType: 'DestinationZone', referenceId: null, minValue: null, maxValue: null })}>{tx('شرط', 'Condition')}</Button>

          <Divider sx={{ my: 2 }}><Chip label={tx('طريقة التسعير', 'Pricing basis')} /></Divider>
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, md: 6 }}>
              <Controller
                control={form.control}
                name="appliesToBasis"
                render={({ field }) => (
                  <TextField select label={tx('تُطبَّق على الشحنات المسعّرة', 'Applies to shipments priced')} value={field.value ?? ''} onChange={(e) => field.onChange(e.target.value === '' ? null : e.target.value)}>
                    <MenuItem value="">{tx('بالوزن وبالحجم', 'By weight and by volume')}</MenuItem>
                    <MenuItem value="Weight">{tx('بالوزن فقط (سعر الكيلو من الشرائح)', 'By weight only (per kg tiers)')}</MenuItem>
                    <MenuItem value="Volume">{tx('بالحجم فقط (سعر المتر المكعب)', 'By volume only (per m³)')}</MenuItem>
                  </TextField>
                )}
              />
            </Grid>
            {num('pricePerCubicMeter', tx('سعر المتر المكعب', 'Price per m³'))}
          </Grid>

          <Divider sx={{ my: 2 }}><Chip label={tx('السعر والوزن', 'Price and weight')} /></Divider>
          <Grid container spacing={2}>
            {num('basePrice', tx('السعر الأساسي', 'Base price'))}
            {num('includedWeightKg', tx('الوزن المشمول كغ', 'Included weight kg'))}
            {num('includedPackageCount', tx('الطرود المشمولة', 'Included packages'))}
            {num('perPackageFee', tx('رسم الطرد الإضافي', 'Extra package fee'))}
          </Grid>
          <Typography variant="subtitle2" sx={{ mt: 2 }}>{tx('شرائح الوزن الإضافي', 'Extra weight tiers')}</Typography>
          {tiers.fields.map((item, index) => (
            <Grid container spacing={1} key={item.id} sx={{ mt: 0.5 }}>
              {num(`weightTiers.${index}.fromWeightKg`, tx('من كغ', 'From kg'))}
              {num(`weightTiers.${index}.toWeightKg`, tx('إلى كغ (فارغ = بلا حد)', 'To kg (empty = no limit)'))}
              {num(`weightTiers.${index}.pricePerKg`, tx('سعر الكغ', 'Price per kg'))}
              <Grid size={1}><IconButton color="error" onClick={() => tiers.remove(index)}><DeleteOutlineIcon /></IconButton></Grid>
            </Grid>
          ))}
          <Button size="small" startIcon={<AddIcon />} onClick={() => tiers.append({ fromWeightKg: 0, toWeightKg: null, pricePerKg: 0 })}>{tx('شريحة', 'Tier')}</Button>

          <Divider sx={{ my: 2 }}><Chip label={tx('الرسوم', 'Fees')} /></Divider>
          <Grid container spacing={2}>
            {num('codFeeFixed', tx('عمولة التحصيل الثابتة', 'COD fixed fee'))}
            {num('codFeePercentage', tx('عمولة التحصيل %', 'COD fee %'))}
            {num('codFeeMinimum', tx('حد أدنى للعمولة', 'COD minimum'))}
            {num('insuranceFeePercentage', tx('التأمين %', 'Insurance %'))}
            {num('insuranceFeeMinimum', tx('حد أدنى للتأمين', 'Insurance minimum'))}
            {num('pickupFee', tx('رسم الاستلام', 'Pickup fee'))}
            {num('packagingFee', tx('رسم التغليف', 'Packaging fee'))}
            {num('remoteAreaFee', tx('رسم المنطقة النائية', 'Remote area fee'))}
            {num('returnFee', tx('رسم الإرجاع', 'Return fee'))}
            {num('discountPercentage', tx('خصم %', 'Discount %'))}
            <Grid size={12}><TextField label={t('common.notes')} multiline {...form.register('notes')} /></Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" variant="contained" disabled={form.formState.isSubmitting}>{t('common.save')}</Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

export default function PricingPlanDetailsPage() {
  const planId = Number(useParams().id);
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission(P.Pricing.Manage);
  const [editing, setEditing] = useState<PricingRuleDto | null | undefined>(undefined);

  const plan = useQuery({ queryKey: ['/pricing-plans', planId], queryFn: () => api.get<PricingPlanDto>(`/pricing-plans/${planId}`) });
  const rules = useQuery({ queryKey: ['/pricing-plans', planId, 'rules'], queryFn: () => api.get<PricingRuleDto[] | { items: PricingRuleDto[] }>(`/pricing-plans/${planId}/rules`) });
  const list = Array.isArray(rules.data) ? rules.data : rules.data?.items ?? [];

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['/pricing-plans', planId] });
  const run = async (action: () => Promise<unknown>) => {
    try { await action(); notify.success(t('common.done')); await refresh(); } catch (e) { notify.error(e); }
  };

  const currency = plan.data?.currencyCode;
  return (
    <>
      <PageHeader back="/pricing" title={plan.data ? `${plan.data.code} - ${plan.data.arabicName}` : '...'} subtitle={plan.data?.contractCustomerName ?? (plan.data?.isDefault ? tx('الخطة الافتراضية', 'Default plan') : undefined)}
        actions={canManage && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing(null)}>{tx('قاعدة جديدة', 'New rule')}</Button>} />
      <Card>
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>{tx('الأولوية', 'Priority')}</TableCell><TableCell>{tx('القاعدة', 'Rule')}</TableCell><TableCell>{tx('الشروط', 'Conditions')}</TableCell>
                <TableCell>{tx('الأساسي', 'Base')}</TableCell><TableCell>{tx('الوزن المشمول', 'Incl. kg')}</TableCell><TableCell>{tx('الشرائح', 'Tiers')}</TableCell><TableCell>{tx('سعر م³', 'Per m³')}</TableCell>
                <TableCell>{tx('عمولة التحصيل', 'COD fee')}</TableCell><TableCell>{t('common.status')}</TableCell><TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {list.map((rule) => (
                <TableRow key={rule.id} hover>
                  <TableCell>{rule.priority}</TableCell>
                  <TableCell>{rule.arabicName}</TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: 'wrap' }}>
                      {rule.appliesToBasis && <Chip color="primary" label={enumLabel('PricingBasis', rule.appliesToBasis, language)} />}
                      {rule.conditions.length === 0 && !rule.appliesToBasis && <Chip label={tx('الكل', 'Any')} />}
                      {rule.conditions.map((c) => <Chip key={c.id} variant="outlined" label={`${enumLabel('PriceConditionType', c.conditionType, language)}: ${c.referenceName ?? `${c.minValue ?? ''}–${c.maxValue ?? ''}`}`} />)}
                    </Stack>
                  </TableCell>
                  <TableCell>{formatMoney(rule.basePrice, currency)}</TableCell>
                  <TableCell>{formatNumber(rule.includedWeightKg, 2)}</TableCell>
                  <TableCell>{rule.weightTiers.map((w) => `${w.fromWeightKg}-${w.toWeightKg ?? '∞'}: ${w.pricePerKg}`).join(' · ') || '-'}</TableCell>
                  <TableCell>{rule.pricePerCubicMeter > 0 ? formatMoney(rule.pricePerCubicMeter, currency) : '-'}</TableCell>
                  <TableCell>{`${formatNumber(rule.codFeeFixed, 2)} + ${formatNumber(rule.codFeePercentage, 2)}%`}</TableCell>
                  <TableCell><ActiveChip active={rule.isActive} /></TableCell>
                  <TableCell align="left" sx={{ whiteSpace: 'nowrap' }}>
                    {canManage && (
                      <>
                        <IconButton size="small" onClick={() => setEditing(rule)}><EditOutlinedIcon fontSize="small" /></IconButton>
                        <IconButton size="small" onClick={() => void run(() => api.patch(`/pricing-plans/${planId}/rules/${rule.id}/${rule.isActive ? 'deactivate' : 'activate'}`))}>
                          {rule.isActive ? <ToggleOnOutlinedIcon fontSize="small" color="success" /> : <ToggleOffOutlinedIcon fontSize="small" />}
                        </IconButton>
                        <IconButton size="small" color="error" onClick={() => void run(() => api.delete(`/pricing-plans/${planId}/rules/${rule.id}`))}><DeleteOutlineIcon fontSize="small" /></IconButton>
                      </>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {list.length === 0 && !rules.isLoading && <TableRow><TableCell colSpan={9}>{t('common.noData')}</TableCell></TableRow>}
            </TableBody>
          </Table>
        </Box>
      </Card>
      {editing !== undefined && <RuleDialog planId={planId} rule={editing} onClose={() => setEditing(undefined)} onSaved={() => { setEditing(undefined); notify.success(t('common.saved')); void refresh(); }} />}
    </>
  );
}
```

### 6.37 `frontend/src/features/reports/OperationalReportsPage.tsx`

```tsx
import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Box, Card, CardContent, CardHeader, Divider, Grid2 as Grid, LinearProgress, MenuItem, Tab, Table, TableBody, TableCell, TableHead, TableRow, Tabs, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { OperationalGroupRowDto, OperationalReportDto } from '@/api/generated';
import PageHeader from '@/components/common/PageHeader';
import StatCard from '@/components/common/StatCard';
import ErrorAlert from '@/components/common/ErrorAlert';
import ExportMenu from '@/components/common/ExportMenu';
import { CategoryBarChart, TrendChart } from '@/components/charts/Charts';
import { enumLabel, enumOptions } from '@/i18n/enums';
import { useLanguage, useTx } from '@/i18n';
import { businessDaysAgo, businessToday, formatMoney, formatNumber, formatPercent } from '@/utils/format';
import ReportFilters, { type ReportFilterValues } from './ReportFilters';

function GroupTable({ rows }: { rows: OperationalGroupRowDto[] }) {
  const tx = useTx();
  const language = useLanguage();
  return (
    <Box sx={{ overflowX: 'auto' }}>
      <Table size="small">
        <TableHead><TableRow><TableCell>{tx('الاسم', 'Name')}</TableCell><TableCell>{tx('الشحنات', 'Total')}</TableCell><TableCell>{tx('سُلّمت', 'Delivered')}</TableCell><TableCell>{tx('فشلت', 'Failed')}</TableCell><TableCell>{tx('قيد التنفيذ', 'In progress')}</TableCell><TableCell>{tx('ملغاة', 'Cancelled')}</TableCell><TableCell>{tx('نسبة التسليم', 'Delivered %')}</TableCell><TableCell>{tx('الأجور', 'Charges')}</TableCell><TableCell>COD</TableCell></TableRow></TableHead>
        <TableBody>
          {rows.map((r, i) => (
            <TableRow key={`${r.id ?? 'x'}-${i}`}>
              <TableCell>{r.name}</TableCell><TableCell>{formatNumber(r.total)}</TableCell><TableCell>{formatNumber(r.delivered)}</TableCell><TableCell>{formatNumber(r.failed)}</TableCell>
              <TableCell>{formatNumber(r.inProgress)}</TableCell><TableCell>{formatNumber(r.cancelled)}</TableCell><TableCell>{formatPercent(r.deliveredPercent, language)}</TableCell>
              <TableCell>{formatMoney(r.totalCharges)}</TableCell><TableCell>{formatMoney(r.codAmount)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  );
}

/** Operational report of the shipments created in a period: KPIs, daily trend, status mix and breakdowns. */
export default function OperationalReportsPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const [values, setValues] = useState<ReportFilterValues>({ fromDate: businessDaysAgo(29), toDate: businessToday(), branchId: null, senderCustomerId: null, driverId: null, status: null });
  const [tab, setTab] = useState(0);
  const report = useQuery({ queryKey: ['/reports/operations', values], queryFn: () => api.get<OperationalReportDto>('/reports/operations', values), placeholderData: keepPreviousData });
  const d = report.data;
  const groups = d ? [d.byBranch, d.byDriver, d.byCustomer, d.byServiceType] : [];

  return (
    <>
      <PageHeader title={t('nav.operationalReports')} />
      <ReportFilters values={values} onChange={setValues} lookups={[
        { name: 'branchId', label: t('common.branch'), source: 'branches' },
        { name: 'senderCustomerId', label: tx('المرسل', 'Sender'), source: 'senders' },
        { name: 'driverId', label: t('common.driver'), source: 'drivers' }
      ]}>
        <TextField select label={t('common.status')} value={values.status ?? ''} onChange={(e) => setValues({ ...values, status: e.target.value || null })} sx={{ width: 170 }}>
          <MenuItem value="">{t('common.all')}</MenuItem>
          {enumOptions('ShipmentStatus', language).map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
        </TextField>
        <ExportMenu url="/reports/operations/export" params={values} />
      </ReportFilters>
      {report.isFetching && <LinearProgress sx={{ mb: 1 }} />}
      {report.error && <ErrorAlert error={report.error} />}
      {d && (
        <>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            <Grid size={{ xs: 6, md: 2 }}><StatCard label={tx('الشحنات', 'Shipments')} value={formatNumber(d.totals.total)} /></Grid>
            <Grid size={{ xs: 6, md: 2 }}><StatCard tone="good" label={tx('سُلّمت', 'Delivered')} value={formatNumber(d.totals.delivered)} hint={formatPercent(d.totals.deliveredPercent, language)} /></Grid>
            <Grid size={{ xs: 6, md: 2 }}><StatCard tone="serious" label={tx('فشلت / مرتجعة', 'Failed / returned')} value={formatNumber(d.totals.failed)} /></Grid>
            <Grid size={{ xs: 6, md: 2 }}><StatCard label={tx('قيد التنفيذ', 'In progress')} value={formatNumber(d.totals.inProgress)} /></Grid>
            <Grid size={{ xs: 6, md: 2 }}><StatCard label={tx('الأجور', 'Charges')} value={formatMoney(d.totals.totalCharges)} /></Grid>
            <Grid size={{ xs: 6, md: 2 }}><StatCard label={tx('مبالغ التحصيل', 'COD amounts')} value={formatMoney(d.totals.codAmount)} /></Grid>
          </Grid>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            <Grid size={{ xs: 12, lg: 7 }}>
              <Card sx={{ height: '100%' }}>
                <CardHeader title={tx('يومياً', 'Daily')} titleTypographyProps={{ variant: 'subtitle1', fontWeight: 700 }} /><Divider />
                <CardContent><TrendChart data={d.daily.map((x) => ({ ...x, label: x.date.slice(5, 10) }))} xKey="label" series={[{ key: 'created', name: tx('أُنشئت', 'Created') }, { key: 'delivered', name: tx('سُلّمت', 'Delivered') }]} /></CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, lg: 5 }}>
              <Card sx={{ height: '100%' }}>
                <CardHeader title={tx('حسب الحالة الحالية', 'By current status')} titleTypographyProps={{ variant: 'subtitle1', fontWeight: 700 }} /><Divider />
                <CardContent><CategoryBarChart data={d.byStatus.map((s) => ({ label: enumLabel('ShipmentStatus', s.status, language), count: s.count }))} labelKey="label" valueKey="count" valueName={tx('الشحنات', 'Shipments')} /></CardContent>
              </Card>
            </Grid>
          </Grid>
          <Card>
            <Tabs value={tab} onChange={(_, v: number) => setTab(v)} variant="scrollable" sx={{ borderBottom: 1, borderColor: 'divider' }}>
              <Tab label={tx('حسب الفرع', 'By branch')} /><Tab label={tx('حسب السائق', 'By driver')} /><Tab label={tx('حسب العميل', 'By customer')} /><Tab label={tx('حسب نوع البضاعة', 'By goods type')} />
            </Tabs>
            <GroupTable rows={groups[tab] ?? []} />
          </Card>
        </>
      )}
    </>
  );
}
```

### 6.38 `frontend/src/features/shipments/DeliverDialog.tsx`

```tsx
import { useRef, useState } from 'react';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Grid2 as Grid, TextField, Typography } from '@mui/material';
import PhotoCameraOutlinedIcon from '@mui/icons-material/PhotoCameraOutlined';
import { useTranslation } from 'react-i18next';
import { AppError, toAppError } from '@/api/errors';
import { shipmentsApi } from '@/api/endpoints/shipments';
import type { ProofOfDeliveryDto } from '@/api/generated';
import SignaturePad, { type SignaturePadHandle } from '@/components/form/SignaturePad';
import { useTx } from '@/i18n';
import { formatMoney } from '@/utils/format';

interface DeliverDialogProps {
  open: boolean;
  shipmentId: number;
  trackingNumber: string;
  defaultRecipient?: string;
  codAmount?: number;
  /** Step 9: shipping charges collected from the recipient (charges on the recipient). */
  chargesAmount?: number;
  currencyCode?: string;
  onClose(): void;
  onDelivered(proof: ProofOfDeliveryDto): void;
}

/**
 * Proof of delivery: recipient name / relation, signature (canvas -> PNG), optional photo (camera on phones) and the
 * device location when the user allows it. Sent as multipart to POST /shipments/{id}/deliver.
 */
export default function DeliverDialog({ open, shipmentId, trackingNumber, defaultRecipient, codAmount, chargesAmount, currencyCode, onClose, onDelivered }: DeliverDialogProps) {
  const { t } = useTranslation();
  const tx = useTx();
  const signature = useRef<SignaturePadHandle>(null);
  const [recipientName, setRecipientName] = useState(defaultRecipient ?? '');
  const [relation, setRelation] = useState('');
  const [notes, setNotes] = useState('');
  const [photo, setPhoto] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<AppError | null>(null);

  const location = (): Promise<GeolocationPosition | null> =>
    new Promise((resolve) => {
      if (!navigator.geolocation) return resolve(null);
      navigator.geolocation.getCurrentPosition(resolve, () => resolve(null), { timeout: 5000, maximumAge: 60_000 });
    });

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const [blob, position] = await Promise.all([signature.current?.toBlob() ?? null, location()]);
      const proof = await shipmentsApi.deliver(
        shipmentId,
        {
          recipientName: recipientName.trim(),
          recipientRelation: relation.trim() || null,
          notes: notes.trim() || null,
          latitude: position ? Number(position.coords.latitude.toFixed(6)) : null,
          longitude: position ? Number(position.coords.longitude.toFixed(6)) : null
        },
        blob,
        photo
      );
      onDelivered(proof);
    } catch (caught) {
      setError(await toAppError(caught));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{tx('تأكيد التسليم', 'Confirm delivery')} · <span dir="ltr">{trackingNumber}</span></DialogTitle>
      <DialogContent dividers>
        {!!codAmount && codAmount > 0 && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            {tx('حصّل من المستلم', 'Collect from the recipient')}: <strong>{formatMoney(codAmount, currencyCode)}</strong>
          </Alert>
        )}
        {!!chargesAmount && chargesAmount > 0 && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            {tx('أجور الشحن على المستلم', 'Shipping charges on the recipient')}: <strong>{formatMoney(chargesAmount, currencyCode)}</strong>
            <br />
            {tx('تُسجَّل في الفرع بفاتورة باسم المستلم ثم سند قبض.', 'Recorded at the branch as an invoice to the recipient, then a receipt.')}
          </Alert>
        )}
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error.message}{Object.values(error.fieldErrors).flat().map((m) => ` · ${m}`)}</Alert>}
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 7 }}>
            <TextField label={tx('اسم المستلم', 'Recipient name')} required value={recipientName} onChange={(e) => setRecipientName(e.target.value)} inputProps={{ maxLength: 200 }} />
          </Grid>
          <Grid size={{ xs: 12, sm: 5 }}>
            <TextField label={tx('صلة القرابة', 'Relation')} value={relation} onChange={(e) => setRelation(e.target.value)} placeholder={tx('نفسه، زوجة، حارس…', 'Self, spouse, guard…')} />
          </Grid>
          <Grid size={12}>
            <SignaturePad ref={signature} />
          </Grid>
          <Grid size={12}>
            <Button component="label" variant="outlined" startIcon={<PhotoCameraOutlinedIcon />}>
              {photo ? photo.name : tx('صورة التسليم (اختياري)', 'Delivery photo (optional)')}
              <input hidden type="file" accept="image/jpeg,image/png,image/webp" capture="environment" onChange={(e) => setPhoto(e.target.files?.[0] ?? null)} />
            </Button>
            <Typography variant="caption" color="text.secondary" display="block">{tx('JPEG / PNG / WEBP حتى 5 ميغابايت', 'JPEG / PNG / WEBP up to 5 MB')}</Typography>
          </Grid>
          <Grid size={12}>
            <TextField label={t('common.notes')} multiline minRows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </Grid>
        </Grid>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>{t('common.cancel')}</Button>
        <Button variant="contained" color="success" onClick={() => void submit()} disabled={busy || recipientName.trim().length < 2}>
          {tx('تأكيد التسليم', 'Confirm delivery')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
```

### 6.39 `frontend/src/features/shipments/ShipmentDetailsPage.tsx`

```tsx
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Card, Chip, CircularProgress, Grid2 as Grid, IconButton, ListItemIcon, Menu, MenuItem, Stack, Tab, Table,
  TableBody, TableCell, TableHead, TableRow, Tabs, TextField, Typography
} from '@mui/material';
import PrintOutlinedIcon from '@mui/icons-material/PrintOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import SyncAltIcon from '@mui/icons-material/SyncAlt';
import TwoWheelerOutlinedIcon from '@mui/icons-material/TwoWheelerOutlined';
import TaskAltIcon from '@mui/icons-material/TaskAlt';
import ReportProblemOutlinedIcon from '@mui/icons-material/ReportProblemOutlined';
import BlockIcon from '@mui/icons-material/Block';
import BuildOutlinedIcon from '@mui/icons-material/BuildOutlined';
import ReceiptLongOutlinedIcon from '@mui/icons-material/ReceiptLongOutlined';
import UploadFileOutlinedIcon from '@mui/icons-material/UploadFileOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import DownloadOutlinedIcon from '@mui/icons-material/DownloadOutlined';
import { useTranslation } from 'react-i18next';
import { api, apiPath, openFile } from '@/api/client';
import { shipmentsApi } from '@/api/endpoints/shipments';
import type { InvoiceDto, ShipmentAttachmentType } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import Section from '@/components/common/Section';
import InfoGrid from '@/components/common/InfoGrid';
import StatusChip from '@/components/common/StatusChip';
import ErrorAlert from '@/components/common/ErrorAlert';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import { useLanguage, useTx } from '@/i18n';
import { enumLabel, enumOptions } from '@/i18n/enums';
import { formatDateTime, formatMoney, formatNumber } from '@/utils/format';
import ShipmentTimeline from './ShipmentTimeline';
import DeliverDialog from './DeliverDialog';
import { AssignDriverDialog, ChangeStatusDialog, CorrectStatusDialog, InvoiceFromShipmentDialog } from './ShipmentActionDialogs';

type DialogName = 'status' | 'correct' | 'driver' | 'deliver' | 'failed' | 'cancel' | 'invoice' | null;

/** Shipment details: summary, actions by permission and status, tracking timeline, packages, pricing, POD, files. */
/** Up to 3 decimals without trailing zeros (240 / 1.75 / 2.125). */
function formatQty(value: number): string {
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 }).format(value);
}

export default function ShipmentDetailsPage() {
  const id = Number(useParams().id);
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [tab, setTab] = useState(0);
  const [dialog, setDialog] = useState<DialogName>(null);
  const [menu, setMenu] = useState<HTMLElement | null>(null);
  const [busy, setBusy] = useState(false);
  const [attachmentType, setAttachmentType] = useState<ShipmentAttachmentType>('Document');

  const shipment = useQuery({ queryKey: ['/shipments', id], queryFn: () => shipmentsApi.get(id) });
  const history = useQuery({ queryKey: ['/shipments', id, 'history'], queryFn: () => shipmentsApi.history(id) });
  const pricing = useQuery({ queryKey: ['/shipments', id, 'pricing'], queryFn: () => shipmentsApi.pricing(id), enabled: tab === 2 });
  const proof = useQuery({
    queryKey: ['/shipments', id, 'pod'],
    queryFn: () => shipmentsApi.proofOfDelivery(id),
    enabled: tab === 3 && shipment.data?.status === 'Delivered',
    retry: false
  });
  const attachments = useQuery({ queryKey: ['/shipments', id, 'attachments'], queryFn: () => shipmentsApi.attachments(id), enabled: tab === 4 });

  const refresh = async () => {
    setDialog(null);
    await queryClient.invalidateQueries({ queryKey: ['/shipments'] });
  };

  const act = async (action: () => Promise<unknown>, message = t('common.done')) => {
    await action();
    notify.success(message);
    await refresh();
  };

  const print = async (format: 'Pdf' | 'Html') => {
    setBusy(true);
    try {
      openFile(await shipmentsApi.label(id, format));
    } catch (error) {
      notify.error(error);
    } finally {
      setBusy(false);
    }
  };

  const download = async (url: string, name: string) => {
    try {
      openFile(await api.download(apiPath(url), { fallbackName: name }));
    } catch (error) {
      notify.error(error);
    }
  };

  if (shipment.isLoading) return <Box sx={{ py: 8, textAlign: 'center' }}><CircularProgress /></Box>;
  if (shipment.error || !shipment.data) return <ErrorAlert error={shipment.error} onRetry={() => void shipment.refetch()} />;

  const s = shipment.data;
  const money = (value: number | null | undefined) => formatMoney(value, s.currencyCode, language);
  const finished = ['Delivered', 'Returned', 'Cancelled', 'LostOrDamaged'].includes(s.status);
  const canDeliver = s.status === 'OutForDelivery' && hasPermission(P.Shipments.ConfirmDelivery);

  return (
    <>
      <PageHeader
        back="/shipments"
        title={<Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}><span dir="ltr">{s.trackingNumber}</span><StatusChip enumName="ShipmentStatus" value={s.status} /></Stack>}
        subtitle={`${s.shipmentNumber} · ${s.serviceTypeName} · ${formatDateTime(s.createdAtUtc)}`}
        actions={
          <>
            <Button variant="outlined" startIcon={<PrintOutlinedIcon />} onClick={() => void print('Pdf')} disabled={busy}>{tx('طباعة الملصق', 'Print label')}</Button>
            {canDeliver && <Button variant="contained" color="success" startIcon={<TaskAltIcon />} onClick={() => setDialog('deliver')}>{tx('تأكيد التسليم', 'Deliver')}</Button>}
            {!finished && hasPermission(P.Shipments.Update) && ['Created', 'PickedUp', 'ReceivedAtBranch'].includes(s.status) && (
              <Button variant="outlined" startIcon={<EditOutlinedIcon />} onClick={() => navigate(`/shipments/${id}/edit`)}>{t('common.edit')}</Button>
            )}
            <IconButton onClick={(event) => setMenu(event.currentTarget)} aria-label={t('common.actions')}><MoreVertIcon /></IconButton>
            <Menu anchorEl={menu} open={!!menu} onClose={() => setMenu(null)} onClick={() => setMenu(null)}>
              <MenuItem onClick={() => void print('Html')}><ListItemIcon><PrintOutlinedIcon fontSize="small" /></ListItemIcon>{tx('ملصق HTML (طابعة حرارية)', 'HTML label (thermal printer)')}</MenuItem>
              {hasPermission(P.Shipments.ChangeStatus) && !finished && <MenuItem onClick={() => setDialog('status')}><ListItemIcon><SyncAltIcon fontSize="small" /></ListItemIcon>{tx('تغيير الحالة', 'Change status')}</MenuItem>}
              {hasPermission(P.Shipments.AssignDriver) && !finished && <MenuItem onClick={() => setDialog('driver')}><ListItemIcon><TwoWheelerOutlinedIcon fontSize="small" /></ListItemIcon>{tx('إسناد سائق', 'Assign driver')}</MenuItem>}
              {s.status === 'OutForDelivery' && hasPermission(P.Shipments.ConfirmDelivery) && <MenuItem onClick={() => setDialog('failed')}><ListItemIcon><ReportProblemOutlinedIcon fontSize="small" /></ListItemIcon>{tx('تسجيل فشل التسليم', 'Record failed delivery')}</MenuItem>}
              {hasPermission(P.Invoices.Create) && !s.invoiceId && s.status !== 'Cancelled' && <MenuItem onClick={() => setDialog('invoice')}><ListItemIcon><ReceiptLongOutlinedIcon fontSize="small" /></ListItemIcon>{tx('إنشاء فاتورة', 'Create invoice')}</MenuItem>}
              {hasPermission(P.Shipments.CorrectStatus) && <MenuItem onClick={() => setDialog('correct')}><ListItemIcon><BuildOutlinedIcon fontSize="small" /></ListItemIcon>{tx('تصحيح الحالة', 'Correct status')}</MenuItem>}
              {hasPermission(P.Shipments.Cancel) && s.nextStatuses.includes('Cancelled') && <MenuItem onClick={() => setDialog('cancel')} sx={{ color: 'error.main' }}><ListItemIcon><BlockIcon fontSize="small" color="error" /></ListItemIcon>{tx('إلغاء الشحنة', 'Cancel shipment')}</MenuItem>}
            </Menu>
          </>
        }
      />

      {s.hasPendingPriceOverride && <Alert severity="warning" sx={{ mb: 2 }}>{tx('يوجد سعر يدوي بانتظار الاعتماد.', 'A manual price is waiting for approval.')}</Alert>}
      {s.cancellationReason && <Alert severity="info" sx={{ mb: 2 }}>{tx('سبب الإلغاء', 'Cancellation reason')}: {s.cancellationReason}</Alert>}

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: 8 }}>
          <Section title={tx('المرسل والمستلم', 'Sender and recipient')}>
            <InfoGrid items={[
              { label: tx('المرسل', 'Sender'), value: `${s.senderCode} - ${s.senderName}`, span: 4 },
              { label: tx('هاتف المرسل', 'Sender phone'), value: <span dir="ltr">{s.senderPhone}</span>, span: 3 },
              { label: tx('عنوان المرسل', 'Sender address'), value: s.senderAddressLine, span: 5 },
              { label: tx('المستلم', 'Recipient'), value: `${s.recipientCode} - ${s.recipientName}`, span: 4 },
              { label: tx('هاتف المستلم', 'Recipient phone'), value: <span dir="ltr">{s.recipientContactPhone ?? s.recipientPhone}</span>, span: 3 },
              { label: tx('عنوان المستلم', 'Recipient address'), value: s.recipientAddressLine, span: 5 }
            ]} />
          </Section>
          <Section title={tx('المسار', 'Route')}>
            <InfoGrid items={[
              { label: tx('من', 'From'), value: `${s.originCityName} · ${s.originBranchName}` },
              { label: tx('إلى', 'To'), value: [s.destinationCityName, s.destinationZoneName, s.destinationBranchName].filter(Boolean).join(' · ') },
              { label: tx('الموقع الحالي', 'Current location'), value: [s.currentBranchName, s.currentWarehouseName].filter(Boolean).join(' · ') },
              { label: t('common.driver'), value: [s.assignedDriverName, s.vehiclePlateNumber].filter(Boolean).join(' · ') },
              { label: tx('محاولات التسليم', 'Delivery attempts'), value: formatNumber(s.deliveryAttemptCount) },
              { label: tx('تاريخ التسليم', 'Delivered at'), value: formatDateTime(s.deliveredAtUtc) },
              { label: tx('المحتوى', 'Contents'), value: `${s.packageDescription} · ${s.serviceTypeName}` },
              { label: tx('الطرود والوزن والحجم', 'Packages, weight, volume'), value: [
                `${formatNumber(s.packageCount)} ${tx('طرد', 'pcs')}`,
                `${formatQty(s.actualWeight)} ${tx('كغ', 'kg')}`,
                ...(s.totalVolumeCbm > 0 ? [`${formatQty(s.totalVolumeCbm)} ${tx('م³', 'm³')}`] : [])
              ].join(' · ') },
              { label: tx('طريقة التسعير', 'Pricing basis'), value: enumLabel('PricingBasis', s.pricingBasis, language) },
              { label: tx('أجور الشحن', 'Shipping charges'), value: enumLabel('ChargesPayer', s.chargesPaidBy, language) },
              ...(s.externalReference ? [{ label: tx('المرجع الخارجي', 'External reference'), value: s.externalReference }] : [])
            ]} />
          </Section>

          <Card>
            <Tabs value={tab} onChange={(_, value: number) => setTab(value)} variant="scrollable" sx={{ borderBottom: 1, borderColor: 'divider', px: 1 }}>
              <Tab label={tx('سجل التتبع', 'Tracking')} />
              <Tab label={`${tx('الطرود', 'Packages')} (${s.packageCount})`} />
              <Tab label={tx('التسعير', 'Pricing')} />
              <Tab label={tx('إثبات التسليم', 'Proof of delivery')} />
              <Tab label={tx('المرفقات', 'Attachments')} />
            </Tabs>
            <Box sx={{ p: 2 }}>
              {tab === 0 && (history.isLoading ? <CircularProgress size={24} /> : history.data ? <ShipmentTimeline history={history.data} /> : <ErrorAlert error={history.error} />)}
              {tab === 1 && (
                <Table size="small">
                  <TableHead><TableRow><TableCell>#</TableCell><TableCell>{tx('باركود الطرد', 'Barcode')}</TableCell><TableCell>{tx('الوصف', 'Description')}</TableCell><TableCell>{tx('الوزن', 'Weight')}</TableCell><TableCell>{tx('الأبعاد سم', 'Dimensions cm')}</TableCell><TableCell>{tx('الحجمي', 'Volumetric')}</TableCell></TableRow></TableHead>
                  <TableBody>
                    {s.packages.map((p) => (
                      <TableRow key={p.id}>
                        <TableCell>{p.sequenceNumber}</TableCell>
                        <TableCell dir="ltr">{p.packageBarcode}</TableCell>
                        <TableCell>{p.description ?? '-'}</TableCell>
                        <TableCell>{formatNumber(p.weightKg, 2)}</TableCell>
                        <TableCell dir="ltr">{p.lengthCm ? `${p.lengthCm} × ${p.widthCm} × ${p.heightCm}` : '-'}</TableCell>
                        <TableCell>{formatNumber(p.volumetricWeightKg, 2)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
              {tab === 2 && (
                <Table size="small">
                  <TableHead><TableRow><TableCell>{t('common.date')}</TableCell><TableCell>{t('common.reason')}</TableCell><TableCell>{tx('المصدر', 'Source')}</TableCell><TableCell>{tx('الوزن المحتسب', 'Chargeable')}</TableCell><TableCell>{tx('الشحن', 'Shipping')}</TableCell><TableCell>{tx('الضريبة', 'Tax')}</TableCell><TableCell>{t('common.total')}</TableCell></TableRow></TableHead>
                  <TableBody>
                    {pricing.data?.map((row, index) => (
                      <TableRow key={row.id ?? index}>
                        <TableCell>{formatDateTime(row.calculatedAtUtc)}</TableCell>
                        <TableCell>{row.reason}{row.overrideReason ? ` · ${row.overrideReason}` : ''}</TableCell>
                        <TableCell>{row.pricingRuleName ?? row.pricingSource}</TableCell>
                        <TableCell>{formatNumber(row.chargeableWeight, 2)}</TableCell>
                        <TableCell>{money(row.totalShippingCharge)}</TableCell>
                        <TableCell>{money(row.taxAmount)}</TableCell>
                        <TableCell><strong>{money(row.totalCharge)}</strong></TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
              {tab === 3 && (s.status !== 'Delivered' ? (
                <Typography color="text.secondary">{tx('لم تُسلّم الشحنة بعد.', 'Not delivered yet.')}</Typography>
              ) : proof.data ? (
                <>
                  <InfoGrid items={[
                    { label: tx('المستلم', 'Received by'), value: `${proof.data.recipientName}${proof.data.recipientRelation ? ` (${proof.data.recipientRelation})` : ''}` },
                    { label: tx('وقت التسليم', 'Delivered at'), value: formatDateTime(proof.data.deliveredAtUtc) },
                    { label: t('common.driver'), value: proof.data.driverName },
                    { label: tx('الموقع', 'Location'), value: proof.data.latitude ? <a href={`https://maps.google.com/?q=${proof.data.latitude},${proof.data.longitude}`} target="_blank" rel="noreferrer">{proof.data.latitude}, {proof.data.longitude}</a> : '-' },
                    { label: t('common.notes'), value: proof.data.notes, span: 12 }
                  ]} />
                  <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
                    {proof.data.signatureUrl && <Button startIcon={<DownloadOutlinedIcon />} onClick={() => void download(proof.data!.signatureUrl!, 'signature.png')}>{tx('التوقيع', 'Signature')}</Button>}
                    {proof.data.photoUrl && <Button startIcon={<DownloadOutlinedIcon />} onClick={() => void download(proof.data!.photoUrl!, 'photo.jpg')}>{tx('الصورة', 'Photo')}</Button>}
                  </Stack>
                </>
              ) : proof.isLoading ? <CircularProgress size={24} /> : <ErrorAlert error={proof.error} />)}
              {tab === 4 && (
                <>
                  {hasPermission(P.Shipments.Update) && (
                    <Stack direction="row" spacing={1} sx={{ mb: 2, alignItems: 'center' }}>
                      <Box sx={{ width: 220 }}>
                        <TextField select label={tx('نوع الملف', 'File type')} value={attachmentType} onChange={(e) => setAttachmentType(e.target.value as ShipmentAttachmentType)}>
                          {enumOptions('ShipmentAttachmentType', language).map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
                        </TextField>
                      </Box>
                      <Button component="label" variant="outlined" startIcon={<UploadFileOutlinedIcon />}>
                        {tx('رفع ملف', 'Upload file')}
                        <input hidden type="file" accept="image/jpeg,image/png,image/webp,application/pdf" onChange={async (event) => {
                          const file = event.target.files?.[0];
                          event.target.value = '';
                          if (!file) return;
                          try {
                            await act(() => shipmentsApi.uploadAttachment(id, file, attachmentType), t('common.saved'));
                          } catch (error) {
                            notify.error(error);
                          }
                        }} />
                      </Button>
                    </Stack>
                  )}
                  <Table size="small">
                    <TableBody>
                      {attachments.data?.map((file) => (
                        <TableRow key={file.id}>
                          <TableCell><Chip label={enumLabel('ShipmentAttachmentType', file.attachmentType, language)} /></TableCell>
                          <TableCell>{file.originalFileName}</TableCell>
                          <TableCell>{formatNumber(file.sizeBytes / 1024, 0)} KB</TableCell>
                          <TableCell>{formatDateTime(file.createdAtUtc)}</TableCell>
                          <TableCell align="left">
                            <IconButton size="small" onClick={() => void download(file.downloadUrl, file.originalFileName)}><DownloadOutlinedIcon fontSize="small" /></IconButton>
                            {hasPermission(P.Shipments.Update) && <IconButton size="small" color="error" onClick={() => void act(() => shipmentsApi.deleteAttachment(id, file.id)).catch(notify.error)}><DeleteOutlineIcon fontSize="small" /></IconButton>}
                          </TableCell>
                        </TableRow>
                      ))}
                      {attachments.data?.length === 0 && <TableRow><TableCell>{t('common.noData')}</TableCell></TableRow>}
                    </TableBody>
                  </Table>
                </>
              )}
            </Box>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, lg: 4 }}>
          <Section title={tx('المبالغ', 'Amounts')}>
            <Table size="small">
              <TableBody>
                {[
                  s.pricingBasis === 'Volume'
                    ? [tx('الحجم المحتسب', 'Chargeable volume'), `${formatQty(s.totalVolumeCbm)} ${tx('م³', 'm³')}`]
                    : [tx('الوزن المحتسب', 'Chargeable weight'), `${formatQty(s.chargeableWeight)} ${tx('كغ', 'kg')}`],
                  [tx('القيمة المصرّح بها', 'Declared value'), money(s.declaredValue)],
                  [tx('أجر الشحن', 'Shipping charge'), money(s.shippingCharge)],
                  [tx('الخصم', 'Discount'), money(s.discountAmount)],
                  [tx('الضريبة', 'Tax'), money(s.taxAmount)],
                  [t('common.total'), money(s.totalCharge)],
                  [tx('يدفعها', 'Paid by'), enumLabel('ChargesPayer', s.chargesPaidBy, language)]
                ].map(([label, value]) => (
                  <TableRow key={label}><TableCell sx={{ color: 'text.secondary' }}>{label}</TableCell><TableCell align="left" sx={{ fontVariantNumeric: 'tabular-nums' }}>{value}</TableCell></TableRow>
                ))}
              </TableBody>
            </Table>
            {s.invoiceId && <Button sx={{ mt: 1 }} onClick={() => navigate(`/invoices/${s.invoiceId}`)}>{tx('الفاتورة', 'Invoice')} {s.invoiceNumber ?? ''}</Button>}
          </Section>
          {s.isCOD && (
            <Section title={tx('تحصيل ثمن البضاعة للمرسل (COD)', 'Goods value collected for the sender (COD)')}>
              <InfoGrid items={[
                { label: tx('المبلغ المطلوب', 'Expected'), value: money(s.codAmount), span: 6 },
                { label: tx('المحصّل', 'Collected'), value: s.codCollectedAmount === null ? '-' : money(s.codCollectedAmount), span: 6 },
                { label: t('common.status'), value: <StatusChip enumName="CODStatus" value={s.codStatus} />, span: 12 }
              ]} />
            </Section>
          )}
        </Grid>
      </Grid>

      {dialog === 'status' && <ChangeStatusDialog open shipment={s} onClose={() => setDialog(null)} onSubmit={(v) => act(() => shipmentsApi.changeStatus(id, v))} />}
      {dialog === 'correct' && <CorrectStatusDialog open shipment={s} onClose={() => setDialog(null)} onSubmit={(v) => act(() => shipmentsApi.correctStatus(id, v))} />}
      {dialog === 'driver' && <AssignDriverDialog open shipment={s} onClose={() => setDialog(null)} onSubmit={(v) => act(() => shipmentsApi.assignDriver(id, v))} />}
      {dialog === 'invoice' && (
        <InvoiceFromShipmentDialog open shipment={s} onClose={() => setDialog(null)} onSubmit={async (v) => {
          const invoice = await api.post<InvoiceDto>(`/invoices/from-shipment/${id}`, v);
          notify.success(t('common.saved'));
          navigate(`/invoices/${invoice.id}`);
        }} />
      )}
      {dialog === 'deliver' && (
        <DeliverDialog open shipmentId={id} trackingNumber={s.trackingNumber} defaultRecipient={s.recipientName} codAmount={s.isCOD ? s.codAmount : 0} chargesAmount={s.chargesPaidBy === 'Recipient' ? s.totalCharge : 0} currencyCode={s.currencyCode}
          onClose={() => setDialog(null)} onDelivered={() => { notify.success(tx('تم تأكيد التسليم', 'Delivery confirmed')); void refresh(); }} />
      )}
      <ReasonDialog open={dialog === 'failed'} title={tx('فشل التسليم', 'Failed delivery')} label={tx('سبب الفشل', 'Reason')} onClose={() => setDialog(null)}
        onSubmit={(reason) => void act(() => shipmentsApi.deliveryFailed(id, { reason })).catch(notify.error)} />
      <ReasonDialog open={dialog === 'cancel'} danger title={tx('إلغاء الشحنة', 'Cancel shipment')} confirmText={tx('إلغاء الشحنة', 'Cancel shipment')} onClose={() => setDialog(null)}
        onSubmit={(reason) => void act(() => shipmentsApi.cancel(id, { reason })).catch(notify.error)} />
    </>
  );
}
```

### 6.40 `frontend/src/features/shipments/ShipmentFormPage.tsx`

```tsx
import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Controller, useForm, type FieldPath } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Card, CardContent, Checkbox, CircularProgress, Divider, FormControlLabel, Grid2 as Grid, IconButton,
  MenuItem, Stack, Table, TableBody, TableCell, TableRow, TextField, ToggleButton, ToggleButtonGroup, Tooltip, Typography
} from '@mui/material';
import PersonAddAltOutlinedIcon from '@mui/icons-material/PersonAddAltOutlined';
import CalculateOutlinedIcon from '@mui/icons-material/CalculateOutlined';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import { AppError } from '@/api/errors';
import { shipmentsApi } from '@/api/endpoints/shipments';
import type { ChargesPayer, CustomerAddressDto, CustomerDto, PricingBasis, SaveShipmentRequest, ShipmentDto, ShipmentPricingDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import Section from '@/components/common/Section';
import { useNotify } from '@/components/common/Notifier';
import LookupSelect from '@/components/form/LookupSelect';
import FormDialog from '@/components/form/FormDialog';
import { customerFields, newCustomerValues } from '@/features/customers/customerFields';
import { useLanguage, useTx } from '@/i18n';
import { formatMoney, formatNumber } from '@/utils/format';
import { splitPackages } from './packages';

const optionalNumber = z.number().nonnegative().nullable();

/** Same rules as the API validator (SaveShipmentRequestValidator); the API checks everything again. */
function buildSchema(tx: (ar: string, en: string) => string) {
  const required = tx('مطلوب', 'Required');
  const money = z.number({ invalid_type_error: required }).min(0, tx('لا يقبل قيمة سالبة', 'Must not be negative'));
  const id = z.number({ invalid_type_error: required, required_error: required }).int().positive(required);
  const maxPackages = 500;

  return z
    .object({
      senderCustomerId: id,
      senderAddressId: z.number().nullable(),
      recipientCustomerId: id,
      recipientAddressId: z.number().nullable(),
      originBranchId: id,
      destinationCityId: id,
      destinationZoneId: z.number().nullable(),
      destinationBranchId: z.number().nullable(),
      serviceTypeId: id,
      packageDescription: z.string().trim().min(1, required).max(300),
      // Step 9: a number of packages and the totals instead of one row per package.
      packageCount: z
        .number({ invalid_type_error: required })
        .int(tx('عدد صحيح', 'Whole number'))
        .min(1, tx('طرد واحد على الأقل', 'At least one package'))
        .max(maxPackages, `≤ ${maxPackages}`),
      totalWeightKg: z.number({ invalid_type_error: required }).gt(0, tx('أكبر من صفر', 'Greater than zero')).max(1_000_000),
      totalVolumeCbm: z.number().min(0, tx('لا يقبل قيمة سالبة', 'Must not be negative')).max(10000).nullable(),
      pricingBasis: z.enum(['Weight', 'Volume']),
      chargesPaidBy: z.enum(['Sender', 'Recipient']),
      declaredValue: money,
      insuranceValue: money,
      isCOD: z.boolean(),
      codAmount: money,
      requiresPickup: z.boolean(),
      requiresPackaging: z.boolean(),
      manualShippingCharge: optionalNumber,
      discountAmount: money,
      manualPriceReason: z.string().max(500).nullable(),
      senderNotes: z.string().max(500).nullable(),
      internalNotes: z.string().max(1000).nullable(),
      // Kept as is (no longer shown) so editing an older shipment does not erase it.
      externalReference: z.string().max(50).nullable()
    })
    .superRefine((v, ctx) => {
      if (v.insuranceValue > v.declaredValue) ctx.addIssue({ code: 'custom', path: ['insuranceValue'], message: tx('لا يزيد عن القيمة المصرّح بها', 'Cannot exceed the declared value') });
      if (v.isCOD && v.codAmount <= 0) ctx.addIssue({ code: 'custom', path: ['codAmount'], message: tx('مبلغ التحصيل مطلوب', 'COD amount is required') });
      if (v.pricingBasis === 'Volume' && !(v.totalVolumeCbm && v.totalVolumeCbm > 0))
        ctx.addIssue({ code: 'custom', path: ['totalVolumeCbm'], message: tx('الحجم مطلوب عند التسعير بالحجم', 'Volume is required for volume pricing') });
      if (Math.round(v.totalWeightKg * 1000) < v.packageCount)
        ctx.addIssue({ code: 'custom', path: ['totalWeightKg'], message: tx('الوزن قليل جداً لهذا العدد من الطرود', 'Weight too small for this number of packages') });
      if (v.senderCustomerId === v.recipientCustomerId) ctx.addIssue({ code: 'custom', path: ['recipientCustomerId'], message: tx('المستلم يختلف عن المرسل', 'Recipient must differ from sender') });
      if ((v.manualShippingCharge !== null || v.discountAmount > 0) && !v.manualPriceReason?.trim())
        ctx.addIssue({ code: 'custom', path: ['manualPriceReason'], message: tx('سبب السعر اليدوي أو الخصم مطلوب', 'Reason is required') });
      if (v.manualShippingCharge !== null && v.discountAmount > v.manualShippingCharge)
        ctx.addIssue({ code: 'custom', path: ['discountAmount'], message: tx('الخصم أكبر من السعر', 'Discount exceeds the price') });
    });
}

type Values = z.infer<ReturnType<typeof buildSchema>>;

function toNumber(text: string): number | null {
  return text === '' ? null : Number(text);
}

function toRequest(values: Values): SaveShipmentRequest {
  const { packageCount, totalWeightKg, totalVolumeCbm, ...rest } = values;
  return {
    ...rest,
    packages: splitPackages(packageCount, totalWeightKg),
    totalVolumeCbm: totalVolumeCbm ?? 0,
    codAmount: values.isCOD ? values.codAmount : 0
  };
}

function fromShipment(shipment: ShipmentDto): Values {
  return {
    senderCustomerId: shipment.senderCustomerId,
    senderAddressId: shipment.senderAddressId,
    recipientCustomerId: shipment.recipientCustomerId,
    recipientAddressId: shipment.recipientAddressId,
    originBranchId: shipment.originBranchId,
    destinationCityId: shipment.destinationCityId,
    destinationZoneId: shipment.destinationZoneId,
    destinationBranchId: shipment.destinationBranchId,
    serviceTypeId: shipment.serviceTypeId,
    packageDescription: shipment.packageDescription,
    packageCount: shipment.packageCount,
    totalWeightKg: shipment.actualWeight,
    totalVolumeCbm: shipment.totalVolumeCbm > 0 ? shipment.totalVolumeCbm : null,
    pricingBasis: shipment.pricingBasis,
    chargesPaidBy: shipment.chargesPaidBy,
    declaredValue: shipment.declaredValue,
    insuranceValue: shipment.insuranceValue,
    isCOD: shipment.isCOD,
    codAmount: shipment.codAmount,
    requiresPickup: shipment.requiresPickup,
    requiresPackaging: shipment.requiresPackaging,
    manualShippingCharge: shipment.manualShippingCharge,
    discountAmount: shipment.manualDiscountAmount,
    manualPriceReason: null,
    senderNotes: shipment.senderNotes,
    internalNotes: shipment.internalNotes,
    externalReference: shipment.externalReference
  };
}

/** Price breakdown returned by POST /shipments/quote (nothing is saved). */
function QuotePanel({ quote, loading }: { quote: ShipmentPricingDto | null; loading: boolean }) {
  const tx = useTx();
  const language = useLanguage();
  if (loading) return <Box sx={{ py: 4, textAlign: 'center' }}><CircularProgress size={28} /></Box>;
  if (!quote) return <Typography color="text.secondary">{tx('اضغط "احسب السعر" لعرض التفاصيل.', 'Press "Calculate price" to see the breakdown.')}</Typography>;
  const m = (value: number) => formatMoney(value, quote.currencyCode, language);
  const byVolume = quote.pricingBasis === 'Volume';
  const rows: [string, string][] = [
    [tx('طريقة التسعير', 'Pricing basis'), byVolume ? tx('بالحجم (م³)', 'By volume (m³)') : tx('بالوزن (كغ)', 'By weight (kg)')],
    byVolume
      ? [tx('الحجم', 'Volume'), `${formatNumber(quote.volumeCbm, 3)} m³`]
      : [tx('الوزن المحتسب', 'Chargeable weight'), `${formatNumber(quote.chargeableWeight, 2)} kg`],
    [tx('القاعدة', 'Rule'), quote.pricingRuleName ?? quote.pricingSource],
    [tx('الأجر الأساسي', 'Base charge'), m(quote.baseCharge)],
    byVolume
      ? [`${tx('أجر الحجم', 'Volume charge')} (${m(quote.pricePerCubicMeter)} / m³)`, m(quote.volumeCharge)]
      : [tx('أجر الوزن', 'Weight charge'), m(quote.weightCharge)],
    [tx('رسوم الخدمات', 'Service fees'), m(quote.serviceFeesTotal)],
    [tx('الخصم', 'Discount'), m(quote.discountAmount)],
    [tx('أجر الشحن', 'Shipping charge'), m(quote.totalShippingCharge)],
    [tx('عمولة التحصيل', 'COD commission'), m(quote.codCommission)],
    [`${tx('الضريبة', 'Tax')} ${formatNumber(quote.taxPercentage, 2)}%`, m(quote.taxAmount)]
  ];
  return (
    <>
      <Table size="small">
        <TableBody>
          {rows.map(([label, value]) => (
            <TableRow key={label}>
              <TableCell sx={{ color: 'text.secondary', border: 0, py: 0.5 }}>{label}</TableCell>
              <TableCell align="left" sx={{ border: 0, py: 0.5, fontVariantNumeric: 'tabular-nums' }}>{value}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <Divider sx={{ my: 1 }} />
      <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
        <Typography variant="h6">{tx('الإجمالي', 'Total')}</Typography>
        <Typography variant="h6" sx={{ fontVariantNumeric: 'tabular-nums' }}>{m(quote.totalCharge)}</Typography>
      </Stack>
    </>
  );
}

/**
 * Create / edit shipment. Client validation (zod) mirrors the API; the price comes from the API (quote) and is
 * recalculated by the API when saving, so the screen never decides a price.
 */
export default function ShipmentFormPage() {
  const { id } = useParams();
  const editId = id ? Number(id) : null;
  const { t } = useTranslation();
  const tx = useTx();
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { user, hasPermission } = useAuth();
  const [serverError, setServerError] = useState<AppError | null>(null);
  const [quote, setQuote] = useState<ShipmentPricingDto | null>(null);
  const [quickAdd, setQuickAdd] = useState<null | 'sender' | 'recipient'>(null);

  const existing = useQuery({ queryKey: ['/shipments', editId], queryFn: () => shipmentsApi.get(editId!), enabled: editId !== null });

  const form = useForm<Values>({
    resolver: zodResolver(buildSchema(tx)),
    defaultValues: {
      senderCustomerId: undefined as unknown as number,
      senderAddressId: null,
      recipientCustomerId: undefined as unknown as number,
      recipientAddressId: null,
      originBranchId: (user?.branchId ?? undefined) as number,
      destinationCityId: undefined as unknown as number,
      destinationZoneId: null,
      destinationBranchId: null,
      serviceTypeId: undefined as unknown as number,
      packageDescription: '',
      packageCount: 1,
      totalWeightKg: undefined as unknown as number,
      totalVolumeCbm: null,
      pricingBasis: 'Weight',
      chargesPaidBy: 'Recipient',
      declaredValue: 0,
      insuranceValue: 0,
      isCOD: false,
      codAmount: 0,
      requiresPickup: false,
      requiresPackaging: false,
      manualShippingCharge: null,
      discountAmount: 0,
      manualPriceReason: null,
      senderNotes: null,
      internalNotes: null,
      externalReference: null
    }
  });
  const { control, register, handleSubmit, watch, setValue, formState, reset } = form;

  useEffect(() => {
    if (existing.data) reset(fromShipment(existing.data));
  }, [existing.data, reset]);

  const senderId = watch('senderCustomerId');
  const recipientId = watch('recipientCustomerId');
  const cityId = watch('destinationCityId');
  const isCOD = watch('isCOD');
  const pricingBasis = watch('pricingBasis');

  const senderAddresses = useQuery({
    queryKey: ['/customers', senderId, 'addresses'],
    queryFn: () => api.get<CustomerAddressDto[]>(`/customers/${senderId}/addresses`),
    enabled: !!senderId
  });
  const recipientAddresses = useQuery({
    queryKey: ['/customers', recipientId, 'addresses'],
    queryFn: () => api.get<CustomerAddressDto[]>(`/customers/${recipientId}/addresses`),
    enabled: !!recipientId
  });

  const quoteMutation = useMutation({
    mutationFn: (values: Values) => shipmentsApi.quote(toRequest(values)),
    onSuccess: setQuote,
    onError: (error) => setServerError(error as AppError)
  });

  const save = handleSubmit(async (values) => {
    setServerError(null);
    try {
      const saved = editId ? await shipmentsApi.update(editId, toRequest(values)) : await shipmentsApi.create(toRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['/shipments'] });
      notify.success(editId ? t('common.saved') : `${tx('تم تسجيل الشحنة', 'Shipment created')} ${saved.trackingNumber}`);
      navigate(`/shipments/${saved.id}`, { replace: true });
    } catch (error) {
      setServerError(error as AppError);
    }
  });

  const err = (name: FieldPath<Values>) => {
    const parts = name.split('.');
    let node: unknown = formState.errors;
    for (const part of parts) node = (node as Record<string, unknown> | undefined)?.[part];
    return (node as { message?: string } | undefined)?.message ?? serverError?.fieldErrors[name]?.[0];
  };

  const lookupField = (name: 'senderCustomerId' | 'recipientCustomerId' | 'originBranchId' | 'destinationCityId' | 'destinationZoneId' | 'destinationBranchId' | 'serviceTypeId', label: string, source: Parameters<typeof LookupSelect>[0]['source'], required = false, params?: Record<string, unknown>, onPicked?: () => void) => (
    <Controller
      control={control}
      name={name}
      render={({ field }) => (
        <LookupSelect
          source={source}
          label={label}
          required={required}
          params={params}
          value={field.value ?? null}
          onChange={(value) => {
            field.onChange(value === null ? (required ? undefined : null) : Number(value));
            onPicked?.();
          }}
          error={!!err(name)}
          helperText={err(name)}
        />
      )}
    />
  );

  const addressSelect = (name: 'senderAddressId' | 'recipientAddressId', addresses: CustomerAddressDto[] | undefined) => (
    <Controller
      control={control}
      name={name}
      render={({ field }) => (
        <TextField select label={tx('العنوان', 'Address')} value={field.value ?? ''} onChange={(event) => field.onChange(event.target.value === '' ? null : Number(event.target.value))} disabled={!addresses}>
          <MenuItem value="">{tx('بدون عنوان محدد', 'No specific address')}</MenuItem>
          {addresses?.map((address) => (
            <MenuItem key={address.id} value={address.id}>{address.label} - {address.cityName} - {address.addressLine}</MenuItem>
          ))}
        </TextField>
      )}
    />
  );

  const numberProps = (name: FieldPath<Values>) => ({
    ...register(name, { setValueAs: (value: string | number | null) => (typeof value === 'number' ? value : toNumber(value ?? '')) }),
    type: 'number',
    inputProps: { step: 'any', dir: 'ltr' as const },
    error: !!err(name),
    helperText: err(name)
  });

  if (editId && existing.isLoading) {
    return <Box sx={{ py: 8, textAlign: 'center' }}><CircularProgress /></Box>;
  }

  return (
    <form onSubmit={save} noValidate>
      <PageHeader
        back={editId ? `/shipments/${editId}` : '/shipments'}
        title={editId ? `${tx('تعديل الشحنة', 'Edit shipment')} ${existing.data?.trackingNumber ?? ''}` : t('nav.newShipment')}
        actions={
          <>
            <Button variant="outlined" startIcon={<CalculateOutlinedIcon />} onClick={handleSubmit((values) => quoteMutation.mutate(values))} disabled={quoteMutation.isPending}>
              {tx('احسب السعر', 'Calculate price')}
            </Button>
            <Button type="submit" variant="contained" disabled={formState.isSubmitting}>
              {editId ? t('common.save') : tx('تسجيل الشحنة', 'Create shipment')}
            </Button>
          </>
        }
      />

      {serverError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {serverError.message}
          {Object.entries(serverError.fieldErrors).length > 0 && (
            <ul style={{ margin: 0 }}>
              {Object.entries(serverError.fieldErrors).map(([field, messages]) => <li key={field}>{messages.join(' · ')}</li>)}
            </ul>
          )}
        </Alert>
      )}

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: 8 }}>
          <Section title={tx('المرسل والمستلم', 'Sender and recipient')}>
            <Grid container spacing={2}>
              <Grid size={{ xs: 12, md: 6 }}>
                <Stack direction="row" spacing={1}>
                  <Box sx={{ flex: 1 }}>{lookupField('senderCustomerId', tx('المرسل', 'Sender'), 'senders', true, undefined, () => setValue('senderAddressId', null))}</Box>
                  <Tooltip title={tx('عميل جديد', 'New customer')}><IconButton onClick={() => setQuickAdd('sender')}><PersonAddAltOutlinedIcon /></IconButton></Tooltip>
                </Stack>
              </Grid>
              <Grid size={{ xs: 12, md: 6 }}>{addressSelect('senderAddressId', senderAddresses.data)}</Grid>
              <Grid size={{ xs: 12, md: 6 }}>
                <Stack direction="row" spacing={1}>
                  <Box sx={{ flex: 1 }}>{lookupField('recipientCustomerId', tx('المستلم', 'Recipient'), 'customers', true, undefined, () => setValue('recipientAddressId', null))}</Box>
                  <Tooltip title={tx('عميل جديد', 'New customer')}><IconButton onClick={() => setQuickAdd('recipient')}><PersonAddAltOutlinedIcon /></IconButton></Tooltip>
                </Stack>
              </Grid>
              <Grid size={{ xs: 12, md: 6 }}>{addressSelect('recipientAddressId', recipientAddresses.data)}</Grid>
            </Grid>
          </Section>

          <Section title={tx('المسار', 'Route')}>
            <Grid container spacing={2}>
              <Grid size={{ xs: 12, md: 4 }}>{lookupField('originBranchId', tx('فرع الإنشاء', 'Origin branch'), 'branches', true)}</Grid>
              <Grid size={{ xs: 12, md: 4 }}>{lookupField('destinationCityId', tx('مدينة الوجهة', 'Destination city'), 'cities', true, undefined, () => setValue('destinationZoneId', null))}</Grid>
              <Grid size={{ xs: 12, md: 4 }}>{lookupField('destinationZoneId', tx('منطقة الوجهة', 'Destination zone'), 'zones', false, cityId ? { cityId } : undefined)}</Grid>
              <Grid size={{ xs: 12, md: 4 }}>{lookupField('destinationBranchId', tx('فرع الوجهة (اختياري)', 'Destination branch (optional)'), 'branches')}</Grid>
              <Grid size={12}>
                <FormControlLabel control={<Controller control={control} name="requiresPickup" render={({ field }) => <Checkbox checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />} />} label={tx('استلام من عند المرسل', 'Pickup from sender')} />
                <FormControlLabel control={<Controller control={control} name="requiresPackaging" render={({ field }) => <Checkbox checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />} />} label={tx('تغليف', 'Packaging')} />
              </Grid>
            </Grid>
          </Section>

          <Section title={tx('البضاعة والتسعير', 'Goods and pricing')}>
            <Grid container spacing={2}>
              <Grid size={{ xs: 12, md: 6 }}>
                <TextField label={tx('وصف المحتوى', 'Contents')} required {...register('packageDescription')} error={!!err('packageDescription')} helperText={err('packageDescription')} />
              </Grid>
              <Grid size={{ xs: 12, md: 6 }}>{lookupField('serviceTypeId', tx('نوع البضاعة', 'Goods type'), 'serviceTypes', true)}</Grid>
              <Grid size={{ xs: 12, sm: 4 }}>
                <TextField label={tx('عدد الطرود', 'Number of packages')} required {...numberProps('packageCount')} inputProps={{ step: 1, min: 1, dir: 'ltr' }} />
              </Grid>
              <Grid size={{ xs: 12, sm: 4 }}>
                <TextField label={tx('الوزن الكلي (كغ)', 'Total weight (kg)')} required {...numberProps('totalWeightKg')} />
              </Grid>
              <Grid size={{ xs: 12, sm: 4 }}>
                <TextField
                  label={tx('الحجم الكلي (م³)', 'Total volume (m³)')}
                  required={pricingBasis === 'Volume'}
                  {...numberProps('totalVolumeCbm')}
                  helperText={err('totalVolumeCbm') ?? tx('مثال: 1.25 متر مكعب', 'e.g. 1.25 cubic metres')}
                />
              </Grid>
              <Grid size={12}>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>{tx('طريقة التسعير', 'Pricing basis')}</Typography>
                <Controller
                  control={control}
                  name="pricingBasis"
                  render={({ field }) => (
                    <ToggleButtonGroup exclusive color="primary" value={field.value} onChange={(_, value: PricingBasis | null) => value && field.onChange(value)}>
                      <ToggleButton value="Weight">{tx('بالوزن (سعر الكيلو)', 'By weight (per kg)')}</ToggleButton>
                      <ToggleButton value="Volume">{tx('بالحجم (سعر المتر المكعب)', 'By volume (per m³)')}</ToggleButton>
                    </ToggleButtonGroup>
                  )}
                />
              </Grid>
            </Grid>
          </Section>

          <Section title={tx('الدفع والتحصيل', 'Payment and collection')}>
            <Grid container spacing={2}>
              <Grid size={12}>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>{tx('أجور الشحن يدفعها', 'Shipping charges paid by')}</Typography>
                <Controller
                  control={control}
                  name="chargesPaidBy"
                  render={({ field }) => (
                    <ToggleButtonGroup exclusive color="primary" value={field.value} onChange={(_, value: ChargesPayer | null) => value && field.onChange(value)}>
                      <ToggleButton value="Recipient">{tx('المستلم (الدفع عند الاستلام)', 'Recipient (on delivery)')}</ToggleButton>
                      <ToggleButton value="Sender">{tx('المرسل', 'Sender')}</ToggleButton>
                    </ToggleButtonGroup>
                  )}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 3 }}><TextField label={tx('القيمة المصرّح بها', 'Declared value')} {...numberProps('declaredValue')} /></Grid>
              <Grid size={{ xs: 12, sm: 3 }}><TextField label={tx('قيمة التأمين', 'Insurance value')} {...numberProps('insuranceValue')} /></Grid>
              <Grid size={{ xs: 12, sm: 6 }}>
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                  <Controller control={control} name="isCOD" render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />} label={tx('تحصيل ثمن البضاعة للمرسل', 'Collect goods value')} sx={{ whiteSpace: 'nowrap' }} />} />
                  <TextField label={tx('المبلغ', 'Amount')} disabled={!isCOD} {...numberProps('codAmount')} />
                </Stack>
              </Grid>
            </Grid>
          </Section>

          {hasPermission(P.Shipments.OverridePrice) && (
            <Section title={tx('سعر يدوي / خصم (يحتاج اعتماداً عند تجاوز الحد)', 'Manual price / discount (needs approval above the threshold)')}>
              <Grid container spacing={2}>
                <Grid size={{ xs: 12, sm: 4 }}><TextField label={tx('سعر الشحن اليدوي', 'Manual shipping charge')} {...numberProps('manualShippingCharge')} /></Grid>
                <Grid size={{ xs: 12, sm: 4 }}><TextField label={tx('الخصم', 'Discount')} {...numberProps('discountAmount')} /></Grid>
                <Grid size={{ xs: 12, sm: 4 }}><TextField label={tx('السبب', 'Reason')} {...register('manualPriceReason', { setValueAs: (v: string) => v || null })} error={!!err('manualPriceReason')} helperText={err('manualPriceReason')} /></Grid>
              </Grid>
            </Section>
          )}

          <Section title={t('common.notes')}>
            <Grid container spacing={2}>
              <Grid size={{ xs: 12, md: 6 }}><TextField multiline minRows={2} label={tx('ملاحظات المرسل (تظهر على الملصق)', 'Sender notes (on the label)')} {...register('senderNotes', { setValueAs: (v: string) => v || null })} /></Grid>
              <Grid size={{ xs: 12, md: 6 }}><TextField multiline minRows={2} label={tx('ملاحظات داخلية', 'Internal notes')} {...register('internalNotes', { setValueAs: (v: string) => v || null })} /></Grid>
            </Grid>
          </Section>
        </Grid>

        <Grid size={{ xs: 12, lg: 4 }}>
          <Card sx={{ position: { lg: 'sticky' }, top: { lg: 88 } }}>
            <CardContent>
              <Typography variant="h6" sx={{ mb: 1 }}>{tx('السعر', 'Price')}</Typography>
              <QuotePanel quote={quote} loading={quoteMutation.isPending} />
              <Button fullWidth variant="outlined" sx={{ mt: 2 }} startIcon={<CalculateOutlinedIcon />} onClick={handleSubmit((values) => quoteMutation.mutate(values))}>
                {tx('احسب السعر', 'Calculate price')}
              </Button>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      <FormDialog<Record<string, unknown>>
        open={quickAdd !== null}
        title={tx('عميل جديد', 'New customer')}
        fields={customerFields(tx)}
        defaultValues={newCustomerValues(quickAdd === 'sender' ? 'Sender' : 'Recipient', user?.branchId ?? null)}
        onClose={() => setQuickAdd(null)}
        onSubmit={async (values) => {
          const created = await api.post<CustomerDto>('/customers', values);
          setValue(quickAdd === 'sender' ? 'senderCustomerId' : 'recipientCustomerId', created.id, { shouldValidate: true });
          await queryClient.invalidateQueries({ queryKey: ['lookup'] });
          notify.success(t('common.saved'));
          setQuickAdd(null);
        }}
      />
    </form>
  );
}
```

### 6.41 `frontend/src/features/shipments/ShipmentListPage.tsx`

```tsx
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, InputAdornment, TextField } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import PrintOutlinedIcon from '@mui/icons-material/PrintOutlined';
import QrCodeScannerIcon from '@mui/icons-material/QrCodeScanner';
import type { GridColDef } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { openFile } from '@/api/client';
import { shipmentsApi } from '@/api/endpoints/shipments';
import type { ShipmentListItemDto } from '@/api/generated';
import { Can } from '@/auth/guards';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import ExportMenu from '@/components/common/ExportMenu';
import { useNotify } from '@/components/common/Notifier';
import DataTable from '@/components/data/DataTable';
import FilterBar, { type FilterDef } from '@/components/data/FilterBar';
import { col, S } from '@/components/data/columns';
import { useApiList } from '@/components/data/useApiList';
import { useListState } from '@/components/data/useListState';
import { useTx } from '@/i18n';
import { businessDayEndUtc, businessDayStartUtc } from '@/utils/format';

/**
 * Shipment list: server paging / sorting / search, filters kept in the URL, Excel / PDF export with the same
 * filters, bulk label printing of the selected rows and a scan box that opens a shipment by tracking number.
 */
export default function ShipmentListPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const navigate = useNavigate();
  const notify = useNotify();
  const state = useListState({ defaultSort: 'createdAtUtc' });
  const [selected, setSelected] = useState<number[]>([]);
  const [scan, setScan] = useState('');

  // The date filters are business days in the URL; the API wants UTC instants.
  const params = useMemo(() => {
    const { createdFrom, createdTo, ...rest } = state.params as Record<string, string | number>;
    return {
      ...rest,
      createdFromUtc: createdFrom ? businessDayStartUtc(String(createdFrom)) : undefined,
      createdToUtc: createdTo ? businessDayEndUtc(String(createdTo)) : undefined
    };
  }, [state.params]);

  const list = useApiList<ShipmentListItemDto>('/shipments', params);

  const filters: FilterDef[] = [
    { name: 'status', label: t('common.status'), type: 'enum', enumName: 'ShipmentStatus' },
    { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' },
    { name: 'senderCustomerId', label: tx('المرسل', 'Sender'), type: 'lookup', lookup: 'senders' },
    { name: 'driverId', label: t('common.driver'), type: 'lookup', lookup: 'drivers' },
    { name: 'serviceTypeId', label: tx('نوع البضاعة', 'Goods type'), type: 'lookup', lookup: 'serviceTypes' },
    { name: 'isCOD', label: tx('دفع عند الاستلام', 'COD'), type: 'boolean' },
    { name: 'createdFrom', label: t('common.fromDate'), type: 'date' },
    { name: 'createdTo', label: t('common.toDate'), type: 'date' }
  ];

  const columns: GridColDef<ShipmentListItemDto>[] = [
    col.text('trackingNumber', tx('رقم التتبع', 'Tracking no.'), { ...S, minWidth: 150, renderCell: ({ value }) => <span dir="ltr" style={{ fontWeight: 600 }}>{value}</span> }),
    col.text('shipmentNumber', tx('رقم الشحنة', 'Shipment no.'), { ...S, minWidth: 130 }),
    col.status('status', t('common.status'), 'ShipmentStatus', S),
    col.text('senderName', tx('المرسل', 'Sender'), { minWidth: 150 }),
    col.text('recipientName', tx('المستلم', 'Recipient'), { minWidth: 150, renderCell: ({ row }) => `${row.recipientName} · ${row.recipientPhone}` }),
    col.text('destinationCityName', tx('مدينة الوجهة', 'Destination'), { minWidth: 110 }),
    col.text('currentBranchName', tx('الفرع الحالي', 'Current branch'), { minWidth: 120 }),
    col.text('assignedDriverName', t('common.driver'), { minWidth: 120 }),
    col.number('packageCount', tx('طرود', 'Pcs'), 0, { width: 70 }),
    col.number('chargeableWeight', tx('الوزن', 'Weight'), 2, { ...S, width: 90 }),
    col.money('codAmount', tx('مبلغ التحصيل', 'COD'), 'currencyCode', S),
    col.money('totalCharge', tx('الأجور', 'Charges'), 'currencyCode', S),
    col.dateTime('createdAtUtc', tx('تاريخ التسجيل', 'Created'), S)
  ];

  const printLabels = async () => {
    try {
      openFile(await shipmentsApi.labels(selected));
    } catch (error) {
      notify.error(error);
    }
  };

  const openScanned = async () => {
    const code = scan.trim();
    if (!code) return;
    try {
      const found = await shipmentsApi.track(code);
      setScan('');
      navigate(`/shipments/${found.shipmentId}`);
    } catch (error) {
      notify.error(error);
    }
  };

  return (
    <>
      <PageHeader
        title={t('nav.shipments')}
        subtitle={list.data ? `${list.data.totalCount} ${tx('شحنة', 'shipments')}` : undefined}
        actions={
          <>
            <TextField
              placeholder={tx('امسح الباركود / رقم التتبع', 'Scan barcode / tracking no.')}
              value={scan}
              onChange={(event) => setScan(event.target.value)}
              onKeyDown={(event) => event.key === 'Enter' && void openScanned()}
              sx={{ width: 260 }}
              inputProps={{ dir: 'ltr' }}
              InputProps={{ startAdornment: <InputAdornment position="start"><QrCodeScannerIcon fontSize="small" /></InputAdornment> }}
            />
            <Button variant="outlined" startIcon={<PrintOutlinedIcon />} disabled={selected.length === 0 || selected.length > 100} onClick={() => void printLabels()}>
              {tx('طباعة الملصقات', 'Print labels')} {selected.length > 0 ? `(${selected.length})` : ''}
            </Button>
            <ExportMenu url="/shipments/export" params={params} />
            <Can permission={P.Shipments.Create}>
              <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/shipments/new')}>
                {t('nav.newShipment')}
              </Button>
            </Can>
          </>
        }
      />
      <FilterBar state={state} filters={filters} searchPlaceholder={tx('رقم، اسم أو هاتف', 'Number, name or phone')} />
      <DataTable<ShipmentListItemDto>
        columns={columns}
        data={list.data}
        loading={list.isFetching}
        error={list.error}
        onRetry={() => void list.refetch()}
        state={state}
        rowLink={(row) => `/shipments/${row.id}`}
        checkboxSelection
        onSelectionChange={(ids) => setSelected(ids.map(Number))}
      />
    </>
  );
}
```

### 6.42 `frontend/src/features/shipments/packages.ts`

```typescript
import type { ShipmentPackageRequest } from '@/api/generated';

/**
 * Splits the total weight over the packages in grams, so the package weights always add up exactly to the total
 * (e.g. 10 kg / 3 = 3.334 + 3.333 + 3.333). The API needs one row per package for the barcodes and labels.
 */
export function splitPackages(count: number, totalWeightKg: number): ShipmentPackageRequest[] {
  const grams = Math.round(totalWeightKg * 1000);
  const base = Math.floor(grams / count);
  const remainder = grams - base * count;
  return Array.from({ length: count }, (_, index) => ({
    description: null,
    weightKg: (base + (index < remainder ? 1 : 0)) / 1000,
    lengthCm: null,
    widthCm: null,
    heightCm: null
  }));
}
```

### 6.43 `frontend/src/test/utils.test.ts`

```typescript
import { describe, expect, it } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { AppError, firstFieldError, toAppError } from '@/api/errors';
import { tokenStore } from '@/api/tokenStore';
import { apiPath } from '@/api/client';
import { splitCodes } from '@/components/form/ScanBox';
import { splitPackages } from '@/features/shipments/packages';
import { enumLabel, enumOptions } from '@/i18n/enums';
import { businessDayEndUtc, businessDayStartUtc, formatDate, formatDateTime, formatMoney } from '@/utils/format';

describe('format', () => {
  it('formats money with two decimals and western digits', () => {
    expect(formatMoney(1234.5, 'SYP', 'en')).toBe('1,234.50 SYP');
    expect(formatMoney(null)).toBe('-');
  });

  it('shows UTC timestamps in the business time zone (Asia/Damascus, UTC+3)', () => {
    expect(formatDateTime('2026-10-01T21:30:00Z')).toBe('2026-10-02 00:30');
    expect(formatDateTime('2026-10-01T21:30:00')).toBe('2026-10-02 00:30');
  });

  it('keeps date-only values as they are', () => {
    expect(formatDate('2026-10-01')).toBe('2026-10-01');
    expect(formatDate('2026-10-01T00:00:00')).toBe('2026-10-01');
  });

  it('converts a business day to a UTC range', () => {
    expect(businessDayStartUtc('2026-10-02')).toBe('2026-10-01T21:00:00.000Z');
    expect(businessDayEndUtc('2026-10-02')).toBe('2026-10-02T21:00:00.000Z');
  });
});

describe('enum labels', () => {
  it('translates API enum names', () => {
    expect(enumLabel('ShipmentStatus', 'OutForDelivery', 'ar')).toBe('خارج للتوزيع');
    expect(enumLabel('ShipmentStatus', 'OutForDelivery', 'en')).toBe('Out for delivery');
    expect(enumLabel('ShipmentStatus', 'Unknown', 'ar')).toBe('Unknown');
    expect(enumOptions('CustomerType', 'en')).toEqual([{ value: 'Individual', label: 'Individual' }, { value: 'Business', label: 'Business' }]);
  });
});

describe('scan codes', () => {
  it('splits pasted codes and removes duplicates', () => {
    expect(splitCodes(' SH1\nSH2, SH1;SH3 ')).toEqual(['SH1', 'SH2', 'SH3']);
    expect(splitCodes('')).toEqual([]);
  });
});

describe('api helpers', () => {
  it('strips the API prefix from server links', () => {
    expect(apiPath('/api/v1/shipments/5/attachments/9/content')).toBe('/shipments/5/attachments/9/content');
  });

  it('maps the error envelope to AppError with field errors', async () => {
    const error = new AxiosError('bad', 'ERR', undefined, undefined, {
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { success: false, message: '', data: null, traceId: 'abc', timestampUtc: '', error: { code: 'VALIDATION_ERROR', message: 'خطأ', details: { phone: ['الهاتف مطلوب'] } } }
    });
    const appError = await toAppError(error);
    expect(appError).toBeInstanceOf(AppError);
    expect(appError.status).toBe(400);
    expect(appError.isValidation).toBe(true);
    expect(appError.traceId).toBe('abc');
    expect(firstFieldError(appError, 'phone')).toBe('الهاتف مطلوب');
  });

  it('reports a network error as status 0', async () => {
    const appError = await toAppError(new AxiosError('Network Error', 'ERR_NETWORK'));
    expect(appError.status).toBe(0);
  });
});

describe('token store', () => {
  it('keeps the access token in memory and detects expiry', () => {
    tokenStore.set('token', new Date(Date.now() + 10_000).toISOString());
    expect(tokenStore.get()).toBe('token');
    expect(tokenStore.isExpiringSoon()).toBe(true);
    tokenStore.set('token', new Date(Date.now() + 600_000).toISOString());
    expect(tokenStore.isExpiringSoon()).toBe(false);
    tokenStore.clear();
    expect(tokenStore.get()).toBeNull();
  });
});

describe('splitPackages', () => {
  it('splits the total weight in grams so the packages add up exactly', () => {
    const packages = splitPackages(3, 10);
    expect(packages.map((p) => p.weightKg)).toEqual([3.334, 3.333, 3.333]);
    expect(Math.round(packages.reduce((sum, p) => sum + (p.weightKg ?? 0), 0) * 1000)).toBe(10000);
  });

  it('gives one package the whole weight', () => {
    expect(splitPackages(1, 2.5)).toEqual([{ description: null, weightKg: 2.5, lengthCm: null, widthCm: null, heightCm: null }]);
  });
});
```

