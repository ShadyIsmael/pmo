# NEXUS PMO — Frontend Engineering Guidelines

These guidelines are mandatory for **frontend tasks only** (changes to `apps/web/`, `packages/*` used by the web app, or frontend-relevant architecture). Before any frontend code change, read and follow this file together with `AGENT-CONTEXT.md` and `CODE-QUALITY-GUIDELINES.md`. Treat all three files as mandatory acceptance criteria.

Backend-only tasks must not read this file unless the change is directly relevant to frontend behavior.

## Performance

- Heavy sheets, dialogs, and palettes must not mount or fetch data while closed. Gate their data queries on the open state.
- Gate inactive-tab and conditional queries with `enabled`. Do not fetch data the user cannot see.
- Avoid duplicate requests when the existing payload already contains the needed data; derive it from what is already fetched.
- Lazy-load heavy non-default UI (e.g. `next/dynamic` for sheets, advanced search, exporters) instead of importing it eagerly into a route.
- Avoid importing feature/API barrels into shared layouts or providers. Prefer precise imports in shared application shells.
- Do not add heavy libraries for trivial UI when native HTML/SVG/CSS is sufficient.
- Measure before optimizing; fix only proven bottlenecks. Do not refactor on speculation.
- Avoid unnecessary state churn, rerenders, and synchronous high-frequency storage writes.

## React Query / API

- Centralize endpoint definitions in the API layer (do not scatter URL construction through components).
- Centralize query-key factories; reuse them for both queries and invalidation.
- Invalidate exactly where possible; avoid broad refetches that retrigger unrelated queries.
- Do not enable mutation retries for non-idempotent actions (POST/PATCH/DELETE) unless explicitly designed for retry safety.
- Avoid broad refetches and duplicate caches of the same resource under different keys.

## React / Next.js

- Keep shared layouts and providers lightweight; move heavy or rare UI out of them.
- Avoid render-phase side effects; keep render logic pure and push side effects into guarded effects or handlers.
- Keep Client Components scoped to where client behavior is actually required.
- Split oversized components and hooks by responsibility when they grow beyond a clear single purpose.
- Preserve stable references (memoized callbacks, stable object/array identities) where they prevent real re-renders or effect loops.
- Do not preload or mount hidden feature trees unnecessarily.

## Standards

- Centralize user-facing enums and their i18n labels; never display raw enum values.
- Centralize currency, number, and date formatting; do not re-implement formatting in components.
- Preserve `null`/unavailable values as `—`; never fabricate zero for a missing value.
- Never aggregate mixed currencies without a defined conversion rule.
- Do not duplicate domain logic or local option arrays; reuse the centralized definitions.
- Do not use `any` or unsafe casts that hide type problems.
- Remove dead/unused code in touched areas.
- Introduce new abstractions only when genuinely reusable; avoid over-abstraction.

## Verification

- Frontend tasks must run typecheck, lint, and build (`npm run typecheck`, `npm run lint`, `npm run build` in `apps/web`).
- Performance and data-loading changes must include focused runtime and Network verification (e.g. DevTools/CDP request capture) — build success alone is not sufficient proof for runtime/performance work.
