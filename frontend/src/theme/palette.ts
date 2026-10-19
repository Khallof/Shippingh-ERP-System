/**
 * Chart and status colors (validated palette, see the guide). Categorical hues are assigned in this FIXED order and
 * follow the entity (the same status keeps its color when a filter hides others). Status colors are reserved for
 * state and are always shown with a label.
 */
export const categorical = {
  light: ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#008300', '#6250d6', '#e34948'],
  dark: ['#3987e5', '#d95926', '#199e70', '#c98500', '#d55181', '#008300', '#9085e9', '#e66767']
} as const;

export const statusColors = {
  good: '#0ca30c',
  warning: '#fab219',
  serious: '#ec835a',
  critical: '#d03b3b'
} as const;

export const surfaces = { light: '#fcfcfb', dark: '#1a1a19' } as const;

export type StatusTone = keyof typeof statusColors | 'neutral' | 'info';
