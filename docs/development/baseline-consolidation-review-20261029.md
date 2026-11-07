# Database Baseline Consolidation Review — 2026-10-29

**Status: BLOCKED - SPECIFIC REASONS.** This is a source-level inventory and preservation record. No migration was generated or applied; no database was read or written. Builds were attempted after the later explicit authorization; no automated test was run.

## Ownership and current source baseline

- Branch: `main`.
- Initial worktree scan: 806 changed/untracked paths across the repository. During review, a new Finance migration appeared; a follow-up scan showed 813 dirty paths before the latest archive/report artifacts. `AppDbContext`, its snapshot, seed provider, multiple entity configurations, and migrations from several modules are changed or untracked. The source changed during review, confirming there is no worktree freeze.
- The only active Agent in this execution is `/root`, assigned as the single migration coordinator for the baseline work. Git cannot identify the owners of the existing uncommitted files, and no contributor handoff or worktree freeze exists. This assignment does not authorize overwriting or adopting another contributor's changes. A safe consolidated baseline cannot be generated until the module owners hand off or commit their work and the EF owner freezes the shared context/snapshot/migrations.
- The current source has one `NEXUS.Persistence.AppDbContext`, 169 `DbSet<>` declarations, 74 explicit `IEntityTypeConfiguration<>` classes, and 20 registered API module-registration calls. `ApplyConfigurationsFromAssembly` scans the persistence assembly. This is an inventory, not proof that every relationship or module model is valid.
- Registered modules are Identity, Organization, Portfolio, Project Management, Work Items, Resource Management, Cost Management, Risk Management, Issue Management, Meetings, Workflow Engine, Approvals, Notifications, Documents, Reporting, Audit, Integrations, Plugin Management, AI Planning, and Project Planning.
- The source migration chain now contains 23 IDs, latest `20261029153000_AddProjectFinanceApprovalMode`. That migration appeared during this review, has no designer, and adds nullable `Projects.FinanceApprovalMode nvarchar(24)`; the property is also present in `Project.cs`, `ProjectConfiguration.cs`, and the dirty snapshot. Its `Down` is intentionally a no-op. Three older migrations also lack designer target models: `20261018190000_AddMultiPlanReportingConfiguration`, `20261021120000_AddProjectChangeRequestApprovals`, and `20261021160000_AddGovernanceRecoveryPublicIds`.
- `AppDbContextModelSnapshot` does not include `ConfigurationSetting` or `OrganizationSettingsGrant`, so it is not synchronized with the Settings mappings. No EF model check was run.

## Preserved legacy migration archive

Two point-in-time archives preserve the migration source as it changed during review. Both are outside the compiled persistence project:

- [First archive](archives/pre-baseline-legacy-migrations-20261029.zip) and [manifest](archives/pre-baseline-legacy-migrations-20261029.manifest.txt): 42 files captured before the concurrent Finance migration appeared; archive SHA-256 `F7E3AAEAA25135E554FA607A282F64D3B725ED0D51A8C644719EC9ECAEF3836F`.
- [Latest archive](archives/pre-baseline-legacy-migrations-20261029-latest.zip) and [manifest](archives/pre-baseline-legacy-migrations-20261029-latest.manifest.txt): 43 files, including the Finance migration; source hashes were unchanged during capture and all archive entries match the manifest. Archive SHA-256 `85177227CF5D11EB6E7BC9539BD52BA2E136D77C18FF795F20C62866747CA46D`.

These archives reflect dirty shared-worktree snapshots, including untracked concurrent migrations. They are preservation copies, not an approved migration baseline. The originals remain in place.

## Model and historical operation inventory

The model is spread across Core/Identity, Organization, Portfolio/Program, Project Management/Planning, Work Items, Resource/Calendar, Cost/Payments, Risk/Issues, Meetings, Workflow/Approvals, Notifications, Documents, Reporting/Audit, Integrations/Directory, Plugins, and AI Planning. A complete schema review cannot be certified without a controlled EF model build and reconciliation of the concurrent changes.

Static scan of the current migration source found nine `migrationBuilder.Sql` calls in seven migrations. No raw-SQL creation of triggers, views, procedures, functions, or sequences was found; no runtime `ExecuteSqlRaw`/`FromSqlRaw` call was found in `apps/api/src`. The SQL operations are historical data backfills, normalization, or precondition guards:

- `20261012103001_AddLocalPasswordLifecycle`: backfills `Users.AuthenticationSource` using directory-link state.
- `20261012110228_AddNotificationDeliveryFoundation`: hashes existing device tokens and de-duplicates existing token/preference rows.
- `20261015150917_MakeHandoverSnapshotPreviousManagerNullable`: rejects inconsistent existing snapshot rows before a nullability alteration.
- `20261018084343_AddProjectPublicId`: backfills existing project public IDs.
- `20261018190000_AddMultiPlanReportingConfiguration`: backfills status-report dates.
- `20261028150000_SeparateMeetingInvitationAndAttendanceStatuses`: migrates legacy attendance/invitation values between columns.
- `20261028170000_AddProjectAuditTrail`: normalizes existing audit field lengths and backfills project/source fields.

All are preserved in the archive. A baseline for an empty database should represent the final model and must not replay old-row cleanup. Before excluding a raw operation from the new migration, its final invariant must be confirmed in the current model or in a separate idempotent initializer.

Static scan found no `migrationBuilder.InsertData` or EF `HasData` seed in the migration/configuration source. Seed data currently lives in runtime initialization, not the migration chain.

## Schema conditions to resolve

- The settings mappings request composite principal keys `(TenantId, Id)` for `Organizations` and `Users`. Current source declares `Users.Id` as the primary key and only a non-unique `TenantId` index; `Organization` inherits `Id` and has no dedicated entity mapping. `HasPrincipalKey` models the needed composite alternate keys, which the new baseline must create before the tenant-safe FKs. A constructed EF model and actual database keys remain unverified.
- `ConfigurationSettings` and `OrganizationSettingsGrants` mappings are present in source but missing from the snapshot. Tenant, Organization, and User boundaries, filtered unique indexes, rowversions, soft-delete and scope constraints must be included in the final baseline.
- `docs/development/local-setup.md` requires SQL Server 2022+ and Docker Compose uses the 2022 image. The actual isolated Development engine/version and compatibility level are unknown. `ISJSON(ValueJson, VALUE)` needs SQL Server 2022+; keep current typed service validation and do not use legacy `ISJSON` for scalar JSON values.
- Shared Audit writes are transaction-integrated in source, but the audit model/table is also among concurrent changes. Confirm the final audit FK/index model before generating the baseline.

## Seed plan for a new empty Development database

The baseline migration should be schema-only. Keep seed initialization outside migration SQL and make each initializer idempotent:

1. Provision a platform administrator through the deployment-managed `PlatformSettings:AdministratorUserIds` allowlist, an approved platform role, or a one-time bootstrap process. Do not add a password or credential to the migration.
2. Supply the initial Tenant GUID from controlled Development configuration; there is no persisted `Tenant` entity in the current model. Create the initial tenant-owned Organization and associate the bootstrap user through the approved tenant authorization model.
3. Seed essential roles/permissions, currency/reference values, work-item status lookups, document catalog, and any required workflow data through explicit idempotent runtime initializers.
4. Configuration defaults are provided by the typed `SettingDefinitionCatalog`; do not insert duplicate setting-default rows unless an approved override is needed.
5. The modified `SeedDataProvider.cs` currently contains a source-controlled `DemoPassword` literal and development/demo reset paths. The literal value is intentionally not reproduced here. Do not copy this provider into a baseline migration or use its demo credential as the initial administrator credential; replace the bootstrap mechanism before relying on that seed path.
6. `ConfiguredStandardWorkingCalendarInitializer` creates a calendar only when a complete schedule is explicitly configured. Workflow provisioning also runs during API initialization; validate these separate seed dependencies before using them on the new target.

`Program.cs` calls `Database.MigrateAsync()` before seeding unless `--skip-development-database-initialization` is supplied. For controlled validation, keep API startup from auto-migrating; use the reviewed database gate for the isolated target, then start the API with initialization skipped until schema verification is complete. Do not point the consolidated baseline at the existing `NexusPmo` history.

## Safe validation sequence

1. Obtain module-owner handoff and freeze EF edits under the named migration owner. Preserve the archive above; reconcile all model and seed changes without discarding work.
2. With separate authorization for a controlled build, generate `InitialFopxPmBaseline` and the snapshot from the reconciled model. Review the generated migration and SQL for complete schema coverage, unexpected drops, custom operations, constraints, indexes, rowversions, and seed leakage.
3. With separate authorization, provision a new isolated Development database; do not drop, overwrite, or repurpose existing `NexusPmo`. Verify the exact server/database, SQL Server version, compatibility level, and recovery options.
4. Review and hash the SQL artifact. Establish backup/recovery evidence appropriate to the new target, then apply only to that verified empty Development database after separate authorization.
5. Verify the single baseline history entry, full physical schema, and required idempotent seeds. Persistence tests and any application UAT require separate authorization under the current task restrictions.

**Policy supersession note (2026-11-04):** Step 3 records the safe sequence for this 2026-10-29 baseline-consolidation review. The later repository policy allows a no-backup reset only for the explicitly Product Owner-authorized Development target `Shady/NexusPmo`, after exact environment and live identity checks, followed by every EF migration and the required seed. This narrow exception does not authorize repurposing the separate baseline-validation target, Production, or any other database.

## Exact blockers and approvals

- Worktree changed during the review, reaching 813 dirty paths before the latest archive; there is no contributor ownership map or freeze, so EF owner assignment is not yet an effective handoff for those changes.
- Snapshot is dirty and lacks Settings entities. Current full EF model consistency is unverified.
- `InitialFopxPmBaseline` generation requires a controlled build; builds/tests were not authorized in this task.
- No Development database identity, engine/compatibility level, applied history, physical schema, or backup was verified. Database creation/application requires separate authorization.
- Seed bootstrap needs a credential-free administrator path; current modified `SeedDataProvider` has a hardcoded demo-password field and must not be copied into migration SQL.
- Baseline generation and empty-database persistence validation remain pending.

**Conclusion: BLOCKED — SPECIFIC REASONS.**

## Controlled execution follow-up - 2026-10-29

- The collaboration roster contained only this Agent at inspection time; no other active Codex Agent was modifying the EF schema. This does not identify the authors or completion state of existing dirty files. The worktree had 920 changed/untracked paths, including 46 Persistence-related paths. `AppDbContext`, the model snapshot, multiple entity mappings, seed/initialization files, and concurrent module migrations remain changed or untracked. No contributor handoff or source freeze is recorded, so this is not a clean, attributable EF baseline.
- The current migration directory still has 23 migration source files, including `20261029153000_AddProjectFinanceApprovalMode`. The latest archive contains all 23 current migration source files, all 19 existing designer files, and `AppDbContextModelSnapshot.cs` (43 entries total). All 43 archive entry hashes match the manifest; there are zero mismatches. Four Designer files are absent from the current source and therefore are absent from the archive: the three historical migrations listed above and the Finance migration.
- At this pre-authorization checkpoint, the current snapshot contained no `ConfigurationSetting` or `OrganizationSettingsGrant` model entries. No EF model construction, snapshot synchronization, migration scaffolding, build, or tests had yet been run. `HasPrincipalKey(TenantId, Id)` is configured by the Settings mappings; whether the complete consolidated model and generated SQL express all required alternate keys and tenant-safe FKs remains unverified until controlled EF scaffolding.
- The baseline must be generated from a clean migration state, not appended to the current 23-migration chain. Preserve the existing source and verified archive. Once ownership is frozen, generate in an isolated baseline copy/branch with the legacy migration chain excluded from EF discovery there and an empty snapshot, then inspect that migration creates the full model. Do not clear or move shared migrations in the current worktree as part of preparation.
- EF scaffolding invokes a build unless explicitly told to use existing outputs. Using `--no-build` would risk generating from stale binaries and is not an acceptable substitute. The exact controlled command to authorize after preparing the isolated clean baseline state is:

  ```powershell
  dotnet ef migrations add InitialFopxPmBaseline --project apps/api/src/NEXUS.Persistence/NEXUS.Persistence.csproj --startup-project apps/api/src/NEXUS.Api/NEXUS.Api.csproj --context NEXUS.Persistence.AppDbContext --output-dir Migrations
  ```

  Run from `C:\Projects\pmo` in the isolated baseline copy. This builds the projects, constructs the SQL Server EF model, emits the initial migration and synchronizes that copy's snapshot. It was not run at this pre-authorization checkpoint; the Product Owner later authorized the build/scaffold attempt recorded below.
- `Program.cs` contains automatic `Database.MigrateAsync()` during Development initialization and supports `--skip-development-database-initialization`. Keep runtime initialization disabled during baseline validation until the isolated target is explicitly verified and the reviewed migration is separately authorized for application. No database identity, engine version, migration history, backup or physical schema was checked in this follow-up.

**Pre-authorization status: BLOCKED - controlled model/build authorization and attributable source freeze were still required at this checkpoint.**

## Authorized generation attempt - 2026-10-29

- The Product Owner explicitly authorized .NET builds, necessary source fixes, replacement of the archived legacy migration source, baseline generation, and SQL script generation. The existing migration files and snapshot still matched the verified archive before execution: 42 current migration files plus the snapshot were checked; zero hash differences. No legacy migration or snapshot was deleted or replaced because a complete build/model could not be produced.
- A static-graph NuGet restore completed for the API dependency graph. NuGet vulnerability-data retrieval emitted `NU1900` warnings because `https://api.nuget.org/v3/index.json` was unavailable; package restore itself completed.
- The first no-restore compilation exposed Settings source errors. Two minimal fixes were made: `SettingsController.GetPlatform` now returns `Task<IActionResult>` to match its error response path, and `OrganizationSettingsAccessService` aliases the domain `Organization` type to avoid collision with the containing namespace. The `BeginTransactionAsync(IsolationLevel, CancellationToken)` errors were caused by stale assets omitting the already-declared EF Relational package; after restore, `NEXUS.Modules.Organization` compiled successfully without weakening transaction isolation.
- The full API build then stopped in `NEXUS.Modules.ProjectPlanning` at IKVM `MavenReferenceItemImport`: `MSB4216` / `MSB4027`, because MSBuild could not start its `NET` x64 task host in this execution environment. A direct `MSBuild.exe` retry with `DOTNET_HOST_PATH` set produced the same task-host failure. The platform rejected an unsandboxed build request because `sandbox_approval` is disabled. This is an environment execution blocker, not a remaining Settings C# compiler error.
- `NEXUS.Persistence` and the API were not fully built; no EF model check, migration scaffolding, snapshot replacement, or SQL script generation could safely follow. The legacy chain remains intact, including all 23 migrations; the archive remains preserved. No database connection or database operation occurred, no seed ran, and no tests were run.
- The Development startup path still contains `Database.MigrateAsync()`. Migration tooling located `DesignTimeDbContextFactory`, which only configures and returns a context; it does not open a connection or initialize the application. No API startup was performed.

**Current status: BLOCKED - sandbox task-host execution prevents the authorized full build, so the current consolidated EF model and initial migration cannot be verified or generated safely.**

## Explicit migration-chain reset - 2026-10-29

- The Product Owner explicitly requested removal of the active migration chain in preparation for one consolidated initial baseline. Before removal, all 43 C# artifacts in `NEXUS.Persistence/Migrations` (42 migration source/designer files plus `AppDbContextModelSnapshot.cs`) matched the preserved archive manifest exactly. The archive SHA-256 is `85177227CF5D11EB6E7BC9539BD52BA2E136D77C18FF795F20C62866747CA46D`; its 43 entries were verified, with zero per-file mismatches.
- The 42 migration source/designer files and the old model snapshot have been removed from the active migrations directory. The verified archive and manifest remain intact. No database was connected to, dropped, recreated, or modified.
- `InitialFopxPmBaseline`, its designer, the replacement snapshot, and the SQL script have **not** been generated. The authorized build remains blocked: the installed SDK lacks workload resolver components; bypassing reference-framework discovery reached `NEXUS.Modules.ProjectPlanning` and failed in IKVM `MavenReferenceItemImport` with `MSB4216` / `MSB4027` because the required .NET x64 task host could not start.
- A fresh `dotnet ef migrations add InitialFopxPmBaseline --project apps/api/src/NEXUS.Persistence/NEXUS.Persistence.csproj --startup-project apps/api/src/NEXUS.Api/NEXUS.Api.csproj --context NEXUS.Persistence.AppDbContext --output-dir Migrations` attempt stopped at the build step. A follow-up check confirmed it created no partial migration or snapshot.
- Current Development database identity, applied migration history, backup, and physical schema remain unverified. Do not start the API migration initializer while the active migration directory is empty; generate and review the consolidated baseline first, then use the documented database workflow only under its separate authorization.

**Current status: legacy migration source is archived and removed; baseline generation is BLOCKED by the build environment.**

## IKVM isolation and baseline generation - 2026-10-29

- The existing MPP parser was moved into `NEXUS.Modules.ProjectPlanning.MppAdapter`. The parser's task/date, dependency, resource, assignment, and calendar mapping remains in the adapter; its format-neutral parse exception now lives with the ProjectPlanning import contract. `NEXUS.Modules.ProjectPlanning` no longer references `MPXJ.Net` or `System.Drawing.Common`. The API runtime references and registers the adapter.
- Added `apps/api/tools/NEXUS.Persistence.DesignTime`, an executable design-time startup that references `NEXUS.Persistence` only. Its factory uses a closed-port `EFDesignTimeOnly` connection string and does not open it. Its entry point does not start the API or initialize, migrate, or seed a database.
- After restoring the design-time and EF gate graphs, their `project.assets.json` files contain no MPXJ, IKVM, or Maven import. The ProjectPlanning core build succeeded. Persistence plus the design-time executable built successfully (7 `NU1900` warnings because NuGet's vulnerability endpoint was unreachable; 0 compiler errors). `dotnet ef dbcontext info` loaded `AppDbContext` through the design-time factory and reported SQL Server without connecting to a database.
- The source-level baseline was generated: `20261029155617_InitialFopxPmBaseline.cs`, its `.Designer.cs`, and `AppDbContextModelSnapshot.cs`. `dotnet ef migrations has-pending-model-changes` reported no model changes. The offline `ef-database-gate.ps1 model` action reported one repository migration and `EF Model synchronized: Yes`.
- Generated `scripts/database/InitialFopxPmBaseline.sql` with EF Core 10.0.10 SQL Server provider, idempotent. It contains 172 `CREATE TABLE` operations and no `DROP TABLE`, `DELETE`, `TRUNCATE`, `UPDATE`, or `DROP INDEX`. SHA-256: `1F9F6C5BBFA2839AB171295DAC8A24A9BEBB79FCCD152ED60D7EBB8AC34134E0`. The script was reviewed but not executed.
- The baseline matches the currently registered EF model. It includes `Project.FinanceApprovalMode`, existing Payment Obligations/Settlements, generic Approval Requests/Actions, and Audit entries. The model does **not** contain the approved Payment Request lifecycle, immutable selected-approver assignments, idempotency key, or request-level concurrency state. Those Sprint 2 persistence requirements remain a separate schema dependency; do not treat this baseline as containing the full Payment Request workflow or apply it as a final Sprint 2 schema until that contract is integrated.
- The runtime API and MPP adapter build still fail at `IKVM.Maven.Sdk.targets` `MavenReferenceItemImport` (`MSB4216` and `MSB4027`): the .NET x64 MSBuild task host cannot start in this environment. The failure is now isolated to the runtime adapter; the EF design-time graph does not invoke the Maven task. No MPP parser runtime test could run because the adapter build is blocked.
- The first automatic EF build attempt under `dotnet ef migrations add` failed without diagnostic errors in this environment. After a successful explicit build of the exact design-time sources, scaffolding was run with `--no-build`; no source changes occurred between that build and scaffolding. No API host was started. The `migrations list` diagnostic attempted only the closed-port sentinel and could not report applied status.
- No Development or Production database identity/history was verified, no backup was made, no migration was applied, and no data/schema was changed. The actual Development database and physical schema remain unverified. Database synchronization, independent backup evidence, and persistence tests remain not run.

**Status: EF design-time isolation and source baseline generation succeeded. MPP runtime verification remains blocked by the IKVM task-host environment; Payment Request schema integration remains a Sprint 2 dependency; database deployment was not performed.**

## Sprint 2 Payment Request persistence integration - 2026-10-29

- Reused `ApprovalRequest` as the canonical request root: title, reason, financial reference (`EntityNumber`), amount, explicit project currency, requester, status, tenant, and request concurrency remain on that entity. Removed the implicit GBP initializer so financial writers must set the project's configured currency. `ApprovalAction` now has a nullable assignment link; historical and generic actions remain valid when it is null.
- Added `ProjectPaymentRequestDetail` as a one-to-one, tenant-scoped extension keyed by `ApprovalRequestId`. It stores project and optional `PlanActivity` references, submitted approval method, Payment/Reversal kind, original request link, idempotency key/fingerprint, submission/completion times, and a SQL rowversion. It does not duplicate amount, currency, requester, or status.
- Added `PaymentRequestApproverAssignment` for tenant-scoped user assignments, Mandatory/Optional requirement, optional sequence position, state, and SQL rowversion. Composite foreign keys bind request, user, project, milestone, reversal source, and action assignment to the same tenant; approval actions additionally link to an assignment from the same request. A filtered unique position index applies only when position is present, allowing parallel checklist members to have no sequence position. Unique request/user and action/assignment indexes prevent duplicate selections and decisions.
- Preserved the established `ApprovalRequest.RowVersion` column shape. `AppDbContext` rotates this binary concurrency token on sync and async writes; the new detail and assignment rows use provider-generated SQL rowversions. Existing `Project.Budget` remains the ceiling; no reservation ledger, settlement deduction, or CostEntry linkage was introduced. `AuditEntries` remains the sole audit store; no artificial event rows or event producers were added in this persistence task.
- Kept `20261029155617_InitialFopxPmBaseline` and its snapshot byte-for-byte. The original files and SQL are archived at `docs/development/archives/InitialFopxPmBaseline-pre-financial-sprint2-20261029`; the active initial migration hash matches the archive. The source-only additive migration is `20261029181208_ProjectPaymentRequestPersistence` with Designer and the updated snapshot. The original initial migration's applied status could not be queried: Development connection, expected server/database, and environment inputs were absent. This choice is explicit: apply both migrations sequentially to a fresh database; do not re-squash or apply either until the actual target history is verified.
- Generated the full idempotent chain at `scripts/database/InitialFopxPmBaseline-through-ProjectPaymentRequestPersistence.idempotent.sql` (SHA-256 `A172A3FEBC60D1CAE7DF5697FC9D2CBC457247A9F65D0AA93A4D61203C7D215D`). Static review found 174 table creates, 11 `ALTER TABLE` statements for additive columns/keys/constraints, and 288 indexes. The two `INSERT` statements target only `__EFMigrationsHistory`. No drop, delete, truncate, update, column alteration, or data seeding was found. The Payment Request migration `Up` contains two new tables, one nullable `ApprovalActions` column, tenant alternate keys, indexes, and foreign keys; it contains no `AlterColumn`, drop, raw SQL, or data DML.
- `dotnet build apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --no-restore -m:1` succeeded (0 errors; 7 `NU1900` vulnerability-source warnings). `dotnet ef dbcontext info` loaded `NEXUS.Persistence.AppDbContext` on the isolated SQL Server design-time sentinel without opening a database. `dotnet ef migrations has-pending-model-changes` reported no changes. `scripts/database/ef-database-gate.ps1 model` reported two repository migrations and `EF Model synchronized: Yes`; its connection remained unopened. Persistence and design-time assets had zero IKVM, Maven, or MPXJ references.
- No database identity/history, independent backup, physical schema, applied migration, or persistence runtime test was verified. No migration or database data was changed. Exact blocker: `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING`, `ASPNETCORE_ENVIRONMENT`, and expected server/database values are absent. Do not treat the initial migration alone as the complete Sprint 2 schema.
- API/UI handover: implement project-scoped draft/edit/submit/decide/cancel commands; validate active `ProjectMember` plus linked active `Resource.UserId`; set the actual `Project.Currency`; freeze submitted assignments; and write corresponding `ApprovalAction` and `AuditEntry` rows in the same transaction. Final approval must acquire a project-keyed transaction-owned SQL Server application lock, re-read `Project.Budget` and the sum of active approved Payment Request roots/details, then atomically record final status, action, and audit. Settlements remain outside this calculation. Reversal request records can be created, but approval and allocation release remain disabled until the unresolved reversal amount/release rule is decided.

**Status: EF FINANCIAL SOURCE INTEGRATION COMPLETE — DATABASE DEPLOYMENT PENDING. API/UI workflow, project-level budget lock and reservation engine, actual audit producers, Development target verification, backup, physical schema, and runtime acceptance remain pending.**

## SQL Server delivery addendum — 2026-10-30

The status above records the 2026-10-29 source review. On 2026-10-30, the verified Development target was `Shady/NexusPmo` (database ID 9). The initially reviewed SQL failed because SQL Server rejected `OR` in the nonunique `IX_OrganizationSettingsGrants_ActiveUserOrg` filtered-index predicate. After owner approval, the active model, all three migration target models, and snapshot were updated from `[IsDeleted] = 0 AND ([CanRead] = 1 OR [CanManage] = 1)` to `[IsDeleted] = 0`; the archived pre-Financial baseline remains unchanged. The revised SQL changed only that index statement, passed SQL Server `PARSEONLY`, and has SHA-256 `A922CBC3BD82A7D2EF12E348B0837D402B5A9FB647EDC283E050566E09BC2B8D`.

The EF delivery gate verified a checksummed copy-only backup with `RESTORE VERIFYONLY`, applied all three migrations, and then verified 3 applied / 0 pending and 0 missing physical schema objects. The backup and database files share the `C:` volume. The Development identity-only seed then succeeded; API build, local admin login, authenticated `/api/auth/me`, and a rollback-only SQL Server EF persistence round-trip all passed. No Production or other database was changed. The broader Payment Request workflow/API/UI and budget reservation behavior remain outside this database delivery.

