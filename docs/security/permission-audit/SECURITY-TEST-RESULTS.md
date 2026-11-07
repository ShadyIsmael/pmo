# Security Test Results

**Snapshot:** 2026-11-03  
**Execution status:** Source implementation is integrated. Live SQL/API authorization evidence remains pending.

This update adds source-level coverage for Cost Utilization read/export separation, empty/out-of-scope management scope, the Project Financial Foundation read key, and separate Program/Portfolio rollup permissions. These new tests were not executed in this update; prior build and test results below retain their original evidence boundary.

## Executed gates

| Gate | Result | Evidence |
|---|---|---|
| React full suite | PASS | 141 files; 765 passed, 0 failed |
| React TypeScript and production build | PASS | npm run build completed with TypeScript and Vite production build |
| API solution build | PASS | dotnet build apps/api/NEXUS.slnx --no-restore --verbosity minimal -m:1; 0 errors and 352 IKVM/MPP warnings |
| EF offline model gate | PASS | scripts/database/ef-database-gate.ps1 model -NoBuild reported AppDbContext, 10 source migrations, synchronized model; database connection not opened |
| Focused React regressions | PASS | Meetings list/workspace and visible-first: 10/10; report finance permission and date formatting: 9/9 |

The UI test fixes align assertions with the current meeting label and visible calendar date range. The visible-first test now checks exact endpoint paths, excluding the visible Overview team-capacity request from hidden Team/People tab requests. The portfolio report reads financial-rollup currency groups from the API and only requests them when the user has ProjectFinance.View.

## Backend authorization tests

**NOT EXECUTED.** The API test project produced a test assembly and VSTest matched the test DLL, but the testhost did not connect to vstest.console after a 60-second timeout when running the Permission filter. A previous full-project attempt with a longer timeout also failed before test execution. New focused source tests (`CostUtilizationExportAuthorizationTests`, `ProgramPortfolioFinancialRollupAuthorizationTests`, `CostUtilizationManagementScopeTests`, and `FinancialPermissionDelegationTests`) were added after that attempt and are also not counted as passed.

Source tests are present for effective permission resolution, canonical policy contracts, catalog architecture, protected Admin behavior, tenant/resource authorization, and no-mutation scenarios. Because the testhost did not connect, those source tests do not provide execution evidence.

## Runtime security matrix

| Scenario | Result |
|---|---|
| Allow on eligible API action | NOT RUN |
| No grant / revoked grant | NOT RUN |
| Role Allow + Direct Deny | NOT RUN |
| Multiple Role Allows + Direct Deny | NOT RUN |
| Direct Allow + applicable Deny | NOT RUN |
| Unknown or retired permission | NOT RUN against API/database |
| ProjectAnnualBudget.Create after same-session Direct Deny | NOT RUN |
| Financial approval separation | NOT RUN |
| ProjectFinancialFoundation.View read allow/missing/retired alias | NOT RUN |
| Cost Utilization Resource.View read and Resource.ViewCostRate redaction | NOT RUN |
| CostUtilization.Export composite allow/missing/deny | NOT RUN |
| Empty Cost Utilization management scope and ProjectId scope resolution | NOT RUN |
| ProgramFinancialRollup.View + Program.View allow/deny/missing/cross-scope | NOT RUN |
| PortfolioFinancialRollup.View + Portfolio.View allow/deny/missing/cross-scope | NOT RUN |
| Roles UI initial grant visibility for new finance keys | BLOCKED: no approved initial grant-holder/path; do not expand Admin baseline |
| Out-of-scope Portfolio/Project | NOT RUN |
| Cross-Tenant read or mutation | NOT RUN |
| Protected Admin removal or Deny | NOT RUN |
| Last Admin removal | NOT RUN |
| Future eligible Tenant Administration key | NOT RUN |
| Denied mutation leaves database unchanged | NOT RUN |
| AuthProvider refresh after 403 | Frontend regression tests pass; no live API/session test |

The action matrix records Allow and Deny evidence as not run. Static source mapping and passing React tests do not establish backend 403 behavior or database integrity after a denied request.

## Active Deny and current sessions

The source permission resolver applies Deny precedence and the permission claims/session pipeline has been updated for fresh effective permissions. Frontend tests pass. Backend resolver tests, direct API Deny tests, multi-instance propagation, and a request made with an already-open session remain unverified because the API testhost and SQL connection were unavailable.

## Protected Admin

The source role identity is Role.SystemRoleKey = tenant.admin. The catalog-derived baseline contains 48 current keys and excludes Platform, CrossTenant, SystemOwner, and unapproved Organization-level permissions. Persistence, role-edit rejection, direct Deny rejection, future-key inclusion, locked browser state, and last-admin runtime scenarios remain unverified.

## SQL and seed

whoami returned shady\shady. The read-only sqlcmd connection attempt failed before SQL authentication with a socket access-permission error and login timeout. It returned no SQL server name, database name, or authenticated principal. No backup, database reset, migration, seed, or persistence test ran.
