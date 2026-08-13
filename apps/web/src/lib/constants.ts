import { config } from "@/lib/config";
export const APP_NAME = "NEXUS PMO";
export const APP_TAGLINE = "Plan. Govern. Execute. Optimize.";
export const APP_NAME_CONFIG_KEY = "app.name";
export const APP_VERSION = "1.0.0";
export { config } from "@/lib/config";
export const API_BASE_URL = config.apiBaseUrl;
export const DEFAULT_LOCALE = config.defaultLocale;

// Locale configuration — single source of truth for enabled locales.
// Arabic is temporarily disabled: keep ONLY "en" here.
// To re-enable Arabic later, restore: export const SUPPORTED_LOCALES = ["en", "ar"] as const;
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
export const DEBOUNCE_DELAY = 300;
export const QUERY_STALE_TIME = config.queryStaleTime;




export const PROJECT_STATUSES = ["Initiation", "Planning", "InProgress", "OnHold", "Completed", "Cancelled", "Closed"] as const;
export const PROJECT_PRIORITIES = ["Low", "Medium", "High", "Critical"] as const;
export const RAG_VALUES = ["Green", "Amber", "Red"] as const;
export const CONFIDENCE_LEVELS = ["Low", "Medium", "High"] as const;
export const HANDOVER_STATUSES = ["Pending", "Accepted", "Rejected", "Cancelled"] as const;
export const RESOURCE_TYPES = ["Employee", "Contractor", "Vendor"] as const;
