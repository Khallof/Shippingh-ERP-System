# النشر والتشغيل — ShippingERP

## 1. المكوّنات

| الخدمة | الصورة | المنفذ | الوظيفة |
|---|---|---|---|
| `sqlserver` | SQL Server 2022 | `127.0.0.1:14333` | قاعدة البيانات (حجم `sqlserver-data`) |
| `api` | `src/ShippingERP.API/Dockerfile` | `127.0.0.1:5080` | الواجهة البرمجية (.NET 8)، الملفات المرفوعة في الحجم `api-uploads` والسجلات في `api-logs` |
| `frontend` | `frontend/Dockerfile` (nginx غير جذري) | `8080` | تطبيق الويب + تمرير `/api` و`/health` إلى `api` |
| `proxy` (اختياري) | Caddy 2.8 | `80/443` | HTTPS بشهادات Let's Encrypt تلقائية |
| `backup` (اختياري) | أدوات SQL Server | — | نسخ احتياطي يومي للقاعدة والملفات مع حذف القديم |

المتصفح يتعامل مع **أصل واحد** (nginx): لذلك لا حاجة لـ CORS، وكوكي التجديد `SameSite=Strict` يعمل.

## 2. تطوير محلي على Windows 11

المتطلبات: .NET SDK 8، Node.js 20 LTS، Docker Desktop (WSL 2)، Git، ‏VS Code أو Visual Studio 2022.

```powershell
# 1) قاعدة البيانات فقط في Docker
Copy-Item .env.example .env      # ثم عدّل القيم
docker compose up -d sqlserver

# 2) الواجهة البرمجية (طرفية 1)
dotnet run --project src/ShippingERP.API --launch-profile http     # http://localhost:5080/swagger

# 3) تطبيق الويب (طرفية 2)
cd frontend
npm ci
npm run dev                                                         # http://localhost:5173
```

‏Vite يمرّر `/api` إلى `http://localhost:5080` (يمكن تغييره في `frontend/.env.local` بالمتغير `VITE_API_PROXY_TARGET`).

## 3. التشغيل الكامل في Docker (خادم أو جهاز Windows)

```powershell
Copy-Item .env.example .env       # غيّر كل القيم: كلمات المرور، JWT_SECRET_KEY، المدير الأول
docker compose up --build -d
docker compose ps                 # كل الخدمات healthy
```

- التطبيق: `http://localhost:8080` (أو `http://IP-الخادم:8080` داخل الشبكة).
- بعد أول دخول ناجح: احذف `SEED_ADMIN_PASSWORD` من `.env` ثم `docker compose up -d api`.

### HTTPS (موصى به لأي تشغيل حقيقي)

1. سجل DNS (مثل `erp.example.com`) يشير إلى الخادم، والمنفذان 80 و443 مفتوحان.
2. في `.env`: `SITE_DOMAIN='erp.example.com'`، `ACME_EMAIL='...'`، و`WEB_BIND='127.0.0.1'` (لا وصول إلى 8080 من الخارج).
3. `docker compose --profile proxy up -d`

‏Caddy يصدر الشهادة ويجددها تلقائياً، ويضيف HSTS. بديل على Windows Server: ‏IIS + ‏ARR كوكيل عكسي إلى `http://127.0.0.1:8080` مع شهادة من الجهة المعتمدة لديكم.

> شبكة داخلية بلا نطاق عام: `SITE_DOMAIN='erp.local'` مع سجل DNS داخلي؛ Caddy يستخدم شهادة داخلية — ثبّت شهادة الجذر الخاصة به (`docker compose cp proxy:/data/caddy/pki/authorities/local/root.crt .`) على أجهزة المستخدمين.

## 4. الملفات المرفوعة

محفوظة في الحجم `api-uploads` (`/app/App_Data/uploads`). تُنسخ احتياطياً مع القاعدة (انظر BACKUP-RESTORE.md).
للتوسع لاحقاً إلى تخزين كائنات (S3 / Azure Blob / MinIO): الواجهة `IFileStorage` (طبقة Application) هي نقطة الاستبدال الوحيدة: أضف تنفيذاً جديداً لها في Infrastructure وسجّله بدل التخزين المحلي؛ لا شيء آخر يتعامل مع المسارات.

## 5. الترحيلات (Migrations) في الإنتاج

- الافتراضي: `APPLY_MIGRATIONS_ON_STARTUP='true'` (تُطبّق عند بدء الـ API).
- للإنتاج المنضبط: أنشئ حزمة ترحيل وطبّقها قبل النشر ثم اجعل القيمة `false`:

```powershell
dotnet ef migrations bundle --project src/ShippingERP.Infrastructure --startup-project src/ShippingERP.API -o efbundle.exe --self-contained -r win-x64
.\efbundle.exe --connection "Server=...;Database=ShippingERP;..."
```

خذ نسخة احتياطية **قبل** أي ترحيل.

## 6. التحديث إلى إصدار جديد

```powershell
git pull                                   # أو استخدم صور ghcr.io من CI
.\scripts\backup-now.ps1 -Destination D:\ERP-Backups
docker compose up --build -d
docker compose ps
```

## 7. السجلات

- سجلات الحاويات: ‏json-file بحد 3 ملفات × 10 ميغابايت لكل خدمة (`docker compose logs -f api`).
- سجل الـ API الملفي: يومي، 30 يوماً، 50 ميغابايت لكل ملف (الحجم `api-logs`)، وكل سطر يحمل `TraceId` الظاهر في رسالة الخطأ للمستخدم.
