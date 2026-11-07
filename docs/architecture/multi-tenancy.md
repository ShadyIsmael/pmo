# Multi-Tenancy and Configuration Scope

## Identity model

FOPX PM uses a shared SQL Server / EF Core model with a persisted Tenant registry and tenant-owned records carrying `TenantId`. The authenticated `tenantId` claim is the trusted tenant boundary and is validated against an Active Tenant registry record. `AddTenantRegistry` adds the source model and schema; the migration still requires controlled Development deployment.

The application must derive TenantId from the authenticated principal. Settings requests never accept TenantId from the body or query string. Missing or malformed request context fails closed. The EF query filter returns no tenant rows when a request has no valid TenantId claim; the demo fallback is retained only outside HTTP requests for existing development initialization paths. Requests for unregistered, suspended, provisioning, or retired tenants are denied by the registry status validator.

`Organization` is a separate persisted entity with its own `Id` and `TenantId`. One tenant can own multiple Organizations. Organization business units, departments and teams remain attached to the Organization hierarchy. Tenant settings and Organization settings are distinct scopes.

## Data isolation

- Tenant-owned `BaseEntity` records are subject to the tenant query filter in `AppDbContext`.
- Before `SaveChanges` or `SaveChangesAsync`, tracked `ITenantEntity` additions, updates, and deletes are rejected unless their non-empty `TenantId` matches the authenticated tenant or an explicit `TrustedTenantExecutionContext`; existing tracked rows cannot be reassigned to another tenant. Services must still enforce project and resource scope, and background workers must establish an explicit trusted tenant scope.
- `Organizations.TenantId` references `Tenants.Id` with restricted deletion. Tenant and Organization settings also reference the registry; Platform settings retain nullable `TenantId` and remain platform-wide.
- Services that intentionally use `IgnoreQueryFilters()` must add explicit predicates for the trusted TenantId, owner ID, active/deleted state, and any required per-user grant.
- Platform configuration rows are global and have null TenantId/OwnerId. The settings repository scopes every query by scope and owner; platform access also requires dedicated PlatformSettings authorization.
- A requested Organization must be verified against `(Organization.Id, Organization.TenantId)` before configuration reads/writes.
- Tenant `Settings.View` / `Settings.ManageTenantConfiguration` does not imply access to every Organization. `OrganizationSettingsGrant` grants a user read/manage access to one Organization's settings and existing identity fields. A user can have grants for multiple Organizations. These grants do not imply general Organization membership or Organization Management authority.

## Configuration scopes and precedence

| Scope | Owner identity | Current delivery |
|---|---|---|
| Platform | `(Scope=Platform, TenantId=NULL, OwnerId=NULL)` | Defaults, global regional settings, read-only product identity |
| Tenant | `(Scope=Tenant, TenantId=trusted tenant, OwnerId=trusted tenant)` | Tenant regional defaults |
| Organization | `(Scope=Organization, TenantId=trusted tenant, OwnerId=Organization.Id)` | Existing Organization identity plus regional overrides |
| Project | Tenant plus Project owner | Resolver extension only; no settings API/storage enabled |
| User | Tenant plus User owner | Resolver extension only; no settings API/storage enabled |

The server resolver precedence is User → Project → Organization → Tenant → Platform override/default → setting definition default. Only definitions that declare a scope can be overridden at that scope. A missing Organization override inherits the effective Tenant value; a missing Tenant override inherits the Platform value/default. Mandatory Platform-only constraints cannot be weakened at lower scopes. User-selected locale remains a separate explicit client preference and takes precedence over inherited defaults.

## Settings authorization

- Platform reads/writes require `PlatformSettings.Read` / `PlatformSettings.Write`, evaluated from trusted platform roles or the deployment-managed administrator allowlist. Tenant roles cannot grant these permissions.
- Tenant settings reads require `Settings.View`; writes require `Settings.ManageTenantConfiguration`.
- Organization settings require the corresponding tenant-level permission plus an active per-user Organization grant. Reads need `CanRead` or `CanManage`; writes require `CanManage` and tenant `Settings.ManageTenantConfiguration`.
- Grant management requires tenant `Settings.ManageTenantConfiguration`; grant targets are restricted to active users in the authenticated tenant and selected Organizations in that same tenant.

## Persistence boundaries

`ConfigurationSettings` has a polymorphic owner identity and no ambient tenant filter because Platform rows have no tenant. The EF repository applies explicit owner predicates, uses SQL Server rowversion for optimistic concurrency, and soft-deletes an override on reset. Tenant- and Organization-scope settings require a valid Tenant registry row. `OrganizationSettingsGrants` is tenant-owned and has a unique active `(TenantId, OrganizationId, UserId)` key.

The source model includes Tenant, Organization-to-Tenant and settings-to-Tenant relationships. `AddTenantRegistry` is the third ordered migration after `InitialFopxPmBaseline` and `ProjectPaymentRequestPersistence`; the snapshot is synchronized at source level. The migration backfills registry records for TenantIds already referenced by Users, Organizations, Roles, and settings. Development database identity, applied migration history, backup, and physical schema remain unverified.

Organization-specific regional settings are exposed by their API/workspace, but current global runtime formatters remain tenant-scoped because the application has no single active Organization context. Project/User configuration remains deferred.
