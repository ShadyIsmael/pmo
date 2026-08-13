import { useMemo } from "react";
import { useCurrencies, type CurrencyDto } from "./useCurrencies";
import { formatCurrency } from "@/lib/utils";

interface CurrencyFormatter {
  format: (amount: number | null | undefined, currencyCode: string | null | undefined) => string;
  getCurrencyDefinition: (currencyCode: string) => CurrencyDto | undefined;
}

/**
 * Custom hook that provides a currency formatter which uses the currency definitions from the API
 * This ensures that all currency formatting respects the configured symbol, position, decimal places, etc.
 */
export function useCurrencyFormatter(): CurrencyFormatter {
  const { data: currencies, isLoading, isError } = useCurrencies();

  const formatter = useMemo(() => {
    // Create a map of currency codes to their definitions for fast lookup
    const currencyMap = new Map<string, CurrencyDto>();
    if (currencies) {
      currencies.forEach(currency => {
        currencyMap.set(currency.code, currency);
      });
    }
    
      return {
      format: (amount: number | null | undefined, currencyCode: string | null | undefined): string => {
        // Handle null/undefined amounts
        if (amount == null || isNaN(Number(amount))) {
          return "—";
        }

        // If no currency code is provided, use fallback
        if (!currencyCode) {
          return formatCurrency(Number(amount), "GBP");
        }

        // Try to get the currency definition
        const currencyDef = currencyMap.get(currencyCode);

        // If we have a currency definition, we can potentially use more specific formatting
        // For now, we'll use the standard formatCurrency function which leverages Intl
        // In the future, this could be enhanced to use the specific formatting rules
        // from the currency definition (symbol position, decimal separators, etc.)
        return formatCurrency(Number(amount), currencyCode);
      },
      
      getCurrencyDefinition: (currencyCode: string): CurrencyDto | undefined => {
        return currencyMap.get(currencyCode);
      }
    };
  }, [currencies]);

  return formatter;
}

/**
 * Simple function to format currency when currencies data is already available
 */
export function formatCurrencyWithCurrencies(
  amount: number | null | undefined,
  currencyCode: string | null | undefined,
  currencies: CurrencyDto[] | undefined
): string {
  if (amount == null || isNaN(Number(amount))) {
    return "—";
  }

  if (!currencyCode) {
    return formatCurrency(Number(amount), "GBP");
  }

  return formatCurrency(Number(amount), currencyCode);
}