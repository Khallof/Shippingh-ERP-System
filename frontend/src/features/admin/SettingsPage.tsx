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
