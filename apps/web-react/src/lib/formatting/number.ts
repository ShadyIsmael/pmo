export function formatNumberValue(
  value: number | bigint,
  locale: string = "en",
  options: Intl.NumberFormatOptions = {},
): string {
  if (typeof value === "number" && !Number.isFinite(value)) return "—";
  try {
    const intlLocale = locale === "ar" ? "ar-EG-u-nu-latn" : "en-US";
    return new Intl.NumberFormat(intlLocale, { useGrouping: true, ...options }).format(value);
  } catch {
    return "—";
  }
}
