import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";
import { formatNumberValue } from "./formatting/number";
import { formatDateTimeValue, formatDateValue } from "./formatting/date";
import { getStatusBadgeVariant, getStatusTone, getStatusTextClass as getToneTextClass, statusToneDotClasses } from "./status-presentation";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatDate(date: Date | string, locale: string = "en", options?: Intl.DateTimeFormatOptions): string {
  return formatDateValue(date, locale, options);
}

export function formatDateTime(date: Date | string, locale: string = "en"): string {
  return formatDateTimeValue(date, locale);
}

export function formatNumber(value: number | bigint, locale: string = "en", options: Intl.NumberFormatOptions = {}): string {
  return formatNumberValue(value, locale, options);
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