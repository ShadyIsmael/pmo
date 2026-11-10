# NexusPmo Development Admin Reset Report

**Date:** 2026-11-04  
**Requested target:** Development SQL Server `Shady`, database `NexusPmo`  
**Outcome:** **NOT EXECUTED**

**Backup-policy supersession (2026-11-10):** The auto-review rejection and backup requirement below are historical. Development/UAT backups are optional; missing backup media, independent storage, or `RESTORE VERIFYONLY` does not block an authorized operation. This update does not authorize a reset: destructive operations still require explicit Product Owner authorization and all exact target, migration, and schema checks.

## Reset outcome

The requested `DROP/RECREATE` was rejected by automatic action review before the destructive command ran. The rejection cited repository policy that requires a verified backup and prohibits database drop/recreate. The request explicitly waived a backup, so no backup was created and no alternate reset route was used. No database write was performed by this task.

**Policy addendum (2026-11-04):** A subsequent repository policy revision created a narrowly scoped no-backup exception for a Product Owner-authorized reset of Development `Shady/NexusPmo`. This later policy does not change the historical review outcome above or retroactively authorize that earlier command; any future reset must follow the updated EF gate and complete every migration plus the required reset-specific seed.

**Minimal Seed addendum (2026-11-04):** The clean Admin-bootstrap reset plan now uses the separate `--seed-minimal-only` mode twice; the existing `--seed-only` Demo Seed remains unchanged and is not used for that reset. This updates the future execution plan only. The reset report's historical outcome remains **NOT EXECUTED**; no reset or seed has run, and this addendum makes no claim about current business-data contents.

The database therefore remains in its pre-reset state. This report does not claim that it contains only an Admin user.

## Read-only target verification

A read-only Windows-authenticated query against only `NexusPmo` returned:

| Check | Result |
|---|---|
| `SERVERPROPERTY('ServerName')` | `Shady` |
| `DB_NAME()` | `NexusPmo` |
| `DB_ID()` | `5` |
| SQL principal | `Shady\shady` |
| Other active requests in `NexusPmo` | `0` |
| Other sessions with recent SQL attributed to `NexusPmo` | `0` |

The repository EF inspection also confirmed the effective Development connection endpoint matched the requested target. No API listener was present on the checked Development ports 5000 or 5100.

## Pre-reset EF state

The read-only EF gate reported:

- **Context / migration assembly:** `NEXUS.Persistence.AppDbContext` / `NEXUS.Persistence`.
- **EF Model synchronized:** Yes.
- **Source migrations:** 10.
- **Applied migrations before the requested reset:** 10.
- **Pending migrations:** 0.
- **Unknown history migrations:** 0.
- **Physical schema verified against applied and current models:** Yes; 0 missing objects.
- **Generated empty-database SQL reviewed:** Yes; 10 migrations, 7 expected index replacements, conditional tenant/team/admin-identity backfills, and no `DROP TABLE`, `DELETE`, or `TRUNCATE` statements.
- **Migrations applied during this task:** None.
- **Backup verified:** No; explicitly waived in the request, and none was created.
- **Persistence tests:** Not run; test-project compilation stopped before execution on `CS9175` / `CS8122` in `apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/CostUtilizationManagementScopeTests.cs`.

Applied migration IDs before the reset request:

1. `20261029155617_InitialFopxPmBaseline`
2. `20261029181208_ProjectPaymentRequestPersistence`
3. `20261029183557_AddTenantRegistry`
4. `20261030122718_AddProjectAnnualBudgetFoundation`
5. `20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation`
6. `20261031000210_AddFinancialPersistenceSprints3to6`
7. `20261031142036_AlignResourceProjectTeamsAndInvitations`
8. `20261101114229_AddProjectAnnualBudgetApprovalConfiguration`
9. `20261101213237_SynchronizePlanningPhysicalSchema`
10. `20261102190318_AddStableSystemRoleIdentity`

## Seed and Admin authentication

`SeedIdentityOnlyAsync` in `apps/api/src/NEXUS.Persistence/SeedDataProvider.cs` now calls an idempotent helper to add the minimal structural Organization row. The API build compiled this source change with **0 errors** and **352 IKVM warnings**. The identity seed was not run because the reset was blocked, so the new row, seed idempotency, Admin membership/permissions, and empty business-data baseline were not verified against SQL Server.

The identity-only path relies on the code-defined setting catalog defaults and does not seed `ConfigurationSettings` override rows. Its expected Admin email is `admin@nexus.com`, but this task did not create a live account or password.

No Admin credential was created or changed, and no Admin login or password-change flow was verified. No browser UAT was run. No username/password is issued by this task.

## Completion fields

| Field | Result |
|---|---|
| Model synchronized | Yes |
| Pending migrations | 0; none |
| Development DB identity verified | Yes — `Shady` / `NexusPmo`, DB ID 5 |
| Backup verified | No — user waived it; no backup created |
| Migrations applied | None in this task; 10 were already applied before reset |
| Physical schema verified | Yes, pre-reset |
| Persistence tests | Not run; test compilation failed with `CS9175` / `CS8122` |
| Seed run twice | No |
| Admin authentication verified | No |
| Blocker | Automatic review rejected `DROP/RECREATE` under repository policy; no alternative route was used |
