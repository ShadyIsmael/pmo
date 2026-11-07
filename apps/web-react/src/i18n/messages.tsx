import * as React from "react";
import enMessages from "./messages/en.json";
import arMessages from "./messages/ar.json";
import { ConfirmProvider } from "@components/ui/ConfirmProvider";
import { formatCurrencyValue } from "@lib/formatting/currency";
import { formatNumberValue } from "@lib/formatting/number";
import { formatDateTimeValue, formatDateValue } from "@lib/formatting/date";
import { getFirstDayOfWeekIndex, setRegionalDefaults, type RegionalDefaults } from "@lib/formatting/regional";
import { setApiErrorLocale } from "@api/client";

export type Locale = "en" | "ar";

const messages = {
  en: enMessages,
  ar: arMessages,
} as const;

interface I18nContextValue {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: (key: string, params?: Record<string, string | number>) => string;
  setRuntimeRegionalSettings: (settings: RegionalDefaults | null) => void;
  firstDayOfWeek: number;
  formatNumber: (value: number | bigint, options?: Intl.NumberFormatOptions) => string;
  formatCurrency: (value: number, currency?: string) => string;
  formatPercent: (value: number) => string;
  formatDate: (date: Date | string, options?: Intl.DateTimeFormatOptions) => string;
  formatDateTime: (date: Date | string) => string;
}

const I18nContext = React.createContext<I18nContextValue | null>(null);

function getNestedValue(obj: Record<string, unknown>, path: string): unknown {
  return path.split(".").reduce((current: unknown, key: string) => {
    if (current && typeof current === "object" && key in current) {
      return (current as Record<string, unknown>)[key];
    }
    return undefined;
  }, obj);
}

function interpolate(template: string, params?: Record<string, string | number>): string {
  if (!params) return template;
  return template.replace(/\{(\w+)\}/g, (_, key) => String(params[key] ?? ""));
}

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [locale, setLocaleState] = React.useState<Locale>(() => {
    if (typeof window === "undefined") return "en";
    return window.localStorage.getItem("nexus-locale") === "ar" ? "ar" : "en";
  });
  const [hasExplicitLocale, setHasExplicitLocale] = React.useState(() => {
    if (typeof window === "undefined") return false;
    const saved = window.localStorage.getItem("nexus-locale");
    return saved === "en" || saved === "ar";
  });
  const [runtimeSettings, setRuntimeSettings] = React.useState<RegionalDefaults | null>(null);

  React.useLayoutEffect(() => {
    setApiErrorLocale(locale);
  }, [locale]);

  const setLocale = React.useCallback((nextLocale: Locale) => {
    setHasExplicitLocale(true);
    setLocaleState(nextLocale);
  }, []);

  const setRuntimeRegionalSettings = React.useCallback((settings: RegionalDefaults | null) => {
    setRuntimeSettings(settings);
    setRegionalDefaults(settings);
    if (!hasExplicitLocale) setLocaleState(settings?.defaultLanguage ?? "en");
  }, [hasExplicitLocale]);

  React.useEffect(() => {
    if (hasExplicitLocale) window.localStorage.setItem("nexus-locale", locale);
    else window.localStorage.removeItem("nexus-locale");
    document.documentElement.lang = locale;
    document.documentElement.dir = locale === "ar" ? "rtl" : "ltr";
  }, [hasExplicitLocale, locale]);

  const t = React.useCallback(
    (key: string, params?: Record<string, string | number>) => {
      const message = getNestedValue(messages[locale] as Record<string, unknown>, key);
      if (message === undefined || typeof message !== "string") {
        const fallback = getNestedValue(messages.en as Record<string, unknown>, key);
        return typeof fallback === "string" ? interpolate(fallback, params) : key;
      }
      return interpolate(message, params);
    },
    [locale]
  );

  const formatNumber = React.useCallback((value: number | bigint, options: Intl.NumberFormatOptions = {}) => {
    return formatNumberValue(value, locale, options);
  }, [locale]);

  const formatPercent = React.useCallback((value: number) => {
    return `${formatNumber(Math.round(value))}%`;
  }, [formatNumber]);

  const formatCurrency = React.useCallback((value: number, currency?: string) => {
    return formatCurrencyValue(value, currency === undefined ? runtimeSettings?.currency : currency, locale);
  }, [locale, runtimeSettings?.currency]);

  const formatDate = React.useCallback((date: Date | string, options?: Intl.DateTimeFormatOptions) => {
    return formatDateValue(date, locale, options);
  }, [locale]);

  const formatDateTime = React.useCallback((date: Date | string) => {
    return formatDateTimeValue(date, locale);
  }, [locale]);

  const value = React.useMemo<I18nContextValue>(
    () => ({ locale, setLocale, t, setRuntimeRegionalSettings, firstDayOfWeek: getFirstDayOfWeekIndex(1), formatNumber, formatCurrency, formatPercent, formatDate, formatDateTime }),
    [locale, setLocale, t, setRuntimeRegionalSettings, formatNumber, formatCurrency, formatPercent, formatDate, formatDateTime, runtimeSettings]
  );

  return <I18nContext.Provider value={value}><ConfirmProvider translate={(key) => t(key)}>{children}</ConfirmProvider></I18nContext.Provider>;
}

export function useI18n(): I18nContextValue {
  const value = React.useContext(I18nContext);
  if (!value) throw new Error("useI18n must be used within I18nProvider");
  return value;
}
