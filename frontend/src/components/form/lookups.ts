/** Server lists used by selects. label builds the option text from a row. */
export interface LookupSource {
  url: string;
  params?: Record<string, string | number | boolean>;
  label?: (row: Record<string, unknown>) => string;
  /** The API returns a plain array (no paging), e.g. /roles. */
  unpaged?: boolean;
}

const codeName = (row: Record<string, unknown>) =>
  [row.code ?? row.plateNumber ?? row.employeeCode, row.arabicName ?? row.employeeName ?? row.model ?? row.arabicFullName ?? row.name]
    .filter((part) => part !== null && part !== undefined && part !== '')
    .join(' - ');

const active = { isActive: true };

export const lookups = {
  branches: { url: '/branches', params: active, label: codeName },
  warehouses: { url: '/warehouses', params: active, label: codeName },
  countries: { url: '/countries', params: active, label: codeName },
  cities: { url: '/cities', params: active, label: codeName },
  zones: { url: '/zones', params: active, label: codeName },
  serviceTypes: { url: '/service-types', params: active, label: codeName },
  customers: { url: '/customers', params: active, label: (row) => `${row.code} - ${row.arabicName} (${row.phone})` },
  senders: { url: '/customers', params: { ...active, role: 'Sender' }, label: (row) => `${row.code} - ${row.arabicName} (${row.phone})` },
  drivers: { url: '/drivers', params: active, label: (row) => `${row.employeeCode} - ${row.employeeName}` },
  vehicles: { url: '/vehicles', params: active, label: (row) => `${row.plateNumber} - ${row.model}` },
  employees: { url: '/employees', params: active, label: codeName },
  currencies: { url: '/currencies', params: active, label: codeName },
  paymentMethods: { url: '/payment-methods', params: active, label: codeName },
  taxRates: { url: '/tax-rates', params: active, label: codeName },
  expenseCategories: { url: '/expense-categories', params: active, label: codeName },
  suppliers: { url: '/suppliers', params: active, label: codeName },
  pricingPlans: { url: '/pricing-plans', params: active, label: codeName },
  cashboxes: { url: '/cashboxes', params: active, label: (row) => `${row.code} - ${row.arabicName} (${row.currencyCode})` },
  bankAccounts: { url: '/bank-accounts', params: active, label: (row) => `${row.code} - ${row.arabicName} (${row.currencyCode})` },
  accounts: { url: '/accounting/accounts', params: { ...active, isPostable: true }, label: codeName },
  parentAccounts: { url: '/accounting/accounts', params: { ...active, isPostable: false }, label: codeName },
  accountCategories: { url: '/accounting/account-categories', label: codeName, unpaged: true },
  dimensions: { url: '/accounting/dimensions', params: active, label: codeName },
  users: { url: '/users', params: active, label: (row) => `${row.userName} - ${row.arabicFullName}` },
  roles: { url: '/roles', unpaged: true, label: (row) => `${row.arabicName} (${row.name})` }
} satisfies Record<string, LookupSource>;

export type LookupName = keyof typeof lookups;
