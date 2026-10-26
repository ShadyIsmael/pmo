import identity from "../../brand.json";

// Presentation only: preserve authentication, storage, API and database identifiers.
export const brand = identity;
export function brandCopy(locale: "en" | "ar") {
  return locale === "ar" ? brand.ar : brand.product;
}
