import { ToggleButton, ToggleButtonGroup } from '@mui/material';
import type { CurrencyDto } from '@/api/generated';

/** One button per currency; the screens using it show that currency only, in its own amounts. */
export default function CurrencyToggle({ list, value, onChange }: { list: CurrencyDto[]; value: number | null; onChange: (id: number) => void }) {
  if (list.length === 0) return null;
  return (
    <ToggleButtonGroup exclusive size="small" color="primary" value={value} onChange={(_, v: number | null) => v !== null && onChange(v)}>
      {list.map((c) => <ToggleButton key={c.id} value={c.id} sx={{ px: 2 }}>{c.code} - {c.arabicName}</ToggleButton>)}
    </ToggleButtonGroup>
  );
}
