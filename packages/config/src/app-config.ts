export interface FeatureFlags {
  aiPlanning: boolean;
  resourceManagement: boolean;
  portfolioManagement: boolean;
  timeTracking: boolean;
  ganttCharts: boolean;
  reports: boolean;
  integrations: boolean;
  workflowAutomation: boolean;
  documentManagement: boolean;
  riskManagement: boolean;
  costManagement: boolean;
}

export interface AppDefaults {
  pageSize: number;
  dateFormat: string;
  dateTimeFormat: string;
  timeFormat: string;
  currencyCode: string;
  locale: string;
  timezone: string;
}

export interface AppConfig {
  appName: string;
  appTagline: string;
  version: string;
  supportEmail: string;
  defaultLocale: string;
  availableLocales: readonly string[];
  features: FeatureFlags;
  defaults: AppDefaults;
}

export const appConfig: AppConfig = {
  appName: "NEXUS PMO",
  appTagline: "Enterprise Project Portfolio Management",
  version: "1.0.0",
  supportEmail: "support@nexuspmo.com",
  defaultLocale: "en",
  availableLocales: ["en", "ar"],
  features: {
    aiPlanning: true,
    resourceManagement: true,
    portfolioManagement: true,
    timeTracking: true,
    ganttCharts: true,
    reports: true,
    integrations: true,
    workflowAutomation: true,
    documentManagement: true,
    riskManagement: true,
    costManagement: true,
  },
  defaults: {
    pageSize: 20,
    dateFormat: "yyyy-MM-dd",
    dateTimeFormat: "yyyy-MM-dd HH:mm",
    timeFormat: "HH:mm",
    currencyCode: "GBP",
    locale: "en",
    timezone: "UTC",
  },
};
