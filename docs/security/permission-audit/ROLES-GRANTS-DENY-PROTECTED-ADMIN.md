# Roles, Grants, Deny, and Protected Admin

**Generated:** 2026-11-13  
**Source:** `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs`, `UserPermissionOverride.cs`, `PermissionCatalog.cs`, `ProtectedTenantAdminPolicy.cs`, `EffectivePermissionResolver.cs`, `RoleAdministrationService.cs`, `UserEffectivePermissionService.cs`, `BootstrapPermissionDelegationPolicy.cs`, `SeedDataProvider.cs`

---

## 1. Role Model

### Role Entity (`Role.cs`)
```csharp
public class Role : BaseEntity
{
    public const string TenantAdminSystemRoleKey = "tenant.admin";

    public string Name { get; set; }           // Display name (editable)
    public string? Description { get; set; }
    public string? Category { get; set; }      // Tenant, Organization, Management, Operation, Business, System
    public bool IsSystem { get; set; }         // System roles are protected
    public string? SystemRoleKey { get; private set; }  // Immutable identity for system roles
    public ICollection<UserRole> UserRoles { get; set; }
    public ICollection<Permission> Permissions { get; set; }
}
```

**Key Properties:**
- `SystemRoleKey`: Immutable, assigned once via `AssignSystemRoleKey()` — used for identity, not display name
- `IsSystem`: Distinguishes system roles (protected) from custom roles
- `Category`: Descriptive only; selectable values: `Tenant`, `Organization`, `Management`, `Operation`, `Business`; `System` is reserved

### Protected System Admin Role
Identified by: `role.IsSystem == true && role.SystemRoleKey == "tenant.admin" && role.TenantId != Guid.Empty`

---

## 2. Permission Grants (Role → Permission)

### Storage
- Many-to-many: `Role.Permissions` collection
- Persisted in `RolePermission` join table (implicit via EF Core)
- Each grant links a `Role` to a `Permission` entity (tenant-scoped)

### Grant Rules (from `RoleAdministrationService.cs`)

| Rule | Enforcement Point |
|------|-------------------|
| Only active catalog permissions (`IsActive=true`) | `GetAssignablePermissionsAsync` |
| Only assignable permissions (`IsAssignable=true`) | `ResolvePermissionsAsync` |
| Excluded scopes: `Platform`, `CrossTenant`, `SystemOwner` | `ResolvePermissionsAsync` |
| Administrative permissions (module=`administration`) require actor's own effective permission | `CanManageRole` |
| Ordinary permissions: actor must have permission OR bootstrap delegation | `CanManageRole` |
| View dependencies must be granted alongside Action permissions | `ResolvePermissionsAsync` |
| Max 1000 permissions per role | `MaximumPermissionsPerRole` constant |

### Delegation Tiers

| Tier | Who Can Grant | Scope |
|------|---------------|-------|
| **Administrative** | Actor must have the permission in their effective set | All assignable scopes |
| **Custom Role Delegation** | Actor has `Roles.ConfigurePermissions` + permission is in assignable scope (Tenant/Org/Portfolio/Program/Project) | Non-administrative only |
| **Bootstrap Delegation** | Actor has `Roles.ConfigurePermissions` + `TenantAdministration.ManageScope` + no Deny on bootstrap keys | 46 specific workflow keys (see `BootstrapPermissionDelegationPolicy.Keys`) |

### Bootstrap Delegation Keys (46 permissions)
```csharp
// Project Planning
"ProjectPlan.Create", "Planning.Edit", "Planning.View"

// Annual Budget (Project-scoped)
"ProjectAnnualBudget.View", "ProjectAnnualBudget.Create", "ProjectAnnualBudget.EditDraft",
"ProjectAnnualBudget.ConfigureDraftApproval", "ProjectAnnualBudget.SubmitForReview",
"ProjectAnnualBudget.Approve", "ProjectAnnualBudget.ReviseApproved",
"ProjectAnnualBudget.ManageApprovalConfiguration", "ProjectAnnualBudget.Delete",
"ProjectAnnualBudget.Withdraw", "ProjectAnnualBudgetClosing.View", "ProjectAnnualBudget.Close",
"ProjectAnnualBudget.CloseApprove", "ProjectAnnualBudget.CarryForward"

// Financial Rollups
"ProjectFinancialFoundation.View", "CostUtilization.Export",
"ProgramFinancialRollup.View", "PortfolioFinancialRollup.View"
```

**Protected Admin Subset (14 keys)** — only these can be added to protected Admin role:
```csharp
"ProjectAnnualBudget.View" through "ProjectAnnualBudget.CarryForward" (14 keys)
```

---

## 3. User Permission Overrides (Allow/Deny)

### Entity (`UserPermissionOverride.cs`)
```csharp
public enum PermissionOverrideEffect { Inherit = 0, Allow = 1, Deny = 2 }

public sealed class UserPermissionOverride : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; }
    public PermissionOverrideEffect Effect { get; set; } = PermissionOverrideEffect.Inherit;
}
```

### Resolution Algorithm (`EffectivePermissionResolver.cs`)
```csharp
public static HashSet<string> Resolve(
    IEnumerable<string> inheritedPermissions,
    IEnumerable<(string Permission, PermissionOverrideEffect Effect)> overrides,
    IEnumerable<string>? mandatoryPermissions = null)
{
    var effective = inheritedPermissions.ToHashSet();
    var denied = new HashSet<string>();

    foreach (var item in overrides)
    {
        if (item.Effect == PermissionOverrideEffect.Allow)
            effective.Add(item.Permission);
        else if (item.Effect == PermissionOverrideEffect.Deny)
            denied.Add(item.Permission);
    }

    effective.ExceptWith(denied);  // Deny wins over Allow/Inherit

    // Protected Admin baseline: mandatory permissions cannot be removed by Deny
    if (mandatoryPermissions is not null)
        effective.UnionWith(mandatoryPermissions);

    return effective;
}
```

### Override Precedence
1. **Role grants** (inherited) → baseline
2. **User Allow** → adds permission
3. **User Deny** → removes permission (wins over Allow/Inherit)
4. **Protected Admin mandatory baseline** → re-adds mandatory permissions even if Denied

### UI Exposure (`AccessUsersPage.tsx`, `UserPermissionsPage.tsx`)
- Users with `Users.ManageOverrides` can set `Inherit` / `Allow` / `Deny` per permission
- Protected Admin baseline permissions show as locked (`Inherit` only)
- Actor can only override permissions they themselves possess (`hasPermission(actorPermissions, permission.name)`)

---

## 4. Protected Tenant Admin Baseline

### Policy Definition (`ProtectedTenantAdminPolicy.cs`)

**Mandatory Permission Keys = Dynamic Baseline:**
```csharp
public static IReadOnlySet<string> MandatoryPermissionKeys =>
    GetAllTenantPermissionKeys(PermissionCatalog.Entries)  // ALL active Tenant-scoped
        .Concat(PortfolioProgramPermissionKeys)             // + approved Portfolio/Program/Handover
        .ToHashSet();
```

**Components:**
1. **All active Tenant-scoped permissions** (141 keys per catalog extraction) — auto-inherited
2. **Approved Portfolio/Program/Handover keys** (14 keys):
   - `Portfolio.View`, `Portfolio.Create`, `Portfolio.Edit`, `Portfolio.Archive`, `Portfolio.Reactivate`
   - `Program.View`, `Program.Create`, `Program.Edit`, `Program.Archive`, `Program.Reactivate`
   - `Handover.View`, `Handover.Manage`, `Handover.Approve`, `Handover.AdminInitiate`

**Legacy Baseline (backward compatibility):**
```csharp
GetTenantAdministrationPermissionKeys() // administration module OR IsTenantAdminBaseline=true (18 keys)
```

### Protection Mechanisms

| Mechanism | Location | Behavior |
|-----------|----------|----------|
| **Role grant rejection** | `RoleAdministrationService.UpdateAsync` | Rejects removal of any `MandatoryPermissionKeys` from protected Admin role |
| **Deny override rejection** | `UserEffectivePermissionService.GetEffectivePermissionsAsync` | Logs error if Deny on mandatory key; `EffectivePermissionResolver` re-adds mandatory keys |
| **Seed reconciliation** | `SeedDataProvider.ReconcileCanonicalPermissionsAsync` | Adds missing mandatory grants; archives conflicting Deny overrides |
| **Last-admin guard** | `TenantAdminMutationGuard` + `RoleAdministrationService` | Prevents removing last effective tenant administrator |

### Protected Admin Bootstrap Grants
Separate from mandatory baseline — additive Project-scoped Annual Budget permissions (14 keys) that can be granted to protected Admin role via bootstrap delegation.

---

## 5. Effective Permission Computation (`UserEffectivePermissionService.cs`)

```csharp
public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId)
{
    // 1. Role-inherited permissions
    var inherited = from UserRole → Role.Permissions (tenant-filtered, non-deleted)
        select Resource.Action

    // 2. User overrides
    var overrides = from UserPermissionOverride (tenant-filtered, non-deleted)
        select (Permission, Effect)

    // 3. Protected Admin check
    var hasProtectedAdminRole = UserRole exists with Role.SystemRoleKey == "tenant.admin"

    // 4. Resolve with mandatory baseline
    var mandatory = hasProtectedAdminRole ? ProtectedTenantAdminPolicy.MandatoryPermissionKeys : empty

    // 5. Apply resolver (Deny wins, but mandatory re-added)
    var effective = EffectivePermissionResolver.Resolve(inherited, overrides, mandatory)

    // 6. Enforce View dependencies
    return PermissionCatalog.EnforceDependencies(effective);
}
```

### Dependency Enforcement (`PermissionCatalog.EnforceDependencies`)
- Iteratively removes Action permissions whose required View dependencies are missing
- Runs until stable (no more removals)
- Applied AFTER Deny resolution and mandatory baseline

---

## 6. Seed-Time Reconciliation (`SeedDataProvider.cs`)

### `ReconcileCanonicalPermissionsAsync` Flow
1. **Catalog sync**: Add/normalize/retire Permission rows to match `PermissionCatalog`
2. **Protected Admin reconciliation**:
   - Find the single protected Admin role (`IsProtectedSystemAdminRole`)
   - Add missing `MandatoryPermissionKeys` grants
   - Add missing `ProtectedAdminProjectSettingsPermissionKeys` (14 Annual Budget keys)
   - Audit log: `ProtectedAdminPermissions.Reconciled`
3. **Deny cleanup for protected Admin members**:
   - Find Deny overrides on mandatory keys for Admin role members
   - Archive them (`Effect=Inherit`, `IsDeleted=true`)
   - Audit log: `PermissionOverride.ProtectedAdminDenyReconciled`
4. **Legacy grant cleanup**: Remove role grants to retired permissions
5. **Legacy override cleanup**: Archive overrides on retired permissions

### Key Invariants
- Runs every seed cycle (idempotent)
- Never removes non-mandatory grants from protected Admin role
- Never removes grants from custom roles except retired permissions
- Audits all changes with `AuditSources.System`

---

## 7. Role Categories (`RoleCategories.cs`)

| Category | Selectable for Custom Roles | Purpose |
|----------|----------------------------|---------|
| `Tenant` | Yes | Tenant-wide administrative roles |
| `Organization` | Yes | Organization-scoped roles |
| `Management` | Yes | Management roles |
| `Operation` | Yes | Operational roles |
| `Business` | Yes | Business function roles |
| `System` | **No** | Reserved for protected system roles |

---

## 8. Verification Status

| Component | Source Verified | Runtime Verified | Persistence Verified |
|-----------|-----------------|------------------|---------------------|
| Role model & SystemRoleKey | ✅ | ❌ | ❌ |
| Grant rules (admin/custom/bootstrap) | ✅ | ❌ | ❌ |
| UserPermissionOverride entity | ✅ | ❌ | ❌ |
| EffectivePermissionResolver algorithm | ✅ | ❌ | ❌ |
| ProtectedTenantAdminPolicy baseline | ✅ | ❌ | ❌ |
| UserEffectivePermissionService computation | ✅ | ❌ | ❌ |
| RoleAdministrationService grant validation | ✅ | ❌ | ❌ |
| BootstrapPermissionDelegationPolicy | ✅ | ❌ | ❌ |
| SeedDataProvider reconciliation | ✅ | ❌ | ❌ |
| Last-admin guard (TenantAdminMutationGuard) | ✅ | ❌ | ❌ |
| UI: locked mandatory grants | ✅ | ❌ | N/A |
| UI: Allow/Deny/Inherit selector | ✅ | ❌ | N/A |

**All runtime/persistence verification blocked by SQL Server identity failure** — no live database connection established.

---

## 9. Evidence References

| Concept | Source File | Key Lines |
|---------|-------------|-----------|
| Role entity & SystemRoleKey | `Domain/Role.cs` | 1-36 |
| PermissionOverride entity | `Domain/UserPermissionOverride.cs` | 1-19 |
| ProtectedTenantAdminPolicy | `Application/ProtectedTenantAdminPolicy.cs` | 1-105 |
| EffectivePermissionResolver | `Application/EffectivePermissionResolver.cs` | 1-31 |
| RoleAdministrationService grants | `Infrastructure/RoleAdministrationService.cs` | 13-792 |
| UserEffectivePermissionService | `Infrastructure/UserEffectivePermissionService.cs` | 1-108 |
| BootstrapPermissionDelegationPolicy | `Infrastructure/BootstrapPermissionDelegationPolicy.cs` | 1-102 |
| Seed reconciliation | `NEXUS.Persistence/SeedDataProvider.cs` | 693-830 |
| Catalog EnforceDependencies | `Application/PermissionCatalog.cs` | 1148-1168 |
| RoleCategories | `Application/RoleCategories.cs` | 1-22 |
| Frontend: locked mandatory | `web-react/.../RolesPage.tsx` | 240-243, 420-422 |
| Frontend: Allow/Deny UI | `web-react/.../AccessPages.tsx` | 311-404 |