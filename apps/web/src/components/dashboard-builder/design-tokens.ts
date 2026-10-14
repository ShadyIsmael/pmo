/**
 * Design Tokens for Premium Dashboard
 * Enterprise, Premium, and Creative Design System
 */

export const colors = {
  // Primary - Deep Blue
  primary: {
    50: '#EFF6FF',
    100: '#DBEAFE',
    200: '#BFDBFE',
    300: '#93C5FD',
    400: '#60A5FA',
    500: '#3B82F6',
    600: '#2563EB',
    700: '#1D4ED8',
    800: '#1E40AF',
    900: '#0F172A',
  },
  // Semantic Colors
  success: {
    50: '#ECFDF5',
    100: '#D1FAE5',
    200: '#A7F3D0',
    300: '#6EE7B7',
    400: '#34D399',
    500: '#10B981',
    600: '#059669',
    700: '#047857',
    800: '#065F46',
    900: '#064E3B',
  },
  warning: {
    50: '#FFFBEB',
    100: '#FEF3C7',
    200: '#FDE68A',
    300: '#FCD34D',
    400: '#FBBF24',
    500: '#F59E0B',
    600: '#D97706',
    700: '#B45309',
    800: '#92400E',
    900: '#78350F',
  },
  danger: {
    50: '#FEF2F2',
    100: '#FEE2E2',
    200: '#FECACA',
    300: '#FCA5A5',
    400: '#F87171',
    500: '#EF4444',
    600: '#DC2626',
    700: '#B91C1C',
    800: '#991B1B',
    900: '#7F1D1D',
  },
  info: {
    50: '#ECFEFF',
    100: '#CFFAFE',
    200: '#A5F3FC',
    300: '#67E8F9',
    400: '#22D3EE',
    500: '#06B6D4',
    600: '#0891B2',
    700: '#0E7490',
    800: '#155E75',
    900: '#164E63',
  },
  // Surface Colors
  surface: {
    0: '#FFFFFF',
    1: '#F8FAFC',
    2: '#F1F5F9',
    3: '#E2E8F0',
    4: '#CBD5E1',
    5: '#94A3B8',
  },
  // Text Colors
  text: {
    primary: '#0F172A',
    secondary: '#475569',
    tertiary: '#64748B',
    muted: '#94A3B8',
    inverse: '#FFFFFF',
  },
};

export const spacing = {
  0: '0',
  1: '4px',
  2: '8px',
  3: '12px',
  4: '16px',
  5: '20px',
  6: '24px',
  8: '32px',
  10: '40px',
  12: '48px',
  16: '64px',
  20: '80px',
  24: '96px',
};

export const borderRadius = {
  none: '0',
  sm: '4px',
  md: '8px',
  lg: '12px',
  xl: '16px',
  '2xl': '24px',
  full: '9999px',
};

export const shadows = {
  none: 'none',
  sm: '0 1px 2px rgba(0, 0, 0, 0.05)',
  md: '0 4px 6px rgba(0, 0, 0, 0.07), 0 2px 4px rgba(0, 0, 0, 0.06)',
  lg: '0 10px 15px rgba(0, 0, 0, 0.1), 0 4px 6px rgba(0, 0, 0, 0.05)',
  xl: '0 20px 25px rgba(0, 0, 0, 0.15), 0 10px 10px rgba(0, 0, 0, 0.04)',
  '2xl': '0 25px 50px rgba(0, 0, 0, 0.25)',
  inner: 'inset 0 2px 4px rgba(0, 0, 0, 0.06)',
};

export const typography = {
  fontFamily: {
    sans: 'Inter, system-ui, -apple-system, sans-serif',
    mono: 'JetBrains Mono, Consolas, monospace',
  },
  fontSize: {
    xs: '11px',
    sm: '12px',
    base: '14px',
    lg: '16px',
    xl: '18px',
    '2xl': '24px',
    '3xl': '32px',
  },
  fontWeight: {
    regular: '400',
    medium: '500',
    semibold: '600',
    bold: '700',
  },
  lineHeight: {
    tight: '1.25',
    normal: '1.5',
    relaxed: '1.75',
  },
  letterSpacing: {
    tighter: '-0.025em',
    tight: '-0.01em',
    normal: '0',
    wide: '0.025em',
    wider: '0.05em',
    widest: '0.1em',
  },
};

export const transitions = {
  duration: {
    fast: '150ms',
    normal: '200ms',
    slow: '300ms',
  },
  easing: {
    ease: 'cubic-bezier(0.4, 0, 0.2, 1)',
    easeIn: 'cubic-bezier(0.4, 0, 1, 1)',
    easeOut: 'cubic-bezier(0, 0, 0.2, 1)',
    easeInOut: 'cubic-bezier(0.4, 0, 0.2, 1)',
  },
};

export const gradients = {
  primary: 'linear-gradient(135deg, #3B82F6 0%, #2563EB 100%)',
  success: 'linear-gradient(135deg, #10B981 0%, #059669 100%)',
  warning: 'linear-gradient(135deg, #F59E0B 0%, #D97706 100%)',
  danger: 'linear-gradient(135deg, #EF4444 0%, #DC2626 100%)',
  info: 'linear-gradient(135deg, #06B6D4 0%, #0891B2 100%)',
  subtle: 'linear-gradient(135deg, #F8FAFC 0%, #F1F5F9 100%)',
};

export const widget = {
  sizes: {
    small: { w: 2, h: 2 }, // KPI cards
    medium: { w: 3, h: 3 }, // Charts
    large: { w: 4, h: 4 }, // Complex widgets
    fullWidth: { w: 12, h: 2 }, // Horizontal widgets
  },
  padding: {
    header: '16px',
    content: '16px',
  },
  headerHeight: '48px',
  borderRadius: '12px',
  gap: '24px',
};

export const breakpoints = {
  sm: '640px',
  md: '768px',
  lg: '1024px',
  xl: '1280px',
  '2xl': '1536px',
};

// CSS Custom Properties Map
export const cssVars = {
  '--color-primary-50': colors.primary[50],
  '--color-primary-500': colors.primary[500],
  '--color-primary-900': colors.primary[900],
  '--color-success-500': colors.success[500],
  '--color-warning-500': colors.warning[500],
  '--color-danger-500': colors.danger[500],
  '--color-surface-0': colors.surface[0],
  '--color-surface-1': colors.surface[1],
  '--color-surface-2': colors.surface[2],
  '--color-surface-3': colors.surface[3],
  '--color-text-primary': colors.text.primary,
  '--color-text-secondary': colors.text.secondary,
  '--color-text-muted': colors.text.muted,
  '--spacing-1': spacing[1],
  '--spacing-2': spacing[2],
  '--spacing-3': spacing[3],
  '--spacing-4': spacing[4],
  '--spacing-6': spacing[6],
  '--border-radius-lg': borderRadius.lg,
  '--border-radius-xl': borderRadius.xl,
  '--shadow-sm': shadows.sm,
  '--shadow-md': shadows.md,
  '--shadow-lg': shadows.lg,
  '--shadow-xl': shadows.xl,
  '--font-family-sans': typography.fontFamily.sans,
  '--transition-duration-normal': transitions.duration.normal,
};
