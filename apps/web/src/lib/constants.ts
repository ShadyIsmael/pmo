import { config } from "@/lib/config";
export const APP_NAME = "NEXUS PMO";
export const APP_TAGLINE = "Plan. Govern. Execute. Optimize.";
export const APP_NAME_CONFIG_KEY = "app.name";
export const APP_VERSION = "1.0.0";
export { config } from "@/lib/config";
export const API_BASE_URL = config.apiBaseUrl;
export const DEFAULT_LOCALE = config.defaultLocale;

// Locale configuration — single source of truth for enabled locales.
export const SUPPORTED_LOCALES = config.supportedLocales;

// Full set of locales the app is translated to. Used by the middleware to
// redirect requests that target a disabled locale back to the default locale.
export const ALL_KNOWN_LOCALES = ["en", "ar"] as const;

export const LOCALE_NAMES: Record<string, string> = {
  en: "English",
  ar: "العربية",
};
export const VIEWS = {
  EXECUTIVE: "executive",
  PORTFOLIO_MANAGER: "portfolio-manager",
  PROJECT_MANAGER: "project-manager",
  TEAM_MEMBER: "team-member",
} as const;
export const VIEW_LABELS: Record<string, string> = {
  "executive": "Executive",
  "portfolio-manager": "Portfolio Manager",
  "project-manager": "Project Manager",
  "team-member": "Team Member",
};
export const DEFAULT_PAGE_SIZE = config.defaultPageSize;
export const PROJECT_DETAIL_PREVIEW_SIZE = Number.isFinite(config.projectDetailPreviewSize)
  ? Math.min(100, Math.max(1, config.projectDetailPreviewSize))
  : 5;
export const DEBOUNCE_DELAY = 300;
export const QUERY_STALE_TIME = config.queryStaleTime;
