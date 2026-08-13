import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

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

export function formatRelativeDate(date: Date | string | null | undefined): string {
  if (!date) return "—";
  const d = typeof date === "string" ? new Date(date) : date;
  if (isNaN(d.getTime())) return "—";
  const now = new Date();
  const diff = d.getTime() - now.getTime();
  const days = Math.round(diff / (1000 * 60 * 60 * 24));

  if (days === 0) return "Today";
  if (days === 1) return "Tomorrow";
  if (days === -1) return "Yesterday";
  if (days > 0 && days <= 7) return `${days} days left`;
  if (days < 0 && days >= -7) return `${Math.abs(days)} days ago`;
  return formatDate(d);
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
  const colors = {
    green: "text-status-green bg-status-green-light",
    amber: "text-status-amber bg-status-amber-light",
    red: "text-status-red bg-status-red-light",
  };
  return colors[health] ?? colors.green;
}

export function getHealthDotColor(health: string): string {
  const colors: Record<string, string> = {
    green: "bg-status-green",
    amber: "bg-status-amber",
    red: "bg-status-red",
  };
  const key = (health ?? "").toLowerCase();
  return String(colors[key] ?? colors.green);
}

export function getStatusVariant(
  status: string
): "default" | "secondary" | "destructive" | "outline" {
  switch (status) {
    case "Active":
    case "InProgress":
      return "default";
    case "At Risk":
      return "destructive";
    case "Delayed":
      return "destructive";
    case "On Hold":
    case "OnHold":
      return "secondary";
    case "Completed":
      return "default";
    case "Cancelled":
      return "outline";
    default:
      return "secondary";
  }
}

export type StatusBadgeVariant = "status-green" | "status-amber" | "status-red";

export function getRagVariant(value: string | null | undefined): StatusBadgeVariant | "outline" {
  const key = (value ?? "").toLowerCase();
  if (key === "green") return "status-green";
  if (key === "amber") return "status-amber";
  if (key === "red") return "status-red";
  return "outline";
}

export function getRagTextColor(value: string | null | undefined): string {
  const key = (value ?? "").toLowerCase();
  if (key === "green") return "text-status-green";
  if (key === "amber") return "text-status-amber";
  if (key === "red") return "text-status-red";
  return "text-foreground";
}

export function getRiskVariant(value: string | null | undefined): StatusBadgeVariant | "outline" {
  const key = (value ?? "").toLowerCase();
  if (key === "critical" || key === "high") return "status-red";
  if (key === "medium") return "status-amber";
  if (key === "low") return "status-green";
  return "outline";
}

export function getRiskTextColor(value: string | null | undefined): string {
  const key = (value ?? "").toLowerCase();
  if (key === "critical" || key === "high") return "text-status-red";
  if (key === "medium") return "text-status-amber";
  if (key === "low") return "text-status-green";
  return "text-foreground";
}

export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export function truncate(str: string, length: number): string {
  if (str.length <= length) return str;
  return str.slice(0, length) + "...";
}