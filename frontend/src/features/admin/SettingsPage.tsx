import { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Grid2 as Grid, Stack, Typography } from '@mui/material';
import CompanyLogo from '@/components/common/CompanyLogo';
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

/** Step 9: upload / replace / remove the company logo (printed on every document, shown in the app). */
function LogoSection({ profile, canEdit }: { profile: CompanyProfileDto | undefined; canEdit: boolean }) {
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const run = async (action: () => Promise<unknown>, done: string) => {
    setBusy(true);
    try {
      await action();
      notify.success(done);
      await queryClient.invalidateQueries({ queryKey: ['/company-profile'] });
    } catch (error) {
      notify.error(error);
    } finally {
      setBusy(false);
    }
  };

  const upload = (file: File | undefined) => {
    if (!file) return;
    const form = new FormData();
    form.append('file', file);
    void run(() => api.putForm('/company-profile/logo', form), tx('تم حفظ الشعار', 'Logo saved'));
  };

  return (
    <Section title={tx('شعار الشركة (يظهر في البرنامج وعلى كل الإيصالات والفواتير والملصقات)', 'Company logo (app, receipts, invoices, labels)')}>
      {!profile ? (
        <Alert severity="info">{tx('احفظ بيانات الشركة أولاً، ثم ارفع الشعار.', 'Save the company profile first, then upload the logo.')}</Alert>
      ) : (
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3} sx={{ alignItems: { sm: 'center' } }}>
          <Stack sx={{ width: 220, height: 120, border: 1, borderColor: 'divider', borderRadius: 2, alignItems: 'center', justifyContent: 'center', bgcolor: '#fff' }}>
            {profile.hasLogo
              ? <CompanyLogo key={profile.logoVersion} version={profile.logoVersion} size={100} sx={{ maxWidth: 200 }} />
              : <Typography color="text.secondary">{tx('لا يوجد شعار', 'No logo')}</Typography>}
          </Stack>
          {canEdit && (
            <Stack spacing={1}>
              <Button variant="contained" component="label" disabled={busy}>
                {profile.hasLogo ? tx('تغيير الشعار', 'Change logo') : tx('رفع الشعار', 'Upload logo')}
                <input hidden type="file" accept="image/png,image/jpeg" onChange={(event) => { upload(event.target.files?.[0]); event.target.value = ''; }} />
              </Button>
              {profile.hasLogo && (
                <Button color="error" disabled={busy} onClick={() => void run(() => api.delete('/company-profile/logo'), tx('تم حذف الشعار', 'Logo removed'))}>
                  {tx('حذف الشعار', 'Remove logo')}
                </Button>
              )}
              <Typography variant="caption" color="text.secondary">{tx('PNG أو JPG، حتى 1 ميغابايت. يُفضّل خلفية بيضاء أو شفافة.', 'PNG or JPG up to 1 MB; white or transparent background.')}</Typography>
            </Stack>
          )}
        </Stack>
      )}
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
        { name: 'address', label: tx('العنوان', 'Address'), span: 8 }
      ]} />
      <LogoSection profile={company.data} canEdit={canEdit} />
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
