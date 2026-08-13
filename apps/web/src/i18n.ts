import { getRequestConfig } from "next-intl/server";
import { notFound } from "next/navigation";
import { SUPPORTED_LOCALES } from "@/lib/constants";

export default getRequestConfig(async ({ locale }) => {
  if (!SUPPORTED_LOCALES.includes(locale as any)) notFound();

  return {
    messages: (await import(`./messages/${locale}.json`)).default,
  };
});
