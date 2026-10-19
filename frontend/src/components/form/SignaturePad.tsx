import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { Box, Button, Stack, Typography } from '@mui/material';
import { useTx } from '@/i18n';

export interface SignaturePadHandle {
  /** PNG of the signature, or null when nothing was drawn. */
  toBlob(): Promise<Blob | null>;
  clear(): void;
}

/** Recipient signature on a canvas (finger, stylus or mouse), sent as a PNG with the proof of delivery. */
const SignaturePad = forwardRef<SignaturePadHandle, { height?: number }>(function SignaturePad({ height = 180 }, ref) {
  const tx = useTx();
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawing = useRef(false);
  const [empty, setEmpty] = useState(true);

  useEffect(() => {
    const canvas = canvasRef.current!;
    const ratio = window.devicePixelRatio || 1;
    canvas.width = canvas.offsetWidth * ratio;
    canvas.height = height * ratio;
    const context = canvas.getContext('2d')!;
    context.scale(ratio, ratio);
    context.lineWidth = 2.2;
    context.lineCap = 'round';
    context.lineJoin = 'round';
    context.strokeStyle = '#111';
  }, [height]);

  const point = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  };

  const clear = () => {
    const canvas = canvasRef.current!;
    canvas.getContext('2d')!.clearRect(0, 0, canvas.width, canvas.height);
    setEmpty(true);
  };

  useImperativeHandle(ref, () => ({
    clear,
    toBlob: () => new Promise((resolve) => (empty ? resolve(null) : canvasRef.current!.toBlob((blob) => resolve(blob), 'image/png')))
  }));

  return (
    <Box>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
        <Typography variant="body2" color="text.secondary">{tx('توقيع المستلم', 'Recipient signature')}</Typography>
        <Button size="small" onClick={clear} disabled={empty}>{tx('مسح', 'Clear')}</Button>
      </Stack>
      <Box
        component="canvas"
        ref={canvasRef}
        sx={{ width: '100%', height, border: 1, borderColor: 'divider', borderRadius: 1, bgcolor: '#fff', touchAction: 'none', display: 'block' }}
        onPointerDown={(event: React.PointerEvent<HTMLCanvasElement>) => {
          drawing.current = true;
          event.currentTarget.setPointerCapture(event.pointerId);
          const context = event.currentTarget.getContext('2d')!;
          const { x, y } = point(event);
          context.beginPath();
          context.moveTo(x, y);
        }}
        onPointerMove={(event: React.PointerEvent<HTMLCanvasElement>) => {
          if (!drawing.current) return;
          const context = event.currentTarget.getContext('2d')!;
          const { x, y } = point(event);
          context.lineTo(x, y);
          context.stroke();
          setEmpty(false);
        }}
        onPointerUp={() => (drawing.current = false)}
        onPointerLeave={() => (drawing.current = false)}
      />
    </Box>
  );
});

export default SignaturePad;
