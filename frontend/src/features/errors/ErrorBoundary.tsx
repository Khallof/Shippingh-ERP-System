import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Alert, AlertTitle, Box, Button } from '@mui/material';

/** Last line of defence: a rendering error shows a message instead of a blank page. */
export default class ErrorBoundary extends Component<{ children: ReactNode }, { error: Error | null }> {
  state = { error: null as Error | null };

  static getDerivedStateFromError(error: Error) {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('UI error', error, info.componentStack);
  }

  render() {
    if (this.state.error) {
      return (
        <Box sx={{ p: 4 }}>
          <Alert severity="error" action={<Button color="inherit" onClick={() => window.location.reload()}>إعادة التحميل / Reload</Button>}>
            <AlertTitle>حدث خطأ في الواجهة / Something went wrong</AlertTitle>
            {this.state.error.message}
          </Alert>
        </Box>
      );
    }

    return this.props.children;
  }
}
