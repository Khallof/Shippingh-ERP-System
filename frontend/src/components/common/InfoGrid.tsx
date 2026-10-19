import type { ReactNode } from 'react';
import { Box, Typography } from '@mui/material';

export interface InfoItem {
  label: ReactNode;
  value: ReactNode;
  /** Columns out of 12 on wide screens (default 3). */
  span?: number;
}

/** Read-only label / value grid of a details page. */
export default function InfoGrid({ items }: { items: (InfoItem | false | null | undefined)[] }) {
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'repeat(1, 1fr)', sm: 'repeat(6, 1fr)', md: 'repeat(12, 1fr)' }, gap: 2 }}>
      {items.filter(Boolean).map((item, index) => {
        const entry = item as InfoItem;
        const span = entry.span ?? 3;
        return (
          <Box key={index} sx={{ gridColumn: { xs: 'span 1', sm: `span ${Math.min(6, span * 2)}`, md: `span ${span}` }, minWidth: 0 }}>
            <Typography variant="caption" color="text.secondary" component="div">
              {entry.label}
            </Typography>
            <Typography variant="body1" component="div" sx={{ fontWeight: 500, overflowWrap: 'anywhere' }}>
              {entry.value === null || entry.value === undefined || entry.value === '' ? '-' : entry.value}
            </Typography>
          </Box>
        );
      })}
    </Box>
  );
}
