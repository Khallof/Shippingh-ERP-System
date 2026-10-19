import { Box, Chip, Stack, Typography } from '@mui/material';
import type { ShipmentStatusHistoryDto } from '@/api/generated';
import StatusChip, { toneOf } from '@/components/common/StatusChip';
import { enumLabel } from '@/i18n/enums';
import { useLanguage, useTx } from '@/i18n';
import { statusColors } from '@/theme/palette';
import { formatDateTime } from '@/utils/format';

/**
 * Vertical tracking timeline (newest on top). Every row: status, when (business time), where, who, source document
 * and notes. Corrections are marked. The line sits on the start side (right in Arabic).
 */
export default function ShipmentTimeline({ history }: { history: ShipmentStatusHistoryDto[] }) {
  const tx = useTx();
  const language = useLanguage();
  const rows = [...history].sort((a, b) => b.occurredAtUtc.localeCompare(a.occurredAtUtc) || b.id - a.id);

  return (
    <Box component="ol" sx={{ listStyle: 'none', m: 0, p: 0 }}>
      {rows.map((row, index) => {
        const tone = toneOf(row.toStatus);
        const color = tone === 'neutral' ? '#9e9e9e' : tone === 'info' ? '#2a78d6' : statusColors[tone];
        return (
          <Box component="li" key={row.id} sx={{ display: 'flex', gap: 2, position: 'relative', pb: index === rows.length - 1 ? 0 : 2.5 }}>
            <Box sx={{ position: 'relative', width: 16, flexShrink: 0 }}>
              <Box sx={{ width: 14, height: 14, borderRadius: '50%', bgcolor: index === 0 ? color : 'background.paper', border: `3px solid ${color}`, mt: 0.5 }} />
              {index < rows.length - 1 && <Box sx={{ position: 'absolute', top: 20, bottom: -4, insetInlineStart: 6, width: 2, bgcolor: 'divider' }} />}
            </Box>
            <Box sx={{ flex: 1, minWidth: 0 }}>
              <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                <StatusChip enumName="ShipmentStatus" value={row.toStatus} />
                {row.isCorrection && <Chip color="warning" label={tx('تصحيح', 'Correction')} />}
                <Typography variant="body2" color="text.secondary">{formatDateTime(row.occurredAtUtc)}</Typography>
              </Stack>
              <Typography variant="body2" sx={{ mt: 0.5 }}>
                {[row.branchName, enumLabel('ShipmentStatusChangeSource', row.source, language), row.referenceNumber, row.changedByUserName].filter(Boolean).join(' · ')}
              </Typography>
              {row.notes && <Typography variant="body2" color="text.secondary">{row.notes}</Typography>}
            </Box>
          </Box>
        );
      })}
    </Box>
  );
}
