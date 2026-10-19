import type { ReactNode } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { useAuth } from './AuthProvider';
import ForbiddenPage from '@/features/errors/ForbiddenPage';

function FullScreenLoader() {
  return (
    <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '100vh' }}>
      <CircularProgress />
    </Box>
  );
}

/** Signed-in users only; others go to /login?returnTo=... */
export function RequireAuth() {
  const { status } = useAuth();
  const location = useLocation();

  if (status === 'loading') {
    return <FullScreenLoader />;
  }

  if (status === 'anonymous') {
    const returnTo = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/login?returnTo=${returnTo}`} replace />;
  }

  return <Outlet />;
}

/** The login page only for signed-out users. */
export function RedirectIfAuthenticated({ children }: { children: ReactNode }) {
  const { status } = useAuth();
  if (status === 'loading') {
    return <FullScreenLoader />;
  }

  return status === 'authenticated' ? <Navigate to="/" replace /> : <>{children}</>;
}

/**
 * Route guard: any of the listed permissions (OR); "all" requires every one (AND).
 * Without permission it shows the 403 page inside the layout (the menu stays usable).
 */
export function RequirePermission({ any, all, children }: { any?: string[]; all?: string[]; children?: ReactNode }) {
  const { hasAny, hasPermission } = useAuth();
  const allowed = (!any || hasAny(...any)) && (!all || all.every(hasPermission));
  if (!allowed) {
    return <ForbiddenPage />;
  }

  return children ? <>{children}</> : <Outlet />;
}

/** Shows its children only with the permission (buttons, menu items). The API still checks the call. */
export function Can({ permission, any, children, fallback = null }: { permission?: string; any?: string[]; children: ReactNode; fallback?: ReactNode }) {
  const { hasPermission, hasAny } = useAuth();
  const allowed = (permission ? hasPermission(permission) : true) && (any ? hasAny(...any) : true);
  return <>{allowed ? children : fallback}</>;
}
