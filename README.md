# ShippingERP — نظام إدارة الشحن والخدمات اللوجستية

واجهة برمجية (ASP.NET Core 8 Web API) وتطبيق ويب (React + TypeScript) لشركة شحن سريع، مبنية بنمط Clean Architecture.
واجهة المستخدم الأساسية عربية (RTL) مع دعم اختياري للإنجليزية.

> **الخطوة الحالية: الخطوة 8** — تطبيق الويب العربي (RTL) لكل الوحدات، لوحة المعلومات التشغيلية والمالية، شاشات التقارير مع Excel/PDF،
> طباعة المستندات، النشر بـ Docker (API + ويب + SQL Server + وكيل HTTPS اختياري)، النسخ الاحتياطي، المراقبة وخطة الجاهزية للإنتاج
> (فوق الخطوات 1-7: الهيكل، المصادقة والصلاحيات، البيانات الأساسية، الشحنات، التسعير والفواتير، التحصيل والنقدية، المحاسبة).

---

## 1. المتطلبات على Windows 11

| الأداة | ملاحظات |
|---|---|
| .NET 8 SDK | `dotnet --list-sdks` يجب أن يُظهر إصداراً يبدأ بـ `8.0` |
| SQL Server 2019/2022 (Express أو Developer) | للتشغيل المحلي بدون Docker |
| Docker Desktop (WSL 2) | للتشغيل عبر Docker Compose |
| Visual Studio 2022 17.8+ أو VS Code أو Rider | اختياري |

---

## 2. بنية الطبقات (Clean Architecture)

```
ShippingERP.API  ──►  ShippingERP.Application  ──►  ShippingERP.Domain
       │                        ▲
       └──►  ShippingERP.Infrastructure ──┘
```

| المشروع | المسؤولية | يعتمد على |
|---|---|---|
| **Domain** | الكيانات (Entities)، التعدادات (Enums)، قواعد العمل الأساسية | لا شيء |
| **Application** | DTOs، الخدمات (Use Cases)، Validators، Mapster، الواجهات (Interfaces) | Domain |
| **Infrastructure** | EF Core، AppDbContext، المستودعات، تنفيذ الواجهات | Application |
| **API** | Controllers، Middleware، Swagger، Serilog، تسجيل الخدمات | Application + Infrastructure |
| **Tests** | اختبارات xUnit واختبارات التكامل | كل المشاريع |

القاعدة الذهبية: **الـ Controllers لا تستخدم `AppDbContext` مباشرة ولا تُرجع كيانات EF Core** — فقط DTOs عبر خدمات Application.

---

## 3. الإعدادات (Configuration)

ترتيب الأولوية (الأخير يتغلب):
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User Secrets (في Development فقط)
4. **متغيرات البيئة** — مثلاً `ConnectionStrings__DefaultConnection`

| المفتاح | الوصف |
|---|---|
| `ConnectionStrings:DefaultConnection` | سلسلة الاتصال بـ SQL Server |
| `Database:ApplyMigrationsOnStartup` | تطبيق الـ migrations تلقائياً عند التشغيل (مفعّل في Docker فقط) |
| `Swagger:Enabled` | تفعيل Swagger خارج بيئة Development |
| `Cors:AllowedOrigins` | قائمة عناوين الواجهة الأمامية المسموح بها |
| `Hosting:UseHttpsRedirection` | إعادة التوجيه إلى HTTPS |

> إذا كان SQL Server مثبتاً كـ **Named Instance** (مثل SQL Express)، غيّر `Server=.` إلى `Server=.\SQLEXPRESS`.

---

## 4. الأوامر (Windows PowerShell)

> نفّذ جميع الأوامر من **المجلد الجذر للحل** (المجلد الذي يحتوي `ShippingERP.sln`).

### 4.1 الاستعادة والبناء والاختبار

```powershell
dotnet tool restore
dotnet restore
dotnet build
dotnet test
```

### 4.2 إنشاء Migration

```powershell
dotnet tool restore
dotnet ef migrations add InitialCreate `
  --project src/ShippingERP.Infrastructure `
  --startup-project src/ShippingERP.API `
  --output-dir Persistence/Migrations
```

> في هذه الخطوة لا توجد كيانات بعد، لذلك ستكون الـ migration الأولى فارغة تقريباً. هذا طبيعي: الهدف هو التأكد من أن أدوات EF Core تعمل.

### 4.3 تحديث قاعدة البيانات

```powershell
dotnet ef database update `
  --project src/ShippingERP.Infrastructure `
  --startup-project src/ShippingERP.API
```

لاستخدام سلسلة اتصال مختلفة مؤقتاً دون تعديل الملفات:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=.\SQLEXPRESS;Database=ShippingERP_Dev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet ef database update --project src/ShippingERP.Infrastructure --startup-project src/ShippingERP.API
Remove-Item Env:ConnectionStrings__DefaultConnection
```

### 4.4 التشغيل محلياً

```powershell
dotnet run --project src/ShippingERP.API --launch-profile http
```

- Swagger: <http://localhost:5080/swagger>
- Health: <http://localhost:5080/api/v1/system/health>

### 4.5 التشغيل عبر Docker Compose

```powershell
Copy-Item .env.example .env
notepad .env          # غيّر MSSQL_SA_PASSWORD إلى كلمة مرور قوية
docker compose up --build -d
docker compose ps
docker compose logs -f api
```

- Swagger: <http://localhost:5080/swagger>
- SQL Server من SSMS: الخادم `localhost,14333` — المستخدم `sa` — كلمة المرور من ملف `.env`

> **مهم:** أنشئ الـ migration (الخطوة 4.2) **قبل** `docker compose up --build`، لأن الحاوية تطبّق الـ migrations الموجودة داخل الكود.

الإيقاف:

```powershell
docker compose down        # يحافظ على البيانات
docker compose down -v     # يحذف قاعدة البيانات والسجلات نهائياً
```

### 4.6 عرض Swagger

افتح <http://localhost:5080/swagger> واختر **V1** من القائمة في الأعلى.

---

## 5. نقاط الصحة (Health Endpoints)

| المسار | الاستخدام | يفحص قاعدة البيانات؟ | عند الفشل |
|---|---|---|---|
| `GET /api/v1/system/health` | للواجهة الأمامية ولوحة التحكم | نعم | 200 مع `status: Unhealthy` |
| `GET /health/live` | هل العملية تعمل؟ | لا | — |
| `GET /health/ready` | هل التطبيق جاهز لاستقبال الطلبات؟ | نعم | 503 |

---

## 6. صيغة الاستجابة الموحدة

نجاح:

```json
{
  "success": true,
  "message": "النظام يعمل بشكل طبيعي",
  "data": { "status": "Healthy", "serverTimeUtc": "2026-10-07T08:00:00.000Z" },
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "timestampUtc": "2026-10-07T08:00:00.000Z"
}
```

خطأ:

```json
{
  "success": false,
  "message": "المسار المطلوب غير موجود.",
  "error": { "code": "NOT_FOUND", "message": "المسار المطلوب غير موجود." },
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "timestampUtc": "2026-10-07T08:00:00.000Z"
}
```

`traceId` يظهر أيضاً في ترويسة `X-Trace-Id` وفي كل سطر في السجلات (`logs/shippingerp-YYYYMMDD.log`).

| الاستثناء | HTTP | `error.code` |
|---|---|---|
| `RequestValidationException` | 400 | `VALIDATION_ERROR` |
| `NotFoundException` | 404 | `NOT_FOUND` |
| `ConflictException` | 409 | `CONFLICT` |
| `DbUpdateConcurrencyException` | 409 | `CONCURRENCY_CONFLICT` |
| `ForbiddenException` | 403 | `FORBIDDEN` |
| `BusinessRuleException` | 422 | `BUSINESS_RULE_VIOLATION` |
| أي استثناء آخر | 500 | `INTERNAL_SERVER_ERROR` |

---

## 7. تصميم الكيانات الأساسية (BaseEntity) والحذف المنطقي

| النوع | الحقول | يُستخدم لـ |
|---|---|---|
| `BaseEntity` | `Id`, `CreatedAtUtc`, `CreatedByUserId`, `UpdatedAtUtc`, `UpdatedByUserId`, `RowVersion` | كل الجداول |
| `SoftDeletableEntity` | + `IsActive`, `IsDeleted`, `DeletedAtUtc`, `DeletedByUserId` | البيانات المرجعية (الفروع، المدن، العملاء، المناطق، الأسعار، المركبات) |
| `IAppendOnlyEntity` | علامة: إدراج فقط، لا تعديل ولا حذف | السجلات المالية والتاريخية |

- **`IsActive`**: مفتاح عمل (الفرع موقوف). يبقى ظاهراً في القوائم والتقارير.
- **`IsDeleted`**: حذف منطقي (أُدخل بالخطأ). يُخفى تلقائياً بفلتر عام في EF Core.
- حقول التدقيق تُملأ تلقائياً عبر `AuditableEntityInterceptor` — لا تضبطها يدوياً.

### كيانات يجب ألا تستخدم الحذف المنطقي

| الكيان (لاحقاً) | السبب | البديل |
|---|---|---|
| قيود اليومية وسطورها | السجل المحاسبي يجب أن يكون غير قابل للتغيير | قيد عكسي (Reversing Entry) |
| معاملات تحصيل COD وتسوياتها | أموال نقدية حقيقية؛ أي حذف يكسر المطابقة | معاملة عكسية بسبب موثّق |
| سندات القبض والصرف بعد الترحيل | إثبات مالي | إلغاء عبر سند عكسي |
| الفواتير المصدرة | متطلبات قانونية وضريبية | حالة "ملغاة" + إشعار دائن |
| سجل حالات الشحنة (Tracking Events) | تاريخ الشحنة يجب أن يبقى كاملاً | إضافة حدث تصحيحي |
| سجل التدقيق (Audit Log) | يفقد قيمته إن أمكن حذفه | لا شيء — إدراج فقط |
| Refresh Tokens | بيانات أمنية مؤقتة | إبطال (Revoke) ثم حذف فعلي بمهمة تنظيف |
| حركات الخزنة والعهدة | يجب أن يساوي الرصيد مجموع الحركات | حركة عكسية |

---

## 8. ملاحظة مهمة للخطوات القادمة (المعاملات)

`EnableRetryOnFailure` مفعّل في SQL Server. لذلك أي معاملة يدوية (المحاسبة، COD) **يجب** أن تُكتب هكذا:

```csharp
var strategy = dbContext.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
    // ... إنشاء السجلات المرتبطة ...
    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
});
```

وإلا سيرمي EF Core استثناءً من نوع `InvalidOperationException`.

## 9. المصادقة والصلاحيات (الخطوة 2)

- ASP.NET Core Identity (المستخدمون والأدوار وتشفير كلمات المرور والقفل بعد 5 محاولات فاشلة لمدة 15 دقيقة).
- رمز دخول JWT صالح 15 دقيقة + رمز تحديث (Refresh Token) صالح 7 أيام، يُخزَّن في قاعدة البيانات كـ SHA-256 فقط، ويتغير عند كل استخدام.
- الصلاحيات معرفة في `src/ShippingERP.Domain/Authorization/Permissions.cs` والأدوار في `Roles.cs`، وتُستخدم في الكود هكذا: `[HasPermission("Shipments.Create")]`.
- سجل تدقيق `AuditLogs` لتسجيل الدخول والمحاولات الفاشلة وإلغاء الرموز وتغيير كلمة المرور وتعديل المستخدمين والأدوار.

### 9.1 الأسرار (لا تُكتب في الكود أبداً)

| المتغير | الوصف |
|---|---|
| `Jwt__SecretKey` | مفتاح توقيع JWT (32 حرفاً عشوائياً على الأقل). بدونه لا يبدأ التطبيق. |
| `Seed__AdminUserName` | اسم مستخدم أول مدير نظام (يُستخدم مرة واحدة فقط). |
| `Seed__AdminEmail` | بريد أول مدير نظام. |
| `Seed__AdminPassword` | كلمة مرور أول مدير نظام. احذفها من البيئة بعد أول تشغيل ناجح. |

في Docker Compose توضع القيم في ملف `.env` (انظر `.env.example`).

### 9.2 نقاط النهاية

| الطريقة | المسار | الصلاحية |
|---|---|---|
| POST | `/api/v1/auth/login` | بدون |
| POST | `/api/v1/auth/refresh-token` | بدون |
| POST | `/api/v1/auth/revoke-token` | بدون |
| POST | `/api/v1/auth/change-password` | مستخدم مسجل |
| GET | `/api/v1/auth/me` | مستخدم مسجل |
| GET | `/api/v1/users` , `/api/v1/users/{id}` | `Users.View` |
| POST, PUT | `/api/v1/users` , `/api/v1/users/{id}` | `Users.Manage` |
| PATCH | `/api/v1/users/{id}/activate` , `/deactivate` | `Users.Manage` |
| GET | `/api/v1/roles` , `/api/v1/roles/{id}` | `Roles.Manage` أو `Users.View` |
| GET | `/api/v1/roles/permissions` | `Roles.Manage` |
| POST | `/api/v1/roles` | `Roles.Manage` |
| PUT | `/api/v1/roles/{id}/permissions` | `Roles.Manage` |

## 10. البيانات الأساسية وإدارة العملاء (الخطوة 3)

- 20 جدولاً: بيانات الشركة، الفروع، المستودعات، الدول، المدن، المناطق، أنواع الخدمة، طرق الدفع، العملات، أسعار الصرف، الضرائب، بنود المصروفات، الموردون، المركبات، العملاء، عناوين العملاء، خطط التسعير وقواعدها، الموظفون، ملفات السائقين.
- كل قائمة تدعم: `pageNumber`، `pageSize`، `search`، `sortBy`، `sortDirection`، `isActive` وفلاتر خاصة بكل جدول.
- كل جدول يدعم: إضافة، تعديل، تفعيل، إيقاف، وحذف منطقي فقط (يُرفض الحذف بـ 422 إذا كان السجل مستخدماً).
- من لا يملك `Branches.ViewAll` يرى ويدير بيانات فرعه فقط (الفروع، المستودعات، العملاء، الموظفون، السائقون، المركبات).
- بيانات تجريبية تُنشأ تلقائياً في بيئة التطوير عندما لا يوجد أي فرع (`Seed:DemoData = true`).
- كل إضافة وتعديل وتفعيل وإيقاف وحذف تُسجَّل في `AuditLogs`.

## 11. الشحنات والعمليات (الخطوة 4)

- **الشحنة**: رقم داخلي `SHP-2026-000001` (عدّاد سنوي ذري في SQL Server) ورقم تتبع عشوائي برقم تحقق `SE48213907651`.
- **دورة الحياة**: `Created → PickedUp → ReceivedAtBranch → InWarehouse → InTransit → ArrivedAtDestination → OutForDelivery → Delivered`،
  مع `DeliveryFailed` و `Returned` و `Cancelled` و `LostOrDamaged`. كل تغيير يمر عبر `ShipmentStatusRules` ويُكتب في `ShipmentStatusHistory` (إدراج فقط).
- الحالات الخاصة لها مسارها فقط: `InTransit` بالتحويلات، `OutForDelivery` ببيانات التوزيع، `Delivered` بإثبات التسليم،
  `Returned` بالمرتجعات، `LostOrDamaged` بحالات التلف. الخروج من `Delivered` فقط بمرتجع «بعد التسليم» أو تصحيح حالة (`Shipments.CorrectStatus`).
- **الوزن**: حجمي = الطول × العرض × الارتفاع ÷ المعامل (افتراضياً 5000)، والمحتسب = الأكبر مقرباً للأعلى (0.5 كغ). الإعدادات في `/api/v1/shipping-settings`.
- **السعر**: من خطة تسعير المرسل (أو الافتراضية) وتُحفظ لقطة سعر غير قابلة للتعديل `ShipmentPricingSnapshots`. السعر اليدوي والخصم بصلاحية `Shipments.OverridePrice`.
- **المستندات**: بيان حركة مستودع `WHM-`، تحويل بين الفروع `BTR-`، بيان توزيع للسائق `DDM-`، مرتجع `RET-`، حالة تلف/فقدان `DLC-`.
- **السائق** (دور Driver بدون `Shipments.AssignDriver`) يرى شحناته وبيانات توزيعه فقط.
- **الملفات**: JPEG/PNG/WEBP/PDF حتى 5MB، يُفحص المحتوى الحقيقي، وتُحفظ بأسماء مولّدة خارج wwwroot (`FileStorage:RootPath`)، وتُنزّل عبر الـ API فقط.
- **الملصقات**: PDF (QuestPDF) أو HTML قابل للطباعة، 100×150 مم افتراضياً، Code128 + QR، صفحة لكل طرد.
- **الاستيراد من Excel**: قالب ← معاينة بأخطاء كل صف ← استيراد كامل في معاملة واحدة (كل الصفوف أو لا شيء).

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `/api/v1/shipments` , `/{id}` , `/track/{code}` , `/{id}/history` , `/{id}/pricing` | `Shipments.View` |
| POST | `/api/v1/shipments` | `Shipments.Create` |
| POST | `/api/v1/shipments/quote` | `Shipments.Create` أو `Shipments.Update` |
| PUT | `/api/v1/shipments/{id}` | `Shipments.Update` |
| PATCH | `/api/v1/shipments/{id}/status` | `Shipments.ChangeStatus` |
| POST | `/api/v1/shipments/{id}/cancel` | `Shipments.Cancel` |
| PATCH | `/api/v1/shipments/{id}/driver` | `Shipments.AssignDriver` |
| POST | `/api/v1/shipments/{id}/status-corrections` | `Shipments.CorrectStatus` |
| POST | `/api/v1/shipments/{id}/deliver` , `/delivery-failed` | `Shipments.ConfirmDelivery` |
| GET | `/api/v1/shipments/{id}/label?format=Pdf` , POST `/api/v1/shipments/labels` | `Shipments.View` |
| GET, POST, DELETE | `/api/v1/shipments/{id}/attachments` | `Shipments.View` / `Shipments.Update` |
| GET, POST | `/api/v1/shipments/import/template` , `/preview` , `/import` | `Shipments.Import` |
| * | `/api/v1/warehouse-manifests` | `Warehouse.ManageMovements` |
| GET / * | `/api/v1/branch-transfers` | `Transfers.View` / `Transfers.Manage` / `Transfers.Receive` |
| GET / * | `/api/v1/driver-manifests` | `Shipments.View` / `DeliveryManifests.Manage` |
| GET / * | `/api/v1/shipment-returns` | `Shipments.View` / `Returns.Manage` |
| GET / * | `/api/v1/damage-cases` | `Shipments.View` / `DamageCases.Manage` |
| GET / PUT | `/api/v1/shipping-settings` | مستخدم مسجل / `MasterData.Manage` |

المكتبات الجديدة: ClosedXML 0.104.2 (MIT)، QuestPDF 2025.12.4 (رخصة Community مجانية للشركات بإيراد أقل من مليون دولار سنوياً)، ZXing.Net 0.16.9 (Apache-2.0)، وخط Cairo (OFL) مضمّن للعربية.

## 12. التسعير والفواتير والذمم المدينة (الخطوة 5)

- **محرك التسعير** (`PriceRule` + `PriceRuleCondition` + `PriceRuleWeightTier`): شروط (مناطق ومدن الانطلاق والوصول، نوع الخدمة،
  مجال الوزن المحتسب، عدد الطرود)، سعر أساسي يشمل وزناً محدداً، شرائح وزن تصاعدية، رسم الطرد الإضافي، عمولة COD (ثابت + نسبة + حد أدنى)،
  رسم التأمين (نسبة + حد أدنى)، الاستلام من المرسل، التغليف، المناطق النائية (`Zone.IsRemoteArea`)، الإرجاع، خصم العقد، ونسبة ضريبة لكل قاعدة.
- **ترتيب الخطط**: عقد العميل (`ContractCustomerId`) ← الخطة المسندة للعميل ← الخطة الافتراضية. أول خطة فيها قاعدة مطابقة هي التي تسعّر.
- **التفصيل**: السعر الأساسي + الوزن + رسوم الخدمات − الخصم = إجمالي الشحن؛ + عمولة COD = الخاضع للضريبة؛ + الضريبة = الصافي.
  يُحفظ كاملاً في `ShipmentPricingSnapshots` (لقطة لا تتغير).
- **السعر اليدوي**: صلاحية `Shipments.OverridePrice` + سبب إلزامي. إذا زاد الفرق عن الحد (`PriceOverrideApprovalThresholdPercent`، افتراضياً 20%)
  يحتاج موافقة مستخدم آخر يملك `Pricing.ApproveOverride` (`ShipmentPriceOverrides`).
- **الفاتورة** `INV-2026-000001`: مسودة بلا رقم ← إصدار (رقم آمن، تاريخ استحقاق، قيد مدين في حساب العميل) ← دفعات / إشعارات دائنة.
  الحالات: `Draft, Issued, PartiallyPaid, Paid, Overdue, Cancelled, Credited`. الفاتورة الصادرة لا تُعدّل أبداً.
- **منع الفوترة المزدوجة**: فهرس فريد `UX_InvoiceLines_ActiveCharge` على (الشحنة، نوع الرسم) للسطور الفعّالة + فحص في الخدمة (409).
- **الإشعار الدائن** `CRN-`: لسطر أو أكثر أو للفاتورة كاملة، مع عكس الضريبة بنسبة السطر. الزائد عن رصيد الفاتورة يبقى رصيداً دائناً للعميل.
- **الدفعات** `PAY-`: نقداً / تحويل / بطاقة، تخصيص على فاتورة أو أكثر أو تلقائياً (الأقدم استحقاقاً أولاً)، دفعات جزئية،
  ومنع التكرار بالترويسة `Idempotency-Key`.
- **دفتر حساب العميل** `CustomerAccountTransactions` (إدراج فقط) ← كشف الحساب برصيد تراكمي، وتقرير أعمار الذمم (حالي، 1-30، 31-60، 61-90، +90).
- **التكامل المحاسبي**: أحداث `InvoiceIssued`, `InvoiceCancelled`, `CreditNoteIssued`, `CustomerPaymentReceived`, `CustomerPaymentAllocated`
  تُحفظ في `OutboxMessages` في المعاملة نفسها، والواجهة `IAccountingIntegrationHandler` جاهزة للمرحلة القادمة.
- **مهمة خلفية** كل ساعة تجعل الفواتير المتأخرة `Overdue` (القسم `Billing` في appsettings.json).

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET, POST, PUT, PATCH, DELETE | `/api/v1/pricing-plans` , `/api/v1/pricing-plans/{planId}/rules` | `Pricing.View` / `Pricing.Manage` |
| POST | `/api/v1/shipments/{id}/calculate-price` | `Shipments.View` |
| POST | `/api/v1/shipments/{id}/apply-price` (200 طُبّق، 202 بانتظار الموافقة) | `Shipments.Update` (+ `Shipments.OverridePrice` لليدوي) |
| GET / POST | `/api/v1/price-overrides` , `/{id}/approve` , `/{id}/reject` | `Pricing.View` / `Pricing.ApproveOverride` |
| GET | `/api/v1/invoices` , `/{id}` , `/credit-notes/{id}` | `Invoices.View` |
| GET | `/api/v1/invoices/eligible-shipments` | `Invoices.Create` |
| POST | `/api/v1/invoices/from-shipment/{shipmentId}` , `/consolidated` ; PUT `/{id}` (مسودة) | `Invoices.Create` |
| POST | `/api/v1/invoices/{id}/issue` | `Invoices.Issue` |
| POST | `/api/v1/invoices/{id}/cancel` | `Invoices.Cancel` |
| POST | `/api/v1/invoices/{id}/credit-notes` | `Invoices.CreditNotes` |
| GET | `/api/v1/invoices/{id}/pdf` , `/api/v1/invoices/export` | `Invoices.Export` |
| GET / POST | `/api/v1/customer-payments` , `/{id}/allocations` | `Payments.View` / `Payments.Create` |
| GET | `/api/v1/customers/{id}/statement` , `/statement/export` | `Receivables.View` |
| GET | `/api/v1/reports/customer-aging` | `Reports.ViewFinancial` |

---

## 13. الدفع عند الاستلام والنقدية (الخطوة 6)

- **مبلغ التحصيل ليس إيراداً**: هو أمانة للمرسل. الإيراد هو فقط ما يُخصم منه (أجور الشحن، عمولة التحصيل، الضريبة، الرسوم المعتمدة) عند دفع التسوية.
- **`CODTransaction`** سجل واحد لكل شحنة دفع عند الاستلام، يُنشأ ويُحدَّث **تلقائياً** مع الشحنة (`CodShipmentSyncInterceptor`) ما دام المبلغ لم يُحصّل،
  ثم لا تغيّره إلا خدمات التحصيل. كل تغيير يضيف سطراً ثابتاً في `CODTransactionHistory`.
- **الحالات**: `Pending → AssignedToDriver → CollectedByDriver → ReceivedAtBranchCashbox → ReadyForSenderSettlement → PartiallySettled / SettledToSender`،
  و`Cancelled` و`Disputed`. القواعد في `Domain/Cod/CodStatusRules`.
- **عهدة السائق** `DriverCashAccount` + `DriverCashMovement` (إدراج فقط): التحصيل يزيدها، التسليم ينقصها. لا يُسلَّم أكثر من الرصيد إلا بتسوية «زيادة» معتمدة.
- **الصناديق** `Cashbox` (عدة صناديق لكل فرع وعملة) + `CashboxTransaction` (إدراج فقط، رصيد بعد كل حركة) + `CashboxDailyClosing` (إقفال يومي؛ الفرق يحتاج سبباً وموافقة).
  الرصيد لا يصبح سالباً أبداً (فحص في الخدمة + قيد CHECK في قاعدة البيانات)، واليوم المقفل لا يقبل حركات.
- **السندات**: قبض `RCV-` (ومنها إيصال استلام عهدة السائق والتحصيل في الفرع) وصرف `PMV-`، مع المبلغ كتابةً، وإلغاء بقيد عكسي فقط.
- **التحويلات** `CTR-`: بين صناديق الفرع، بين الفروع (قيد النقل حتى الاستلام)، وإلى البنك (برقم إشعار الإيداع)، باعتماد مستخدم آخر.
- **تسويات المرسلين** `SST-`: مسودة ← اعتماد (مستخدم آخر) ← دفع نقداً أو بنكياً، مع التسوية الجزئية والعكس. فهرس فريد يمنع حجز المبلغ نفسه في تسويتين.
- **التسويات والنزاعات** `CAD-`: شطب عجز، زيادة نقدية، تصحيح أو إلغاء تحصيل، رسم إضافي أو تعويض للمرسل؛ بسبب إلزامي واعتماد مستخدم آخر.
- **قاعدة «العينين»**: كل اعتماد يجب أن يكون من غير مقدّم الطلب. `Cash:AllowSelfApproval = true` في `appsettings.Development.json` فقط للتجربة بمستخدم واحد.
- **اليوم المحاسبي**: `Cash:BusinessTimeZoneId` (افتراضياً `Asia/Damascus`) يحدد إلى أي يوم تنتمي الحركة؛ كل التواريخ مخزنة UTC.
- **الطباعة (PDF)**: سند القبض، سند الصرف، إيصال استلام عهدة السائق (مع الشحنات والعجز)، كشف تسوية المرسل.

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `/api/v1/cod` , `/{id}` , `/by-shipment/{shipmentId}` | `COD.View` |
| POST | `/api/v1/cod/collect` | `COD.Collect` (+ `COD.Handover` للتحصيل في الفرع) |
| POST | `/api/v1/cod/driver-handover` (`Idempotency-Key`) | `COD.Handover` |
| POST | `/api/v1/cod/mark-ready` | `COD.Settle` |
| POST | `/api/v1/cod/{id}/dispute` ; `/{id}/resolve-dispute` | `COD.Adjust` ; `COD.ApproveAdjustment` |
| GET | `/api/v1/cod/driver-balances` , `/driver-balances/{driverId}/movements` , `/sender-balances` , `/aging` , `/dashboard` | `COD.View` |
| GET / POST | `/api/v1/cod/settlements` , `/candidates` , `/{id}/pdf` ; POST `/` , `/{id}/cancel` | `COD.View` ; `COD.Settle` |
| POST | `/api/v1/cod/settlements/{id}/approve` , `/{id}/reverse` ; `/{id}/pay` | `COD.ApproveSettlement` ; `COD.PaySettlement` |
| GET / POST | `/api/v1/cod/adjustments` ; `/{id}/approve` , `/{id}/reject` | `COD.Adjust` ; `COD.ApproveAdjustment` |
| GET / POST / PUT | `/api/v1/cashboxes` , `/{id}/transactions` | `Cashboxes.View` / `Cashboxes.Manage` |
| POST / GET | `/api/v1/cashboxes/{id}/daily-closings` , `/cashboxes/daily-closings` , `/daily-closings/{id}/approve` , `/reject` | `Cashboxes.Close` / `Cashboxes.ApproveClosing` |
| GET / POST / PUT | `/api/v1/bank-accounts` | `Cashboxes.View` / `Cashboxes.Manage` |
| GET / POST | `/api/v1/cash-receipts` , `/cash-payments` (`Idempotency-Key`) , `/{id}/cancel` , `/{id}/pdf` | `CashVouchers.Create` / `CashVouchers.Cancel` |
| GET / POST | `/api/v1/cash-transfers` , `/{id}/approve` , `/reject` , `/cancel` , `/receive` | `CashTransfers.Create` / `Approve` / `Receive` |
| GET | `/api/v1/reports/daily-cash-movement` | `Cashboxes.View` |
| GET | `/api/v1/reports/driver-collections` , `/reports/sender-settlements` | `COD.View` |

---

## 14. المحاسبة بالقيد المزدوج (الخطوة 7)

- **دليل الحسابات** `Account` شجرة بستة أنواع (أصول، خصوم، حقوق ملكية، إيرادات، تكلفة إيرادات، مصاريف). الحساب الرئيسي لا يقبل القيود؛
  الحسابات الرقابية (الصناديق، الذمم المدينة، عهدة السائقين، أمانات المرسلين، نقدية العملاء قيد الإيداع) تُرحَّل آلياً فقط.
  الأرصدة لا تُخزَّن: هي مجموع سطور القيود المرحّلة.
- **السنة المالية والفترات** `FiscalYear` + `AccountingPeriod` (شهرية). لا ترحيل في فترة مقفلة. الإقفال يُرفض ما دام في الفترة قيد غير مرحّل
  أو عملية تشغيلية لم تُرحَّل أو ترحيل آلي فاشل. صف الفترة يُقفل (`HOLDLOCK`) أثناء كل ترحيل فلا يمر قيد أثناء الإقفال.
- **قيود اليومية** `JournalEntry` + `JournalEntryLine` (+ `JournalEntryDimension` لمراكز التكلفة): `Draft → Submitted → Approved → Posted → Reversed`، و`Rejected`.
  القيد المرحّل متوازن دائماً (مدين = دائن، بعملته وبالعملة الأساسية) ولا يُعدَّل ولا يُحذف: ثلاث طبقات حماية (الخدمة، `JournalIntegrityInterceptor`،
  قيود CHECK ومشغّلات SQL Server). التصحيح بقيد عكسي (`ReversalJournalLink`) أو قيد تسوية.
- **الترحيل الآلي**: `AccountingPostingBackgroundService` يقرأ `OutboxMessages` كل 10 ثوانٍ ويحوّل كل حدث إلى قيد مرحّل في معاملة واحدة.
  الأدوار المحاسبية (`CashOnHand`، `ShippingRevenue`...) تُربط بالحسابات عبر `AccountingPostingRule` (القاعدة الأكثر تحديداً تفوز).
  **لا تكرار**: `AccountingPostingEvents.EventId` فريد، و`JournalEntries.SourceKey` فريد لكل مستند.
- **التسوية البنكية** `BankTransaction` + `BankReconciliation` + `BankReconciliationLine`: مطابقة تلقائية، حركات البنك فقط وحركات الدفاتر فقط، والإكمال عند فرق صفر.
- **التقارير**: دليل الحسابات، اليومية، الأستاذ العام، ميزان المراجعة، قائمة الدخل، الميزانية العمومية، التدفقات النقدية، أعمار العملاء والموردين،
  أرصدة الصناديق والبنوك، الإيراد حسب الفرع، المصروفات حسب الفئة، عهد السائقين، أمانات المرسلين، ومطابقة الحسابات الرقابية مع الدفاتر المساعدة.
- **قاعدة العينين**: `Accounting:AllowSelfApproval = true` في `appsettings.Development.json` فقط.

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET / POST / PUT / DELETE | `/api/v1/accounting/accounts` , `/accounts/tree` , `/account-categories` , `/dimensions` | `Accounting.View` / `Accounting.ManageChart` |
| GET / POST / PUT / DELETE | `/api/v1/accounting/fiscal-years` , `/{id}/close` | `Accounting.View` / `Accounting.ManagePeriods` |
| GET / PUT / POST | `/api/v1/accounting/periods` , `/{id}/close-check` , `/{id}/close` , `/{id}/reopen` | `Accounting.View` / `Accounting.ManagePeriods` |
| GET / POST / PUT / DELETE | `/api/v1/accounting/journal-entries` (`Idempotency-Key`) , `/{id}/submit` | `Accounting.CreateJournal` |
| POST | `/api/v1/accounting/journal-entries/{id}/approve` , `/reject` ; `/post` ; `/reverse` | `ApproveJournal` ; `PostJournal` ; `ReverseJournal` |
| GET / POST / PUT / DELETE | `/api/v1/accounting/posting-rules` , `/roles` , `/resolve` | `Accounting.View` / `Accounting.ManageChart` |
| GET / POST | `/api/v1/accounting/posting-events` , `/summary` , `/process` , `/{id}/retry` | `Accounting.View` / `Accounting.ManagePostings` |
| GET / POST | `/api/v1/accounting/bank-transactions` , `/bank-reconciliation` , `/{id}/complete` | `Accounting.BankReconciliation` |
| GET | `/api/v1/accounting/general-ledger` , `/trial-balance` , `/profit-loss` , `/balance-sheet` , `/cash-flow` , `/reports/*` | `Accounting.View` + `Reports.ViewFinancial` |
| DELETE | `/api/v1/bank-accounts/{id}` | `Cashboxes.Manage` |

---

## 15. تطبيق الويب والتقارير والنشر (الخطوة 8)

### تشغيل التطوير (Windows 11)

```powershell
docker compose up -d sqlserver                                        # أو SQL Server محلي
dotnet run --project src/ShippingERP.API --launch-profile http        # http://localhost:5080
cd frontend; npm ci; npm run dev                                      # http://localhost:5173 (يمرّر /api إلى 5080)
```

أوامر الويب: `npm run lint` · `npm run test` · `npm run build` · `npm run gen:api` (توليد الأنواع من DTOs).

### التشغيل الكامل بـ Docker

```powershell
Copy-Item .env.example .env          # عدّل كل القيم
docker compose up --build -d         # http://localhost:8080
docker compose --profile proxy up -d    # HTTPS تلقائي على SITE_DOMAIN (Caddy)
docker compose --profile backup up -d   # نسخ احتياطي يومي
```

### ما أُضيف في الخادم

- **جلسة المتصفح الآمنة**: `X-Auth-Mode: cookie` على `/auth/login` و`/auth/change-password` يضع توكن التجديد في كوكي `HttpOnly; Secure; SameSite=Strict`
  محصور بالمسار `/api/v1/auth/web`، والتوكن لا يظهر في الجسم. `POST /auth/web/refresh` و`POST /auth/web/logout`. الإعداد `Jwt:RefreshCookieSecure`.
- **لوحة المعلومات** `GET /api/v1/dashboard?days=30&branchId=`: الشحنات والحالات ونسب التسليم والإخفاق، التحصيل بمراحله، أرصدة الصناديق، الإيراد والمصروف وصافي الربح،
  الفواتير المتأخرة، عهد السائقين، أكثر العملاء، آخر الحركات. كل قسم يظهر فقط لمن يملك صلاحيته.
- **التقرير التشغيلي** `GET /api/v1/reports/operations` (+ `/export`): حسب الحالة والفرع والسائق والعميل والخدمة ويومياً، مع مرشحات التاريخ والفرع والعميل والسائق والحالة.
- **التصدير** `?format=excel|pdf` (Excel بـ ClosedXML باتجاه RTL، وPDF بـ QuestPDF بخط Cairo العربي ورأس الشركة): الشحنات، التقرير التشغيلي، ميزان المراجعة،
  الأستاذ العام، قائمة الدخل، الميزانية؛ و`GET /driver-manifests/{id}/pdf` لكشف التوزيع مع التواقيع (الحد 10000 صف).
- **سجل التدقيق** `GET /api/v1/audit-logs` (صلاحية جديدة `AuditLogs.View`، تُمنح تلقائياً للمدير والمدير المالي عند بدء التشغيل؛ لا ترحيل قاعدة بيانات في هذه الخطوة).
- **خلف وكيل عكسي**: `Hosting:UseForwardedHeaders` (مفعّل في Docker) لعنوان العميل الحقيقي وhttps.

| الطريقة | المسار | الصلاحية |
|---|---|---|
| POST | `/api/v1/auth/web/refresh` , `/auth/web/logout` (+ `X-Auth-Mode: cookie`) | كوكي التجديد |
| GET | `/api/v1/dashboard` | أي مستخدم (الأقسام حسب الصلاحيات) |
| GET | `/api/v1/reports/operations` , `/export` | `Reports.ViewOperational` + `Shipments.View` |
| GET | `/api/v1/shipments/export` | `Shipments.View` |
| GET | `/api/v1/accounting/{trial-balance|general-ledger|profit-loss|balance-sheet}/export` | `Accounting.View` + `Reports.ViewFinancial` |
| GET | `/api/v1/driver-manifests/{id}/pdf` | `Shipments.View` |
| GET | `/api/v1/audit-logs` | `AuditLogs.View` |

### المستندات المطبوعة

| المستند | المصدر |
|---|---|
| ملصق الشحنة الحراري (100×150 مم، باركود + QR) PDF/HTML | `/shipments/{id}/label` و`/shipments/labels` (حتى 100) |
| الفاتورة PDF | `/invoices/{id}/pdf` |
| كشف التوزيع PDF | `/driver-manifests/{id}/pdf` |
| سند القبض / سند الصرف PDF (المبلغ كتابةً) | `/cash-receipts/{id}/pdf` , `/cash-payments/{id}/pdf` |
| كشف تسوية المرسل PDF | `/cod/settlements/{id}/pdf` |
| ميزان المراجعة / الأستاذ العام / الدخل / الميزانية PDF/Excel | `/accounting/*/export` |

### الوثائق

- `docs/frontend/FRONTEND.md` — بنية التطبيق، طبقة الـ API، تدفق المصادقة، حماية المسارات، دليل تصميم RTL.
- `docs/operations/DEPLOYMENT.md` — Windows 11، Docker، HTTPS، الترحيلات، التحديث، السجلات.
- `docs/operations/BACKUP-RESTORE.md` — الجدولة والاحتفاظ والاستعادة واختبارها (`scripts/backup-now.ps1`, `scripts/restore.ps1`).
- `docs/operations/MONITORING.md` — نقاط الصحة والمراقبة والتنبيهات.
- `docs/operations/SECURITY-CHECKLIST.md` — قائمة التحقق قبل الإنتاج.
- `docs/operations/GIT-BRANCHING.md` — الفروع وخط CI في `.github/workflows/ci.yml`.

