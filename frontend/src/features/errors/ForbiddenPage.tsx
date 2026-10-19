import { Box, Button, Typography } from '@mui/material';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

export default function ForbiddenPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  return (
    <Box sx={{ textAlign: 'center', py: 10 }}>
      <LockOutlinedIcon sx={{ fontSize: 64, color: 'text.secondary' }} />
      <Typography variant="h5" sx={{ mt: 2 }}>{t('auth.forbiddenTitle')}</Typography>
      <Typography color="text.secondary" sx={{ mt: 1, mb: 3 }}>{t('auth.forbiddenText')}</Typography>
      <Button variant="contained" onClick={() => navigate('/')}>{t('errors.goHome')}</Button>
    </Box>
  );
}
