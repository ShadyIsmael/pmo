# Database Sprint 04 — Tenant Foundation

**Source status:** Tenant registry implemented. On 2026-10-30, all three current migrations were applied to the verified Development target `Shady/NexusPmo` (database ID 9); see the delivery record below. Production was not touched.

## Model and migration

The source model now includes a distinct `Tenant` entity with a GUID key, normalized unique code, display name, `Provisioning` / `Active` / `Suspended` / `Retired` lifecycle, created/updated audit fields, and SQL Server `rowversion`. The Tenant has many Organizations. `Organizations.TenantId` references `Tenants.Id` with restricted deletion. Tenant- and Organization-scope `ConfigurationSettings.TenantId` also references the registry; Platform rows keep a null TenantId.

Role and permission uniqueness is now tenant-scoped so each customer can hold its own `admin` role and permission catalog. The migration changes the former global unique indexes to `(TenantId, Name)` for Roles and `(TenantId, Resource, Action)` for Permissions.

The ordered source migrations are:

1. `20261029155617_InitialFopxPmBaseline`
2. `20261029181208_ProjectPaymentRequestPersistence`
3. `20261029183557_AddTenantRegistry`

The pre-Financial Sprint 2 baseline archive remains unchanged. During Development delivery, SQL Server rejected the nonunique `IX_OrganizationSettingsGrants_ActiveUserOrg` filtered-index predicate because it contained `OR`. With owner approval, the active model, migration targets, and snapshot were updated to use the SQL Server-compatible `[IsDeleted] = 0` filter. This broadens the nonunique index to all active grants without changing row or query semantics. The reviewed, target-specific three-migration script is [FopxPm-FreshDevelopment-reviewed-20261030.sql](../../scripts/database/FopxPm-FreshDevelopment-reviewed-20261030.sql).

The cumulative SQL contains all three migrations, including Tenant/Organization settings, project payment requests and assignments, approvals, projects, meetings, working calendars, notifications, audit tables, and other baseline modules. Static inspection confirmed the revised script includes the Tenant table and both Tenant foreign keys, and has no `CREATE DATABASE`, table/column drops, updates, deletes, or truncation. The original SQL SHA-256 was `E74018BD9DCCCBFCE20FED1607640867D67394E3DAA2DCD4D05E3A1CD2724863`; it was superseded after the SQL Server syntax failure. The revised script SHA-256 is `A922CBC3BD82A7D2EF12E348B0837D402B5A9FB647EDC283E050566E09BC2B8D` and was applied through the documented EF Core workflow.

## Existing data reconciliation requirement

The migration inserts registry rows for each non-empty TenantId found in `Users`, `Organizations`, `Roles`, and `ConfigurationSettings` that is not already registered. It assigns deterministic codes `LEGACY-<guid-without-dashes>`, a `Legacy Tenant <guid>` display name, and initial `Active` status. This preserves existing IDs and permits the Organization and Settings foreign keys to be added. Before deployment, the owner must confirm that each candidate ID represents the intended tenant, review any conflicting real-world tenant mapping, and replace the generic code/name as part of a controlled reconciliation plan. This backfill is compatibility scaffolding, not a source of verified customer identity.

Read-only pre-migration diagnostic to inventory the IDs the migration will register:

```sql
SELECT source.TenantId, source.SourceTable, COUNT_BIG(*) AS RowCount
FROM (
    SELECT TenantId, N'Users' AS SourceTable FROM dbo.Users
    UNION ALL
    SELECT TenantId, N'Organizations' FROM dbo.Organizations
    UNION ALL
    SELECT TenantId, N'Roles' FROM dbo.Roles
    UNION ALL
    SELECT TenantId, N'ConfigurationSettings'
    FROM dbo.ConfigurationSettings
    WHERE TenantId IS NOT NULL
) AS source
WHERE source.TenantId <> '00000000-0000-0000-0000-000000000000'
GROUP BY source.TenantId, source.SourceTable
ORDER BY source.TenantId, source.SourceTable;
```

After applying the migration to an authorized target, verify there are no orphaned organization or non-Platform configuration rows:

```sql
SELECT o.Id, o.TenantId
FROM dbo.Organizations AS o
LEFT JOIN dbo.Tenants AS t ON t.Id = o.TenantId
WHERE t.Id IS NULL;

SELECT c.Id, c.Scope, c.TenantId
FROM dbo.ConfigurationSettings AS c
LEFT JOIN dbo.Tenants AS t ON t.Id = c.TenantId
WHERE c.TenantId IS NOT NULL AND t.Id IS NULL;
```

The documented orphan queries were not run. Before migration, the verified Development database had zero application tables, so the Tenant registry backfill had no legacy IDs to reconcile. On populated targets, run these diagnostics and verify every tenant-owned module's TenantId values; the migration deliberately adds Tenant foreign keys only to Organizations and ConfigurationSettings rather than blindly constraining every legacy table.

## Provisioning and authorization

`POST /api/platform/tenants` and the related Tenant registry endpoints require `platform_role=PlatformAdministrator`, the dedicated `platform_permission=tenants.manage`, or an ID in the deployment-managed `PlatformSettings:AdministratorUserIds` allowlist. Tenant-level `Settings.Write` does not grant provisioning authority.

Provisioning creates the Tenant and its initial Organization in one EF transaction. A Tenant without an administrator stays in `Provisioning`; tenant requests are denied until it becomes Active. `POST /api/platform/tenants/{tenantId}/administrators/{userId}` requires an already provisioned, active User whose TenantId matches exactly. It creates/uses a tenant-local `admin` role, copies the active permission definitions from an existing `admin` role as a template, assigns the role, and activates the Tenant in the same transaction. No user account or credentials are generated. If no active `admin` role template exists, provisioning fails closed and requires permission-catalog initialization before assignment.

Platform endpoints also list tenants and can suspend or retire a tenant. `Retired` is terminal; activation requires a valid administrator assignment. Configuration and tenant changes append shared audit entries inside the same context transaction. SQL Server rowversion protects status changes from stale writes.

The explicit Development-only `SeedDataProvider` ensures the deterministic demo Tenant row before seeding demo users and roles. On 2026-10-30, the identity-only seed created the active demo Tenant, one admin user, eight roles, 95 permissions, and one user-role link. It did not seed projects or financial requests. This demo fixture remains separate from first production Platform Administrator / Tenant provisioning.

The application does not create a first Platform Administrator account or mint platform claims. Initial platform identity must come from the trusted token issuer or a pre-existing authenticated user explicitly entered in the deployment-managed platform administrator allowlist; no credentials are generated here.

Authenticated tenant requests continue to derive TenantId only from the signed `tenantId` claim. The request permission middleware now rejects missing/inactive registry tenants before downstream authorization. A trusted platform-only principal may omit TenantId to establish the first tenant; the permission middleware removes malformed tenant context and adds no tenant permissions, so tenant query filters resolve to an empty TenantId. The legacy demo fallback applies only outside HTTP requests.

## Pre-deployment state (2026-10-29)

No database was created or changed, no migration was applied, and no seed or provisioning operation was executed. The owner reports the historical `localhost/NexusPmo` Development endpoint is unreachable; actual SQL Server identity, engine version, compatibility level, applied migration history, independent backup, and physical schema remain unverified. Confirm those items and review the cumulative SQL and legacy Tenant inventory before authorizing deployment. Keep automatic application startup migration disabled.

The isolated `NEXUS.Persistence.DesignTime` build succeeded. EF `has-pending-model-changes` reported no changes. No automated tests, API tests, or browser UAT were run; persistence tests and physical schema verification remain pending for the isolated database gate.

## Development delivery record (2026-10-30)

- **Target:** SQL Server `Shady`, database `NexusPmo`, database ID `9`, verified through `SERVERPROPERTY('ServerName')`, `DB_NAME()`, and `DB_ID()`. The database had no application tables before migration; the tenant backfill had no candidates.
- **Migrations and schema:** `EF Model synchronized: Yes`; applied `20261029155617_InitialFopxPmBaseline`, `20261029181208_ProjectPaymentRequestPersistence`, and `20261029183557_AddTenantRegistry`; pending count `0`; the final gate inspection found `0` objects missing from both the last-applied and current models.
- **Backup:** The gate created a copy-only backup with checksums and completed `RESTORE VERIFYONLY` before migration. File: `C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup\NexusPmo-Development-20261029\NexusPmo-Development-20261029-222402-a4a87d2346454867ac7c45ee340ea068.bak`. The database files and backup are on `C:`; this is a verified local recovery point, not an off-volume backup.
- **API and identity:** API build succeeded with 8 `NU1900` warnings (NuGet vulnerability metadata endpoint unavailable) and 0 build errors. The Development identity-only bootstrap succeeded. With database initialization explicitly skipped, local `POST /api/auth/login` and authenticated `GET /api/auth/me` both returned HTTP 200 for `admin@nexus.com`.
- **Financial Sprint 2 persistence:** A focused EF Core SQL Server round-trip persisted a payment-request detail, approver assignment, linked approval action, idempotency values, and SQL rowversions inside a transaction. The transaction was rolled back; read-only verification found no retained probe project, request, detail, or assignment. The final counts for payment-request details and assignments were both zero.
- **Remaining scope:** The physical Financial Sprint 2 schema is ready. This record does not claim the broader project-payment workflow/API/UI, budget reservation engine, or business approval lifecycle is complete.
