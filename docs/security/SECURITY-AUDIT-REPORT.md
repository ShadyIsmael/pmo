# Security Audit Report — NEXUS PMO

## Scope update (2026-10-27)

The original frontend dependency finding below was collected against the former apps/web Next.js package. That app has since been removed under ADR-009, but removal does not establish the current exposure of apps/web-react, which may use overlapping dependencies. No fresh audit of apps/web-react was performed in this change. Treat the counts and package versions below as historical, and do not interpret removal as remediation for dependencies that remain in React.

**Scope:** C:\Projects\pmo (apps/web-react, apps/api; historical finding 1 refers to retired apps/web)
**Date:** 2026-10-27

## Summary
Historical snapshot. Overall posture was good at the audit date; the old frontend dependency finding does not establish current React exposure, and apps/web-react has not been freshly audited:

## Findings

### 1. Historical frontend dependency vulnerabilities (React status unverified)
- Historical `npm audit` result for apps/web: 23 vulnerabilities — 4 critical, 11 high, 8 moderate. This is not a current result for apps/web-react.
- Historical examples: `zod <4.6.6` (DoS via oversized constrained collections — GHSA-mrhx-c7xc-m3w5), `uuid <11.1.1` (buffer bounds check — GHSA-w5hq-g745-h8pq) via exceljs. React dependency exposure has not been checked.
- Current action: audit apps/web-react to establish its current dependency exposure. Removing apps/web does not resolve vulnerabilities in packages still used by React.

### 2. TrustServerCertificate=True in connection strings (MEDIUM)
- `apps/api/src/NEXUS.Api/appsettings.json` and `appsettings.Development.json` set `TrustServerCertificate=True` (and `Encrypt=False` in dev).
- Risk: disables TLS certificate validation → MITM possible on DB traffic in production if not overridden.
- Recommendation: remove `TrustServerCertificate=True` in production config; require valid cert; keep dev-only.

### 3. JWT secret optional in non-dev (MEDIUM/needs validation)
- docker-compose sets `Jwt__Secret: ${NEXUS_JWT_SECRET:-}` (empty default). Code refuses to start without it outside Development, but empty value in compose could silently disable auth if environment mis-set.
- Recommendation: make compose fail-fast (`:?`) like DB password; never allow empty JWT secret.

### 4. Rich-text sanitizer fallback path (LOW)
- `sanitizeRichText` falls back to `fallbackSanitize` when DOMParser unavailable, which strips tags but escapes text. Server-rendered/SSR path may skip sanitization differently.
- Recommendation: ensure sanitizer runs consistently; add unit tests for mutation-XSS payloads.

### 5. innerHTML assignment in rich-text-editor (LOW)
- `editorRef.current.innerHTML = nextValue` uses sanitized value; acceptable, but ensure no unsanitized path. Already mitigated.

### 6. Anonymous stakeholder endpoints (INFO)
- `StakeholderInvitationAcceptanceController` is `[AllowAnonymous]` with rate limiting "auth" — appropriate, but verify token entropy and single-use/expiry in service.

### 7. Firebase web config in .env (INFO)
- Public Firebase web config is not a secret; `FIREBASE_CREDENTIALS_JSON` kept empty — good. Ensure never committed.

## Positive controls observed
- CORS: `WithOrigins` from config, no `AllowCredentials`, throws if empty in dev.
- No vulnerable NuGet packages (`dotnet list package --vulnerable` clean).
- No `FromSqlRaw`/`ExecuteSqlRaw` with string interpolation in app code.
- `.env` git-ignored.

## Recommendations priority
1. Patch npm dependencies (zod, uuid).
2. Harden production DB TLS (remove TrustServerCertificate).
3. Fail-fast JWT secret in compose.
4. Add security regression tests (XSS sanitizer, authz on anonymous endpoints).
