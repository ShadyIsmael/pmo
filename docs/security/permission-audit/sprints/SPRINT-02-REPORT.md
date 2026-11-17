# Sprint S2 — Tenant Admin Provisioning and Lockout Baseline

**Date:** 2026-11-13  
**Result:** **PARTIAL — SOURCE UPDATED; TESTS AND DATABASE STATE NOT VERIFIED**  
**Baseline:** Document-Center at cdd1bddf46b851eef38fc915137d4eefe32c1bc4

## Policy boundary

- Keep the existing protected tenant.admin identity as the sole protected tenant identity for this task.
- Protect the 63-key Tenant Admin Core. Core permissions are restored by protected-role reconciliation; optional grants remain policy-managed.
- New tenant Admins receive the active, assignable permission set outside Platform, CrossTenant, and SystemOwner scopes, except Program.CreateAcrossPortfolios, which remains an explicit opt-in.
- ProjectSettings.View and ProjectSettings.Edit are optional initial grants and are not restored if removed from an existing Admin role.
- Existing optional grants are preserved. Reseeding adds only missing Core entries and removes no existing role grants.
- Licensing provider integration and a new recovery/succession flow are deferred until a trusted provider contract and secure credential handoff exist.

## Source changes

- TenantProvisioningService now uses the initial grant set only when creating a new protected Admin role. Existing Admin roles receive missing Core grants only.
- Development seed creation uses the same initial grant set. Seed reconciliation for an existing Admin is Core-only.
- Removed Project Settings role-grant additions from the initial delegation bootstrap and normal seed reconciliation. The bootstrap now ensures canonical permission rows and verifies administrative capability without changing optional role grants.
- Added architecture, new-tenant provisioning, and seed regression tests covering the default grant boundary, Core restoration, explicit opt-in preservation, and non-restoration of optional Project Settings grants.

The current catalog and policy imply an initial grant set of 187 keys, including financial approval actions, while only 63 are immutable Core. This broad initial privilege is an explicit recorded decision, not a test or database observation. Project and Organization resource-scope coverage remains a security gate.

## Owner verification and limits

Tests were authored but not run. Seed behavior, persisted grants, Development DB schema, and last-Admin concurrency remain unverified. No DB operation, migration, seed command, build, API runtime, or browser UAT was performed.
