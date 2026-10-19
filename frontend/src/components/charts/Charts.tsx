import { Box, useTheme } from '@mui/material';
import {
  Bar, BarChart, CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis
} from 'recharts';
import { categorical } from '@/theme/palette';
import { formatNumber } from '@/utils/format';
import { useLanguage } from '@/i18n';

export function useChartColors() {
  const theme = useTheme();
  const dark = theme.palette.mode === 'dark';
  return {
    series: dark ? categorical.dark : categorical.light,
    grid: theme.palette.divider,
    axis: theme.palette.text.secondary,
    text: theme.palette.text.primary,
    surface: theme.palette.background.paper
  };
}

function tooltipStyle(colors: ReturnType<typeof useChartColors>) {
  return {
    contentStyle: { background: colors.surface, border: `1px solid ${colors.grid}`, borderRadius: 8, color: colors.text },
    labelStyle: { color: colors.text, fontWeight: 700 },
    itemStyle: { color: colors.text }
  };
}

export interface SeriesDef {
  key: string;
  name: string;
}

/**
 * Charts are drawn in an LTR box (dir attribute: the RTL emotion plugin would flip a CSS "direction"), and mirrored
 * explicitly for Arabic (reversed axis, axis on the right).
 *
 * Line chart over time: one y axis, 2 px lines, a legend for 2+ series, a crosshair tooltip.
 * In RTL the time axis is reversed so time reads right-to-left like the text.
 */
export function TrendChart({ data, xKey, series, height = 260 }: { data: object[]; xKey: string; series: SeriesDef[]; height?: number }) {
  const colors = useChartColors();
  const rtl = useLanguage() === 'ar';
  return (
    <Box dir="ltr" sx={{ width: '100%', height }}>
      <ResponsiveContainer>
        <LineChart data={data} margin={{ top: 8, right: 16, bottom: 0, left: 16 }}>
          <CartesianGrid stroke={colors.grid} vertical={false} />
          <XAxis dataKey={xKey} reversed={rtl} tick={{ fill: colors.axis, fontSize: 12 }} tickLine={false} axisLine={{ stroke: colors.grid }} />
          <YAxis orientation={rtl ? 'right' : 'left'} allowDecimals={false} tick={{ fill: colors.axis, fontSize: 12 }} tickLine={false} axisLine={false} width={40} />
          <Tooltip {...tooltipStyle(colors)} formatter={(value) => formatNumber(Number(value))} cursor={{ stroke: colors.axis, strokeDasharray: '3 3' }} />
          {series.length > 1 && <Legend wrapperStyle={{ color: colors.text }} />}
          {series.map((item, index) => (
            <Line key={item.key} type="monotone" dataKey={item.key} name={item.name} stroke={colors.series[index]} strokeWidth={2} dot={{ r: 4 }} activeDot={{ r: 6 }} />
          ))}
        </LineChart>
      </ResponsiveContainer>
    </Box>
  );
}

/**
 * Horizontal bars of one measure per category (counts by status, by branch). Single series: one hue, no legend;
 * the category names are on the axis and the value is in the tooltip.
 */
export function CategoryBarChart({ data, labelKey, valueKey, valueName, height, colorIndex = 0, formatValue }: {
  data: object[];
  labelKey: string;
  valueKey: string;
  valueName: string;
  height?: number;
  colorIndex?: number;
  formatValue?: (value: number) => string;
}) {
  const colors = useChartColors();
  const rtl = useLanguage() === 'ar';
  const computedHeight = height ?? Math.max(160, data.length * 34 + 30);
  return (
    <Box dir="ltr" sx={{ width: '100%', height: computedHeight }}>
      <ResponsiveContainer>
        <BarChart data={data} layout="vertical" margin={{ top: 4, right: 16, bottom: 4, left: 8 }} barCategoryGap={6}>
          <CartesianGrid stroke={colors.grid} horizontal={false} />
          <XAxis type="number" reversed={rtl} allowDecimals={false} tick={{ fill: colors.axis, fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis
            type="category"
            dataKey={labelKey}
            orientation={rtl ? 'right' : 'left'}
            width={150}
            tickLine={false}
            axisLine={{ stroke: colors.grid }}
            // The label sits outside the plot: after the axis in RTL (right side), before it in LTR.
            tick={({ x, y, payload }: { x: number | string; y: number | string; payload: { value: unknown } }) => (
              <text x={rtl ? Number(x) + 8 : Number(x) - 8} y={y} dy={4} textAnchor={rtl ? 'start' : 'end'} fill={colors.text} fontSize={12}>
                {String(payload.value)}
              </text>
            )}
          />
          <Tooltip {...tooltipStyle(colors)} formatter={(value) => (formatValue ? formatValue(Number(value)) : formatNumber(Number(value)))} cursor={{ fill: colors.grid, opacity: 0.4 }} />
          <Bar isAnimationActive={false} dataKey={valueKey} name={valueName} fill={colors.series[colorIndex]} radius={rtl ? [4, 0, 0, 4] : [0, 4, 4, 0]} maxBarSize={22} />
        </BarChart>
      </ResponsiveContainer>
    </Box>
  );
}
