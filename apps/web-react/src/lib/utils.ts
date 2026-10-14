import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";
import { getStatusBadgeVariant, getStatusTone, getStatusTextClass as getToneTextClass, statusToneDotClasses } from "./status-presentation";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatDate(date: Date | string, locale: string = "en", options?: Intl.DateTimeFormatOptions): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return new Intl.DateTimeFormat(locale === "ar" ? "ar-SA" : "en-US", {
    dateStyle: "medium",
    ...options,
  }).format(d);
}

// NOTE: dateStyle/timeStyle cannot be combined with individual time fields in
// ICU (throws "Invalid option"). Use explicit fields for date+time output.
export function formatDateTime(date: Date | string, locale: string = "en"): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return new Intl.DateTimeFormat(locale === "ar" ? "ar-SA" : "en-US", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(d);
}

export function formatNumber(value: number, locale: string = "en"): string {
  return new Intl.NumberFormat(locale === "ar" ? "ar-SA" : "en-US", { useGrouping: true }).format(value);
}

export function getHealthDotColor(health: string | null | undefined): string {
  const tone = getStatusTone(health);
  return statusToneDotClasses[tone] ?? statusToneDotClasses.green;
}

export function getStatusVariant(status: string | null | undefined) {
  return getStatusBadgeVariant(status);
}

export function getStatusTextClass(status: string | null | undefined): string {
  return getToneTextClass(status);
}

export function getLookupTextClass(value: string | null | undefined): string {
  return value ? "text-info" : "text-muted-foreground";
}

export function toDateInput(value?: string | null): string {
  if (!value) return "";
  return value.slice(0, 10);
}