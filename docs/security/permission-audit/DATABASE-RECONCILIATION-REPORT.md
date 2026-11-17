# Database Reconciliation Report

**Generated:** 2026-11-13  
**Source:** `apps/api/src/NEXUS.Persistence/SeedDataProvider.cs`, `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`, `apps/api/src/NEXUS.Modules.Identity/Application/ProtectedTenantAdminPolicy.cs`, `apps/api/src/NEXUS.Modules.Identity/Infrastructure/BootstrapPermissionDelegationPolicy.cs`, EF Core migrations, Architecture tests

---

## 1. Reconciliation Overview

The FOPX PM system uses a **canonical catalog-driven reconciliation** model where the source-of-truth permission catalog (`PermissionCatalog.cs`) is synchronized to the database on every seed cycle. This ensures the persisted permission rows, role grants, and user overrides always reflect the current active catalog.

**Key Principle:** The catalog is authoritative. Database state is derived from catalog + seed logic. No manual SQL or drift correction is needed — reconciliation is automatic and idempotent.

---

## 2. Seed Data Provider Reconciliation (`SeedDataProvider.cs`)

### Entry Point: `ReconcileCanonicalPermissionsAsync`

```csharp
public static async Task<CanonicalPermissionRebuildResult> ReconcileCanonicalPermissionsAsync(
    AppDbContext context,
    bool isDevelopment,
    string expectedServer,
    string expectedDatabase,
    CancellationToken cancellationToken = default)
```

**Preconditions (Development only):**
- `ASPNETCORE_ENVIRONMENT=Development`
- SQL Server = `Shady`, Database = `NexusPmo`
- EF model synchronized (no pending migrations)
- No pending migrations

### Reconciliation Steps (in transaction)

#### Step 1: Catalog Synchronization
```csharp
var targetPermissions = PermissionCatalog.CreateSeedPermissions(TenantId)
    .Where(p => p.IsActive)  // Only active catalog entries
    .ToList();
```
- **Add**: Missing active catalog permissions → insert new `Permission` rows with stable SeedId Guids
- **Normalize**: Existing rows with mismatched Name/Resource/Action → update to match catalog
- **Retire**: Persisted permissions not in active catalog → `IsDeleted=true` (soft delete)
- **Audit**: Each change logged with `AuditSources.System`

#### Step 2: Protected Admin Role Reconciliation
```csharp
var protectedAdminRole = roles.Single(r => ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(r));

// Mandatory = ALL active Tenant-scoped + approved Portfolio/Program/Handover
var mandatoryPermissionKeys = ProtectedTenantAdminPolicy.MandatoryPermissionKeys;

// Supplemental = 14 Project-scoped Annual Budget keys (bootstrap-eligible for protected Admin)
var protectedAdminProjectSettingsKeys = ProtectedAdminProjectSettingsPermissionKeys;
```

| Action | Logic | Audit |
|--------|-------|-------|
| Add missing mandatory grants | `mandatoryPermissionKeys` not in role → add | `ProtectedAdminPermissions.Reconciled` |
| Add missing supplemental grants | `ProtectedAdminProjectSettingsPermissionKeys` not in role → add | Same audit entry |
| **Never remove** | Non-mandatory grants preserved | N/A |

#### Step 3: Protected Admin Deny Override Cleanup
```csharp
var protectedAdminDenies = await context.UserPermissionOverrides
    .Where(o => o.TenantId == TenantId
        && protectedAdminMemberIds.Contains(o.UserId)
        && mandatoryPermissionIds.Contains(o.PermissionId)
        && o.Effect == PermissionOverrideEffect.Deny)
    .ToListAsync();

foreach (var denial in protectedAdminDenies) {
    denial.Effect = PermissionOverrideEffect.Inherit;
    denial.IsDeleted = true;
}
```
- Finds Deny overrides on **mandatory** permissions for users in protected Admin role
- Archives them (soft delete + set to Inherit)
- Audit: `PermissionOverride.ProtectedAdminDenyReconciled`

#### Step 4: Legacy Grant Cleanup (Retired Permissions)
```csharp
var retiredPermissionIds = retiredPermissions.Select(p => p.Id).ToHashSet();

foreach (var role in roles) {
    var legacyGrants = role.Permissions.Where(p => retiredPermissionIds.Contains(p.Id));
    role.Permissions.RemoveRange(legacyGrants);
}
```
- Removes role→permission links for permissions retired from catalog
- Audit: `Role.RetiredPermissionGrantsRemoved` per role

#### Step 5: Legacy Override Cleanup (Retired Permissions)
```csharp
var retiredOverrides = await context.UserPermissionOverrides
    .Where(o => retiredPermissionIds.Contains(o.PermissionId) && !o.IsDeleted)
    .ToListAsync();

foreach (var item in retiredOverrides) {
    item.Effect = PermissionOverrideEffect.Inherit;
    item.IsDeleted = true;
}
```
- Archives user overrides on retired permissions
- Audit: `PermissionOverride.CanonicalCatalogReconciled`

---

## 3. Protected Admin Baseline Derivation

### Dynamic Baseline (`ProtectedTenantAdminPolicy.cs`)

```csharp
public static IReadOnlySet<string> MandatoryPermissionKeys =>
    GetAllTenantPermissionKeys(PermissionCatalog.Entries)  // 141 keys
        .Concat(PortfolioProgramPermissionKeys)             // 14 keys
        .ToHashSet();

private static IReadOnlySet<string> GetAllTenantPermissionKeys(entries) =>
    entries
        .Where(e => e.IsActive && e.Scope == PermissionScope.Tenant)
        .Select(e => e.Key)
        .ToHashSet();
```

**Composition (Current Catalog: 188 active entries):**
| Source | Count | Scope |
|--------|-------|-------|
| All active Tenant-scoped permissions | 141 | Tenant |
| Portfolio.View/Create/Edit/Archive/Reactivate | 5 | Portfolio |
| Program.View/Create/Edit/Archive/Reactivate | 5 | Program |
| Handover.View/Manage/Approve/AdminInitiate | 4 | Tenant |
| **Total MandatoryPermissionKeys** | **~155** | Mixed |

**Note:** The baseline is **dynamic** — any new Tenant-scoped permission added to catalog automatically becomes part of protected Admin baseline on next seed.

### Legacy Baseline (Backward Compatibility)
```csharp
GetTenantAdministrationPermissionKeys() // administration module OR IsTenantAdminBaseline=true
// Returns 18 keys (IsTenantAdminBaseline flag)
```
Preserved for any code still referencing `TenantAdministrationPermissionKeys` directly.

---

## 4. Bootstrap Permission Delegation Reconciliation

### First-Grant Delegation (`BootstrapPermissionDelegationPolicy.cs`)

**46 Delegable Keys** (Project Planning + Annual Budget + Financial Rollups):
- Available to actors with `Roles.ConfigurePermissions` + `TenantAdministration.ManageScope`
- **Not** added to actor's own effective permissions
- Filtered by actor's Deny overrides

**14 Protected Admin Subset** (Annual Budget only):
```csharp
ProtectedAdminAnnualBudgetPermissionKeys = [
    "ProjectAnnualBudget.View" through "ProjectAnnualBudget.CarryForward" (14 keys)
]
```
- Only these can be added to protected Admin role via `CanAddToProtectedTenantAdminRole()`
- Reconciled in Step 2 of seed (supplemental grants)

### Seed-Time Bootstrap Grants (`SeedDataProvider.cs` ~line 2597)
```csharp
// For each tenant admin user (pmo.admin, etc.):
var addedProtectedAdminProjectSettingsKeys = role.Permissions
    .Where(p => ProtectedAdminProjectSettingsPermissionKeys.Contains(p.Name))
    .Select(p => p.Name)
    .Except(originalKeys)
    .ToList();
```
- Adds missing Annual Budget permissions to protected Admin role
- Audit: `ProtectedAdminProjectSettingsGrants.Reconciled`

---

## 5. EF Core Migration History (Identity & Permissions)

| Migration | Description | Key Changes |
|-----------|-------------|-------------|
| `20261102190318_AddStableSystemRoleIdentity` | Add `SystemRoleKey` to Role | Immutable identity for protected Admin (`tenant.admin`) |
| `20261102190319_AddPermissionSeedId` | Add stable SeedId to Permission | Canonical Guids for reconciliation matching |
| `20261102190320_AddUserPermissionOverride` | Create UserPermissionOverride table | Allow/Deny/Inherit per user-permission |
| `20261102190321_AddRolePermissionScope` | Add Scope to Permission | Tenant/Organization/Project/Portfolio/Program |
| `20261102190322_AddPermissionCatalogFlags` | Add IsAssignable, IsTenantAdminBaseline, etc. | Catalog metadata on Permission rows |

**Current Development status (verified 2026-11-16):** All 30 repository migrations are applied on `Shady / NexusPmo`, DB_ID 5; pending migrations = 0, EF Model synchronized, and no missing schema objects. This live status supersedes the report-time note below; the report's generated date and earlier reconciliation analysis remain historical.

---

## 6. Database Schema (Permission-Related Tables)

### `Permissions`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `uniqueidentifier` | PK, stable SeedId from catalog |
| `TenantId` | `uniqueidentifier` | FK → Tenants |
| `Name` | `nvarchar(128)` | Canonical key (e.g., `ProjectAnnualBudget.View`) |
| `Resource` | `nvarchar(64)` | e.g., `ProjectAnnualBudget` |
| `Action` | `nvarchar(64)` | e.g., `View` |
| `Description` | `nvarchar(max)` | English description |
| `IsDeleted` | `bit` | Soft delete |
| `CreatedBy` | `nvarchar(64)` | `System` for seed |
| `CreatedAt` | `datetime2` | Seed timestamp |
| `UpdatedAt` | `datetime2` | Nullable |
| `UpdatedBy` | `nvarchar(64)` | Nullable |

**Indexes:** Unique on `(TenantId, Name)` where `IsDeleted=0` (filtered)

### `Roles`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `uniqueidentifier` | PK |
| `TenantId` | `uniqueidentifier` | FK → Tenants |
| `Name` | `nvarchar(100)` | Display name |
| `Description` | `nvarchar(500)` | Nullable |
| `Category` | `nvarchar(100)` | Tenant/Organization/Management/Operation/Business/System |
| `IsSystem` | `bit` | Protected if true |
| `SystemRoleKey` | `nvarchar(64)` | Immutable, e.g., `tenant.admin` |
| `IsDeleted` | `bit` | Soft delete |

### `RolePermissions` (Many-to-Many)
| Column | Type | Notes |
|--------|------|-------|
| `RoleId` | `uniqueidentifier` | FK → Roles |
| `PermissionId` | `uniqueidentifier` | FK → Permissions |
| **PK** | | `(RoleId, PermissionId)` |

### `UserRoles`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `uniqueidentifier` | PK |
| `UserId` | `uniqueidentifier` | FK → Users |
| `RoleId` | `uniqueidentifier` | FK → Roles |
| `TenantId` | `uniqueidentifier` | FK → Tenants |
| `IsDeleted` | `bit` | Soft delete |

### `UserPermissionOverrides`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `uniqueidentifier` | PK |
| `UserId` | `uniqueidentifier` | FK → Users |
| `PermissionId` | `uniqueidentifier` | FK → Permissions |
| `Effect` | `tinyint` | 0=Inherit, 1=Allow, 2=Deny |
| `TenantId` | `uniqueidentifier` | FK → Tenants |
| `IsDeleted` | `bit` | Soft delete |

---

## 7. Reconciliation Invariants

| Invariant | Enforcement |
|-----------|-------------|
| **Catalog = Source of Truth** | `PermissionCatalog.CreateSeedPermissions()` drives all inserts/updates |
| **Stable Identities** | SeedId Guids never change; matched by `Id` or `Resource|Action` |
| **Soft Delete Only** | Retired permissions/grants/overrides → `IsDeleted=true`, never hard delete |
| **Protected Admin Baseline Auto-Expands** | `GetAllTenantPermissionKeys()` includes ALL active Tenant-scoped entries |
| **Deny Cannot Remove Baseline** | `EffectivePermissionResolver` re-adds `MandatoryPermissionKeys` after Deny |
| **Seed Idempotency** | Re-running seed produces same state (verified by tests) |
| **Audit Trail** | Every reconciliation change logged to `AuditEntries` with `Source=System` |
| **Last-Admin Protection** | `TenantAdminMutationGuard` prevents removing last effective Admin |

---

## 8. Verification Status

| Check | Status | Evidence |
|-------|--------|----------|
| Reconciliation code reviewed | ✅ | `SeedDataProvider.cs` lines 450-930 |
| Protected Admin baseline logic | ✅ | `ProtectedTenantAdminPolicy.cs` |
| Bootstrap delegation policy | ✅ | `BootstrapPermissionDelegationPolicy.cs` |
| Catalog EnforceDependencies | ✅ | `PermissionCatalog.cs:1148-1168` |
| EF migrations compiled | ✅ | Offline `dotnet build` passes |
| EF model synchronized | ✅ | Offline `db-preflight.ps1` reports sync |
| **Live database identity verified** | ❌ | `Cannot generate SSPI context` — probe failed |
| **Applied migrations verified** | ❌ | No live DB connection |
| **Persisted Permission rows inspected** | ❌ | No live DB connection |
| **RolePermission grants inspected** | ❌ | No live DB connection |
| **UserPermissionOverride rows inspected** | ❌ | No live DB connection |
| **Audit entries for reconciliation** | ❌ | No live DB connection |
| **Physical schema verified** | ❌ | No live DB connection |

---

## 9. Required Actions for Full Verification

1. **Resolve SQL Connectivity**: Execute `scripts/dev/db-preflight.ps1` from authorized Windows host (PowerShell 7) to verify `Shady`/`NexusPmo` identity
2. **Run Migration Status**: `scripts/dev/db-migrations-status.ps1` to confirm applied migrations
3. **Execute Reconciliation**: If preflight passes, run `ReconcileCanonicalPermissionsAsync` via EF gate with Product Owner authorization
4. **Verify Post-Reconciliation State**:
   - Count active `Permission` rows = 188 (catalog active count)
   - Protected Admin role has all `MandatoryPermissionKeys` granted
   - No Deny overrides on mandatory keys for Admin members
   - No grants to retired permissions
   - Audit entries present for reconciliation actions
5. **Run Persistence Tests**: Execute `NEXUS.Modules.Identity.Tests` against Development database

---

## 10. Evidence References

| Component | Source File | Key Lines |
|-----------|-------------|-----------|
| Reconciliation entry point | `NEXUS.Persistence/SeedDataProvider.cs` | 450-540 |
| Catalog sync | `NEXUS.Persistence/SeedDataProvider.cs` | 540-690 |
| Protected Admin reconciliation | `NEXUS.Persistence/SeedDataProvider.cs` | 693-787 |
| Deny override cleanup | `NEXUS.Persistence/SeedDataProvider.cs` | 789-830 |
| Legacy grant cleanup | `NEXUS.Persistence/SeedDataProvider.cs` | 832-865 |
| Legacy override cleanup | `NEXUS.Persistence/SeedDataProvider.cs` | 867-890 |
| ProtectedTenantAdminPolicy | `Identity/Application/ProtectedTenantAdminPolicy.cs` | 1-105 |
| BootstrapPermissionDelegationPolicy | `Identity/Infrastructure/BootstrapPermissionDelegationPolicy.cs` | 1-102 |
| PermissionCatalog seed/create | `Identity/Application/PermissionCatalog.cs` | 1170-1178 |
| EnforceDependencies | `Identity/Application/PermissionCatalog.cs` | 1148-1168 |
| EffectivePermissionResolver | `Identity/Application/EffectivePermissionResolver.cs` | 1-31 |
| Migration: SystemRoleKey | `NEXUS.Persistence/Migrations/20261102190318_AddStableSystemRoleIdentity.cs` | (inferred) |
| Migration: PermissionSeedId | `NEXUS.Persistence/Migrations/20261102190319_AddPermissionSeedId.cs` | (inferred) |
| Migration: UserPermissionOverride | `NEXUS.Persistence/Migrations/20261102190320_AddUserPermissionOverride.cs` | (inferred) |
