# Final Permissions Closure Report

**Classification:** IMPLEMENTED — VERIFICATION PENDING  
**Snapshot:** 2026-11-02  
**Development database:** Not verified, not rebuilt, and not seeded.

## Catalog recount addendum (2026-11-05)

The current source recount is 166 unique active keys (44 View, 122 Action); six active `ApprovalRequest.*` permissions in active service use were omitted from an earlier 160-key tally. They were already in source and are outside the protected Admin baseline. The Roles & Permissions metadata changes did not add/remove keys or IDs and preserved the exact dynamic 47-key Admin baseline. This supersedes the 154-key / 48-key source figures later in this 2026-11-02 snapshot; persisted grant and runtime evidence remain unverified.

The action matrix has 174 rows marked `Catalog Active=true`, representing 159 distinct active Action IDs. Seven current source keys are absent from its mapping: the six `ApprovalRequest.*` keys and `ProjectSettings.View`. This is a deferred documentation mapping follow-up; the matrix was not rewritten. No database operation or grant reconciliation was performed for this task.

## A. Multi-agent execution

The four required workstreams were run in the prior implementation wave. No subagent performed database operations.

| Agent | Workstream and result | Verification |
|---|---|---|
| A — Catalog and coverage | Reconciled the active canonical catalog and continued the existing matrix and endpoint/UI evidence. | 154 active permissions; no duplicate canonical keys or seed IDs found by source comparison. The matrix has 203 evidence rows and 161 distinct in-scope business actions/exceptions. Runtime results remain pending. |
| B — Backend security | Implemented centralized permission and scope enforcement, explicit Deny precedence, protected Admin checks, and regression coverage. | API solution build passes. The permission test assembly built, but VSTest could not connect to its testhost; no backend test executed. |
| C — Frontend permissions | Updated React authorization, permission refresh and access screens, bilingual labels, and relevant regressions. | 141 frontend test files passed: 765/765 tests. TypeScript and production build pass. |
| D — EF migrations and seed | Consolidated the source migration path, stable Admin identity, and deterministic seed/baseline implementation. | Offline EF model gate passes with 10 source migrations. No live database inspection, migration, seed, or persistence test ran. |

Primary repository artifacts include the authoritative catalog at apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs; the matrix and endpoint inventory under docs/security/permission-audit/; the resolver and Admin policy under apps/api/src/NEXUS.Modules.Identity/; SeedDataProvider.cs and the EF model snapshot under apps/api/src/NEXUS.Persistence/; and React authorization in apps/web-react/src/auth/AuthProvider.tsx, apps/web-react/src/lib/permissions.ts, apps/web-react/src/routes/AppRoutes.tsx, and the Roles & Permissions pages. This retry also changed OperationsPagesFull.tsx and focused React tests for portfolio finance, meetings, date formatting, and visible-first project loading.

## B. Permission architecture

| Measure | Result |
|---|---|
| Active canonical permissions | 154: 39 View and 115 Action |
| Duplicate permission keys / seed IDs | 0 / 0 by source comparison |
| Protected Admin source baseline | 48 dynamic, tenant-scoped keys |
| Action matrix | 203 rows: 154 catalog capabilities, 7 authorization exceptions, 9 mapped source surfaces, 32 retired/history rows, 1 documentation row |
| Distinct in-scope action denominator | 161, comprising 154 canonical capabilities and 7 documented exceptions |
| API endpoint inventory | 602 routes; source inventory records nearby authorization evidence |
| Runtime action outcomes | Not verified |

The matrix records Allow and Deny evidence as NOT RUN. It does not claim complete runtime coverage.

A source search found no active API or React references to the named legacy or broad aliases projects.read, finances.write, Roles.Manage, ReferenceData.Manage, ProjectFinance.Manage, or Resource.ManageRates. Resource.Allocate remains only in negative tests. Static source evidence does not prove that persisted rows or direct HTTP requests reject those values.

The source resolver gives explicit Deny priority over ordinary Allow. Backend tests for role/direct grants, active-session changes, tenant/resource boundaries, and 403/no-mutation behavior did not execute. Direct API and existing-session behavior remain unverified.

Portfolio report cards now read financial totals from the financial-rollup API and request them only when ProjectFinance.View is present. The endpoint requires both Portfolio.View and ProjectFinance.View; React tests verify the UI does not request or display financial totals without ProjectFinance.View.

## C. Protected Admin

The role is identified by the immutable system key tenant.admin. Its source baseline dynamically includes active tenant-scoped Administration permissions and the separately approved Portfolio, Program, and Handover capabilities. The source baseline is 48 keys. Platform, CrossTenant, SystemOwner, and unapproved Organization permissions are excluded by the source policy.

Source protections cover mandatory-grant restoration, removal and Deny rejection, tenant bootstrap membership, and last-admin mutations. Persisted grants, direct API rejection, dynamic future-key inclusion, locked UI behavior against a live role, and last-admin runtime behavior remain unverified.

## D. Database rebuild and seed

| Required database gate | Result |
|---|---|
| EF model synchronized | Yes — offline gate passed for NEXUS.Persistence.AppDbContext |
| Repository migration count | 10 |
| Live pending migration count and names | Not run — migration history was not readable |
| Development SQL identity verified | No |
| Independent backup verified | No |
| Migrations applied | No |
| Physical schema verified | Not run |
| Data Seed executed | No |
| Seed executed twice / idempotency verified | No |
| Persistence tests | Not run |

The process identity was shady\shady. The read-only sqlcmd probe to tcp:localhost,1433 failed with a socket access-permission error and login timeout before returning SQL Server identity, SYSTEM_USER, or database name. The SQL Server service/listener was not visible from this execution environment. The target Shady/NexusPmo database and authenticated SQL principal therefore remain unverified.

ASPNETCORE_ENVIRONMENT, NEXUS_EF_DEVELOPMENT_CONNECTION_STRING, and NEXUS_ADMIN_BOOTSTRAP_PASSWORD are absent from the current process. No independent backup passed SQL Server verification. No SQL read or write, DROP, recreation, migration, or seed occurred. The current executor could not start the Windows app server needed to inspect a preconnected local SQL UI session.

## E. Testing

| Gate | Result |
|---|---|
| React full suite | PASS — 141 files, 765/765 tests |
| TypeScript | PASS — included in npm run build |
| React production build | PASS |
| API solution build | PASS — 0 errors, 352 IKVM/MPP warnings |
| EF offline model gate | PASS — 10 migrations, model synchronized, database connection not opened |
| Focused React retry tests | PASS — meetings, report finance permission, date formatting, and visible-first loading |
| Backend permission tests | NOT EXECUTED — VSTest failed to connect to testhost after 60 seconds; no test results |
| Runtime Grant/Deny API tests | NOT RUN |
| Existing-session revocation tests | NOT RUN |
| SQL persistence, seed, and idempotency | NOT RUN |
| Browser runtime security UAT | NOT RUN |

The React build reports existing bundle-size and dynamic-import warnings. They did not fail the build.

## F. Outstanding gates

1. Run the EF gate from a host-connected Windows process with ASPNETCORE_ENVIRONMENT=Development and the approved secure Development connection.
2. Verify the Development environment, actual server/principal, and `Shady/NexusPmo` database identity. Development/UAT backups are optional for resets and ordinary authorized changes; the reset remains destructive and requires explicit Product Owner authorization. Production backup and recovery safeguards remain mandatory.
3. Supply the bootstrap secret through the approved secure environment.
4. Apply the ten source migrations to the recreated Development database; verify __EFMigrationsHistory and physical schema.
5. Run the complete application seed twice and compare persisted catalog, roles, Admin grants, membership, and reference data.
6. Execute backend authorization tests, direct API Grant/Deny tests, session revocation, tenant isolation, protected Admin, and no-mutation checks against that database.

**Final classification: IMPLEMENTED — VERIFICATION PENDING.** Builds and frontend tests pass, but the database remains untouched and all required live SQL/API security gates remain open.

## Current role-permission delegation clarification (2026-11-09)

This report’s 2026-11-02 snapshot predates the current Product Owner delegation rule. The earlier actor-possession ceiling for ordinary custom-role permissions is superseded. An actor authorized through `Roles.ConfigurePermissions` may grant or revoke any active, assignable, scoped non-Administration permission to a same-Tenant user-managed custom role regardless of target category or whether the actor holds the key. Administration-module permissions remain actor-authorized only.

The clarification preserves tenant/resource boundaries, declared dependencies, Active Deny, Platform/CrossTenant/SystemOwner restrictions, protected/system-managed role safeguards, the exact 13-key protected tenant Admin exception, and last-administrator protection. No-self-escalation remains: a role assigned to the actor cannot be changed to add an effective permission beyond the permissions currently effective for the actor. Cross-category BudgetRevision grant/save/reload/revoke UAT is pending in the Master UAT Checklist. The screenshot cause was the prior actor-possession/first-grant `CanDelegate` filter: ordinary permission rows could appear disabled when the authorized role administrator did not personally hold each key. Under the clarified policy, actor possession alone must not disable an eligible non-Administration custom-role grant. A missing `Roles.ConfigurePermissions` capability, Administration-module authorization, target-role restriction, dependency, Active Deny, scope, protected-role boundary, or no-self-escalation remains a valid restriction. The earlier API build/test attempt stopped at a locked output DLL; API build and focused permission tests remain unverified, and no pass is recorded. Browser UAT remains pending. This addendum documents policy and screenshot cause; no build, test, or database operation was run for this documentation update.
