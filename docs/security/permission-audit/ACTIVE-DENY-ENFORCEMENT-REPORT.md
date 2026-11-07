# Active Deny Enforcement Report

## Source behavior

`EffectivePermissionResolver.Resolve` merges inherited role grants and direct Allow overrides, collects explicit Deny overrides, and removes denied keys from the ordinary effective set. Deny therefore wins over multiple role Allows and direct Allows for the same key. Protected tenant Admin permissions are re-added only as a separate mandatory invariant; normal users do not receive that exception.

`UserPermissionClaimsMiddleware` removes JWT permission/role snapshots and obtains current tenant-scoped effective permissions for each authenticated request. It also validates active user sessions and tenant status before downstream policy evaluation. Permission grant, revoke, role, or direct-Deny changes therefore affect the next protected API request without waiting for token expiry or a UI relogin.

Time-tracking scope permissions now pair by resource scope: `ManageOwn` depends on `ViewOwn`, and `ManageTeam` depends on `ViewTeam`. Read, mutation, list, and summary paths apply the matching effective key to own-resource versus team-project data. The React timesheet page and hours dashboard widget require explicit View keys; Manage keys do not act as read fallbacks. A team grant no longer exposes the actor's own time when `ViewOwn` is denied, out-of-scope list/summary filters return 403, and summary planning/capacity queries are filtered by tenant, resource, and authorized project scope.

The frontend refreshes `/api/auth/me` after a protected API returns 403 and periodically/on focus while the session remains active. This updates permission-gated controls without requiring login/logout; API authorization remains authoritative.

Protected controllers use API authorization policies; business services that expose direct entrypoints also validate permission and tenant/resource eligibility. Denied API mutations are expected to return 403 and leave state unchanged.

User and role administration services additionally bind caller-supplied actor and tenant identifiers to the authenticated request and re-read effective permissions from persisted tenant assignments before reads or writes. Direct service calls without that identity binding fail closed.

Sensitive resource costs are returned only when the effective principal has the exact `Resource.ViewCostRate` key. `Resource.EditCostRate` depends on that View key, and an edit-only claim does not reveal capacity or cost-utilization rates. The retired `Resource.ManageRates` fallback was removed from API services and React gates.

## Evidence and limits

- Source tests cover explicit Deny against multiple inherited Role Allows and ordinary direct Allow behavior.
- Catalog architecture tests assert that a missing own/team View dependency removes the matching TimeTracking Manage key. Route contract tests assert that Handover/Assignment Recovery helper routes use canonical View policies while their services retain exact action checks. The API testhost did not connect, so these backend tests are NOT EXECUTED.
- Added SQLite service coverage for a Team View grant without Own View: the actor's own entries and own-resource summaries must be rejected/filtered, while an in-scope teammate entry remains readable. This backend test is NOT EXECUTED.
- Frontend permission coverage now asserts that ManageOwn/ManageTeam without a View permission does not fetch or display time-tracking data.
- A frontend session test covers hiding a previously available action after the API returns 403 and `/api/auth/me` reflects the revoked permission.
- A financial negative test covers legacy/broad permission attempts and asserts no obligation mutation.
- The React full suite passed 765/765 tests across 141 files, including the frontend permission/session regressions. Backend authorization tests did not execute. Authenticated HTTP 403 behavior, cross-instance behavior, and a previously authenticated second-session revocation scenario remain runtime verification gates.
- New source coverage exercises user and role service-entry identity binding and requires effective `Users.View`/`Roles.View` for administrative reads; this coverage is authored but remains unexecuted.
- No persisted direct-Deny rows were inspected because the read-only SQL probe failed with a socket access-permission error before SQL authentication.

Current status: source implementation present; runtime grant/Deny verification pending.

## Final source review addendum - 2026-11-02

- Protected tenant Admin resolution now uses `IsSystem` plus the stable `SystemRoleKey = "tenant.admin"` and Tenant scope. It does not rely on role name or creator as an authorization fallback.
- Permission middleware discards permission/role snapshots from JWTs and reloads the current tenant-scoped effective set after session validation for each authenticated API request. Existing-session revocation is therefore implemented in source but still needs live HTTP verification.
- Source review found no active backend references to the retired keys `projects.read`, `finances.write`, `Roles.Manage`, `ReferenceData.Manage`, `ProjectFinance.Manage`, `Resource.Allocate`, `Resource.ManageRates`, `settings.read`, `settings.write`, or `users.write`; no unknown literal permission policy keys remain. Historical rows and explicit negative tests are not runtime grants.
- Remaining OR policies were reviewed as bounded alternatives: TimeTracking selects own versus team permission by resource scope and the catalog pairs each Manage key with its corresponding View; assigned payment-request review is limited to a currently pending reviewer assignment and rechecked by the service; `ProjectFinancialFoundation.View` is an explicit AND of both source View permissions. Work Item edit/delete depend on WorkItem.View and mutations still require WorkItem.Edit. Platform settings use separate platform authorization handlers.
- Project Audit detail payloads are now filtered by source entity permission while `Audit.View` gates the audit surface. Project change requests, reports, handovers, stakeholders, people/membership records, and project snapshots use their respective source View key before Changes/Before/After details are serialized.
- Plugin Registry list/detail uses the current non-empty Tenant context and `Plugin.View`. The authenticated Settings runtime bootstrap exception returns only six Standard `regional.*` values for the Tenant claim validated in the active session; it does not accept a caller-supplied Tenant ID or return fiscal-year settings.
- New negative/source contract tests cover audit detail masking, Tenant runtime settings isolation, protected Admin fixtures, and exact Settings policies. These backend tests have **NOT RUN** because VSTest could not connect to testhost. Direct API Deny, unchanged mutation state, cross-instance/session revocation, Tenant isolation, and browser runtime authorization remain **NOT EXECUTED**.
