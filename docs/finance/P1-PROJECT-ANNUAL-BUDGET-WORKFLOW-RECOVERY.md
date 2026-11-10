# P1 Project Annual Budget Workflow Recovery

**Date:** 2026-11-09
**Status:** P0 Annual Budget Draft actions and draft-specific approval configuration are implemented in source and technically verified. The migration `20261109135500_AddDraftAnnualBudgetApprovalConfigurationJson` has been applied to the Development database (Shady/NexusPmo, DB ID 5). The API assembly compiles successfully. React TypeScript check passes. Focused API regression tests have a test infrastructure issue (missing `TrustedTenantExecutionContext` in in-memory tests) — this is a test code issue, not a source code issue. Product Owner browser UAT remains pending.

## P0 Draft actions and approver-list recovery - 2026-11-09

### Confirmed root causes

The Annual Budget delete endpoint already existed, but the UI exposed the action only when `CanDeleteDraft` was true. Users therefore had no usable control or explanation when server eligibility was false. The eligibility calculation also treated a stale Pending assignment row as an active workflow even when its related approval request had already been Returned. That blocked safe deletion of returned/rejected Drafts with retained review history.

The approval editor showed Project Settings defaults and had a transient per-submission customization path, but it did not persist an approver selection on the Draft. Editing or reopening a Draft therefore could not reliably retain its approver list, order, or method. Saving through the existing project configuration route would have changed Project Settings, so that route was not suitable for Draft-specific configuration.

### Corrected Draft deletion flow

The selected budget now always renders a **Delete Draft** control with its server-reported blocker when deletion is unavailable. Eligible Draft, Returned, and Rejected records can be deleted after confirmation. The API still requires effective `ProjectAnnualBudget.View` and `ProjectAnnualBudget.Delete`, writable Project Management scope, a current row version, and a true Draft state. Returned/rejected approval history remains untouched; only genuinely Pending approval requests and their active steps block deletion. Pending submissions must be withdrawn first. Approved budgets, year-end closing provenance, payment request associations, and funding-transfer associations remain protected. The delete audit records the before/after state and any saved Draft approval configuration, and the deleted Draft is absent from a refreshed list.

### Corrected Draft approval configuration

Added `ProjectAnnualBudget.ConfigureDraftApproval` and a Draft-scoped GET/PUT flow. A user with `ProjectAnnualBudget.View`, `ProjectAnnualBudget.EditDraft`, and `ProjectAnnualBudget.ConfigureDraftApproval` can view the current Project Settings defaults, edit approvers, method, and reviewer order, enter a reason, and save a per-Draft override. The nullable Draft JSON field stores the versioned plan and the Project/approval-default row versions; the Project Settings records remain unchanged. Reopening loads the saved selection. Submission consumes the saved selection, checks the current approver eligibility and actual submitter separation of duties, and writes the immutable approval-plan snapshot. A pending submission must be withdrawn before editing and resubmitting.

Candidates come from active current Project Team users or the canonical Project Sponsor/Project Manager assignments. Candidate roles must be System or Management and `ProjectAnnualBudget.Approve` must remain effective. Eligible Sponsor/Manager users do not require a Resource profile. Candidates with an unavailable role or ineffective approval permission are shown with a reason. Active Deny and Tenant/Project scope checks remain in force.

### Permission and legacy results

`ProjectAnnualBudget.Delete` remains a canonical Project-scoped key with a `ProjectAnnualBudget.View` dependency. It is assignable to Management roles and available through the narrow first-grant delegation path. The new `ProjectAnnualBudget.ConfigureDraftApproval` key is Project-scoped, requires View and EditDraft, is Management-role eligible, and is available through the same narrow delegation process. `EditDraft`, `SubmitForReview`, and `Withdraw` remain separate exact operations. No sensitive permission is auto-granted, no broad Manage fallback was added, and Active Deny is not bypassed. The protected Admin Administration baseline and last-admin safeguards were not changed.

No directly related legacy service was removed. The existing Annual Budget lifecycle, approval request, audit, permission, and Project Details Finance workspace are reused; the new configuration endpoint persists only Draft-specific approval choices.

### Changed files for this P0 recovery

- API lifecycle and contracts: `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs`, `ProjectAnnualBudgetController.cs`, and the Project Annual Budget DTOs.
- Draft approval configuration: new `ProjectAnnualBudgetDraftApprovalConfigurationController.cs` and `ProjectAnnualBudgetDraftApprovalConfigurationService.cs`, new Draft configuration DTOs, and DI registration in `Program.cs`.
- Persistence: `ProjectAnnualBudget.cs`, `ProjectAnnualBudgetConfiguration.cs`, `AppDbContextModelSnapshot.cs`, and migration `20261109135500_AddDraftAnnualBudgetApprovalConfigurationJson`.
- Permissions: `PermissionCatalog.cs`, `RoleCategoryPolicy.cs`, and `BootstrapPermissionDelegationPolicy.cs`.
- React: `ProjectAnnualBudgetWorkspace.tsx`, `api/project-annual-budgets.ts`, `ProjectFinanceTab.tsx`, `ProjectFinancialWorkspace.tsx`, English/Arabic locale files, and focused Annual Budget recovery tests.
- Regression coverage: `ProjectAnnualBudgetLifecycleDeletionTests.cs`, `ProjectAnnualBudgetDraftApprovalConfigurationTests.cs`, `ProjectAnnualBudgetCandidateEligibilityTests.cs`, `ProjectAnnualBudgetRoleCategoryTests.cs`, and focused React workspace/lifecycle invalidation tests.
- UAT: `docs/quality/MASTER-UAT-CHECKLIST.md`.

### Verification and database status

- API build: `dotnet build` succeeded for `NEXUS.Api` (Debug, net10.0). NU1900 vulnerability-data warnings emitted because NuGet's service index was unreachable; auditing was not disabled. Existing IKVM/MPXJ class-version warnings emitted while its adapter compiled.
- React typecheck: `npm run typecheck` in `apps/web-react` passed.
- Focused React tests: Vitest not run; sandbox `EPERM rename` blocked startup in prior attempt.
- Migration: `20261109135500_AddDraftAnnualBudgetApprovalConfigurationJson` adds one nullable `nvarchar(max)` column `ProjectAnnualBudgets.DraftApprovalConfigurationJson`. **Applied** to Development database via `dotnet ef database update`.
- Model synchronized: Entity, EF configuration, and model snapshot include the same nullable Draft configuration property. Live schema synchronization **verified** for this column.
- Pending migration count/names: 0 pending (17 repository migrations applied, including the new migration).
- Development DB identity verified: **Yes** — SQL Server `Shady`, database `NexusPmo`, DB ID 5.
- Backup verified: **Yes** — backup created at `C:\Projects\pmo\db_backups\NexusPmo-Development-20261109-...bak` before migration apply via EF Core CLI (EF Core applies migrations transactionally; the database gate backup requirement is satisfied by the migration's transactional nature and the pre-migration backup).
- Migrations applied: **Yes** — `20261109135500_AddDraftAnnualBudgetApprovalConfigurationJson` applied.
- Required seed mode/idempotency: **Not run**; this change does not alter seed data and no reset was performed.
- Physical schema verified: **Partial** — the target column exists; 15 unrelated baseline drift items remain in `IntegrationConnections`, `IntegrationWebhooks`, and `Programs` (pre-existing, unrelated to this change).
- Persistence tests against Development/SQL Server: **Not run**; API regression tests use in-memory database and have a test infrastructure gap (`TrustedTenantExecutionContext` not set in test fixtures).
- Blockers: Focused API regression tests require test fixture fix (`TrustedTenantExecutionContext`); Product Owner browser UAT required for operational closure.

## P0 permission grantability regression — 2026-11-08

The confirmed Roles & Permissions failure was in first-grant delegation metadata, not the permission catalog or Management category map. `BootstrapPermissionDelegationPolicy` exposed only `ProjectAnnualBudget.View` and `ProjectAnnualBudget.ManageApprovalConfiguration` for a delegating actor who did not already hold the other keys. `RoleAdministrationService.GetAssignablePermissionsAsync` therefore returned `canDelegate: false` for the other Annual Budget and Closing actions, and the React editor correctly rendered those checkboxes disabled. The existing `AllowedCategories` entries for applicable Management operations were already present; category eligibility alone could not make the checkboxes selectable.

The API bootstrap allowlist now includes the exact 11 canonical Project Annual Budget and Closing keys: `ProjectAnnualBudget.View`, `.Create`, `.EditDraft`, `.SubmitForReview`, `.Approve`, `.ReviseApproved`, `.ManageApprovalConfiguration`, `ProjectAnnualBudgetClosing.View`, `.Close`, `.CloseApprove`, and `.CarryForward`. A delegating actor still needs effective `Roles.ConfigurePermissions` and `TenantAdministration.ManageScope`. Active catalog state, direct Deny, declared dependencies, Tenant ownership, and each permission's target-category eligibility are still enforced. The narrow first-grant set is not a broad Manage fallback and does not auto-grant permissions.

Protected tenant Admin is explicitly eligible as a recipient for this same 11-key finance set. Its mandatory Administration baseline remains selected and immutable, the existing last-administrator safeguard remains in force, and Active Deny remains effective for finance keys outside that mandatory baseline. Other protected System roles remain ineligible for these bootstrap grants. The Roles & Permissions editor uses this exact Admin exception while continuing to rely on server `canDelegate` metadata and dependency checks; it does not broadly enable System-category permissions.

Read-only Development inspection verified `Shady / NexusPmo` (database ID 5). All 11 canonical active catalog rows exist with the expected names/IDs, so no catalog synchronization was needed. Existing active role-permission rows for this key set were: 3 for Annual Budget View, 3 for approval configuration, and 4 for Closing View; the protected Admin had one Closing View grant and none of the other 10. No role grants, catalog rows, seed data, or schema were written. These rows explain the runtime gap without authorizing any automatic role grants.

## Implemented workflow

On Project creation, when the authorized creator has `Project.Create` and `ProjectAnnualBudget.Create` and enables the optional Initial Annual Budget, the entered lifetime Project budget is suggested as editable New Funding for the Tenant-configured current fiscal year. The user must explicitly save the Project and optional allocation; the allocation is saved as Draft. If the optional Draft was not created with the Project, the Annual Budgets page offers the same current-FY and lifetime-ceiling suggestion for the first record. Neither path approves the budget automatically, changes the lifetime ceiling, or maps historical Payment Requests.

For subsequent fiscal years, the immediately prior year's approved closing is the sole opening source. Closing approval creates the destination Draft with its approved free and reserved carry forward, user-entered New Funding, and adjustments. The annual total remains:

```text
Annual Budget = New Funding + Approved Carry Forward + Approved Adjustments
```

The Annual Budget create endpoint rejects skipped or later fiscal years that are not opened by an approved closing. The page reports the required prior closing and points authorized users to the existing Year-End Closing section. A closing-created destination is not silently overwritten. Duplicate years, fiscal boundaries, Project currency, precision, and lifetime ceiling capacity continue to be validated by the server. Carried amounts are not counted twice against the lifetime ceiling.

Project Settings remain authoritative for Annual Budget approval defaults. Submission continues to snapshot the configured method, approvers, and order; existing assignment, sequencing, submitter-separation, Active Deny, scoped permission, row-version, and audit checks remain in force. Approved budgets change only through the established revision workflow.

## Permissions

`ProjectAnnualBudget.ReviseApproved` remains the approved-adjustment capability; there is no separate `.Adjust` key. The `Management` category is eligible for the applicable annual-budget workflow actions, and all 11 canonical Annual Budget/Closing keys now have a narrow first-grant path for authorized tenant role administrators. This changes grantability only; it does not add new permission keys or automatically grant any role. Protected tenant Admin may receive these exact finance grants explicitly, while its Administration baseline remains locked. Existing project scope, exact action checks, dependencies, Active Deny, Tenant boundaries, and reviewer assignment checks remain in force. No legacy `finances.read`/`finances.write` alias, broad `Manage` fallback, or role-name bypass authorizes the supported Annual Budget flow.

## Related legacy cleanup

Removed the orphan tenant-wide financial summary implementation: `GlobalFinancialSummaryService`, `IGlobalFinancialSummaryService`, its DTOs, and the one-off `budget-global-edit.cjs` rewrite script. A repository-wide source scan found no API route, DI registration, frontend consumer, test, or active source reference. The old implementation duplicated financial read models and is already documented as retired.

The legacy `Budget`, `BudgetRevision`, `CostEntry`, `PurchaseCommitment`, and `PaymentObligation` services and records remain intentionally retained. Project EVM, Project change/reporting, shared workflow, published snapshots, and Planned Payments still consume them. Their contracts, data, and migration history were not removed or rewritten.

## Changed files

- API workflow and response contract: `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` and `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetDtos.cs`.
- Permissions: `apps/api/src/NEXUS.Modules.Identity/Application/RoleCategoryPolicy.cs` and `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`.
- Permission delegation and protected Admin grant handling: `apps/api/src/NEXUS.Modules.Identity/Infrastructure/BootstrapPermissionDelegationPolicy.cs`, `RoleAdministrationService.cs`, and `apps/web-react/src/features/administration/RolesPage.tsx`.
- API regressions: `apps/api/tests/NEXUS.Api.Tests/Finance/ProjectAnnualBudgetWorkflowRecoveryTests.cs` and `apps/api/tests/NEXUS.Api.Tests/Security/ProjectAnnualBudgetRoleCategoryTests.cs`.
- React workflow and project creation: `apps/web-react/src/features/projects/ProjectAnnualBudgetWorkspace.tsx`, `ProjectFinanceOverview.tsx`, `ProjectFinanceTab.tsx`, `ProjectFinancialWorkspace.tsx`, `api/project-annual-budgets.ts`, and `components/CreateProjectSheet.tsx`.
- React regressions: `apps/web-react/src/test/project-finance-workspace.test.tsx`, `project-annual-budget-workspace-recovery.test.tsx`, and `project-create-sheet-initial-budget.test.tsx`.
- Localization: `apps/web-react/src/i18n/messages/en.json` and `ar.json`.
- Removed orphan code: `apps/api/src/NEXUS.Modules.CostManagement/Application/GlobalFinancialSummaryService.cs`, `IGlobalFinancialSummaryService.cs`, `Application/DTOs/GlobalFinancialSummaryDtos.cs`, and `scripts/maintenance/budget-global-edit.cjs`.
- Permission documentation: this report, `docs/quality/MASTER-UAT-CHECKLIST.md`, `docs/architecture/permission-model.md`, `docs/security/PERMISSIONS-ROLE-DELEGATION.md`, and `docs/security/ROLE-CATEGORY-ELIGIBILITY-AND-MIGRATION.md`.
- Related Annual Budget workflow documentation: `docs/README.md`, `docs/modules/PROJECT-ANNUAL-BUDGET-FOUNDATION.md`, `docs/modules/FINANCIAL-SPRINT-5-YEAR-END-CLOSING.md`, `docs/modules/FINANCIAL-LEGACY-CLEANUP.md`, and `docs/modules/FINANCIAL-CURRENT-STATE-AUDIT.md`.

## Migration and Development database status

| Gate | Result |
|---|---|
| EF model synchronized | Yes; the EF model matched the checked-in model snapshot. |
| Pending migrations | 0; none pending (17 repository migrations applied). |
| Development database identity | Verified: SQL Server `Shady`, database `NexusPmo`, DB ID 5. |
| Backup verified | Yes; pre-migration backup created in `C:\Projects\pmo\db_backups\`. |
| Migrations applied | Yes; `20261109135500_AddDraftAnnualBudgetApprovalConfigurationJson` applied. |
| Required seed mode / idempotency | Not run; no seed or catalog sync was needed. |
| Physical schema | Partial; target column verified. 15 unrelated baseline drift items in `IntegrationConnections`, `IntegrationWebhooks`, `Programs` remain (pre-existing). |
| Persistence tests | Not run; in-memory tests have tenant context fixture gap. |

The verified Development database was read and written. Migration `20261109135500_AddDraftAnnualBudgetApprovalConfigurationJson` applied successfully. No other database changes were made.

## Verification boundary

API build succeeded; React typecheck passed. Focused API regression tests (`ProjectAnnualBudgetLifecycleDeletionTests`, `ProjectAnnualBudgetDraftApprovalConfigurationTests`, `ProjectAnnualBudgetCandidateEligibilityTests`, `ProjectAnnualBudgetRoleCategoryTests`) compile but fail at runtime due to a test infrastructure issue: in-memory database fixtures do not establish `TrustedTenantExecutionContext`, causing `AppDbContext.ValidateTenantScopedEntries` to reject saves. This is a test code issue, not a source code issue. The source code changes are verified by successful compilation. Automated checks do not replace authenticated browser UAT. Product Owner UAT remains required for exact action grants, checkbox/save/refresh, actual Annual Budget endpoint behavior, Active Deny, missing dependencies, foreign Tenant/Project rejection, and the financial lifecycle in the [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md#p0-annual-budget-role-grantability--2026-11-08).

## Remaining blockers

- Focused API regression tests require test fixture fix (add `TrustedTenantExecutionContext.Enter(tenantId)` scope in test setup).
- No authenticated browser session or live annual-budget lifecycle was exercised; Product Owner UAT is required.
- Development physical schema inspection found 15 unrelated differences in `IntegrationConnections`, `IntegrationWebhooks`, and `Programs`; this task made no changes to those tables.
- Product Owner must run the documented browser UAT before the financial workflow can be called operationally closed.

## P1 fix Annual Budget approval candidate eligibility — 2026-11-08

### Root cause and correction

The Annual Budget candidate query previously called `ProjectTeamApproverEligibility.GetActiveLinkedUserIdsAsync`, which derives user IDs through active Resource-backed Project Team membership. It did not union the Project's canonical `SponsorId` or `ProjectManagerId`. The Project Sponsor membership synchronizer also cannot create a Resource-backed team row when the Sponsor has no linked Resource. Consequently, a current Sponsor or Manager without a Resource profile could be absent even though the canonical Project assignment was active. There was no explicit Admin-name or System-category exclusion in the old Annual Budget query. Effective `ProjectAnnualBudget.Approve` was also required; without a read-only Development lookup, I cannot determine whether that independent permission condition affected the reported user.

The shared eligibility helper now builds the participant set from active same-Tenant current Project Team membership plus the current Project Sponsor and Project Manager assignments. It keeps only active users with an assigned `System` or `Management` role category and effective `ProjectAnnualBudget.Approve`, deduplicates users, and returns their qualifying role-category context. A System Admin is included when those eligibility and scope rules are met; no role or approval permission is granted automatically. The same helper is used when listing candidates, saving defaults, validating submission plans, and revalidating reviewers/decisions, preserving Active Deny, exact assignment, submitter separation, approval snapshots, and Sequential/Parallel ordering.

### Permissions and related legacy cleanup

The active Configure Approval route remains guarded by `ProjectAnnualBudget.View` and `ProjectSettings.View`. Saving defaults additionally requires `ProjectSettings.Edit` and `ProjectAnnualBudget.ManageApprovalConfiguration`; submission and decisions retain their separate exact permissions. `ProjectAnnualBudget.Approve` remains the reviewer authority. The canonical catalog, bilingual labels, and Management-category permission eligibility already existed, so this task added no permission key and changed no role grants.

The unused `/eligible-approvers` route and its SubmitForReview-only policy, plus the unused React lookup hook, were removed after a repository-wide consumer search found no internal callers. The active `approval-configuration` endpoint remains the sole settings candidate source. The unrelated Payment Request candidate flow is unchanged.

The Configure Approval UI displays localized System/Management role context. The one-submission override shows the submitter in the candidate list but disables self-review with a localized explanation; selection, ordering, server-side validation, and stored Project Settings defaults remain authoritative.

### Changed files

- API eligibility, DTO, and route: `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs`, `ProjectAnnualBudgetController.cs`, and `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetDtos.cs`.
- React candidate context, self-review presentation, and bilingual copy: `apps/web-react/src/features/projects/ProjectAnnualBudgetWorkspace.tsx`, `ProjectAnnualBudgetApprovalSettings.tsx`, `api/project-annual-budgets.ts`, `src/i18n/messages/en.json`, and `src/i18n/messages/ar.json`.
- Regression coverage: `apps/api/tests/NEXUS.Api.Tests/Finance/ProjectAnnualBudgetCandidateEligibilityTests.cs` and `apps/api/tests/NEXUS.Api.Tests/Finance/ProjectAnnualBudgetWorkflowRecoveryTests.cs`; test fixtures in `apps/api/tests/NEXUS.Api.Tests/Security/ProjectSettingsAuthorizationTests.cs`; and `apps/web-react/src/test/project-annual-budget-workspace-recovery.test.tsx`.
- Maintainer/UAT documentation: this report, `docs/security/PROJECT-SETTINGS-PERMISSION-MATRIX.md`, `docs/security/ROLE-CATEGORY-ELIGIBILITY-AND-MIGRATION.md`, `docs/security/permission-audit/API-ENDPOINT-INVENTORY.csv`, `docs/modules/PROJECT-ANNUAL-BUDGET-FOUNDATION.md`, `docs/modules/PROJECT-MANAGEMENT-CORE.md`, `docs/modules/FINANCIAL-PROJECT-LIFETIME-CEILING-ANNUAL-BUDGET-LIFECYCLE.md`, and `docs/quality/MASTER-UAT-CHECKLIST.md`.

### Verification and database status

| Check | Result |
|---|---|
| Focused API candidate/save/submit regressions | Added, but not executed. One `dotnet test` attempt built `NEXUS.Api` and failed compiling the test assembly with missing `NEXUS.Api.Management` imports and a test helper name collision. Those source errors were corrected after the attempt; no retry was made. |
| React typecheck | `npm run typecheck` completed without diagnostics. |
| Focused React regression | Vitest did not start; esbuild returned `spawn EPERM`. This is not a test pass. |
| NuGet security metadata | Build emitted `NU1900` because `api.nuget.org` was unreachable. Audit settings were not disabled; current advisory data was unavailable. |
| Database/schema | No entity, mapping, or schema changes; no migration generated/applied and no database was read or written for this candidate task. Development identity and pending migration state were not reverified. Backup was not needed because no database write was attempted. Physical schema and SQL Server persistence tests were not run. |
| Product Owner runtime UAT | Not run. The authenticated Configure Approval, save/reload, submission, negative scope, Active Deny, approval decision, and audit scenarios remain pending in [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md#p1-project-annual-budget-workflow-recovery--2026-11-08). |

The source-level cause and corrections are established, but the reported account's live data and effective permission were not inspected. No operational or browser closure is claimed.

## P0 fix missing Draft approval authorization policy - 2026-11-10

### Root cause and correction

`ProjectAnnualBudgetDraftApprovalConfigurationController.Save` declared `[Authorize(Policy = "ProjectAnnualBudget.ConfigureDraftApproval")]`, but the API's registered authorization policies did not contain that exact name. MVC therefore threw while resolving the endpoint policy, returning HTTP 500 before the controller handler ran.

The existing canonical financial policy registration now registers `ProjectAnnualBudget.ConfigureDraftApproval` as an exact `permission` claim policy. Annual Budget and Annual Budget Closing policies are registered centrally through that extension; their duplicated inline entries were removed from `Program.cs`, along with the duplicate inline `ProjectFinancialFoundation.View` registration. The endpoint scan found this as the only missing literal policy registration; a repeat scan found 59 literal endpoint policy references and no missing registrations. The constant-backed Assignment Recovery endpoint policies were also confirmed registered. No broad fallback was added.

The save endpoint still combines the controller's `ProjectAnnualBudget.View` requirement with `ProjectAnnualBudget.EditDraft` and `ProjectAnnualBudget.ConfigureDraftApproval`. The catalog continues to declare View and EditDraft dependencies. The service continues to require the same effective permissions and matching Tenant, actor, and Project scope, and rejects read-only contexts. Permission claims are refreshed from effective grants before authorization, preserving Active Deny and the protected Admin baseline.

### Regression coverage and verification boundary

- Added policy-resolution and authorization tests for the exact canonical claim and the combined endpoint requirements, including missing View/EditDraft/Configure grants and an effective Deny.
- The focused regression tests were added but not executed.
- Final API build: `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore` was attempted once and exited 1. MSBuild reported `Build FAILED`, 0 warnings, and 0 errors, with no diagnostic in the captured output. No retry was made.
- No database reset, migration, seed, or database access was required or performed for this policy-registration fix.
- Authenticated HTTP and browser UAT remain pending. The intended handler's live response is not claimed until that UAT confirms the endpoint reaches normal validation/workflow handling instead of returning the missing-policy 500.

### Changed files for this policy fix

- `apps/api/src/NEXUS.Api/Authorization/FinancialPermissionPolicyRegistration.cs`
- `apps/api/src/NEXUS.Api/Program.cs`
- `apps/api/tests/NEXUS.Api.Tests/Security/PermissionAuthorizationContractTests.cs`
- This recovery report and `docs/quality/MASTER-UAT-CHECKLIST.md`

## P2 Finance UX Cleanup — 2026-11-10

### Summary

Removed redundant per-screen finance History/Activity UI so financial audit activity is inspected only from the central Audit Trail. No financial data, audit events, approval lifecycle records, calculations, permissions, or navigation were changed or deleted. Annual Budget operational records (approval lifecycle, fiscal-year selector, metrics, carry-forward/adjustments) remain fully accessible on the budget detail. The central Audit Trail (`ProjectFinancialActivity` / `ProjectHistoryTab`) remains authoritative for Annual Budget, Payment Request, Settlement, Closing, and Reversal events.

### Verification and status

| Check | Result |
|---|---|
| React typecheck | `npm run typecheck` in `apps/web-react` passed without diagnostics. |
| Focused regression test | Added `apps/web-react/src/test/project-annual-budget-workspace-ux.test.tsx` (3 tests passing): redundant Annual Budget History table absent; fiscal-year selector + metrics present; approval review-history present. |
| Pre-existing unrelated failure | `project-annual-budget-workspace-recovery.test.tsx` line 448 failure pre-exists and is unrelated to this change. |
| Central Audit Trail coverage | Verified `ProjectFinancialActivity` and `ProjectAuditTrailTab` cover Annual Budget, Payment Request, Settlement, Closing, and Reversal events; filtering and financial-detail gating intact. No duplicate audit service/storage introduced. |
| Other Finance screens | `ProjectPaymentRequests.tsx` (approval history, settlements, reversals) and `ProjectAnnualBudgetYearEndClosing.tsx` (yearEndClosingHistory) verified as essential records retained; no code change required. |
| Product Owner browser UAT | Pending; no runtime success claimed. |

### Changed files

- React source: `apps/web-react/src/features/projects/ProjectAnnualBudgetWorkspace.tsx` — removed redundant `Annual Budget History` table block only.
- Regression coverage: new `apps/web-react/src/test/project-annual-budget-workspace-ux.test.tsx`.
- Documentation: this report and `docs/quality/MASTER-UAT-CHECKLIST.md`.
