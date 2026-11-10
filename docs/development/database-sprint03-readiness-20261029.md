# Database Sprint 03 — Baseline Validation & Development Deployment Readiness

**Review date:** 2026-10-29  
**Status: BLOCKED — SPECIFIC TECHNICAL REASONS.** The baseline and current EF model were reviewed at source level. The configured Development SQL Server could not be reached, so its identity, version, migration history, physical schema, and backup state remain unverified. No database was created or modified, and no migration or seed was applied.

## Source and EF model

- Context: `NEXUS.Persistence.AppDbContext`; migration assembly: `NEXUS.Persistence`; runtime host: `NEXUS.Api`; provider: SQL Server.
- The active source currently contains two migrations, in order:
  1. `20261029155617_InitialFopxPmBaseline`
  2. `20261029181208_ProjectPaymentRequestPersistence`
- The latest offline `scripts/database/ef-database-gate.ps1 model` run completed successfully and reported **2 repository migrations** and **EF Model synchronized: Yes**. It did not open a database connection.
- The legacy migration archives remain in `docs/development/archives/`. This review did not modify the archives, migration files, or model snapshot.
- EF source files changed while the review was in progress. The final model check was run after the current two-migration set and snapshot were present. Re-run the offline model check once the shared migration owner freezes the source for delivery.

## Coverage and schema review

| Area | Baseline/current source finding | Status |
|---|---|---|
| Tenant and Organization | `Organizations` exists and has tenant-scoped ownership keys. `TenantId` is a scalar on records, but there is no `Tenant` domain type, `DbSet<Tenant>`, or `Tenants` table. The approved distinct Tenant → Organization hierarchy is not represented relationally. | **BLOCKED** |
| Platform/Tenant/Organization settings | `ConfigurationSettings` and `OrganizationSettingsGrants` are in the baseline. Settings enforce scope/owner rules, typed values, and active scope-specific unique indexes. Organization settings reference `Organizations(TenantId, Id)` with `RESTRICT`. | Present, subject to JSON engine requirement below |
| Organization Settings grants | Grants have composite tenant-safe FKs to Organizations and Users, a `CanManage` implies `CanRead` check, filtered active uniqueness, and SQL Server rowversion. | Present |
| Payments and approvals | The initial baseline contains payment obligations/settlements and generic approval requests/actions. `ProjectPaymentRequestPersistence` adds `ProjectPaymentRequestDetails`, frozen approver assignments, idempotency constraints, tenant-scoped composite FKs, and approval action linkage. | Present across both migrations; baseline-only SQL is stale for the current chain |
| Projects, meetings, calendars, notifications, audit | The baseline creates these module schemas, including meeting actions/minutes, working calendar rules, notification delivery tables, and audit tables. | Present in source |

`ConfigurationSettings` has database checks for Platform/Tenant/Organization ownership, `ValueType`, and JSON validity. Platform, Tenant, and Organization setting uniqueness uses separate filtered indexes, avoiding nullable composite-key ambiguity. `OrganizationSettingsGrants` uses active-row filtering and restricted deletes; the payment-request migration uses tenant-scoped alternate keys and foreign keys, with cascade only for request-owned rows and restricted deletes for selected users/projects/activities/reversal references.

### Missing Tenant model disposition

The missing `Tenant` entity is an approved-architecture gap, not a reason to blindly regenerate the baseline. The proposed validation database has not been created, but the configured Development target and any other applied history could not be inspected. After source ownership is frozen and histories are verified:

- If no database has applied `InitialFopxPmBaseline`, implement the Tenant model and its tenant-safe relationships, then correct the initial baseline and regenerate its Designer, snapshot, and complete SQL before first application.
- If the baseline has been applied anywhere, leave that migration immutable and add a reviewed Tenant schema migration after it.

The `ProjectPaymentRequestPersistence` migration is already present as the post-baseline schema change; keep it in the ordered chain and deploy both migrations from `0` to an empty validation database unless a separate product decision explicitly consolidates it before any application.

The settings mapping currently emits `ISJSON([ValueJson], VALUE) = 1`. Microsoft documents the `json_type_constraint` argument, including `VALUE`, as introduced in SQL Server 2022. The constraint accepts top-level scalar JSON as well as arrays and objects; the older one-argument `ISJSON` check accepts only objects or arrays, so it is not a drop-in replacement. The configured Development engine version and database compatibility level could not be inspected. Require SQL Server 2022 or later for this constraint, or deliberately revise the database constraint while retaining typed server-side validation and the existing JSON serialization format. See [Microsoft ISJSON documentation](https://learn.microsoft.com/en-us/sql/t-sql/functions/isjson-transact-sql).

The baseline stores native SQL Server `rowversion` for the new Settings rows and several concurrency-managed entities. Other legacy byte-array `RowVersion` properties are represented as `varbinary(max)` in the baseline; verify their intended concurrency semantics before treating every such property as a native SQL Server rowversion.

## Generated SQL artifact

`scripts/database/InitialFopxPmBaseline.sql` is an idempotent SQL Server script for the initial baseline only. Its SHA-256 is:

```text
1F9F6C5BBFA2839AB171295DAC8A24A9BEBB79FCCD152ED60D7EBB8AC34134E0
```

Static inspection found 172 `CREATE TABLE` statements (171 application tables plus the EF history table), 342 `CREATE INDEX` statements, and no `DROP`, `DELETE`, `TRUNCATE`, or `UPDATE` statements. Its sole `INSERT` records the migration in `__EFMigrationsHistory`; it is not business seed data. The script does **not** include the later `ProjectPaymentRequestPersistence` migration. Generate and review a fresh complete script from migration `0` after the shared source is frozen; do not deploy the baseline-only script as the current full schema.

The payment-request migration adds two tables, composite tenant-safe keys/FKs, unique and filtered indexes, and check constraints. It also narrows `ApprovalRequests.Type`/`Status` from `nvarchar(max)` to `nvarchar(450)`, converts that table's rowversion from `varbinary(max)` to native `rowversion`, and adds an approver-assignment link. These operations must be reviewed in the complete generated SQL. They are intended for the new empty validation database; do not apply the consolidated source chain to the existing legacy database.

## Development target and database inspection

The effective checked-in Development endpoint is `localhost` / `NexusPmo` using trusted authentication. The historical physical target `Shady` / `NexusPmo` is not established by that alias alone. No `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING` or `ASPNETCORE_ENVIRONMENT` override was present when this review began.

The repeatable read-only gate was run with the Development connection sourced from `appsettings.Development.json` and expected identity `Shady` / `NexusPmo`. The gate build succeeded with 0 compiler errors and 7 `NU1900` warnings because NuGet vulnerability metadata could not be fetched. Opening the configured SQL Server failed with a network/instance error before the identity or schema queries ran.

Therefore:

- Actual server and database identity: **Not verified**.
- SQL Server version and database compatibility level: **Not verified**.
- Existing schema and `dbo.__EFMigrationsHistory`: **Not verified**. Source contains two migrations; target pending migration count/names are **unknown**.
- Backup availability, location, and recoverability: **Not verified**.
- Migrations applied by this execution: **None**. Historical application state remains unknown.
- Physical schema verification: **Not run**.
- Database writes, database creation, migration application, and seed execution: **None**.

### Read-only preflight SQL

Run only after a connection to the intended server is available. Capture these results without connection credentials:

```sql
SELECT
    CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS ServerName,
    CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS ProductVersion,
    CONVERT(int, SERVERPROPERTY('ProductMajorVersion')) AS ProductMajorVersion,
    DB_NAME() AS DatabaseName,
    DB_ID() AS DatabaseId,
    d.compatibility_level AS CompatibilityLevel,
    d.recovery_model_desc AS RecoveryModel
FROM sys.databases AS d
WHERE d.database_id = DB_ID();

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    PRINT 'dbo.__EFMigrationsHistory is absent';
ELSE
    SELECT MigrationId, ProductVersion
    FROM dbo.__EFMigrationsHistory
    ORDER BY MigrationId;

SELECT TOP (20)
    b.database_name,
    b.backup_start_date,
    b.backup_finish_date,
    b.type AS BackupType,
    b.is_copy_only,
    b.has_backup_checksums,
    m.physical_device_name
FROM msdb.dbo.backupset AS b
JOIN msdb.dbo.backupmediafamily AS m ON m.media_set_id = b.media_set_id
WHERE b.database_name = DB_NAME()
ORDER BY b.backup_finish_date DESC;
```

Backup history is only evidence of recorded media, not recovery proof. Under the current Development/UAT policy, a backup is optional and its absence does not block `apply`. When explicitly requested, `--backup-directory` makes the gate create a unique copy-only backup and run `RESTORE VERIFYONLY`; otherwise omit that option. Target identity, reviewed SQL/hash, migration, and schema checks remain required.

## Isolated validation procedure — not executed

1. Freeze the EF owner/source state. Resolve the missing Tenant entity/schema and decide the SQL Server 2022 JSON constraint requirement before treating the model as an approved complete architecture.
2. Independently verify the SQL Server identity, engine version, compatibility level, access, and recovery path. Confirm the target is Development and is isolated from `NexusPmo` and Production.
3. Only after separate Product Owner authorization, create the new empty database `NexusPmo_BaselineValidation` on the independently verified Development instance:

   ```sql
   CREATE DATABASE [NexusPmo_BaselineValidation];
   ```

   Do not drop, repurpose, or migrate `NexusPmo` as part of this 2026-10-29 Sprint 03 baseline-validation task.

   **Policy supersession note (2026-11-04):** This task-specific restriction remains valid for its baseline-validation workflow. A later repository policy permits a no-backup reset only for an explicitly Product Owner-authorized Development `Shady/NexusPmo` reset after exact target verification; it does not authorize this task to use another baseline target or change Production.
4. Set process-only Development connection overrides through a secure source. Do not place credentials in source files, command arguments, or logs:

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = 'Development'
   $env:ConnectionStrings__DefaultConnection = '<securely supplied connection to the verified validation database>'
   $env:NEXUS_EF_DEVELOPMENT_CONNECTION_STRING = $env:ConnectionStrings__DefaultConnection
   ```

5. Verify the connection points at the isolated database and the engine reports the expected server name:

   ```powershell
   .\scripts\database\ef-database-gate.ps1 inspect --environment Development --expected-server '<SERVERPROPERTY ServerName>' --expected-database 'NexusPmo_BaselineValidation'
   ```

   On a newly created empty target, pending migrations are expected at this stage; the pre-apply inspection is not a synchronization pass.

6. Generate the full pending SQL for review from the empty database:

   ```powershell
   .\scripts\database\ef-database-gate.ps1 script --environment Development --expected-server '<verified server name>' --expected-database 'NexusPmo_BaselineValidation' --script-output 'scripts/database/NexusPmo_BaselineValidation.sql'
   ```

   Review every table, key, FK, index, check constraint, `rowversion`, `ALTER COLUMN`, delete action, and seed/data operation; record the exact SHA-256.

7. Database application requires a separate Product Owner authorization. Only then use `apply` with the exact reviewed file/hash. A backup is optional for Development/UAT; supply `--backup-directory` only when an optional backup was explicitly requested:

   ```powershell
   .\scripts\database\ef-database-gate.ps1 apply --environment Development --expected-server '<verified server name>' --expected-database 'NexusPmo_BaselineValidation' --script-file 'scripts/database/NexusPmo_BaselineValidation.sql' --approved-sha256 '<reviewed SHA-256>'
   ```

8. Re-run `inspect` and confirm the two expected migrations are applied, there are no history-only or pending migrations, the current model is synchronized, and the physical schema matches. Run persistence tests only after separate authorization; they were not run in this sprint.

The gate restricts `inspect`, `script`, and `apply` to the explicitly named Development environment and verifies actual server/database identity before database inspection or writes. The API's Development `Program.cs` previously migrated and seeded on ordinary startup. It has now been changed so ordinary Development startup skips database initialization; API startup no longer calls `Database.MigrateAsync()`. Explicit seed options do not migrate and must only be run against an already-migrated schema with the validation-database connection configured. Migration application remains the gate's explicit operation. The API itself was not started.

## Provisioning and application readiness

- `SeedDataProvider` has deterministic Development/demo identity and reference-data seeding, including a fixed demo tenant GUID and a hardcoded demo password. It is not a secure first Platform Administrator bootstrap. Do not put that password in a migration or use it for a shared environment.
- No relational `Tenant` entity exists to provision, and a separate secure administrator/initial Tenant/Organization provisioning path is not present. Required role/permission initialization and reference defaults need an idempotent, credential-free path. Keep demo data separate from that bootstrap.
- The runtime Development API configuration still points at `localhost` / `NexusPmo`; no active connection settings were changed. For validation UAT, point the process to `NexusPmo_BaselineValidation` and launch with `--skip-development-database-initialization`. No Production connection or service was used.
- The API build after the startup safety edit was attempted but failed in `NEXUS.Modules.ProjectPlanning.MppAdapter` while MSBuild could not start the IKVM `MavenReferenceItemImport` task host (`MSB4216` / `MSB4027`). The EF gate/persistence build succeeded; API compilation after the `Program.cs` edit is not verified. No automated tests, browser UAT, or service restarts were run.

### Runtime settings coverage

| Setting | Current runtime consumer/source | Remaining gap |
|---|---|---|
| Default language | `TenantRuntimeSettingsBridge` → `I18nProvider`; an explicit user locale choice takes precedence. | Organization language requires active Organization context. |
| Timezone | React date/time formatters receive the tenant effective value. | Organization-specific timezone is not selected by global runtime formatting. |
| Currency | `I18nProvider.formatCurrency` uses the tenant currency options/API when a caller omits currency. | Organization-specific currency is not selected; Platform currency is disallowed. |
| Date/time formats | Shared React formatters consume tenant effective values unless a caller supplies explicit formatter options. | Organization context is not propagated to global consumers. |
| First day of week | `I18nProvider.firstDayOfWeek` and `DateInput` use the tenant effective value. | Organization-specific calendar preference is not selected. |
| Project/User overrides | Resolver extension points only. | Persistence, APIs, and runtime consumption are deferred. |

## Owner UAT checklist — pending database delivery

- Confirm the API is connected only to the verified isolated validation database and starts with initialization skipped.
- Confirm secure first-admin login, role/permission grants, session revocation, and Platform Settings authorization.
- Save, reload, reset, and audit Platform defaults; verify Tenant overrides inherit correctly and Tenant A cannot read/write Tenant B settings.
- Verify Organization access grants, read/manage boundaries, revocation, cross-tenant rejection, and Organization profile updates. Do not sign off Organization regional runtime behavior until active Organization context propagation is implemented.
- Create Payment Requests with idempotency keys; verify frozen approver assignments, sequential/parallel approval, rejection/reversal rules, concurrency conflicts, and audit records.
- Verify project, meeting/recurrence, working-calendar, notification-delivery, and project-audit persistence and authorization in the isolated schema.
- Verify effective language/timezone/currency/date/time/week-start behavior at Tenant scope and document that Organization-specific runtime formats remain deferred.
- Confirm the legacy `NexusPmo` database remains unchanged.

## Required delivery status fields

- EF Model synchronized: **Yes (offline source check; 2 source migrations)**.
- Pending migrations: **Target unknown; source IDs are `20261029155617_InitialFopxPmBaseline` and `20261029181208_ProjectPaymentRequestPersistence`.**
- Development DB identity verified: **No**; configured endpoint is `localhost` / `NexusPmo`, physical identity unavailable.
- Backup verified: **No / not run**.
- Migrations applied: **None by this execution; target history unknown**.
- Physical schema verified: **No**.
- Persistence tests: **Not run**.
- Browser UAT: **Not run**.
- Remaining blockers: missing Tenant entity/schema; SQL Server version compatibility unknown for `ISJSON(..., VALUE)`; target connectivity/history/schema unverified; full-chain deployment SQL not generated; secure bootstrap incomplete; API build blocked by IKVM task-host error. Backup status is informational and is not a Development/UAT gate.

**Conclusion: BLOCKED — SPECIFIC TECHNICAL REASONS.**
