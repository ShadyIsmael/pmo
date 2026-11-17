# S1 - Identity API Scope Regression-Test Slice

**Date:** 2026-11-13  
**Scope:** Protected Core boundary source update, Identity API permission contracts, and cross-tenant direct-permission administration  
**Status:** Source and regression tests added; **not run**  
**Baseline:** `Document-Center` at `cdd1bddf46b851eef38fc915137d4eefe32c1bc4`

## Changed source and coverage

- `ProtectedTenantAdminPolicy.cs` changes the protected baseline from every active Tenant-scoped permission (141 keys) to a 63-key Core: active Tenant Administration-module or `IsTenantAdminBaseline` permissions plus the 14 explicitly approved Portfolio/Program/Handover keys. Future active Tenant keys that satisfy the Administration-module or baseline-flag rule are included.
- The policy checks that Core Actions have dependencies inside the Core. This prevents a protected capability from being silently removed by `PermissionCatalog.EnforceDependencies`; an out-of-Core, inactive, or cross-scope dependency must be resolved as a catalog error.
- `PermissionCatalogArchitectureTests.cs` now pins the 188-key catalog, 49 View / 139 Action census, 63-key Core, future Administration/flag behavior, and Core dependency closure.
- `IdentityApiAuthorizationScopeTests.cs` pins explicit route policies on Roles and Users administration actions, plus canonical active Tenant scope and Action→View dependencies.
- `UserPermissionAdministrationTests.cs` adds a two-tenant regression case: an actor in Tenant A cannot read or alter direct overrides for a Tenant B user by passing Tenant A as scope. It asserts the 404 outcome, no foreign override write, and preservation of the foreign user's existing effective permission.

The Core policy change follows the decision recorded in `DECISION-REGISTER.md`. Existing persisted role grants were not removed or reconciled; no seed or database operation occurred. The exact persisted grant state is not verified.

## Checklist for later execution

- [ ] Run `PermissionCatalogArchitectureTests` and confirm the catalog/Core/dependency assertions.
- [ ] Run `IdentityApiAuthorizationScopeTests` and confirm Roles/Users route-policy mappings.
- [ ] Run `Direct_permission_administration_cannot_read_or_update_a_user_from_another_tenant` and the existing tenant-scoped override cases.
- [ ] Run `ApiAuthorizationFallbackTests` and workflow lookup contracts to recheck the authenticated fallback and narrow lookup policies.
- [ ] Run device-token ownership and workflow-provision regressions, including read-only, cross-user/Tenant, entity-write permission, and project-scope cases.
- [x] Source-map all 683 routes to exact policy metadata, service/resource scope, and current React consumer or explicit no-consumer status; current census has 617 Tenant-policy, 8 Platform-only, and 58 no-direct-policy routes, with no source/UI mapping fields marked unverified.
- [ ] Execute current API/React regressions and authenticated cross-Tenant, cross-Organization, cross-Project, malformed ownership, and concurrency checks; the current source map is not runtime verification.
- [ ] Add cross-project, malformed ownership-chain, and minimal lookup tests after mapping the exact endpoint/service contracts.
- [ ] Add concurrent revoke/grant and last-admin mutation tests after mutation-consistency behavior is finalized.

## Evidence limits

- No test, build, runtime, or database command was run; compilation and outcomes remain **not verified**.
- The direct-override test uses the existing in-memory SQLite test context. It is not SQL Server or live API evidence.
- Route contract tests inspect controller metadata and catalog definitions; they do not prove runtime policy registration or downstream service resource scope.
- DG-03 conflict precedence is decided, but scope coverage is partial. Overrides remain Tenant-level; service checks must enforce resource ownership. S3 now rechecks actor permissions inside the tenant mutation guard; transaction/concurrency behavior remains unverified.
- S1 remains partial until the full endpoint/action/service/resource mapping, negative scope coverage, and test execution are complete.
