# P0-07 — Security Code Closure and Regression Prevention

**Date:** 2026-11-06  
**Repository:** C:\Projects\pmo  
**Scope:** Source-level security remediation and regression prevention carried forward from P0-01–P0-06C.  
**Status:** **PARTIAL — implementation work is recorded; backend and static-analysis verification remain incomplete.**

The Product Owner directed that work stop once implementation was complete and only verification remained. I stopped the ongoing API test-project build during IKVM generation and am recording the verification boundary here. No database, seed, migration, browser UAT, or Production operation was performed.

A large pre-existing uncommitted worktree was preserved. This report covers the P0-07 changes and reviewed controls only; it does not attribute the rest of the worktree to this task.

## 1. Security findings and disposition

| Finding area | Current disposition | Evidence and limit |
|---|---|---|
| Browser token persistence and account isolation | Persistent JWT storage is removed from the React client. Access JWTs remain in memory; localStorage contains only a non-secret session coordination ID. Legacy token keys are cleared. The refresh credential is held in a Secure, HttpOnly, SameSite=Strict cookie. | Current frontend contract/session tests passed. A JavaScript-readable in-memory access token remains exposed to same-origin script execution if an XSS is introduced. |
| Refresh and permission propagation | Fixed refresh behavior that treated any in-memory access token as proof another tab had already refreshed it. On a 403, permission-gated UI is hidden while the client reloads authoritative permissions. | Both regressions are covered by the focused session-isolation suite; 9/9 tests passed in the final run. |
| Rich-text XSS | Existing rich-text output is sanitized; the new Semgrep rule rejects unreviewed raw HTML sinks. | Rich-text tests passed 6/6. Semgrep itself was not executed. |
| Tenant and project scope | Earlier P0-07 work recorded in the existing draft adds a SaveChanges tenant write guard, fail-closed plan scope checks and trusted background tenant execution scopes. These changes remain in the worktree. | The API tests and the related static rules were not successfully executed in this pass. Treat these as implemented in source, with current verification outstanding. |
| Integration secret storage and disclosure | Source contains a versioned Data Protection protector, EF save interceptor for IntegrationConnection and IntegrationWebhook secret fields, fail-closed handling for unmarked legacy plaintext/corrupt ciphertext, and DTO projections that expose secret presence rather than values. | Source reviewed. Backend tests were not run; existing stored data and deployment configuration were not inspected. Do not mark the secret-storage finding fully verified yet. |
| Data Protection key-ring readiness | Production configuration requires a stable application name, absolute provisioned key-ring path and certificate; startup checks key-ring writability and a protection round trip. Three focused key-ring regression cases were added. | Test project did not complete its build, so these tests remain unexecuted. Shared key-ring topology and certificate availability across deployed instances remain unverified. |
| Protected Admin and Active Deny | No change to protected Admin or Active Deny semantics was made in P0-07. | Previous P0-06C artifacts remain in place, but they do not establish a fresh pass for this run. API regression tests were not executed. |

**Finding closure rule:** Code-present findings whose focused backend tests or required static checks did not run are not reported as fully closed.

## 2. Remaining vulnerabilities and blockers

- **Development HTTPS/CSRF:** The browser flow requires Secure antiforgery/refresh cookies. The current HTTP Development endpoint cannot satisfy that contract. Cookie security was not weakened; the separate HTTPS Development task remains required.
- **Access-token runtime exposure:** The access JWT is not persisted, but remains reachable from application JavaScript while the page is open. A compromised same-origin script could use it.
- **CSP:** React’s current policy includes style-src 'unsafe-inline'. It remains a hardening opportunity; no incompatible CSP change was made.
- **Legacy integration secrets:** Unmarked legacy values are deliberately rejected by the protector. P0-07C must determine whether any such values exist and, if so, re-encrypt them through an approved, target-verified data procedure before secret-consuming integration features rely on them.
- **Multi-instance key ring:** Source validates configuration and local readiness, but does not prove that deployment instances see the same durable key ring or have the active/prior certificates needed for rotation.
- **Static coverage limits:** Semgrep rules cover direct patterns for browser token storage, selected tenant fallbacks, raw HTML sinks, integration secret writes/logging/DTOs, Minimal API authorization and role-name checks. They do not prove every controller/service operation has the correct permission, resource scope, Active Deny behavior or protected Admin semantics. The logger rule can miss aliased secret values.
- **API verification:** The API assembly compiled in an isolated output directory, but the API test project did not produce a successful final build and no backend suite ran.
- **Scanner/dependency evidence:** Semgrep and CodeQL were not run locally. NuGet vulnerability metadata could not be retrieved because nuget.org was unreachable from this environment.

## 3. Authentication and Secrets disposition

### Authentication

The browser uses CSRF-protected cookie login, refresh and logout calls. Refresh credentials are HttpOnly; access credentials are kept in memory and are not restored from localStorage or sessionStorage. Non-browser compatibility remains in the API contract, and the browser explicitly requests cookie mode.

This continuation fixed the refresh retry condition and made permission-gated UI fail closed until a 403-triggered permission reload completes. The focused tests passed after those fixes. No live cookie, HTTPS, CSRF or browser behavior was exercised.

### Integration secrets

The persistence interceptor protects secret fields on added and modified integration rows for both synchronous and asynchronous SaveChanges. The protector rejects plaintext and unreadable ciphertext rather than returning it as usable secret material. Integration service projections reviewed in this pass return configured/presence state instead of secret values.

The secret and key-ring implementation is not fully verified until the API regression tests run and P0-07C establishes the state of any pre-existing secret rows. No credentials or encryption keys were added to source or seed data.

## 4. Regression tests

The final React security test invocation passed:

- auth/session isolation: 9 passed
- P0-05 security hardening: 10 passed
- rich-text editor: 6 passed
- P0-05 DevOps guardrails: 2 passed
- **Total: 4 files, 27 tests passed**

The added Data Protection key-ring tests cover shared protection across independent providers, startup failure when the configured key ring is unavailable, and successful startup round trip. They were **not run**.

Existing integration secret tests cover protection on persistence, legacy/corrupt value handling and projection redaction. Existing P0-01–P0-06C regression source and TRX artifacts were preserved. The latest P0-06C report records known limitations, including a Plugin SQLite schema failure, one Permission Catalog architecture failure, and a VSTest loopback restriction on some API runs; those historical artifacts are not represented as a fresh all-green result here.

## 5. Static analysis and CI guardrails

The custom Semgrep rules live in .semgrep/pmo-security.yml. The P0-07 addition flags direct IntegrationConnection/IntegrationWebhook secret fields passed to ILogger methods. Existing rules cover the selected browser token, raw HTML, tenant fallback, secret persistence/API contract, Minimal API authorization and role-name patterns.

The existing PR validation workflow configures blocking Semgrep, npm audits, NuGet vulnerability auditing, React typecheck/lint/tests, API build and focused security regressions. Dependabot covers npm, NuGet and GitHub Actions. CodeQL is configured for C# and JavaScript/TypeScript with security-extended queries, but uploads are disabled; SARIF is retained as a workflow artifact. CodeQL jobs are gated for private repositories on CODEQL_LICENSED.

These workflows were reviewed but not run. The local NuGet audit emitted NU1900 because the vulnerability feed was unreachable. CI results are required before calling the static/dependency gates verified.

## 6. Final build and test results

| Gate | Result |
|---|---|
| React production build | **Passed.** TypeScript and Vite completed; 2,895 modules transformed. Existing warnings remain for a statically and dynamically imported finance page and bundles above 500 KB. |
| Focused React security tests | **Passed, 27/27.** See p07-artifacts/web-security-vitest.log. |
| API source compilation | **NEXUS.Api compiled** during the isolated API test-project build. |
| API test-project build | **Incomplete.** The isolated build failed at Microsoft.CodeCoverage with MSB3491: access denied writing the source-roots mapping file under the sandbox Temp path. A prior default-output build was blocked copying assemblies locked by the running API process. |
| API regression tests | **Not run.** The follow-up test-project build was stopped during IKVM generation per the Product Owner’s instruction. No P0-07 API TRX is claimed. |
| Semgrep / CodeQL | **Not run.** CI execution is outstanding. |
| Database, migration, seed, browser UAT | **Not run**, as required by task scope. |

Build logs are in p07-artifacts: web-build.log, api-build.log and api-test-build.log. The API logs contain the output-path failure and the stopped IKVM attempt; they are not pass evidence.

## 7. Seed and EF changes for P0-07C

- **Seed:** No seed change was identified in this source-only pass. Keep real integration credentials, encryption keys and bootstrap passwords out of seed data. P0-07C must review the actual Minimal Seed contract and preserve idempotency; no seed was run.
- **Existing integration secret values:** Do not assume the recently reset database contains legacy rows or that it is clean. P0-07C must inspect the verified target and decide whether a reviewed re-encryption data operation is required. No database was queried or written here.
- **EF schema:** No P0-07 migration was authored or applied. P0-07C must run the documented offline model/snapshot gate and inspect physical schema before deciding whether a migration is needed, including the secret-column capacity for protected values. A seed cannot replace a schema migration.
- **Database evidence:** Model synchronization, pending migration count/names, Development database identity, backup, migration application, seed idempotency, physical schema and persistence tests are all **Not assessed / Not run**. No Production changes occurred.

## 8. Skills and subagent contributions

**Guidance used:** Repository security-audit, Semgrep, CodeQL and frontend-data-integrity skills were read and applied where relevant. The EF Core database workflow documentation and prior P0-01–P0-06C reports were reviewed. No installed .NET, testing or CI-specific skill package was available in the selected environment; repository workflow instructions and existing CI configuration were used. No local security scanner was executed.

**Subagents and integration:**

- **Main:** preserved the existing uncommitted tree, integrated the work, fixed the refresh retry and permission-refresh UI regressions, ran the final React build/tests, and documented the API verification boundary.
- **A — Authentication & Frontend Security:** reviewed the existing cookie/session and XSS controls. No additional source edits were needed in this continuation.
- **B — Secrets & Backend Security:** reviewed existing secret protection, DTO redaction and key-ring controls. No additional source edits were needed in this continuation.
- **C — Security Architecture & Prevention:** added the direct integration-secret logging Semgrep rule. The scanner was not run.
- **D — Security Regression Coverage:** added three key-ring regression cases. They were not executed because the API test-project build did not complete.

## Overall status

**Security code hardened in several reviewed paths; P0-07 is not fully closed.** The React build and 27 focused tests pass. Backend regression tests, Semgrep/CodeQL execution, deployment key-ring verification, Development HTTPS/CSRF verification, and P0-07C seed/EF/data inspection remain outstanding. Do not claim those findings as closed until their required evidence exists.

