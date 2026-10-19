import type { Dictionary } from './ar';

type Strings<T> = { [K in keyof T]: T[K] extends string ? string : Strings<T[K]> };

// English dictionary: same keys as ar.ts (TypeScript reports a missing or extra key).
const en: Strings<Dictionary> = {
  app: { name: 'Shipping ERP', company: 'Express Courier Co.' },
  common: {
    save: 'Save', cancel: 'Cancel', close: 'Close', delete: 'Delete', edit: 'Edit', add: 'Add', create: 'Create',
    search: 'Search', filter: 'Filter', reset: 'Reset', refresh: 'Refresh', confirm: 'Confirm', back: 'Back',
    yes: 'Yes', no: 'No', all: 'All', actions: 'Actions', details: 'Details', print: 'Print',
    exportExcel: 'Export Excel', exportPdf: 'Export PDF', loading: 'Loading…', noData: 'No data',
    active: 'Active', inactive: 'Inactive', activate: 'Activate', deactivate: 'Deactivate', status: 'Status',
    fromDate: 'From', toDate: 'To', branch: 'Branch', customer: 'Customer', driver: 'Driver',
    currency: 'Currency', amount: 'Amount', date: 'Date', notes: 'Notes', reason: 'Reason', code: 'Code',
    arabicName: 'Arabic name', englishName: 'English name', total: 'Total', saved: 'Saved',
    done: 'Done', required: 'This field is required', retry: 'Retry', language: 'Language',
    darkMode: 'Dark mode', open: 'Open', select: 'Select', view: 'View', submit: 'Submit', approve: 'Approve',
    reject: 'Reject', post: 'Post', reverse: 'Reverse', pay: 'Pay', receive: 'Receive', dispatch: 'Dispatch',
    perPage: 'per page', of: 'of', traceId: 'Trace id'
  },
  auth: {
    title: 'Sign in', userName: 'User name or e-mail', password: 'Password', signIn: 'Sign in',
    signOut: 'Sign out', profile: 'Profile', changePassword: 'Change password',
    currentPassword: 'Current password', newPassword: 'New password', confirmPassword: 'Confirm password',
    passwordChanged: 'Password changed; other devices were signed out.', sessionExpired: 'Your session expired, please sign in again.',
    forbiddenTitle: 'Access denied', forbiddenText: 'This page needs a permission your account does not have. Contact the administrator.',
    roles: 'Roles', permissions: 'Permissions', lastLogin: 'Last sign-in'
  },
  nav: {
    dashboard: 'Dashboard', operations: 'Operations', shipments: 'Shipments', newShipment: 'New shipment',
    transfers: 'Branch transfers', manifests: 'Delivery manifests', driver: 'Driver screen', returns: 'Returns',
    customers: 'Customers', masterData: 'Master data', pricing: 'Pricing', pricingPlans: 'Pricing plans and rules',
    priceOverrides: 'Price approvals', billing: 'Billing', invoices: 'Invoices', payments: 'Customer payments',
    cod: 'Cash on delivery', codDashboard: 'COD dashboard', driverBalances: 'Driver balances', settlements: 'Sender settlements',
    cash: 'Cash', cashboxes: 'Cashboxes', receipts: 'Receipt vouchers', cashPayments: 'Payment vouchers', cashTransfers: 'Cash transfers',
    expenses: 'Expenses', accounting: 'Accounting', chartOfAccounts: 'Chart of accounts', journalEntries: 'Journal entries',
    fiscalPeriods: 'Fiscal years and periods', postingRules: 'Automatic posting', reports: 'Reports', financialReports: 'Financial reports',
    operationalReports: 'Operational reports', admin: 'Administration', users: 'Users', roles: 'Roles and permissions',
    auditLogs: 'Audit log', settings: 'Settings and company'
  },
  dashboard: {
    title: 'Dashboard', todayShipments: "Today's shipments", delivered: 'Delivered today', failedToday: 'Failed today',
    openShipments: 'Open shipments', outForDelivery: 'Out for delivery', deliveredPercent: 'Delivered %',
    failedPercent: 'Failed %', byStatus: 'Shipments by status', last7Days: 'Last 7 days', created: 'Created',
    codPending: 'COD not collected', codWithDrivers: 'With drivers', codReady: 'Ready for settlement',
    cashboxes: 'Cashbox balances', revenue: 'Revenue (this month)', expenses: 'Expenses (this month)',
    netProfit: 'Estimated net profit', overdueInvoices: 'Overdue invoices', driverBalances: 'Highest driver balances',
    topCustomers: 'Top customers', recentActivity: 'Recent activity', period: 'Period (days)', receivables: 'Open receivables',
    shipmentsCount: 'Shipments', charges: 'Charges'
  },
  errors: {
    notFoundTitle: 'Page not found', notFoundText: 'The link is wrong or the page moved.', goHome: 'Back to home',
    unexpected: 'Unexpected error'
  }
};

export default en;
