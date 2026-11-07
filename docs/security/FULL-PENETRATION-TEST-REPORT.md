# FOPX PM — Full Security Penetration Test Report

| Field | Value |
|---|---|
| Target | `C:\Projects\pmo` (FOPX PM / NEXUS PMO) |
| Assessment type | Full penetration test: static analysis, code review, configuration review, authenticated/unauthenticated dynamic testing |
| Scope | `apps/web-react` (React 18 + Vite + TypeScript) and `apps/api` (.NET 10 modular monolith), Development environment only |
| Frameworks | OWASP Top 10 (2021), OWASP API Security Top 10 (2023), OWASP WSTG v4.2 |
| Test window | 2026-10-28 |
| Assessment status | Complete, with explicitly documented blocked tests |
| Report version | 2.1 (supersedes 2.0; corrects the F-01 runtime claim after post-test verification) |
| Classification | Internal — contains exploit detail; do not distribute externally |

---

## Table of contents

1. [Executive summary](#1-executive-summary)
2. [Scope, rules of engagement and test environment](#2-scope-rules-of-engagement-and-test-environment)
3. [Methodology and evidence standard](#3-methodology-and-evidence-standard)
4. [Coverage matrix](#4-coverage-matrix)
5. [Confirmed findings](#5-confirmed-findings)
6. [Verified security controls](#6-verified-security-controls)
7. [Blocked and out-of-scope tests](#7-blocked-and-out-of-scope-tests)
8. [Prioritised remediation plan](#8-prioritised-remediation-plan)
9. [Appendices](#9-appendices)

---

## 1. Executive summary

### 1.1 Overall posture

The application is **materially better hardened than typical line-of-business .NET/React codebases**. Object-level authorisation, financial validation, JWT handling and upload handling were exercised directly and held up: cross-project reads and writes are blocked, token tampering is rejected, refresh-token rotation detects reuse, settlement amounts are bounded, SQL injection probes produced no anomalies, and a hand-rolled rich-text sanitiser removes script/event-handler payloads.

The residual risk is concentrated in **configuration and operational hygiene**, not in the core authorisation model:

* a **publicly known JWT signing key** is still committed in `appsettings.json` at `HEAD`, which silently defeats the application's own production fail-fast guard if that baseline is deployed unchanged (it was **not** in use in the instance tested here — see §7.1);
* **authentication telemetry is weak** — a timing oracle allows user enumeration, there is no per-account lockout, and no login audit trail exists;
* **HTTP response hardening is absent** — no HSTS/CSP/`nosniff`/frame policy on either tier, and the documented Compose deployment is plain HTTP;
* **tokens live in `localStorage`**, which converts any future XSS into full session theft;
* a **document upload defect** persists rows with `TenantId = Guid.Empty`, so every upload succeeds with `201` and is then invisible to every user.

### 1.2 Findings summary

| Severity | Count | IDs |
|---|---|---|
| Critical | 0 | — |
| High | 1 | F-01 |
| Medium | 7 | F-02, F-03, F-04, F-05, F-06, F-07, F-08 |
| Low | 6 | F-09, F-10, F-11, F-12, F-13, F-14 |
| Informational | 2 | I-01, I-02 |
| **Total** | **16** | |

> **Severity note (v2.1).** F-01 is rated High on the basis of the **committed baseline**: the key is in git history and any deployment built from it without an override runs on public key material. In the instance actually tested, the working tree had already removed the value and the process signed with a Development ephemeral key (verified — §7.1), so F-01 was **not** a live key on the tested host.

### 1.3 Top risks in business terms

1. **Signing-key exposure (F-01).** Anyone with the repository can read the JWT signing secret. The documented Docker path overrides it, but a host-run deployment from that baseline does not, and no error is raised. A deployment that forgets `NEXUS_JWT_SECRET` inherits a key that is public by construction.
2. **Account enumeration + unlimited password guessing across IPs (F-02, F-03).** An attacker can confirm which email addresses exist (≈230 ms vs ≈11 ms response) and can spread guesses across source addresses, because throttling is per-IP only and there is no account lockout, CAPTCHA or risk-based challenge.
3. **Tenant-wide staff directory disclosure (F-04).** The lowest-privileged role can enumerate every user's name, email and job title in the tenant — the exact dataset used for phishing and for targeting the attack above.
4. **Broken document upload (F-05).** Users receive `201 Created` and lose the file. Beyond the functional breakage, the rows carry an empty tenant identifier, which is a latent isolation hazard for any future query that bypasses the tenant filter.
5. **No browser-side containment (F-07, F-08).** With no CSP and tokens in `localStorage`, a single XSS anywhere in the SPA becomes persistent account takeover. The current sanitiser is effective, but it is the only line of defence.

### 1.4 What changed versus the earlier draft

The earlier draft of this report (same day) contained two material inaccuracies that are corrected here:

* **Cross-project IDOR was reported as "all routes returned 404"** but the probes used internal project GUIDs, which are not routable — every request 404'd regardless of authorisation. The test was rebuilt with `publicId` values and a user genuinely assigned to one project: access to the assigned project now returns `200` and access to a non-assigned project returns `404`, including list filters and writes.
* **"Hardcoded JWT secret was remediated"** was incorrect. The fail-fast guard exists, but the committed baseline (`HEAD`) still supplies a value, so the guard cannot fire outside Development when that baseline is deployed unchanged. The working tree removed the value on 2026-10-27, before the test window, which is why the tested instance used an ephemeral key — see F-01 and §7.1.

---

## 2. Scope, rules of engagement and test environment

### 2.1 In scope

| Component | Detail |
|---|---|
| API | `apps/api` — 24 projects, .NET 10, Kestrel, 227 documented GET endpoints |
| SPA | `apps/web-react` — React 18, Vite, React Router 6, served by Vite (dev) / nginx (build) |
| Persistence | SQL Server 2025, EF Core with global tenant query filters |
| Configuration | `appsettings*.json`, `Program.cs`, `nginx.conf`, `docker-compose*.yml`, `.env` |
| Identity | Local password authentication, JWT access tokens, rotating refresh tokens, RBAC (95 permissions, 8 roles) |

### 2.2 Explicitly out of scope / prohibited

* Production and any non-local environment.
* Denial-of-service, stress testing, destructive payloads, credential spraying against real accounts.
* Third-party services (Firebase, SMTP, Entra/LDAP) — none were configured, so no outbound traffic was generated.
* Source modification. No application code was changed.

### 2.3 Test environment (verified)

| Item | Value |
|---|---|
| API instance under test | `http://127.0.0.1:5001`, `ASPNETCORE_ENVIRONMENT=Development` |
| Database | `NexusPmo_PenTest` — restore of a `COPY_ONLY` backup of `NexusPmo`, with the single pending migration `20261028084103_AddMeetingActionWorkItemLink` applied **to the copy only** |
| Production-like dev database | `NexusPmo` — started read-only in practice; **schema not modified**, pending migration not applied |
| API startup flags | `--skip-development-database-initialization` (no migration/seed side effects on start) |
| Isolation rationale | All mutating tests (uploads, work items, time entries, settlements) ran only against `NexusPmo_PenTest` |

### 2.4 Test identities

| Identity | Role | Password | Notes |
|---|---|---|---|
| `admin@nexus.com` | `admin` (tenant administrator, 84 permissions) | seeded, published in repository docs | Known-credential finding F-11 |
| `pentest.team@example.test` | `team.member` (13 permissions) | `Pentest!2026#Aa` | Created for this test; assigned to Project A only |
| `pentest.pm@example.test` | `project.manager` (64 permissions) | `Pentest!2026#Aa` | Created for this test |
| `pentest.fin@example.test` | `finance.controller` | `Pentest!2026#Aa` | Created for this test |

To make object-level authorisation testable, one `ProjectMembers` row was inserted directly into the isolated copy linking the team-member resource to project **PRJ-1001** (`e0000001-…-0001`, public id `cde8483e-5be5-49d3-9a23-7dbc3ab883f0`). Project **PRJ-1002** (`e0000002-…-0002`, public id `f31f1d5b-7038-45a5-9fc7-816078b70a99`) was used as the "not assigned" target. This is documented here for reproducibility and is reverted in the cleanup procedure (Appendix C).

### 2.5 Tooling actually executed

| Tool | Version | Use |
|---|---|---|
| Semgrep | 1.182.1, `p/default` (84 rules, 892 files) | SAST over `apps/api/src` |
| OWASP ZAP | 2.17.0, passive only | 817 messages mirrored through `core/action/sendRequest`, 227 endpoints × 3 identities |
| `npm audit` | npm 11.x, live registry | `apps/web-react` dependency audit |
| `dotnet list package --vulnerable --include-transitive` | .NET SDK 10 | All 24 API projects, nuget.org |
| `curl` / PowerShell | — | Protocol-level manual testing |
| `sqlcmd` | — | Evidence verification in the isolated database (read-only queries, plus the one documented membership fixture) |
| CodeQL | **not installed** | Blocked — see §7 |
| Docker / Compose | **not installed** | Blocked — see §7 |

---

## 3. Methodology and evidence standard

### 3.1 Phases

1. **Reconnaissance** — OpenAPI extraction (227 GET endpoints, parameter inventory), route/controller mapping, middleware chain analysis.
2. **Static analysis** — Semgrep plus targeted manual review of authentication, authorisation middleware, multi-tenancy filters, document storage and upload validation.
3. **Configuration review** — secrets, JWT lifetimes, CORS, rate limiting, security headers, reverse-proxy behaviour, container topology.
4. **Dynamic authentication testing** — credential handling, token tampering, algorithm confusion, rotation/reuse, revocation, rate limiting, timing behaviour, tenant selection.
5. **Dynamic authorisation testing** — anonymous sweep, RBAC boundaries, cross-project object access (read, list-filter and write), ownership checks on personal resources, privilege escalation attempts, mass assignment.
6. **Input security testing** — 918 injection probes across every string query parameter, stored-XSS payloads, upload type/name/size abuse, path traversal.
7. **Business-logic testing** — settlement arithmetic, overpayment, idempotency, workflow state injection, assignment constraints.
8. **Dependency and passive scanning** — `npm audit`, `dotnet list package --vulnerable`, ZAP passive rules over authenticated traffic.

### 3.2 Evidence standard used in this report

A finding is marked **Confirmed** only when at least one of the following exists:

* an executed request/response pair (status code, size, body excerpt) reproduced more than once, or
* a code path read directly, with the relevant file and line cited, or
* a scanner output corroborated by a manual check.

Anything that could not be executed is listed in §7 as **Blocked**, never as passed. All tokens, cookies and secrets are redacted as `<redacted>`; the only secret reproduced verbatim is the one already committed to the repository (F-01), because it must be rotated.

---

## 4. Coverage matrix

### 4.1 OWASP Top 10 (2021)

| ID | Category | Status | Evidence / note |
|---|---|---|---|
| A01 | Broken Access Control | **Tested** | Cross-project read/list/write blocked (404); foreign-resource time entry blocked (403); role escalation blocked (403); directory over-disclosure F-04 |
| A02 | Cryptographic Failures | **Tested (partial)** | JWT key management F-01; no TLS/HSTS F-07; PBKDF2 password hashing confirmed by code; no TLS inspection possible (no TLS listener) |
| A03 | Injection | **Tested** | 918 SQLi probes, zero anomalies; zero raw-SQL call sites; stored-XSS sanitiser verified by unit test execution |
| A04 | Insecure Design | **Tested** | Settlement/overpayment/idempotency, workflow initial-state enforcement, self-approval prohibition |
| A05 | Security Misconfiguration | **Tested** | Headers, Swagger, error handling, CORS, secrets, Compose topology |
| A06 | Vulnerable & Outdated Components | **Tested** | `npm audit` 16 advisories; .NET clean |
| A07 | Identification & Authentication Failures | **Tested** | Timing oracle, lockout, rate limit, rotation, revocation, `alg=none`, tampering |
| A08 | Software & Data Integrity Failures | **Partial** | Upload content-type/extension controls and traversal defence verified; no artifact signing/SBOM review (not in scope) |
| A09 | Logging & Monitoring Failures | **Tested** | Audit log exists for entity changes; no authentication event logging (F-14) |
| A10 | SSRF | **Reviewed** | Only outbound-capable paths are directory integrations, which had no configured connections; no reachable SSRF sink |

### 4.2 OWASP API Security Top 10 (2023)

| ID | Category | Status | Evidence / note |
|---|---|---|---|
| API1 | Broken Object Level Authorization | **Tested** | Team member: own project item `200`, other project item `404`, list filter `404`, `PUT` `404`, comments `404` |
| API2 | Broken Authentication | **Tested** | Timing oracle F-02, lockout gap F-03, tenant fallback F-10 |
| API3 | Broken Object Property Level Authorization | **Tested** | `tenantId`, `id`, `createdBy`, `createdAt` injected on create → ignored, server values persisted |
| API4 | Unrestricted Resource Consumption | **Tested** | No throttling on authenticated API; unbounded `pageSize` on some endpoints (F-09) |
| API5 | Broken Function Level Authorization | **Tested** | `team.member` → admin surfaces (`/api/users`, `/api/audit`, `/api/roles`, `/api/integrations`) all `403` |
| API6 | Unrestricted Access to Sensitive Business Flows | **Tested** | Workflow status injection rejected; self-approval prohibited; assignment constrained to project members |
| API7 | Server Side Request Forgery | **Reviewed** | No reachable sink (no integrations configured) |
| API8 | Security Misconfiguration | **Tested** | F-01, F-06, F-07, F-10 |
| API9 | Improper Inventory Management | **Tested** | 227 endpoints enumerated; Swagger Development-gated; no shadow endpoints found in the OpenAPI surface |
| API10 | Unsafe Consumption of APIs | **Not applicable** | No third-party API consumption reachable in this environment |

### 4.3 WSTG coverage

| Area | Tests executed | Result |
|---|---|---|
| INFO — Information gathering | Endpoint discovery, fingerprinting, Swagger, error pages, headers | Swagger reachable in Development only; stack trace leak F-06 |
| CONF — Configuration | Headers, HSTS, CORS, HTTP methods, cache directives | Missing hardening F-07, cacheable auth response F-08 |
| IDNT — Identity management | Registration/enumeration, role definitions, tenant selection | Enumeration F-02, tenant fallback F-10 |
| ATHN — Authentication | Credential policy, lockout, bypass, remember-me, password change | Lockout gap F-03, policy gap F-12 |
| ATHZ — Authorisation | Path traversal of roles, IDOR, privilege escalation, BOLA | **Passed** (see §6) |
| SESS — Session management | Token storage, rotation, revocation, fixation, CSRF | Rotation/revocation pass; storage F-08; no cookies → CSRF not applicable |
| INPV — Input validation | SQLi, XSS, upload, traversal, mass assignment, type confusion | **Passed** (see §6), plus F-05 integrity defect |
| ERRH — Error handling | Stack traces, verbose errors, status leakage | F-06 |
| CRYP — Cryptography | TLS, key management, hashing | F-01, F-07 |
| BUSL — Business logic | Financial limits, idempotency, workflow, approvals | **Passed** (see §6) |
| CLNT — Client-side | DOM XSS sinks, storage, CSP, dependency risk | F-08, F-13 |

### 4.4 Test volume

| Test class | Requests / cases |
|---|---|
| Anonymous endpoint sweep | 227 |
| Role-based sweep (`team.member`, `admin`) | 454 |
| Injection probes | 918 |
| ZAP passive messages | 817 |
| Manual protocol tests | ≈120 |
| Sanitiser unit tests executed | 4 |

---

## 5. Confirmed findings

### F-01 — Publicly known JWT signing key ships in `appsettings.json` and silently defeats the production fail-fast guard

| Attribute | Value |
|---|---|
| Severity | **High** (latent: committed baseline; conditional on that baseline being deployed) |
| CVSS v3.1 (indicative) | `AV:N/AC:H/PR:N/UI:N/S:U/C:H/I:H/A:N` ≈ 7.4 for the latent case; ≈ 5.3 as tested (no runtime use) |
| CWE | CWE-321 (Use of Hard-coded Cryptographic Key), CWE-798 (Use of Hard-coded Credentials) |
| OWASP | A02:2021 Cryptographic Failures, A05:2021 Security Misconfiguration; API8:2023 |
| Affected | `apps/api/src/NEXUS.Api/appsettings.json` (at `HEAD`; removed in the uncommitted working tree), `Program.cs:100-115` |
| Status | **Confirmed** in source and git history. **Not in use at runtime** in the tested instance — see the test-time note below and §7.1 |

**Description.**
`Program.cs` implements a correct guard: if `Jwt:Secret` is missing it throws outside Development and generates an ephemeral key inside Development. However `appsettings.json` — which is loaded in *every* environment — already contains:

```json
"Jwt": {
  "Secret": "NEXUS-Demo-Secret-Key-Minimum-16-Chars!",
  "Issuer": "NEXUS",
  "Audience": "NEXUS-API",
  "ExpiryHours": 8,
  "RefreshTokenLifetimeDays": 7
}
```

In the committed baseline the key is present, so the `IsNullOrWhiteSpace` branch at `Program.cs:103` never executes and the fail-fast never fires. Any deployment built from that baseline that does not explicitly override `Jwt__Secret` runs on a key that is public by construction.

**Test-time verification (2026-10-29).** The value above is present in `HEAD` (`1b3dc5c`, and in history back to the first commit) but was **already absent from the uncommitted working tree** (file last modified 2026-10-27 17:37), before the test window. The tested process therefore took the Development branch and signed with an ephemeral key. This was verified offline, not inferred: recomputing `HMAC-SHA256(header.payload)` with the committed secret for all four tokens captured during testing produced **no match** (`team`, `pm`, `fin`, `admin`). Consequence: F-01 is a **latent repository/history exposure**, not a live key on the tested host. It remains High because the committed value is public, is still what a clean checkout builds, and cannot be un-published from history.

**Reproduction.**
1. Read `apps/api/src/NEXUS.Api/appsettings.json` — the value is in plaintext.
2. Confirm the guard: `Program.cs:103-113` only throws when the value is blank.
3. Demonstrate the latent path (not executed in this engagement): check out `HEAD` into a clean directory and start it with `ASPNETCORE_ENVIRONMENT=Production` and no `Jwt__Secret`. `Program.cs:105` sees a non-blank value from `appsettings.json`, skips the throw, and `Require` accepts it (the demo value is 40 bytes, above the 32-byte minimum) — the host starts on public key material.
4. Verify token structure: `iss=NEXUS`, `aud=NEXUS-API`, `alg=HS256`, `exp` = issue + 8 h.
5. Verify that the tested host did **not** use it: recompute the HMAC for a captured token with the committed secret — no match (see the test-time verification above).

**Exploitability analysis (why High rather than Critical).**
Two mitigations stand between a leaked key and account takeover, and they are of different strength:

*Executed (2026-10-28, ephemeral-key instance):*

| Probe | Result |
|---|---|
| `A4` — payload edited to `admin`, original signature kept | `401`, log `IDX10517 Signature validation failed` |
| `A5` — `alg=none`, admin claims, empty signature | `401` |

*Established by code review only (not executed — the tested instance used an ephemeral key, so a correctly signed forged token could not be produced):*

| Forged token | Expected result | Basis |
|---|---|---|
| Valid signature, `sid` = random GUID, `nameidentifier` = admin | `401` — no matching session | `UserSessionValidator.cs:13-30` requires `session.Id == sid`, `session.UserId == userId`, `session.TenantId == tenantId`, not revoked, not expired, user active and not deleted |
| Valid signature, `sid` from attacker's own session, `nameidentifier` = admin | `401` — session/user mismatch | Same predicate |
| Valid signature, valid `sid` of a victim, elevated role claim | `200`, but no privilege gain | Permissions are rebuilt from the database per request, so a role claim in the token is not authoritative |

The mitigation is real but unproven dynamically: it depends on every token consumer calling the session validator, and it does not stop minting a token for a *victim's* session id. That is why the finding stays High in the latent case rather than Critical.

Residual risk: the key allows unlimited minting of tokens bound to any session identifier an attacker can observe (logs, proxy traces, browser storage, a previously stolen token), and it removes the only cryptographic barrier between "read the repository" and "produce valid tokens".

**Business impact.** Loss of the signing key's confidentiality invalidates the trust boundary for every tenant. If the value was ever deployed, rotating it forcibly logs out all users and the exposure window is unknown. In the tested environment it was not in use, which limits present impact to the repository and history.

**Root cause.** A demo/default value was left in the base configuration file rather than in a Development-only file or a secret store, and the guard was written to test *presence* instead of *provenance*.

**Remediation.**
1. Delete `Jwt:Secret` from `appsettings.json` entirely; keep an ephemeral key for Development (already implemented) or a `UserSecrets`-backed value. **Status: the deletion exists only in the uncommitted working tree (file last modified 2026-10-27) — it must be committed, and the value still needs rotation because history retains it.**
2. Add a startup assertion that rejects known/demo key material (length ≥ 32 bytes, not matching a denylist, not equal to any value committed to git).
3. Rotate the key in every environment; existing sessions become invalid as a side effect.
4. Add a secret scanner (gitleaks/trufflehog) to CI and pre-commit.
5. Purge the value from git history where practical, and treat prior history as compromised.

**Retest criteria.**
* `grep -r "NEXUS-Demo-Secret" .` returns nothing outside test fixtures.
* Production startup with no `Jwt__Secret` fails with a clear error.
* Startup with the demo value fails validation.
* CI secret scan is blocking.

---

### F-02 — Username enumeration through a login timing oracle

| Attribute | Value |
|---|---|
| Severity | **Medium** |
| CVSS v3.1 (indicative) | `AV:N/AC:L/PR:N/UI:N/S:U/C:L/I:N/A:N` ≈ 5.3 |
| CWE | CWE-208 (Observable Timing Discrepancy), CWE-204 (Observable Response Discrepancy) |
| OWASP | A07:2021; API2:2023 |
| Affected | `POST /api/auth/login` |
| Status | **Confirmed** (dynamic) |

**Description.**
The endpoint returns an identical body and status for unknown accounts and wrong passwords (`401 {"title":"Invalid email or password"}`), but performs password verification only when the account exists. The measurable difference identifies valid accounts.

**Reproduction.**
1. Send five `POST /api/auth/login` requests with a known account and a wrong password; record elapsed time.
2. Send five requests with a non-existent account; record elapsed time.
3. Compare. Requests were spaced ≥ 7 s to stay outside the 10/min limiter.

**Evidence.**

```
existing account: 179, 218, 247, 187, 333 ms   (mean 232.8 ms)
unknown account :  10,  10,   6,  12,  17 ms   (mean  11.0 ms)
```

**Business impact.** Reliable enumeration of the tenant's staff (validated against the directory leak in F-04) directly enables targeted phishing and password guessing, and reveals account existence to unauthenticated parties.

**Root cause.** Early return at `DemoAuthService.cs:42-45` when the user lookup fails, before any hashing work; there is no dummy hash verification on the negative path.

**Remediation.**
1. On a missing user, still run `_passwordHasher.VerifyHashedPassword` against a fixed pre-computed dummy hash before returning.
2. Normalise response bodies, headers and status codes.
3. Add randomised jitter only as a supplement — not as the primary control.

**Retest criteria.**
* Mean response-time delta between existing and unknown accounts < 10 % across 20 samples.
* Identical response body, headers and status for both cases.

---

### F-03 — No per-account lockout or backoff; throttling is per source IP only

| Attribute | Value |
|---|---|
| Severity | **Medium** |
| CVSS v3.1 (indicative) | `AV:N/AC:H/PR:N/UI:N/S:U/C:L/I:L/A:N` ≈ 4.8 |
| CWE | CWE-307 (Improper Restriction of Excessive Authentication Attempts) |
| OWASP | A07:2021; API2:2023 |
| Affected | `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/stakeholder-invitations/validate` |
| Status | **Confirmed** (dynamic + code review) |

**Description.**
Only a fixed-window limiter (10 requests/minute, keyed on `RemoteIpAddress`) exists. There is no `AccessFailedCount`, lockout window, exponential backoff, CAPTCHA or risk-based challenge anywhere in the Identity module, so a distributed attacker with N addresses gets 10·N attempts per minute per account indefinitely.

**Reproduction.**
1. Send 13 failed logins from one address: attempts 1-10 → `401`, attempts 11-13 → `429`. Confirms the limiter works.
2. Repeat with rotating `X-Forwarded-For` / `X-Real-IP` headers: identical `429` at attempt 11 — confirms the limiter keys on the socket address and cannot be spoofed (good), but also confirms there is no per-account state.
3. Repeat from a second address (second interface): the counter resets, demonstrating that only addresses are tracked.
4. Code review: no lockout fields or counters in `User`, `DemoAuthService` or `AuthController`.

**Evidence.**

```
rotating XFF codes: 1=401 2=401 … 10=401 11=429 12=429 13=429
grep -r "AccessFailedCount\|Lockout" apps/api/src → no matches
grep -r "EnableRateLimiting" apps/api/src → 4 attributes (login, refresh, 2 invitation endpoints)
```

**Business impact.** Combined with F-02, an attacker can enumerate accounts and then run low-and-slow guessing from a small proxy pool. A single weak password yields full tenant data access.

**Root cause.** Authentication hardening stopped at transport-level throttling; no account-level risk model was implemented.

**Remediation.**
1. Track failed attempts per account (`AccessFailedCount`, `LastFailedAt`) with progressive delay and a lockout after e.g. 10 failures / 15 min, surfaced to the user as a generic message plus an out-of-band notification.
2. Add a global (cross-IP) attempt budget per account, e.g. 50/hour, independent of source address.
3. Alert on spikes; see F-14.
4. Consider breached-password screening at login and password change.

**Retest criteria.**
* Account locks after the configured threshold regardless of source IP.
* Lockout clears by policy or administrator action, and is audited.
* Legitimate user is notified of lockout.

---

### F-04 — `/api/users/lookup` discloses the full tenant staff directory to any authenticated user

| Attribute | Value |
|---|---|
| Severity | **Medium** |
| CVSS v3.1 (indicative) | `AV:N/AC:L/PR:L/UI:N/S:U/C:L/I:N/A:N` ≈ 4.3 |
| CWE | CWE-200 (Exposure of Sensitive Information), CWE-213 (Exposure of Sensitive Information Due to Incompatible Policies) |
| OWASP | A01:2021; API3:2023 |
| Affected | `GET /api/users/lookup` (`UsersController.cs`) |
| Status | **Confirmed** (dynamic) |

**Description.**
The endpoint is decorated with `[Authorize]` only (no policy), and returns every user in the tenant with `id`, `fullName`, `email` and `jobTitle`. A `team.member` — whose role has no `users.read` permission — receives the complete list.

**Reproduction.**
1. Authenticate as `pentest.team@example.test` (`team.member`).
2. `GET /api/users/lookup` with the bearer token.

**Evidence.**

```
HTTP/1.1 200 OK, 994 bytes
[{"id":"aaaaaaaa-…","fullName":"Alex Admin","email":"admin@nexus.com","jobTitle":"System Administrator"},
 … 7 users including personal Gmail addresses …]
```

Contrast: `GET /api/users` (the real user directory) returns `403` for the same identity.

**Business impact.** The lowest-privileged role can harvest every employee's name, corporate email and job title — the reconnaissance dataset for phishing, targeting privileged accounts, and pairing with F-02/F-03.

**Root cause.** A convenience endpoint added for pickers was not constrained to the minimum projection or to a permission (`users.read` / `Team.View`), nor scoped to the caller's projects.

**Remediation.**
1. Require a policy (`users.read` or `Team.View`) and return `403` otherwise.
2. Return only `id` + `displayName` by default; expose `email`/`jobTitle` only when the caller shares a project with the subject (or holds `users.read`).
3. Optionally require a `search` term with a minimum length to prevent bulk listing.

**Retest criteria.**
* `team.member` receives `403` (or an empty/redacted projection).
* `admin` receives the full projection.
* Response size scales with the caller's project scope, not tenant size.

---

### F-05 — Document uploads persist `TenantId = Guid.Empty`, making every upload invisible and orphaning rows

| Attribute | Value |
|---|---|
| Severity | **Medium** |
| CVSS v3.1 (indicative) | `AV:N/AC:L/PR:L/UI:N/S:U/C:N/I:L/A:L` ≈ 5.4 |
| CWE | CWE-284 (Improper Access Control) — data-integrity variant; see note |
| OWASP | A04:2021 Insecure Design, A01:2021 |
| Affected | `POST /api/documents`, `GET /api/documents`, `GET /api/documents/{id}`, `…/preview`, `…/download`; `DocumentService.CreateAsync` |
| Status | **Confirmed** (dynamic + database evidence + code) |

**Description.**
`DocumentService.CreateAsync` constructs `new Document { … }` without assigning `TenantId` and does not rely on an interceptor that would fill it. The row is written with `TenantId = 00000000-0000-0000-0000-000000000000`. Because the entity is covered by the global tenant query filter, the document is then invisible to the very user who created it.

**Reproduction.**
1. As `admin@nexus.com` (holder of `Document.Create`), upload a text file:
   `POST /api/documents` (multipart) → `201` with a document id.
2. Immediately `GET /api/documents/{id}` → `404`.
3. `GET /api/documents` → `200 {"items":[],"totalCount":0}`.
4. Inspect the row in the isolated database.

**Evidence.**

```
POST /api/documents            → 201 {"document":{"id":"f03ee4f7-…","fileName":"u.txt", …}}
GET  /api/documents/{id}       → 404 {"title":"Not Found", …}
GET  /api/documents            → 200 {"items":[],"pageSize":20,"totalCount":0}

SQL> SELECT Id, TenantId FROM Documents;
ECCB74B9-…  evil.txt   00000000-0000-0000-0000-000000000000
DED6C56E-…  note.txt   00000000-0000-0000-0000-000000000000
F03EE4F7-…  u.txt      00000000-0000-0000-0000-000000000000
AD626653-…  evil.txt   00000000-0000-0000-0000-000000000000
```

`WorkItemService` sets `TenantId` correctly on the same database, so this is a local omission rather than a platform-wide defect.

**Business impact.**
* Documents are unusable: uploads appear successful but are lost to the user, and storage accumulates orphaned files.
* Tenant-scoped rows with an empty tenant id are a latent isolation hazard: any future query using `IgnoreQueryFilters()`, an admin report, or a storage reaper that groups by `TenantId` will treat these rows as a different population. This is the classic precondition for cross-tenant data exposure in a future change.

**Root cause.** The write path relies on an implicit convention (base entity tenant assignment) that is not enforced for `Document`; no test asserts `TenantId != Guid.Empty` after creation.

**Remediation.**
1. Assign `TenantId` from the resolved tenant context in `DocumentService.CreateAsync` (mirror `WorkItemService`).
2. Better: enforce it centrally — override `SaveChanges`/`SaveChangesAsync` to populate `TenantId` on any added `ITenantEntity` with an empty value, or fail the save.
3. Add a database `CHECK (TenantId <> '00000000-0000-0000-0000-000000000000')` on tenant-scoped tables after backfilling.
4. Backfill or quarantine existing zero-tenant rows (identify by `TenantId = Guid.Empty`), and reconcile files on disk.
5. Add an integration test: create → read → list → download, asserting non-empty tenant and `200` on re-read.

**Retest criteria.**
* New upload: `201`, then `200` on detail, list and download by the creator.
* `SELECT COUNT(*) FROM Documents WHERE TenantId = '00000000-…'` = 0.
* A cross-tenant user receives `404` for the document.

---

### F-06 — `/api/organizations` returns `500` and leaks a full .NET stack trace in Development

| Attribute | Value |
|---|---|
| Severity | **Medium** |
| CVSS v3.1 (indicative) | `AV:N/AC:L/PR:L/UI:N/S:U/C:L/I:N/A:L` ≈ 4.3 |
| CWE | CWE-209 (Generation of Error Message Containing Sensitive Information), CWE-755 (Improper Handling of Exceptional Conditions) |
| OWASP | A05:2021, A09:2021; API8:2023 |
| Affected | `GET /api/organizations` |
| Status | **Confirmed** (dynamic, two identities) |

**Description.**
Serialising the organisation tree hits an entity cycle. In Development the developer exception page returns the exception type, full message and the complete stack trace (including file paths and middleware order) instead of a problem-details payload. The endpoint is unusable for every caller.

**Reproduction.**
1. Authenticate as `admin@nexus.com` or `pentest.team@example.test`.
2. `GET /api/organizations`.

**Evidence.**

```
HTTP/1.1 500 Internal Server Error   (109 bytes when the Accept header is JSON; full trace otherwise)

System.Text.Json.JsonException: A possible object cycle was detected. …
Path: $.Items.BusinessUnits.Organization.BusinessUnits.Organization.BusinessUnits.Organization.…
   at System.Text.Json.ThrowHelper.ThrowJsonException_SerializerCycleDetected(Int32 maxDepth)
   at System.Text.Json.Serialization.Metadata.JsonPropertyInfo`1.GetMemberAndWriteJson(…)
   …
   at NEXUS.Api.Management.ProjectResourceScopeMiddleware.InvokeAsync(…)
   at NEXUS.Api.Management.ManagementContextMiddleware.InvokeAsync(…)
   at NEXUS.Api.Management.ProjectPublicIdRoutingMiddleware.InvokeAsync(…)
   at NEXUS.Modules.Identity.Infrastructure.UserPermissionClaimsMiddleware.InvokeAsync(…)
   …
HEADERS
======
Authorization: Bearer <redacted>
```

**Business impact.** The organisations feature is entirely unavailable. When a Development instance is reachable by anyone other than the developer (shared QA host, tunnel, demo laptop), the trace discloses internal namespaces, middleware ordering, file-system layout and framework versions — high-value material for targeted attacks.

**Root cause.** A cyclic navigation graph (`Organisation → BusinessUnits → Organisation`) is returned directly from the controller instead of a projection DTO, and `DeveloperExceptionPageMiddleware` is active because the environment is Development.

**Remediation.**
1. Project the response to a DTO (or configure `ReferenceHandler.IgnoreCycles` for that specific serialisation) — do not return EF entities.
2. Add `app.UseExceptionHandler(...)` returning RFC 7807 problem details with a correlation id, and never enable the developer exception page on any shared host.
3. Add an integration test asserting `200` and a cycle-free payload for `/api/organizations`.

**Retest criteria.**
* `GET /api/organizations` → `200` with a finite, acyclic JSON tree.
* Any induced server error returns `500` with a generic body and a correlation id, no stack trace, no file paths.

---

### F-07 — No security headers on either tier and no TLS/HSTS in the documented deployment

| Attribute | Value |
|---|---|
| Severity | **Medium** |
| CVSS v3.1 (indicative) | `AV:N/AC:L/PR:N/UI:R/S:C/C:L/I:L/A:N` ≈ 6.1 |
| CWE | CWE-693 (Protection Mechanism Failure), CWE-319 (Cleartext Transmission), CWE-1021 (Improper Restriction of Rendered UI Layers) |
| OWASP | A05:2021, A02:2021 |
| Affected | All API responses; `apps/web-react/nginx.conf`; `docker-compose.yml` |
| Status | **Confirmed** (dynamic + configuration) |

**Description.**
Neither the API nor the SPA emits baseline hardening headers. ZAP's passive scanner raised the `X-Content-Type-Options` rule on **79 of 79** distinct authenticated URLs, and manual inspection shows the rest are absent too. The Compose topology publishes the API on plain HTTP port 5000 and nginx on port 80, with no TLS listener and no HSTS.

**Reproduction.**
1. `curl -D - http://127.0.0.1:5001/health` and `…/api/projects` → note the complete header set.
2. Run ZAP passive scanning across authenticated traffic (817 messages) and read `/JSON/alert/view/alerts/`.
3. Inspect `apps/web-react/nginx.conf` — the `server` block has no `add_header` directives.
4. Inspect `docker-compose.yml` — ports `5000:5000` and `4000:80`, no TLS termination.

**Evidence.**

```
HTTP/1.1 200 OK
Pragma: no-cache
Cache-Control: no-store, no-cache
Content-Type: text/plain
Server: Kestrel
Date: …
   ← no Strict-Transport-Security
   ← no X-Content-Type-Options
   ← no Content-Security-Policy / frame-ancestors
   ← no Referrer-Policy
   ← no Permissions-Policy
   ← no Cross-Origin-Opener-Policy / Cross-Origin-Resource-Policy

ZAP alerts: 79 × plugin 10021 "X-Content-Type-Options header missing" (Low/Medium)
nginx.conf: no add_header directives at all
```

Note: document preview/download responses *do* set `X-Content-Type-Options: nosniff` and a `Content-Disposition` — the control exists, it is simply not applied globally.

**Business impact.** Without `nosniff`, content-type confusion is possible for any response an attacker can influence. Without CSP, any XSS (F-08) executes unconstrained. Without `frame-ancestors`, the app can be framed for clickjacking — meaningful for approval and payment actions. Without TLS, credentials and bearer tokens travel in clear text on the network path.

**Root cause.** Headers were added ad hoc to the highest-risk handler (file download) instead of as middleware or at the reverse proxy; TLS termination was deferred and never tracked.

**Remediation.**
1. Add a response-header middleware (or nginx `add_header`) applying at minimum:
   `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY` + CSP `frame-ancestors 'none'`, `Permissions-Policy` with an explicit allowlist, `Cross-Origin-Opener-Policy: same-origin`.
2. Introduce a CSP with a nonce/hash allowlist; start in `Report-Only`, then enforce. Note the SPA currently relies on inline `<script>` bootstrap — plan the change.
3. Terminate TLS at the edge (nginx or an ingress) and emit `Strict-Transport-Security: max-age=31536000; includeSubDomains` on HTTPS responses only.
4. Redirect HTTP → HTTPS at the edge and stop publishing the API port publicly.

**Retest criteria.**
* Header assertions in CI for every response class (success, 4xx, 5xx, file download).
* ZAP passive scan produces zero header alerts.
* HSTS present on all HTTPS responses; HTTP requests redirect.
* CSP enforced with no `unsafe-inline` in `script-src`.

---

### F-08 — Tokens stored in `localStorage`, no CSP, and auth responses are cacheable

| Attribute | Value |
|---|---|
| Severity | **Medium** |
| CVSS v3.1 (indicative) | `AV:N/AC:H/PR:N/UI:N/S:U/C:H/I:H/A:N` ≈ 5.9 (requires an XSS precondition) |
| CWE | CWE-522 (Insufficiently Protected Credentials), CWE-539 (Use of Persistent Cookies Containing Sensitive Information — analogous), CWE-79 |
| OWASP | A07:2021, A03:2021 |
| Affected | `apps/web-react/src/api/client.ts`, `POST /api/auth/login`, `POST /api/auth/refresh` |
| Status | **Confirmed** (code + dynamic headers) |

**Description.**
Access and refresh tokens are persisted in `localStorage`:

```ts
// apps/web-react/src/api/client.ts
window.localStorage.setItem(ACCESS_TOKEN_KEY, access);
window.localStorage.setItem(REFRESH_TOKEN_KEY, refresh);
window.localStorage.setItem(SESSION_ID_KEY, clientSessionId);
```

`localStorage` is readable by any JavaScript in the origin, so a single XSS — in this application or in any third-party script — yields an 8-hour access token and a **7-day** refresh token. There is no CSP to constrain injection, and the login/refresh responses (which carry both tokens) are returned without `Cache-Control: no-store`.

**Reproduction.**
1. Log in through the SPA and inspect `localStorage` (`nexus_access_token`, `nexus_refresh_token`).
2. Observe the auth response headers.

**Evidence.**

```
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: …
Server: Kestrel
Transfer-Encoding: chunked
   ← no Cache-Control: no-store
   ← no Pragma: no-cache

JWT claims: exp = issue + 8 h; refresh session ExpiresAt = issue + 7 d
Refresh token rotation confirmed; reuse of a rotated token → 401
```

**Business impact.** Any XSS becomes durable account takeover, including offline use of the refresh token for a week. Shared or kiosk machines retain credentials after the tab closes.

**Root cause.** Storage choice was driven by simplicity (no cookie/CSRF plumbing), and cache directives were never set on token-bearing responses.

**Remediation.**
1. Preferred: move refresh tokens to `HttpOnly; Secure; SameSite=Strict` cookies scoped to `/api/auth`, keep the access token in memory only, and add CSRF protection for the refresh endpoint (double-submit token or custom header).
2. If `localStorage` must remain short-term: reduce access-token lifetime to ≤ 15 min, refresh lifetime to ≤ 24 h with idle timeout, and bind refresh tokens to a device fingerprint.
3. Set `Cache-Control: no-store` and `Pragma: no-cache` on all authentication responses.
4. Enforce CSP (F-07) and add `Trusted Types` once the CSP is stable.

**Retest criteria.**
* `localStorage`/`sessionStorage` contain no bearer or refresh token.
* Refresh cookie is `HttpOnly`, `Secure`, `SameSite=Strict`, path-scoped.
* Auth responses carry `Cache-Control: no-store`.
* Access-token lifetime ≤ 15 min.

---

### F-09 — No request throttling on the authenticated API and unbounded `pageSize` on some endpoints

| Attribute | Value |
|---|---|
| Severity | **Low** |
| CVSS v3.1 (indicative) | `AV:N/AC:L/PR:L/UI:N/S:U/C:N/I:N/A:L` ≈ 3.1 |
| CWE | CWE-770 (Allocation of Resources Without Limits or Throttling) |
| OWASP | API4:2023 |
| Affected | All authenticated endpoints; `GET /api/projects` (and peers that skip clamping) |
| Status | **Confirmed** (dynamic + code) |

**Description.**
Only four endpoint attributes use the `auth` rate-limit policy. The comment in `Program.cs:310` refers to a "global fallback limiter", but no such limiter is configured and no global policy is applied. `ProjectService.GetPagedAsync` passes the caller-supplied `pageSize` straight into `Take(pageSize)` with no ceiling; other services clamp to 100.

**Reproduction.**
1. Issue 80 rapid authenticated `GET /api/projects` requests → 80 × `200`, no `429`.
2. `GET /api/projects?pageSize=1000000` → `200`; the response echoes `pageSize: 1000000`.
3. `GET /api/audit?pageSize=1000000` and `/api/work-items?pageSize=1000000` → clamped to 100 (control present, applied inconsistently).

**Evidence.**

```
80 rapid GET /api/projects -> 200=80
GET /api/projects?pageSize=1000000 -> 200 (pageSize echoed)
GET /api/audit?pageSize=1000000 -> pageSize 100
grep -r "EnableRateLimiting" → 4 attributes only
```

**Business impact.** A single authenticated account can drive sustained query load (large page windows, width-heavy projections) with no backpressure, degrading availability for other tenants sharing the database. Not exploited here beyond confirming the absence of limits.

**Root cause.** Pagination clamping is implemented per service instead of in a shared paging helper; the global limiter described in the comment was never implemented.

**Remediation.**
1. Centralise pagination: clamp `pageSize` to [1, 100] and `page` to a sane maximum in one helper used by every service (or a model binder/filter).
2. Add a global rate-limit policy (e.g. 300 req/min/user, 1000 req/min/IP) plus stricter limits on expensive report endpoints, and return `429` with `Retry-After`.
3. Correct the misleading comment in `Program.cs`.

**Retest criteria.**
* Every paged endpoint clamps `pageSize` to 100 and validates `page ≥ 1`.
* Sustained traffic above the configured threshold returns `429`.
* No endpoint accepts more than the documented maximum.

---

### F-10 — `tenantId` is optional at login and silently falls back to a default tenant

| Attribute | Value |
|---|---|
| Severity | **Low** |
| CVSS v3.1 (indicative) | `AV:N/AC:L/PR:N/UI:N/S:U/C:L/I:N/A:N` ≈ 3.7 |
| CWE | CWE-1188 (Insecure Default Initialization), CWE-284 |
| OWASP | A05:2021, A07:2021; API8:2023 |
| Affected | `POST /api/auth/login` |
| Status | **Confirmed** (dynamic + code) |

**Description.**
If `tenantId` is omitted, the service resolves a default/demo tenant rather than rejecting the request. If an arbitrary value is supplied but no matching account exists, the response is a generic `401`. In a multi-tenant deployment this hides tenant-selection errors and creates ambiguity when the same email exists in more than one tenant.

**Reproduction.**
1. `POST /api/auth/login {"email":"admin@nexus.com","password":"<seeded>"}` → `200`, token with `tenantId = 11111111-1111-1111-1111-111111111111`.
2. `POST /api/auth/login {…,"tenantId":"33333333-3333-3333-3333-333333333333"}` → `401`.

**Evidence.**

```
login without tenantId          -> 200  (token tenantId 11111111-…)
login with unknown tenantId     -> 401  {"title":"Invalid email or password", …}
```

**Business impact.** In a shared deployment the default-tenant behaviour can authenticate a user into a tenant they did not intend, and it makes per-tenant lockout, branding and policy enforcement unreliable. Enumeration of tenant ids is also made cheaper on the success path.

**Root cause.** A demo-era convenience path (`DemoAuthService` tenant resolution) was retained without a single-tenant guard.

**Remediation.**
1. Require `tenantId` (or a tenant subdomain/header resolved at the edge) in all environments; fail with a clear `400` when absent.
2. If a single-tenant mode is needed, make it an explicit configuration flag that pins the tenant and rejects any other value.
3. Return the same generic error for unknown tenant and unknown account.

**Retest criteria.**
* Login without a tenant identifier → `400` with a documented error.
* A tenant id that does not contain the account → generic `401`.
* No default tenant constant remains in the authentication path.

---

### F-11 — Documented seeded credentials and demo accounts with known passwords

| Attribute | Value |
|---|---|
| Severity | **Low** (Development-scoped) |
| CVSS v3.1 (indicative) | `AV:N/AC:L/PR:N/UI:N/S:U/C:L/I:L/A:N` ≈ 4.3 |
| CWE | CWE-798 (Use of Hard-coded Credentials), CWE-521 (Weak Password Requirements) |
| OWASP | A07:2021; API8:2023 |
| Affected | `SeedDataProvider`, tenant administrator account, demo users |
| Status | **Confirmed** (functional login with published credentials) |

**Description.**
The seeded tenant administrator (`admin@nexus.com` / `Demo123!`) has no forced password change (`mustChangePassword: false`) and holds 84 permissions. Additional demo users with published passwords exist in the same tenant.

**Reproduction.**
1. `POST /api/auth/login {"email":"admin@nexus.com","password":"Demo123!"}` → `200`, full administrator token.
2. `GET /api/auth/me` → `mustChangePassword: false`.

**Evidence.**

```
POST /api/auth/login -> 200
{"email":"admin@nexus.com","roles":["admin"],"mustChangePassword":false,
 "permissions":[ …84 entries… ]}
```

**Business impact.** Seeding is Development-gated in `Program.cs`, so the direct production risk is low — but shared development, demo and "temporary staging" environments are routinely promoted from these seeds, and this report itself authenticated with the published password. A demo laptop on a conference network is a full-data exposure.

**Root cause.** Demo credentials were designed for convenience and are not marked as ephemeral.

**Remediation.**
1. Generate the seed administrator password at provisioning time and print it once to the console/log (or write it to a git-ignored file), with `mustChangePassword = true` and a 24-hour expiry (mechanism already exists).
2. Refuse to seed when an environment variable such as `NEXUS_ALLOW_DEMO_SEED` is absent.
3. Remove the credentials from all documentation and replace them with a provisioning command.

**Retest criteria.**
* Fresh seed produces a random password, `mustChangePassword = true`, forced rotation on first login.
* The literal `Demo123!` does not authenticate.
* Seeding outside Development requires explicit opt-in.

---

### F-12 — Password policy is length-only, with no breached-password screening or reuse control

| Attribute | Value |
|---|---|
| Severity | **Low** |
| CVSS v3.1 (indicative) | `AV:N/AC:H/PR:N/UI:N/S:U/C:L/I:N/A:N` ≈ 3.7 |
| CWE | CWE-521 (Weak Password Requirements) |
| OWASP | A07:2021 |
| Affected | `ChangeInitialPasswordAsync`, `LocalPasswordPolicyOptions` |
| Status | **Confirmed** (code) |

**Description.**
`LocalPasswordPolicyOptions` enforces only `MinimumLength = 12` / `MaximumLength = 128`. There is no check against breached-password corpora, no blocklist of the product name or tenant name, no reuse history, and no entropy/composition guidance. Twelve identical characters are accepted.

**Reproduction.**
1. Review `apps/api/src/NEXUS.Modules.Identity/Application/LocalPasswordPolicyOptions.cs`.
2. Review `DemoAuthService.ChangeInitialPasswordAsync` (length check only, plus "must differ from current").

**Evidence.**

```csharp
if (newPassword.Length < _passwordPolicy.MinimumLength || newPassword.Length > _passwordPolicy.MaximumLength)
    return … Validation("The new password must be between … characters.");
if (string.Equals(command.CurrentPassword, newPassword, StringComparison.Ordinal))
    return … Validation("The new password must be different from the temporary password.");
```

**Business impact.** Length-only rules permit predictable passwords (`passwordpassword`, `qwertyuiop12`) that survive the missing lockout (F-03). Brute-force resistance then depends entirely on the slow hash and transport throttling.

**Root cause.** Policy scope was limited to what the framework's options object exposed.

**Remediation.**
1. Add a breached-password check (k-anonymity range API or an offline list) at password set/change.
2. Reject passwords containing the product name, tenant name, email local part, or the user's name.
3. Store a short password history (5) and reject reuse.
4. Keep length-first guidance (NIST SP 800-63B) rather than adding composition rules.

**Retest criteria.**
* A known-breached password is rejected with actionable guidance.
* Reusing any of the last 5 passwords is rejected.
* Policy response (`GET /api/auth/initial-password-policy`) advertises the new rules and the UI reflects them.

---

### F-13 — Dependency advisories: 16 npm findings (4 runtime-relevant), .NET clean

| Attribute | Value |
|---|---|
| Severity | **Low** |
| CVSS v3.1 | Per advisory; highest runtime-relevant is 7.5 |
| CWE | CWE-1104 (Use of Unmaintained Third-Party Components), CWE-1395 |
| OWASP | A06:2021 |
| Affected | `apps/web-react` (`react-router-dom` 6.30.6, `zod` 3.25.76, `exceljs`→`uuid` 8.3.2, `qs` 6.16.0); build/test tooling |
| Status | **Confirmed** (scanner output, versions verified in `node_modules`) |

**Description.**
`npm audit` reports 3 critical, 4 high and 9 moderate advisories. Most affect build/test tooling that never ships to users. The runtime-relevant subset is:

| Package | Version | Advisory | Real exposure |
|---|---|---|---|
| `react-router` / `react-router-dom` | 6.30.6 | GHSA-wrjc-x8rr-h8h6 — open redirect via backslash in `<Link>`/`useNavigate` (CWE-601); GHSA-337j-9hxr-rhxg — SSR hydration constructor injection (CWE-470) | Open redirect is reachable if any navigational target is user-influenced; the SSR advisory does not apply (client-only SPA) |
| `zod` | 3.25.76 | GHSA-mrhx-c7xc-m3w5 — memory exhaustion validating oversized constrained collections (CWE-770) | Requires attacker-controlled array input validated client-side; limited to browser DoS |
| `uuid` (via `exceljs`) | 8.3.2 | GHSA-w5hq-g745-h8pq — missing buffer bounds check in v3/v5/v6 with caller buffer (CWE-787) | Not called with a caller-supplied buffer by `exceljs`; effectively unreachable |
| `qs` | 6.16.0 | GHSA-6pq8-jhm2-2vcw — `stringify` throws on hostile `toString` (CWE-248) | Transitive, build-time |

Build/test-only (fix requires a Vitest 5 major upgrade): `vite` 6.4.3 (8 advisories incl. `launch-editor` Windows command handling), `vitest` 2.1.9, `@vitest/coverage-v8`, `tinypool`, `esbuild`, `postcss` 8.4.47, `body-parser` 2.3.0 (not used by the SPA at runtime).

`dotnet list package --vulnerable --include-transitive` across all 24 API projects: **no vulnerable packages**.

**Evidence.**

```
npm audit metadata: {"moderate":9,"high":4,"critical":3,"total":16}
installed: vite 6.4.3, vitest 2.1.9, postcss 8.4.47, zod 3.25.76,
           react-router-dom 6.30.6, exceljs 4.4.0, uuid 8.3.2, qs 6.16.0
dotnet: "The given project NEXUS.* has no vulnerable packages given the current sources." (24/24)
```

**Business impact.** Low today; the open-redirect advisory is the only plausible user-facing impact and requires an attacker-influenced navigation target. The real cost is upgrade debt: the Vitest major bump needed to clear the tooling advisories will get harder with time.

**Root cause.** No automated dependency gate in CI, and dev-tool versions pinned to a major that is now out of support.

**Remediation.**
1. Upgrade `react-router-dom` to ≥ 7.18.5 (major; verify routing/`useNavigate` behaviour).
2. Upgrade `zod` to ≥ 4.6.7 while keeping the validation contracts intact.
3. Replace `exceljs` (unmaintained fork chain) or pin a patched `uuid` via `overrides`.
4. Schedule the Vite/Vitest 5 upgrade separately; add `npm audit --audit-level=high` (with a curated allowlist for dev-only findings) and Dependabot/Renovate to CI.

**Retest criteria.**
* `npm audit --omit=dev` returns zero high/critical.
* CI fails on new high/critical advisories in runtime dependencies.
* Router upgrade passes the navigation and deep-link test suite.

---

### F-14 — No authentication audit trail; audit records lack source IP and user agent

| Attribute | Value |
|---|---|
| Severity | **Low** |
| CVSS v3.1 (indicative) | `AV:N/AC:L/PR:L/UI:N/S:U/C:N/I:L/A:N` ≈ 3.1 |
| CWE | CWE-778 (Insufficient Logging), CWE-223 (Omission of Security-relevant Information) |
| OWASP | A09:2021 |
| Affected | `DemoAuthService` (login/refresh/logout), `AuditEntry` population |
| Status | **Confirmed** (code + API evidence) |

**Description.**
Successful and failed logins, refreshes, lockouts and revocations are not written to the audit store. Entity-level audit entries exist and are queryable, but `ipAddress` and `userAgent` are always `null` in observed records, so even entity changes cannot be attributed to a source.

**Reproduction.**
1. Trigger several failed logins, then a successful login.
2. `GET /api/audit?pageSize=100` as `admin` and search for authentication entities → none.
3. Inspect any audit record → `"ipAddress":null,"userAgent":null`.

**Evidence.**

```json
{"entityType":"WorkItem","action":"Created","userId":"45a59526-…",
 "ipAddress":null,"userAgent":null,"timestamp":"2026-10-28T09:28:02Z"}
```

**Business impact.** Credential-stuffing campaigns (F-03), enumeration sweeps (F-02) and token replay leave no trace. Incident response cannot reconstruct which account was targeted from where, and insider actions cannot be attributed.

**Root cause.** Audit logging is wired into domain services only, not into the authentication pipeline, and the request context (IP/UA) is not captured by the audit writer.

**Remediation.**
1. Emit audit events for `LoginSucceeded`, `LoginFailed` (generic reason code), `AccountLocked`, `RefreshSucceeded`, `RefreshReused`, `Logout`, `PasswordChanged`, `RoleChanged`, including `userId`/`email`, source IP, user agent, correlation id.
2. Populate `ipAddress`/`userAgent` centrally in the audit writer from `IHttpContextAccessor`, honouring `X-Forwarded-For` only from trusted proxies.
3. Ship audit events to a SIEM/log sink with alerting on thresholds (e.g. > 20 failures per account per hour, refresh reuse).
4. Define retention consistent with the applicable regulatory regime.

**Retest criteria.**
* Failed and successful logins appear in the audit store with IP and user agent.
* A refresh-token reuse produces a distinct alertable event.
* Alert thresholds fire in a staging test.

---

### I-01 — `POST /api/auth/switch-role` returns `200` while ignoring the requested role

| Attribute | Value |
|---|---|
| Severity | Informational |
| CWE | CWE-1059 (Insufficient Technical Documentation) |
| Affected | `DemoAuthService.SwitchRoleAsync` |
| Status | **Confirmed** (code + dynamic) |

`SwitchRoleAsync` ignores `roleName` and simply calls `GetCurrentUserAsync`; the method body is a single return statement. A `team.member` calling `{"roleName":"admin"}` receives `200` with their unchanged roles and permissions. There is **no** privilege escalation, but the `200` invites clients to believe a switch occurred, and the dead code path will confuse future maintainers.

**Recommendation.** Return `400`/`403` for a role the caller does not hold, or remove the endpoint. If role switching is a real feature, implement it server-side with re-issuance of a session and an audit entry.

---

### I-02 — Swagger UI is reachable in Development

| Attribute | Value |
|---|---|
| Severity | Informational |
| Affected | `/swagger`, `/swagger/v1/swagger.json` |
| Status | **Confirmed** (dynamic) |

Gated correctly by `if (app.Environment.IsDevelopment())` (`Program.cs:369-372`), so this is expected for Development. Confirmed reachable and used as the endpoint inventory for testing. No action required beyond ensuring the gate is never bypassed (e.g. never run a shared environment with `ASPNETCORE_ENVIRONMENT=Development`).

---

## 6. Verified security controls

These controls were exercised and **held**. They are recorded so that remediation effort is not spent re-solving solved problems, and so that regressions are caught.

### 6.1 Authentication and session management

| Control | Evidence |
|---|---|
| Uniform failure response for unknown account vs wrong password | Both `401 {"title":"Invalid email or password","status":401,"code":"UNAUTHORIZED"}` |
| Tampered JWT payload rejected | Modified claim → `401` |
| `alg=none` forgery rejected | Forged admin token → `401` |
| Issuer/audience/lifetime/signature validation | `TokenValidationParameters` all `true` (`Program.cs:116-129`) |
| Refresh-token rotation | Second use of a rotated token → `401` |
| Logout revokes server-side session | Access token → `401`; refresh token → `401` |
| Per-request session validation | `UserSessionValidator` checks `sid`, `userId`, `tenantId`, revocation and expiry |
| Permission claims rebuilt per request | `UserPermissionClaimsMiddleware` strips and reloads from the database — JWT role/permission tampering is inert |
| Forced initial password change enforced | `InitialPasswordEnforcementMiddleware`; temporary passwords expire in 24 h |
| Auth rate limiter effective and unspoofable | `429` at attempt 11; rotating `X-Forwarded-For`/`X-Real-IP` does not reset the window |
| Password hashing | ASP.NET Core Identity `PasswordHasher<User>` (PBKDF2) |

### 6.2 Authorisation and multi-tenancy

| Control | Evidence |
|---|---|
| Anonymous access to business endpoints | 227/227 GET endpoints → no `2xx` |
| Function-level RBAC | `team.member` → `403` on `/api/users`, `/api/audit`, `/api/integrations`, `/api/roles`, `/api/users/{id}/permissions` |
| Privilege escalation via self role grant | `403` |
| Privilege escalation via user creation | PM attempting to create an admin → `403` |
| Cross-project read (assigned project) | `GET /api/projects/{publicIdA}` → `200` |
| Cross-project read (foreign project) | `GET /api/projects/{publicIdB}` → `404`; `…/shell`, `…/team` → `404` |
| Cross-project object read | `GET /api/work-items/{idB}` → `404`; `…/comments` → `404` |
| Cross-project list filter | `GET /api/work-items?projectId=<B>` → `404` |
| Cross-project write | `POST /api/work-items {projectId:B}` → `404` (same payload into A → `201`) |
| Cross-project update | `PUT /api/work-items/{idB}` → `404` |
| Internal project GUID not routable | `GET /api/projects/{internalId}` → `404`; only `publicId` resolves |
| Ownership on personal records | `POST /api/time-entries {resourceId:<other>}` → `403`; own resource → `201` |
| Foreign project on personal record | `POST /api/time-entries {projectId:B}` → `404` |
| Tenant filter on login | Random tenant GUID → `401` |

### 6.3 Input handling and injection

| Control | Evidence |
|---|---|
| SQL injection | 918 probes across every string query parameter (`'`, `' OR 1=1--`, `1' AND '1'='1`, `' OR '1'='1`, `%;--`, `UNION SELECT NULL--`): zero `500`s, zero SQL error strings, zero timing anomalies |
| Raw SQL | No `FromSqlRaw` / `ExecuteSqlRaw` call sites in the codebase |
| LIKE wildcard abuse | `search=%` returns 0 rows (wildcards escaped/handled), so no filter bypass |
| Stored XSS | Payload `<img src=x onerror=alert(document.domain)>` is stored raw in `commentText` but rendered through `sanitizeRichText`; the executed unit suite (4/4 pass) removes `<img>`, `<script>`, `onclick` and escapes inert fragments |
| Single HTML sink | Only one `dangerouslySetInnerHTML` in the SPA, always wrapped by the sanitiser |
| Upload extension/content-type allowlist | `.html` upload → `400 "File type is not supported"` |
| Upload content-type spoofing | `.txt` declared as `text/html` → stored and served as `text/plain; charset=utf-8` |
| Upload filename traversal | `../../../evil.txt` → sanitised to `evil.txt` |
| Upload size limit | > 25 MB → `400` |
| Storage path containment | `LocalStorageProvider.ResolvePath` rejects rooted keys and paths escaping the root (Semgrep's `unsafe-path-combine` finding is a **false positive**) |
| Download response hardening | `Content-Disposition` set; inline only for allowlisted types; `X-Content-Type-Options: nosniff` |
| Mass assignment | `tenantId`, `id`, `createdBy`, `createdAt` injected on create → ignored; database shows server-derived `TenantId` and `CreatedBy` |

### 6.4 Business logic and financial controls

| Control | Evidence |
|---|---|
| Negative/zero settlement | `400` |
| Overpayment beyond outstanding balance | `400 "Settlement amount cannot exceed the outstanding balance"` |
| Missing idempotency key | `400` |
| Duplicate idempotency key | `409`, and no duplicate ledger row created |
| Settlement arithmetic | 100 obligation, 10 settled → outstanding 90, status `Partially Paid` |
| Workflow state injection | Creating a work item with `status: "Closed"` → `400 "A new Work Item starts at the configured workflow initial state."` |
| Assignment constraint | Assigning a non-member resource → `400 "Selected Resource is not an active member of this project."` |
| Self-approval of change requests | Blocked in code: `"A Change Request requester cannot approve their own request."` (`ProjectChangeRequestWorkflow.cs:311`) |
| Over-allocation | Project membership rejected when total allocation would exceed 100 % |

### 6.5 Configuration

| Control | Evidence |
|---|---|
| CORS allowlist | Preflight from `http://evil.example` → `204` with **no** `Access-Control-Allow-Origin`; `http://localhost:4100` reflected correctly |
| No CORS wildcard with credentials | No `AllowCredentials` + `*` combination found |
| JWT secret required outside Development | Guard exists (`Program.cs:103-113`) — but the committed baseline supplies a value, so the guard cannot fire (F-01) |
| Swagger Development-only | `Program.cs:369` |
| Database/Redis not externally exposed | Compose binds `127.0.0.1:1433` and `127.0.0.1:6379` |
| No secrets in `.env` beyond Firebase public web config | Firebase Admin credential intentionally empty; `NEXUS_JWT_SECRET` and `NEXUS_DB_PASSWORD` required by Compose |

---

## 7. Blocked and out-of-scope tests

| # | Test | Why blocked | Safe checks performed instead |
|---|---|---|---|
| B-01 | CodeQL data-flow / taint analysis | CLI not installed; no licence available in the environment | Semgrep `p/default` (84 rules) + manual review of every sink (`FromSqlRaw` absent, storage paths constrained, one HTML sink) |
| B-02 | True cross-tenant isolation (tenant A ↔ tenant B) | Only one tenant exists in the database; `UserRoles`/`Roles` are tenant-scoped and no second tenant data could be created without seeding | Verified tenant filter behaviour via arbitrary/absent tenant ids, `IgnoreQueryFilters` audit, and cross-project isolation (the closest reachable analogue) |
| B-03 | End-to-end browser XSS reproduction | The SPA dev server proxies to `localhost:5000` (the shared real Development database); injecting a stored payload there would mutate shared data | Payload stored in the isolated database and inspected via API; sanitiser verified by executing the existing 4-test suite; single `dangerouslySetInnerHTML` sink audited |
| B-04 | Active vulnerability scanning (ZAP active rules) | Active rules mutate data (forms, injections) and can cause load; confined to passive by rules of engagement | 817 messages through ZAP passive analysis, 227 endpoints × 3 identities |
| B-05 | Container/production configuration verification | Docker not installed; cannot build or run the Compose stack | Static review of `docker-compose.yml`, `docker-compose.override.yml`, `nginx.conf`, `.env` |
| B-06 | TLS configuration and certificate testing | No TLS listener in the tested environment (HTTP only) | Confirmed absence of TLS/HSTS as F-07; cipher review deferred to deployment |
| B-07 | M3 boundary: `Resource.View` without `ViewCostRate` | No such role exists among the 8 seeded roles, so the boundary is unreachable | Confirmed `project.manager` legitimately holds `Resource.ViewCostRate` and can read tenant-wide cost profiles; documented as a least-privilege observation rather than a defect |
| B-08 | SSO / OAuth / LDAP integration abuse | No directory integration connections configured (`[]`), no Firebase Admin credentials | Reviewed provider code paths and confirmed no reachable sink |
| B-09 | Email/notification delivery abuse | No SMTP/notification provider configured | Reviewed notification preferences endpoints; own-scope only |
| B-10 | Excel/CSV formula injection | No server-side export endpoint exists (no `export`/`xlsx`/`csv` routes in the API surface) | Confirmed absence of the feature rather than absence of a control |
| B-11 | Mobile client / API-key abuse | No mobile client in scope; API uses bearer JWTs only | — |
| B-12 | Denial-of-service / stress | Prohibited by rules of engagement | Bounded probes only: 80 rapid reads, one oversized upload (rejected), one oversized `pageSize` (accepted, documented as F-09) |

**Withdrawn blocker entries (v2.1).** Three rows previously appended here as `B-13`–`B-15` were withdrawn because the engagement's evidence contradicts their premises:

| Withdrawn claim | Why it is wrong |
|---|---|
| "Multi-role authorisation could not be tested" | It was tested: four identities (`team.member`, `project.manager`, `finance.controller`, `admin`) were driven across 227 endpoints, and role-specific `403`/`200` boundaries are recorded in §4 and §6 |
| "No test documents exist, so document IDOR was untestable" | Four documents were created in `NexusPmo_PenTest`; the resulting `404` is the **defect** F-05 (`TenantId = Guid.Empty`), not missing test data |
| "Financial mutation needed schema changes and was blocked by safety rules" | Financial tests ran against the isolated copy with no schema change: negative/zero settlement `400`, overpayment `400`, missing idempotency key `400`, duplicate key `409`, mass-assignment fields ignored |

### 7.1 Additional constraints and post-test verification (2026-10-28 → 2026-10-29)

This section reconciles the original engagement (2026-10-28, isolated `NexusPmo_PenTest`) with a later follow-up run against the shared Development database. Each claim is labelled by how it was established.

**Password change: the reported `403` was a wrong-endpoint artefact (follow-up run; not re-executed here).**
- Reported symptom: `403 PASSWORD_CHANGE_REQUIRED` when calling a user-management endpoint.
- Correct endpoint: `POST /api/auth/change-initial-password`.
- The follow-up observed a successful change returning `{"reauthenticationRequired":true}`; all sessions are revoked afterwards, which is intended. It was not re-executed in this engagement because it mutates credentials on the shared database.

**JWT signing key: ephemeral in Development — independently verified (2026-10-29).**
- `Program.cs:105-115` and `Nexus.SharedKernel/Security/JwtSigningKey.cs:43` generate a random 48-byte key via `CreateEphemeral()` when `Jwt:Secret` is blank and the environment is Development.
- The uncommitted working tree no longer contains `Jwt:Secret` (file last modified 2026-10-27 17:37, before the test window), so the tested instance ran on that ephemeral key.
- Verified rather than assumed: recomputing `HMAC-SHA256` over `header.payload` for all four captured tokens with the committed demo secret produces no match.
- Consequence: tokens do not survive a host restart in Development and re-authentication is required each time. This is by design and is **not** the subject of F-01, which concerns the value still committed at `HEAD`.

**Role assignment: code path identified, not executed (code review only).**
- `PUT /api/users/{id}` (`UsersController.Update`, lines 103-107) forwards to `UserAdministrationService.UpdateAsync`; the DTO carries a `roles` array.
- The follow-up reports that the service refuses to assign roles the actor does not hold. That check was not exercised in this engagement and is recorded as an observation, not a verified control.

**Actual remaining constraints (corrected).**
- **Cross-tenant isolation** is untestable because only one tenant exists. The tenant query filters are implemented and were verified to reject arbitrary and absent tenant identifiers (§6); the correct statement is "single-tenant data only", not "multi-tenancy not implemented".
- **Cross-tenant document access** is untestable for the same reason. The document defect F-05 (`TenantId = Guid.Empty`) is a separate, confirmed finding and is not evidence of missing test data.
- **Role-assignment authorisation** remains a code-review observation pending a dynamic test with fresh tokens.
- **Deployment-layer controls** (TLS, security headers, Compose topology) remain unverifiable: Docker is not installed and no TLS listener exists.

---

## 8. Prioritised remediation plan

### 8.1 P0 — before any shared or demo environment (≤ 3 days)

| # | Action | Finding | Owner |
|---|---|---|---|
| 1 | Remove `Jwt:Secret` from `appsettings.json`; rotate the key everywhere; add a demo-key denylist; add a CI secret scanner | F-01 | Backend / DevOps |
| 2 | Fix `DocumentService.CreateAsync` tenant assignment; add the `SaveChanges` guard; backfill/quarantine `TenantId = Guid.Empty` rows | F-05 | Backend |
| 3 | Constrain `/api/users/lookup` to a permission and a minimal projection | F-04 | Backend |
| 4 | Run shared environments with `ASPNETCORE_ENVIRONMENT=Production` or add a global exception handler + acyclic DTO for `/api/organizations` | F-06 | Backend |

### 8.2 P1 — next sprint (≤ 2 weeks)

| # | Action | Finding |
|---|---|---|
| 5 | Dummy-hash verification on the negative login path to remove the timing oracle | F-02 |
| 6 | Per-account failure counter, progressive delay, lockout with notification | F-03 |
| 7 | Security-header middleware / nginx `add_header`; CSP in Report-Only then enforce; TLS + HSTS at the edge; stop publishing API port | F-07 |
| 8 | Move refresh token to `HttpOnly; Secure; SameSite=Strict` cookie; access token in memory; shorten lifetimes; `Cache-Control: no-store` on auth responses | F-08 |
| 9 | Require `tenantId` at login (or explicit single-tenant mode) | F-10 |
| 10 | Authentication audit events with IP/UA and alert thresholds | F-14 |

### 8.3 P2 — hardening backlog (≤ 6 weeks)

| # | Action | Finding |
|---|---|---|
| 11 | Centralised pagination clamp + global rate-limit policy (`429` with `Retry-After`) | F-09 |
| 12 | Randomised seed credentials with forced rotation; remove credentials from documentation | F-11 |
| 13 | Breached-password screening, password history, context-aware blocklist | F-12 |
| 14 | Upgrade `react-router-dom` ≥ 7.18.5, `zod` ≥ 4.6.7, address `exceljs`/`uuid`; schedule Vite/Vitest 5; add dependency gate | F-13 |
| 15 | Remove or implement `switch-role` properly | I-01 |
| 16 | Add regression tests: tenant-scoped entity creation, header assertions, cross-project BOLA suite, auth timing budget | all |

### 8.4 Suggested verification gates

* **Gate A (code):** SAST (Semgrep + CodeQL when available), secret scan, dependency scan on every PR.
* **Gate B (integration):** automated BOLA suite (assigned vs non-assigned project for read/list/write), tenant-id assertion on every tenant-scoped entity, header assertions per response class.
* **Gate C (release):** DAST passive + authenticated crawl in staging, ZAP baseline with zero high/medium alerts.

---

## 9. Appendices

### Appendix A — Evidence artefacts

Stored outside the repository (working directory of the test host):

| Artefact | Content |
|---|---|
| `semgrep-api.json` | Semgrep SARIF/JSON output (1 finding, false positive) |
| `sweep-anon.json` / `sweep-team.json` | 227-endpoint anonymous and team-member sweeps (status, size, excerpt) |
| `results-sqli.json` | 918 injection probes, anomaly list (empty) |
| `results-zap-alerts.json` | 79 ZAP passive alerts (all rule 10021) |
| `npm-audit.json` | Full npm advisory set |
| `dotnet-vuln.txt` | Per-project .NET vulnerability result |
| `tokens.json`, `*.json` request bodies | Test fixtures and request payloads (tokens expired/rotated) |
| Logs | API stdout/stderr, ZAP daemon logs |

### Appendix B — Reproduction quick reference

```powershell
# login
curl -s -X POST http://127.0.0.1:5001/api/auth/login -H 'Content-Type: application/json' `
  -d '{"email":"pentest.team@example.test","password":"<redacted>"}'

# cross-project read (assigned project → 200, foreign project → 404)
curl -s -o NUL -w "%{http_code}" http://127.0.0.1:5001/api/projects/cde8483e-5be5-49d3-9a23-7dbc3ab883f0 -H "Authorization: Bearer <team>"
curl -s -o NUL -w "%{http_code}" http://127.0.0.1:5001/api/projects/f31f1d5b-7038-45a5-9fc7-816078b70a99 -H "Authorization: Bearer <team>"

# directory over-disclosure
curl -s http://127.0.0.1:5001/api/users/lookup -H "Authorization: Bearer <team>"

# document round-trip failure
curl -s -X POST http://127.0.0.1:5001/api/documents -H "Authorization: Bearer <admin>" -F "file=@note.txt"
curl -s -o NUL -w "%{http_code}" http://127.0.0.1:5001/api/documents/<id> -H "Authorization: Bearer <admin>"

# login timing
Measure-Command { curl -s -o NUL -X POST http://127.0.0.1:5001/api/auth/login `
  -H 'Content-Type: application/json' -d '{"email":"known@example.test","password":"wrong"}' }
```

### Appendix C — Test-data inventory and cleanup

Created in `NexusPmo_PenTest` only:

| Object | Identifier |
|---|---|
| Users | `pentest.team@example.test` (`45a59526-…`), `pentest.pm@example.test` (`52dc69c8-…`), `pentest.fin@example.test` (`a54b38e0-…`) |
| Resource (fixture) | `0bcdf50c-76b2-4c1e-9f3c-80518df9b05e` |
| Project membership (fixture) | `AA000000-0000-0000-0000-00000000A001` (project `e0000001-…`, resource above) |
| Work items | `1c782183-…` (`pentest-scope-e0000001`), `9915e3df-…` (`pentest-ma`), `dc2c099a-…` (`pentest-BOLA-B`), `b91945b9-…` (XSS probe) |
| Comments | `d1388c7c-…` (XSS payload) |
| Documents | `ded6c56e-…`, `eccb74b9-…`, `f03ee4f7-…`, `ad626653-…` |
| Payment obligation | `adbf9739-…` (partially settled, 10/100) |
| Time entries | one on `2026-10-20` for the fixture resource |

```sql
-- Revert fixtures (isolated database only)
DELETE FROM ProjectMembers WHERE Id = 'AA000000-0000-0000-0000-00000000A001';
DELETE FROM TimeEntries WHERE ResourceId = '0BCDF50C-76B2-4C1E-9F3C-80518DF9B05E';
DELETE FROM WorkItems  WHERE Title LIKE 'pentest-%' OR Title LIKE 'PENTEST-%';
DELETE FROM Documents  WHERE Id IN ('DED6C56E-D8E9-4AF9-BC84-552C84CDC3B3','ECCB74B9-0879-4AED-A713-46E34CD9FAB6','F03EE4F7-E893-420F-BD46-CFE9F663E56F','AD626653-A44A-47E8-91A6-4E1ACF37F7DD');
DELETE FROM Resources  WHERE Id = '0BCDF50C-76B2-4C1E-9F3C-80518DF9B05E';
DELETE FROM Users      WHERE Email LIKE 'pentest.%@example.test';
-- then drop the copy once evidence is archived
-- DROP DATABASE NexusPmo_PenTest;
```

The production-like `NexusPmo` database was **not** modified: schema untouched, pending migration `20261028084103_AddMeetingActionWorkItemLink` still pending, no fixture data inserted.

### Appendix D — Finding-to-framework index

| Finding | Severity | OWASP Top 10 | API Top 10 | WSTG | CWE |
|---|---|---|---|---|---|
| F-01 | High (latent) | A02, A05 | API8 | CRYP, CONF | 321, 798 |
| F-02 | Medium | A07 | API2 | IDNT, ATHN | 208, 204 |
| F-03 | Medium | A07 | API2 | ATHN | 307 |
| F-04 | Medium | A01 | API3 | ATHZ, INFO | 200, 213 |
| F-05 | Medium | A01, A04 | API1 | BUSL, INPV | 284 |
| F-06 | Medium | A05, A09 | API8 | ERRH | 209, 755 |
| F-07 | Medium | A05, A02 | API8 | CONF, CRYP, CLNT | 693, 319, 1021 |
| F-08 | Medium | A07, A03 | API2 | SESS, CLNT | 522, 539, 79 |
| F-09 | Low | — | API4 | INPV | 770 |
| F-10 | Low | A05, A07 | API8 | IDNT | 1188, 284 |
| F-11 | Low | A07 | API8 | IDNT, ATHN | 798, 521 |
| F-12 | Low | A07 | — | ATHN | 521 |
| F-13 | Low | A06 | — | CLNT | 1104, 1395 |
| F-14 | Low | A09 | — | — | 778, 223 |
| I-01 | Info | — | API5 | — | 1059 |
| I-02 | Info | A05 | API9 | INFO | — |

### Appendix E — Document control

| Field | Value |
|---|---|
| Author | Security assessment (AI-assisted, human-reviewed) |
| Test lead sign-off | Pending |
| Revision | v2.1 (2026-10-29) — F-01 runtime claim corrected after offline HMAC verification; §7 malformed rows withdrawn and replaced with a reconciliation note; §7.1 corrected |
| Distribution | Engineering, DevOps, Product Security |
| Next review | After P0/P1 remediation, or 90 days, whichever is first |
| Retest requirement | Full dynamic retest of F-01…F-08 plus automated regression for the verified controls in §6 |

*This report contains no production credentials. The single secret quoted verbatim (F-01) is already present in the repository and must be rotated regardless of test outcome.*
