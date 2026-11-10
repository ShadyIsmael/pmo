# New Role Permission Selection Regression (current delegation clarification 2026-11-09)

**Historical test date:** 2026-11-05
**Scope:** Roles & Permissions New Role workflow  
**Database:** No database operations performed

**Current policy:** An actor authorized through `Roles.ConfigurePermissions` may grant or revoke active, assignable, scoped non-Administration permissions to a same-Tenant user-managed custom role without holding each permission, regardless of target category. Administration-module permissions remain actor-authorized only. Dependencies, Active Deny, scope, protected/system-managed role rules, and no-self-escalation remain enforced. The earlier actor-possession ceiling is superseded.

## Authorization contract

The New Role editor uses the current role-delegation contract. A permission row is selectable when the API reports the actor is authorized to configure that permission for the target role and its required permissions are grantable or already selected. For an unassigned user-managed custom role, ordinary active, assignable, scoped non-Administration permissions are not limited by actor possession or target category. Administration-module permissions remain actor-authorized only; protected/system-managed roles and a role assigned to the actor retain their separate safeguards.

The catalog API derives `CanDelegate` from active/assignable catalog status, role-administration authority, permission class, and required dependencies; it does not mean the actor already holds the permission. The create/update service revalidates tenant ownership, target-role eligibility, scope, Active Deny, dependencies, and no-self-escalation at save time. The UI must not substitute `isAssignable` or role names for this authorization decision.

If an authorized role administrator sees an ordinary non-Administration row disabled, inspect the catalog response and role-administration capability, active/assignable status, dependencies, and scope. Actor possession is not required for eligible custom-role grants. Administration-module rows still require actor authorization; system-role and self-escalation restrictions are checked for the target role.

## Root cause and fix

The permission catalog query used a static cache key even though its response contains actor-specific `CanDelegate` values. After the current actor permission or authority state changed during the same session, React Query could reuse the previous snapshot for up to the 60-second stale window. The UI correctly treated stale `false` values as non-delegable. This source-level cache regression is separate from the current rule that ordinary non-Administration custom-role grants do not require actor possession; no live database or browser session was used to inspect an individual catalog response.

`usePermissionCatalog` keys the cache by actor ID, tenant ID, and a sorted, deduplicated fingerprint of effective permissions. `RolesPage` passes the refreshed user snapshot to the hook. It also refetches stale data on focus, and relevant role/user permission mutations invalidate catalog queries. Selection requires `canDelegate === true`; that state represents current delegation authority under the target-role rules, not merely possession of the selected permission.

## Historical regression coverage added (2026-11-05)

- React New Role: a delegated View and Action can be selected; selecting the Action includes its required View.
- React New Role: a non-delegable Action remains disabled with an explanation.
- React Query cache: a refreshed actor permission set changes the cache key, triggers a new catalog request, and replaces stale `canDelegate: false` data with the new response.
- React New Role: selected IDs remain checked through search, module collapse/expand, level filtering, and grant-state filtering; the saved request contains only selected IDs.
- Historical API service assertions required actor possession or a bootstrap key for delegated entries and rejected an unheld Delete. Those assertions describe the prior actor-possession ceiling and are not evidence for the current custom-role grant rule.
- Existing React and API regressions continue to cover editing an existing role, protected Admin locks, tenant boundaries, and Active Deny.

## Historical verification status (2026-11-05; does not verify current grant policy)

Focused React suites pass: `access-security-pages.test.tsx` - 28/28; `permission-catalog-cache.test.tsx` - 1/1 (29/29 total). The React production build passes; Vite reports dynamic-import and large-chunk warnings.

The API build errors reported afterward in `AuditProjectionActionHandler` were fixed by typing the parsed actor ID as nullable and reading entity type from the workflow entity snapshot. `WorkflowExecutionPhase2Tests` now asserts the projected audit entity type and null user ID for a non-GUID actor. Verification in this environment remains blocked before compilation: MSBuild fails during `_GetProjectReferenceTargetFrameworkProperties`, with `MSB4276` resolving `Microsoft.NET.SDK.WorkloadAutoImportPropsLocator` and `Microsoft.NET.SDK.WorkloadManifestTargetsLocator` from the installed .NET 10.0.302 SDK. The focused API regression test is added but not executed, so backend build/test results remain unavailable here.

No Development database grants, permission rows, or protected Admin baseline were changed or queried; no browser UAT was performed. Current possession-neutral custom-role delegation, Administration-module authorization, and no-self-escalation require the focused verification and UAT listed in the Master Checklist.
