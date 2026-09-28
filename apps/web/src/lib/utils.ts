import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";
import { getRiskLevelTextClass } from "@/lib/enums";
import { getStatusBadgeVariant, getStatusTextClass, getStatusTone, statusToneClasses, statusToneDotClasses, type StatusBadgeVariant } from "@/lib/status-presentation";

export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}

export function formatCurrency(
  amount: number,
  currency: string = "GBP",
  locale: string = "en-GB"
): string {
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency,
    minimumFractionDigits: 2,  // Updated to show 2 decimal places by default
    maximumFractionDigits: 2,
  }).format(amount);
}

export function formatNullableCurrency(
  amount: number | null | undefined,
  currencyCode: string = "GBP",
  locale: string = "en-GB"
): string {
  if (amount == null || isNaN(amount)) {
    return "—";
  }
  return formatCurrency(amount, currencyCode, locale);
}

export function formatDate(
  date: Date | string | null | undefined,
  locale: string = "en-GB",
  options?: Intl.DateTimeFormatOptions
): string {
  if (!date) return "—";
  const d = typeof date === "string" ? new Date(date) : date;
  if (isNaN(d.getTime())) return "—";
  return new Intl.DateTimeFormat(locale, {
    day: "numeric",
    month: "short",
    year: "numeric",
    ...options,
  }).format(d);
}

export function formatDateTime(
  date: Date | string | null | undefined,
  locale: string = "en-GB",
): string {
  return formatDate(date, locale, {
    hour: "numeric",
    minute: "2-digit",
  });
}

export function formatRelativeDate(date: Date | string | null | undefined, locale: string = "en-GB"): string {
  if (!date) return "—";
  const d = typeof date === "string" ? new Date(date) : date;
  if (isNaN(d.getTime())) return "—";
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });
  const diffInDays = Math.round((d.getTime() - Date.now()) / (1000 * 60 * 60 * 24));
  return rtf.format(diffInDays, "day");
}

export function getInitials(name: string): string {
  return name
    .split(" ")
    .map((n) => n[0])
    .filter(Boolean)
    .slice(0, 2)
    .join("")
    .toUpperCase();
}

export function getHealthColor(health: "green" | "amber" | "red"): string {
  return statusToneClasses[getStatusTone(health)];
}

export function getHealthDotColor(health: string): string {
  const tone = getStatusTone(health);
  return statusToneDotClasses[tone] ?? statusToneDotClasses.green;
}

export function getStatusVariant(
  status: string
): StatusBadgeVariant {
  return getStatusBadgeVariant(status);
}

export function getRagVariant(value: string | null | undefined): StatusBadgeVariant {
  return getStatusBadgeVariant(value);
}

export function getRagTextColor(value: string | null | undefined): string {
  return getStatusTextClass(value);
}

export function getRiskVariant(value: string | null | undefined): StatusBadgeVariant {
  return getStatusBadgeVariant(value);
}

export function getRiskTextColor(value: string | null | undefined): string {
  const normalized = value
    ? `${value.charAt(0).toUpperCase()}${value.slice(1).toLowerCase()}`
    : value;
  return getRiskLevelTextClass(normalized);
}

export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export function truncate(str: string, length: number): string {
  if (str.length <= length) return str;
  return str.slice(0, length) + "...";
}

export function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

export type NullableNumber = number | null | undefined;

export function formatNullablePercentage(value: NullableNumber, fractionDigits = 0, signed = false, locale: string = "en-GB"): string {
  if (!isFiniteNumber(value)) return "—";
  const num = Math.abs(value);
  const formatted = new Intl.NumberFormat(locale, { minimumFractionDigits: fractionDigits, maximumFractionDigits: fractionDigits }).format(num);
  return `${signed && value > 0 ? "+" : ""}${formatted}%`;
}

export function toDateInput(value?: string | null): string {
  if (!value) return "";
  return value.slice(0, 10);
}
