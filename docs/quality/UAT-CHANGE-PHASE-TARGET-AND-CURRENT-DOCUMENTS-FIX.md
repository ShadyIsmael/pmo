# FOPX PM | P0 Repeated Change Phase Target Options 404 Recovery

**Date:** 2026-11-13  
**Priority:** P1 — Confirmed UAT blocker  
**Scope:** Project Settings → Change Phase → Target Phase  
**Status:** BLOCKED — repeated live 404 confirmed; actual failing stage and authenticated Development success remain unverified.

## Earlier source diagnosis (not confirmed for the repeated live failure)

The latest frontend implementation selected the first Organization in the Management Snapshot and sent it as the Project's Organization. For users with access to multiple Organizations, that first item is not guaranteed to own the Project. The lifecycle service validates a supplied Organization against the Project scope and responds 404 on a mismatch. This was a source-level defect hypothesis addressed earlier; it does not establish the cause of the current request, which omits organizationId.

The external identity contract is unchanged: callers send the Project.PublicId. ProjectPublicIdRoutingMiddleware resolves that ID within the authenticated Tenant and rewrites the query value to the internal Project.Id before the controller and service run. The service therefore consumes Project.Id after middleware. Earlier service-only tests used the internal ID directly and did not prove the HTTP PublicId path; focused HTTP coverage has now been added.

## Backend correction

- Project-specific options now require the Project to be in the actor's Management Scope.
- The owning Organization is resolved from persisted Project ownership using the existing ProjectOrganizationLookup: Project → Program/Portfolio → Organization, or the supported Department → Business Unit → Organization path.
- An optional organizationId is checked against that persisted owner. A mismatch, unresolved owner, invalid Project, cross-Tenant Project, or out-of-scope Project fails closed; no Project-scoped Tenant-wide phase fallback is returned.
- Target phase options include only active entries configured for the owning Organization. Tenant-seeded legacy phases, inactive entries, and other Organizations' entries are excluded without deleting historical records.
- Protected Pre-Initiation provisioning and its first position are preserved.
- The existing Project.Status filtering and non-Project/Create Project option behavior are preserved. No permissions, transitions, or database schema changed. Effective permission checks and Active Deny remain enforced.

## Frontend correction

- ChangePhaseSettings and ProjectPhaseGateSideSheet send project.publicId and omit organizationId.
- Organization selection from organizationReferences[0] / organizationIds[0] is removed.
- Client-side synthetic Pre-Initiation and forced phase ordering are removed. The API is authoritative for phase existence, labels, and displayOrder; API failures remain visible with retry.
- Lifecycle React Query options are keyed by project ID, so changing Projects selects that Project's cached/fetched options. Both components enable the options request only when project.publicId is available.
- Both Change Phase interfaces keep the document checklist bound to the persisted Current Phase; selecting another Target Phase does not change the checklist.
- The Create Project lookup contract remains unchanged: purpose=create, portfolioId, and departmentId.

## Verification results

| Check | Result |
|---|---|
| React Change Phase component tests | Passed — npm run test -- src/test/change-phase-settings.test.tsx src/test/project-phase-gate-side-sheet.test.tsx; 2 files, 13 tests |
| React TypeScript check | Passed — npm run typecheck |
| Backend focused tests | Not run successfully — dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --filter FullyQualifiedName~ProjectLifecycleCatalogOptionsTests --verbosity minimal exited 1 without output before test discovery |
| Backend HTTP integration execution | Not verified — the new test runs an HTTP request through authentication, PublicId middleware, MVC controller, and service, but the test runner did not reach Kestrel |
| .NET CLI diagnosis | dotnet --info exited 1 with Service Control Manager access denied from the WindowsUpdateAgent/workload-installer path |
| Development HTTP runtime | Not verified — requests to https://localhost:5001 and http://127.0.0.1:5002 were blocked before an HTTP response |
| Browser UAT | Not run, as requested; awaiting Product Owner |
| Database changes | None — no reset, manual SQL, migration, seed, or Development database write |

Focused backend coverage is present in ProjectLifecycleCatalogOptionsTests.cs. Service-level tests use internal Project.Id and cover stale multi-Organization scope metadata, persisted ownership resolution, mismatched/unresolved Organization, active exact-Organization phases, Pre-Initiation-only configuration, and legacy phase exclusion. The Kestrel integration test sends external Project.PublicId through ProjectPublicIdRoutingMiddleware and covers the authorized 200 path plus mismatch, invalid, cross-Tenant, and out-of-management-scope 404 responses. ProjectPhaseGateHistoryTests.cs now uses persisted ownership for its lifecycle options fixture. The test project retains the permanent _MSTestEnableParentProcessQuery=false setting. The backend test source could not be executed in this environment.

## Authentication and runtime diagnosis

The API requires authentication for both /api/auth/me and lifecycle options. AuthProvider skips /api/auth/me when there is no local session. When a stored session exists, it calls /api/auth/me; after a 401 the client attempts cookie refresh and retries. Without the session state or application logs, the specific reported /api/auth/me 401 cannot be classified as a transient refresh step versus an expired/missing session.

The authentication request and lifecycle 404 are separate. The lifecycle 404 branches are Project PublicId resolution, Project management-scope membership, or a mismatched supplied Organization. The 401 did not justify changing authentication, cookies, CSRF, or authorization. Vite proxies /api to https://localhost:5001 with secure certificate validation. No runtime response or relevant application logs were available.

## Database and persistence status

| Required report item | Status |
|---|---|
| EF model synchronized | Not rechecked; this task changed no persisted model or schema |
| Pending Development migrations | Not checked for this task |
| Development target identity | Not verified |
| Backup | Not requested |
| Migrations applied | None |
| Seed mode/idempotency | Not run |
| Physical schema | Not checked |
| Development persistence tests | Not run |
| Blockers | Restricted .NET CLI execution and inaccessible local API runtime; backend HTTP test and live Project ownership remain unverified |

## Product Owner retest

1. Sign in with an active session and use an account authorized for the Project and ProjectPhase.Change.
2. Open Project Settings → Change Phase. In the Network panel, confirm the options request includes projectId equal to the Project.PublicId and contains no guessed organizationId.
3. Confirm Target Phase shows only active phases configured for that Project's persisted Organization, with protected Pre-Initiation first and the configured display order and EN/AR labels.
4. Repeat for a user with access to multiple Organizations and a Project whose owning Organization is not first in the Management Snapshot. Then switch to a Project in another Organization and confirm its options change with the Project.
5. Change Target Phase and confirm the document checklist still describes the persisted Current Phase. After an authorized phase transition, confirm the checklist refreshes for the new Current Phase.
6. Confirm Create Project Phase/Status defaults still load with its existing Portfolio/Department scope, and that Phase Gate Readiness and History continue to load for the Project.
7. If /api/auth/me returns 401, record whether cookie refresh succeeds and the final authentication state separately from the lifecycle options response.

Record the Project.PublicId, owning Organization, request/response status, and sanitized returned phase IDs if any step fails. The Development database ownership and Reference Data comparison remain NOT VERIFIED until a host-side runtime check is available.



## P0 repeated 404 recovery — 2026-11-13 authoritative status

**Outcome: BLOCKED. RUNTIME VERIFICATION: BLOCKED / NOT VERIFIED.**

The supplied request omits organizationId, so the earlier first-Organization explanation is not a confirmed cause of this failure. Native Edge DevTools inspection independently showed the exact supplied response and trace `0HNQ1D8GSO4ML:00000001`. No new production fix is justified without identifying the failing stage. No frontend workaround or production behavior change was made in this recovery.

| Check | Actual result | Evidence | Status |
|---|---|---|---|
| Project PublicId exists | Cannot query current Development rows; no conclusion about existence | Fresh preflight SQL open denied before identity checks | NOT VERIFIED |
| Internal Project ID mapped | Supplied GUID could not be checked against either Projects.PublicId or Projects.Id | Same SQL execution boundary | NOT VERIFIED |
| Middleware rewrite | Source globally detects projectId query, looks up PublicId in authenticated Tenant, rewrites IQueryFeature to Id and stores both IDs | ProjectPublicIdRoutingMiddleware.InvokeAsync | SOURCE VERIFIED; LIVE NOT VERIFIED |
| Controller input | Forwards MVC-bound projectId unchanged to service | ProjectLifecycleCatalogController.GetOptions | SOURCE VERIFIED; LIVE NOT VERIFIED |
| Service lookup | Uses Project.Id with Tenant and !IsDeleted; missing Project or absent management scope returns supplied detail | ProjectLifecycleCatalogService.GetOptionsAsync | SOURCE VERIFIED; LIVE NOT VERIFIED |
| Organization resolution | Registered ProjectOrganizationLookup follows persisted Program/Portfolio or Department/BusinessUnit, then requires active same-Tenant Organization; unresolved owner or mismatch also returns same detail | Program.cs DI; ProjectOrganizationLookup.cs; service | SOURCE VERIFIED; LIVE NOT VERIFIED |
| Authentication context | DevTools contains auth/me 401 errors alongside lifecycle 404; no correlated session/claims/server log evidence | Native DevTools observation; frontend refresh behavior in previous section | NOT VERIFIED |
| HTTP integration tests | Existing fixture strengthened to production middleware ordering and explicit identifier/session assertions; execution result below | ProjectLifecycleCatalogOptionsTests.cs | SEE EXECUTION RESULT |
| Live authenticated HTTP request | No successful 200 for supplied PublicId; recorded 404 observed | Native DevTools exact response; executor localhost connections fail before HTTP | BLOCKED |
| Reference Data comparison | No live owning Organization or returned phases available | Requires SQL identity/ownership and successful authenticated response | NOT VERIFIED |

The service's exact detail is compatible with Project-not-found, scope exclusion, unresolved owner, or Organization mismatch. Middleware currently emits a different resource-not-found detail. This narrows source candidates but cannot prove which code/version produced the live response. No double conversion was found in current source. The global Project query filter adds Tenant/soft-delete filtering; its simple lookup has no ownership joins.

Runtime configuration points Vite HTTPS :4100 /api to HTTPS :5001 with certificate validation enabled. The executor could not connect to either port (curl exit 7, HTTP 000); its process inventory exposed no backend/node process and TCP/CIM inspection was denied. Filesystem DLL timestamps do not establish the running host binary version. No server log correlated the supplied trace. Authentication, cookies, CSRF and authorization were preserved.

### Fresh database evidence

- Approved read-only invocation: `./scripts/dev/db-preflight.ps1 -NoBuild`, once.
- Offline model synchronized: **Yes**, 26 repository migrations; `artifacts/ef-review/dev-db-preflight-model-20261113T094744Z-07ee4219.json`.
- SQL inspect: exit 1, `TCP Provider: Access is denied`, before SQL identity/history; `artifacts/ef-review/dev-db-preflight-inspect-20261113T094809Z-9e875fe0.json`.
- Actual Development server/database/DB ID, Windows Authentication success, pending migration count/names and physical schema: **Not verified**. Expected Shady/NexusPmo is not treated as observed identity.
- Backup: **Not requested**. Migrations applied: **None**. Seed mode/idempotency: **Not run**. Development persistence tests: **Not run**.
- No reset, manual SQL patch, migration, seed or database write. No alternate host aliases, tunnel or security changes.

### Changed files and remaining verification

This recovery changes only this report, MASTER-UAT-CHECKLIST.md and ProjectLifecycleCatalogOptionsTests.cs. The test fixture now runs real PublicId routing, ManagementContextMiddleware and ProjectResourceScopeMiddleware in production order, with a mocked scope resolver and test authentication (not the production JWT/session stack). It asserts rewritten internal ID and original PublicId, rejects external use of internal ID, and checks missing/invalid test sessions. Existing configured phase, multi-Organization, invalid, inaccessible and cross-Tenant assertions remain. Service-level tests continue using internal IDs. `_MSTestEnableParentProcessQuery=false` remains set.

Required host step (read-only, PowerShell 7):

```powershell
Set-Location 'C:\Projects\pmo'; .\scripts\dev\db-preflight.ps1
```

Review its sanitized report for actual SERVERPROPERTY('ServerName'), DB_NAME(), DB ID, authentication, EF history and schema. The existing gate does not include a Project GUID diagnostic action; preflight alone cannot establish Project ownership. Obtain authorized read-only Project/ownership evidence and correlate the exact trace/session with the receiving API version before choosing any fix. Then execute current HTTP regressions and authenticated request for the supplied PublicId, compare returned phase IDs to the actual owning Organization Reference Data, and verify safe errors, Create Project options, Phase Gate Readiness/History, Active Deny and scope boundaries. Browser UAT is still open; this is an environment/identity blocker handoff, not a claim of correction.

### Execution result for this recovery

- Existing-binary VSTest command: `dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~ProjectLifecycleCatalogOptionsTests -p:_MSTestEnableParentProcessQuery=false`.
- Reached VSTest assembly discovery (one assembly matched); aborted because vstest.console could not connect to testhost after 90 seconds. This does not execute or verify the updated source and is an environment/testhost communication failure, not a failing assertion.
- Actual completed test counts for this recovery: **0 passed, 0 failed assertions; run aborted before test results**. Mandatory HTTP integration, Create Project and Phase Gate runtime regressions remain unverified.
- Current-source serial build: INCONCLUSIVE / CANCELLED with Ctrl+C in its owned session after approximately eight minutes of IKVM MppAdapter compilation. Exit 1 reflects cancellation; no C# compilation error was observed and no fresh test assembly was produced. Updated-source tests: Not run. Partial module compilation is not a passing build. No owned build/test session remains active. The earlier SCM failure was not reproduced on these CLI paths; the separate VSTest testhost communication failure remains unexplained.


