import { Box, Button, Typography } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

export default function NotFoundPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  return (
    <Box sx={{ textAlign: 'center', py: 10 }}>
      <Typography variant="h2" color="text.secondary">404</Typography>
      <Typography variant="h5" sx={{ mt: 1 }}>{t('errors.notFoundTitle')}</Typography>
      <Typography color="text.secondary" sx={{ mt: 1, mb: 3 }}>{t('errors.notFoundText')}</Typography>
      <Button variant="contained" onClick={() => navigate('/')}>{t('errors.goHome')}</Button>
    </Box>
  );
}
