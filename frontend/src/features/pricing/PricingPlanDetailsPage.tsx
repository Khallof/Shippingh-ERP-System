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
            {num('priority', tx('الأولوية (الرقم الأكبر يفوز)', 'Priority (higher wins)'), 2)}
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
