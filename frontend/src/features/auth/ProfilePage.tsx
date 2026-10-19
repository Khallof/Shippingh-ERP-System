import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Alert, Box, Button, Chip, Grid2 as Grid, Stack, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { AppError } from '@/api/errors';
import { useAuth } from '@/auth/AuthProvider';
import PageHeader from '@/components/common/PageHeader';
import Section from '@/components/common/Section';
import InfoGrid from '@/components/common/InfoGrid';
import { useNotify } from '@/components/common/Notifier';
import { useTx } from '@/i18n';
import { formatDateTime } from '@/utils/format';

export default function ProfilePage() {
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const { user, changePassword } = useAuth();
  const [error, setError] = useState<AppError | null>(null);

  // Same policy as the API (Identity options): 8+ chars, upper, lower, digit, symbol.
  const schema = z
    .object({
      currentPassword: z.string().min(1, t('common.required')),
      newPassword: z
        .string()
        .min(8, tx('8 أحرف على الأقل', 'At least 8 characters'))
        .regex(/[A-Z]/, tx('حرف كبير واحد على الأقل', 'One upper-case letter'))
        .regex(/[a-z]/, tx('حرف صغير واحد على الأقل', 'One lower-case letter'))
        .regex(/\d/, tx('رقم واحد على الأقل', 'One digit'))
        .regex(/[^A-Za-z0-9]/, tx('رمز واحد على الأقل', 'One symbol')),
      confirmNewPassword: z.string()
    })
    .refine((values) => values.newPassword === values.confirmNewPassword, { path: ['confirmNewPassword'], message: tx('غير متطابقة', 'Does not match') });
  type Values = z.infer<typeof schema>;

  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { currentPassword: '', newPassword: '', confirmNewPassword: '' } });

  const submit = form.handleSubmit(async (values) => {
    setError(null);
    try {
      await changePassword(values);
      form.reset();
      notify.success(t('auth.passwordChanged'));
    } catch (caught) {
      setError(caught as AppError);
    }
  });

  const fieldError = (name: keyof Values) => form.formState.errors[name]?.message ?? error?.fieldErrors[name]?.[0];

  return (
    <>
      <PageHeader title={t('auth.profile')} />
      <Section title={tx('بيانات الحساب', 'Account')}>
        <InfoGrid
          items={[
            { label: tx('اسم المستخدم', 'User name'), value: user?.userName },
            { label: tx('الاسم', 'Name'), value: user?.arabicFullName },
            { label: tx('الاسم بالإنجليزية', 'English name'), value: user?.fullName },
            { label: tx('البريد', 'E-mail'), value: user?.email },
            { label: tx('رمز الموظف', 'Employee code'), value: user?.employeeCode },
            { label: t('auth.lastLogin'), value: formatDateTime(user?.lastLoginAtUtc) },
            { label: t('auth.roles'), value: <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: 'wrap' }}>{user?.roles.map((role) => <Chip key={role} label={role} />)}</Stack>, span: 6 }
          ]}
        />
      </Section>
      <Section title={t('auth.changePassword')}>
        <Box component="form" onSubmit={submit} noValidate sx={{ maxWidth: 720 }}>
          {error && !error.isValidation && <Alert severity="error" sx={{ mb: 2 }}>{error.message}</Alert>}
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 4 }}>
              <TextField type="password" label={t('auth.currentPassword')} autoComplete="current-password" {...form.register('currentPassword')} error={!!fieldError('currentPassword')} helperText={fieldError('currentPassword')} />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <TextField type="password" label={t('auth.newPassword')} autoComplete="new-password" {...form.register('newPassword')} error={!!fieldError('newPassword')} helperText={fieldError('newPassword')} />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <TextField type="password" label={t('auth.confirmPassword')} autoComplete="new-password" {...form.register('confirmNewPassword')} error={!!fieldError('confirmNewPassword')} helperText={fieldError('confirmNewPassword')} />
            </Grid>
          </Grid>
          <Button type="submit" variant="contained" sx={{ mt: 2 }} disabled={form.formState.isSubmitting}>{t('auth.changePassword')}</Button>
        </Box>
      </Section>
      <Section title={t('auth.permissions')}>
        <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: 'wrap' }}>
          {user?.permissions.map((permission) => <Chip key={permission} label={permission} variant="outlined" />)}
        </Stack>
      </Section>
    </>
  );
}
