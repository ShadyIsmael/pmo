import createMiddleware from "next-intl/middleware";
import { NextRequest, NextResponse } from "next/server";
import { SUPPORTED_LOCALES, ALL_KNOWN_LOCALES, DEFAULT_LOCALE } from "@/lib/constants";

const intlMiddleware = createMiddleware({
  locales: SUPPORTED_LOCALES as unknown as string[],
  defaultLocale: DEFAULT_LOCALE,
  localePrefix: "always",
});

export default function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const segments = pathname.split("/").filter(Boolean);
  const firstSegment = segments[0]?.toLowerCase();

  const knownLocales = ALL_KNOWN_LOCALES as readonly string[];
  const activeLocales = SUPPORTED_LOCALES as readonly string[];

  // Redirect disabled locales (e.g. Arabic) to the default locale while
  // preserving the pathname and query parameters.
  if (
    firstSegment &&
    knownLocales.includes(firstSegment) &&
    !activeLocales.includes(firstSegment)
  ) {
    const rest = segments.slice(1).join("/");
    const url = request.nextUrl.clone();
    url.pathname = rest ? `/${DEFAULT_LOCALE}/${rest}` : `/${DEFAULT_LOCALE}`;
    return NextResponse.redirect(url);
  }

  return intlMiddleware(request);
}

export const config = {
  matcher: ["/((?!api|_next|_vercel|.*\\..*).*)"],
};
