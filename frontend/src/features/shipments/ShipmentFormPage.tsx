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
      // Step 9: price per kg / per m³ typed on the shipment (empty = the price lists).
      unitPrice: z.number({ invalid_type_error: required }).min(0, tx('لا يقبل قيمة سالبة', 'Must not be negative')).nullable(),
      currencyId: z.number({ invalid_type_error: required }).nullable(),
      declaredValue: money,
      insuranceValue: money,
      isCOD: z.boolean(),
      codAmount: z.number().min(0, tx('لا يقبل قيمة سالبة', 'Must not be negative')).nullable(),
      // Step 9: extra service fees added to the charge.
      extraFee: z.number().min(0, tx('لا يقبل قيمة سالبة', 'Must not be negative')).nullable(),
      requiresPickup: z.boolean(),
      requiresPackaging: z.boolean(),
      manualShippingCharge: optionalNumber,
      discountAmount: money,
      manualPriceReason: z.string().max(500).nullable(),
      senderNotes: z.string().max(500).nullable(),
      internalNotes: z.string().max(1000).nullable(),
      // Kept as is (no longer shown) so editing an older shipment does not erase it.
      // Step 9: رقم الإرسالية (consignment), optional.
      consignmentNumber: z.string().max(30).nullable(),
      externalReference: z.string().max(50).nullable()
    })
    .superRefine((v, ctx) => {
      if (v.insuranceValue > v.declaredValue) ctx.addIssue({ code: 'custom', path: ['insuranceValue'], message: tx('لا يزيد عن القيمة المصرّح بها', 'Cannot exceed the declared value') });
      if (v.isCOD && !(v.codAmount && v.codAmount > 0)) ctx.addIssue({ code: 'custom', path: ['codAmount'], message: tx('مبلغ التحصيل مطلوب', 'COD amount is required') });
      if (v.unitPrice === null && v.manualShippingCharge === null)
        ctx.addIssue({ code: 'custom', path: ['unitPrice'], message: tx('اكتب السعر', 'Enter the price') });
      if (v.currencyId === null && v.manualShippingCharge === null)
        ctx.addIssue({ code: 'custom', path: ['currencyId'], message: tx('اختر العملة', 'Choose the currency') });
      if (v.unitPrice !== null && v.manualShippingCharge !== null)
        ctx.addIssue({ code: 'custom', path: ['unitPrice'], message: tx('أدخل سعر الوحدة أو السعر اليدوي، وليس الاثنين', 'Unit price or manual price, not both') });
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
    currencyId: values.unitPrice !== null ? values.currencyId : null,
    codAmount: values.isCOD ? values.codAmount ?? 0 : 0,
    extraFee: values.extraFee ?? 0
  };
}

function fromShipment(shipment: ShipmentDto): Values {
  return {
    senderCustomerId: shipment.senderCustomerId,
    senderAddressId: shipment.senderAddressId ?? null,
    recipientCustomerId: shipment.recipientCustomerId,
    recipientAddressId: shipment.recipientAddressId ?? null,
    originBranchId: shipment.originBranchId,
    destinationCityId: shipment.destinationCityId,
    destinationZoneId: shipment.destinationZoneId ?? null,
    destinationBranchId: shipment.destinationBranchId ?? null,
    serviceTypeId: shipment.serviceTypeId,
    packageDescription: shipment.packageDescription ?? '',
    packageCount: shipment.packageCount,
    totalWeightKg: shipment.actualWeight,
    totalVolumeCbm: shipment.totalVolumeCbm > 0 ? shipment.totalVolumeCbm : null,
    pricingBasis: shipment.pricingBasis,
    chargesPaidBy: shipment.chargesPaidBy,
    unitPrice: shipment.unitPrice ?? null,
    extraFee: shipment.extraFee > 0 ? shipment.extraFee : null,
    consignmentNumber: shipment.consignmentNumber ?? null,
    currencyId: shipment.currencyId ?? null,
    declaredValue: shipment.declaredValue,
    insuranceValue: shipment.insuranceValue,
    isCOD: shipment.isCOD,
    codAmount: shipment.codAmount ?? null,
    requiresPickup: shipment.requiresPickup,
    requiresPackaging: shipment.requiresPackaging,
    manualShippingCharge: shipment.manualShippingCharge ?? null,
    discountAmount: shipment.manualDiscountAmount,
    manualPriceReason: null,
    senderNotes: shipment.senderNotes ?? null,
    internalNotes: shipment.internalNotes ?? null,
    externalReference: shipment.externalReference ?? null
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
      unitPrice: null,
      currencyId: null,
      extraFee: null,
      consignmentNumber: null,
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
  const unitPrice = watch('unitPrice');
  const totalWeight = watch('totalWeightKg');
  const totalVolume = watch('totalVolumeCbm');
  const quantity = pricingBasis === 'Volume' ? totalVolume : totalWeight;
  const extraFee = watch('extraFee');

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

  // Fields the user must fix before saving: listed at the top (a field lower on the page is easy to miss).
  const [clientErrors, setClientErrors] = useState<string[]>([]);
  const fieldLabels: Partial<Record<string, string>> = {
    senderCustomerId: tx('المرسل', 'Sender'), recipientCustomerId: tx('المستلم', 'Recipient'), originBranchId: tx('فرع الإنشاء', 'Origin branch'),
    destinationCityId: tx('مدينة الوجهة', 'Destination city'), serviceTypeId: tx('نوع البضاعة', 'Goods type'), packageDescription: tx('وصف المحتوى', 'Contents'),
    packageCount: tx('عدد الطرود', 'Packages'), totalWeightKg: tx('الوزن الكلي', 'Total weight'), totalVolumeCbm: tx('الحجم الكلي', 'Total volume'),
    unitPrice: tx('السعر', 'Price'), currencyId: tx('العملة', 'Currency'), extraFee: tx('رسوم الخدمات', 'Service fees'), codAmount: tx('مبلغ التحصيل', 'COD amount'),
    insuranceValue: tx('قيمة التأمين', 'Insurance'), declaredValue: tx('القيمة المصرّح بها', 'Declared value'), manualShippingCharge: tx('السعر اليدوي', 'Manual price'),
    discountAmount: tx('الخصم', 'Discount'), manualPriceReason: tx('سبب السعر اليدوي', 'Manual price reason'), consignmentNumber: tx('رقم الإرسالية', 'Consignment'),
    externalReference: tx('المرجع الخارجي', 'External reference'), senderNotes: tx('ملاحظات المرسل', 'Sender notes'), internalNotes: tx('ملاحظات داخلية', 'Internal notes')
  };
  const onInvalid = (errors: Record<string, { message?: string } | undefined>) => {
    setClientErrors(Object.entries(errors).map(([field, error]) => `${fieldLabels[field] ?? field}: ${error?.message ?? tx('قيمة غير صالحة', 'Invalid value')}`));
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const save = handleSubmit(async (values) => {
    setClientErrors([]);
    setServerError(null);
    try {
      const saved = editId ? await shipmentsApi.update(editId, toRequest(values)) : await shipmentsApi.create(toRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['/shipments'] });
      notify.success(editId ? t('common.saved') : `${tx('تم تسجيل الشحنة', 'Shipment created')} ${saved.trackingNumber}`);
      navigate(`/shipments/${saved.id}`, { replace: true });
    } catch (error) {
      setServerError(error as AppError);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }, (errors) => onInvalid(errors as Record<string, { message?: string } | undefined>));

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
            <Button variant="outlined" startIcon={<CalculateOutlinedIcon />} onClick={handleSubmit((values) => { setClientErrors([]); quoteMutation.mutate(values); }, (errors) => onInvalid(errors as Record<string, { message?: string } | undefined>))} disabled={quoteMutation.isPending}>
              {tx('احسب السعر', 'Calculate price')}
            </Button>
            <Button type="submit" variant="contained" disabled={formState.isSubmitting}>
              {editId ? t('common.save') : tx('تسجيل الشحنة', 'Create shipment')}
            </Button>
          </>
        }
      />

      {clientErrors.length > 0 && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setClientErrors([])}>
          {tx('لم تُحفظ الشحنة. صحّح الحقول التالية:', 'Not saved. Fix these fields:')}
          <ul style={{ margin: 0 }}>{clientErrors.map((message) => <li key={message}>{message}</li>)}</ul>
        </Alert>
      )}
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
              <Grid size={{ xs: 12, md: 4 }}>
                <TextField label={tx('رقم الإرسالية (اختياري)', 'Consignment no. (optional)')} {...register('consignmentNumber', { setValueAs: (v: string) => (v ? v.trim() || null : null) })} error={!!err('consignmentNumber')} helperText={err('consignmentNumber')} />
              </Grid>
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
              <Grid size={{ xs: 12, sm: 4 }}>
                <TextField
                  label={pricingBasis === 'Volume' ? tx('سعر المتر المكعب', 'Price per m³') : tx('سعر الكيلو', 'Price per kg')}
                  required
                  {...numberProps('unitPrice')}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 4 }}>
                <Controller
                  control={control}
                  name="currencyId"
                  render={({ field }) => (
                    <LookupSelect
                      source="currencies"
                      label={tx('العملة', 'Currency')}
                      required
                      value={field.value ?? null}
                      onChange={(value) => field.onChange(value === null ? null : Number(value))}
                      helperText={err('currencyId')}
                      error={!!err('currencyId')}
                    />
                  )}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 4 }}>
                <TextField
                  label={tx('رسوم الخدمات', 'Service fees')}
                  {...numberProps('extraFee')}
                  helperText={err('extraFee') ?? tx('تغليف، تخزين، أوراق... تُضاف إلى السعر (اختياري)', 'Packing, storage, papers... added to the price (optional)')}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 8 }}>
                {unitPrice !== null && unitPrice !== undefined && !Number.isNaN(unitPrice) && quantity ? (
                  <Alert severity="info" icon={false} sx={{ py: 0.5 }}>
                    <span dir="ltr">{formatNumber(quantity, 3)} {pricingBasis === 'Volume' ? 'm³' : 'kg'} × {formatNumber(unitPrice, 2)}{extraFee ? ` + ${formatNumber(extraFee, 2)}` : ''} = <strong>{formatNumber(quantity * unitPrice + (extraFee || 0), 2)}</strong></span>
                    <Typography variant="caption" component="div" color="text.secondary">{tx('السعر النهائي (مع التقريب والضريبة) يظهر في «احسب السعر»', 'Final price (rounding, tax): "Calculate price"')}</Typography>
                  </Alert>
                ) : null}
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
              <Button fullWidth variant="outlined" sx={{ mt: 2 }} startIcon={<CalculateOutlinedIcon />} onClick={handleSubmit((values) => { setClientErrors([]); quoteMutation.mutate(values); }, (errors) => onInvalid(errors as Record<string, { message?: string } | undefined>))}>
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
