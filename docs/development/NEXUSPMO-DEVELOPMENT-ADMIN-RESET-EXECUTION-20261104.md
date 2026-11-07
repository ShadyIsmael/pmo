# NexusPmo Development Admin Reset Execution Report

**Date:** 2026-11-04  
**Requested target:** Development SQL Server `Shady`, database `NexusPmo`  
**Outcome:** **BLOCKED — reset not executed**

## Decision

The active request explicitly authorizes resetting only Shady/NexusPmo and authorizes creating a backup. The reset was not attempted because the repository policy in force at the time prohibited database drops/recreation and migration-history resets, while AGENTS.md allowed this narrowly scoped Product Owner-authorized Development reset. That policy conflict blocked the action and is retained here as historical context only.

No database was dropped, recreated, seeded, or otherwise modified. No Production or other database was touched. The only database operation that wrote anything was the separately authorized backup to a file. The user’s Product Owner authorization is recorded in the active task; the reset gate’s typed database-name confirmation and authorization-reference arguments were not submitted because the reset action was not run.

## Target and environment verification

The repository EF gate was run read-only with `--environment Development`, expected server `Shady`, and expected database `NexusPmo`. It reported `SERVERPROPERTY('ServerName')=Shady`, `DB_NAME()=NexusPmo`, and `DB_ID()=5`. The effective Development `DefaultConnection` from `appsettings.Development.json` was supplied to the gate for that invocation; the live SQL target matched it. The action was `scripts/database/ef-database-gate.ps1 inspect --environment Development --expected-server Shady --expected-database NexusPmo`.

The host shell initially had no `ASPNETCORE_ENVIRONMENT`, `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING`, or `ConnectionStrings__DefaultConnection` environment variable. No API listener was present on ports 5000 or 5100, so there was no running API process to stop or observe. The SQL Server service was running as `NT Service\MSSQLSERVER` on the default instance.

## EF gate inspection

- **Context / migration assembly:** `NEXUS.Persistence.AppDbContext` / `NEXUS.Persistence`.
- **EF model synchronized:** Yes. A fresh `scripts/database/ef-database-gate.ps1 model` build completed with 0 warnings and 0 errors.
- **Repository migrations:** 10.
- **Applied migrations before the requested reset:** 10.
- **Pending migrations:** 0; none.
- **Unknown history migrations:** 0.
- **Physical schema:** Verified against the last applied and current repository models; 0 mapped objects missing.
- **Migrations applied during this task:** None.
- **Reset migration SQL review:** Not run; the reset was blocked before migration planning or application.

Applied migration IDs:

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

## Backup

**Backup verified:** Yes. SQL Server created a full copy-only backup with checksum and compression at:

`C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup\NexusPmo_pre_reset_20261104_ac24eb7564884f1f80b65169ab684b2b.bak`

SQL Server reported the file present. `RESTORE VERIFYONLY ... WITH CHECKSUM` reported, “The backup set on file 1 is valid.” `RESTORE HEADERONLY` identified server `Shady`, database `NexusPmo`, full backup type, `IsCopyOnly=1`, `HasBackupChecksums=1`, `IsDamaged=0`, and compression enabled. The backup size was 21,061,632 bytes (8,768,002 compressed bytes).

SQL Server reported only `C:` as a mounted SQL-visible drive; the database files and default backup folder are both on `C:`. `D:\NexusPmoBackups` was absent to SQL Server. The verified backup therefore protects against this logical reset but is not volume-independent. No volume was mounted and no ACLs were changed.

## Minimal Seed and Admin readiness

- **Required seed mode:** `--seed-minimal-only` was not run.
- **Seed idempotency:** Not verified against SQL Server; the required two reset-seed runs did not occur.
- **Exactly one active Admin / protected Admin permission baseline:** Not verified after reset.
- **Zero demo/business records:** Not verified after reset.
- **Development Admin bootstrap secret:** `NEXUS_ADMIN_BOOTSTRAP_PASSWORD` was absent from the process environment. A fresh Admin bootstrap is not ready until a secure secret is provided. No password was generated, changed, or printed.
- **Admin login readiness:** Not verified; no login or password-change flow was run.

## Build and test results

- **EF gate model/build:** Passed; 0 warnings, 0 errors; model synchronized; no database connection was opened by the model action.
- **Focused Minimal Seed test:** The test project compiled, but the test run exited 1 before test cases started. VSTest aborted after 90 seconds because it could not connect to the testhost process. No SQL-backed persistence tests ran.

```powershell
dotnet test apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/NEXUS.Modules.ResourceManagement.Tests.csproj --no-restore -m:1 -p:FocusedMinimalSeed=true --filter "FullyQualifiedName~SeedMinimalAsync" --logger "console;verbosity=normal"
```
- **Browser UAT:** Not run, as requested.

## Required completion checks

| Check | Result |
|---|---|
| EF model synchronized | Yes |
| Pending migrations | 0; none |
| Development DB identity verified | Yes — `Shady` / `NexusPmo`, DB ID 5 |
| Backup verified | Yes — path above; same volume as database files |
| Migrations applied during this task | None |
| Minimal Seed run twice | No |
| Minimal Seed idempotency verified | No |
| Physical schema verified | Yes — pre-reset schema |
| Exactly one active Admin and protected baseline verified | No |
| No demo/business records verified | No |
| Persistence tests | Not completed — testhost failed before test execution; SQL-backed tests not run |
| Admin authentication verified | No |

## Remaining blockers

1. Reconcile the protected EF skill’s no-drop rule with the reset exception in `AGENTS.md` through the authorized policy-maintenance process. Until those instructions agree, the reset action cannot be run while following all mandatory instructions.
2. The focused Minimal Seed testhost did not start, so no test outcomes are available; the required SQL-backed persistence tests also remain unrun.
3. A separate SQL-visible backup volume was unavailable. The verified backup is on the same `C:` volume as the database files.
4. Minimal Seed, Admin bootstrap/login, reset-specific row counts, and post-reset physical-schema checks remain unverified because the reset did not run.
