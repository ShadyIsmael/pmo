# NexusPmo Development Minimal Admin Seed Report

**Date:** 2026-11-04  
**Requested target:** Development SQL Server `Shady`, database `NexusPmo`  
**Outcome:** **BLOCKED - seed not executed**

## Scope and actions

The request was to initialize the existing manually cleared Development database with `--seed-minimal-only`, run it twice, and verify the resulting Admin and empty business-data baseline. No database reset, clear, migration, seed, SQL write, or Demo Seed was run. The EF offline model gate passed. The script executor could not connect to SQL Server (`Access is denied`), so read-only live inspection was completed in the local SSMS host session instead. That session confirmed the target identity, migration history, and table row inventory. The standalone seed remains blocked by the independent-backup requirement: SQL Server reports only drive `C:`, which also contains the database files, and no approved separate backup destination was available.

## Seed and permission source review

- `--seed-minimal-only` is implemented as a Development-only command and calls `SeedDataProvider.SeedMinimalAsync`; it does not apply migrations or invoke the Demo Seed.
- The authoritative catalog is `PermissionCatalog.cs`. A static source scan found **160 active canonical permissions**, with 160 distinct keys and stable IDs and no detected legacy aliases. This supersedes the **154** count in older November 2 permission reports; those catalog summaries are stale relative to the current source.
- `ProtectedTenantAdminPolicy` currently derives a **46-key** tenant-scoped protected baseline: 26 Administration entries, 6 explicitly flagged governance/workflow entries, and 14 approved Portfolio/Program/Handover entries. It excludes Platform, CrossTenant, SystemOwner, and Organization-scoped privileges. The source separately defines 102 supplemental Admin grants and two Project Settings grants; the reviewed supplemental list included some financial viewing and payment-request operations, but no financial approval or settlement permission.
- Current permission reports say 154 active permissions and 48 protected Admin keys; both counts are stale against source. The authoritative current source scan found 160 active keys and 46 protected baseline keys. Catalog summaries and target matrices need a separate synchronization pass.
- The source seed verifier checks the expected catalog, protected baseline inclusion, one Admin, expected tenant roles/membership, and absence of non-allowlisted business data. It does not enforce an exact total Admin grant set. These are source-level assertions only; none was evaluated against this database in this task.

## Admin bootstrap

The seed expects `admin@nexus.com`. A new or legacy-hash account requires `NEXUS_ADMIN_BOOTSTRAP_PASSWORD` with at least 12 characters. The implementation hashes it with ASP.NET Core's password hasher, requires a password change, and sets a temporary expiry (24 hours by default, configurable from 1 to 168 hours). Re-seeding preserves a valid existing hash and expiry state.

`ASPNETCORE_ENVIRONMENT`, `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING`, and `NEXUS_ADMIN_BOOTSTRAP_PASSWORD` were absent from the current shell at inspection time. No credential was generated, read, printed, or changed. Admin login readiness was not verified.

## EF and database evidence

| Gate | Result |
|---|---|
| EF Model synchronized | **Yes** — `scripts/database/ef-database-gate.ps1 model` passed; 0 warnings, 0 errors. |
| Repository migrations | **10**; IDs are listed by the offline model gate. |
| Live pending migration count/names | **0 pending** - SSMS read-only query found all 10 repository migrations in `dbo.__EFMigrationsHistory`, through `20261102190318_AddStableSystemRoleIdentity`. |
| Development DB identity verified | **Yes** - SSMS read-only query returned server `Shady`, database `NexusPmo`, database ID `5`, state `ONLINE`, recovery model `FULL`. |
| Backup verified for this write | **No** - SQL Server reports only drive `C:` and the database data/log files are on `C:`. The prior verified backup is also on `C:` and does not satisfy the standalone seed gate's independent-destination requirement. No new backup was created. |
| Migrations applied | **10 already recorded** in live migration history; **none applied during this task**. |
| Physical schema verified | **Partial only** - the live table inventory was readable and showed application tables empty, but the mandatory repeatable physical-schema inspection could not run from the script executor. Full model-to-database schema comparison is **not verified**. |
| Minimal Seed runs / idempotency | **Not run / not verified**. |
| Persistence tests | **Not run** - the focused `SeedMinimalAsync` VSTest run compiled but VSTest aborted after 90 seconds because it could not connect to testhost; no test cases started. |

The live migration history contained these 10 entries, all with EF product version `10.0.10`: `20261029155617_InitialFopxPmBaseline`, `20261029181208_ProjectPaymentRequestPersistence`, `20261029183557_AddTenantRegistry`, `20261030122718_AddProjectAnnualBudgetFoundation`, `20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation`, `20261031000210_AddFinancialPersistenceSprints3to6`, `20261031142036_AlignResourceProjectTeamsAndInvitations`, `20261101114229_AddProjectAnnualBudgetApprovalConfiguration`, `20261101213237_SynchronizePlanningPhysicalSchema`, and `20261102190318_AddStableSystemRoleIdentity`. This is current read-only evidence and supersedes the earlier inspection summary that reported nine entries and one pending migration. The 10-entry history establishes zero pending migrations against the current 10-migration repository inventory; it does not substitute for the required physical-schema comparison.

## Admin and row counts

Read-only partition metadata inventory of all non-system tables returned only `dbo.__EFMigrationsHistory` with **10 rows**. Every other existing non-system table had **0 rows** at inspection time. Therefore the live counts were: Admin users **0**, Admin role membership **0**, persisted Permission catalog/grants **0**, Tenant **0**, Organization **0**, Projects **0**, Programs **0**, Portfolios **0**, demo users **0**, teams **0**, handovers **0**, and financial records **0**. The current canonical catalog and protected baseline counts (160 and 46) are source definitions only and are not persisted in this database. These counts are a snapshot from the read-only SSMS inspection; no write was performed afterward.

## Blockers

1. The standalone seed is a database write. Repository policy requires an independently verified backup. Only `C:` is available to SQL Server and it contains the database files; the prior same-volume backup does not qualify. No approved separate destination was available. The reset-only no-backup exception does not apply because this task prohibits reset and requests a standalone seed.
2. The exact live target and migration history were verified read-only through SSMS, but the repeatable EF database gate could not connect from the script executor. Full physical-schema verification therefore remains incomplete.
3. `NEXUS_ADMIN_BOOTSTRAP_PASSWORD` was not configured in the execution shell. No secret was generated, exposed, or stored, so a new Admin could not be initialized.

**Completion status:** Minimal Admin seed is **not complete**. Seed runs: **0**; idempotency: **not verified**; active Admins: **0**; Admin role membership: **0**; persisted permissions: **0**; business and demo records: **0**; live migrations pending: **0**. No database was modified. Resume when the independent backup destination, approved gate connectivity, and secure bootstrap secret are available.
