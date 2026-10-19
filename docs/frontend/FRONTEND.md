# تطبيق الويب (React + TypeScript) — البنية والتصميم

## 1. التقنيات (إصدارات مثبّتة بدقة في package.json)

React 18.3، ‏TypeScript 5.6 (strict)، ‏Vite 5.4، ‏React Router 6.30، ‏TanStack Query 5، ‏Axios 1.x، ‏MUI 6.5 + ‏MUI X DataGrid 7 (RTL)، ‏Emotion + ‏stylis-plugin-rtl، ‏react-hook-form + ‏zod، ‏i18next، ‏Recharts 3، ‏dayjs، خط Cairo (‏@fontsource، بلا CDN)، ‏Vitest، ‏ESLint 10.

## 2. بنية المجلدات

```
frontend/
├── Dockerfile, nginx/default.conf.template     ← بناء وخدمة الإنتاج
├── scripts/generate-api-types.py               ← يولّد أنواع TypeScript من DTOs الخادم (npm run gen:api)
├── vite.config.ts                              ← وكيل /api إلى الـ API في التطوير + تقسيم الحزم
└── src/
    ├── main.tsx                                ← نقطة الدخول (الخط، i18n، App)
    ├── app/        App, AppProviders (Query/Theme/RTL/Router/Auth), routes (حماية المسارات), navigation (القائمة)
    ├── api/        http.ts (Axios + التجديد), client.ts (get/list/post/download/export), errors.ts (AppError),
    │               tokenStore.ts (التوكن في الذاكرة), types.ts (الغلاف والصفحات), generated.ts (من DTOs),
    │               endpoints/shipments.ts (مثال وحدة منمّطة)
    ├── auth/       AuthProvider (تسجيل الدخول/الخروج/الجلسة الصامتة), guards (RequireAuth, RequirePermission, Can),
    │               permissions.generated.ts (رموز الصلاحيات من Permissions.cs)
    ├── components/
    │   ├── layout/   AppLayout (الشريط العلوي + القائمة الجانبية المتجاوبة)
    │   ├── common/   PageHeader, StatusChip, StatCard, Section, InfoGrid, Dialogs, ExportMenu/PrintButton, Notifier, ErrorAlert
    │   ├── data/     DataTable (DataGrid بترقيم وفرز من الخادم), FilterBar, useListState (الحالة في الرابط),
    │   │             useApiList, columns, ResourcePage (شاشة CRUD عامة)
    │   ├── form/     fields (حقول من تعريفات), FormDialog, LookupSelect (بحث خادمي), ScanBox (قارئ الباركود), SignaturePad
    │   └── charts/   TrendChart, CategoryBarChart
    ├── features/   شاشة لكل وحدة: dashboard, shipments, operations (تحويلات/كشوف/سائق/مرتجعات), customers,
    │               masterData, pricing, billing, cod, cash, accounting, reports, admin, auth, errors
    ├── i18n/       ar.ts (الافتراضي), en.ts (نفس المفاتيح إلزامياً بفحص TypeScript), enums.ts (أسماء حالات الـ API)
    ├── theme/      theme.ts (MUI + RTL + Cairo), palette.ts (ألوان الرسوم والحالات)
    └── utils/      format.ts (المبالغ والتواريخ بتوقيت العمل)
```

## 3. طبقة الاتصال بالـ API

- `http.ts`: مثيل Axios واحد، ‏`baseURL=/api/v1`، ‏`withCredentials`، مُسلسِل معاملات يرسل المصفوفات `ids=1&ids=2` ويحذف القيم الفارغة.
  - طلب: يجدد التوكن **قبل** انتهائه بـ 30 ثانية، ثم يضع `Authorization: Bearer` و`Accept-Language`.
  - استجابة 401: تجديد واحد مشترك (single-flight) ثم إعادة الطلب مرة واحدة؛ إن فشل ⇒ انتهاء الجلسة وصفحة الدخول.
- `client.ts`: ‏`api.get/list/post/put/patch/delete/postForm` تُرجع `data` من الغلاف الموحد وترمي `AppError`؛ ‏`api.download/export` للملفات (PDF/Excel/HTML)، ‏`openFile` للطباعة و`saveFile` للتنزيل، ‏`newIdempotencyKey` لعمليات المال.
- `errors.ts`: ‏`AppError(status, code, message, fieldErrors, traceId)`؛ أخطاء الحقول (camelCase) تظهر تحت حقولها، و`traceId` يظهر للمستخدم ليبلغ به الدعم.
- أنواع البيانات مولّدة من C#: ‏`python scripts/generate-api-types.py` بعد أي تغيير في DTOs.
- TanStack Query: مفتاح كل قائمة `[url, 'list', params]`؛ بعد أي تعديل `invalidateQueries({ queryKey: [url] })`. لا إعادة محاولة لأخطاء 4xx.

## 4. تدفق المصادقة (آمن ضد سرقة التوكن)

```
تسجيل الدخول:  POST /auth/login  + X-Auth-Mode: cookie
               ← accessToken (يُحفظ في الذاكرة فقط)
               ← Set-Cookie: erp_refresh=…; HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth/web
كل طلب:        Authorization: Bearer <accessToken>
قبل الانتهاء/401: POST /auth/web/refresh (الكوكي يُرسل تلقائياً) ← توكن جديد + تدوير الكوكي
تحديث الصفحة:   لا توكن في الذاكرة ← refresh صامت ← GET /auth/me (المستخدم والأدوار والصلاحيات)
الخروج:        POST /auth/web/logout ← إبطال التوكن في القاعدة وحذف الكوكي ومسح ذاكرة الاستعلامات
```

لماذا؟ لا شيء قابل للسرقة في `localStorage`؛ كوكي التجديد لا يقرؤه JavaScript (‏XSS)، ولا يُرسل من مواقع أخرى (‏SameSite=Strict ضد CSRF)، ومساره محصور بنقطتي التجديد والخروج. إعادة استخدام توكن تجديد قديم تُبطل كل جلسات المستخدم (من الخطوة 2).

## 5. حماية المسارات بالأدوار والصلاحيات

- `RequireAuth`: غير المسجّل ⇒ ‏`/login?returnTo=…` (مسارات نسبية فقط، لا إعادة توجيه مفتوحة).
- `RequirePermission any={[…]} all={[…]}`: نفس صلاحيات نقاط الـ API التي تستدعيها الصفحة؛ بدونها صفحة 403 داخل الإطار.
- `<Can permission="…">` يخفي الأزرار، والقائمة الجانبية تُخفي ما لا يملكه المستخدم.
- **الخادم هو المرجع**: الإخفاء للراحة فقط، وكل طلب يُفحص في الـ API.

## 6. نظام التصميم العربي (RTL)

- `dir="rtl"` و`lang="ar"` على `<html>` (يتبدلان مع اللغة)، و`theme.direction`، و`stylis-plugin-rtl` يقلب الهوامش والحدود تلقائياً. اكتب الأنماط بالاتجاه المنطقي (`marginInlineStart`, `paddingInlineStart`, `insetInlineStart`).
- **لا تكتب `direction:` في `sx`** (الإضافة تقلبها)؛ استخدم السمة `dir="ltr"` للعناصر اللاتينية: أرقام التتبع، الهواتف، البريد، الرسوم البيانية.
- الأرقام لاتينية (0-9) بفاصل آلاف وخانتين عشريتين، بخط Cairo ومحاذاة `tabular-nums` في الجداول.
- التواريخ: الخادم UTC؛ العرض بتوقيت العمل `Asia/Damascus` بصيغة `YYYY-MM-DD HH:mm`؛ مرشحات الأيام تتحول إلى بداية/نهاية يوم العمل بـ UTC.
- الأيقونات الاتجاهية (رجوع، توسيع) تُعكس حسب اللغة؛ الأيقونات غير الاتجاهية لا تُعكس.
- الكثافة: حقول `small`، جداول `compact`، زوايا 8px، بلا ظلال؛ ألوان الحالات محجوزة (أخضر نجاح، كهرماني تحذير، برتقالي خطير، أحمر حرج) **ومعها دائماً أيقونة ونص**.
- الرسوم: لوحة ألوان فئوية ثابتة الترتيب (مجرّبة لعمى الألوان)، محور واحد، وسيلة إيضاح عند سلسلتين فأكثر، تلميح عند المرور، المحور الزمني معكوس في العربية.
- الوضع الداكن واللغة الإنجليزية من الشريط العلوي (محفوظان في المتصفح).
- الشاشات متجاوبة: القائمة درج على الهاتف، وشاشة السائق مصممة للهاتف أولاً (اتصال، خريطة، تسليم بتوقيع وصورة وموقع).

## 7. إضافة شاشة جديدة (النمط المتبع)

1. أنواع: ‏`npm run gen:api` بعد إضافة DTO في الخادم.
2. قائمة + نموذج بسيط: ‏`ResourcePage` مع `columns` و`filters` و`form.fields` (انظر `CustomersPage`).
3. أعمدة الفرز: ‏`S` (‏`sortable: true`) **فقط** لأسماء `AllowedSortFields` في الخادم (غيرها يعيد 400).
4. شاشة تفاصيل/سير عمل: صفحة خاصة مثل `ShipmentDetailsPage` (‏`useQuery` + أزرار حسب الحالة والصلاحية + `ReasonDialog`).
5. المسار في `app/routes.tsx` مع `RequirePermission`، والعنصر في `app/navigation.tsx`، والنصوص بـ `tx('عربي', 'English')` أو `t('key')`.
