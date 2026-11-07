import { formatNumberValue } from "./number";

export function formatFileSize(bytes: number | null | undefined, locale: string = "en"): string {
  if (typeof bytes !== "number" || !Number.isFinite(bytes) || bytes < 0) return "—";
  const units = ["B", "KB", "MB", "GB", "TB"] as const;
  let value = bytes;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  const digits = unitIndex === 0 ? 0 : 1;
  return `${formatNumberValue(value, locale, { maximumFractionDigits: digits })} ${units[unitIndex]}`;
}
