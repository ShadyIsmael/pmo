# Platform, Tenant and Organization Settings

**Status: PARTIAL - settings and Tenant Registry source persistence, EF migration, and cumulative SQL are generated; Development database deployment and owner UAT remain pending.**

The configuration architecture is Platform → Tenant → Organization → Project → User. Tenant is an authenticated ownership boundary represented by a trusted `tenantId` claim; it is not an Organization row. A tenant may own multiple Organization records. This delivery implements Platform, Tenant and Organization settings. Project/User precedence is defined in the resolver extension interface, but Project/User persistence and APIs remain deferred.

## Current architecture findings

### Tenant and Organization are different concepts

- Persisted entities inherit `TenantId` from `BaseEntity` and are tenant-filtered by `AppDbContext`.
- The active tenant is resolved from the authenticated `tenantId` claim. Authentication middleware validates the user session and requires an Active registry tenant before downstream authorization. A malformed or missing request claim resolves to no tenant; outside HTTP requests, existing development initialization retains the seeded Development tenant fallback.
- Settings services deliberately fail closed if the authenticated tenant claim or actor ID is missing; they do not use that Development fallback.
- The Tenant registry is a distinct persisted entity with lifecycle status, unique code, audit metadata, and SQL Server rowversion. The additive `AddTenantRegistry` migration is generated; physical Development deployment is pending.
- `Organization` is a tenant-owned entity with its own `Id`, `TenantId`, display name, logo URL, description, and contact fields. It is not the tenant itself. A tenant may own multiple organizations.
- Existing organization identity remains on the current `Organization` entity. Regional configuration is stored separately as an Organization-scope `ConfigurationSetting` override.
- Organization Settings access is per user and per Organization through `OrganizationSettingsGrant`; tenant `Settings.Read/Write` alone does not expose every organization to every user.

Tenant settings are keyed only by the authenticated tenant context. Organization routes accept an Organization ID, derive TenantId from the authenticated context, verify that the Organization belongs to that tenant, and require a matching user grant. No settings endpoint accepts TenantId from the request body.

### Existing settings surfaces

- The unsupported `app.name` write has been removed. Platform identity is read-only and sourced from `apps/web-react/brand.json`.
- Tenant `Settings.Read/Write` permissions remain tenant-scoped. Platform endpoints now use separate `PlatformSettings.Read/Write` policies enforced from trusted platform claims/roles or the deployment-managed `PlatformSettings:AdministratorUserIds` allowlist.
- `settings.read` and `settings.write` are current tenant-scoped permission codes used by administration/reference-data endpoints. They do not establish a separate FOPX platform-administrator boundary.
- `OrganizationController` exposes Organization profile reads/updates only for explicitly granted Organizations, plus tenant-admin endpoints to manage same-tenant user grants.
- Administration screens remain siblings in the Administration hub. Platform Settings contains platform identity and platform-wide configuration only; Organization Settings is a separate tenant-scoped route. No Users, Roles, Reference Data, or Organization Management screens were moved or duplicated.
- The current Organization entity already owns organization identity fields. A new tenant profile table would duplicate identity data unless the domain model is deliberately changed.
- The Audit module provides a shared append-only `IAuditService`; its current audit data and permissions are tenant-scoped. Configuration, Organization profile and grant writes now append through this service; platform changes are attributed to the acting user's tenant because the existing audit schema requires `TenantId`.
- An explicit user locale choice remains a client preference. Organization and tenant language defaults apply only when no explicit locale choice exists.

The Platform Settings page was changed to show the centrally configured FOPX identity as read-only. Its unsupported Save path was removed. Parent company, product name, full product name, description, tagline, and intended domain come from `apps/web-react/brand.json`; tenant administrators cannot rename the product through this screen.

### Navigation hierarchy

The earlier settings workspace introduced cross-links between Platform Settings and Organization Settings. This made tenant configuration appear nested inside the platform area. It did not move or duplicate User Management, Roles & Permissions, Reference Data, or Organization Management. The cross-links have been removed; both configuration areas are siblings under Administration. The global sidebar also previously gated Platform Settings with tenant `Settings.Read`, which exposed the platform route to tenant administrators and hid it from platform-only administrators. The route is now shown only when `/api/settings/access` confirms platform read access; Organization Settings remains tenant-permission gated.

- `/administration` is the hub. It is available to users with tenant administration permissions or to a verified platform administrator.
- User Management and Roles & Permissions remain under `/administration/access` and its existing child routes.
- Reference Data remains under `/administration/reference-data` and its existing routes.
- Organization Management remains at `/administration/organization`.
- Platform Settings is `/administration/settings`; it contains general platform configuration and the read-only FOPX identity. Future sections are not exposed as functional pages.
- Tenant Settings is `/administration/tenant-settings`; it contains tenant-wide regional defaults.
- Organization Settings is `/administration/organization-settings`; it lists only Organizations the current user is explicitly authorized to access and edits the selected Organization profile and regional overrides.
- Existing Integrations, Governance, Currency, License, and Plugin administration routes remain in Administration.

The platform workspace checks `/api/settings/access`, and its API endpoints require dedicated `PlatformSettings.Read/Write`. Tenant Settings requires tenant `Settings.Read/Write`. Organization Settings additionally requires the per-user Organization grant; service and repository checks derive TenantId from authenticated context and verify Organization ownership server-side.

## Implemented source-level foundation

- `SettingDefinitionCatalog` defines typed setting keys, default values, allowed scopes, server validation, override behavior and security classification. Global language, time zone, date/time formats and first-day-of-week support Platform, Tenant and Organization overrides. Currency is tenant/organization scoped and must match that tenant's active currency list. Password policies are excluded from this configuration catalog and UI until the Security Settings sprint can connect them to enforced authentication behavior.
- `EffectiveConfigurationResolver.ResolveEffectiveConfiguration` defines Platform → Tenant → Organization → Project → User precedence. This delivery wires Platform, Tenant and Organization maps. Project/User have resolver extension points only; their persistence and APIs are not enabled.
- The resolver and definitions support constrained overrides and fail closed if a constrained definition has no validator. No current catalog entry uses a mandatory/inherited constraint; mandatory Security Policy controls remain deferred and no such policy should be presented as implemented.
- `ConfigurationSetting` is a polymorphic setting row and intentionally has no ambient tenant filter. `SettingsScopeIdentity` keeps Platform `(null, null)`, Tenant `(TenantId, TenantId)` and Organization `(trusted TenantId, OrganizationId)` distinct. The EF repository filters explicitly and verifies Organization ownership before reads/writes.
- `User` and `Organization` retain their `Id` primary keys and expose `(TenantId, Id)` alternate keys used by the tenant-safe Organization and grant relationships. These keys are represented by the baseline migration; physical Development key definitions remain unverified.
- `OrganizationSettingsGrant` defines per-user Organization read/manage rights. The access service requires both the tenant Settings.Read/Write policy and an explicit same-tenant Organization grant. Grant targets are active Users in the trusted tenant. Grant management requires Settings.Write; it cannot grant Platform access.
- Grant bootstrap is tenant-administrator authority: an authenticated user with `Settings.Write` can create the first grant for any active Organization in that same Tenant and can select themself as the target. It is not limited to Organizations for which that administrator already has a grant. The service rejects cross-Tenant Organizations and Users. Organization read/manage grants are rechecked from persistence for each API request; the Organization workspace refreshes its authorized-Organization list every 30 seconds while open, so already-rendered regional/profile data can remain visible until that refresh, but stale client state cannot authorize a server request.
- `SettingsController` exposes platform, tenant and organization read/update/reset APIs. API contracts do not accept TenantId from clients. Platform authorization uses dedicated `PlatformSettings.Read/Write` claims/roles or the deployment-managed allowlist.
- Settings routes: `GET /api/settings/access`; `GET /api/settings/platform`; `PUT /api/settings/platform/{key}`; `POST /api/settings/platform/{key}/reset`; `GET /api/settings/tenant`; `PUT /api/settings/tenant/{key}`; `POST /api/settings/tenant/{key}/reset`; `GET /api/settings/organizations/{organizationId}`; `PUT /api/settings/organizations/{organizationId}/{key}`; `POST /api/settings/organizations/{organizationId}/{key}/reset`; `GET /api/settings/runtime`.
- Organization routes: `GET /api/organizations/general-settings` returns only granted Organizations; `PUT /api/organizations/{id}/general-settings` updates the existing profile; `GET /api/organizations/settings-access/users` returns active same-tenant grant targets; `GET/PUT /api/organizations/{id}/settings-access/grants` reads/writes grants; `POST /api/organizations/{id}/settings-access/grants/{userId}/revoke` revokes a grant.
- Configuration, profile and grant updates append shared Audit events within the same EF transaction where the existing `IAuditService` permits. Audit includes scope/owner, key or profile fields, old/new values, actor and correlation ID. Settings secrets are rejected from ordinary storage. The shared Audit store remains tenant-partitioned; a platform-wide audit query is not implemented.
- React now has separate Platform General Settings, Tenant General Settings and Organization General Settings workspaces. Editable settings call their server APIs and display effective value source, validation/concurrency feedback, Save/Cancel/Reset and unsaved-change warnings. Organization profile identity uses existing Organization records. Tenant Settings includes a scoped Organization grant-management panel. FOPX identity remains read-only from `apps/web-react/brand.json`.
- Platform navigation is gated by `/api/settings/access`; Tenant Settings by tenant Settings.Read; Organization Settings navigation uses the server-authorized Organization profile list. Direct API requests are additionally protected by backend policies and per-organization grants.
- Runtime regional defaults remain tenant-scoped. Applying organization-specific preferences to a selected organization runtime context is deferred because the app has no single active Organization context. Explicit user locale choice continues to take precedence over tenant defaults.
## Runtime and future design boundaries

| Setting | Persisted scopes after migration | Runtime consumer | Effective runtime source | Current gap |
|---|---|---|---|---|
| `regional.defaultLanguage` | Platform, Tenant, Organization | `TenantRuntimeSettingsBridge` → `I18nProvider`; used only when no explicit local user locale exists | Tenant override → Platform override/default → definition default | Organization value is not used without an active Organization context. |
| `regional.defaultTimeZone` | Platform, Tenant, Organization | `lib/formatting/date.ts` date/time formatters | Tenant effective value | Organization runtime selection is deferred. |
| `regional.defaultCurrency` | Tenant, Organization | `I18nProvider.formatCurrency` when callers omit an explicit currency; options come from the active tenant currency API | Tenant override → definition default (`null`) | Platform currency is disallowed; Organization runtime selection is deferred. |
| `regional.defaultDateFormat` | Platform, Tenant, Organization | `formatDateValue` when no explicit `Intl` options are supplied | Tenant effective value | Organization runtime selection is deferred; explicit formatter options take precedence. |
| `regional.defaultTimeFormat` | Platform, Tenant, Organization | `formatDateTimeValue` / `formatTime` | Tenant effective value | Organization runtime selection is deferred. |
| `regional.firstDayOfWeek` | Platform, Tenant, Organization | `I18nProvider.firstDayOfWeek` and `DateInput` calendar layout | Tenant effective value | Organization runtime selection is deferred. |
| Password policies | Not in this catalog or UI | Authentication continues to use its existing options configuration | Existing authentication options only | Deferred to Security Settings until configuration is enforced by authentication. |

`GET /api/settings/runtime` returns tenant-effective regional settings. Although the resolver supports Organization scope, runtime consumers currently receive no active Organization ID and therefore must not borrow another Organization's value.

1. Platform, Tenant and Organization overrides share one typed configuration table. A unique active row is defined by scope, owner identity and setting key. Platform rows have no tenant/owner; Tenant rows use the trusted tenant GUID as both TenantId and OwnerId; Organization rows use trusted TenantId plus Organization.Id.
2. Organization identity fields continue to use the existing tenant-owned Organization entity. Organization regional overrides are separate ConfigurationSetting rows. A user's settings access is specific to each Organization and supports multiple Organizations per user.
3. Platform permission is independent from tenant settings permissions. Tenant `settings.read/write` cannot grant Platform access. Organization reads/writes need both the tenant policy and an explicit Organization grant.
4. The resolver supports User > Project > Organization > Tenant > Platform override/default > application default. Project and User APIs, storage and screens are deferred; no request may infer those owners from tenant-level permission.
5. Runtime reads for locale and formatters currently use tenant context. Organization preferences are returned by their settings API but are not applied to global runtime formatters without a selected Organization context.
6. Keep secrets such as credentials, signing/encryption keys and provider tokens outside this ordinary settings store. The definitions reject secret classification.
7. Organization profile logo upload remains unavailable; only existing profile fields are editable.
## Database and migration delivery

Current source now contains three ordered migrations: `InitialFopxPmBaseline`, `ProjectPaymentRequestPersistence`, and `AddTenantRegistry`. The model snapshot is synchronized, and a full idempotent cumulative SQL script has been generated. The original 22-migration inventory below is retained as a historical Sprint 01D record, not as the active migration chain.

Repository migration source IDs present at audit time (their status in the Development database is unknown):

- `20261010182322_NexusUpdatedBaseline`
- `20261011205628_AddDirectoryIntegration`
- `20261012103001_AddLocalPasswordLifecycle`
- `20261012110228_AddNotificationDeliveryFoundation`
- `20261015131616_AddAdministrativeManagerHandover`
- `20261015150917_MakeHandoverSnapshotPreviousManagerNullable`
- `20261018084343_AddProjectPublicId`
- `20261018190000_AddMultiPlanReportingConfiguration`
- `20261021120000_AddProjectChangeRequestApprovals`
- `20261021130000_AddChangeRequestApprovalModelIndexes`
- `20261021160000_AddGovernanceRecoveryPublicIds`
- `20261026121145_AddResourceWorkingCapacity`
- `20261026140000_AllowPlanAssignmentReassignmentAfterDelete`
- `20261026150000_AddWorkingCalendarDayRules`
- `20261026160000_AddPlannedPaymentLifecycle`
- `20261026170000_AddPaymentTenantScopedRelationships`
- `20261027174321_AddStatusReportActionsSupport`
- `20261027202734_AddStatusReportPaymentSnapshotFields`
- `20261028084103_AddMeetingActionWorkItemLink`
- `20261028130000_AddMeetingLifecycleTracking`
- `20261028150000_SeparateMeetingInvitationAndAttendanceStatuses`
- `20261028170000_AddProjectAuditTrail`

The Development database history and physical schema remain unverified. The Tenant migration registers IDs already referenced by Users, Organizations, Roles and ConfigurationSettings before adding tenant foreign keys; production-like or existing Development data still requires pre-deployment review. No database write, migration application or seed operation occurred.

Known repository EF wiring is `NEXUS.Persistence.AppDbContext`, the `NEXUS.Persistence` migration assembly at `apps/api/src/NEXUS.Persistence`, and the startup project `apps/api/src/NEXUS.Api/NEXUS.Api.csproj`, using SQL Server. The actual Development server and database identity were not verified.

### Settings schema reference (implemented in the current source migrations)

The settings tables and their scoped constraints are present in `InitialFopxPmBaseline`; `AddTenantRegistry` adds registry integrity for Tenant- and Organization-scope settings. The following schema notes remain a reference for future review and do not describe a pending settings-table migration.

`ConfigurationSettings` schema:

- `Id uniqueidentifier` primary key; `Scope nvarchar(16)` constrained to the implemented scopes Platform, Tenant and Organization. Project/User remain resolver extension points and are not writable/persistable in this migration.
- `TenantId uniqueidentifier NULL`, `OwnerId uniqueidentifier NULL`, and `OrganizationId uniqueidentifier NULL` (scope-specific FK). Platform requires all three NULL; Tenant requires TenantId = OwnerId and OrganizationId NULL; Organization requires OwnerId = OrganizationId and a composite FK `(TenantId, OrganizationId)` to `Organizations(TenantId, Id)`. The repository still verifies Organization ownership before reads/writes.
- `SettingKey nvarchar(160)`, `ValueType nvarchar(16)` constrained to the catalog types, and `ValueJson nvarchar(max)` with `ISJSON(ValueJson, VALUE) = 1`. This accepts the scalar JSON values used by the typed catalog and requires SQL Server 2022 or later; the active target version remains unverified.
- `CreatedAt datetime2`, `CreatedBy nvarchar(100)`, nullable `UpdatedAt datetime2`/`UpdatedBy nvarchar(100)`, nullable `DeletedAt datetime2`/`DeletedBy nvarchar(100)`, and SQL Server `RowVersion rowversion`.
- Check constraints for valid scope/owner combinations, supported value types and JSON syntax; a nullable `OrganizationId` plus composite FK enforces Tenant/Organization ownership while leaving the polymorphic OwnerId intact. The service validates that each JSON token matches the catalog type, but the current SQL constraints do not cross-check the JSON token kind against `ValueType`; add an equivalent SQL constraint or explicitly retain API-only type matching before migration approval.
- Separate filtered unique indexes to avoid SQL Server nullable-key ambiguity: Platform `(SettingKey)` where Platform and active; Tenant `(TenantId, OwnerId, SettingKey)` where Tenant and active; Organization `(TenantId, OwnerId, SettingKey)` where Organization and active. Add Project/User indexes only with their later owner/FK implementations.
- Active-owner lookup index `(Scope, TenantId, OwnerId)` filtered by `DeletedAt IS NULL`.

`OrganizationSettingsGrants` schema:

- `Id uniqueidentifier` primary key; `TenantId`, `OrganizationId`, `UserId` uniqueidentifier NOT NULL; `CanRead`, `CanManage`, `IsDeleted` bit NOT NULL; `CreatedAt datetime2`, `CreatedBy nvarchar(100)`, nullable `UpdatedAt datetime2`/`UpdatedBy nvarchar(100)`, and SQL Server `RowVersion rowversion`.
- Check constraint `CanManage = 0 OR CanRead = 1`; composite FKs `(TenantId, OrganizationId)` to `Organizations(TenantId, Id)` and `(TenantId, UserId)` to `Users(TenantId, Id)`, both with delete restricted. These require principal alternate keys on `(TenantId, Id)`. Service checks also verify both belong to the authenticated TenantId; a cross-tenant grant must never authorize an API request.
- Unique filtered index `(TenantId, OrganizationId, UserId)` where `IsDeleted = 0`; active-user lookup index `(TenantId, UserId, OrganizationId)` filtered to active readable/manage grants.

Only explicit overrides are stored; defaults are resolved dynamically. Reset soft-deletes the scoped override. Organization profile identity needs no new profile table; it uses existing Organization fields and optimistic `UpdatedAt` checks. The existing `Lookups` table cannot represent typed scoped JSON values.

Before database delivery, review the complete cumulative SQL and legacy Tenant inventory, verify the explicit Development target and migration history, verify an independent backup, apply only with separate authorization, inspect physical schema, and run persistence tests against the isolated intended database. The source-level migration work and model synchronization are summarized in [database-sprint04-tenant-foundation.md](../development/database-sprint04-tenant-foundation.md); no database identity or history was inspected here.
## Completion matrix and remaining gaps

| Area | Status | Evidence / blocker |
|---|---|---|
| Tenant/Organization separation | PASS (source) | Trusted TenantId and OrganizationId are separate; Organization records are tenant-owned. |
| Definitions and resolver | PASS (source) | Typed defaults/validation/scopes and effective Platform → Tenant → Organization resolution; Project/User precedence extension exists without persistence/API. |
| Mandatory policy constraints | PARTIAL | Constrained override validation is fail-closed when configured, but the current setting catalog defines no mandatory policy constraint. Security policy controls are deferred. |
| Read/update/reset APIs | PASS (source), BLOCKED at DB gate | Platform, Tenant and Organization routes use server validation, rowversion and policy/grant boundaries. Durable tables need migration deployment. |
| Organization access | PASS (source), BLOCKED at DB gate | Per-user/per-Organization read/manage grants, same-tenant grant targets, authorization checks and grant admin API/UI are implemented. Grant table is not deployed. |
| Frontend | PASS (source), BLOCKED at DB gate | Three separate workspaces, effective source indicators, Save/Cancel/Reset, server errors, unsaved warnings, EN/AR labels and permission-aware navigation. No browser UAT by instruction. |
| Security | PASS (source), BLOCKED at DB gate | Platform policy independent; tenant context trusted; org owner membership and per-user grants checked server-side. No runtime security/UAT performed. |
| Audit | PASS (source), BLOCKED at DB gate | Configuration/profile/grant audit events share transaction and include old/new values, actor and correlation. Actual table availability is unverified. |
| Runtime consumers | PARTIAL | Tenant language, timezone, currency, date/time formats and first day of week feed React runtime consumers. Organization-specific formatting requires an active Organization context; Project/User runtime consumption is deferred. Password policies are not exposed here and remain for the Security Settings sprint. |
| Persistence source | PASS | EF repository, settings mappings, Tenant entity, and Tenant-scoped relationships are integrated in the model. |
| EF migration/model synchronization | PASS (source) | `AddTenantRegistry` is generated; EF reports no pending model changes. Development database deployment remains pending. |

Current source migration count is **3**: `20261029155617_InitialFopxPmBaseline`, `20261029181208_ProjectPaymentRequestPersistence`, and `20261029183557_AddTenantRegistry`. EF delivery gate fields: model synchronized **Yes**; pending Development migration count/names **unknown** (database history was not queried); Development DB identity **not verified**; independent backup **not verified**; migrations applied **none by this task**; physical schema **not verified**; persistence tests **not run**. The isolated design-time build and offline EF scaffolding/script generation were performed; automated tests, API tests, browser UAT, database writes, migration application and service restarts were not performed.

> **Status update (2026-10-30):** The prior pre-deployment statements in this section are historical. The later [Development delivery record](../development/database-sprint04-tenant-foundation.md#development-delivery-record-2026-10-30) reports the three current migrations applied to the verified Development target, a synchronized model, and zero pending migrations. Fiscal Year Start Month in Sprint 0 reuses the already-migrated `ConfigurationSettings` table and adds no EF schema change.

## Tenant fiscal-year configuration

`financial.fiscalYearStartMonth` is an integer setting with default `1` (January), writable only at Tenant scope. Every Organization and Project in that Tenant inherits the same fiscal calendar; Platform, Organization, Project, and User overrides are rejected. The Tenant Settings screen displays a month selector and a live, server-calculated fiscal-year label and date range.

The fiscal-year label uses the ending year. January-start years use the calendar-year label; for other months the label is the year in which the period ends. The reusable calendar service calculates date-only start/end boundaries and half-open timestamp ranges using the Tenant's existing `regional.defaultTimeZone` setting (UTC when absent), never the API host's local zone.

Updates use the existing serializable `ConfigurationSettings` write and `AuditEntries` transaction. The audit action is `FiscalYearStartMonthChanged`, with Tenant, actor, UTC timestamp, and old/new month values. Changes are blocked if the Tenant has existing `Budget` or `PortfolioBudget` rows containing fiscal-year data, including soft-deleted history. Project budget ceilings and Payment Request allocations are not rewritten. No retrospective calendar migration is included.

Implementation, API, tests, database status, and the Annual Budget prerequisite checklist are tracked in [Tenant Fiscal Year Configuration](../development/tenant-fiscal-year-configuration.md).

## Workflow-specific Project approval settings

Project Annual Budget approval defaults are a workflow-owned Project setting, not a generic `ConfigurationSetting` key. The Project's existing `FinanceApprovalMode` continues to constrain allowed methods; the Annual Budget configuration stores that workflow's default method and mandatory approvers. No higher-level approval-default inheritance contract exists today, so resolution is Project-scoped. This narrowly scoped configuration does not enable general Project/User settings persistence or change Payment Request/Year-End Closing defaults. Its EF table mappings require a separate owner-reviewed additive migration; they are not database-synchronized by this source implementation.
