# Sprint S3 — Delegation and Permission Operations

**Date:** 2026-11-13  
**Result:** **PARTIAL — ACTOR PERMISSION FRESHNESS GUARDS AND REGRESSIONS ADDED; CONCURRENCY AND SELF-ESCALATION ASSURANCE OPEN**

## Source changes

- `RoleAdministrationService` acquires the Tenant mutation guard and rereads the actor’s effective permissions before role creation, update, and deletion. Create, delete, and update permission changes commit under the guard; last-effective-admin checks remain inside the guarded update path.
- `UserAdministrationService` applies the same guarded recheck before user creation, password reset, profile/status/role updates, and direct permission override changes. Operation-specific permissions are recalculated after lock acquisition.
- On SQL Server, the shared guard serializes these Tenant administration mutations with the existing application lock. The guard also uses a serializable transaction for relational providers.
- Added regressions for role creation and direct override updates when the actor’s permission is revoked between preflight and lock acquisition.
- Added an architecture regression requiring the active permission dependency graph to be acyclic.

## Scope and limits

- This does not prove every controller/service authorization path or delegation composition across role configuration, target role assignment, and multi-role effective access.
- No concurrent SQL Server test was run. The in-memory regression provider does not validate SQL Server application-lock semantics.
- Last-admin race coverage, bulk grant/revoke coverage, and all self-escalation combinations remain open.
- No catalog key, SeedId, database row, migration, or grant was changed in S3.

## Verification

Tests were authored but not run, following the Product Owner’s preference to run tests and browser UAT. No API build, database operation, API runtime check, or browser UAT was performed.
