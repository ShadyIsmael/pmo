export interface ColorTokens {
  primary: string;
  primaryLight: string;
  primaryDark: string;
  secondary: string;
  secondaryLight: string;
  secondaryDark: string;
  accent: string;
  background: string;
  surface: string;
  surfaceVariant: string;
  text: string;
  textSecondary: string;
  textDisabled: string;
  border: string;
  divider: string;
  error: string;
  warning: string;
  success: string;
  info: string;
}

export interface Spacing {
  xs: string;
  sm: string;
  md: string;
  lg: string;
  xl: string;
  xxl: string;
}

export interface Typography {
  fontFamily: string;
  fontFamilyAr: string;
  fontSizeXs: string;
  fontSizeSm: string;
  fontSizeMd: string;
  fontSizeLg: string;
  fontSizeXl: string;
  fontSizeH1: string;
  fontSizeH2: string;
  fontSizeH3: string;
  fontWeightRegular: number;
  fontWeightMedium: number;
  fontWeightBold: number;
  lineHeight: string;
}

export interface StatusColors {
  draft: string;
  active: string;
  completed: string;
  cancelled: string;
  onHold: string;
  overdue: string;
}

export interface ChartColors {
  primary: readonly string[];
  sequential: readonly string[];
  diverging: readonly string[];
  qualitative: readonly string[];
}

export interface ThemeConfig {
  colors: ColorTokens;
  spacing: Spacing;
  typography: Typography;
  statusColors: StatusColors;
  chartColors: ChartColors;
}

export const themeConfig: ThemeConfig = {
  colors: {
    primary: "#2563EB",
    primaryLight: "#3B82F6",
    primaryDark: "#1D4ED8",
    secondary: "#7C3AED",
    secondaryLight: "#8B5CF6",
    secondaryDark: "#6D28D9",
    accent: "#F59E0B",
    background: "#F8FAFC",
    surface: "#FFFFFF",
    surfaceVariant: "#F1F5F9",
    text: "#0F172A",
    textSecondary: "#64748B",
    textDisabled: "#94A3B8",
    border: "#E2E8F0",
    divider: "#CBD5E1",
    error: "#EF4444",
    warning: "#F59E0B",
    success: "#10B981",
    info: "#3B82F6",
  },
  spacing: {
    xs: "4px",
    sm: "8px",
    md: "16px",
    lg: "24px",
    xl: "32px",
    xxl: "48px",
  },
  typography: {
    fontFamily: "'Inter', -apple-system, sans-serif",
    fontFamilyAr: "'Cairo', 'Noto Naskh Arabic', sans-serif",
    fontSizeXs: "0.75rem",
    fontSizeSm: "0.875rem",
    fontSizeMd: "1rem",
    fontSizeLg: "1.125rem",
    fontSizeXl: "1.25rem",
    fontSizeH1: "2rem",
    fontSizeH2: "1.5rem",
    fontSizeH3: "1.25rem",
    fontWeightRegular: 400,
    fontWeightMedium: 500,
    fontWeightBold: 700,
    lineHeight: "1.5",
  },
  statusColors: {
    draft: "#94A3B8",
    active: "#3B82F6",
    completed: "#10B981",
    cancelled: "#EF4444",
    onHold: "#F59E0B",
    overdue: "#DC2626",
  },
  chartColors: {
    primary: ["#2563EB", "#3B82F6", "#60A5FA", "#93C5FD", "#BFDBFE"],
    sequential: ["#F0FDF4", "#BBF7D0", "#4ADE80", "#16A34A", "#166534"],
    diverging: ["#EF4444", "#F97316", "#F59E0B", "#10B981", "#2563EB"],
    qualitative: ["#2563EB", "#7C3AED", "#F59E0B", "#EF4444", "#10B981", "#EC4899", "#6366F1", "#14B8A6"],
  },
};
