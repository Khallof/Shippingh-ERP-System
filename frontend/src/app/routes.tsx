import { lazy } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { RedirectIfAuthenticated, RequireAuth, RequirePermission } from '@/auth/guards';
import { Permissions as P } from '@/auth/permissions.generated';
import AppLayout from '@/components/layout/AppLayout';
import LoginPage from '@/features/auth/LoginPage';
import NotFoundPage from '@/features/errors/NotFoundPage';

// Every screen is its own chunk (downloaded on first visit).
const DashboardPage = lazy(() => import('@/features/dashboard/DashboardPage'));
const ProfilePage = lazy(() => import('@/features/auth/ProfilePage'));
const ShipmentListPage = lazy(() => import('@/features/shipments/ShipmentListPage'));
const ShipmentFormPage = lazy(() => import('@/features/shipments/ShipmentFormPage'));
const ShipmentDetailsPage = lazy(() => import('@/features/shipments/ShipmentDetailsPage'));
const TransfersPage = lazy(() => import('@/features/operations/TransfersPage'));
const TransferDetailsPage = lazy(() => import('@/features/operations/TransferDetailsPage'));
const ManifestsPage = lazy(() => import('@/features/operations/ManifestsPage'));
const ManifestDetailsPage = lazy(() => import('@/features/operations/ManifestDetailsPage'));
const DriverPage = lazy(() => import('@/features/operations/DriverPage'));
const ReturnsPage = lazy(() => import('@/features/operations/ReturnsPage'));
const CustomersPage = lazy(() => import('@/features/customers/CustomersPage'));
const CustomerDetailsPage = lazy(() => import('@/features/customers/CustomerDetailsPage'));
const MasterDataPage = lazy(() => import('@/features/masterData/MasterDataPage'));
const PricingPlansPage = lazy(() => import('@/features/pricing/PricingPlansPage'));
const PricingPlanDetailsPage = lazy(() => import('@/features/pricing/PricingPlanDetailsPage'));
const PriceOverridesPage = lazy(() => import('@/features/pricing/PriceOverridesPage'));
const InvoicesPage = lazy(() => import('@/features/billing/InvoicesPage'));
const InvoiceDetailsPage = lazy(() => import('@/features/billing/InvoiceDetailsPage'));
const PaymentsPage = lazy(() => import('@/features/billing/PaymentsPage'));
const CodPage = lazy(() => import('@/features/cod/CodPage'));
const DriverBalancesPage = lazy(() => import('@/features/cod/DriverBalancesPage'));
const SettlementsPage = lazy(() => import('@/features/cod/SettlementsPage'));
const SettlementDetailsPage = lazy(() => import('@/features/cod/SettlementDetailsPage'));
const CashboxesPage = lazy(() => import('@/features/cash/CashboxesPage'));
const CashboxDetailsPage = lazy(() => import('@/features/cash/CashboxDetailsPage'));
const VouchersPage = lazy(() => import('@/features/cash/VouchersPage'));
const CashTransfersPage = lazy(() => import('@/features/cash/CashTransfersPage'));
const AccountsPage = lazy(() => import('@/features/accounting/AccountsPage'));
const JournalEntriesPage = lazy(() => import('@/features/accounting/JournalEntriesPage'));
const JournalEntryPage = lazy(() => import('@/features/accounting/JournalEntryPage'));
const PeriodsPage = lazy(() => import('@/features/accounting/PeriodsPage'));
const FinancialReportsPage = lazy(() => import('@/features/reports/FinancialReportsPage'));
const OperationalReportsPage = lazy(() => import('@/features/reports/OperationalReportsPage'));
const UsersPage = lazy(() => import('@/features/admin/UsersPage'));
const RolesPage = lazy(() => import('@/features/admin/RolesPage'));
const AuditLogsPage = lazy(() => import('@/features/admin/AuditLogsPage'));
const SettingsPage = lazy(() => import('@/features/admin/SettingsPage'));

/**
 * Routes. RequireAuth: signed-in users. RequirePermission: the same permissions as the API endpoints the page calls
 * (any = OR, all = AND). Hiding a page is only for usability: the API authorizes every request again.
 */
export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<RedirectIfAuthenticated><LoginPage /></RedirectIfAuthenticated>} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />
          <Route path="profile" element={<ProfilePage />} />

          {/* Operations */}
          <Route element={<RequirePermission any={[P.Shipments.View]} />}>
            <Route path="shipments" element={<ShipmentListPage />} />
            <Route path="shipments/:id" element={<ShipmentDetailsPage />} />
          </Route>
          <Route path="shipments/new" element={<RequirePermission any={[P.Shipments.Create]}><ShipmentFormPage /></RequirePermission>} />
          <Route path="shipments/:id/edit" element={<RequirePermission any={[P.Shipments.Update]}><ShipmentFormPage /></RequirePermission>} />
          <Route element={<RequirePermission any={[P.Transfers.View]} />}>
            <Route path="transfers" element={<TransfersPage />} />
            <Route path="transfers/:id" element={<TransferDetailsPage />} />
          </Route>
          <Route element={<RequirePermission all={[P.Shipments.View]} any={[P.DeliveryManifests.Manage]} />}>
            <Route path="manifests" element={<ManifestsPage />} />
            <Route path="manifests/:id" element={<ManifestDetailsPage />} />
          </Route>
          <Route path="driver" element={<RequirePermission any={[P.Shipments.ConfirmDelivery]}><DriverPage /></RequirePermission>} />
          <Route path="returns" element={<RequirePermission any={[P.Returns.Manage]}><ReturnsPage /></RequirePermission>} />

          {/* Master data and pricing */}
          <Route element={<RequirePermission any={[P.Customers.View]} />}>
            <Route path="customers" element={<CustomersPage />} />
            <Route path="customers/:id" element={<CustomerDetailsPage />} />
          </Route>
          <Route path="master-data" element={<MasterDataPage />} />
          <Route path="master-data/:tab" element={<MasterDataPage />} />
          <Route element={<RequirePermission any={[P.Pricing.View]} />}>
            <Route path="pricing" element={<PricingPlansPage />} />
            <Route path="pricing/overrides" element={<PriceOverridesPage />} />
            <Route path="pricing/:id" element={<PricingPlanDetailsPage />} />
          </Route>

          {/* Billing */}
          <Route element={<RequirePermission any={[P.Invoices.View]} />}>
            <Route path="invoices" element={<InvoicesPage />} />
            <Route path="invoices/:id" element={<InvoiceDetailsPage />} />
          </Route>
          <Route path="payments" element={<RequirePermission any={[P.Payments.View]}><PaymentsPage /></RequirePermission>} />

          {/* COD */}
          <Route element={<RequirePermission any={[P.Cod.View]} />}>
            <Route path="cod" element={<CodPage />} />
            <Route path="cod/driver-balances" element={<DriverBalancesPage />} />
            <Route path="cod/settlements" element={<SettlementsPage />} />
            <Route path="cod/settlements/:id" element={<SettlementDetailsPage />} />
          </Route>

          {/* Cash */}
          <Route element={<RequirePermission any={[P.Cashboxes.View]} />}>
            <Route path="cash/cashboxes" element={<CashboxesPage />} />
            <Route path="cash/cashboxes/:id" element={<CashboxDetailsPage />} />
            <Route path="cash/receipts" element={<VouchersPage kind="receipts" />} />
            <Route path="cash/payments" element={<VouchersPage kind="payments" />} />
            <Route path="cash/transfers" element={<CashTransfersPage />} />
            <Route path="expenses" element={<VouchersPage kind="expenses" />} />
          </Route>

          {/* Accounting and reports */}
          <Route element={<RequirePermission any={[P.Accounting.View]} />}>
            <Route path="accounting" element={<Navigate to="/accounting/accounts" replace />} />
            <Route path="accounting/accounts" element={<AccountsPage />} />
            <Route path="accounting/journal-entries" element={<JournalEntriesPage />} />
            <Route path="accounting/journal-entries/:id" element={<JournalEntryPage />} />
            <Route path="accounting/periods" element={<PeriodsPage />} />
          </Route>
          <Route path="reports/financial" element={<RequirePermission all={[P.Accounting.View, P.Reports.ViewFinancial]}><FinancialReportsPage /></RequirePermission>} />
          <Route path="reports/operations" element={<RequirePermission all={[P.Reports.ViewOperational, P.Shipments.View]}><OperationalReportsPage /></RequirePermission>} />

          {/* Administration */}
          <Route path="admin/users" element={<RequirePermission any={[P.Users.View]}><UsersPage /></RequirePermission>} />
          <Route path="admin/roles" element={<RequirePermission any={[P.Roles.Manage, P.Users.View]}><RolesPage /></RequirePermission>} />
          <Route path="admin/audit-logs" element={<RequirePermission any={[P.AuditLogs.View]}><AuditLogsPage /></RequirePermission>} />
          <Route path="settings" element={<SettingsPage />} />

          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Route>
    </Routes>
  );
}
