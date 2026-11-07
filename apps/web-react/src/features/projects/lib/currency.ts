import { useI18n } from "@i18n/messages";
import { formatCurrencyValue } from "@lib/formatting/currency";

export function useCurrencyFormatter() {
  const { locale } = useI18n();
  return {
    format: (amount: number | null | undefined, currencyCode: string | null | undefined): string =>
      formatCurrencyValue(amount, currencyCode, locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
  };
}
