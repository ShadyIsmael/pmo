# P0 Code Review Remediation 01 — Tenant Isolation & Authorization

**Date:** 2026-11-04
**Repository:** `C:\Projects\pmo`
**Status:** Source remediation and API build reported complete; offline EF model gate passed. Product Owner deferred the focused test run to the next session. Runtime security verification is blocked because the Development API/database are unavailable or not independently verified. **P0 is not verified closed.**

## Findings and disposition

| Finding | Classification | Root cause and remediation |
|---|---|---|
| Missing tenant context could resolve to the Demo Tenant. | **Confirmed — remediated in source** | `AppDbContext.GetCurrentTenantId` now returns `Guid.Empty` for missing, malformed, or unauthenticated HTTP context. Out-of-request work requires an explicit `TrustedTenantExecutionContext`; Development startup seeding enters that scope in `Program.cs`. An unauthenticated HTTP request cannot inherit an ambient trusted scope. |
| Portfolio/Program reads could become unfiltered when management context was absent; status changes looked up records by ID. | **Confirmed — remediated in source** | Tenant and management scope are required for relevant reads and writes. `SetStatusAsync` uses tenant and management-scope checks. Protected Tenant Admin authority and Active Deny behavior were preserved. |
| Project services could substitute the Demo Tenant or pass an unscoped query through; baseline snapshots could be stamped with the fallback tenant. | **Confirmed — remediated in source** | Project reads, creates, and hierarchy resolution fail closed without tenant/scope. Baseline snapshot creation requires a tenant and verifies project ownership. The dead Project Handover Demo helper was removed; active paths already required tenant context. |
| Identity login and `/me`/role-switch flows did not consistently fail closed on missing or ambiguous tenant identity. | **Confirmed — remediated in source** | A client tenant selector only narrows login candidates. The authenticated token tenant is sourced from the persisted user. Ambiguous or malformed identity fails generically; email/password-only login remains supported for a unique matching account. |
| Workflow Definition reads and mutations could be tenant-sensitive; creation/audit paths had Demo fallback behavior. | **Already fixed / confirmed remediation** | Authenticated List/Get/Update/draft-delete are protected by the existing combined tenant and soft-delete query filters. Missing-context create/audit paths now require a valid tenant. Project/plan/version route ownership is checked by `ProjectResourceScopeMiddleware`. |
| Workflow guards trusted stale plural `permissions`/`roles` claims, and client metadata could replace guard inputs. | **Confirmed — remediated in source** | `WorkflowEntityController.BuildExecutionContext` derives guard claims from the refreshed singular `permission` and `ClaimTypes.Role` claims. `WorkflowExecutionService` reserves `permissions` and `roles` for trusted context metadata and strips client attempts, while retaining ordinary request metadata. |
| Documents could be exposed when management scope was null, or through project-linked ownership; category and upload/version tenant ownership were not consistently validated. | **Confirmed — remediated in source** | Document reads require trusted actor/tenant context and applicable project scope. Ownership applies only to unlinked documents. Categories are tenant-validated; document/version writes stamp the trusted tenant and actor. |
| AI Planning requests/responses were not stamped from trusted identity. | **Confirmed - remediated in source** | AI Planning now requires authenticated tenant and requester context and stamps those values on records. History reads are scoped to both tenant and requester. The claimed cross-tenant project-data retrieval was a **false positive**: the service does not retrieve project data. Request/response entities have no Project or Organization association; any broader shared-history policy needs explicit Product Owner direction. |
| Supplemental services had Demo Tenant fallbacks or treated missing management context as broad access. | **Confirmed — remediated in source** | The assigned cross-module review reports fail-closed tenant/scope handling in Meetings, Organization, Resource Management, Risks, Work Items, ProjectPlanning, and workflow adapters. Trusted startup provisioning uses the explicit execution context. |

These are source-level dispositions from the assigned reviewers. Authenticated cross-tenant runtime behavior was not verified because the API/test build did not reach execution.

## Files changed

Primary tenant/authentication scope:

- `apps/api/src/NEXUS.Persistence/AppDbContext.cs`
- `apps/api/src/NEXUS.SharedKernel/TrustedTenantExecutionContext.cs` (new)
- `apps/api/src/NEXUS.Api/Program.cs`
- `apps/api/src/NEXUS.Modules.Identity/Api/AuthController.cs`
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs`
- `apps/api/src/NEXUS.Modules.Identity/Infrastructure/DemoAuthService.cs`
- `apps/api/src/NEXUS.Modules.Portfolio/Application/IPortfolioService.cs`
- `apps/api/src/NEXUS.Modules.Portfolio/Application/IProgramService.cs`
- `apps/api/src/NEXUS.Modules.Portfolio/Application/PortfolioService.cs`
- `apps/api/src/NEXUS.Modules.Portfolio/Application/ProgramService.cs`
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectHandoverRequestService.cs`
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectService.cs`
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectHandoverRequestService.cs`
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs`
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectSnapshotService.cs`

Workflow, planning, documents, and AI:

- `apps/api/src/NEXUS.Modules.WorkflowEngine/Api/WorkflowEntityController.cs`
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowExecutionService.cs`
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowManagementService.cs`
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/IPlanAssignmentService.cs`
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanActivityService.cs`
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanAssignmentService.cs`
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanDependencyService.cs`
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanGridCommandHistoryService.cs`
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanService.cs`
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanVersionService.cs`
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanWbsService.cs`
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanningCalendarService.cs`
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanningImportService.cs`
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanningTablePreferenceService.cs`
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentService.cs`
- `apps/api/src/NEXUS.Modules.AIPlanning/Application/AIPlanningService.cs`
- `docs/modules/DOCUMENT-MANAGEMENT-CORE.md`

Supplemental fail-closed service changes:

- Meetings: `apps/api/src/NEXUS.Modules.Meetings/Application/IMeetingService.cs`, `MeetingService.cs`
- Organization: `apps/api/src/NEXUS.Modules.Organization/Application/IOrganizationalUnitService.cs`, `OrganizationalUnitService.cs`, `WorkItemStatusReferenceService.cs`
- Resource Management: `apps/api/src/NEXUS.Modules.ResourceManagement/Application/AllocationService.cs`, `IVendorService.cs`, `ProjectRoleService.cs`, `ResourceService.cs`, `TeamService.cs`, `VendorService.cs`
- Risk Management: `apps/api/src/NEXUS.Modules.RiskManagement/Application/RiskService.cs`, `RiskWorkflowIntegration.cs`
- Work Items: `apps/api/src/NEXUS.Modules.WorkItems/Application/WorkItemService.cs`, `WorkItemWorkflowIntegration.cs`

Eight focused test files were added under `apps/api/tests/NEXUS.Api.Tests/Security/`: `TenantContextFailClosedTests.cs`, `DemoAuthTenantIsolationTests.cs`, `DocumentTenantBoundaryTests.cs`, `PortfolioProgramStatusScopeTests.cs`, `WorkItemWorkflowProvisioningTenantTests.cs`, `WorkflowDefinitionTenantIsolationTests.cs`, `WorkflowTrustedGuardMetadataTests.cs`, and `AIPlanningTenantIsolationTests.cs`.

The working tree contains many other concurrent edits, including an `AppDbContextModelSnapshot.cs` change. Those changes were preserved. This remediation did not generate or apply a migration.

## Skills and subagent execution

Skills discovered, read, and applied during this remediation/closure work:

| Skill | Source | Application |
|---|---|---|
| `security-audit` | Repository `.agents/skills` | Reviewed trust boundaries, test evidence, and safe limits for runtime verification. |
| EF Core database tooling | Repository documentation and scripts | Ran only its offline model check; no Development database connection or writes. |
| `documentation-first-task-execution` | Global `.codex/skills` | Read project setup and SDK troubleshooting guidance before build/EF checks. |
| `efcore-patterns` | Global `.codex/skills` | Reviewed EF model/snapshot conventions; used the repository gate for model consistency. |
| `entity-framework-core` | Global `.codex/skills` | Applied EF model and migration inspection guidance. |
| `secure-coding-standards` | Global `.codex/skills` | Applied fail-closed identity, ownership, and tenant-boundary review. |
| `universal-software-engineering` | Global `.codex/skills` | Applied bounded, sequential verification and preserved concurrent work. |
| `codeql` | Repository `.agents/skills` | Read; not applied because this was closure verification, not a new code scan. |
| `semgrep` | Repository `.agents/skills` | Read; not applied because this was closure verification, not a new code scan. |

The project document `docs/development/dotnet-sdk-troubleshooting.md` was read and used for supported SDK-repair guidance; it is not a skill. Repository/global skill discovery found **no dedicated .NET SDK, MSBuild, IKVM, multi-tenancy, or testing skill**. CodeQL and Semgrep were read but not run; this turn continued the existing remediation rather than restarting a code scan.

- **Main Agent:** integrated the four workstreams, checked current SDK/build/test/runtime evidence, started a no-build VSTest run, then stopped it when the Product Owner asked to defer testing. No test result was claimed.
- **A - Build Environment:** verified SDK selection/MSBuild, found the workload resolver registration and assembly present, and found no evidence justifying SDK repair. Prior full API build and IKVM conversion completed; no lock owner could be identified because process-handle inspection was denied.
- **B - Security Tests:** reviewed the eight remediation test files and existing Active Deny/protected Admin suites. Identified remaining coverage gaps listed below; did not run tests.
- **C - AI History & EF:** confirmed requester-and-tenant-owned AI history and a passing offline EF model/snapshot gate; no DB access.
- **D - Runtime Security:** found no API or SQL listener and no independently verified Development DB target; made no runtime calls or DB changes.

## Verification results

- **SDK:** `global.json` pins `10.0.302` (`latestFeature`). `dotnet --version` succeeds; `dotnet msbuild -version` reports MSBuild `18.6.11.33009`. The workload resolver registration points to a resolver DLL present in the SDK root. `dotnet --info` remains unavailable in this token because its Windows Service Control Manager query returns Access Denied (Win32 5). This is not evidence of SDK corruption; no SDK repair or file modification was performed. Earlier missing `Sdks/...Locator/Sdk` messages were not reproduced as a fatal build cause.
- **IKVM/API build:** a full serial API build with project references enabled completed according to the build run recorded in this task: **0 errors, 352 IKVM Java multi-release/unsupported-class warnings**. The MppAdapter conversion completed. The exact command transcript/binlog was not retained in the resumed workspace. A prior test build hit `MSB3027/MSB3021` copying the MppAdapter DLL; the isolated test output was built afterward, but no lock owner was identified. No persistent SDK/IKVM blocker was established by the successful full build.
- **Focused tests:** **deferred by Product Owner instruction**. A direct `dotnet vstest` attempt used the already-built `bin/p0-01-tests/Debug/net10.0/NEXUS.Api.Tests.dll` with a 300-second testhost connection timeout and the eight remediation suites plus Active Deny/protected Admin suites. VSTest matched one test assembly and emitted no test results during about 150 seconds; it was then interrupted per the user's instruction to leave testing for the next session. No TRX was produced; **0 tests are reported as passed or failed**. The test sources and adjacent Active Deny/Admin suites were reviewed, not executed.
- **Security test review:** reviewed coverage includes cross-tenant workflow definition operations, tenant/requester AI history access, document ownership/category boundaries, Portfolio/Program management scope, missing/malformed tenant context, `Guid.Empty`, trusted startup context isolation from HTTP, and same-tenant positive cases. Existing `EffectivePermissionResolverTests` and `UserPermissionAdministrationTests` cover explicit Deny and protected Admin grants/removal. Gaps remain for a ProjectPlanning service cross-tenant write/scope test, direct `AppDbContext.SaveChanges` with no tenant, foreign-project document upload/create, and last-admin lockout.
- **Runtime:** not run. The runtime workstream observed no running API and no listeners on ports `5000`, `5001`, `5002`, or `1433`; SQL service status was unavailable. `appsettings.Development.json` names `tcp:localhost,1433`/`NexusPmo`, but that is configuration evidence, not target verification. No authenticated API requests were made.
- **Diff hygiene:** the earlier scoped `git diff --check` passed with line-ending conversion warnings; the repository-wide check reported unrelated whitespace/EOF issues in the heavily modified worktree. No new repository-wide diff check was run during this closure turn.
- **Database operations:** none. No query, reset, seed, migration, grant change, or data write occurred.

## EF/documented database workflow

| Required evidence | Status |
|---|---|
| Model synchronized | **Yes**, offline gate passed; `NEXUS.Persistence.AppDbContext`, 10 source migrations, no pending model changes. |
| Pending Development migration count and names | **Unknown / not inspected**; the offline model gate does not connect to Development. |
| Development database identity verified | **No**. |
| Backup verified | **Not run / not needed**, because no DB write was authorized or performed. |
| Migrations applied | **None**. |
| Physical schema verified | **No**. |
| Persistence tests | **Not run**. |
| Blockers | Focused tests were deferred by Product Owner request; Development API/DB unavailable and target identity not verified. |

## Remaining blockers

1. Run the focused security tests in the next session using the already-built output or a controlled serial build; include the uncovered ProjectPlanning, missing-tenant write, document upload, and last-admin cases where feasible. Preserve the TRX and exact command.
Next-session focused command (uses the already-built isolated test assembly):

```powershell
$env:VSTEST_CONNECTION_TIMEOUT='300'
dotnet vstest 'apps/api/tests/NEXUS.Api.Tests/bin/p0-01-tests/Debug/net10.0/NEXUS.Api.Tests.dll' '--logger:trx;LogFileName=p0-01-tenant-isolation.trx' '--ResultsDirectory:apps/api/tests/NEXUS.Api.Tests/TestResults' '--TestCaseFilter:FullyQualifiedName~TenantContextFailClosedTests|FullyQualifiedName~DemoAuthTenantIsolationTests|FullyQualifiedName~DocumentTenantBoundaryTests|FullyQualifiedName~PortfolioProgramStatusScopeTests|FullyQualifiedName~WorkItemWorkflowProvisioningTenantTests|FullyQualifiedName~WorkflowDefinitionTenantIsolationTests|FullyQualifiedName~WorkflowTrustedGuardMetadataTests|FullyQualifiedName~AIPlanningTenantIsolationTests|FullyQualifiedName~ProtectedTenantAdminIdentityTests|FullyQualifiedName~EffectivePermissionResolverTests|FullyQualifiedName~UserPermissionAdministrationTests'
```

2. Start the authorized Development API and SQL Server, then independently verify the connected server/database through the read-only EF gate before authenticated cross-tenant and cross-organization requests. Use existing authorized test identities and data; do not reset or migrate the DB without separate authorization.
3. Decide whether AI Planning history should remain personal to the requester or support explicit tenant/team sharing. Current enforcement is requester + tenant; the entities have no Project/Organization association. `RequestedBy` is nullable, so legacy ownerless rows would be hidden; their presence is unknown without DB inspection.
4. Keep Development schema synchronization open until the gate `inspect` action verifies actual migration history and physical schema. The offline model pass does not establish database state.

**Closure decision:** not closed. Source remediation, full API build, and offline EF model consistency are reported/passed, but automated security tests were deferred and authenticated runtime verification remains unavailable. Phase 02 was not started.
