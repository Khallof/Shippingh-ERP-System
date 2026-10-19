import type { ReactNode } from 'react';
import { Box, Card, CardContent, Skeleton, Typography } from '@mui/material';
import { statusColors, type StatusTone } from '@/theme/palette';

interface StatCardProps {
  label: ReactNode;
  value: ReactNode;
  hint?: ReactNode;
  icon?: ReactNode;
  tone?: StatusTone;
  loading?: boolean;
  onClick?: () => void;
}

/** A KPI tile: one number with its label (a single headline is a stat, not a chart). */
export default function StatCard({ label, value, hint, icon, tone, loading, onClick }: StatCardProps) {
  const accent = tone && tone !== 'neutral' && tone !== 'info' ? statusColors[tone] : undefined;
  return (
    <Card
      onClick={onClick}
      sx={{
        height: '100%',
        cursor: onClick ? 'pointer' : undefined,
        borderInlineStart: accent ? `4px solid ${accent}` : undefined,
        '&:hover': onClick ? { borderColor: 'primary.main' } : undefined
      }}
    >
      <CardContent sx={{ py: 1.5, '&:last-child': { pb: 1.5 } }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, color: 'text.secondary' }}>
          {icon}
          <Typography variant="body2" color="text.secondary" noWrap>
            {label}
          </Typography>
        </Box>
        <Typography variant="h5" sx={{ mt: 0.5, fontVariantNumeric: 'tabular-nums' }}>
          {loading ? <Skeleton width="60%" /> : value}
        </Typography>
        {hint && (
          <Typography variant="caption" color="text.secondary">
            {hint}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}
