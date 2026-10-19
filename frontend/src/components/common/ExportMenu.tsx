import { useState } from 'react';
import { Button, ListItemIcon, Menu, MenuItem } from '@mui/material';
import FileDownloadOutlinedIcon from '@mui/icons-material/FileDownloadOutlined';
import GridOnIcon from '@mui/icons-material/GridOn';
import PictureAsPdfOutlinedIcon from '@mui/icons-material/PictureAsPdfOutlined';
import { useTranslation } from 'react-i18next';
import { useTx } from '@/i18n';
import { api, openFile, saveFile } from '@/api/client';
import type { ExportFormat } from '@/api/types';
import { useNotify } from './Notifier';

interface ExportMenuProps {
  /** Export endpoint, e.g. "/shipments/export". The current filters are sent as query parameters. */
  url: string;
  params?: object;
  formats?: ExportFormat[];
}

/** Excel (download) / PDF (opens for printing). Same filters as the screen; the API caps the rows. */
export default function ExportMenu({ url, params, formats = ['excel', 'pdf'] }: ExportMenuProps) {
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [busy, setBusy] = useState(false);

  const run = async (format: ExportFormat) => {
    setAnchor(null);
    setBusy(true);
    try {
      const file = await api.export(url, format, params);
      if (format === 'pdf') {
        openFile(file);
      } else {
        saveFile(file);
      }
    } catch (error) {
      notify.error(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <Button variant="outlined" startIcon={<FileDownloadOutlinedIcon />} disabled={busy} onClick={(event) => setAnchor(event.currentTarget)}>
        {tx('تصدير', 'Export')}
      </Button>
      <Menu anchorEl={anchor} open={!!anchor} onClose={() => setAnchor(null)}>
        {formats.includes('excel') && (
          <MenuItem onClick={() => void run('excel')}>
            <ListItemIcon>
              <GridOnIcon fontSize="small" />
            </ListItemIcon>
            {t('common.exportExcel')}
          </MenuItem>
        )}
        {formats.includes('pdf') && (
          <MenuItem onClick={() => void run('pdf')}>
            <ListItemIcon>
              <PictureAsPdfOutlinedIcon fontSize="small" />
            </ListItemIcon>
            {t('common.exportPdf')}
          </MenuItem>
        )}
      </Menu>
    </>
  );
}

/** Button that opens a server document (label, voucher, invoice PDF) in a new tab for printing. */
export function PrintButton({ url, params, label, icon, method = 'get', body, variant = 'outlined' }: {
  url: string;
  params?: object;
  label: string;
  icon?: React.ReactNode;
  method?: 'get' | 'post';
  body?: unknown;
  variant?: 'outlined' | 'contained' | 'text';
}) {
  const notify = useNotify();
  const [busy, setBusy] = useState(false);
  return (
    <Button
      variant={variant}
      startIcon={icon}
      disabled={busy}
      onClick={async () => {
        setBusy(true);
        try {
          openFile(await api.download(url, { params, method, body, fallbackName: 'document.pdf' }));
        } catch (error) {
          notify.error(error);
        } finally {
          setBusy(false);
        }
      }}
    >
      {label}
    </Button>
  );
}
