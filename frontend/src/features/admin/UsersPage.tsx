import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Chip, Stack } from '@mui/material';
import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { RoleDto, UserDto } from '@/api/generated';
import { Permissions as P } from '@/auth/permissions.generated';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import type { FieldDef } from '@/components/form/fields';
import { useTx } from '@/i18n';

type Row = UserDto & GridValidRowModel;

/**
 * Users. Passwords are set by the API with ASP.NET Core Identity hashing (never stored or shown here); the policy is
 * 8+ characters with upper, lower, digit and symbol. Deactivating a user ends his sessions.
 */
export default function UsersPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const roles = useQuery({ queryKey: ['/roles'], queryFn: () => api.get<RoleDto[]>('/roles') });
  const roleOptions = useMemo(() => (roles.data ?? []).filter((r) => r.isActive).map((r) => ({ value: r.name, label: `${r.arabicName} (${r.name})` })), [roles.data]);

  const fields: FieldDef[] = [
    { name: 'userName', label: tx('اسم المستخدم', 'User name'), required: true, createOnly: true, span: 4 },
    { name: 'email', label: tx('البريد', 'E-mail'), type: 'email', required: true, span: 4 },
    { name: 'phoneNumber', label: tx('الهاتف', 'Phone'), span: 4 },
    { name: 'arabicFullName', label: tx('الاسم بالعربية', 'Arabic name'), required: true },
    { name: 'fullName', label: tx('الاسم بالإنجليزية', 'English name'), required: true },
    { name: 'password', label: tx('كلمة المرور الأولية', 'Initial password'), type: 'password', required: true, hidden: (v) => !!v.id, helperText: tx('8 أحرف على الأقل: كبير وصغير ورقم ورمز', '8+ chars: upper, lower, digit, symbol') },
    { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' },
    { name: 'employeeCode', label: tx('رمز الموظف', 'Employee code') },
    { name: 'roles', label: t('auth.roles'), type: 'multiselect', options: roleOptions, required: true, span: 12 }
  ];

  return (
    <ResourcePage<Row>
      config={{
        title: t('nav.users'),
        url: '/users',
        managePermission: P.Users.Manage,
        canActivate: true,
        defaultSort: 'userName',
        defaultDirection: 'asc',
        columns: [
          col.text('userName', tx('اسم المستخدم', 'User name'), S),
          col.text('arabicFullName', tx('الاسم', 'Name'), S),
          col.text('email', tx('البريد', 'E-mail'), S),
          col.text('roles', t('auth.roles'), { minWidth: 220, renderCell: ({ row }) => <Stack direction="row" spacing={0.5}>{row.roles.map((r) => <Chip key={r} label={r} />)}</Stack> }),
          col.dateTime('lastLoginAtUtc', t('auth.lastLogin'), S),
          col.bool('isLockedOut', tx('مقفل', 'Locked')),
          col.active('isActive', t('common.status'))
        ],
        filters: [
          { name: 'role', label: t('auth.roles'), type: 'select', options: roleOptions.map((o) => ({ value: String(o.value), label: o.label })) },
          { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' },
          { name: 'isActive', label: t('common.active'), type: 'boolean' }
        ],
        form: {
          fields,
          toValues: (row) => row
            ? { id: row.id, userName: row.userName, email: row.email, phoneNumber: row.phoneNumber, arabicFullName: row.arabicFullName, fullName: row.fullName, branchId: row.branchId, employeeCode: row.employeeCode, roles: row.roles }
            : { userName: '', email: '', phoneNumber: null, arabicFullName: '', fullName: '', password: '', branchId: null, employeeCode: null, roles: [] },
          toRequest: ({ id, userName, password, ...values }) => (id ? values : { ...values, userName, password })
        }
      }}
    />
  );
}
