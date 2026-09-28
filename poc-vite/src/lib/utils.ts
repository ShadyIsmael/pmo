import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatDate(
  date: Date | string | null | undefined,
  locale: string = "en-GB",
  options?: Intl.DateTimeFormatOptions
): string {
  if (!date) return "\u2014";
  const d = typeof date === "string" ? new Date(date) : date;
  if (isNaN(d.getTime())) return "\u2014";
  return new Intl.DateTimeFormat(locale, {
    day: "numeric",
    month: "short",
    year: "numeric",
    ...options,
  }).format(d);
}
