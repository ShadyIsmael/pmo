# FOPX PM Frontend Engineering Guidelines

These guidelines apply to the sole supported frontend, apps/web-react. Before frontend changes, read this file together with AGENT-CONTEXT.md and CODE-QUALITY-GUIDELINES.md.

## React and Vite

- Use React Router for navigation and Vite for development and production builds.
- Keep shared layouts and providers lightweight; lazy-load heavy, rarely used views with React.lazy and dynamic import.
- Keep render logic pure. Place navigation, state changes, and data mutations in guarded effects or event handlers.
- Keep components and hooks focused and split responsibilities where useful.
- Preserve stable references when they prevent real rerenders or effect loops.
- Do not add Next.js, app-router conventions, server components, or NEXT_PUBLIC environment variables.

## Performance

- Heavy sheets, dialogs, and palettes must not mount or fetch data while closed.
- Gate inactive-tab and conditional queries with enabled.
- Avoid duplicate requests when existing query data already contains the needed values.
- Avoid importing feature/API barrels into shared layouts or providers.
- Do not add heavy libraries for trivial UI when native HTML, SVG, or CSS is sufficient.
- Measure before optimizing and fix proven bottlenecks.

## React Query and API

- Centralize endpoint definitions under the existing API layer.
- Centralize query-key factories and reuse them for queries and invalidation.
- Invalidate exactly where possible; avoid broad refetches.
- Do not retry non-idempotent mutations unless the operation is explicitly retry-safe.
- Keep query caches for the same resource under one canonical key family.

## Standards and verification

- Centralize user-facing enums, EN/AR labels, currency, number, and date formatting.
- Preserve null or unavailable values; never fabricate zero or aggregate mixed currencies without a conversion rule.
- Do not use any or unsafe casts to hide type problems. Remove dead or unused code in touched areas.
- Run npm --prefix apps/web-react run typecheck, lint, build, and focused tests for changed behavior.
- Performance and data-loading changes also need focused runtime and Network verification.
