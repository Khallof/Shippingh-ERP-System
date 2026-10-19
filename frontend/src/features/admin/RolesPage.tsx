import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Card, CardContent, Checkbox, Chip, Divider, FormControlLabel, Grid2 as Grid, List, ListItemButton, ListItemText, Stack, Typography
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { PermissionDto, RoleDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import { useNotify } from '@/components/common/Notifier';
import FormDialog from '@/components/form/FormDialog';
import { useTx } from '@/i18n';

/** Roles and their permissions as a matrix grouped by module (Roles.Manage); a read-only view for Users.View. */
export default function RolesPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission, reloadUser } = useAuth();
  const canManage = hasPermission(P.Roles.Manage);
  const roles = useQuery({ queryKey: ['/roles'], queryFn: () => api.get<RoleDto[]>('/roles') });
  const catalog = useQuery({ queryKey: ['/roles', 'permissions'], queryFn: () => api.get<PermissionDto[]>('/roles/permissions'), enabled: canManage });
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [draft, setDraft] = useState<Set<string> | null>(null);
  const [creating, setCreating] = useState(false);
  const role = roles.data?.find((r) => r.id === selectedId) ?? roles.data?.[0];
  const current = draft ?? new Set(role?.permissions ?? []);

  const modules = useMemo(() => {
    const map = new Map<string, PermissionDto[]>();
    (catalog.data ?? (role?.permissions ?? []).map((code) => ({ code, module: code.split('.')[0], description: code }))).forEach((p) => map.set(p.module, [...(map.get(p.module) ?? []), p]));
    return [...map.entries()];
  }, [catalog.data, role]);

  const toggle = (codes: string[], on: boolean) => {
    const next = new Set(current);
    codes.forEach((code) => (on ? next.add(code) : next.delete(code)));
    setDraft(next);
  };

  const save = async () => {
    try {
      await api.put(`/roles/${role!.id}/permissions`, { permissions: [...current] });
      notify.success(tx('تم حفظ الصلاحيات. تسري على المستخدمين عند تجديد جلستهم.', 'Saved. Users get the change when their session renews.'));
      setDraft(null);
      await queryClient.invalidateQueries({ queryKey: ['/roles'] });
      await reloadUser();
    } catch (error) {
      notify.error(error);
    }
  };

  return (
    <>
      <PageHeader title={t('nav.roles')} actions={canManage && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreating(true)}>{tx('دور جديد', 'New role')}</Button>} />
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 3 }}>
          <Card>
            <List dense>
              {roles.data?.map((r) => (
                <ListItemButton key={r.id} selected={r.id === role?.id} onClick={() => { setSelectedId(r.id); setDraft(null); }}>
                  <ListItemText primary={r.arabicName} secondary={`${r.name} · ${r.userCount} ${tx('مستخدم', 'users')}`} />
                  {r.isSystemRole && <Chip label={tx('نظام', 'System')} />}
                </ListItemButton>
              ))}
            </List>
          </Card>
        </Grid>
        <Grid size={{ xs: 12, md: 9 }}>
          {role && (
            <Card>
              <CardContent>
                <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                  <Box>
                    <Typography variant="h6">{role.arabicName}</Typography>
                    <Typography variant="body2" color="text.secondary">{role.description} · {current.size} {tx('صلاحية', 'permissions')}</Typography>
                  </Box>
                  {canManage && <Button variant="contained" disabled={!draft} onClick={() => void save()}>{t('common.save')}</Button>}
                </Stack>
                {role.name === 'Admin' && <Alert severity="info" sx={{ mb: 1 }}>{tx('دور المدير يملك كل الصلاحيات.', 'The Admin role has every permission.')}</Alert>}
                <Divider sx={{ mb: 1 }} />
                <Grid container spacing={2}>
                  {modules.map(([module, permissions]) => {
                    const codes = permissions.map((p) => p.code);
                    const all = codes.every((code) => current.has(code));
                    return (
                      <Grid key={module} size={{ xs: 12, sm: 6, lg: 4 }}>
                        <FormControlLabel disabled={!canManage} control={<Checkbox checked={all} indeterminate={!all && codes.some((c) => current.has(c))} onChange={(e) => toggle(codes, e.target.checked)} />} label={<strong>{module}</strong>} />
                        <Box sx={{ paddingInlineStart: 3 }}>
                          {permissions.map((p) => (
                            <FormControlLabel key={p.code} disabled={!canManage} sx={{ display: 'flex' }} control={<Checkbox size="small" checked={current.has(p.code)} onChange={(e) => toggle([p.code], e.target.checked)} />}
                              label={<Typography variant="body2">{p.description} <Typography component="span" variant="caption" color="text.secondary" dir="ltr">{p.code}</Typography></Typography>} />
                          ))}
                        </Box>
                      </Grid>
                    );
                  })}
                </Grid>
              </CardContent>
            </Card>
          )}
        </Grid>
      </Grid>
      <FormDialog<Record<string, unknown>>
        open={creating} maxWidth="sm" title={tx('دور جديد', 'New role')}
        fields={[
          { name: 'name', label: tx('الاسم البرمجي (لاتيني)', 'Name (latin)'), required: true },
          { name: 'arabicName', label: tx('الاسم بالعربية', 'Arabic name'), required: true },
          { name: 'description', label: tx('الوصف', 'Description'), type: 'textarea' }
        ]}
        defaultValues={{ name: '', arabicName: '', description: null }}
        onClose={() => setCreating(false)}
        onSubmit={async (values) => {
          const created = await api.post<RoleDto>('/roles', { ...values, permissions: [] });
          setCreating(false);
          await queryClient.invalidateQueries({ queryKey: ['/roles'] });
          setSelectedId(created.id);
          setDraft(null);
        }}
      />
    </>
  );
}
