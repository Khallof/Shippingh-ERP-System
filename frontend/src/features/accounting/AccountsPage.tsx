import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Box, Button, Card, Checkbox, FormControlLabel, IconButton, Table, TableBody, TableCell, TableHead, TableRow, TextField, Tooltip } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { AccountDto, AccountTreeNodeDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import ErrorAlert from '@/components/common/ErrorAlert';
import { ActiveChip } from '@/components/common/StatusChip';
import { useNotify } from '@/components/common/Notifier';
import FormDialog from '@/components/form/FormDialog';
import type { FieldDef } from '@/components/form/fields';
import { enumLabel } from '@/i18n/enums';
import { useLanguage, useTx } from '@/i18n';
import { formatMoney } from '@/utils/format';

/** Chart of accounts as a tree with balances; header accounts group postable accounts. */
export default function AccountsPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [includeInactive, setIncludeInactive] = useState(false);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const [filter, setFilter] = useState('');
  const [editing, setEditing] = useState<{ account?: AccountDto; parentId?: number | null } | null>(null);

  const tree = useQuery({
    queryKey: ['/accounting/accounts', 'tree', includeInactive],
    queryFn: () => api.get<AccountTreeNodeDto[]>('/accounting/accounts/tree', { includeInactive, includeBalances: true })
  });

  // Flatten the visible part of the tree (all expanded while searching).
  const rows = useMemo(() => {
    const result: AccountTreeNodeDto[] = [];
    const text = filter.trim().toLowerCase();
    const matches = (node: AccountTreeNodeDto): boolean => !text || node.code.toLowerCase().startsWith(text) || node.arabicName.toLowerCase().includes(text) || node.children.some(matches);
    const walk = (nodes: AccountTreeNodeDto[]) => nodes.filter(matches).forEach((node) => {
      result.push(node);
      if (text || expanded.has(node.id)) walk(node.children);
    });
    walk(tree.data ?? []);
    return result;
  }, [tree.data, expanded, filter]);

  const fields: FieldDef[] = [
    { name: 'code', label: tx('رقم الحساب', 'Account code'), required: true, span: 3 },
    { name: 'arabicName', label: tx('الاسم بالعربية', 'Arabic name'), required: true, span: 5 },
    { name: 'englishName', label: tx('الاسم بالإنجليزية', 'English name'), span: 4 },
    { name: 'accountType', label: tx('النوع', 'Type'), type: 'enum', enumName: 'AccountType', required: true, span: 4 },
    { name: 'parentAccountId', label: tx('الحساب الأب', 'Parent account'), type: 'lookup', lookup: 'parentAccounts', span: 4 },
    { name: 'accountCategoryId', label: tx('التصنيف', 'Category'), type: 'lookup', lookup: 'accountCategories', span: 4 },
    { name: 'currencyId', label: tx('عملة الحساب (اختياري)', 'Account currency (optional)'), type: 'lookup', lookup: 'currencies' },
    { name: 'description', label: tx('الوصف', 'Description') },
    { name: 'isPostable', label: tx('حساب فرعي يقبل الترحيل', 'Postable (leaf) account'), type: 'checkbox', span: 4 },
    { name: 'allowManualPosting', label: tx('يسمح بالقيود اليدوية', 'Allows manual entries'), type: 'checkbox', span: 4 },
    { name: 'isCashAccount', label: tx('حساب نقدية', 'Cash account'), type: 'checkbox', span: 4 },
    { name: 'requiresCostCenter', label: tx('يتطلب مركز تكلفة', 'Requires cost center'), type: 'checkbox', span: 4 },
    { name: 'isActive', label: t('common.active'), type: 'checkbox', span: 4 }
  ];

  const open = async (node?: AccountTreeNodeDto, asParent = false) => {
    if (node && !asParent) {
      try {
        setEditing({ account: await api.get<AccountDto>(`/accounting/accounts/${node.id}`) });
      } catch (error) {
        notify.error(error);
      }
    } else {
      setEditing({ parentId: node?.id ?? null });
    }
  };

  const canManage = hasPermission(P.Accounting.ManageChart);
  const Chevron = language === 'ar' ? ChevronLeftIcon : ChevronRightIcon;

  return (
    <>
      <PageHeader title={t('nav.chartOfAccounts')} actions={
        <>
          <TextField placeholder={tx('رقم أو اسم الحساب', 'Code or name')} value={filter} onChange={(e) => setFilter(e.target.value)} sx={{ width: 240 }} />
          <FormControlLabel control={<Checkbox checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)} />} label={tx('غير الفعّالة', 'Inactive')} />
          <Button onClick={() => setExpanded(new Set(collectIds(tree.data ?? [])))}>{tx('توسيع الكل', 'Expand all')}</Button>
          {canManage && <Button variant="contained" startIcon={<AddIcon />} onClick={() => void open()}>{tx('حساب جديد', 'New account')}</Button>}
        </>
      } />
      {tree.error && <ErrorAlert error={tree.error} />}
      <Card>
        <Box sx={{ overflowX: 'auto', maxHeight: 'calc(100vh - 220px)' }}>
          <Table size="small" stickyHeader>
            <TableHead><TableRow><TableCell>{tx('الحساب', 'Account')}</TableCell><TableCell>{tx('النوع', 'Type')}</TableCell><TableCell>{tx('ترحيل', 'Postable')}</TableCell><TableCell>{tx('الرصيد', 'Balance')}</TableCell><TableCell>{t('common.status')}</TableCell><TableCell /></TableRow></TableHead>
            <TableBody>
              {rows.map((node) => (
                <TableRow key={node.id} hover>
                  <TableCell sx={{ paddingInlineStart: `${(node.level - 1) * 24 + 8}px`, fontWeight: node.isPostable ? 400 : 700 }}>
                    {node.children.length > 0 ? (
                      <IconButton size="small" onClick={() => setExpanded((current) => { const next = new Set(current); if (next.has(node.id)) next.delete(node.id); else next.add(node.id); return next; })}>
                        {expanded.has(node.id) || filter ? <ExpandMoreIcon fontSize="small" /> : <Chevron fontSize="small" />}
                      </IconButton>
                    ) : <Box component="span" sx={{ display: 'inline-block', width: 34 }} />}
                    <span dir="ltr">{node.code}</span> - {node.arabicName}
                  </TableCell>
                  <TableCell>{enumLabel('AccountType', node.accountType, language)}</TableCell>
                  <TableCell>{node.isPostable ? '✓' : ''}</TableCell>
                  <TableCell sx={{ fontVariantNumeric: 'tabular-nums' }}>{formatMoney(node.balance)}</TableCell>
                  <TableCell><ActiveChip active={node.isActive} /></TableCell>
                  <TableCell align="left" sx={{ whiteSpace: 'nowrap' }}>
                    {canManage && <Tooltip title={t('common.edit')}><IconButton size="small" onClick={() => void open(node)}><EditOutlinedIcon fontSize="small" /></IconButton></Tooltip>}
                    {canManage && !node.isPostable && <Tooltip title={tx('حساب فرعي', 'Child account')}><IconButton size="small" onClick={() => void open(node, true)}><AddIcon fontSize="small" /></IconButton></Tooltip>}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      </Card>
      <FormDialog<Record<string, unknown>>
        open={!!editing}
        title={editing?.account ? `${t('common.edit')} ${editing.account.code}` : tx('حساب جديد', 'New account')}
        fields={fields}
        editing={!!editing?.account}
        defaultValues={editing?.account ? { ...editing.account } : { code: '', arabicName: '', englishName: null, accountType: 'Expenses', parentAccountId: editing?.parentId ?? null, accountCategoryId: null, currencyId: null, description: null, isPostable: true, allowManualPosting: true, isCashAccount: false, requiresCostCenter: false, isActive: true }}
        onClose={() => setEditing(null)}
        onSubmit={async (values) => {
          if (editing?.account) await api.put(`/accounting/accounts/${editing.account.id}`, values);
          else await api.post('/accounting/accounts', values);
          notify.success(t('common.saved'));
          setEditing(null);
          await queryClient.invalidateQueries({ queryKey: ['/accounting/accounts'] });
        }}
      />
    </>
  );
}

function collectIds(nodes: AccountTreeNodeDto[]): number[] {
  return nodes.flatMap((node) => [node.id, ...collectIds(node.children)]);
}
