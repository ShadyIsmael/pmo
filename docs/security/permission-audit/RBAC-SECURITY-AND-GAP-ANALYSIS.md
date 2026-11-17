# RBAC Security and Gap Analysis

> **Supersession notice — 2026-11-14:** The findings below describe the pre-implementation baseline. Current code changes and unresolved implementation/security verification gaps are recorded in `RBAC-SECURITY-AND-GAP-ANALYSIS.md`'s implementation addendum and `RBAC-IMPLEMENTATION-FINAL-REPORT.md`.

**Review date:** 2026-11-14  
**Evidence level:** Static current working-tree source/documentation review. Database and runtime state are unverified.

## Evidence labels

- **Observed source:** directly present in files reviewed in the current worktree.
- **Derived:** consequence of observed source and a confirmed Product Owner rule.
- **Missing evidence:** needs live database, runtime, or other unavailable system evidence. The original scenario source is now located in conversation Sources and is reviewed in the scenario matrix.
- The worktree was extensively dirty at review start, including Identity, seed, React, test and audit files. Findings describe that working copy and do not imply committed/deployed behavior.

## Priority findings

### G-01 — Current seeded System-role set conflicts with PO direction (S0)

**Observed source:** Seed verifier expects eight immutable SystemRoleKeys and asserts every seeded row is `IsSystem=true` (`SeedDataProvider.cs:2358-2377`). The role creation helper marks Tenant Admin, Executive, Portfolio Manager, Project Manager, Team Member, PMO Admin, Finance Controller and Resource Manager as System (`:2492-2521`). The Org Admin entry is absent from that inspected seed.  
**Derived:** Seven of the eight seeded roles are outside the approved System-role set. The source counts as seven: eight rows minus the permitted Tenant Admin; Organization Admin is not among the eight. The critical reviewer initially reported six; coordinator recount corrects that arithmetic.  
**Missing evidence:** Current persisted Development role rows were not queried. No claim is made that all eight exist in the current database.

### G-02 — Required Project Manager assignment remains blocked by classification (S0)

`UserAdministrationService.CanAssignRolesAsync` rejects unheld permission grants on System roles at `:562-565`; the possession-independent Tenant Admin branch follows only for ordinary non-System roles (`:568-573`). Seed marks Project Manager System (`SeedDataProvider.cs:2516`). This conflicts with the explicitly required Tenant Admin → Create User → Assign Project Manager success. The historical `POST /api/users` 403 is recorded in an untracked handover document, but that document says runtime/test verification is pending (`USER-CREATION-403-ROOT-CAUSE-AND-FIX.md:4-28`). Static code shows a blocking path, not the exact historical live branch.

### G-03 — Reclassification cannot be only `IsSystem=false` (S0)

`CanAssignRolesAsync` rejects non-System rows with a non-null `SystemRoleKey` (`UserAdministrationService.cs:505-509`). Domain assignment of that key requires `IsSystem=true` and prevents changing/clearing a key (`Role.cs:21-35`). SQL check constraint enforces key ⇒ System (`RoleConfiguration.cs:11-26`; migration snapshot `AppDbContextModelSnapshot.cs:2437-2463`). Seed expects old SystemRoleKey set. A migration only flipping PM's flag would fail the schema check and/or identity validation.

### G-04 — System status conflates protection, editability and assignability (S0)

Protected Admin status derives from `IsSystem` + Tenant + key `tenant.admin` (`ProtectedTenantAdminPolicy.cs:122-125`), but many adjacent policies use plain `IsSystem` for role name/category edit, permission grant rules, delete, and role assignment (`RoleAdministrationService.cs:246-269,307-314,407-412,675-699`; `UserAdministrationService.cs:562-589`). The DTO separates `IsSystem` and `IsProtectedSystemAdmin`, but no role-level Assignable or Editable policy is exposed (`RoleAdministrationDtos.cs:33-47`). React still uses `isSystem` for badge, edit/delete control and delegation branch (`RolesPage.tsx:386,450-508,562-598`).

### G-05 — Assignment-role lookup requires a different permission from the action (S1)

React enables the user role picker on `Users.AssignRoles` and fetches roles through `useAssignableRoles` (`AccessPages.tsx:68-75`; `admin.ts:110-115`). This calls `/api/roles/manage` behind `Roles.View` (`RolesController.cs:37-43`). `Users.AssignRoles` depends on `Users.View`, not `Roles.View` (`PermissionCatalog.cs:915-919`). Therefore an actor can be authorized to assign roles yet unable to load the picker. Provide an appropriately scoped minimal assignment lookup, while retaining mutation-side revalidation.

### G-06 — Direct override UI does not implement backend delegation authority (S1)

Backend supports protected Admin delegation of eligible unheld ordinary overrides and validates scope/restrictions (`UserAdministrationService.cs:739-765`). Frontend enables a control only if the actor holds the permission and disables overall save if an unheld active assignable override already exists (`AccessPages.tsx:326-328,389-413`). This creates a dead-end for removal/edit and prevents the approved possession-independent flow through the UI.

### G-07 - Organization Admin role/scope semantics missing (S0)

The role seed has no Organization Admin role. `Role`, `UserRole`, and `User` identity models are Tenant-based with no Organization role-membership column (`Role.cs:5-15`; `UserRole.cs:5-11`; `User.cs:5-20`). A sibling-Organization access failure could expose or mutate another Organization's data, meeting the provisional S0 scope/privilege boundary. Before implementation, identify whether there is another authoritative Organization membership mechanism and decide whether Organization Admin is Tenant-assigned with resource scoping or Organization-assigned. Do not infer organization authority from role category/System status.

### G-08 — Role deactivation is not modeled separately (S2)

`Role` contains no `IsActive` field; delete is soft-delete for an unassigned custom role and rejects System/assigned roles (`Role.cs:5-15`; `RoleAdministrationService.cs:402-424`). Expected behavior for disabling/restoring an assigned role is not established. Decide whether deactivation is required or soft deletion is sufficient.

### G-09 — Role assignment request uses mutable display names (S2)

Create/update DTOs submit role names and `ResolveRolesAsync` resolves them by Tenant plus display name (`UserAdministrationService.cs:452-467`). Tenant uniqueness prevents duplicate active names, but the request does not carry the stable role identity and can become stale across rename/lookup changes. This is not evidence of a current cross-Tenant authorization bypass; it is a role identity and workflow-integrity gap. Include stale selection/rename cases and consider a role-ID-based contract in an approved future design.

### G-10 — Existing direct Admin branch and current seed could mask delegation defect (S1)

Initial Tenant Admin seed includes catalog-derived active, assignable, non-reserved tenant permissions (`ProtectedTenantAdminPolicy.cs:60-74`; `SeedDataProvider.cs:2526-2531`). A standard seeded account may therefore already hold PM permissions, masking the exact “without possession” requirement. Acceptance evidence must remove/omit at least one ordinary PM permission from the Admin actor while preserving the protected Admin baseline.

## Security controls observed in source

| Control | Current source behavior | Evidence / limitation |
|---|---|---|
| Actor and Tenant context | User service derives actor/Tenant from authenticated context and rechecks during writes. | `UserAdministrationService.cs:106-123,281-288`; no runtime. |
| Full target-role grant validation | Checks each permission for tenant/deletion/catalog/activity/scope/assignability/restriction/delegation. | `UserAdministrationService.cs:470-594`; no tests run. |
| Protected Core | Dynamic Tenant Administration/baseline keys plus required Portfolio/Program/Handover entries; dependencies checked. | `ProtectedTenantAdminPolicy.cs:81-120`; live catalog rows unverified. |
| Active Deny | Deny removes ordinary allows; mandatory Protected Core reapplied for protected Tenant Admin. | `EffectivePermissionResolver.cs:13-29`; no runtime. |
| Effective-role union and Tenant filters | Inherited role grants and overrides are tenant/deletion-filtered; dependencies applied. | `UserEffectivePermissionService.cs:35-89`. |
| Self-escalation | User assignment/direct overrides and actor-held role edits have self-escalation checks. | `UserAdministrationService.cs:551-559,674-679`; `RoleAdministrationService.cs:274-282`. |
| Last Admin | Tenant mutation guard serializes writes and verifies effective Admin; change paths roll back on loss. | `TenantAdminMutationGuard.cs:22-68`; SQL concurrency not tested. |
| Reserved permission scopes | Platform/CrossTenant/SystemOwner excluded from tenant role assignment. | `UserAdministrationService.cs:532-540`. |
| Audit | Role/user/override mutation paths create audit entries. | Source reviewed, not persisted/observed at runtime. |

## Findings not promoted to defects

- A role-management action appearing in a page-access calculation without `Roles.View` was raised by a workflow reviewer. Catalog actions `Roles.Create`, `Roles.EditDetails`, `Roles.ConfigurePermissions`, and `Roles.Delete` require `Roles.View` (`PermissionCatalog.cs:1041-1063`), and effective permission dependencies are enforced (`UserEffectivePermissionService.cs:81-89`). The action-only case is therefore not a confirmed deadlock and is excluded as a defect.
- `UserPermissions.Manage` is represented in the React constants by `Permissions.Users.ManageOverrides`; the key maps to `UserPermissions.Manage` (`apps/web-react/src/lib/permissions.ts:145-146`), matching the API permission name.

## Evidence not obtained

- **Scenario source:** Sources item Pasted markdown(9).md was located and reviewed. It supplies 83 rows (39 Direct / 44 Corner) and a W/S rubric, but explicitly says enumerated, not completeness-certified, and pending PO approval. Its 12 GAP candidates remain open; the audit proposes 16 additional uniquely numbered candidates.
- **Live Development DB:** not queried. `docs/development/development-database-connectivity-and-runner.md` records that this executor cannot reach SQL and prohibits retrying from the isolated shell. No role inventory or physical schema verification is claimed.
- **Runtime 403:** no request trace/rejection reason observed. Current source logger includes reason, role classification/key, permission key/scope/assignability/activity/restriction (`UserAdministrationService.cs:597-622`), but no log was collected.
- **Tests/browser:** none run; no browser UAT.

## Original source conflict and rating register

| Source ID / rule | Conflict or ambiguity | Security and business impact | Disposition |
|---|---|---|---|
| RM-09, DP-14 | Source groups Tenant Admin and Organization Admin as immutable Protected roles although Organization Admin protection/appointment is PO-open and System must remain distinct from Protected. | Over-protects an unapproved role or permits Tenant-wide admin assumptions; can block ordinary role management or expand cross-Organization access. | Preserve IDs; mark Conflicting/Requires PO Decision; define Tenant Admin protection separately. |
| DP-04 / ADR-RBAC-05 | Source proposes ordinary Administration permission delegation; established Admin policy restricts Administration-module/baseline grants and requires effective authority. | A blanket relaxation can expose administrative control; a blanket deny can block legitimate scoped administration. | PO decision required; keep reserved/core checks mandatory. |
| PA-04 | Source proposes removing optional Tenant Admin operational permission; established rule retains all mandatory tenant Administration permissions, including future additions. | Could silently remove continuity baseline if optional versus mandatory is undefined. | PO decision; only optional grants outside mandatory baseline may be considered. |
| UR-09 | Source labels create permission as Users.Create; current API contract uses Users.ManageAccounts and separately checks Users.AssignRoles. | Tests may exercise the wrong authorization contract or mask a role-delegation hole. | Correct the scenario fixture/key through PO-reviewed amendment; do not silently rename. |
| EP-03 | Deny precedence is a negative/security case but source classifies it Direct under its own rule that Corner includes negative/security/boundary cases. | Coverage accounting may understate security corner cases. | Preserve EP-03 and W5/S0; propose Direct-to-Corner reclassification. |
| UR-03 and prior RBC-D02 | Source rates required PM onboarding W5/S1; the earlier synthetic RBC row rated it S0 using mandatory-release priority. | Severity must measure impact, not release priority. | Preserve source W5/S1; do not treat prior synthetic S0 as an adjustment. |
| EP-07 | Source says deny a Project-scoped permission if user is not assigned as that Project's PM. | Project-scoped authority may be valid for other authorized project roles; blanket PM-only assumption can deny legitimate access. | Requires exact permission-to-resource assignment policy and PO decision. |
| UR-14 / CM-08 | Source says rollback related events; other rows require auditable denial/failure diagnostics. | Rolling back all audit events can erase security evidence; persisting business audit for a failed transaction can misstate success. | Separate immutable security-attempt telemetry from committed business-change audit; PO/compliance decision. |

## Security conclusion

Core denial, protected-admin, Tenant-isolation and concurrency safeguards are present in source, but the role-classification migration and related workflow behavior are not ready to implement. Resolve the role identity and Organization Admin scope/assignability, Administration delegation conflict, assignment lookup, and direct-override UI gaps; then obtain explicit Product Owner approval of the reconciled scenario matrix and any candidate additions. No implementation is authorized by this analysis.
