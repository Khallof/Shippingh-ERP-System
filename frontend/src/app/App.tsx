import AppProviders from './AppProviders';
import AppRoutes from './routes';
import ErrorBoundary from '@/features/errors/ErrorBoundary';

export default function App() {
  return (
    <ErrorBoundary>
      <AppProviders>
        <AppRoutes />
      </AppProviders>
    </ErrorBoundary>
  );
}
