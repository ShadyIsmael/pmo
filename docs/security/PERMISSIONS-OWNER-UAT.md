# Permission Architecture Owner UAT Checklist

**Purpose:** Verify the implemented permission architecture through browser and end-to-end scenarios. Browser UAT remains outstanding.

## Current readiness status (2026-11-02)

- Current source catalog has 154 definitions, 136 active keys (34 View, 102 Action), and 18 retained inactive entries. The dynamically derived protected Admin source baseline has 46 permissions, including eligible tenant Reference Data Administration entries.
- Focused API catalog/Admin protection tests passed 16/16, protected Admin seed/reconciliation tests 10/10, Reference Data category/Project Role service tests 14/14, and Reference Data/Project Finance UI tests 30/30. The React production build did not complete in this continuation.
- Current SQL Server inspection could not connect to `localhost` or `Shady`; current database identity, migration state, backup, and persisted grants were not verified. No database write or migration was made. The earlier 43-key baseline/reconciliation figures in the report are historical and do not verify the new 46-key baseline.
- Browser runtime/UAT was not performed. Keep the UI and end-to-end scenarios below open until exercised in the application.

## Entry gates

- [ ] Reconfirm actual Development database identity, tenant, backup, and migration history before database UAT; do not infer identity from a hostname or configuration filename.
- [ ] Capture read-only before-state counts and exact IDs for Roles, RolePermissions, UserRoles, direct user overrides, Permission rows, and effective permissions by test actor.
- [x] Verify no Production target is selected.
- [ ] Complete any required builds; focused continuation tests passed, while the React production build remains unverified.
- [x] Ensure no existing role grants were changed by startup seeding; the Admin grant delta was made only by the explicit audited reconciliation.

## Catalog and role editor

- [ ] Compare all 154 catalog definitions to the final CSV; confirm 136 active keys (34 View, 102 Action) and 18 retained inactive compatibility definitions.
- [ ] Confirm each active key appears once under one Module → Screen; search by English label, Arabic label, and key.
- [ ] Save an Action and confirm its required View is selected only in the draft and persisted only when explicitly saved.
- [ ] Remove a View with dependent Actions; confirm the dialog and removal apply only to the unsaved draft until save.
- [ ] Attempt an invalid Action-without-View configuration through the API; expect rejection and no data change.
- [ ] Verify unauthorized keys cannot be added by editing the request payload; test self-grant and delegation boundary denials.
- [ ] Save/reload a role and compare selected permission IDs; verify inactive legacy grants are neither offered as active grants nor silently deleted.
- [ ] Check EN/AR labels, RTL layout, empty/loading/error states, indeterminate counts, responsive layout, and light/dark mode.

### Reference Data permissions

- [ ] Confirm `ReferenceData.View`, Create, Edit, and Delete appear once under Administration → Reference Data; ordinary roles can save each operation independently.
- [ ] Confirm protected tenant Admin has all active Tenant-scoped Administration permissions, including ReferenceData Create/Edit/Delete, which remain locked and cannot be denied or removed. Confirm Platform-only data privileges are absent.
- [ ] Verify unauthorized create/edit/delete calls are rejected; cross-tenant IDs and Platform-managed records cannot be modified by a tenant actor.
- [ ] Verify used/immutable values cannot be deleted, soft-delete/deactivation preserves audit history and references, and currency choices come from approved system data.
- [ ] Confirm no Import/Export permission is shown unless a supported operation exists; check EN/AR, RTL, themes, and responsive role editor behavior.

## Tenant, organization, and management scope

- [ ] Confirm tenant administrator authority remains within its tenant and no role-name `admin` or `pmo.admin` shortcut creates global scope.
- [ ] Confirm Organization boundaries are enforced independently from Portfolio/Program/Project assignment.
- [ ] Verify Portfolio, Program, Project, Resource, and Organization queries remain scoped even when the user has the relevant View key.
- [ ] Verify tenant users cannot invoke platform settings policies.

## User and role administration

- [ ] An active same-tenant user with effective `TenantAdministration.ManageScope` can open Roles & Permissions and edit role permission assignments even without `Roles.ConfigurePermissions`; the permission list remains capped at that user's effective grants.
- [ ] After a permitted grant change, reload the role and the affected user's `/api/auth/me` permissions; confirm the new effective key appears and a subsequent API request uses it without waiting for token expiry.
- [ ] Tenant scope alone cannot create/delete roles, edit role details, assign user roles, or change direct user overrides. Direct API attempts are denied.
- [ ] A Tenant Administrator cannot add a permission outside their own effective set, cannot change another tenant's role, and cannot remove the last valid role-management capability from their own account.
- [ ] A user with only `users.write` cannot open the role permission editor or use role permission APIs; `Roles.Manage` is inactive and is absent from effective claims.
- [ ] For the existing Admin regression, capture the before-state and verify any approved recovery adds only the missing `Roles.View`, `Roles.Create`, `Roles.EditDetails`, and `Roles.ConfigurePermissions` keys needed by the requested controls; preserve all other `RolePermissions` and `UserOverrides` and record an audit event. Add `Roles.Delete` only under separate authorization; do not use `users.write` as a runtime bypass.
- [ ] A direct `Deny` override for `TenantAdministration.ManageScope` removes the role-administration bridge immediately; confirm the screen and API both deny access.
- [ ] `Users.ManageAccounts` can create accounts and edit profile fields, but cannot change activation state.
- [ ] `Users.SetStatus` can activate/deactivate another tenant user and writes the correct before/after audit record; self-status changes fail.
- [ ] `Users.AssignRoles` can change role assignments only within the actor's effective delegation boundary; profile fields stay read-only for role-only administrators.
- [ ] `UserPermissions.Manage` is required for direct Allow/Deny overrides and cannot add permissions beyond actor authority.
- [ ] `users.write` compatibility does not authorize role assignment, direct overrides, password resets, or status changes.

## Program and Portfolio operations

- [ ] Native Portfolio Manager with `Program.Create` can create only under eligible assigned active Portfolios.
- [ ] With explicit `Program.CreateAcrossPortfolios`, user can create under other eligible active Portfolios only within authorized Tenant/Organization scope.
- [ ] The eligible-Portfolio GET and create POST return/enforce the same target policy.
- [ ] Cross-tenant and cross-organization creates fail.
- [ ] Create/Edit/Archive/Reactivate operations require their distinct keys; a legacy `Program.Manage` or `Portfolio.Manage` grant does not confer the newly separated sensitive capabilities.

## Financial workflows and CR-only Project ceiling

- [ ] Finance shell View/Manage does not grant any child data View; a shell-only user triggers no financial data request.
- [ ] Each Finance section has independent View/Manage grants, and each Manage key requires its matching View without granting sibling sections.
- [ ] Annual Budget Manage supports draft create/edit/submit without Approve, revision, approval override, or configuration management.
- [ ] Payment Request Manage supports ordinary draft operations without approval, settlement recording, or reversal authority.
- [ ] Settlement Manage does not grant `PaymentSettlement.Record` or reversal actions; Fiscal Year Closing Manage does not grant CloseApprove or CarryForward.
- [ ] Financial Audit Trail has View only and remains immutable. Assigned reviewers can access only their assigned records without general Finance View/Manage.
- [ ] Verify `ProjectFinance.View/Manage`, each section View/Manage, and the audit View labels/descriptions in EN/AR and RTL.

- [ ] Project Edit with a changed lifetime ceiling returns a validation/denial result and leaves the Project unchanged.
- [ ] Creating, editing, or submitting a ceiling-changing CR does not change the Project ceiling.
- [ ] A non-final CR approval step does not apply the ceiling change.
- [ ] Final CR approval without `ProjectFinancialCeiling.ApproveChange` fails without applying the ceiling.
- [ ] Final CR approval requires `ChangeRequest.Approve`, the ceiling qualifier, exact assigned reviewer, scope, separation of duties, and valid workflow state.
- [ ] Stale current-ceiling value, annual allocations over the proposed ceiling, commitments/actuals beyond the proposed ceiling, currency mismatch, or invalid amount blocks final approval.
- [ ] Successful final approval updates the Project ceiling and audit record in one transaction.
- [ ] No legacy/non-workflow CR approval path can update the ceiling.
- [ ] Annual Budget preparer cannot approve using `finances.write`; reviewer-only user can open only assigned review records.
- [ ] Payment Request approval is independent of `finances.write`; assigned reviewer sees no broad request list without View.
- [ ] Assigned Payment Request reviewer with `ProjectPaymentRequest.Approve` but no `ProjectPaymentRequest.View` can open only the exact assigned review URL and does not load request list/history APIs.
- [ ] Assigned reversal reviewer with `ProjectPaymentReversal.Approve` but no Payment Request View can open only the exact assigned reversal review URL; unrelated and unassigned request/reversal IDs are denied or not found.
- [ ] Legacy Budget Revision approve/reject is denied to a `finances.write`-only actor, requires `BudgetRevision.Approve` plus its View dependency, remains within Project/Portfolio scope, and is not automatically seeded to `admin` or `pmo.admin`.
- [ ] Verify the legacy Budget Revision workflow has no per-reviewer assignment model; do not treat `BudgetRevision.Approve` as assigned-reviewer access.
- [ ] Settlement, reversal create, reversal approval, closing, closing approval, and carry-forward remain independently authorized.
- [ ] `PaymentSettlement.Record` alone exposes the record-settlement operation to a scoped actor; `finances.write` alone does not expose or authorize settlement recording.
- [ ] Project Settings shows persistent approval configuration to `ProjectAnnualBudget.View`; only `ProjectAnnualBudget.ManageApprovalConfiguration` with writable management scope can save it.
- [ ] Persistent Project approval configuration is reachable at `/projects/{id}/settings`, not embedded in Payment Requests; one-submission overrides do not change these defaults.
- [ ] A user with only `Audit.View` cannot load the financial activity panel; `finances.read`/legacy `finances.write` and Project scope are required consistently with the API policy.
- [ ] Approval-route override applies to one submission and never writes persistent Project approval defaults.

## Reports, dashboard, and route/API parity

- [ ] Reports screen and Reports read APIs both require `reports.read` and return only tenant/resource-scoped rows.
- [ ] Dashboard route/navigation require `Dashboard.View`; widget APIs still enforce their own permissions.
- [ ] Direct URL navigation and direct API calls receive the same denial as hidden UI controls.
- [ ] View-only actors cannot perform Actions; API responses are 403 where authenticated but unauthorized.
- [ ] Confirm resource, team, user, settings, finance, audit, directory, plugin, and AI mapping cases from `PERMISSIONS-CANONICAL-API-MAPPING.md` against live routes and APIs.

## After-state and sign-off

- [ ] Compare role/grant/override/user-assignment before and after; explain every difference and verify no sensitive automatic grant.
- [ ] Verify effective permissions after sign-out/in or documented cache invalidation.
- [ ] Verify audit events preserve actor, tenant, target, operation, outcome, and before/after where applicable.
- [ ] Record build/test outputs, exact Development DB identity, migration state, runtime scenario results, and unresolved issues in the implementation report.
