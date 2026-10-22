import { useState } from 'react';
import { Box, type SxProps, type Theme } from '@mui/material';
import { API_BASE_URL } from '@/api/http';

/**
 * Step 9: the company logo uploaded in Settings (GET /company-profile/logo, public). Falls back to the app icon
 * when there is no logo. `version` (CompanyProfileDto.logoVersion) makes the browser reload a changed logo.
 */
export default function CompanyLogo({ version, size = 40, sx }: { version?: number | string; size?: number; sx?: SxProps<Theme> }) {
  const [failed, setFailed] = useState(false);
  const src = failed ? '/favicon.svg' : `${API_BASE_URL}/company-profile/logo?v=${version ?? 0}`;
  return (
    <Box
      component="img"
      src={src}
      alt=""
      onError={() => setFailed(true)}
      sx={{ height: size, maxWidth: size * 2.2, objectFit: 'contain', flexShrink: 0, ...sx }}
    />
  );
}
