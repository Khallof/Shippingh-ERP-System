# استراتيجية الفروع (Git) والتكامل المستمر

## الفروع

| الفرع | الغرض | يُدمج في |
|---|---|---|
| `main` | ما هو منشور في الإنتاج فقط؛ كل دمج = إصدار | — |
| `develop` | التكامل اليومي، يُنشر على بيئة الاختبار | `main` عبر `release/*` |
| `feature/<رقم>-<وصف>` | ميزة واحدة (مثال `feature/142-cod-dispute`) | `develop` عبر Pull Request |
| `release/<إصدار>` | تثبيت الإصدار: إصلاحات فقط | `main` + `develop` |
| `hotfix/<وصف>` | إصلاح عاجل للإنتاج | `main` + `develop` |

## القواعد

- لا دفع مباشر إلى `main` أو `develop` (Branch protection): Pull Request + مراجع واحد على الأقل + نجاح CI.
- رسائل الالتزام بصيغة Conventional Commits: `feat(cod): ...`، `fix(api): ...`، `docs: ...`.
- الإصدار بوسم دلالي `v1.8.0` على `main` ⇒ CI يبني الصور ويدفعها إلى `ghcr.io/<owner>/shippingerp-api:1.8.0` و`...-web:1.8.0`.
- أي تغيير على الكيانات = ترحيل EF في نفس الـ PR، ويُذكر في وصفه.

## خط CI (`.github/workflows/ci.yml`)

1. **backend**: ‏restore ← build (Release) ← test (وحدات + تكامل، بلا SQL Server) ← رفع نتائج الاختبار.
2. **frontend**: ‏`npm ci` ← lint ← vitest ← build (يتضمن فحص TypeScript).
3. **docker**: بعد نجاح السابقين؛ يبني صورتي الـ API والويب دائماً، ويدفعهما إلى GHCR عند الدفع إلى `main` أو وسم `v*`.

النشر من الصور: ضع `API_IMAGE` و`WEB_IMAGE` في `.env` ثم `docker compose pull api frontend; docker compose up -d`.
