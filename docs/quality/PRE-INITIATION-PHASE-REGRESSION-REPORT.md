# P1 — Mandatory Pre-Initiation Phase Regression

**Repository:** `C:\Projects\pmo`  
**Status:** OPEN — Product Owner UAT failed after the previous implementation. Earlier closure claims are superseded by the 2026-11-27 evidence below; the live API and Development data gates remain open.

## Confirmed source causes

1. `ProjectService.CreateAsync` created and saved projects without setting `ProjectPhaseId` or the legacy `Phase` projection. The frontend therefore rendered the project phase as unavailable even when a phase row existed.
2. `TenantProvisioningService.CreateAsync` committed the Tenant and Organization before calling phase provisioning. It swallowed every provisioning exception, and `ProjectLifecycleCatalogService.HandleOrganizationCreatedAsync` discarded the `EnsurePreInitiationPhaseAsync` failure result. A newly provisioned Organization could therefore remain without its required phase.
3. Project lifecycle options allowed tenant-wide phase fallback when a persisted Project's owner could not be resolved. The owner must instead come from the persisted Project-to-Program/Portfolio or Department-to-BusinessUnit chain; unresolved ownership now fails closed.
4. Organization-scoped phase lists and options included tenant-level phases. Those fallback rows could mask a missing Organization-owned phase and produce confusing duplicate/manual-create behavior.

## Implementation

- Project creation resolves the active owning Organization from the selected hierarchy, provisions or reuses that Organization's canonical phase, and sets both `ProjectPhaseId` and the stable legacy code before persisting the Project. Project writes and associated creation records now share a transaction. A missing owner or valid phase stops creation.
- Tenant provisioning now creates the protected phase within the Tenant/Organization transaction and rolls the whole operation back if phase provisioning returns a failure.
- `EnsurePreInitiationPhaseAsync` requires an active Organization, rejects tenant-level provisioning, repairs one existing inactive/deleted record in place, clears conflicting defaults, and retains the existing scoped unique index and serializable transaction. A process-level gate serializes same-process repair attempts.
- Lifecycle reads use only the resolved Organization phase catalog for project-scoped requests. Missing phases are repaired through the same idempotent provisioning path. Phase details on project DTOs are resolved against the actual owning Organization so EN and AR labels display correctly.
- Existing lifecycle actions continue to protect Pre-Initiation from edits, deletion, and deactivation. The canonical names remain EN `Pre-Initiation` and AR `مرحلة ما قبل البدء`; the canonical code is `pre-initiation` and its display order is zero.
- No existing Development records, database schema, migrations, seeds, credentials, or unrelated workflows have been changed.

## Regression coverage

Focused coverage includes Tenant/Organization provisioning and rollback, missing and existing phases, inactive phase recovery, Organization isolation, no tenant-level fallback, concurrent Project creation, no-owner failure, phase identity reuse, and localized Project API results. The focused test assembly builds successfully. All 14 xUnit `[Fact]` method bodies pass when invoked sequentially in-process. The regular VSTest host could not connect over its local socket channel in this sandbox, so test discovery and VSTest fixture orchestration were not exercised.

## Development data diagnostics and controlled remediation

The Agent did not connect to or modify SQL Server. Run [inspect-pre-initiation-state.sql](../../scripts/database/inspect-pre-initiation-state.sql) locally through the Product Owner's authorized Development SQL connection. The script asserts the approved `Shady` / `NexusPmo` target and performs read-only `SELECT` queries for:

- Tenant-level and Organization-level canonical-name/code rows, including inactive and soft-deleted rows.
- Every Organization, including inactive and soft-deleted Organizations, with counts for non-deleted candidates and active canonical defaults.
- Candidate aliases matching the canonical English or Arabic name even when their normalized code is noncanonical.
- Projects without a phase, with unresolved or inconsistent ownership, linked to a phase from a different scope, inactive/deleted phase references, or a legacy Phase/code mismatch. Project PublicIds are included for follow-up API reads.

No automatic record cleanup is proposed. After the diagnostic output is reviewed:

- A single inactive or soft-deleted row can be restored by the application provisioning path while retaining its ID and Project references.
- Multiple non-deleted canonical rows require a Product Owner-reviewed choice of the surviving identity after comparing Project references; do not merge or reassign them automatically.
- A tenant-level or wrong-Organization row is not reused as a fallback. Provision the correct Organization-owned phase; leave legacy rows and references unchanged until a separate, explicitly approved remediation specifies exact records and reference handling.

## Verification status

| Check | Status | Evidence |
|---|---|---|
| Backend focused tests | Pass with runner limitation | Focused test assembly builds; 14 `[Fact]` methods pass via sequential in-process invocation. VSTest IPC is blocked by sandbox local socket restrictions. Build emitted 352 IKVM dependency warnings and 0 errors. |
| Backend API build | Pass | `NEXUS.Api` built to isolated output: 0 errors, 352 IKVM dependency warnings. |
| Frontend typecheck | Pass | `apps/web-react`: `npm run typecheck`. |
| Frontend production build | Pass | `apps/web-react`: `npm run build`; Vite completed with the repository's large-chunk advisory. |
| UPI refresh and validation | Pass | Refresh confirmed the current snapshot `20261126T220221-d00a29d` (1,927 files, 15,150 symbols, 76,229 edges); validation returned 0 errors and 0 warnings. |
| Development database identity and phase inventory | Blocked before query execution | Both `sqlcmd -S Shady -d NexusPmo -E ...` and `sqlcmd -S tcp:localhost,1433 -d NexusPmo -E ...` failed to connect. The first reported Named Pipes error 5/server unavailable; the second reported socket access forbidden. The target guard and SELECTs did not execute, so no database identity or record-level findings are available. |
| Authenticated Development API behavior | Blocked before request | No listener was present on the configured API ports 5000/5001, and local socket access is restricted in this execution environment. No credentials were submitted and no API response was obtained. |
| Runtime protection/deactivation behavior | Not verified | The phase schema has no persisted protection flag; protection is enforced in application code. Verifying mutation guards through the API would require an available runtime and is not permitted by this read-only task. |
| Database records modified | No | None. |

## Runtime verification resume — 2026-11-26

The authorized read-only SQL diagnostic was attempted using both the repository's `Shady` server name and the previously established TCP endpoint. Both attempts failed before SQL execution because this environment could not open a connection. Consequently, this run confirms no Organization counts, phase uniqueness, active/protected/order state, project assignment validity, or specific bad records in the Development database. No database writes were made.

The Development launch profile configures `https://localhost:5001` and `http://localhost:5000`. Neither port had a listener. The sandbox also denied local socket access. No login request was sent; the previously verified Development Admin credentials remain unused. The authenticated verification plan, once the API is reachable, is to use `GET /api/auth/me`, then for PublicIds from the SQL report call `GET /api/projects/{PublicId}`, `GET /api/project-lifecycle/options?projectId={PublicId}`, and compare each with `GET /api/project-lifecycle/phases?organizationId={OrganizationId}&search=Pre-Initiation`. The routes are authenticated in source, but their runtime responses were not verified.

### Controlled remediation proposal (conditional; no records identified)

Use the diagnostic output to prepare a record-specific change list, then obtain Product Owner approval before any record mutation. Do not execute this proposal as part of the current read-only task:

- Missing Organization phase: provision it through the transactional, idempotent application path; list existing projects that require a separately approved phase assignment.
- One inactive/deleted candidate: restore/reuse its existing identity through the application provisioning path, preserving project references.
- Multiple live candidates or same-name aliases: stop and request a Product Owner choice of survivor after reviewing project references.
- Tenant-level or wrong-Organization references: provision the correct Organization-owned phase; do not bulk-reassign references until an approved, record-specific plan defines the survivor and affected projects.
- Legacy Phase/code mismatches: propose row-specific corrections only after reviewing each project and its history.

This resume closes neither the Development data gate nor the authenticated API gate. Source remediation and earlier build/test results are retained as prior evidence and were not rerun.

## P0 Product Owner UAT failure resume - 2026-11-27

The Product Owner reported that a newly created project still displayed **Not Available** after an API restart. This contradicts the prior implementation report. The defect is reopened; no source-level phase assignment or DTO defect was reproduced in the current source. No claim is made that the live API has been verified.

### Current source execution path

The Create Project sheet submits the selected Program, Portfolio, and/or Department ownership fields to `POST /api/projects`; it does not submit a phase ID. `ProjectController.Create` calls `ProjectService.CreateAsync`. That service resolves the owning Organization from the submitted hierarchy, provisions or reuses that Organization's canonical phase, assigns `ProjectPhaseId` and the legacy phase code, then saves the project. The response DTO contains the phase ID, code, and localized names.

After the UI navigates using the returned PublicId, the shell request passes through `ProjectPublicIdRoutingMiddleware`, which resolves that PublicId to the internal project ID. `ManagementContextMiddleware` establishes the authenticated management scope. `ProjectController.GetShell` then calls `ProjectService.GetShellAsync`; the service reloads the persisted project, resolves the owning Organization's phase, and maps the localized names to the DTO. Frontend phase rendering consumes those shell DTO fields. The Create request intentionally has no phase selection, so the backend is responsible for this assignment.

The traced current source path is internally consistent. Because the live endpoint and Development database were not reachable from this execution environment, the exact production loss point cannot be distinguished from the UAT symptom alone. In particular, a correct shell JSON with a blank UI would place the remaining defect in frontend response handling; a missing database reference would place it in creation/persistence; and a database reference with a blank shell DTO would place it in retrieval/mapping.

### Why the earlier 14 methods passed

The earlier 14 methods exercised provisioning and direct service calls. They did not invoke `ProjectController.Create`, the PublicId route middleware, management-context middleware, or shell retrieval with a fresh `DbContext`. In the direct service fixtures, no HTTP context was supplied, so `TrustedTenantExecutionContext` bypassed the production `IManagementContextAccessor` requirement. The new integration test initially exposed that missing middleware setup in its own harness; it now runs the actual management-context middleware before controller actions. This is a test-coverage gap, not evidence that the production middleware is absent.

### New create-and-reload regression

`ProjectInitialPhaseAssignmentTests.CreateEndpoint_ThenReloadShell_ReturnsPersistedOrganizationPhase` runs the real API controller methods and both production middlewares, creates with an authenticated management context, closes the creation context, and reloads through the PublicId shell route with a fresh context. It verifies the Organization-owned phase row, persisted FK and legacy code, localized DTO fields, and web-default camelCase JSON fields. The theory covers both missing-phase provisioning and reuse of an existing phase without duplication.

The focused test assembly builds with 0 errors. Both cases passed through a sequential in-process runner. Regular VSTest discovery succeeded, but its testhost failed to connect after 90 seconds in this sandbox; no VSTest result was produced. The integration fixture uses isolated SQLite and invokes controller actions/middleware directly; it does not replace verification against the running Development API or SQL Server.

### Runtime artifact finding and remaining verification

No `NEXUS.Api` process or listener on ports 5000/5001 was visible in this execution environment, and SQL Server access is restricted here. The standard `apps/api/src/NEXUS.Api/bin/Debug/net10.0` output is demonstrably older than current module build outputs:

| Assembly | Standard API output | Current module output |
|---|---|---|
| `NEXUS.Modules.ProjectManagement.dll` | Nov 26 13:33; SHA256 `00ACA799…211785` | Nov 26 20:54; SHA256 `6A859516…1F6C5` |
| `NEXUS.Persistence.dll` | Nov 26 12:59; SHA256 `1322E3DF…77E03B` | Nov 26 20:59; SHA256 `1A83F251…95EA6AD` |
| `NEXUS.SharedKernel.dll` | Nov 26 12:56; SHA256 `FE6ECFC0…8C1E03` | Nov 26 20:36; SHA256 `7EB377F4…19C26D` |

This establishes that the standard Debug output is stale relative to the module outputs, but it does not establish which output the Product Owner's restarted process loaded. A standard API build intended to synchronize the output was stopped because the API's IKVM reference compilation continued producing a long first-pass warning stream; the standard output remains unchanged. Do not treat the stale-output finding as the proven cause until the local process's loaded assembly path/hash is checked.

Run this single read-only diagnostic from the Product Owner's PowerShell session, using the PublicId of the project that displayed Not Available:

```powershell
.\scripts\development\Diagnose-PreInitiationRuntime.ps1 -ProjectPublicId '<UAT-project-PublicId>'
```

The script reports the API process and loaded assembly hash, runs the existing SQL identity-guarded `SELECT` diagnostic against `Shady / NexusPmo`, and makes an authenticated read to `GET /api/projects/{PublicId}/shell`. It prompts locally for a short-lived bearer token without saving it. Interpret its results as follows: no database `ProjectPhaseId` indicates create/persist loss; a correct database FK but blank shell fields indicates retrieval/DTO loss; correct shell JSON with the UI still blank indicates frontend resolution/rendering; a loaded assembly path/hash different from the refreshed source output indicates the API is serving another build. Do not create or mutate any database records for this check.

### Current gate status

| Acceptance check | Status | Evidence |
|---|---|---|
| Missing Organization phase is provisioned by the create path | Pass in focused integration fixture | `seedPhase: false` passed; SQL Server runtime not verified. |
| Existing Organization phase is reused without duplication | Pass in focused integration fixture | `seedPhase: true` passed; one canonical row asserted. |
| FK/code persist and survive a fresh context | Pass in focused integration fixture | New context readback asserted. |
| Shell retrieval returns localized phase and expected JSON fields | Pass in focused integration fixture | PublicId and management middleware, controller shell action, fresh-context DTO and JSON assertions. |
| Actual running API contains the current implementation | Blocked | No API process/listener visible; standard API output stale; local diagnostic script prepared. |
| Product Owner UAT reload displays the phase | Blocked | Requires the Product Owner's actual API/SQL diagnostic and UI verification. |
| Development database diagnostics | Blocked in this environment | SQL connection unavailable; no database reads or writes were made. |
| Source-level root cause of the reported production failure | Unconfirmed | Current source path and focused integration fixture pass; runtime output and project row must be checked before attribution. |
| UPI refresh and validation | Pass | Snapshot `20261127T011240-a5eb4bb`; 1,927 files, 15,156 symbols, 76,280 edges; 0 errors and 0 warnings. |

### 2026-11-27 standard API output correction attempt

The prior report did not record the isolated API output path or its four assembly hashes. No retained isolated `NEXUS.Api.dll` was found in the repository or Temp, so an exact isolated-versus-standard binary comparison is unavailable. The standard `dotnet run --no-build` runtime is `apps/api/src/NEXUS.Api/bin/Debug/net10.0`; its API assembly was also older than the API compilation output in `obj`.

| Assembly | Standard runtime timestamp / SHA256 | Latest available project output timestamp / SHA256 |
|---|---|---|
| `NEXUS.Api.dll` | Nov 26 12:59:48.0438652 / `7CFCA5540B9E6CC5878CB8C1BCEBA723182FB4C0B8704023FF9E33C23302E677` | Nov 26 21:31:32.8108750 (`obj`) / `D642FC00DDB025D27D173BD2D563993789B72A33849AE3A617CCC21BCF95CE1E` |
| `NEXUS.Modules.ProjectManagement.dll` | Nov 26 13:33:15.6800999 / `00ACA7993E8DFB4D0EABA500260ED0A287DDC1A2B8A7D822EE3A24898A211785` | Nov 26 20:54:10.2583563 / `6A859516A7967409B53776BCF585C4346D2D2C4489C7CFE33E6902B18AD1F6C5` |
| `NEXUS.Persistence.dll` | Nov 26 12:59:11.9248346 / `1322E3DFA6AB99D088A0FECE546D2CF9AE5B777DA2D3793C8BBE6394E177E03B` | Nov 26 20:59:17.9026748 / `1A83F251A1D37EEE7E8BBB8A2A1C555E9CA9D03A2FA1D7553B85662CC95EA6AD` |
| `NEXUS.SharedKernel.dll` | Nov 26 12:56:13.3612527 / `FE6ECFC0EA414D4A8455CF1370E60FC872490BDA77E57BB58892D66CDF8C1E03` | Nov 26 20:36:11.9490919 / `7EB377F4ECD1C91A465D3CBC4B371A2DB1D5D0E90F94C67A879EA58DC319C26D` |

A standard API build was attempted without tests but did not complete: IKVM reference compilation remained in its warning-heavy first pass. Direct copying the four verified assemblies into the standard runtime was attempted; Windows denied the first copy because `NEXUS.Api.dll` is in use by another process. Process enumeration/CIM access is unavailable in this execution environment. The four standard runtime files therefore remain unchanged and stale; no database operation or test was run. After the current API process releases the output, use a normal build followed by the standard `NEXUS.Api` Development launch profile, then verify all four runtime hashes against the project outputs above.
