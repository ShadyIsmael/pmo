# FOPX PM — Project Ownership Integrity & Lifecycle Options 404 Recovery

**Date:** 2026-11-13  
**Priority:** P0  
**Repository:** `C:\Projects\pmo`  
**Outcome:** **OWNERSHIP DECISION REQUIRED**

## Disposition

The ownership-resolution failure is the leading explanation for the Lifecycle Options 404, but it is **not independently confirmed**. The user-provided tab-separated row contains 38 values while the earlier query was reported to have 40 output columns and had no headers. If those values align with the earlier column order, the Project has a direct Portfolio with no `OrganizationId`, and the Department-to-Business-Unit-to-Organization joins are empty. That would leave the current resolver with no Organization. The two missing values and absent headers prevent treating that interpretation as verified data.

No intended Organization or candidate list was supplied. No Development data was changed, no Organization was guessed, and the authenticated request was not replayed. A successful lifecycle options request can call `EnsurePreInitiationPhaseAsync`, which can write lifecycle catalog data.

The source-level prevention gap was addressed narrowly: new Project creation and ownership reassignment now fail validation when the current same-tenant ownership resolver cannot find an active Organization. Unrelated edits to legacy Projects with unresolved ownership remain allowed. The focused tests were added but could not reach test discovery in this environment. These code changes do not establish or repair this Project's existing owner.

## Preparation and preservation

- Read the repository instructions, current lifecycle/ownership specifications, the prior Lifecycle Options root-cause report, and the prior Change Phase recovery report.
- The worktree contained 779 existing status entries, including broad concurrent changes. No stash, reset, commit, or cleanup was performed.
- A non-destructive checkpoint was created at `C:\Users\shady\AppData\Local\Temp\pmo-worktree-checkpoint-20261113T134446Z` before edits. It records HEAD `335b19dff3f39e074251587a8f420d3c32ead11d`, a tracked working-tree patch (SHA-256 `92C1B77EC45D29D66A156DB00D0ECF7DAB0D2AFDA32D33A695013A136653CD1A`), and an archive of all 648 pre-existing untracked files (SHA-256 `4CACEDF2D28569D3F72D700029273F4F4D6BFF978C85A25FEE20B0194C6CA668`). Both hashes were re-read and matched.
- Independent reviews covered database evidence, ownership/domain rules, and lifecycle routing/authorization. Only the main agent made repository changes; no database write was made.

## Development database evidence

The Product Owner supplied the output of `scripts/dev/db-preflight.ps1 -NoBuild` from PowerShell 7.7.0:

| Check | Actual result |
|---|---|
| EF model synchronization | Yes; offline model check; 26 repository migrations listed |
| Development target identity | Verified by the wrapper: server `Shady`, database `NexusPmo`, database ID `5` |
| Authentication | Windows Integrated Security configured and SQL connection opened |
| Database history/schema inspection | Failed with `SqlException: Execution Timeout Expired` after target identity was reported |
| Applied/pending migration count and names | Unknown; the inspection did not complete |
| Physical schema verification | Not verified |
| Migration application | None |
| Seed mode/idempotency | Not run |
| Backup | Not requested; Development backup is optional under repository policy |
| Persistence tests | Not run |

The sanitized reports supplied with the run are `artifacts/ef-review/dev-db-preflight-model-20261113T130458Z-f4c5f0e1.json` and `artifacts/ef-review/dev-db-preflight-inspect-20261113T130536Z-9eadea72.json`. The model-only success is not database/schema verification. No second SQL connection attempt was made from the isolated agent execution context.

## Project and ownership evidence

The Product Owner identified the target Project as:

| Field | Supplied value |
|---|---|
| Public ID | `dc99079f-0a9c-4045-aacf-9a3f7a7f3afa` |
| Internal ID | `7abb23ff-07c8-4695-82fe-a885fe1b2225` |
| Tenant ID | `11111111-1111-1111-1111-111111111111` |
| Development database | `Shady` / `NexusPmo` / ID `5` |

The latest row supplied by the Product Owner was counted as **38 tab-separated values**. Under the prior 40-column query's assumed ordering, the first values appear to represent Project ID, Tenant ID, `IsDeleted = 0`, `ProgramId = NULL`, `PortfolioId = 4F7A5AEE-995D-48C9-90C1-F321FB743F70`, and `DepartmentId = AF339983-CDC8-4431-92EF-2354AA63BA60`. The apparent direct Portfolio row has the same Tenant, is not deleted, and has `OrganizationId = NULL`. The projected Department, Business Unit, and Organization join fields are null in the pasted portion. This is a **conditional reading**, not an independently verified query result: no column headers were included, and the final two expected values were absent.

| Ownership check | Evidence/status |
|---|---|
| Project exists, tenant, deleted state, parent FKs | User-supplied row appears consistent with the values above; not independently re-queried |
| Direct Portfolio identity, tenant, deleted state, OrganizationId | User-supplied row appears to show same-tenant, non-deleted Portfolio with null `OrganizationId`; column alignment remains unverified |
| Department identity and tenant/deleted/active state | Not independently confirmed; the row appears to have no matching joined Department |
| Department → Business Unit → Organization | Not independently confirmed; the supplied join values appear null and the row is short by two values |
| Program ownership path | `ProgramId` appears null under the assumed ordering; no Program path is indicated by this row |
| Valid same-tenant active Organization candidates | Not provided or queried |
| Other Projects/Portfolios with the same pattern | Not queried; prevalence is unknown |

Accordingly, the ownership chain is not declared confirmed as the runtime cause. The minimum next evidence is a fresh, header-labelled read-only result from the authorized Windows Development host containing every Project, Program, direct/Program Portfolio, Department, Business Unit, and Organization identity, Tenant, soft-delete, and active-state column. Include the same-tenant active Organization candidates and a read-only census of Projects/Portfolios with unresolved ownership. If more than one Organization is supported, the Product Owner must select the intended one before any correction.

## Approved ownership rules and source review

- The approved Project hierarchy supports either a direct Portfolio assignment or a Project assigned through a Program in that Program's Portfolio. When a Program is selected, its Portfolio is authoritative and a conflicting Project Portfolio is rejected. See [Portfolio Management Core](../modules/PORTFOLIO-MANAGEMENT-CORE.md#project-integration).
- `Project` has nullable Program, Portfolio, and Department references and no `OrganizationId`. `Portfolio.OrganizationId` is nullable. Portfolio create/edit currently does not expose an Organization assignment. The reviewed specifications do not establish that every Portfolio must have an Organization.
- The current `ProjectOrganizationLookup` resolves Program → Portfolio → Organization, or direct Project → Portfolio → Organization, and only if that lookup has no Organization does it try Department → Business Unit → Organization. It filters by Tenant and deletion state and requires an active Department, Business Unit, and Organization. Department is therefore a fallback in the current resolver; the broader domain specification does not state that Department supersedes the Program/Portfolio hierarchy.
- The project-specific Lifecycle Options service requires an in-scope Project and a resolved Organization. It returns active phase options for that exact Organization; it does not use a tenant-wide phase fallback for a Project request.
- The HTTP boundary accepts external `Project.PublicId`; middleware resolves it within the authenticated Tenant and rewrites it to internal `Project.Id`. Service-level calls consume the internal ID. Source review found the expected `ManagementScope.ProjectIds` check and permission policy. No authorization, Active Deny, tenant filter, or project scope was weakened.

## Lifecycle 404 trace and runtime limits

The current source path is:

1. Authenticated `Project.PublicId` resolution in `ProjectPublicIdRoutingMiddleware`.
2. Management context and project resource scope checks.
3. `ProjectLifecycleCatalogService.GetOptionsAsync` checks permission, tenant, active Project, and `ManagementScope.ProjectIds`.
4. The service resolves the Organization from persisted ownership; a null result returns the same `The requested project was not found.` detail reported for the 404.
5. If an Organization resolves, the Project request is limited to that Organization's active configured phases.

This makes a null Organization resolution consistent with the reported response in the reviewed source, but the runtime branch was not observed. The user-provided 404 does not by itself prove the authenticated tenant, effective management scope, API build, or the exact failing predicate. No authenticated Development request was available to repeat. The GET was not replayed because its success path can call `EnsurePreInitiationPhaseAsync` and mutate the catalog.

## Changes performed

- `ProjectService.CreateAsync` now returns an actionable validation failure before Pre-Initiation provisioning or Project persistence if the candidate hierarchy cannot resolve to an active Organization in the current Tenant.
- `ProjectService.UpdateAsync` applies the same guard only when Program, Portfolio, or Department ownership changes. A non-ownership edit to an unresolved legacy Project remains possible; an unresolved reassignment does not save.
- Added `ProjectOwnershipIntegrityTests` with cases for unresolved create, unresolved reassignment, unrelated legacy edit, valid direct Portfolio ownership, valid Department → Business Unit ownership, and valid Program → Portfolio ownership.
- No resolver fallback or arbitrary Organization default was added. Portfolio/Program create/edit was not changed because a portfolio may be intentionally unassigned and their current DTOs do not provide an Organization assignment. A Project cannot now be newly created or reassigned unless the existing resolver produces its owner.
- No database repair, manual SQL mutation, migration, seed, or permission/grant change was performed. The exact Project and any linked business records remain untouched by this recovery.

## Focused checks and actual results

| Check | Result |
|---|---|
| Source review of PublicId/internal-ID conversion, scope, tenant, soft-delete, permission, and phase filters | Completed read-only; no runtime confirmation |
| Ownership prevention tests authored | Yes; six cases in `ProjectOwnershipIntegrityTests.cs` |
| Lifecycle Options PublicId/scope/active-phase HTTP and service integration tests | Present in the working tree, but not run in this recovery |
| Focused test command | `dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --filter FullyQualifiedName~ProjectOwnershipIntegrityTests --logger "console;verbosity=normal" -v normal` |
| Test execution | **Not reached.** The test-project build failed after 32.01 seconds with 0 warnings and 0 reported errors; VSTest did not discover or run the tests. A prior non-verbose attempt also exited 1 without output. |
| `git diff --check` | No whitespace errors reported; Git emitted line-ending notices for pre-existing worktree files. The new focused test file also has no trailing whitespace. |
| Full API build | Not run |
| Authenticated Development HTTP request | Not run; `RUNTIME NOT VERIFIED` |
| Browser UAT | Not run; Product Owner performs browser verification |

The test source is not evidence that the new guard passes. Resolve the test-project build blocker and run the focused tests before treating the prevention code as verified.

## Authorization, data integrity, and remaining risk

No direct Development mutation was made. No Projects, Portfolios, departments, lifecycle rows, phase history, documents, budgets, or other records were deleted or rewritten by this recovery. Because no fresh complete database query or API read was available, the live database's full relationship integrity and historical-data preservation could not be independently verified.

The prevention guard uses existing tenant-filtered ownership logic and leaves project authorization and scope checks unchanged. Its Department path is the current resolver's fallback behavior, not a newly declared domain override. Product Owner review remains necessary if the intended model should reject Department-only ownership or require every Portfolio itself to have an Organization.

## Required next steps

1. On the authorized Windows Development host, rerun the repository's read-only `scripts/dev/db-migrations-status.ps1 -NoBuild` and share its sanitized result. The supplied preflight timed out during database inspection, so applied/pending history and physical schema remain unknown.
2. On that verified `Shady` / `NexusPmo` Development target, rerun the exact ownership query with column headers and all projected values. Include same-tenant active Organization candidates and a read-only unresolved-ownership census. Do not run any `UPDATE`, `INSERT`, `DELETE`, reset, migration, or seed.
3. If the persisted graph does not establish one Organization, Product Owner selects the intended Organization. If more than one valid candidate exists, explicitly name the correct Organization ID. No correction is authorized until this decision is recorded.
4. Confirm whether the correction is supported by an existing authorized application workflow. If the intended fix requires a new repair command or direct Development mutation, present the exact proposed operation and stop for separate Product Owner approval.
5. After the data decision and any authorized correction, verify the complete ownership graph, run the focused ownership tests, correlate the authenticated request without exposing credentials, and compare the returned active phase IDs with that Organization's persisted lifecycle reference data. Keep the request's external identifier as `Project.PublicId`.
6. Product Owner then performs browser UAT for Project Settings → Change Phase and confirms the exact Project's Target Phase list.

## Related evidence

- [Prior Lifecycle Options 404 root-cause investigation](PROJECT-LIFECYCLE-OPTIONS-404-ROOT-CAUSE-INVESTIGATION.md)
- [Prior Change Phase recovery report](UAT-CHANGE-PHASE-TARGET-AND-CURRENT-DOCUMENTS-FIX.md)
- [Master UAT Checklist](MASTER-UAT-CHECKLIST.md)
- [Development database connectivity and runner](../development/development-database-connectivity-and-runner.md)
