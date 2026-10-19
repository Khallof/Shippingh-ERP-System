import { useRef, useState } from 'react';
import { Button, InputAdornment, Stack, TextField } from '@mui/material';
import QrCodeScannerIcon from '@mui/icons-material/QrCodeScanner';
import { useTx } from '@/i18n';

/**
 * Barcode input for USB / Bluetooth scanners (they type the code and press Enter). Several codes can be pasted at
 * once (separated by spaces, commas or new lines). Keeps the focus for the next scan.
 */
export default function ScanBox({ onCodes, label, busy, buttonText }: { onCodes(codes: string[]): void | Promise<void>; label?: string; busy?: boolean; buttonText?: string }) {
  const tx = useTx();
  const [value, setValue] = useState('');
  const input = useRef<HTMLInputElement>(null);

  const submit = async () => {
    const codes = splitCodes(value);
    if (codes.length === 0) return;
    await onCodes(codes);
    setValue('');
    input.current?.focus();
  };

  return (
    <Stack direction="row" spacing={1}>
      <TextField
        inputRef={input}
        value={value}
        autoFocus
        label={label ?? tx('امسح أو اكتب رقم التتبع / الباركود', 'Scan or type tracking no. / barcode')}
        onChange={(event) => setValue(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            void submit();
          }
        }}
        onPaste={(event) => {
          const text = event.clipboardData.getData('text');
          if (/[\s,]/.test(text.trim())) {
            event.preventDefault();
            void onCodes(splitCodes(text));
          }
        }}
        inputProps={{ dir: 'ltr' }}
        InputProps={{ startAdornment: <InputAdornment position="start"><QrCodeScannerIcon fontSize="small" /></InputAdornment> }}
      />
      <Button variant="contained" onClick={() => void submit()} disabled={busy || !value.trim()} sx={{ whiteSpace: 'nowrap' }}>
        {buttonText ?? tx('إضافة', 'Add')}
      </Button>
    </Stack>
  );
}

export function splitCodes(text: string | null | undefined): string[] {
  return Array.from(new Set((text ?? '').split(/[\s,;]+/).map((code) => code.trim()).filter(Boolean)));
}
