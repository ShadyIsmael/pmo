# RBAC Independent Implementation Review

> **Build-only phase reconciliation (2026-11-15):** The Independent Critical Reviewer confirms no unresolved critical source regression and reviewed the supplied successful build summary. This is source/build handover clearance only. No tests, API, browser, or database actions were run by the reviewer. Migration/data preservation, role inventory, Project Manager onboarding, and Organization Admin runtime behavior remain unverified. See [RBAC build-only handover](RBAC-BUILD-ONLY-HANDOVER-2026-11-15.md).

The reviewer reconciled the integrated `NEXUS.Api.Tests.csproj` compilation with project references enabled: SDK 11.0.100, exit 0, 389 warnings, 0 errors, 5:27.78. SharedKernel, referenced modules including MppAdapter, Persistence, API and test sources compiled in the graph. No test discovery or execution was performed. The reviewer did not rerun builds; this reconciliation is not independent runtime, database, or test evidence.

> **Status reconciliation - 2026-11-15:** This remains an independent read-only source review. The React production build passed; targeted React runs passed 2 files/12 tests, followed by 1 file/6 tests after the row-level OA guard (overlap). .NET tests did not execute: API tests stopped during IKVM generation and seed tests failed at testhost connection after test-project compilation. Development DB/API/browser execution remains unavailable or not run. The coordinator's `RBAC-IMPLEMENTATION-FINAL-REPORT.md` controls current readiness and remains **BLOCKED**.

**Reviewer:** Independent Critical Reviewer  
**Date:** 2026-11-15  
**Method:** Independent read-only source review; no database, runtime, or browser actions.

## Findings

### Blocking implementation evidence

1. **System-role database CHECK NULL semantics - source finding corrected, execution pending.** Initial review found that `IsSystem=1` with null `SystemRoleKey` or `RoleKey` could produce SQL CHECK `UNKNOWN`, which SQL Server accepts. Coordinator added explicit `IS NOT NULL` guards in `RoleConfiguration`, migration, migration designer, and model snapshot, plus SQLite regression cases for null/missing/unapproved identities. Persistence source compiles in isolated output, but regression tests did not execute and target SQL was not generated/reviewed. No Development migration has run.
2. **P0 user creation 403 — no root cause confirmed.** Source now separates delegation authority from ordinary operational permission possession and a DEL03A test fixture covers unheld Project.Edit. The controller-level POST, deployed API, persisted membership, and historical rejection trace were not exercised. Do not report the 403 resolved.
3. **Development role/membership/grant inventory — unknown.** Preflight stopped before identity/history due executor SQL access boundary. The migration cannot be applied or preserved-data counts asserted from this session.
4. **Existing Organization Admin grant reconciliation - fail-closed source correction, unverified data.** The ordinary seed path now rejects active permissions on an existing canonical Organization Admin outside its approved Organization Configuration grants before changing role identity; it does not auto-revoke. Relational seed paths use a transaction. Persistence and seed test sources compile, but the testhost did not run cases and actual Development grants are unknown.

### Non-blocking or policy-dependent gaps

- **BusinessUnit scope:** Organization-owned data is currently protected by Tenant Settings policy; by-ID service lookup does not validate an Organization grant. Keep Tenant-only. Product Owner must decide if Business Units are part of OA-01/02 and approve a scoped access model before exposing them.
- **Organization lifecycle state vs. configuration:** Scoped Project Lifecycle catalog configuration is wired. The active Product Owner task resolves OA-03 as Organization-level configuration authority only. Organization state transitions remain separate and are excluded from this role authority.
- **Organization Admin lifecycle governance:** Appointment, removal, protection, recovery, and last-Organization-Admin behavior remain unresolved; assignment fails closed and no membership is seeded.
- **Users route/list parity:** React Users administration can route on `Users.ManageAccounts`, while paginated/list APIs require `Users.View`. Tenant Admin baseline contains both, but least-privilege management-only users can enter a page that cannot load its list. Clarify intended separation between create/manage and list/view.
- **Organization profile cache:** `useOrganizationProfiles` query key is Tenant-based and the payload is user-grant-specific. Review of `AuthProvider` shows session changes call `resetSessionCache` (cancel + clear + new QueryClient) on login/logout/session transitions. This resolves the suspected cross-user cached-profile display under current session lifecycle; no extra key change was made. Auth/session behavior remains untested in this task.
- **PM role vs Project assignment:** Role membership does not assign a user as Project Manager on a Project. The latter remains a separate initial assignment/Handover transition and must be tested independently.
- **User-managed optional role grants:** migration source preserves Role IDs and links by design; only a before/after Development inventory can prove actual counts and no seed grant drift.

## Independent position

The source design is directionally aligned with the approved boundaries: two System Role identities; stable role identity separated from classification; delegation authority separate from operational access; Organization Admin scope requires canonical identity, active same-Tenant membership, fresh effective permission, and explicit active Organization grant; `GetOptions` and other operational actions retain their separate permissions.

The post-recovery independent review confirmed the canonical React permission key, row-level Tenant-inherited lifecycle UI guard, and fail-closed OA seed guard; it found no new critical source regression. It also confirmed that React build/UI evidence is not a substitute for API/database proof. This is not a release approval. Completion remains blocked on executable focused .NET tests, migration/model review against the verified Development target, actual migration and seed verification, authenticated Development API tests for both P0 journeys, and persisted preservation counts. Product Owner browser UAT remains separate.


## Independent final recovery review — 2026-11-15

The independent reviewer confirmed the migration SQL hash and its transactional, fail-closed structure. The migration does not delete/recreate roles or mutate memberships, permission grants, direct overrides or audit records. Source review found that the protected Tenant Admin delegation branch validates catalog, scope, activity, assignability, restrictions and dependencies without requiring the Admin to possess the delegated operational permission. Organization Admin authorization requires canonical identity, active same-tenant membership, explicit Organization scope and refreshed effective permissions. The seed guard stops on unapproved Organization Admin grants instead of silently revoking them.

No new critical source-level regression was identified. This review is not runtime or persistence-test evidence: live role inventory and preservation counts are absent; no .NET test executed; no serving API assembly was verified; neither the Project Manager onboarding nor Organization Admin API journey was run. A valid Organization Admin membership/scope fixture cannot be synthesized while appointment policy remains unresolved; no direct database patch is authorized.
