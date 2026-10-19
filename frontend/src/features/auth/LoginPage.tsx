import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Alert, Box, Button, Card, CardContent, IconButton, InputAdornment, Stack, TextField, Typography } from '@mui/material';
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined';
import VisibilityOffOutlinedIcon from '@mui/icons-material/VisibilityOffOutlined';
import TranslateIcon from '@mui/icons-material/Translate';
import { useTranslation } from 'react-i18next';
import { AppError } from '@/api/errors';
import { useAuth } from '@/auth/AuthProvider';
import { setLanguage, useLanguage, useTx } from '@/i18n';

/**
 * Login page. Validation: zod (client) + the API (FluentValidation, lockout after repeated failures).
 * The refresh token never reaches JavaScript: the API sets it as an httpOnly cookie (X-Auth-Mode: cookie).
 */
export default function LoginPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const { login, sessionExpired } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<AppError | null>(null);

  const schema = z.object({
    userNameOrEmail: z.string().trim().min(1, t('common.required')).max(256),
    password: z.string().min(1, t('common.required')).max(128)
  });
  type Values = z.infer<typeof schema>;

  const { register, handleSubmit, formState } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { userNameOrEmail: '', password: '' } });

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      await login(values.userNameOrEmail, values.password);
      // Only same-site relative paths (no open redirect).
      const returnTo = searchParams.get('returnTo');
      navigate(returnTo && returnTo.startsWith('/') && !returnTo.startsWith('//') ? returnTo : '/', { replace: true });
    } catch (caught) {
      setError(caught as AppError);
    }
  });

  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', p: 2, background: (theme) => (theme.palette.mode === 'dark' ? '#121211' : 'linear-gradient(135deg, #e8f0fb 0%, #f7f7f5 60%)') }}>
      <Card sx={{ width: '100%', maxWidth: 420 }}>
        <CardContent sx={{ p: 4 }}>
          <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 3 }}>
            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
              <Box component="img" src="/favicon.svg" alt="" sx={{ width: 44, height: 44 }} />
              <Box>
                <Typography variant="h5">{t('app.name')}</Typography>
                <Typography variant="body2" color="text.secondary">{t('auth.title')}</Typography>
              </Box>
            </Stack>
            <IconButton onClick={() => setLanguage(language === 'ar' ? 'en' : 'ar')} title={language === 'ar' ? 'English' : 'العربية'}>
              <TranslateIcon />
            </IconButton>
          </Stack>

          {sessionExpired && !error && <Alert severity="info" sx={{ mb: 2 }}>{t('auth.sessionExpired')}</Alert>}
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error.message}</Alert>}

          <form onSubmit={onSubmit} noValidate>
            <Stack spacing={2}>
              <TextField
                label={t('auth.userName')}
                autoComplete="username"
                autoFocus
                inputProps={{ dir: 'ltr' }}
                size="medium"
                {...register('userNameOrEmail')}
                error={!!formState.errors.userNameOrEmail || !!error?.fieldErrors.userNameOrEmail}
                helperText={formState.errors.userNameOrEmail?.message ?? error?.fieldErrors.userNameOrEmail?.[0]}
              />
              <TextField
                label={t('auth.password')}
                type={showPassword ? 'text' : 'password'}
                autoComplete="current-password"
                inputProps={{ dir: 'ltr' }}
                size="medium"
                {...register('password')}
                error={!!formState.errors.password}
                helperText={formState.errors.password?.message}
                InputProps={{
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton onClick={() => setShowPassword((value) => !value)} edge="end" aria-label={tx('إظهار كلمة المرور', 'Show password')}>
                        {showPassword ? <VisibilityOffOutlinedIcon /> : <VisibilityOutlinedIcon />}
                      </IconButton>
                    </InputAdornment>
                  )
                }}
              />
              <Button type="submit" variant="contained" size="large" disabled={formState.isSubmitting}>
                {formState.isSubmitting ? t('common.loading') : t('auth.signIn')}
              </Button>
            </Stack>
          </form>
        </CardContent>
      </Card>
    </Box>
  );
}
