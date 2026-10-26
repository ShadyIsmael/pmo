import * as React from "react";
import enMessages from "./messages/en.json";
import arMessages from "./messages/ar.json";
import { ConfirmProvider } from "@components/ui/ConfirmProvider";

export type Locale = "en" | "ar";

const messages = {
  en: enMessages,
  ar: arMessages,
} as const;

interface I18nContextValue {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: (key: string, params?: Record<string, string | number>) => string;
  formatNumber: (value: number) => string;
  formatCurrency: (value: number, currency?: string) => string;
  formatPercent: (value: number) => string;
  formatDate: (date: Date | string, options?: Intl.DateTimeFormatOptions) => string;
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
  const [locale, setLocale] = React.useState<Locale>(() => {
    if (typeof window === "undefined") return "en";
    const saved = window.localStorage.getItem("nexus-locale");
    return saved === "ar" ? "ar" : "en";
  });

  React.useEffect(() => {
    window.localStorage.setItem("nexus-locale", locale);
    document.documentElement.lang = locale;
    document.documentElement.dir = locale === "ar" ? "rtl" : "ltr";
  }, [locale]);

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

  const formatNumber = React.useCallback((value: number) => {
    return new Intl.NumberFormat(locale === "ar" ? "ar-SA" : "en-US", { useGrouping: true }).format(value);
  }, [locale]);

  const formatPercent = React.useCallback((value: number) => {
    return `${formatNumber(Math.round(value))}%`;
  }, [formatNumber]);

  const formatCurrency = React.useCallback((value: number, currency: string = "USD") => {
    return new Intl.NumberFormat(locale === "ar" ? "ar-SA" : "en-US", {
      style: "currency",
      currency,
      minimumFractionDigits: 0,
      maximumFractionDigits: 2,
    }).format(value);
  }, [locale]);

  const formatDate = React.useCallback((date: Date | string, options?: Intl.DateTimeFormatOptions) => {
    const d = typeof date === "string" ? new Date(date) : date;
    return new Intl.DateTimeFormat(locale === "ar" ? "ar-SA" : "en-US", {
      dateStyle: "medium",
      ...options,
    }).format(d);
  }, [locale]);

  const value = React.useMemo<I18nContextValue>(
    () => ({ locale, setLocale, t, formatNumber, formatCurrency, formatPercent, formatDate }),
    [locale, t, formatNumber, formatCurrency, formatPercent, formatDate]
  );

  return <I18nContext.Provider value={value}><ConfirmProvider translate={(key) => t(key)}>{children}</ConfirmProvider></I18nContext.Provider>;
}

export function useI18n(): I18nContextValue {
  const value = React.useContext(I18nContext);
  if (!value) throw new Error("useI18n must be used within I18nProvider");
  return value;
}
