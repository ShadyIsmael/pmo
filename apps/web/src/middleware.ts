import createMiddleware from "next-intl/middleware";
import { NextRequest, NextResponse } from "next/server";
import { SUPPORTED_LOCALES, ALL_KNOWN_LOCALES, DEFAULT_LOCALE } from "@/lib/constants";

const intlMiddleware = createMiddleware({
  locales: SUPPORTED_LOCALES as unknown as string[],
  defaultLocale: DEFAULT_LOCALE,
  localePrefix: "always",
});

/**
 * With `localePrefix: "always"`, next-intl answers every locale-prefixed request
 * with an absolute `x-middleware-rewrite` whose *path* is identical to the
 * request path - e.g. `/en/home` is rewritten to `http://localhost:3000/en/home`.
 * Only the origin is added, and next-intl derives that origin from the incoming
 * `Host` header while normalising loopback IPs to `localhost`.
 *
 * Next.js, on the other hand, derives the base it relativises against from the
 * *server* configuration (`initUrl`, built from the `HOSTNAME`/`PORT` env vars in
 * `next/dist/server/lib/router-utils/resolve-routes.js`). `relativizeURL` only
 * strips the origin when it matches that base, so as soon as the two disagree -
 * which they always do when the server is started with a loopback IP such as
 * `HOSTNAME=127.0.0.1` - the absolute URL survives, `resolve-routes` returns
 * `finished: true`, and the router proxies the request back to this very server.
 * The self-proxy then loops until the socket is reset and every page route 500s.
 *
 * Because the rewrite target path is the same path we already have, the origin
 * carries no routing information. Replacing the rewrite with `x-middleware-next`
 * keeps Next.js on the original path and makes routing independent of how the
 * server address is configured.
 */
function toOriginIndependentRouting(response: NextResponse, request: NextRequest): NextResponse {
  const rewrite = response.headers.get("x-middleware-rewrite");
  if (!rewrite) return response;

  let target: URL;
  try {
    target = new URL(rewrite);
  } catch {
    // Already path-relative, so it cannot disagree with anything.
    return response;
  }

  if (target.pathname === request.nextUrl.pathname && target.search === request.nextUrl.search) {
    response.headers.delete("x-middleware-rewrite");
    response.headers.set("x-middleware-next", "1");
  }

  return response;
}

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

  return toOriginIndependentRouting(intlMiddleware(request), request);
}

export const config = {
  matcher: ["/((?!api|_next|_vercel|.*\\..*).*)"],
};
