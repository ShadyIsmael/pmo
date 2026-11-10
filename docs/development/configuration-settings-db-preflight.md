# Configuration Settings Database Preflight — Sprint 01D

**Status: Prepared, not executed.** This package contains a read-only checklist and diagnostic SQL for an owner-authorized Development database inspection. It does not establish the identity, schema, migration history, or backup state of any database.

**Backup-policy update:** The historical same-volume backup observation below remains part of the preflight evidence, but Development/UAT operations no longer require independent backup storage or `RESTORE VERIFYONLY`. A backup may be created when explicitly requested; its absence is not a migration or change gate. This does not alter the target identity, SQL-version, migration, or schema checks.

## Current source baseline

- Branch at review: `main`.
- At Sprint 01D package creation, the source migration directory contained 22 IDs, latest `20261028170000_AddProjectAuditTrail`. A later baseline review found a new untracked Finance migration; the current source has 23 IDs, latest `20261029153000_AddProjectFinanceApprovalMode`.
- `AppDbContext` and `AppDbContextModelSnapshot` are modified in the shared worktree. The context changes include Working Calendar and Payment entities; the snapshot has 427 changed lines.
- Ten additional migration pairs are untracked in the shared worktree: Resource Working Capacity; Working Calendar Day Rules; Planned Payment Lifecycle; Payment Tenant-Scoped Relationships; Status Report Actions; Status Report Payment Snapshot Fields; Meeting Action Work Item Link; Meeting Lifecycle Tracking; Separate Meeting Invitation and Attendance Statuses; Project Audit Trail. Several related entity configurations are also changed or untracked.
- Three older migration source files lack designer target models: `20261018190000_AddMultiPlanReportingConfiguration`, `20261021120000_AddProjectChangeRequestApprovals`, and `20261021160000_AddGovernanceRecoveryPublicIds`. The Finance migration added later also has no designer. Verify the target's applied-history position before relying on historical target-model comparisons.
- Git status does not record the author/owner of uncommitted or untracked changes. No single migration owner or frozen baseline can be established from the repository state alone. Do not add a settings migration to this shared chain yet.
- The model snapshot contains no `ConfigurationSetting` or `OrganizationSettingsGrant` entries. It is not synchronized for Settings.

## Entity keys and proposed relationships

`User` and `Organization` inherit `Id` and non-null `TenantId` from `BaseEntity`. `UserConfiguration` explicitly declares `Id` as the primary key and has a non-unique `TenantId` index; it does not declare `(TenantId, Id)` as a key. `Organization` has no dedicated entity configuration in the current source and relies on the inherited `Id` key convention. The Settings mappings call `HasPrincipalKey(TenantId, Id)` for both referenced types. EF therefore models composite alternate keys, which the migration must create as unique principal keys before adding the composite foreign keys. The identities in the entities make `(TenantId, Id)` logically unique because `Id` is already the primary key; the physical constraints still must be verified against Development before delivery.

Required relationships:

- `ConfigurationSettings(TenantId, OrganizationId)` → `Organizations(TenantId, Id)`, nullable for Platform/Tenant rows and populated for Organization rows.
- `OrganizationSettingsGrants(TenantId, OrganizationId)` → `Organizations(TenantId, Id)`.
- `OrganizationSettingsGrants(TenantId, UserId)` → `Users(TenantId, Id)`.
- All three composite FKs use restricted deletes. The service rejects inactive or cross-tenant owners; physical user/organization deletion must not silently cascade settings or grant records.

The tenant is an authenticated ownership boundary, not a persisted `Tenant` entity. Platform rows have null Tenant/Owner/Organization IDs. Tenant rows use the trusted Tenant ID for both TenantId and OwnerId. Organization rows use the trusted TenantId and Organization ID, with OwnerId equal to OrganizationId. Separate filtered unique indexes are required for Platform, Tenant, and Organization active keys; nullable composite uniqueness is not used as the invariant.

`ConfigurationSettings` uses `DeletedAt/DeletedBy` for reset and SQL Server `rowversion`; `OrganizationSettingsGrants` inherits the shared `IsDeleted` soft-delete flag and `rowversion`. Shared audit writes are enlisted through the same scoped `DbContext` and transaction in source. Platform audit entries remain tenant-partitioned by the existing audit schema.

## SQL Server JSON compatibility decision

`docs/development/local-setup.md` declares SQL Server 2022+ as the supported database version, and Docker Compose uses `mcr.microsoft.com/mssql/server:2022-latest`. This establishes the project source requirement, but does not pin the actual Development database engine or database compatibility level. The repository's historical EF gate record describes a previous database inspection; it predates the current migration chain and is not current proof of target identity or schema.

That historical record named `Shady` / `NexusPmo`, reported 19 migrations applied, and found backup media on the same `C:` volume as the database files without independent restore verification. Later notes say the database evidence predates subsequent Meetings migrations; the source had 22 IDs when this package was prepared and now has 23. Treat identity and history as stale until the authorized preflight repeats them. The backup statement records historical evidence only and does not make an independent backup a current prerequisite.

`ISJSON(ValueJson, VALUE)` requires SQL Server 2022 (16.x) or later. The `VALUE` constraint accepts any JSON value, including scalar strings, numbers, booleans, and `null`; legacy `ISJSON(ValueJson)` only accepts an object or array and would reject the scalar JSON representation used by current settings. The project does not currently establish a minimum Development database engine or compatibility level. Capture both from the verified target before migration generation. Do not change the database compatibility level as part of this migration without separate approval.

For SQL Server earlier than 2022, preserve the existing JSON serialization format and typed server-side validation, but omit the `ISJSON(..., VALUE)` database check. Keep the allowed `ValueType` check and catalog validation, and ensure application-only writes are enforced by database permissions. The current SQL check does not enforce that JSON token kind matches `ValueType`; the application validator does. If database-level type matching is required, decide on a version-compatible constraint before migration generation. Do not substitute legacy `ISJSON` or change the stored format silently.

## Organization grant bootstrap recommendation

Current behavior: tenant `Settings.Write` authorizes tenant configuration edits and also permits managing Organization Settings grants for any active Organization and active User in that same Tenant. A user with that permission can establish the first grant and can grant themself `CanManage`. Organization configuration updates still require tenant `Settings.Write` plus an active Organization grant with `CanManage`; a grant does not confer Platform access. Server authorization rechecks persistence on requests, so revocation takes effect for the next server request. Previously rendered UI data can remain visible until its next authorized-list refresh.

This is a tenant-bounded but broad delegation capability. Recommendation for Product Owner review: separate grant administration into a privileged permission such as `OrganizationSettings.Access.Manage`, distinct from editing tenant configuration and from editing Organization settings. Bootstrap that permission through a documented tenant-owner/admin role or a controlled provisioning path. Keep current behavior unchanged until that authorization change is approved; do not remove self-grant or bootstrap behavior in this database-preflight sprint.

## Active Organization runtime integration design

There is no global active-Organization context in the React app or authenticated user claims. The existing Organization Settings grant list is a Settings-specific entitlement, not a general Organization membership claim. Existing `OrganizationMembership` records represent resource-to-organizational-unit assignments and should not be treated as user access to an Organization. In `SettingsWorkspaces.tsx`, the selected Organization is page-local React state; organization-setting query keys include the Organization ID and authenticated Tenant ID. The runtime-settings query and `TenantRuntimeSettingsBridge` are Tenant-only, and localization/formatting consumers receive no Organization ID. Organization Settings UI selection does not establish a runtime-wide Organization context.

Focused future integration:

1. Source Organization choices from a server-authorized list for the authenticated user. Do not treat a local-storage ID or client-supplied TenantId as authorization.
2. Keep the selected Organization in a scoped React context for Organization-bound routes. Include TenantId and OrganizationId in query-cache keys; clear or invalidate organization-scoped cache entries on a switch or revocation.
3. Send an explicit Organization ID in the route/query for each Organization-scoped request. Resolve actor and TenantId from authenticated claims on the server, then verify the Organization is active, belongs to that Tenant, and is authorized for the requested operation on every request.
4. Pass that validated Organization identity explicitly to the effective resolver. With no selected/validated Organization, resolve Tenant → Platform → definition default. Never select another Organization's value as a fallback.
5. Return each effective value with its source. Apply Organization language/timezone/currency/date/time/week-start within the scoped Organization runtime provider when no explicit user preference overrides it. Keep tenant-wide pages and cross-Organization reports on tenant defaults or format each Organization row with its own explicit resolved context.

## Owner-authorized read-only preflight checklist

Do not run the SQL file until the owner authorizes Development database reads and supplies a verified target. Use the repository gate rather than a design-time fallback or a connection-string filename.

1. Obtain the effective Development API connection securely, set `ASPNETCORE_ENVIRONMENT=Development`, and configure the gate's connection variable through a secret manager or process environment. Never paste or print the connection string.
2. Use `scripts/database/ef-database-gate.ps1 inspect` with the exact expected `SERVERPROPERTY('ServerName')` and `DB_NAME()` values. Confirm the target is Development and matches the effective API configuration. The old gate reconciliation record is historical evidence only.
3. After the gate confirms identity, fill the SQL script's two expected-target placeholders with those exact public metadata values and run it against that same database. It fails closed on a mismatch.
4. Compare all source IDs with `dbo.__EFMigrationsHistory`, identify history-only IDs, verify the actual `Users`/`Organizations` key and FK structures, and inspect the physical settings tables if they already exist. Any history-only migration, missing source migration, or unexpected schema requires owner review before generation.
5. Review the server version and database compatibility level. Confirm SQL Server 2022+ before using `ISJSON(..., VALUE)`; for an older engine, use the compatibility decision above without changing the serialized JSON contract.
6. If a backup is explicitly requested, record the latest full backup metadata, media path, checksums, database data-file paths, and recovery model. Backup metadata alone is not proof of recoverability. Independent storage, `RESTORE VERIFYONLY`, and a test restore are not prerequisites for Development/UAT operations.
7. Reconcile all concurrent module changes under one named EF owner. Establish one ordered source baseline, synchronize the snapshot, and run the offline model gate. Only then generate target-specific pending SQL and review every operation for unrelated or destructive changes.
8. This package does not authorize migration application. Application requires separate explicit authorization, reviewed SQL/hash, and the repository's environment-appropriate apply gate. A Development/UAT backup is optional and its absence does not block the migration.

## Diagnostic SQL

Use [preflight-multi-tenant-configuration-settings.sql](../../scripts/database/preflight-multi-tenant-configuration-settings.sql). It contains SELECTs against the current database, EF history, SQL catalog views, and `msdb` backup metadata. It creates no objects and changes no database state. It is deliberately not run as part of this task.

## Delivery status

**READY FOR CONTROLLED DATABASE PREFLIGHT; NOT READY FOR MIGRATION GENERATION.** The read-only package is ready for separate owner authorization. Shared EF ownership is unresolved; `AppDbContext` and the snapshot are dirty; ten untracked concurrent migration pairs exist; the model snapshot lacks Settings entities; actual Development target/version/history/schema are unverified; and the SQL-version decision is not established for that target. Do not generate `AddMultiTenantConfigurationSettings` until those blockers are cleared. Backup state is not a Development/UAT prerequisite under the current policy.

