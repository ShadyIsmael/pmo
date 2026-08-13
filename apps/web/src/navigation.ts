import { createNavigation } from "next-intl/navigation";
import { SUPPORTED_LOCALES, DEFAULT_LOCALE } from "@/lib/constants";

export const { Link, usePathname, useRouter, getPathname, redirect, permanentRedirect } =
  createNavigation({
    locales: SUPPORTED_LOCALES,
    defaultLocale: DEFAULT_LOCALE,
  });
