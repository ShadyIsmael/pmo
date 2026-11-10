# Financial Sprint 2 — Payment Requests and Approval Workflow

**Report date:** 2026-10-30  
**Scope:** Project Details → Financial Tab  
**Classification:** Sprint 2 source implemented; database integration pending. The earlier partial reversal status is superseded by Sprint 4 source implementation; Sprint 4 compile/runtime verification remains pending.

> The content below the original Sprint 2 report records the implementation before Annual Budget integration. The current status and scope are updated in the latest addendum at the end of this document; historical descriptions of the reservation source are superseded there.

## Implemented at source level

- Project-scoped Payment Request list, detail, eligible-approver and milestone endpoints, plus create, draft update, submit, decision, and pre-approval cancellation commands.
- Reuses `ApprovalRequest` as the request root, `ProjectPaymentRequestDetail` for financial-specific fields, `PaymentRequestApproverAssignment` for submitted approver snapshots, `ApprovalAction` for individual decisions, and `AuditEntries` for the financial timeline.
- Creation is restricted to an active authorized Project Manager. Approver candidates are active Project Team members linked to active `Resource.UserId` and active users. Optional milestones are validated against the active project plan.
- Project currency is required and validated. Draft and Pending requests do not reserve budget. Only final Approved requests enter the derived allocation sum.
- Sequential approvals follow stored order and stop on rejection. Parallel approvals store Mandatory/Optional assignments, require at least one Mandatory approver, and finish when all Mandatory approvers approve; Optional decisions do not block completion.
- Pre-approval cancellation is supported for the requester. Direct cancellation after approval is rejected.
- Final approval rechecks `Project.Budget` less active Approved Payment Request allocations. It does not create a second reservation ledger, deduct settlements, or create `CostEntry` records.
- Financial Activity events use the shared `AuditEntries` store. The source actions are `Created`, `Edited`, `Submitted`, `ApproversAssigned`, `ApproverApproved`, `ApproverRejected`, `Approved`, `Rejected`, `BudgetReserved`, and `Cancelled`. Business and audit writes share the command transaction.
- The Project Financial Tab contains the request list, draft form, milestone selection, approval method and approver selection, sequential/parallel decision history, and permission-based actions. Existing planned-payment routes remain in place.

## Persistence, concurrency, and deployment

The active source migration is `20261029181208_ProjectPaymentRequestPersistence`. The model reuses the shared approval tables and adds the project-payment detail and approver-assignment persistence. The offline EF gate previously reported four repository migrations and `EF Model synchronized: Yes`.

Financial commands use serializable transactions and the same tenant/project-scoped SQL Server row lock (`UPDLOCK, HOLDLOCK`) on the `Projects` row. Final approval rereads the project ceiling and active approved allocations before saving the status, approval action, and audit entries. This is source review only; SQL Server concurrency behavior has not been integration-tested.

No database deployment was performed by this work. The current Development database identity, applied migration history, backup readiness, and physical schema remain unverified. The previously supplied runtime log showed SQL Server error 208, `Invalid object name 'Tenants'`, at `GET /api/auth/me`; without a verified current target, this does not establish which database or migration is responsible. Do not infer that the source migration is applied or pending from its file alone.

The workflow currently reserves against legacy `Project.Budget`. Annual Budget records remain informational; a cutover to annual-budget reservations is a separate business/deployment decision. Settlements continue to remain cash-paid facts and do not reduce the budget or create costs.

## Authorization and remaining workflow gaps

Read endpoints require `Finances.Read`; commands require `Finances.Write`, an active tenant user, and tenant/project scope. Creation validates Project Manager ownership/assignment. Decisions require the assigned active approver and `Finances.Write`.

The eligible-approver endpoint returns active Project Team users but does not filter candidates by `Finances.Write`. A selected team member without that permission cannot decide through the current API/UI. Before UAT, either eligible candidates must be filtered by the permission policy or the decision permission policy must be explicitly resolved. No permission is granted automatically.

At the time of the original Sprint 2 review, reversal linkage was prepared but Reversal Request commands, approval decisions, and reservation-release behavior were not implemented, and the release rules were unresolved. This historical status is superseded by the approved unpaid-commitment workflow in Financial Sprint 4; paid-settlement refunds/corrections remain excluded.

## Verification

| Check | Result |
|---|---|
| API build | Succeeded, 0 errors; 352 IKVM/MPXJ warnings |
| Persistence/design-time build | Succeeded, 0 errors |
| Offline EF model check | Synchronized: Yes; four repository migrations; database connection not opened |
| Frontend TypeScript and production build | Succeeded; Vite reported existing large-chunk warnings |
| Unit/API/database/browser/security tests | Not run; deferred to consolidated Sprint 7 UAT |
| SQL Server reservation concurrency | Source-reviewed only; runtime behavior not verified |
| Database migration application | Not performed; actual Development history is unverified |

## Key implementation files

- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs`
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs`
- `apps/api/src/NEXUS.Modules.CostManagement/Application/PaymentRequestWorkflowPolicy.cs`
- `apps/api/src/NEXUS.Persistence/Migrations/20261029181208_ProjectPaymentRequestPersistence.cs`
- `apps/web-react/src/features/projects/ProjectPaymentRequests.tsx`
- `apps/web-react/src/features/projects/api/project-financials.ts`
- `apps/web-react/src/features/projects/ProjectFinancialWorkspace.tsx`

## Overall status

**Core Payment Request workflow: source implemented.**  
**Reversal workflow at the original Sprint 2 review: partial / unavailable; superseded by Financial Sprint 4 source implementation.**  
**Development database integration: pending verification and authorized deployment.**  
**End-to-end status: not verified.**

## Current Sprint 2 Annual Budget Integration - 2026-10-30

> This section records the initial blocker state. Its build, migration, and environment status is superseded by the final technical closure below.

### Implementation

- New Payment Request creation derives the Fiscal Year from the authenticated Tenant's calendar using the server financial date; the client cannot choose a Tenant or Fiscal Year.
- The request detail stores nullable `ProjectAnnualBudgetId`, `AnnualBudgetFiscalYear`, and `AnnualBudgetReference` snapshot. Null association remains reserved for historical records. Composite Tenant/Project/Budget FK and index protect association scope.
- Draft creation requires a current-year Project Annual Budget. Submission requires that budget to be Approved, match the Project/Tenant, currency, and fiscal-year period, and have enough availability. An unassociated historical request cannot be submitted or finalized.
- Only final approval reserves. The authoritative formula is `Approved Annual Budget - active Approved requests linked to that budget`. Draft/Pending/Rejected/Cancelled requests do not reserve; settlement state does not reduce or release the original approved allocation.
- Existing Sequential/Parallel approval assignments, Mandatory/Optional decisions, `ApprovalRequest`, `ApprovalAction`, and `AuditEntries` remain the workflow. Final approval uses the current serializable transaction, locks Project and Annual Budget scope, recalculates allocations, and writes approval/reservation audit events in the same transaction.
- Approved unassociated legacy requests are preserved and not backfilled. Until controlled reconciliation, the service fails closed for new spending transitions that might overcommit a Fiscal Year. No automatic Annual Budget is created from `Project.Budget`.
- The final-decision path revalidates that the assigned approver remains an active Project Team member linked to an active resource user and still has `finances.write`. The availability summary clamps spendable availability at zero; it never returns a negative available balance.
- Financial Overview labels `Project.Budget` as the legacy ceiling and no longer reports a second legacy spendable balance. Payment Request list/detail and Financial Activity identify Fiscal Year and Annual Budget reference.

### Permissions

No new Payment Request permission was needed. `finances.read` remains the read permission; `finances.write` protects request create/edit/submit/cancel and assigned approval decisions. Creation additionally enforces the authorized Project Manager condition. Approval decisions also require an active eligible Project Team member linked to an active resource user and assigned to the specific request. Annual Budget approval remains separate under `ProjectAnnualBudget.Approve`. No role grants changed. Frontend permission checks and backend controller/service checks are retained.

### Persistence and deployment

At the initial report, the first no-build scaffold loaded a stale compiled model and generated empty files; those attempt-created files were removed. The valid scaffold, current snapshot check, and reviewed SQL are recorded in the final technical closure below. `dotnet --info` still reports a Service Control Manager access denial while enumerating workload installer state. Existing MSBuild diagnostics show the workload SDK resolver handling the locator SDK names, so the absent physical directories do not establish SDK corruption. The earlier EF `migrations remove` helper did not change schema or records. Actual Development identity/history, physical schema, and independent backup remain unverified.

### Verification boundary

| Check | Result |
|---|---|
| API compile | **Initial state: unverified** — The final isolated compile result is recorded in the technical closure below. |
| Persistence/design-time build | **Initial state: unverified** — The final offline build result is recorded in the technical closure below. |
| EF snapshot/migration | **Initial state: not synchronized** — The generated migration and current model check are recorded in the technical closure below. |
| Frontend TypeScript/production build | **Initial state: unverified** — The final offline results are recorded in the technical closure below. |
| Permission implementation (source review) | **PASS** — Existing `finances.read`/`finances.write` are reused; `ProjectAnnualBudget.Approve` stays separate. No new keys or automatic role grants. Runtime permission checks remain untested. |
| Financial integrity (source review) | **PASS, SOURCE ONLY** — Annual Budget association, final-approval-only reservation, scoped legacy guard, settlement separation, audit reference, and approver revalidation were reviewed. Runtime concurrency and SQL behavior remain untested. |
| Functional, API/database, browser, permission, and concurrency tests | **NOT TESTED** — Deferred to Sprint 7 consolidated UAT. |
| Database deployment/runtime | **NOT PERFORMED** — The failed remove-helper metadata read did not alter the database. No runtime tests were run. |

### Sprint 2B environment and security review - 2026-10-30

The earlier security review reported an attempted external **Visual Studio Telemetry** connection despite `DOTNET_CLI_TELEMETRY_OPTOUT=1` and npm offline mode. It did not expose the endpoint hostname/URL, originating process/PID, or payload, and it did not establish whether the attempt completed or transmitted data. No external-transmission approval was requested or granted. The later verification used the restricted local workspace, pre-restored packages, `--no-restore`, and the documented .NET CLI telemetry opt-out; it did not identify the earlier telemetry event. The observed `NU1900` messages concern the unavailable NuGet vulnerability metadata service at `https://api.nuget.org/v3/index.json`, not telemetry. Do not treat these separate observations as identifying the prior destination or payload.

### Source integrity review

Source review confirms that create derives the FY through the authenticated Tenant calendar, the composite relationship scopes the budget to Tenant/Project, submit requires an Approved matching budget, and only final approval adds the request to active approved allocations. Final approval retains the serializable transaction, project lock, and `UPDLOCK, HOLDLOCK` on the associated Annual Budget before recalculating the scoped allocation sum. Sequential/Parallel behavior and shared audit writes remain in the same workflow. Settlement tables are not included in reservation calculations, and no CostEntry is created. Legacy unassociated approved requests are only queried within the same Tenant/Project and fail closed for that affected project. No live SQL Server concurrency claim is made. The source does not currently model a formally closed Annual Budget status; enforcement against a closed period remains dependent on Year-End Closing work in Sprint 5.

### Current classification

**Current status is summarized in the final technical closure below; runtime tests remain deferred and database deployment remains pending the documented database workflow and separate authorization.**

## P0 Financial Sprint 2 Technical Closure - 2026-10-30

**Result: Sprint 2 source technical closure achieved.** API/design-time and React builds pass, the focused association migration is generated, its SQL is reviewed, and the offline EF model check is synchronized. No database command that connects to Development was run, no migration was applied, and runtime/UAT tests remain deferred.

### SDK and authorized execution path

- `global.json` pins .NET SDK `10.0.302` with `latestFeature` roll-forward; `dotnet --list-sdks` reports that x64 SDK and `dotnet msbuild -version` reports MSBuild `18.6.11`. `dotnet --info` still reports Service Control Manager access denied while enumerating workload installer state. Existing diagnostic logs show the workload SDK resolver resolving `Microsoft.NET.SDK.WorkloadAutoImportPropsLocator` as a virtual/null location and `Microsoft.NET.SDK.WorkloadManifestTargetsLocator` from the installed `10.0.100` manifest. The missing physical locator directories alone do not prove SDK corruption. No SDK files or installation settings were changed.
- Verification used the workspace's restricted network environment, pre-restored packages, `--no-restore`, and `DOTNET_CLI_TELEMETRY_OPTOUT=1`. The exact endpoint/process/payload behind the earlier Visual Studio Telemetry review remains unknown; the review did not establish that data was sent. The only observed warning tied to a network source was `NU1900` for the unreachable NuGet vulnerability metadata service. No unknown telemetry destination was approved.

### EF state

- Repository source migration count is **5**: `20261029155617_InitialFopxPmBaseline`, `20261029181208_ProjectPaymentRequestPersistence`, `20261029183557_AddTenantRegistry`, `20261030122718_AddProjectAnnualBudgetFoundation`, and `20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation`.
- EF generated the focused association migration and updated `AppDbContextModelSnapshot.cs`. Both `dotnet ef migrations has-pending-model-changes` and the repository EF Database Gate `model` action report no pending model changes; the gate reports five repository migrations and confirms it did not open a database connection. **EF Model synchronized: Yes (offline).** The gate action was invoked from its built tool directly to avoid the PowerShell wrapper's unconditional package restore.
- Incremental SQL from the Annual Budget Foundation migration is saved as [`pending-migration-20261030161223.sql`](../../scripts/database/pending-migration-20261030161223.sql), SHA-256 `B58BFC2F8A2F4BAF915DD2B2B7CF57EEB49F7462B9CCD8E26A0BCAE85E43B880`. It adds three nullable columns, the scoped alternate key/FK, index, and association check constraint. Existing request rows retain null association fields; the only `INSERT` is the migration-history row. No destructive or business-data DML was found.
- The SQL was reviewed but not applied. The Development database identity and migration history were not inspected, so the number and names of migrations pending on that database remain unknown.

### Build and policy results

| Deliverable | Status | Evidence |
|---|---|---|
| API compile | **PASS** | `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore --no-dependencies --output .tmp/api-compile -m:1 -p:NuGetAudit=false`; 0 errors. The default API output directory contains DLLs locked by another process, so the successful compile used isolated output. 360 warnings were reported, mainly IKVM multi-release-class warnings and `NU1900`. |
| Persistence/design-time compile | **PASS** | `dotnet build apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --no-restore -m:1 -p:NuGetAudit=false`; 0 errors and 7 `NU1900` warnings. |
| EF snapshot synchronization | **PASS, OFFLINE** | `dotnet ef migrations has-pending-model-changes` reports no model changes since the latest migration. |
| Association migration and SQL review | **PASS, NOT APPLIED** | Focused migration generated; incremental SQL reviewed for additive, nullable-column and scoped-key/FK/check/index operations. |
| Frontend TypeScript | **PASS** | `npm run typecheck` with npm offline mode. |
| Frontend production build | **PASS WITH WARNING** | `npm run build` succeeds; Vite reports chunks above 500 kB after minification. |
| Permission-first source review | **PASS, SOURCE ONLY** | Reuses `finances.read` and `finances.write`; `ProjectAnnualBudget.Approve` stays separate; no role grants were changed. Initial delegation authority for annual-budget approval remains an existing policy gap. |
| Financial integrity source review | **PASS, SOURCE ONLY** | See the source review above; SQL/runtime concurrency remains untested. No closed-period state exists yet; Sprint 5 closing remains a dependency. |
| Runtime, integration, browser, permission, concurrency tests | **NOT TESTED** | Deferred to consolidated Sprint 7 UAT and checklist. |
| Development database identity/history | **NOT VERIFIED** | No Development connection or migration-history query was run. Database-pending migration count/names are unknown. |
| Backup verification | **NO / NOT RUN** | No database write was planned or attempted. |
| Migrations applied | **None** | No migration was applied to any database. |
| Physical schema verification | **NO / NOT RUN** | Development target was not inspected. |
| Persistence tests | **NOT RUN** | Deferred; no runtime or UAT tests were run. |
| Database deployment | **NOT PERFORMED** | Requires verified Development identity/history, physical schema, reviewed SQL, and separate authorization. A backup is optional for Development/UAT and does not gate deployment. |

**Sprint 2 source technical closure is achieved. Database delivery and end-to-end verification remain pending; no Development database was changed.**

## Sprint 4 superseding note — unpaid commitment reversals

The historical status above describes the Sprint 2 review. Financial Sprint 4 has since added source commands and Project Financial UI for a separately approved Reversal Request. Only an approved reversal of an unpaid commitment reduces the original Annual Budget allocation. It does not alter the original request, approvals, settlement records, or create a refund. Paid-settlement reversals/corrections remain outside Sprint 4. The Sprint 4 source has not been compiled; EF migration/snapshot reconciliation and runtime UAT are deferred to Sprint 7. See [Financial Sprint 4 Reversals](FINANCIAL-SPRINT-4-PAYMENT-REQUEST-REVERSALS.md).

## Sprint 5 dependency update

The earlier report's statement that closed-period handling remained a Sprint 5 dependency is superseded by the Project closing and funding-assignment source implementation in [Sprint 5](FINANCIAL-SPRINT-5-YEAR-END-CLOSING.md). New approvals cannot use a closed originating budget; after approved carry-forward, settlements and unpaid-commitment reversals use the current funding assignment. This is source-level only. EF reconciliation, compilation, database delivery, and runtime validation remain deferred to Sprint 7A/7B; no live behavior is claimed.
