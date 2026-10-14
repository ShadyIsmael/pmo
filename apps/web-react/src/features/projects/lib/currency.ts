import * as React from "react";
import { useI18n } from "@i18n/messages";
import { useCurrencies } from "@features/administration/api/admin";

export function useCurrencyFormatter() {
  const { locale } = useI18n();
  const { data: currencies } = useCurrencies(false);

  return React.useMemo(
    () => ({
      format: (amount: number | null | undefined, currencyCode: string | null | undefined): string => {
        if (amount == null || Number.isNaN(Number(amount))) return "—";
        const code = currencyCode?.trim() ? currencyCode : "USD";
        try {
          return new Intl.NumberFormat(locale === "ar" ? "ar-SA" : "en-US", {
            style: "currency",
            currency: code,
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
          }).format(Number(amount));
        } catch {
          return `${Number(amount).toFixed(2)} ${code}`;
        }
      },
    }),
    [locale, currencies]
  );
}
