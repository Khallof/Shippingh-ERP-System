import type { ReactNode } from 'react';
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import LocalShippingOutlinedIcon from '@mui/icons-material/LocalShippingOutlined';
import AddBoxOutlinedIcon from '@mui/icons-material/AddBoxOutlined';
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import AssignmentOutlinedIcon from '@mui/icons-material/AssignmentOutlined';
import TwoWheelerOutlinedIcon from '@mui/icons-material/TwoWheelerOutlined';
import AssignmentReturnOutlinedIcon from '@mui/icons-material/AssignmentReturnOutlined';
import PeopleAltOutlinedIcon from '@mui/icons-material/PeopleAltOutlined';
import StorageOutlinedIcon from '@mui/icons-material/StorageOutlined';
import PriceChangeOutlinedIcon from '@mui/icons-material/PriceChangeOutlined';
import FactCheckOutlinedIcon from '@mui/icons-material/FactCheckOutlined';
import ReceiptLongOutlinedIcon from '@mui/icons-material/ReceiptLongOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import SavingsOutlinedIcon from '@mui/icons-material/SavingsOutlined';
import BadgeOutlinedIcon from '@mui/icons-material/BadgeOutlined';
import HandshakeOutlinedIcon from '@mui/icons-material/HandshakeOutlined';
import PointOfSaleOutlinedIcon from '@mui/icons-material/PointOfSaleOutlined';
import CallReceivedIcon from '@mui/icons-material/CallReceived';
import CallMadeIcon from '@mui/icons-material/CallMade';
import CompareArrowsIcon from '@mui/icons-material/CompareArrows';
import MoneyOffOutlinedIcon from '@mui/icons-material/MoneyOffOutlined';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import MenuBookOutlinedIcon from '@mui/icons-material/MenuBookOutlined';
import EventNoteOutlinedIcon from '@mui/icons-material/EventNoteOutlined';
import AssessmentOutlinedIcon from '@mui/icons-material/AssessmentOutlined';
import QueryStatsOutlinedIcon from '@mui/icons-material/QueryStatsOutlined';
import ManageAccountsOutlinedIcon from '@mui/icons-material/ManageAccountsOutlined';
import AdminPanelSettingsOutlinedIcon from '@mui/icons-material/AdminPanelSettingsOutlined';
import HistoryOutlinedIcon from '@mui/icons-material/HistoryOutlined';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import { Permissions as P } from '@/auth/permissions.generated';

export interface NavItem {
  /** i18n key under "nav." */
  key: string;
  path: string;
  icon: ReactNode;
  /** Shown when the user has ANY of these permissions (empty = every signed-in user). */
  any?: string[];
}

export interface NavGroup {
  key: string;
  items: NavItem[];
}

/** Side menu. Items the user cannot open are hidden; the routes are guarded with the same permissions. */
export const navigation: NavGroup[] = [
  {
    key: 'operations',
    items: [
      { key: 'dashboard', path: '/', icon: <DashboardOutlinedIcon /> },
      { key: 'shipments', path: '/shipments', icon: <LocalShippingOutlinedIcon />, any: [P.Shipments.View] },
      { key: 'newShipment', path: '/shipments/new', icon: <AddBoxOutlinedIcon />, any: [P.Shipments.Create] },
      { key: 'transfers', path: '/transfers', icon: <SwapHorizIcon />, any: [P.Transfers.View] },
      { key: 'manifests', path: '/manifests', icon: <AssignmentOutlinedIcon />, any: [P.DeliveryManifests.Manage] },
      { key: 'driver', path: '/driver', icon: <TwoWheelerOutlinedIcon />, any: [P.Shipments.ConfirmDelivery] },
      { key: 'returns', path: '/returns', icon: <AssignmentReturnOutlinedIcon />, any: [P.Returns.Manage] }
    ]
  },
  {
    key: 'masterData',
    items: [
      { key: 'customers', path: '/customers', icon: <PeopleAltOutlinedIcon />, any: [P.Customers.View] },
      { key: 'masterData', path: '/master-data', icon: <StorageOutlinedIcon /> },
      { key: 'pricingPlans', path: '/pricing', icon: <PriceChangeOutlinedIcon />, any: [P.Pricing.View] },
      { key: 'priceOverrides', path: '/pricing/overrides', icon: <FactCheckOutlinedIcon />, any: [P.Pricing.View] }
    ]
  },
  {
    key: 'billing',
    items: [
      { key: 'invoices', path: '/invoices', icon: <ReceiptLongOutlinedIcon />, any: [P.Invoices.View] },
      { key: 'payments', path: '/payments', icon: <PaymentsOutlinedIcon />, any: [P.Payments.View] }
    ]
  },
  {
    key: 'cod',
    items: [
      { key: 'codDashboard', path: '/cod', icon: <SavingsOutlinedIcon />, any: [P.Cod.View] },
      { key: 'driverBalances', path: '/cod/driver-balances', icon: <BadgeOutlinedIcon />, any: [P.Cod.View] },
      { key: 'settlements', path: '/cod/settlements', icon: <HandshakeOutlinedIcon />, any: [P.Cod.View] }
    ]
  },
  {
    key: 'cash',
    items: [
      { key: 'cashboxes', path: '/cash/cashboxes', icon: <PointOfSaleOutlinedIcon />, any: [P.Cashboxes.View] },
      { key: 'receipts', path: '/cash/receipts', icon: <CallReceivedIcon />, any: [P.Cashboxes.View] },
      { key: 'cashPayments', path: '/cash/payments', icon: <CallMadeIcon />, any: [P.Cashboxes.View] },
      { key: 'cashTransfers', path: '/cash/transfers', icon: <CompareArrowsIcon />, any: [P.Cashboxes.View] },
      { key: 'expenses', path: '/expenses', icon: <MoneyOffOutlinedIcon />, any: [P.Cashboxes.View] }
    ]
  },
  {
    key: 'accounting',
    items: [
      { key: 'chartOfAccounts', path: '/accounting/accounts', icon: <AccountTreeOutlinedIcon />, any: [P.Accounting.View] },
      { key: 'journalEntries', path: '/accounting/journal-entries', icon: <MenuBookOutlinedIcon />, any: [P.Accounting.View] },
      { key: 'fiscalPeriods', path: '/accounting/periods', icon: <EventNoteOutlinedIcon />, any: [P.Accounting.View] }
    ]
  },
  {
    key: 'reports',
    items: [
      { key: 'financialReports', path: '/reports/financial', icon: <AssessmentOutlinedIcon />, any: [P.Reports.ViewFinancial] },
      { key: 'operationalReports', path: '/reports/operations', icon: <QueryStatsOutlinedIcon />, any: [P.Reports.ViewOperational] }
    ]
  },
  {
    key: 'admin',
    items: [
      { key: 'users', path: '/admin/users', icon: <ManageAccountsOutlinedIcon />, any: [P.Users.View] },
      { key: 'roles', path: '/admin/roles', icon: <AdminPanelSettingsOutlinedIcon />, any: [P.Roles.Manage, P.Users.View] },
      { key: 'auditLogs', path: '/admin/audit-logs', icon: <HistoryOutlinedIcon />, any: [P.AuditLogs.View] },
      { key: 'settings', path: '/settings', icon: <SettingsOutlinedIcon /> }
    ]
  }
];
