# RBAC Migration and Data Integrity Evidence

**Date:** 2026-11-15; execution update 2026-11-16  
**Status:** Ownership migration applied and verified on Development; full application acceptance remains open.  

> **Superseding Development execution (2026-11-16):** The authorized target was verified as `Shady / NexusPmo`, DB_ID 5. Migration `20261116093145_AddOrganizationOwnedRoles` was the only pending migration and was applied through the approved wrapper after fresh SQL/hash review and Product Owner authorization. Final status is 30/30 applied, 0 pending, model synchronized, and 0 missing schema objects. Pre/post comparison confirmed unchanged Role IDs, active UserRole rows, and RolePermission links; the two approved Roles now belong to NEXUS Group and five unassigned operational Roles remain unresolved. See [final integration handover](ORGANIZATION-OWNED-ROLES-FINAL-INTEGRATION-2026-11-16.md) for artifact paths and remaining login/test blockers. The 2026-11-15 status below is historical and superseded for live target state.

> **Historical build-only handover (2026-11-15):** At that checkpoint database application remained pending. Its host preflight and SQL artifact are not evidence for the 2026-11-16 ownership migration. The new live target and data evidence are recorded in the superseding execution above.

## Scope and evidence boundary

This report assesses the working-tree EF model, migration, seed reconciliation, and documented Development database gate. It does not claim that current persisted roles, memberships, grants, or overrides match the source seed.

The repository's approved read-only host command is:

```powershell
Set-Location 'C:\Projects\pmo'; .\scripts\dev\db-preflight.ps1
```

The current executor's preflight could not establish SQL identity/history because the connection failed before those checks with `TCP Provider: Access is denied`. This is an execution-context boundary under [Development database connectivity and runner](../../development/development-database-connectivity-and-runner.md). The active Development server name, database name/ID, migration history, physical schema, and role-row counts therefore remain **unverified**. No retry through a host alias or alternate network path was attempted.

## Source migration assessment

Migration: `20261114232613_SeparateCanonicalRoleIdentityFromSystemClassification`.

| Concern | Source behavior | Evidence status |
|---|---|---|
| Canonical identity | Adds nullable `Roles.RoleKey` independently of `SystemRoleKey`; domain setter is set-once and rejects reserved admin keys for ordinary roles. | Source reviewed; build result below. |
| Legacy backfill | Backfills `RoleKey` from legacy `SystemRoleKey`, then changes non-approved Tenant System rows to `IsSystem=false` and clears their `SystemRoleKey`. | Source reviewed only; no rows read or changed. |
| Collision safety | Guards duplicate legacy keys, conflicting identity values, projected canonical collisions, malformed non-System rows, and non-Tenant System rows before reconciliation. | Source reviewed only; SQL generation/runtime not verified. |
| Data preservation | Migration statements do not delete/recreate Roles and do not update `UserRoles`, `RolePermissions`, permission overrides, or audit rows. Role IDs and those relationships are preserved by the migration's shown operations. | Design evidence only; preservation counts are unknown until pre/post DB inventory. |
| System invariant | Allows only `tenant.admin` and `organization.admin` as System identities and enforces equality with `RoleKey`. Explicit `IS NOT NULL` guards now prevent SQL CHECK `UNKNOWN` from accepting malformed System rows. | Critical review found and reported the nullable-CHECK flaw; guards were added to EF configuration, migration, migration designer, and model snapshot. Regression test source added; execution pending. |
| Rollback | `Down` throws before DDL/data mutation because reclassifying ordinary roles back to System would violate the approved rule. | Source reviewed; no rollback script generated or executed. This is a forward-only migration. |
| Seed idempotency and OA grants | Canonical seed resolves existing roles by `RoleKey`, legacy `SystemRoleKey`, then stable ID; it mutates the existing row rather than replacing it. New Organization Admin seed is canonical, Organization-scoped, and has no seeded membership. Existing OA role rows with active grants outside `OrganizationConfiguration.View/Manage` now fail closed before identity mutation; grants are not silently revoked. | Persistence source build passed; fail-closed and idempotency regressions compile but did not execute. No seed run or persisted state verified. |
| Ordinary role grants | Existing role IDs, memberships, and grant links are not removed by the migration. Seed logic is designed to preserve existing optional ordinary-role grants. | Source only; before/after grant inventory not available. |

## Source inventory, not database inventory

The prior seed source defined eight role types as System. The approved set previously contained Tenant Admin only, so seven operational roles violated the approved classification. Current seed source defines nine canonical role types, with exactly two System identities: Tenant Admin and Organization Admin. Project Manager remains a Management-category Non-System role. These figures describe code-defined seed data; they are **not** a live Development DB count.

Actual pre-migration and post-migration counts are not available for:

- Role IDs, RoleKeys, categories, `IsSystem`/`SystemRoleKey`, and soft-delete state.
- UserRole memberships and Organization settings grants.
- RolePermission links and user-level Allow/Deny overrides.
- Protected Core grants, duplicate rows, and membership multiplicity.
- Audit/history rows.

## Required host-side migration sequence

1. Run the exact read-only preflight command above from the authorized Windows Development host. Confirm `SERVERPROPERTY('ServerName')`, `DB_NAME()`, database ID, Development environment, Windows Authentication, applied/pending migrations, model synchronization, and physical schema.
2. Capture a read-only pre-migration inventory of role identity, membership, role grants, direct overrides, active/deleted state, and last effective Tenant Admin count, keyed by stable IDs.
3. Generate target-specific SQL through the documented EF gate only after identity succeeds. Review the full generated SQL, model/snapshot delta, and SHA-256. Stop if collision guards, physical schema, model synchronization, or expected target data do not match.
4. Apply only through `scripts/dev/db-apply-migrations.ps1` with the reviewed path/hash, active authorization reference, and `-ConfirmDevelopmentApply`. Do not use manual SQL or seed execution before the migration is applied.
5. Run the approved idempotent seed/reconciliation mode, then verify exact role identity/classification, stable Role IDs, unchanged memberships/grants/overrides/audit, no duplicate keys, only two System roles, protected Tenant Admin continuity/Core, and no unintended permission additions/revocations.
6. Run the relevant isolated persistence tests and a second read-only status inspection. Record counts and compare before/after. No Production/UAT target is permitted.

## Rollback and recovery

The migration's `Down` intentionally refuses rollback before mutation. Do not edit an applied migration or run rollback against a migrated database. Recovery must be a separately reviewed, forward corrective migration that retains the two-System-Role invariant and preserves identity/relationships. Before apply, the rollback for a failed deployment is **stop and diagnose**; do not restore legacy flags or delete role rows. No backup was requested; Development backups are optional under repository policy.

## Current result

| Gate | Result |
|---|---|
| EF model synchronized | Yes. |
| Pending migration count/names in Development | 0 pending after apply; the sole pre-apply pending migration was `20261116093145_AddOrganizationOwnedRoles`. |
| Target Development DB identity | Verified: Development `Shady / NexusPmo`, DB_ID 5, Windows Authentication. |
| Migration SQL generated/reviewed/hash recorded | Yes; fresh artifact and SHA-256 are listed in the final integration handover. |
| Migration applied | Yes, through `scripts/dev/db-apply-migrations.ps1`. |
| Seed executed/idempotency verified | Seed not run; idempotency not run. |
| Physical schema verified | Yes; 0 missing objects in the last-applied and current repository models. |
| Role/membership/grant data | Role IDs and active UserRole/RolePermission link IDs preserved; approved ownership reconciled; five unassigned operational Roles remain unresolved. |
| API build/tests | API build passed with 0 warnings/errors. Focused login/session and role-isolation tests passed 20/20; broader filtered suite passed 43/91 and failed 48. No Development SQL Server persistence test run. |
| Remaining blockers | Successful live login credential unavailable; broader filtered test failures and application-path persistence/UAT remain open. |


## Final recovery migration review — 2026-11-15

Host preflight evidence: artifacts/ef-review/dev-db-preflight-inspect-20261115T094923Z-77e35528.json. It confirms Development Shady/NexusPmo, DB_ID 5, Windows Authentication, model synchronized, 27/28 migrations applied, and one pending migration: 20261114232613_SeparateCanonicalRoleIdentityFromSystemClassification. Exit code 2 reflects that pending migration. The last-applied physical schema matched; the report lists dbo.Roles.RoleKey and dbo.Roles.UX_Roles_Tenant_RoleKey as the two current-model gaps.

The SQL artifact artifacts/ef-review/dev-pending-20261115T094740Z-eacb7b37.sql was rehashed and independently reviewed. SHA-256 is 29AF00E3847B795A261405258EDC8F8F524B880C89A7B5262AAADF1A60E1F85B, matching the host report. It drops/re-adds the same-named check constraint within the EF transaction, adds/backfills RoleKey, checks duplicate/colliding identities, reclassifies unapproved tenant System Roles, and creates the unique filtered index and replacement check. It changes only Roles identity/classification fields and migration history; it does not touch role IDs, UserRoles, RolePermissions, direct overrides, audit rows or unrelated business tables. The Organization Admin seed guard fails closed on extra active grants; approved grants are append-only and audited.

**Historical 2026-11-15 disposition:** No migration or seed had been applied at that checkpoint. The live ownership migration was applied on 2026-11-16; this older canonical-role evidence remains a record of its earlier source review and does not replace the current target-specific apply/status reports.
