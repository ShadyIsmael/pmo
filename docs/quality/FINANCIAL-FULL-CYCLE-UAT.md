# FOPX PM — P0 Full Financial Lifecycle UAT

**Execution date:** 2026-11-04
**Status: NOT CLOSED**
**Target:** Development — SQL Server Shady, database NexusPmo (database ID 5)
**Scope:** Project Settings → Annual Budget approval → Payment Request approval and budget allocation. No external payment or disbursement was attempted.

## Acceptance status

The required live sequence was not completed. The repository EF gate verified the Development database and schema, and focused API/React regression checks passed. The user confirmed NexusPmo is the UAT database, authorized using it, authorized creating test users, and explicitly waived a backup for this UAT run. No backup was created. The VS Code embedded browser is now working with an authenticated Portfolio Manager session, but approval permissions have no persisted grants or approved first-grant path, and the current session exposes no user/role administration path.

The embedded application view showed Shady Ismael (`portfolio.manager`), one project (PRJ-1005), no assigned Project Manager, and zero team members. Its existing FY2026 Annual Budget is a Draft for £12,000 with £0 available. The Project Settings sheet showed no Annual Budget approval defaults and a read-only explanation naming the required Settings, Annual Budget, approval-configuration, and mutable-project permissions. The separate Financial Approval Settings section was not rendered; source inspection shows that section is gated by `ProjectFinancialFoundation.View`, so its absence implies the current session lacks that view permission, although the effective-permission payload was not captured. The Administration accordion had no visible child page; Governance exposed only Risks and Document Center. This project and its existing Draft were not changed. No test user, project, budget, payment request, grant, or other business record was created in this run; the browser view itself did not expose a database identity diagnostic or capture an API response.

## Database delivery gate

| Required evidence | Result |
|---|---|
| EF model synchronized | Yes. Offline model gate passed. |
| Repository migrations | 10. |
| Pending migrations | 0; none. |
| Development DB identity verified | Yes: SQL Server Shady, database NexusPmo, database ID 5. Verified by the connected EF gate against the configured Development connection and expected identity. The authenticated browser view did not independently expose its API connection identity. |
| Applied migration history | 10 of 10 repository migrations are recorded in dbo.__EFMigrationsHistory. |
| Physical schema verified | Yes. The gate reported zero objects missing from either the last-applied model or current repository model. |
| Backup verified | No. The user explicitly waived a backup for this NexusPmo UAT run; no backup was created or verified. This is recorded as a waiver, not as a remaining blocker. |
| Migrations applied in this run | None. |
| Persistence tests against Development SQL | Not run. The gate inspection was read-only; application lifecycle persistence was not exercised. |

The source and applied migration IDs were: 20261029155617_InitialFopxPmBaseline; 20261029181208_ProjectPaymentRequestPersistence; 20261029183557_AddTenantRegistry; 20261030122718_AddProjectAnnualBudgetFoundation; 20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation; 20261031000210_AddFinancialPersistenceSprints3to6; 20261031142036_AlignResourceProjectTeamsAndInvitations; 20261101114229_AddProjectAnnualBudgetApprovalConfiguration; 20261101213237_SynchronizePlanningPhysicalSchema; 20261102190318_AddStableSystemRoleIdentity.

## Step-by-step UAT evidence

| Operation | Actor and required permission | Project / UAT record IDs | API response and business status | Persisted database and financial evidence | Result |
|---|---|---|---|---|---|
| Verify Development target, migration history, and schema | Read-only gate; no application actor | No project or UAT records selected | EF gate verified Shady/NexusPmo, DB ID 5 | Model synchronized; 10 applied, 0 pending; physical schema verified; no writes | PASS — readiness inspection only |
| Check persisted financial approval grants | Read-only SQL inspection | No project selected | No authenticated API call | Role grants / direct overrides: ProjectAnnualBudget.View 3/0; ProjectAnnualBudget.ManageApprovalConfiguration 0/0; ProjectAnnualBudget.Approve 0/0; ProjectAnnualBudget.SubmitForReview 2/0; ProjectApproval.Override 0/0; ProjectPaymentRequest.View 3/0; ProjectPaymentRequest.Create 2/0; ProjectPaymentRequest.Submit 3/0; ProjectPaymentRequest.Approve 0/0. These are persisted grant counts, not effective project-scoped authorization results. | BLOCKED — no eligible Annual Budget or Payment Request approver is provisioned |
| Open the Development application | Authenticated VS Code embedded browser; Shady Ismael (`portfolio.manager`) | Project GUID `3572f28b-2fa1-4a16-a511-ab7a9da2b601` / PRJ-1005 | Project overview and Financials pages loaded in the embedded browser. No API response or database identity diagnostic was captured. | No database change | PARTIAL — authenticated UI observed |
| Select a suitable project and verify manager/effective permissions | Authenticated project manager; Project scope and applicable project permissions | PRJ-1005 was the only visible project | UI showed no assigned Project Manager and zero team members. Existing FY2026 budget is Draft (£12,000) with £0 available. No API permission decision was captured. | Project is unsuitable for this lifecycle and its existing Draft was preserved | BLOCKED |
| Create authorized UAT users and configure delegation | Authenticated same-tenant administrator through Roles & Permissions | No UAT user IDs | No user/role administration request was made; Administration had no visible child page and Governance exposed only Risks and Document Center. | The user authorized test-user creation and waived the backup requirement. The current session has no visible user/role administration path. Existing first-grant delegation excludes ProjectAnnualBudget.Approve and ProjectPaymentRequest.Approve; direct overrides require the grantor to already hold each key. No permission was granted. | BLOCKED |
| Save Financial Approval Method and Annual Budget defaults | Settings actor: ProjectSettings.View, ProjectSettings.Edit, ProjectFinancialFoundation.View / ProjectAnnualBudget.View, and ProjectAnnualBudget.ManageApprovalConfiguration | PRJ-1005; no settings row version | Project Settings opened in the authenticated UI. | Annual Budget defaults were unconfigured; the UI showed a read-only reason requiring Settings View/Edit, Annual Budget View, approval-configuration permission, and a mutable project context. The Financial Approval Settings section did not render; source gating makes missing ProjectFinancialFoundation.View the likely cause (inference; no effective-permission payload captured). No Finance method, reviewer, or order was written. | BLOCKED |
| Create and submit Annual Budget | Creator: ProjectAnnualBudget.Create and project scope; submitter: ProjectAnnualBudget.SubmitForReview | No new budget ID; existing PRJ-1005 FY2026 Draft `PRJ-1005-FY2026-AB-69f44ca7cc4b4e2d9b378ddf225091a5` was observed and preserved | No create/submit request was made | No new Draft, approval request, assignment, audit event, or plan snapshot was created in this run | NOT RUN |
| Approve Annual Budget | Assigned active Project Team reviewer with ProjectAnnualBudget.Approve; no self-approval | No budget or approval-request ID | No review request was made | The permission has 0 role grants and 0 direct overrides; no final Approved state or immutable plan snapshot was produced | BLOCKED |
| Create and submit Payment Request | Authorized current Project Manager with ProjectPaymentRequest.Create; submitter with ProjectPaymentRequest.Submit | No request ID | No create/submit request was made | No request, allocation, or audit row was written | NOT RUN |
| Approve Payment Request and verify availability | Exact assigned active Project Team reviewer with ProjectPaymentRequest.Approve; no self-approval | No request or approval-request ID | No review request was made | The permission has 0 role grants and 0 direct overrides. Before/after budget calculations, allocation reservation, remaining available budget, reload persistence, and audit trail were not measured. | BLOCKED |
| Negative/security scenarios | Authorized and unauthorized scoped actors, including Active Deny and cross-project cases | No test records | No authenticated negative HTTP requests were made | No Active Deny, cross-scope, duplicate approval/allocation, concurrency, or unauthorized no-mutation behavior was exercised against Development | NOT RUN — source and isolated test coverage only |

## Build and regression results

| Check | Result |
|---|---|
| EF model gate | Passed; synchronized, 10 repository migrations. |
| EF read-only Development inspect | Passed; target identity, 10/10 history, and physical schema verified. |
| Focused API regressions | Passed 35/35: ProjectSettingsAuthorizationTests, FinancialPermissionDelegationTests, FinancialApprovalDelegationBoundaryTests, and PaymentRequestWorkflowPolicyTests. |
| Focused React regressions | Passed 31/31 across four project settings, permissions, navigation, and workflow parity files. |
| React production build | Passed after correcting the test fixture's empty-array type to string[]. Vite reported an existing dynamic/static import overlap and large output chunks. |
| API/test project compilation | Passed as part of the focused dotnet test run; compiler/analyzer warnings were emitted in existing tests. |
| Additional payment-obligation regression run | 41/52 passed; 11 failed in PaymentObligationLifecycleTests. Failures included SQLite syntax near max, NullReferenceException, and assertion/setup failures. This suite covers the separate planned-payment/settlement obligation path, not the requested ProjectPaymentRequest approval lifecycle; it was not changed here. |
| Development SQL persistence tests | Not run. |

The React fixture correction changes only the inferred TypeScript type of validationIssues; it does not change runtime application behavior.

## Remaining blockers

1. The live database has no ProjectAnnualBudget.Approve or ProjectPaymentRequest.Approve grants. The existing first-grant policy excludes both keys, and direct user overrides require the grantor to already hold each key. An approved, auditable authorization path or a grantor who already holds each permission is needed; no Admin or Project Manager grant was added.
2. The current Portfolio Manager session exposes no user/role administration path, so the authorized test users could not be created through the application. A suitably authorized same-tenant administrator is needed to provision users through the auditable UI.
3. No suitable project or UAT record was used or created. The only visible project has no Project Manager or team members and already contains a Draft budget, so no financial before/after calculation, approval audit, allocation, or reload persistence evidence was produced.

Until these blockers are resolved and the full business sequence is repeated with actual Development SQL persistence, the P0 financial lifecycle remains NOT CLOSED.

## Documentation reviewed

AGENTS.md; docs/project/README.md; docs/development/local-setup.md; docs/security/permission-audit/PERMISSION-GAP-REPORT.md; docs/security/PROJECT-SETTINGS-PERMISSION-MATRIX.md; docs/security/permission-audit/PROJECT-SETTINGS-DATABASE-RUNTIME-CLOSURE-2026-11-04.md; docs/quality/MASTER-UAT-CHECKLIST.md; docs/modules/FINANCIAL-CURRENT-STATE-AUDIT.md; docs/modules/PROJECT-ANNUAL-BUDGET-FOUNDATION.md; docs/modules/FINANCIAL-PROJECT-LIFETIME-CEILING-ANNUAL-BUDGET-LIFECYCLE.md; docs/modules/FINANCIAL-MANAGEMENT-PLANNED-PAYMENTS.md; docs/modules/FINANCIAL-TARGET-BUSINESS-MODEL.md; docs/development/ef-core-database-workflow.md; the documentation-first-task-execution, EF Core, and frontend-data-integrity skills.
