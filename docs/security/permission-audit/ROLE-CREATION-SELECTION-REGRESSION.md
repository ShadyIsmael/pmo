# New Role Permission Selection Regression

**Date:** 2026-11-05  
**Scope:** Roles & Permissions New Role workflow  
**Database:** No database operations performed

## Authorization contract

The New Role editor uses the same actor-specific delegation contract as existing-role editing. A permission row is selectable only when the API projects `canDelegate: true` and its required permissions are grantable or already selected. `isAssignable` describes catalog policy; it does not grant the current actor authority to delegate that permission.

The catalog API derives `CanDelegate` from active permission metadata, the actor's effective permissions or the narrowly scoped bootstrap-delegation policy, and required permission dependencies. The role create/update service revalidates tenant ownership, assignability, actor authority, and dependencies at save time. The UI must not substitute `isAssignable` or role names for this authorization decision.

If a properly authorized Tenant Admin sees every row disabled, inspect the actual catalog response for `canDelegate` and the session's effective `Roles.ConfigurePermissions` and individual delegable permissions. Missing or false actor-specific values correctly disable selection; a stale API payload or incorrect effective-permission state must be corrected at its source.

## Root cause and fix

The permission catalog query used a static cache key even though its response contains actor-specific `CanDelegate` values. After the current actor's effective permissions changed during the same session, React Query could reuse the previous snapshot for up to the 60-second stale window. The UI correctly treated stale `false` values as non-delegable, leaving previously grantable controls disabled. This is the source-level regression path; no live database or browser session was used to inspect an individual catalog response.

`usePermissionCatalog` now keys the cache by actor ID, tenant ID, and a sorted, deduplicated fingerprint of effective permissions. `RolesPage` passes the refreshed user snapshot to the hook. It also refetches stale data on focus and the relevant role/user permission mutations invalidate catalog queries. Selection still requires `canDelegate === true`; the UI does not use `isAssignable` as an authorization fallback.

## Regression coverage added

- React New Role: a delegated View and Action can be selected; selecting the Action includes its required View.
- React New Role: a non-delegable Action remains disabled with an explanation.
- React Query cache: a refreshed actor permission set changes the cache key, triggers a new catalog request, and replaces stale `canDelegate: false` data with the new response.
- React New Role: selected IDs remain checked through search, module collapse/expand, level filtering, and grant-state filtering; the saved request contains only selected IDs.
- API service: delegated View and Action entries project `CanDelegate=true`, an undelegated Delete entry projects false, the authorized role creation persists the selected View and Action, and a Delete escalation is rejected.
- Existing React and API regressions continue to cover editing an existing role, protected Admin locks, tenant boundaries, and Active Deny.

## Verification status

Focused React suites pass: `access-security-pages.test.tsx` - 28/28; `permission-catalog-cache.test.tsx` - 1/1 (29/29 total). The React production build passes; Vite reports dynamic-import and large-chunk warnings.

The API build errors reported afterward in `AuditProjectionActionHandler` were fixed by typing the parsed actor ID as nullable and reading entity type from the workflow entity snapshot. `WorkflowExecutionPhase2Tests` now asserts the projected audit entity type and null user ID for a non-GUID actor. Verification in this environment remains blocked before compilation: MSBuild fails during `_GetProjectReferenceTargetFrameworkProperties`, with `MSB4276` resolving `Microsoft.NET.SDK.WorkloadAutoImportPropsLocator` and `Microsoft.NET.SDK.WorkloadManifestTargetsLocator` from the installed .NET 10.0.302 SDK. The focused API regression test is added but not executed, so backend build/test results remain unavailable here.

No Development database grants, permission rows, or protected Admin baseline were changed or queried; no browser UAT was performed.
