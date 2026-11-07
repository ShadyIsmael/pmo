export type CurrencyLocale = "en" | "ar" | string;

export interface CurrencyFormatOptions {
  minimumFractionDigits?: number;
  maximumFractionDigits?: number;
  notation?: Intl.NumberFormatOptions["notation"];
  numberingSystem?: string;
}

export function formatCurrencyValue(
  value: number | null | undefined,
  currency: string | null | undefined,
  locale: CurrencyLocale,
  options: CurrencyFormatOptions = {},
): string {
  if (typeof value !== "number" || !Number.isFinite(value)) return "—";
  const code = currency?.trim().toUpperCase();
  if (!code || !/^[A-Z]{3}$/.test(code)) return "—";

  try {
    return new Intl.NumberFormat(locale === "ar" ? "ar-EG-u-nu-latn" : "en-US", {
      style: "currency",
      currency: code,
      minimumFractionDigits: 0,
      maximumFractionDigits: 2,
      ...options,
    }).format(value);
  } catch {
    return "—";
  }
}
