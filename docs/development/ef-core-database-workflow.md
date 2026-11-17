# EF Core database workflow: migrations, safety, and seeding

This repository uses `NEXUS.Persistence.AppDbContext`, the `NEXUS.Persistence` migration assembly, and SQL Server. `NEXUS.Api` is the runtime host; EF CLI migration scaffolding uses the isolated `apps/api/tools/NEXUS.Persistence.DesignTime` executable. Check current project documentation and source before relying on these identifiers; a design-time fallback connection string is not evidence of the runtime target.

## Backup policy by environment

Backups are optional for Development and UAT database operations. Do not require a backup before a migration or another authorized change, require independent backup storage, or run `RESTORE VERIFYONLY` as a prerequisite. A missing or unavailable backup alone must not block an otherwise authorized operation. Create a backup only when explicitly requested, and report its outcome separately from the operation's identity, migration, model, and physical-schema checks.

Development and UAT are distinct environments. For either one, verify the actual environment, active application connection, SQL Server `SERVERPROPERTY('ServerName')`, `DB_NAME()`, and database ID against that environment's approved target. Do not identify a target from a host alias or relabel a UAT database as Development. The current reset procedure below remains limited to its explicitly named Development target and does not authorize resetting a UAT database. Production remains prohibited by this workflow; its mandatory backup and recovery controls remain in force in any separately approved Production procedure.

Historical deployment records below retain their original evidence and outcomes. Statements in those records that treated a Development backup as a deployment prerequisite describe the policy at the time and are superseded by this section; they do not establish current backup state or current schema readiness.

## Safe model check (offline)

From the repository root:

```powershell
.\scripts\database\ef-database-gate.ps1 model
```

This compares the current EF model with the migration snapshot and lists repository migrations without opening a database connection. A failing model check means a migration may be missing or the snapshot/model may be inconsistent; inspect and resolve that before delivery.

## Canonical EF Core migration procedure (copy-ready)

Use this single procedure for every future Development schema change. It covers CREATE → PREFLIGHT → INSPECT → REVIEW SQL → HASH → APPROVE → APPLY → VERIFY. Do not create an alternative execution path.

### Environment initialization (required before every gate invocation)

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:NEXUS_EF_DEVELOPMENT_CONNECTION_STRING = '<retrieve securely; do not paste into chat or source control>'
Set-Location 'C:\Projects\pmo'
```

The EF Database Gate also validates these variables and refuses any value other than `Development`. The offline `model` check does not require a connection string.

### CREATE (only when a model change requires a new migration)

```powershell
dotnet restore apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --ignore-failed-sources --disable-parallel -m:1
dotnet build apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --no-restore -m:1
dotnet ef migrations add <MigrationName> --project apps/api/src/NEXUS.Persistence/NEXUS.Persistence.csproj --startup-project apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --context NEXUS.Persistence.AppDbContext --output-dir Migrations --no-build
dotnet build apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --no-restore -m:1
```

### PREFLIGHT (read-only, host-side)

```powershell
.\scripts\dev\db-preflight.ps1
```

The wrapper generates a unique pending SQL artifact under `artifacts/ef-review/` only when migrations are pending and writes a sanitized JSON report. If the model check fails, preflight stops before inspection and no SQL artifact is generated or reused.

### INSPECT / STATUS

```powershell
.\scripts\dev\db-migrations-status.ps1
```

### REVIEW SQL

Open only the SQL file path printed by the successful preflight for the current run. Review ordering, data effects, drops, destructive type changes, constraints, indexes, and transaction boundaries. Do not review, hash, approve, or apply an artifact from a previous or failed run.

### HASH

```powershell
Get-FileHash -Algorithm SHA256 'artifacts/ef-review/<generated-pending-file>.sql'
```

### APPROVE / APPLY (only after explicit Product Owner authorization)

```powershell
.\scripts\dev\db-apply-migrations.ps1 `
  -ReviewedSqlPath 'artifacts/ef-review/<generated-pending-file>.sql' `
  -ApprovedSha256 '<reviewed-64-character-SHA256>' `
  -AuthorizationReference '<active Product Owner task or conversation reference>' `
  -ConfirmDevelopmentApply
```

### VERIFY

Confirm from the gate output and sanitized report: `EF Model synchronized: Yes`; target identity matches `Shady` / `NexusPmo`; pending migrations = 0; physical schema verified; applied migration names. Then run the focused persistence/integration tests for the changed model.

### Permanent safeguards for this procedure

- Initialize `ASPNETCORE_ENVIRONMENT` and `DOTNET_ENVIRONMENT` explicitly; never rely on a default or inherited value.
- Stop immediately when any mandatory step fails. Do not continue to review or apply SQL after a failed model check, inspection, hash comparison, or gate rejection.
- Use unique or freshly regenerated SQL artifacts. The preflight wrapper refuses to overwrite an existing artifact path.
- Apply regenerates the pending SQL from the current target and rejects the operation unless its hash exactly matches the reviewed file hash. A mismatch is a hard stop, not a warning.
- Never copy a hash from an error message, console output, or report as authorization. The authorization hash must be calculated from the reviewed SQL file.
- Treat offline `model` output as model/snapshot verification only. It is not evidence of live database identity, history, or schema.
- Direct `dotnet ef migrations script` output is reference-only and must never be used as an approval artifact for Development apply.
- Deferred application must be reported as **Database Migration Pending**, not Complete.

## Read-only Development inspection

Populate the connection variable through a secret manager or the current PowerShell process, and set the same Development environment used by the API. Do not put credentials in source control, a command argument, or a log.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:NEXUS_EF_DEVELOPMENT_CONNECTION_STRING = '<retrieve securely; do not paste into chat>'
# Use a fresh, unique --script-output path for every run; the gate overwrites only on a successful generation.
.\scripts\database\ef-database-gate.ps1 inspect --environment Development --expected-server '<SERVERPROPERTY(''ServerName'')>' --expected-database '<DB_NAME()>' --script-output 'artifacts/ef-review/development-pending-<timestamp>.sql'
```

The tool compares its connection endpoint with the effective Development API configuration, opens that explicit target, and queries the server identity directly. It never prints the connection string. A server alias is not accepted as proof: use the value returned by `SERVERPROPERTY('ServerName')` for the expected server and the value returned by `DB_NAME()` for the expected database. The reported database ID is also included in the verification output.

The inspection reports current model/snapshot status, all source migration IDs, applied history IDs, migrations pending in the target, history entries missing from source, and missing mapped tables, columns, foreign keys, and indexes. A non-zero exit code means a required check failed or remains pending.

## Restricted execution and Windows host access

If a restricted execution context fails before SQL identity/history checks with `TCP Provider: Access is denied`, treat that as an execution-context boundary. Do not repeatedly probe `localhost` or a SQL Server alias, change firewall/authentication settings, or run manual SQL. Run the same EF gate's read-only `inspect -NoBuild` once from an approved Windows host context, with the Development environment and the securely loaded connection string that matches the API's effective Development configuration. Verify `SERVERPROPERTY('ServerName')`, `DB_NAME()`, and database ID from the successful gate output. Only that successful inspection may establish the target or produce target-specific pending SQL. Review and apply through the same EF gate; if host-side execution is unavailable or fails, stop without writing and report the blocker. A host-side inspection succeeded on 2026-11-12 for Development `Shady` / `NexusPmo` (database ID 5), while restricted execution returned `Access is denied`; this verified that the SQL Server endpoint was reachable from the approved host context.

## Review and apply

Inspect the generated SQL file, migration ordering, and data effects. Pay special attention to drops, destructive type changes, data updates, constraints, indexes, and transaction boundaries. The review script is based on the target database's last applied migration.

Only after explicit Development authorization, verified target identity, a clean model/snapshot, and review of the exact SQL file, use:

```powershell
Get-FileHash -Algorithm SHA256 'artifacts/ef-review/development-pending-<timestamp>.sql'
.\scripts\database\ef-database-gate.ps1 apply --environment Development --expected-server '<verified-server>' --expected-database '<verified-database>' --script-file 'artifacts/ef-review/development-pending-<timestamp>.sql' --approved-sha256 '<reviewed-sha256>'
```

Apply refuses Production and a different SQL file. It applies only the reviewed pending migrations, then rechecks EF history and the physical schema. A backup may be created when explicitly requested, but it is not a Development/UAT apply prerequisite; absence of a backup directory or `RESTORE VERIFYONLY` result must not block apply.

When a backup is explicitly requested, pass the optional `--backup-directory` argument to `apply`; the gate creates and verifies that backup before applying. Omitting the argument is the normal Development/UAT path.

## Product Owner-authorized Development reset

The reset procedure remains limited to the disposable Development database on SQL Server `Shady`, database `NexusPmo`. It applies to both an existing database being reset and this exact database being confirmed absent and created. It does not authorize Production, another server/database, a UAT process environment, or any other database write. The general optional-backup policy does not grant reset authority.

Before invoking reset, the active task/conversation must contain explicit Product Owner authorization to destroy and recreate `Shady/NexusPmo`. The command's `--product-owner-authorization` value must point to that authorization; the reference and `--confirm-reset-database NexusPmo` are additional safeguards, not substitutes for actual Product Owner approval. The operator must set `ASPNETCORE_ENVIRONMENT=Development`, supply `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING` securely, and use the same endpoint configured as the API's effective Development connection. Read the connected server and database identity first; do not assume `Shady` from a host alias. The reset command must verify `SERVERPROPERTY('ServerName')`, `DB_NAME()`, database ID, and the active API Development endpoint before reset and recheck the target immediately before deletion or creation.

After source changes and the required final build are complete, confirm the EF model/snapshot is synchronized, inspect the complete empty-database EF migration path, ensure the secure Admin bootstrap secret is available, and build the Development API so the repository tool can run its seed entrypoint before any deletion. Then run:

```powershell
.\scripts\database\ef-database-gate.ps1 reset -NoBuild --environment Development --expected-server Shady --expected-database NexusPmo --confirm-reset-database NexusPmo --product-owner-authorization '<active task or conversation reference>'
```

No backup or `RESTORE VERIFYONLY` is required for this exact authorized reset workflow. The reset command remains hard-restricted in source to verified `Shady/NexusPmo`; it refuses another target, wrong environment, a connection that differs from the API Development endpoint, and a target that exists but cannot be opened. It checks model readiness and migration availability, generates the complete empty-database EF migration path before deletion, re-verifies the live target, uses EF `EnsureDeletedAsync` for an existing target, creates only the confirmed target when absent, and applies every repository migration through EF `MigrateAsync`. It never resets `__EFMigrationsHistory`, removes migrations, or rewrites an applied migration. Destructive reset still requires explicit Product Owner authorization and all target, model, migration, and schema checks.

For an existing, verified reset target, a backup may be requested with `--backup-directory <SQL-Server-accessible-directory>`. The gate creates and verifies that optional backup before deletion. Omit the option when no backup is requested; the backup is not a prerequisite. A backup cannot be created for a target confirmed absent. Requesting a backup does not replace Product Owner authorization or any reset safety check.

The reset command preflights both the secure `NEXUS_ADMIN_BOOTSTRAP_PASSWORD` and the prebuilt Development API before deletion. After EF migrations and schema verification, it invokes the API's Minimal Seed entrypoint `--seed-minimal-only` twice, passing the already verified Development connection through process environment (never command-line credentials), then rechecks target identity and schema. Minimal Seed enforces one active Admin and rejects non-system business records; the tool also verifies that table row counts remain stable across the second pass. A non-zero seed exit, changed row counts, or failed post-seed check makes the reset command fail. Record both seed passes, row-count stability, and post-seed checks in the completion report. This required seed step is part of the same Product Owner-authorized reset. Other authorized Development/UAT writes, including standalone seed or migration operations, do not require a backup; the optional-backup policy does not authorize destructive changes. Never run the reset command against another database or Production.

Minimal Seed and Demo Seed are separate modes. `--seed-minimal-only` creates the system baseline and approved reference catalogs without business/demo records. The existing `--seed-only` Demo Seed behavior remains unchanged and is not used for a clean Admin-bootstrap reset.

**Historical policy-maintenance blocker (superseded):** At the time of this record, a protected repository agent policy conflicted with the reset procedure documented here. This note explains the historical readiness status only; current safeguards are defined by repository instructions.

If inspection finds pre-existing physical-schema drift, the normal apply path stops. A user-authorized exception for unrelated drift must pass the exact `Baseline schema drift SHA-256` printed by inspection as `--allow-unrelated-baseline-drift-sha256`. The tool accepts it only if the hash still matches and none of the affected `dbo` tables occur in the pending migration SQL. After application, the tool reports both overall physical-schema status and whether the pending migration's schema is verified with only the acknowledged baseline drift remaining; it does not repair or conceal those existing differences.

## Persistence tests and completion

After schema verification, run the focused persistence/integration tests for the changed model against an isolated Development/test database. Confirm the test target independently; do not use production data or an in-memory-only provider as SQL Server evidence. Capture the exact command and outcome.

Report these fields on every database-related task:

- EF Model synchronized: Yes/No.
- Pending migrations: count and names.
- Target environment and DB verified: Yes/No.
- Backup status: Not requested / Requested and completed / Requested but unavailable (backup status is not a Development/UAT operation gate).
- Migrations applied: names.
- Minimal Seed applied and idempotency verified (including row-count stability): Yes/No/Not run.
- Physical schema verified: Yes/No.
- Persistence tests: Passed/Failed/Not run.
- Remaining blockers.

Do not call the task fully complete when a required Development database update or verification remains blocked.

## Current lifecycle verification — 2026-11-12

- Read-only host-side `inspect -NoBuild` verified Development SQL Server `Shady`, database `NexusPmo`, database ID `5`, using the effective Windows-authenticated Development connection. The restricted execution attempt still fails before identity/history with `TCP Provider: Access is denied`.
- `20261111173858_AddProjectLifecycleCatalogs` is present in `__EFMigrationsHistory`; the target has 23 applied repository migrations and 0 pending. The offline model gate reports 23 repository migrations and `EF Model synchronized: Yes`.
- Overall physical-schema comparison remains `No` for 15 pre-existing differences on `IntegrationConnections`, `IntegrationWebhooks`, and `Programs`, fingerprint `7A6D2E074284D56438749F563AF4AEF3BD2DEE4E3687DC54F9ADF467C095E9FB`. No lifecycle migration is pending. No write, target-specific SQL generation, backup, seed, or persistence test occurred during this inspection.

## Development deployment record — 2026-11-11

- Requested action: apply pending Development migrations. Environment `Development`; live identity `Shady` / `NexusPmo`, database ID `5`.
- Offline `model` gate: 23 repository migrations; `EF Model synchronized: Yes`; no database connection.
- Read-only `inspect`: 22 applied, 1 pending (`20261111173858_AddProjectLifecycleCatalogs`). Target-specific SQL: `artifacts/ef-review/development-pending.sql`, SHA-256 `9806B75F63C93AFB4ADA5DE3F686C106DFEA5DD297E2C810A42CDF852F96B821`. Review found catalog table creates, bilingual seed inserts, legacy `LEGACY-*` mapping, additive project/Phase Gate ID columns, index/constraint replacements, and no `DROP TABLE` / `DELETE` / `TRUNCATE`.
- Pre-existing baseline drift (15 objects) fingerprint `7A6D2E074284D56438749F563AF4AEF3BD2DEE4E3687DC54F9ADF467C095E9FB` on `IntegrationConnections`, `IntegrationWebhooks`, and `Programs` only; disjoint from pending SQL. Apply used the documented unrelated-drift exception.
- Backup status: Not requested.
- `apply` exit 0: migration recorded; 23 applied; 0 pending; post-apply schema verified excluding the same acknowledged baseline drift. Minimal Seed not run. Persistence tests not run (available lifecycle tests use in-memory EF).

## Development deployment record — 2026-10-31

- The approved host-connected workflow verified SQL Server `Shady` (default instance), database `NexusPmo`, database ID `9`, state `ONLINE`. The Windows-authenticated connection reported `NTLM`.
- `scripts/database/ef-database-gate.ps1 model` passed (exit 0): seven repository migrations and no pending model changes. Before application, six migrations were present in `dbo.__EFMigrationsHistory` and only `20261031142036_AlignResourceProjectTeamsAndInvitations` was pending.
- The reviewed, target-specific SQL SHA-256 was `891E95751AE2A3A4EE2A6232D548541D5C6D4E5B6A145484764AE51D73B22303`. The migration preflight found zero duplicate active Resource-to-User groups, zero invalid Resource-to-User/Team links, zero ProjectMembers with a null Resource target, zero eligible legacy TeamMember insertions, and zero invitation rows. It does not constrain ProjectMember allocation totals.
- The last-applied model already had 13 physical differences, fingerprint `DC357F6C2FBB616E6E4781E8C9DD2E647996E61386844E7B8A189DD53543C5E0`: eight nullable text-format columns across PlanActivities and PlanWbsNodes; three nullable PlanningCalendars columns; and the two self-referential PlanActivities/PlanWbsNodes foreign keys. The migration SQL references none of those tables. The tool's explicit hash-bound exception checks that exact drift fingerprint and table disjointness, while preserving an overall physical-schema status of `No`.
- The tool created `C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup\NexusPmo-Development-20261031-173336-ff25cfdc03f847d9a0666d6af1b7f00a.bak` with `COPY_ONLY, CHECKSUM`; `RESTORE VERIFYONLY` passed before migration. The backup is on the same host volume as this disposable Development database.
- Application passed (command exit 0): migration 7 is recorded, seven total migrations are applied, none are pending, and the migration schema is verified excluding only the acknowledged 13 baseline differences. Direct SQL checks confirmed the new invitation columns, nullable ProjectMembers.ResourceId and TeamId, the exactly-one-target check constraint, two foreign keys, and three indexes. No invitation rows or TeamMembers rows were written; the migration contains no financial-table operation.
- The overlapping ProjectMember allocation remains: one Resource/Project group has three concurrent active rows totaling 120% (maximum single row 90%). No membership or financial row was reconciled or deleted. The 13 baseline schema differences also remain and require separate, data-reviewed remediation.
- API smoke passed using the existing compiled API assembly with Development initialization and hosted workers disabled: `/health` and `/swagger/v1/swagger.json` both returned HTTP 200. A fresh API build was stopped during the existing MPXJ/IKVM class scan after compatibility warnings; no compiler error was observed. No SQL Server-backed persistence test target was found, so persistence tests were not run; available matching tests use the in-memory provider. Full runtime/UAT and financial UAT were not run, and no email was sent.
- Command results: `model` exit 0; read-only `inspect` exit 2 because the known baseline drift remained; `apply` exit 0; post-apply SQL metadata checks exit 0; API smoke exit 0.

## Current reconciliation record — 2026-10-28

The repository then contained 19 migration source IDs, in order:

```text
20261010182322_NexusUpdatedBaseline
20261011205628_AddDirectoryIntegration
20261012103001_AddLocalPasswordLifecycle
20261012110228_AddNotificationDeliveryFoundation
20261015131616_AddAdministrativeManagerHandover
20261015150917_MakeHandoverSnapshotPreviousManagerNullable
20261018084343_AddProjectPublicId
20261018190000_AddMultiPlanReportingConfiguration
20261021120000_AddProjectChangeRequestApprovals
20261021130000_AddChangeRequestApprovalModelIndexes
20261021160000_AddGovernanceRecoveryPublicIds
20261026121145_AddResourceWorkingCapacity
20261026140000_AllowPlanAssignmentReassignmentAfterDelete
20261026150000_AddWorkingCalendarDayRules
20261026160000_AddPlannedPaymentLifecycle
20261026170000_AddPaymentTenantScopedRelationships
20261027174321_AddStatusReportActionsSupport
20261027202734_AddStatusReportPaymentSnapshotFields
20261028084103_AddMeetingActionWorkItemLink
```

The six resource/calendar/payment/status-report migrations dated 2026-10-26 and 2026-10-27 are concurrent working-tree files and were not modified during Meetings reconciliation. The Meetings migration sorts after them. The source snapshot was updated only for nullable `MeetingAction.WorkItemId`, its index, and its optional `SET NULL` relationship; the new migration target model matches that snapshot. A source comparison against the prior migration designer showed only these expected Meetings additions.

Three existing migrations have inline `[Migration]`/`[DbContext]` attributes but no `.Designer.cs` and no `BuildTargetModel` override: `20261018190000_AddMultiPlanReportingConfiguration`, `20261021120000_AddProjectChangeRequestApprovals`, and `20261021160000_AddGovernanceRecoveryPublicIds`. Their `Up`/`Down` operations are explicit; EF discovers them and generated the full SQL script. The checked-in history does not establish whether the missing target models were intentional. Without those models, a physical-schema baseline cannot be reconstructed from one of these migrations if it is the latest applied migration. Do not synthesize designers from the current snapshot or alter these migrations without ownership and historical-model evidence.

### 2026-10-28 migration readiness review

- **MSBuild:** In the restricted process environment, the EF tooling build exits 1 with zero warnings and zero compiler errors while resolving project-reference target frameworks. The same metadata target succeeds outside that isolation. A full EF tooling build also succeeds outside isolation with `BaseOutputPath` directed to a separate temporary directory: 0 warnings, 0 errors. This matches the repository's earlier diagnosis of IKVM `MavenReferenceItemImport` task-host creation failing inside isolation (`MSB4216`/`MSB4027`); no source-level bypass or unrelated project change was made. SDK `10.0.302`, EF tooling `10.0.10`, and the `net10.0` projects are compatible in that successful build.
- **Model and discovery:** The offline EF tooling enumerated all 19 migrations and reported `EF Model synchronized: Yes`; `dotnet ef migrations has-pending-model-changes` reported no model changes. These commands did not open a database connection. The latest migration designer target model and snapshot source were previously compared; the only difference from the prior migration target was the expected MeetingAction WorkItem link.
- **Generated deployment artifact:** `scripts/database/repository-migrations-through-20261028084103.idempotent.sql` was generated by EF Core 10.0.10's SQL Server provider, from migration `0` through all 19 repository migrations. It has one history guard/insert per migration and SHA-256 `02694590AE819FDC4592255EBC414D0950E2F5C383C96FE42FC725C3BC25BE7B`. This is a repository-wide idempotent script, not a target-specific pending-migrations script. It was not executed.
- **SQL and data review:** The repository-wide script includes nullable `MeetingActions.WorkItemId`, `IX_MeetingActions_WorkItemId`, and `FK_MeetingActions_WorkItems_WorkItemId` with `ON DELETE SET NULL`; existing action rows receive no default or backfill. The only other MeetingActions FK is its existing `MeetingId` cascade. The generated model has no cascade FK path joining the Meetings and Work Items principals. Repeated object names in the script are accounted for by explicit drops before index/FK replacement; there are no duplicate table definitions or unresolved same-name creations. The script has no `DROP TABLE` or `DELETE` statements. Historical `20261012110228_AddNotificationDeliveryFoundation` drops `Notifications.Channel` without a backfill, hashes device tokens, deactivates duplicate active tokens, soft-deletes duplicate preferences, and narrows `NotificationPreferences.Type` from `nvarchar(max)` to `nvarchar(64)`. That migration is already applied to the verified Development database, so these operations are not in a pending script. The database no longer has `Notifications.Channel` and has `NotificationDeliveries.Channel`. Other historical migrations backfill project public IDs and status-report dates. Five existing indexes are dropped as part of later index-definition changes.
- **Historical metadata:** The three migrations above remain unchanged. EF discovery and script generation succeeded. Their missing target models remain a limitation if one is the latest applied migration; on the verified Development target the latest applied migration is `20261028084103_AddMeetingActionWorkItemLink`, which has target metadata, and the tool's applied-model schema comparison passed. No historical model was fabricated.
- **Development target and migration history:** Using the configured `localhost` alias, SQL Server reported actual identity `Shady` / `NexusPmo` (database ID 19). The exact-identity read-only inspection found all 19 repository migrations applied, 0 pending, no history-only migrations, and `EF Model synchronized: Yes`. Physical schema comparison found zero missing objects against both the latest applied model and current repository model. No database write or migration application occurred. No target-specific pending SQL file was generated because there are no pending migrations; the repository-wide script above remains a reference only.
- **Backup and recovery:** SQL Server `msdb` contains eight full-backup records; the latest is copy-only with checksums, completed 2026-10-28 11:52, and SQL Server confirmed that its media file exists. The latest media and database data files are both on `C:\`, so an independent backup is not established. The database uses `FULL` recovery, with no differential or log backups recorded in `msdb`; point-in-time recovery and log-growth risk remain. No backup was created, restored, or integrity-verified.

**Status: BLOCKED for deployment; no migrations are pending.** The requested database preflight is complete: target identity, history, and physical schema are verified, and the historical Notifications migration is already applied. There is no target-specific SQL to apply. The backup file found is on the same volume as the database files; this is historical backup evidence and does not block Development/UAT work under the current policy. Before any future schema deployment, verify the current target and address the physical-schema drift. The previously noted log-backup chain is an operational observation, not a Development/UAT backup prerequisite. No migrations were applied during this task. Persistence/functional/API/integration/UI/browser tests were not run.

**Historical policy note (2026-11-04):** This deployment status records the backup requirement for an ordinary migration deployment under the policy then in force. That requirement is historical and is superseded by the current Development/UAT optional-backup policy at the top of this document. Production backup and recovery safeguards remain mandatory.

### 2026-10-28 Meetings lifecycle refinement

Two Meetings migrations were added after the reconciliation record above: `20261028130000_AddMeetingLifecycleTracking` and `20261028150000_SeparateMeetingInvitationAndAttendanceStatuses`; the repository now contains 21 migration source IDs. The latter adds nullable `MeetingParticipants.InvitationResponseStatus`, copies legacy `Pending`/`Accepted`/`Declined`/`Tentative` values into it, and maps legacy `Attended` to `Present` in `AttendanceStatus`. It preserves participant rows. A rollback after both fields have new values can retain only one status in the legacy column; the migration chooses actual attendance when present, otherwise the invitation response.

The model snapshot and the new migration target model were synchronized in source for `InvitationResponseStatus` with nullable `nvarchar(20)` mapping. EF tooling was not run for this refinement, so model synchronization has not been verified by EF. No migration SQL was generated, no database was connected to or written, no migration was applied, and no tests/builds were run. The earlier Development identity/history/backup evidence above predates these migrations and is not current deployment verification. Review both Meetings migrations through the documented workflow before applying them; a current Development history check, SQL review, physical schema verification, and focused persistence tests remain outstanding. A backup may be created when explicitly requested, but it is not a Development/UAT prerequisite.

## EF CLI without the MPP adapter

The API runtime references `NEXUS.Modules.ProjectPlanning.MppAdapter` to keep
`.mpp` parsing available. The design-time executable references only
`NEXUS.Persistence`, whose ProjectPlanning dependency contains format-neutral
contracts and no MPXJ package. Its factory uses a closed-port model-only
connection string and does not open a connection. The executable does not
start the API, initialize a database, apply migrations, or seed data.

From the repository root, restore and build the isolated graph first. In
restricted shells, serial MSBuild avoids the worker-pool failure seen with the
default build. `--no-build` is used only after a successful build of the exact
current sources; rebuild after scaffolding before generating SQL.

```powershell
dotnet restore apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --ignore-failed-sources --disable-parallel -m:1
dotnet build apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --no-restore -m:1
dotnet ef migrations add <MigrationName> --project apps/api/src/NEXUS.Persistence/NEXUS.Persistence.csproj --startup-project apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --context NEXUS.Persistence.AppDbContext --output-dir Migrations --no-build
dotnet build apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --no-restore -m:1
dotnet ef migrations script --idempotent --project apps/api/src/NEXUS.Persistence/NEXUS.Persistence.csproj --startup-project apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --context NEXUS.Persistence.AppDbContext --output scripts/database/<MigrationName>.sql --no-build
```

Use the repository database workflow for target inspection or deployment. Do not
use `dotnet ef database update` through the model-only design-time host. The
tool's `model` action remains the offline model/snapshot check, and its
inspection/deployment actions retain explicit environment, target, migration,
and schema verification. Development/UAT deployment actions do not require a
backup; Production remains prohibited here and retains its mandatory backup and
recovery requirements in any separately approved workflow.

## Microsoft guidance

Microsoft recommends reviewing generated SQL scripts and using controlled migration deployment, rather than treating an application run as sufficient deployment evidence. The EF migration-history table is the record of migrations applied by EF.

- [Managing EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing)
- [Applying EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
- [Migrations history table](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table)

## Restricted-shell Development recheck — 2026-10-31 (superseded by the host-connected recheck below)

The repository tool built its inspection host, then failed to connect to the expected `Shady` / `NexusPmo` Development endpoint with SQL Server Named Pipes provider error 40 (server unavailable). No database identity query or `__EFMigrationsHistory` query completed. No backup was checked or created, no SQL was executed, and no migration was applied. Current applied/pending migration counts, physical schema, and backup state therefore remain **unverified**; the user-reported six applied migrations are not treated as observed evidence.

The offline migration history audit found the deleted `20261026140000_AllowPlanAssignmentReassignmentAfterDelete.cs` and Designer in committed `HEAD` and in both archived 2026-10-29 migration bundles; their checksums match the archived manifests. The migration changes the PlanAssignment unique index to a filtered active-row index, and the replacement `20261029155617_InitialFopxPmBaseline` contains the same filtered index operation. This supports the documented baseline-consolidation path, but it does not establish the current Development database lineage. Older workflow records describe the legacy 19-migration history, while the 2026-10-30 Sprint 4 record describes a new baseline lineage and different database ID. The current shared worktree has broad uncommitted migration replacement/deletion changes. No historical migration was restored, removed, renumbered, or rewritten during this recheck; reconcile current target history and migration ownership before deployment.

The 2026-10-31 alignment migration remains source-only and unapplied. Its reviewed offline SQL SHA-256 is `310C8B958A5955372FE2868CF683C313447F376315A3CC3E664357189BF1C0CE`; it contains preflight failures and a cutover-date copy from legacy `Resources.TeamId` to `TeamMembers`. The migration retains the old `Resources.TeamId` and User professional-profile columns; no profile-value backfill is included. Preserve the legacy User values. Any backfill into Resource requires a row-level comparison against the actual Development data and an approved source-of-truth rule before an additive migration is prepared. Never remove the compatibility columns before usage and data impact are verified.

## Host-connected Development recheck — 2026-10-31

The restricted execution shell could not resolve the local SQL Server endpoint. A host-connected Windows PowerShell session then verified the intended target without changing SQL Server configuration: host `SHADY`, default instance `MSSQLSERVER` (service running, TCP 1433 listening), Windows identity `SHADY\shady`, and integrated-auth connection to `Shady` / `NexusPmo`, database ID `9`, status `ONLINE`. The effective API Development setting is `Server=localhost;Database=NexusPmo;Trusted_Connection=True`; in the host context, the repository tool connected through that configured endpoint and verified `SERVERPROPERTY('ServerName') = Shady`. The earlier Named Pipes error is therefore an execution-environment isolation issue, not evidence of database corruption or a stopped service. The SQL authentication scheme observed was NTLM; the original login was the configured Microsoft account identity.

The actual history contains six applied migrations, in order: `20261029155617_InitialFopxPmBaseline`, `20261029181208_ProjectPaymentRequestPersistence`, `20261029183557_AddTenantRegistry`, `20261030122718_AddProjectAnnualBudgetFoundation`, `20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation`, and `20261031000210_AddFinancialPersistenceSprints3to6`. The repository contains seven migrations; `20261031142036_AlignResourceProjectTeamsAndInvitations` is the sole pending migration. The deleted `20261026140000_AllowPlanAssignmentReassignmentAfterDelete` MigrationId is not in this history. The applied baseline's filtered unique PlanAssignment index is present in SQL Server with `IsDeleted = 0`, consistent with the historical baseline-consolidation evidence. No separate approval record for the consolidation was found, and no other live agent was present; broad shared-worktree changes still prevent inferring ownership from Git status alone. The historical files were not restored or rewritten.

The repository EF tooling had a false-negative schema comparison: it supplied the resolved default `dbo` schema to EF's table-mapping identifier even when the model mapping stores a null schema. This caused table-specific column mappings to disappear and the previous inspection to report zero missing current-model objects. The inspection tool now preserves EF's mapped schema and canonicalizes SQL Server's `timestamp` synonym to EF `rowversion`. The rebuilt inspection tool reports **EF model synchronized: Yes**, **13 objects/definitions differing from the last-applied model**, and **27 differing from the current repository model**. The 13 baseline differences are nullable text-format flags on `PlanActivities` and `PlanWbsNodes` (eight columns), three nullable `PlanningCalendars` columns, and two absent self-referencing foreign keys. The 14 additional current-model gaps are `ProjectMembers.ResourceId` nullability and `TeamId`, seven invitation consent/delivery columns, three indexes, and two foreign keys for the alignment. These baseline drifts block declaring the target schema ready; they are outside this P0 scope and were not altered.

The target-generated pending SQL is `C:\Users\shady\AppData\Local\Temp\pmo-ef-pending-details-54b24bff366941a0afb7f66b1c75fd8a.sql`, SHA-256 `891E95751AE2A3A4EE2A6232D548541D5C6D4E5B6A145484764AE51D73B22303`. The checked-in offline alignment SQL remains SHA-256 `310C8B958A5955372FE2868CF683C313447F376315A3CC3E664357189BF1C0CE`. Review found migration preflights for duplicate active Resource/User links, missing or cross-tenant User/Team references, and ProjectMembers with no assignment target. The migration adds the invitation fields, permits either Resource or Team ProjectMember targets, creates the tenant-safe Resource/User index and foreign keys, and copies valid legacy Team links at cutover. It includes an `Untracked` delivery-status update, but there are no invitation rows. It does not delete tables, columns, or rows; the `DROP CONSTRAINT` operations only replace defaults while changing column definitions. The old `Resources.TeamId` and legacy User profile columns are retained.

Read-only target data checks found: 4 Users, 2 Resources (one linked, one unlinked), 1 Team, 0 TeamMembers, 0 non-null legacy `Resources.TeamId` values, 4 ProjectMembers, 0 ResourceAllocations, and 0 StakeholderInvitations. Three Users have no Resource. Duplicate Resource email/employee identifiers, duplicate User links, cross-tenant/missing User or Team references, invalid TeamMember dates, and ProjectMembers without a target all returned zero. One linked Resource/User pair has different legacy `JobTitle` values (no phone mismatch); neither value was changed or backfilled. One Resource/Project has three status-active ProjectMember rows with overlapping future ranges beginning 2026-11-01; their allocations total 120% (maximum 90%). They have distinct `Role` values and no `ProjectMemberRoles` associations. This is a pre-existing allocation/data-integrity blocker not handled by the migration's preflight; preserve these rows until an authorized reconciliation rule is established. `ResourceAllocations`, `WorkItemAssignments`, and `TimeEntries` each have zero rows; `AuditEntries` has 34 rows. No test records were created.

The latest full copy-only Development backup in `msdb` finished `2026-10-30 00:24:02`; SQL Server `RESTORE VERIFYONLY` reported “The backup set on file 1 is valid.” This verifies that existing backup media, not a fresh pre-deployment backup. No migration or SQL change was applied. A current deployment still requires a separately authorized migration and disposition of the physical-schema drift and duplicate ProjectMember allocation before runtime UAT. A backup may be created when explicitly requested, but it is not a Development/UAT prerequisite.
