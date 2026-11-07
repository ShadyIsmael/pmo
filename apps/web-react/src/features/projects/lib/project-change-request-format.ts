import { formatCurrencyValue } from "@lib/formatting/currency";

export function formatProjectChangeRequestAmount(amount: number, currency: string | null | undefined, locale: string): string {
  return formatCurrencyValue(amount, currency, locale, { minimumFractionDigits: 0, maximumFractionDigits: 2 });
}
