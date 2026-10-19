import type { ReactNode } from 'react';
import { Box, IconButton, Stack, Tooltip, Typography } from '@mui/material';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useLanguage } from '@/i18n';

interface PageHeaderProps {
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
  /** Shows a back button (to this path, or history back). */
  back?: string | true;
}

export default function PageHeader({ title, subtitle, actions, back }: PageHeaderProps) {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const language = useLanguage();

  return (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ mb: 2, alignItems: { sm: 'center' }, justifyContent: 'space-between' }}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', minWidth: 0 }}>
        {back && (
          <Tooltip title={t('common.back')}>
            <IconButton onClick={() => (back === true ? navigate(-1) : navigate(back))} size="small">
              {/* The arrow points to the start side: right in Arabic, left in English. */}
              {language === 'ar' ? <ArrowForwardIcon /> : <ArrowBackIcon />}
            </IconButton>
          </Tooltip>
        )}
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="h5" component="h1" noWrap>
            {title}
          </Typography>
          {subtitle && (
            <Typography variant="body2" color="text.secondary">
              {subtitle}
            </Typography>
          )}
        </Box>
      </Stack>
      {actions && (
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
          {actions}
        </Stack>
      )}
    </Stack>
  );
}
