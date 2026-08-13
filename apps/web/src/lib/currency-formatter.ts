export interface CurrencyDefinition {
  id: string;
  code: string;
  name: string;
  symbol: string;
  exchangeRate: number;
  isBase: boolean;
}

/**
 * Formats a monetary value according to the specified currency definition.
 * @param amount The monetary amount to format
 * @param currencyDef The currency definition containing formatting rules
 * @returns Formatted currency string
 */
export function formatCurrencyWithDefinition(
  amount: number | null | undefined,
  currencyDef: CurrencyDefinition | null | undefined
): string {
  // Handle null/undefined amounts
  if (amount == null || isNaN(amount)) {
    return "—";
  }

  // Handle null/undefined currency definition by using fallback
  if (!currencyDef) {
    // Use the currency code as fallback if available, otherwise use GBP
    return new Intl.NumberFormat("en-GB", {
      style: "currency",
      currency: "GBP",
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(amount);
  }

  // Determine the locale based on the currency code to get appropriate formatting
  let locale = "en-GB"; // default
  switch (currencyDef.code) {
    case "USD":
      locale = "en-US";
      break;
    case "EUR":
      locale = "de-DE"; // Using German locale as representative for EUR
      break;
    case "JPY":
      locale = "ja-JP";
      break;
    case "GBP":
      locale = "en-GB";
      break;
    case "CAD":
      locale = "en-CA";
      break;
    case "AUD":
      locale = "en-AU";
      break;
    case "CHF":
      locale = "de-CH";
      break;
    case "CNY":
      locale = "zh-CN";
      break;
    case "SEK":
      locale = "sv-SE";
      break;
    case "NZD":
      locale = "en-NZ";
      break;
    case "MXN":
      locale = "es-MX";
      break;
    case "SGD":
      locale = "en-SG";
      break;
    case "HKD":
      locale = "zh-HK";
      break;
    case "NOK":
      locale = "nb-NO";
      break;
    case "KRW":
      locale = "ko-KR";
      break;
    case "TRY":
      locale = "tr-TR";
      break;
    case "RUB":
      locale = "ru-RU";
      break;
    case "INR":
      locale = "en-IN";
      break;
    case "BRL":
      locale = "pt-BR";
      break;
    default:
      // For unknown currencies, try to use the currency code directly
      locale = "en-GB"; // fallback
  }

  // Create the formatter based on locale
  const formatter = new Intl.NumberFormat(locale, {
    style: "currency",
    currency: currencyDef.code,
    minimumFractionDigits: 2, // Default to 2 decimal places
    maximumFractionDigits: 2, // Default to 2 decimal places
  });

  // Format the amount
  let formatted = formatter.format(amount);

  // If the currency symbol from the definition is different from the locale default,
  // replace it with the one from the definition
  if (currencyDef.symbol && currencyDef.symbol !== getCurrencySymbol(currencyDef.code, locale)) {
    // Replace the default symbol with the one from the definition
    const defaultSymbol = getCurrencySymbol(currencyDef.code, locale);
    if (formatted.includes(defaultSymbol)) {
      // Determine symbol position based on the formatted string
      if (formatted.startsWith(defaultSymbol)) {
        // Symbol is at the beginning
        formatted = formatted.replace(defaultSymbol, currencyDef.symbol);
      } else if (formatted.endsWith(defaultSymbol)) {
        // Symbol is at the end
        formatted = formatted.replace(defaultSymbol, currencyDef.symbol);
      } else {
        // Symbol is in the middle or not at expected position
        // In this case, we'll construct the string ourselves
        const amountWithoutSymbol = formatted.replace(/[^\d.,\-'']/g, '').trim();
        if (formatter.format(1).startsWith(getCurrencySymbol(currencyDef.code, locale))) {
          // Symbol should be at the start
          formatted = `${currencyDef.symbol}${amountWithoutSymbol}`;
        } else {
          // Symbol should be at the end
          formatted = `${amountWithoutSymbol}${currencyDef.symbol}`;
        }
      }
    } else {
      // If the default symbol isn't found, prepend or append based on common patterns
      // We'll construct the string using the definition's symbol
      const amountPart = formatter.format(amount).replace(/[^\d.,\-'']/g, '').trim();
      // Determine position based on the currency code common practices
      const symbolAtStart = ["USD", "GBP", "EUR", "CAD", "AUD", "CHF", "CNY", "SEK", "NZD", "MXN", "SGD", "HKD", "NOK", "KRW", "TRY", "RUB", "INR", "BRL"].includes(currencyDef.code);
      formatted = symbolAtStart ? `${currencyDef.symbol}${amountPart}` : `${amountPart}${currencyDef.symbol}`;
    }
  }

  return formatted;
}

/**
 * Gets the default currency symbol for a given currency code and locale.
 */
function getCurrencySymbol(currencyCode: string, locale: string): string {
  return (0).toLocaleString(
    locale,
    { style: "currency", currency: currencyCode, minimumFractionDigits: 0, maximumFractionDigits: 0 }
  ).replace(/\d/g, '').trim();
}

/**
 * Creates a memoized currency formatter function that prevents excessive API calls.
 * @param currencies All available currencies
 * @returns A function that formats amounts using the appropriate currency definition
 */
export function createMemoizedCurrencyFormatter(currencies: CurrencyDefinition[]) {
  // Create a map for quick lookup
  const currencyMap = new Map(currencies.map(currency => [currency.code, currency]));
  
  // Return a function that formats using the cached currency definitions
  return function formatAmount(amount: number | null | undefined, currencyCode: string | null | undefined): string {
    if (amount == null || isNaN(amount)) {
      return "—";
    }
    
    if (!currencyCode) {
      // If no currency code is provided, use fallback
      return new Intl.NumberFormat("en-GB", {
        style: "currency",
        currency: "GBP",
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      }).format(amount);
    }
    
    const currencyDef = currencyMap.get(currencyCode);
    return formatCurrencyWithDefinition(amount, currencyDef);
  };
}