import { getRequestConfig } from "next-intl/server";
import { notFound } from "next/navigation";
import { SUPPORTED_LOCALES } from "@/lib/constants";

export default getRequestConfig(async ({ locale }) => {
  if (!(SUPPORTED_LOCALES as readonly string[]).includes(locale)) notFound();

  return {
    messages: (await import(`./messages/${locale}.json`)).default,
  };
});
