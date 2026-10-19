import { Chip } from '@mui/material';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import ScheduleIcon from '@mui/icons-material/Schedule';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked';
import LocalShippingOutlinedIcon from '@mui/icons-material/LocalShippingOutlined';
import { enumLabel, type EnumName } from '@/i18n/enums';
import { useLanguage } from '@/i18n';
import { statusColors, type StatusTone } from '@/theme/palette';

/** State -> tone. Status colors are never the only signal: every chip has an icon and a text label. */
const tones: Record<string, StatusTone> = {
  Delivered: 'good', Paid: 'good', Posted: 'good', Completed: 'good', Approved: 'good', Received: 'good', Closed: 'neutral',
  SettledToSender: 'good', Resolved: 'good', Open: 'info', Issued: 'info', Dispatched: 'info', InTransit: 'info',
  OutForDelivery: 'info', ArrivedAtDestination: 'info', PickedUp: 'info', ReceivedAtBranch: 'info', InWarehouse: 'info',
  CollectedByDriver: 'warning', ReceivedAtBranchCashbox: 'info', ReadyForSenderSettlement: 'info', AssignedToDriver: 'info',
  Draft: 'neutral', Created: 'neutral', Pending: 'warning', Submitted: 'warning', PendingApproval: 'warning',
  PartiallyPaid: 'warning', PartiallySettled: 'warning', PartiallyReceived: 'warning', UnderInvestigation: 'warning',
  DeliveryFailed: 'serious', Returned: 'serious', Overdue: 'serious', Disputed: 'serious', Skipped: 'neutral', Duplicate: 'neutral',
  Cancelled: 'neutral', Rejected: 'critical', Reversed: 'neutral', LostOrDamaged: 'critical', Failed: 'critical', Credited: 'neutral', Missing: 'critical'
};

const icons: Record<StatusTone, JSX.Element> = {
  good: <CheckCircleOutlineIcon />,
  warning: <ScheduleIcon />,
  serious: <WarningAmberIcon />,
  critical: <ErrorOutlineIcon />,
  info: <LocalShippingOutlinedIcon />,
  neutral: <RadioButtonUncheckedIcon />
};

export function toneOf(value: string | null | undefined): StatusTone {
  return (value && tones[value]) || 'neutral';
}

export default function StatusChip({ enumName, value, label }: { enumName: EnumName; value: string | null | undefined; label?: string }) {
  const language = useLanguage();
  if (!value) {
    return <>-</>;
  }

  const tone = toneOf(value);
  const color = tone === 'neutral' ? undefined : tone === 'info' ? '#2a78d6' : statusColors[tone];
  return (
    <Chip
      icon={icons[tone]}
      label={label ?? enumLabel(enumName, value, language)}
      variant="outlined"
      sx={{
        fontWeight: 600,
        borderColor: color,
        '& .MuiChip-icon': { color: color ?? 'text.secondary' },
        bgcolor: color ? `${color}14` : undefined
      }}
    />
  );
}

export function ActiveChip({ active }: { active: boolean }) {
  const language = useLanguage();
  return (
    <Chip
      icon={active ? <CheckCircleOutlineIcon /> : <RadioButtonUncheckedIcon />}
      label={active ? (language === 'ar' ? 'فعّال' : 'Active') : language === 'ar' ? 'غير فعّال' : 'Inactive'}
      color={active ? 'success' : 'default'}
      variant="outlined"
    />
  );
}
