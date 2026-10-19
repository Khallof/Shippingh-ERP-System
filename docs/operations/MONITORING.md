# المراقبة

## نقاط الصحة

| النقطة | المعنى | يستخدمها |
|---|---|---|
| `/health/live` | العملية تعمل (لا تلمس القاعدة) | `HEALTHCHECK` في صورة الـ API |
| `/health/ready` | جاهز للخدمة: SQL Server متاح (503 عند العطل) | المراقبة الخارجية / موازن الحمل |
| `/healthz` (nginx) | خادم الويب يعمل | `HEALTHCHECK` في صورة الويب |

```powershell
docker compose ps                                     # STATUS = healthy لكل خدمة
Invoke-RestMethod http://localhost:8080/health/ready  # عبر nginx
```

## المراقبة الخارجية (الحد الأدنى الموصى به)

- **مراقب توافر** (Uptime Kuma ذاتي الاستضافة، أو UptimeRobot/Better Stack): يفحص `https://SITE_DOMAIN/health/ready` كل دقيقة ويرسل تنبيهاً (بريد/تيليغرام) بعد فشلين متتاليين. يُضاف إليه فحص انتهاء شهادة TLS.
- **المساحة**: تنبيه عند امتلاء القرص 80% (القاعدة + النسخ الاحتياطية + السجلات).
- **النسخ الاحتياطي**: تنبيه إذا لم يظهر ملف `.bak` جديد خلال 26 ساعة.

## مؤشرات التطبيق التي تُراجع يومياً

- لوحة التحصيل: أحداث ترحيل فاشلة (`/accounting/posting-events?status=Failed`)، إقفالات صناديق بانتظار الاعتماد، تحويلات نقدية معلقة.
- سجل التدقيق: `LoginFailed` و`RefreshTokenReuseDetected` (محاولة سرقة جلسة).
- سجلات الأخطاء: `docker compose logs api | Select-String "ERR"`؛ كل خطأ يحمل `TraceId` يطابق ما يراه المستخدم.

## توسعة لاحقة

- تجميع السجلات: Serilog يدعم Seq أو Grafana Loki بإضافة Sink دون تغيير الكود.
- المقاييس: OpenTelemetry (‏ASP.NET Core + EF Core) إلى Prometheus/Grafana: زمن الاستجابة، الأخطاء 5xx، زمن استعلامات SQL.
