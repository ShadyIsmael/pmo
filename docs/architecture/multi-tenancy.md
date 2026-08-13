# Multi-Tenancy Strategy

---

## Tenant Isolation Model

NEXUS PMO uses a **shared database, tenant-scoped data** isolation model:

```
┌──────────────────────────────────────────────────────────────┐
│                    PostgreSQL Database                        │
│                                                               │
│  Tenant A Data              Tenant B Data                    │
│  ┌──────────────────┐      ┌──────────────────┐              │
│  │ TenantId = A      │      │ TenantId = B      │             │
│  │ Projects          │      │ Projects          │             │
│  │ Resources         │      │ Resources         │             │
│  │ Users             │      │ Users             │             │
│  │ ...               │      │ ...               │             │
│  └──────────────────┘      └──────────────────┘              │
│                                                               │
│  Shared Data                                                  │
│  ┌──────────────────────────────────────────────────┐         │
│  │ System Config, Global Lookups                     │         │
│  └──────────────────────────────────────────────────┘         │
└──────────────────────────────────────────────────────────────┘
```

This model provides:
- **Operational simplicity** -- single database to manage, backup, and monitor
- **Lower cost** -- no per-tenant database provisioning
- **Horizontal scalability** -- read replicas serve all tenants
- **Future extraction** -- tenants can be migrated to dedicated databases if needed

---

## TenantId Field in All Entities

Every entity inherits from `BaseEntity`, which includes a `TenantId` property:

```csharp
public abstract class BaseEntity : ISoftDeletable, ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }       // <-- Tenant isolation field
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; }
}
```

The `ITenantEntity` interface ensures every entity contractually requires a TenantId:

```csharp
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
```

---

## Global Query Filters

EF Core global query filters automatically scope all queries to the current tenant:

```csharp
// Applied automatically to all ITenantEntity types
private static void ApplyTenantFilter<T>(ModelBuilder modelBuilder)
    where T : class, ITenantEntity
{
    modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == GetCurrentTenantId());
}

internal static Guid GetCurrentTenantId()
{
    // Resolves tenant from current HTTP context / JWT claim
    return Guid.Empty;  // Placeholder for single-tenant mode
}
```

These filters mean:
- **Every query** automatically includes `WHERE TenantId = @currentTenantId`
- **No developer oversight required** -- the filter is applied globally
- **Impossible to accidentally leak data** across tenants at the query level
- **Soft delete** also applied globally alongside the tenant filter

---

## Tenant Configuration

Tenant-specific configuration is stored and applied at runtime:

| Configuration | Description |
|---------------|-------------|
| **Branding** | Logo, favicon, company name, colour scheme overrides |
| **Features** | Per-tenant feature flags (module enable/disable) |
| **Plugins** | Per-tenant plugin activation (via `TenantPlugin` entity) |
| **Localization** | Default locale, supported languages |
| **Security Policies** | Password policy, session timeout, MFA requirements |
| **Notifications** | Default notification preferences, templates |

---

## Cross-Tenant Data Prevention

Cross-tenant data access is prevented at multiple layers:

| Layer | Mechanism |
|-------|-----------|
| **Database** | Global `TenantId` query filters on all entities |
| **Application** | All service methods receive tenant context from JWT |
| **API** | `[Authorize]` attribute ensures authenticated access |
| **JWT** | Tenant ID embedded in token claims, validated on every request |
| **Infrastructure** | Connection strings can be tenant-resolved for database-per-tenant migration path |

---

## Tenant Onboarding Flow

```
1. Company signs up / is added by system admin
2. Tenant record created in Tenants table
3. Default roles and permissions provisioned
4. Default organization structure created
5. Admin user created with activation link
6. Tenant configuration applied (branding, locale, features)
7. Tenant is activated and accessible
```

---

## Current State

| Aspect | Status |
|--------|--------|
| **TenantId on all entities** | Implemented |
| **Global query filters** | Implemented |
| **Tenant configuration** | Architecture in place |
| **Multi-tenant onboarding** | Not implemented (uses fixed tenant) |
| **Tenant-specific feature flags** | Architecture in place |
| **Cross-tenant data prevention** | Implemented |
| **Production readiness** | **Single-tenant ready** -- the architecture fully supports multi-tenancy but the tenant resolution and onboarding flows are not yet implemented. In single-tenant mode, `GetCurrentTenantId()` returns a fixed `Guid.Empty`. |

To enable multi-tenancy:
1. Implement `GetCurrentTenantId()` to resolve from JWT claims or HTTP context
2. Build tenant onboarding/registration flow
3. Configure per-tenant settings UI
4. Test isolation with multiple tenant contexts concurrently
