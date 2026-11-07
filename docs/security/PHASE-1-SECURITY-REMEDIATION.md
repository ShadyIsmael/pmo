# Phase 1 Security Remediation — Critical & High Findings

> Historical record: Next.js references below describe the retired frontend and are kept only to preserve the original decision or implementation history. The supported frontend is apps/web-react (React, Vite, and React Router); these references are not build, deployment, agent, or audit targets.

| Item | Detail |
|------|--------|
| Repository | `C:\Projects\pmo` (FOPX PM / NEXUS PMO) |
| Audit baseline | `a698e86` — 44 findings (3 Critical, 9 High, 16 Medium, 16 Low) |
| Audit report | `C:\Users\shady\security-audit-skill\nexus-pmo\run-1\REPORT.md` |
| Remediation date | 27 October 2026 |
| Method | Static review of the live working tree, targeted code changes, unit/regression tests, Semgrep re-scan. No live services were started, no intrusive testing was performed, and no shared credentials were rotated. |
| Working-tree safety | The 121 pre-existing uncommitted changes were preserved. No reset, stash, checkout, or discard was performed. |

---

## 1. Verification and classification

Findings were re-checked against the current working tree rather than the audited commit.

| ID | Severity | Status after Phase 1 | Notes |
|----|----------|----------------------|-------|
| C1 | Critical | **Fixed in code — operational rotation outstanding** | No signing key in source; host fails fast outside Development |
| C2 | Critical | **Fixed in code — operational rotation outstanding** | Loopback binding, no committed `sa` password |
| C3 | Critical | **Fixed and regression-tested** | Stored XSS in the Next.js rich-text editor |
| H1 | High | **Partially mitigated — operational action outstanding** | Demo credential no longer advertised or defaulted; seeded value unchanged |
| H2 | High | **Fixed (rate limiting)** | Per-account lockout remains open |
| H3 | High | **Fixed** | AI planning requires authorization |
| H4 | High | **Fixed and regression-tested** | Integration secrets excluded from API responses |
| H5 | High | **Fixed (conditional exposure)** | Redis loopback-only; optional AUTH; Redis is unused by application code |
| H6 | High | **Fixed and regression-tested** | Client-controlled content type no longer served |
| H7 | High | **Fixed and regression-tested** | Meeting links restricted to http/https |
| H8 | High | **Fixed** | Compose defaults to Production |
| H9 | High | **Fixed in code — operational rotation outstanding** | Literal `sa` password removed from script/docs |
| Semgrep `unsafe-path-combine` | High (tool) | **False positive** | `LocalStorageProvider.ResolvePath` resolves and constrains the path; unchanged |

Medium and Low findings (M1–M16, L1–L16) were deliberately left for Phase 2; none of them were introduced by this phase.

---

## 2. Critical findings

### C1 — Hard-coded JWT signing key

**Verified exposure (confirmed).** The signing secret existed in `appsettings.json` and as an inline fallback in `Program.cs` and `DemoAuthService`. Any repository reader could mint tokens for any user, role, or tenant. No deployment path removed the fallback before this change.

**Fix**

- New `JwtSigningKey` resolver (`NEXUS.SharedKernel.Security`) requires at least 32 UTF-8 bytes and rejects empty or whitespace values.
- `Program.cs` no longer has a fallback. Development generates a cryptographically random, process-local key when none is configured (tokens die on restart). Every other environment throws at startup.
- `DemoAuthService` resolves the key through the same helper.
- `Jwt:Secret` removed from `appsettings.json` and `appsettings.Development.json`.
- Compose supplies `Jwt__Secret: ${NEXUS_JWT_SECRET:-}`; an empty value means “fail fast” outside Development.

**Files:** `apps/api/src/NEXUS.SharedKernel/Security/JwtSigningKey.cs` (new), `apps/api/src/NEXUS.Api/Program.cs`, `apps/api/src/NEXUS.Modules.Identity/Infrastructure/DemoAuthService.cs`, `apps/api/src/NEXUS.Api/appsettings.json`, `apps/api/src/NEXUS.Api/appsettings.Development.json`, `docker-compose.yml`

**Tests:** `JwtSigningKeyTests` (7 cases) — rejects null/empty/whitespace/short keys, measures UTF-8 bytes rather than characters, accepts the 32-byte floor, and produces distinct ephemeral keys.

**Remaining risk**

- The previously committed value must be treated as burned. Until the deployed key is rotated, tokens forged with the old value remain valid.
- Tokens remain valid for 8 hours after rotation unless sessions are also revoked (refresh tokens last 7 days).
- Algorithm remains HS256 with a shared symmetric key; asymmetric signing and key rotation (`kid`) are Phase 2 hardening.

**Manual actions**

1. Generate a new 48-byte random secret per environment and store it in the platform secret store.
2. Deploy `Jwt__Secret`; confirm startup succeeds and old tokens are rejected.
3. Invalidate refresh sessions (or wait for the 7-day window).
4. Purge the secret from Git history (`git filter-repo`) or accept permanent disclosure.

### C2 — SQL Server `sa` reachable on `0.0.0.0:1433`

**Verified exposure (confirmed).** Compose published port 1433 on all interfaces, and an `sa` password was written literally in a maintenance script.

**Fix**

- SQL Server now publishes `127.0.0.1:1433:1433`; Redis `127.0.0.1:6379:6379`.
- `docker-compose.yml` requires `NEXUS_DB_PASSWORD` (`:?`) and passes it to the container and connection string.
- The health check reads `$$MSSQL_SA_PASSWORD` inside the container instead of interpolating the host value.
- `scripts/database/reset-database-users-only.ps1` reads `NEXUS_DB_PASSWORD` and throws when it is unset.

**Files:** `docker-compose.yml`, `scripts/database/reset-database-users-only.ps1`

**Tests:** Not applicable (infrastructure configuration). Validated by inspection of the rendered compose file.

**Remaining risk / manual actions**

1. Rotate the `sa` password on any environment where the old value was used.
2. Purge the credential from Git history.
3. For deployments beyond a single developer workstation, remove the published port entirely and reach SQL Server only over the compose network.
4. Verify host firewall rules independently of compose; repository configuration is not proof of production exposure.

### C3 — Stored XSS in the Next.js rich-text editor

**Verified exposure (confirmed).** `sanitizeRichText` returned decoded text nodes verbatim, so encoded markup such as `&lt;img src=x onerror=...&gt;` became live HTML when rendered with `dangerouslySetInnerHTML`. `apps/web-react` already had the correct implementation.

**Fix**

- Text nodes are escaped (`&`, `<`, `>`), dangerous element names are dropped entirely, and every element is rebuilt from an allow-list without attributes.
- Both the editor load path and `RichTextContent` render path pass through the sanitizer; paste is forced to plain text.
- `fallbackSanitize` now strips tags and escapes the remainder instead of deleting attributes only.

**Files:** `apps/web/src/components/ui/rich-text-editor.tsx`, `apps/web/src/components/ui/rich-text-editor.test.tsx` (new)

**Tests:** 5 regression cases with harmless payloads: attribute stripping, encoded-payload reactivation, bare angle brackets, `javascript:`/event-handler removal, and rendered persisted content never producing an `<img>`.

**Remaining risk:** No CSP (`script-src`) exists yet, so a future sanitizer regression would still execute. Tokens are stored in `localStorage` (M7), so any XSS is full account takeover.

---

## 3. High findings

### H1 — Seeded administrator with a known password

**Verified exposure (confirmed).** The login pages of both applications advertised `admin@nexus.com` / `Demo123!`, the user-administration screen defaulted new accounts to the same value, and compose set `Development` (seeding active) by default.

**Fix**

- Removed the credential from both login pages.
- Removed the `"Demo123!"` default in the administration user form and stopped sending an unused `password` field on create (the API always issues a temporary password).
- Base compose now defaults to `Production`; only the override enables Development and seeding.
- Seeding remains Development-only.

**Not fully resolved.** `SeedDataProvider.DemoPassword` is intentionally unchanged: altering it would break existing development databases and password hashes. The seed value is therefore still a known credential wherever seeding has run.

**Manual actions**

1. Rotate the password of every seeded account in any environment that is not a developer workstation.
2. Never run the base compose file with `ASPNETCORE_ENVIRONMENT=Development` on a shared host.
3. Schedule removal of the fixed seed password once development databases can be reseeded.

### H2 — No login rate limiting

**Verified exposure (confirmed).** No limiter, lockout, or failed-attempt tracking existed.

**Fix:** ASP.NET Core fixed-window limiter registered in `Program.cs` (10 requests/minute per client IP, `QueueLimit = 0`, rejection status 429) and applied via `[EnableRateLimiting("auth")]` to `POST /api/auth/login` and `POST /api/auth/refresh`; `app.UseRateLimiter()` runs after routing.

**Files:** `apps/api/src/NEXUS.Api/Program.cs`, `apps/api/src/NEXUS.Modules.Identity/Api/AuthController.cs`

**Tests:** No HTTP-level test issues auth requests, so the existing 526-test suite is unaffected (verified by full run).

**Remaining risk:** The limiter is in-process and keyed by IP, so it is per-instance, shared behind NAT, and bypassable via `X-Forwarded-For` unless forwarded headers are configured correctly. There is still no per-account lockout or exponential backoff, and failed logins are not audited.

**Manual actions:** Enable and trust forwarded headers at the reverse proxy; add per-account lockout in Phase 2.

### H3 — Anonymous AI planning endpoints

**Verified exposure (confirmed).** The controller had no `[Authorize]`, so generation wrote rows and consumed provider quota anonymously.

**Fix:** `Planning.Manage` on `POST /generate`, `Planning.View` on the list and detail reads. Policies already exist in `Program.cs` and are claim-based, so existing RBAC is preserved.

**Files:** `apps/api/src/NEXUS.Modules.AIPlanning/Api/AIPlanningController.cs`

**Remaining risk:** No global fallback authorization policy was added. Several endpoints (for example stakeholder invitations) currently rely on being anonymous by omission, and a fallback policy would break them. Consequently a future controller that forgets `[Authorize]` is still anonymous; this must be caught by review or an architecture test in Phase 2.

### H4 — Integration credentials returned to any authenticated user

**Verified exposure (confirmed).** The controller returned the raw `IntegrationConnection` entity, including `ApiKey`, `ApiSecret`, and `ErrorLog`, to any authenticated caller.

**Fix**

- New `IntegrationConnectionDto` / `IntegrationSyncDto` projections expose only non-secret metadata plus `HasApiKey` / `HasApiSecret` booleans.
- The paged query projects in SQL, so credential columns are never materialized.
- Controller policy raised from `[Authorize]` to `[Authorize(Policy = "Settings.Read")]`.
- `page`/`pageSize` clamped (1–100).

**Files:** `apps/api/src/NEXUS.Modules.Integrations/Application/IntegrationConnectionDto.cs` (new), `IIntegrationService.cs`, `IntegrationService.cs`, `Api/IntegrationController.cs`

**Tests:** `IntegrationConnectionDtoTests` (2 cases) — serialized payload contains no `ApiKey`/`ApiSecret`/`ErrorLog`/`Config` properties while still reporting secret presence; the contract type declares no credential property.

**Remaining risk:** Credentials are still stored in plaintext columns and there are no create/update endpoints (read-only surface). Encryption at rest and secret rotation APIs are Phase 2.

### H5 — Redis exposed without authentication

**Verified (conditional).** Port 6379 was published on all interfaces with no `requirepass`. The API does not reference Redis anywhere in code, so runtime impact was limited, but the exposure was real for anyone reaching the host.

**Fix:** Redis publishes on `127.0.0.1` only; `NEXUS_REDIS_PASSWORD` optionally enables `--requirepass` with a matching conditional health check.

**Files:** `docker-compose.yml`

**Remaining risk / manual actions:** Authentication is optional and currently unset. If Redis is ever wired into application code, `NEXUS_REDIS_PASSWORD` must be set and the API connection string updated. Consider removing the service until it is used.

### H6 — Client-controlled document content type served inline

**Verified exposure (confirmed).** `ContentType` was taken verbatim from the upload request and echoed back with `Content-Disposition: inline`, allowing an uploaded `.txt` declared as `text/html` to execute in the application origin when previewed.

**Fix**

- New `DocumentContentTypes` maps extensions to fixed MIME types and unknown extensions to `application/octet-stream`.
- Stored, listed, and downloaded content types are all derived from the file name; legacy rows with hostile stored types are neutralized.
- Preview serves inline only for `application/pdf`, `image/png`, and `image/jpeg`; everything else is `attachment`.
- Preview and download send `X-Content-Type-Options: nosniff`.
- Both document UIs fall back to download when the response blob type is not previewable.

**Files:** `apps/api/src/NEXUS.Modules.Documents/Application/DocumentContentTypes.cs` (new), `DocumentService.cs`, `Api/DocumentController.cs`, `apps/web/src/components/documents/DocumentCenter.tsx`, `apps/web-react/src/features/projects/ProjectDocumentsTab.tsx`

**Tests:** `DocumentContentTypesTests` (18 cases) — fixed mappings, never returns HTML/SVG/JavaScript types, inline preview allow-list.

**Remaining risk:** No antivirus/content inspection; `.txt` and `.csv` previews now download instead of rendering (intentional). Object storage retains original bytes.

### H7 — `javascript:` in the meeting-link field

**Verified exposure (confirmed).** Links were only trimmed, then rendered directly into `href`.

**Fix:** New `MeetingLinkPolicy` accepts only absolute `http`/`https` URLs (empty allowed), enforced in the shared create/update validation path (`UpdateMeetingRequest` derives from `CreateMeetingRequest`). Both frontends render the link only when `safeHttpUrl` returns an allowed URL.

**Files:** `apps/api/src/NEXUS.Modules.Meetings/Application/MeetingLinkPolicy.cs` (new), `MeetingService.cs`, `apps/web/src/lib/safe-url.ts` (new), `apps/web/src/app/[locale]/(dashboard)/meetings/[id]/page.tsx`, `apps/web-react/src/lib/safe-url.ts` (new), `apps/web-react/src/features/delivery/MeetingPages.tsx`

**Tests:** `MeetingLinkPolicyTests` (13 cases) — accepts http/https (with trimming), rejects `javascript:`, `data:`, `vbscript:`, `ftp:`, relative and schemeless values, allows empty.

**Remaining risk:** Existing rows may contain unsafe values; rendering is blocked, and editing such a meeting now fails validation until the link is corrected. Other user-supplied URLs (for example `BaseUrl` in integrations) should be reviewed in Phase 2.

### H8 — Compose defaulted to Development

**Verified exposure (confirmed).** A plain `docker compose up` enabled Swagger, automatic migration, demo seeding, and a source bind-mount.

**Fix:** Base `docker-compose.yml` sets `ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}` for `api` and `worker`. `docker-compose.override.yml` — the local-only file — explicitly sets Development for both. The source bind-mount remains confined to the override.

**Files:** `docker-compose.yml`, `docker-compose.override.yml`

**Remaining risk:** Anyone who runs the override on a shared host still gets Development behavior; this is documented in `docs/development/local-setup.md`.

### H9 — Literal `sa` password in scripts and documentation

**Verified (confirmed).** The value appeared in a maintenance script and in deployment documentation, and it is present in Git history.

**Fix:** Script now reads `NEXUS_DB_PASSWORD` and fails otherwise; the literal was removed from `DEPLOYMENT-ARCHITECTURE.md`, `NEXUS-PMO-ARCHITECTURE.md`, and `docs/project/README.md`. Documentation now instructs supplying database and JWT secrets through environment variables.

**Files:** `scripts/database/reset-database-users-only.ps1`, `docs/architecture/DEPLOYMENT-ARCHITECTURE.md`, `docs/architecture/NEXUS-PMO-ARCHITECTURE.md`, `docs/project/README.md`

**Not fully resolved:** The credential remains in history and must be rotated. See C2 manual actions.

---

## 4. Additional secret-hygiene changes

| Change | Files |
|--------|-------|
| Root `.env.example` with placeholders for `NEXUS_DB_PASSWORD`, `NEXUS_JWT_SECRET`, `NEXUS_REDIS_PASSWORD`, Firebase web values (no real values) | `.env.example` (new) |
| Load tests refuse to start without `K6_PASSWORD` instead of defaulting to the seeded password | `tests/load/auth-only.js`, `tests/load/nexus-baseline.js`, `docs/testing/load/*` |
| Documented JWT key resolution, loopback data services, and Compose environment defaults | `docs/development/local-setup.md`, `docs/architecture/SECURITY-ARCHITECTURE.md`, `docs/architecture/ARCHITECTURE-STATUS.md`, `docs/architecture/AGENT-CONTEXT.md`, `docs/modules/AUTHENTICATION-AND-IDENTITY.md` |
| Removed the shared development password from documentation (single source of truth is now `SeedDataProvider`) | documentation listed above |

Verification: `git grep` for the former JWT secret, former `sa` password, and `Demo123!` now matches only `SeedDataProvider.cs` (Development seed data) and one comment in that file.

---

## 5. Verification executed

### Build

| Command | Result |
|---------|--------|
| `dotnet build NEXUS.slnx` (API) | **Succeeded — 0 warnings, 0 errors** |
| `dotnet build` of `NEXUS.Modules.Documents` / `NEXUS.Modules.Meetings` after H6/H7 | **Succeeded — 0 warnings, 0 errors** |
| Full solution rebuild after H6/H7 | Compilation succeeded; the copy step failed because a running `NEXUS.Api` process (PID 27292) held `src/NEXUS.Api/bin/Debug/net10.0/*.dll`. No process was stopped and no compile error occurred. |

### Backend tests

| Scope | Result |
|-------|--------|
| Full `NEXUS.Api.Tests` (526 tests) | **525 passed, 1 failed** |
| `NEXUS.Api.Tests.Security` (new) | **40 passed** |
| `NEXUS.Modules.ResourceManagement.Tests` | **149 passed** |

The single failure is `ProjectReportingPersistenceTests.Persisted_multi_plan_configuration_uses_weighted_EVM_and_freezes_generated_report_data` (error: “An approved reporting configuration with at least one included section is required…”). It reproduces in isolation and belongs to the in-progress project-reporting work: both the test and `ProjectStatusReportService` are uncommitted modifications unrelated to this phase. It was not introduced or altered here.

### Frontend

| Scope | Result |
|-------|--------|
| `apps/web` Vitest (13 files) | **120 passed**, includes the 5 new rich-text cases |
| `apps/web` `tsc --noEmit` | 2 errors, both pre-existing in `src/components/projects/ProjectOverview.tsx` (`comparisonLabel` prop) and present at `HEAD`; no new errors |
| `apps/web-react` `tsc --noEmit` | **Clean** |

### Semgrep (`p/default`, metrics off)

| Target | Rules | Findings |
|--------|-------|----------|
| `apps/api/src` (all, including untracked) | 84 | 1 — `LocalStorageProvider.cs:24` `unsafe-path-combine`, previously verified as a false positive (`ResolvePath` resolves and constrains the path) |
| `apps/web/src` + `apps/web-react/src` | 213 | **0** |
| Files touched in this phase | 242 | **0** |

### Database gate

No entity, relationship, `DbContext` mapping, migration, or schema change was made. Model synchronization, pending migrations, database identity, backup, and migration application are therefore **not applicable** for this phase (`Not run`). All persistence changes are query projections only.

---

## 6. Manual operational actions (required before sign-off)

| # | Action | Owner | Blocks |
|---|--------|-------|--------|
| 1 | Generate and deploy a new `JWT__SECRET` per environment; confirm startup and rejection of old tokens | Platform/Ops | C1 |
| 2 | Invalidate refresh sessions issued before rotation | Identity owner | C1 |
| 3 | Rotate the SQL Server `sa` password everywhere the old value was used | DBA/Ops | C2, H9 |
| 4 | Purge the JWT secret and `sa` password from Git history, or formally accept permanent disclosure | Repo owner | C1, C2, H9 |
| 5 | Rotate the password of seeded accounts in any non-workstation environment | Ops | H1 |
| 6 | Remove the SQL Server published port for deployed environments; verify host firewall | Ops | C2 |
| 7 | Set `NEXUS_REDIS_PASSWORD` if Redis is ever used, or remove the service | Ops | H5 |
| 8 | Configure trusted forwarded headers so the auth limiter keys on real client IPs | Platform | H2 |
| 9 | Confirm no deployment runs with `ASPNETCORE_ENVIRONMENT=Development` | Ops | H1, H8 |

---

## 7. Residual risk summary

- **Highest residual risk:** the leaked JWT key and `sa` password remain valid until rotated; code changes do not revoke them. C1/C2/H9 must stay “fixed in code, operationally open” until items 1–4 are complete.
- No CSP or security headers exist, and tokens remain in `localStorage`, so any future XSS is still a full account takeover (M7/M8).
- Authorization has no global fallback policy; anonymity-by-omission remains possible for new endpoints (H3 residual).
- Rate limiting is per-instance and IP-based; no account lockout or failed-login audit trail (H2 residual).
- Integration credentials and uploaded documents are stored unencrypted at rest.
- The seeded development password remains in `SeedDataProvider` for backward compatibility (H1 residual).

## 8. Phase 2 candidates

M1–M16 and L1–L16 from the audit, plus: global fallback authorization policy with an explicit anonymous allow-list, per-account lockout and login auditing, CSP/security headers, httpOnly cookie sessions, secret encryption for integration credentials, `kid`-based key rotation, and architecture tests that fail the build when a controller lacks authorization.

## Dependency and deployment hardening follow-up (27 October 2026)

- Base Compose requires a non-empty NEXUS_JWT_SECRET for API and worker. Backend validation still rejects whitespace and keys shorter than 32 UTF-8 bytes. The local override also requires a secret because interpolation precedes merging.
- Base Compose and the base API connection-string default specify Encrypt=True and TrustServerCertificate=False. Local Development overrides preserve prior connectivity. Production needs a trusted SQL Server certificate matching the host. Externally supplied connection strings can override these defaults and require independent review. No live deployment or certificate was inspected or changed.
- Both installations resolve direct zod 3.25.76 and exceljs 4.4.0 with transitive uuid 8.3.2. The previous audit reports Next full audit 4 critical, 11 high, 8 moderate; this is historical evidence, not a verified current or production-only result. Current full and production audits for both apps could not reach registry.npmjs.org (DNS ENOTFOUND). No dependency or lockfile changes were made. Verify advisory ranges and upstream fixes, upgrade compatibly, and repeat both audits before release.
- Compose config and fail-fast execution remain unverified: Docker CLI is unavailable. Actual live production configuration, certificate and deployed frontend are unverified. Compose defines Next as the default web service and React as an optional profile; this is not proof of live deployment.
- Existing sanitizer regression suites cover both frontends. Invitation expiry, single-use behavior, and HTTP-level anonymous rate-limiting regression coverage remain to be added and executed.
