# Protected Tenant Admin Verification

**Snapshot:** 2026-11-02  
**Status:** Source implementation present; persistence and runtime verification pending.

## Identity and baseline

The protected Tenant Admin role is identified by immutable `Role.SystemRoleKey = "tenant.admin"`, assigned once by trusted seed/provisioning code to an `IsSystem` role in the matching Tenant. Authorization does not identify this role by its editable display name or `CreatedBy` value. The additive migration `20261102190318_AddStableSystemRoleIdentity` performs a bounded one-time conversion for active tenant-scoped system roles whose name is a binary-exact `admin`; it fails if a Tenant has multiple such roles. That backfill changes only the identity marker and does not copy or modify grants.

`ProtectedTenantAdminPolicy` dynamically derives the baseline from every active Tenant-scoped catalog entry in the `administration` module or marked `IsTenantAdminBaseline`, plus the separately approved Tenant-scoped Portfolio, Program, and Handover permissions. It excludes Platform, CrossTenant, SystemOwner, and Organization-scoped capabilities. The current source baseline is **48 keys** by architecture test; persisted role links have not been queried.

## Source protections

- The permission catalog and seed create active canonical permission rows only. Protected role seed definitions use stable system-role keys; bootstrap membership resolves the actual role ID by key.
- New Tenant provisioning initializes permissions from that Tenant's active catalog and grants only the dynamic protected baseline. It no longer clones role grants from another Tenant.
- Repeated seeding restores missing mandatory Admin grants without rewriting unrelated roles or memberships. `pmo.admin` receives no automatic Admin baseline or Platform privileges.
- Backend role permission replacement rejects attempts to remove mandatory grants. Direct Deny assignment for a protected key is rejected; effective resolution preserves the mandatory baseline even if malformed historical Deny data exists.
- Last-administrator changes are validated with tenant-scoped transactional protection.
- Roles & Permissions marks mandatory Admin grants as enabled and locked using server-projected role metadata.

## Verification status

- Static source baseline: 48 mandatory keys; catalog: 154 active keys (39 View, 115 Action).
- React full-suite tests pass (765/765 across 141 files). Backend catalog, Admin, provisioning, and seed tests remain NOT EXECUTED because VSTest could not connect to the API testhost.
- SQL target identity, applied migrations, persisted membership/grants, Deny rows, and physical schema are **not verified**. The read-only sqlcmd probe failed with a socket access-permission error before SQL authentication; no backup, migration application, database reset, or seed ran.
- Browser lock behavior, direct API removal/Deny rejection, dynamic future-key inclusion, and last-admin runtime behavior remain **NOT EXECUTED**.

## Source and Roles & Permissions addendum (2026-11-05)

The current source catalog contains 166 active keys (44 View, 122 Action). `ProtectedTenantAdminPolicy` derives a 47-key baseline. The exact baseline set is unchanged before and after the Roles & Permissions metadata corrections. The six active `ApprovalRequest.*` capabilities are in service use and remain outside this baseline. These source counts supersede the earlier report's 154-key / 48-key figures; they do not establish persisted grant state.

The role editor consumes `isMandatoryForSystemAdmin` from the API and keeps those baseline entries checked and locked. A focused backend catalog contract test and React regressions for protected locks, search, filters, dependencies, scopes, and RTL have been added but not executed in this update. Database grants, direct Deny rows, last-admin runtime behavior, and SQL identity remain unverified; this task made no database changes.


## Final focused verification (2026-11-05)

- Roles & Permissions React regressions: **PASS — 27/27** (`src/test/access-security-pages.test.tsx`), including module/screen expansion, search, filtering, dependency display and selection, locked protected Admin entries, bilingual direction, and duplicate-label distinctions.
- React production build: **PASS** — TypeScript check and Vite production build completed; 2,890 modules transformed.
- Identity authorization module build: **PASS** — `NEXUS.Modules.Identity.csproj`, 0 warnings and 0 errors.
- Focused API permission-catalog tests: **BLOCKED before test execution** by compilation errors in existing Approval and WorkflowEngine sources (`ApprovalService` cannot resolve `NEXUS.Modules.Audit` / `IAuditService`; `AuditProjectionActionHandler` has a `Guid`/null conditional error and references missing `WorkflowExecutionContext.EntityType`). No API test results were produced.
- Source review continues to find 166 active canonical keys (44 View, 122 Action) and an unchanged 47-key protected Admin baseline. The action matrix still lacks mappings for six `ApprovalRequest.*` keys and `ProjectSettings.View`; this documentation follow-up is deferred.
- No database, migration, SQL permission row, or persisted role grant was changed or inspected. No browser UAT was run.
