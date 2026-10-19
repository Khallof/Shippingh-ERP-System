import type { ReactNode } from 'react';
import { Card, CardContent, CardHeader, Divider } from '@mui/material';

/** A titled card that groups a part of a page. */
export default function Section({ title, action, children, noPadding }: { title?: ReactNode; action?: ReactNode; children: ReactNode; noPadding?: boolean }) {
  return (
    <Card sx={{ mb: 2 }}>
      {title && (
        <>
          <CardHeader title={title} action={action} titleTypographyProps={{ variant: 'h6' }} sx={{ py: 1.25 }} />
          <Divider />
        </>
      )}
      {noPadding ? children : <CardContent>{children}</CardContent>}
    </Card>
  );
}
