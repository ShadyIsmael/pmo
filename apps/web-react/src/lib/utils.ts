import { type ClassValue, clsx } from "clsx";

export function cn(...inputs: ClassValue[]) {
  return clsx(inputs);
}

export function formatDate(date: Date | string, locale: string = "en", options?: Intl.DateTimeFormatOptions): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return new Intl.DateTimeFormat(locale === "ar" ? "ar-SA" : "en-US", {
    dateStyle: "medium",
    ...options,
  }).format(d);
}

export function formatNumber(value: number, locale: string = "en"): string {
  return new Intl.NumberFormat(locale === "ar" ? "ar-SA" : "en-US", { useGrouping: true }).format(value);
}