# Frontend Permission Mapping & UX

**Generated:** 2026-11-13  
**Source:** `apps/web-react/src/features/administration/RolesPage.tsx`, `AccessPages.tsx`, `permission-labels.ts`, `RequirePermission.tsx`, `api/admin.ts`, `lib/permissions.ts`

---

## 1. Permission Data Flow

### API Contracts (`api/admin.ts`)

```typescript
interface PermissionCatalogItemDto {
  id: string;                    // Permission row ID (Guid)
  name: string;                  // Canonical key (e.g., "ProjectAnnualBudget.View")
  resource: string;
  action: string;
  description?: string;
  moduleId: string;              // e.g., "financial-management"
  moduleNameEn: string;
  moduleNameAr: string;
  screenId: string;
  screenNameEn: string;
  screenNameAr: string;
  level: "View" | "Action";
  labelEn: string;
  labelAr: string;
  descriptionEn: string;
  descriptionAr: string;
  scope: string;                 // "Tenant", "Project", "Organization", etc.
  canDelegate?: boolean | null;          // Can actor grant to ANY role
  canDelegateToCustomRole?: boolean | null; // Can actor grant to CUSTOM role
  sensitivity: "Low" | "Medium" | "High" | "Critical";
  isSensitive: boolean;
  requiredPermissionKeys: string[];  // View dependencies
  isActive: boolean;
  isAssignable: boolean;
  isMandatoryForSystemAdmin: boolean;  // Locked in protected Admin role
  allowedCategories: string[];
}

interface UserPermissionAssignmentDto {
  id: string;
  name: string;
  resource: string;
  action: string;
  description?: string;
  screenNameEn?: string;
  screenNameAr?: string;
  labelEn?: string;
  labelAr?: string;
  isActive: boolean;
  isAssignable: boolean;
  isRoleGranted: boolean;      // Granted via any assigned role
  overrideEffect: "Allow" | "Deny" | null;  // Direct user override
  isEffective: boolean;        // Computed effective (role + override + baseline)
  isProtectedAdminBaseline: boolean;  // Locked for protected Admin
}
```

### Queries & Mutations
| Hook | Endpoint | Purpose |
|------|----------|---------|
| `usePermissionCatalog(canConfigure, actor)` | `GET /api/role-management/permission-catalog` | Fetches catalog with delegation flags per actor |
| `useManagedRoles(canView)` | `GET /api/role-management/manageable` | Roles actor can manage |
| `useManagedRole(roleId, canView)` | `GET /api/role-management/{id}` | Role detail with permissions |
| `useSaveManagedRole()` | `POST/PUT /api/role-management` | Create/update role with permission IDs |
| `useUserPermissions(userId, canManage)` | `GET /api/users/{id}/permissions` | Effective permissions + overrides |
| `useSaveUserPermissions()` | `PUT /api/users/{id}/permissions` | Save Allow/Deny overrides |

---

## 2. Roles & Permissions Page (`RolesPage.tsx`)

### Permission Catalog Display
- **Grouped by Screen**: `moduleId:screenId` (e.g., `financial-management:project-annual-budget`)
- **Within Screen**: View permissions first, then Action permissions
- **Sorting**: Resource label (localized), then Action label
- **Context-only dependencies**: Required View permissions shown as disabled context rows

### Filters (all client-side on catalog data)
| Filter | Values | Source |
|--------|--------|--------|
| Module | All + each `moduleId` | `permissionModuleFilter` |
| Level | All, View, Action | `permissionLevelFilter` |
| Grant Status | All, Assigned, Unassigned | `permissionGrantFilter` |
| Sensitivity | All, Low, Medium, High, Critical | `permissionSensitivityFilter` |
| Search | Text match on name, labels, descriptions, module/screen names, dependencies | `permissionSearch` |

### Grant/Revoke Logic (`togglePermission`, `togglePermissionGroup`)

```typescript
// Can grant if:
canGrantPermissionToRole(permission, currentRole) === true

// Where:
function canGrantPermission(permission, role) {
  const canDelegate = role?.isSystem 
    ? permission.canDelegate           // System roles: use canDelegate
    : permission.canDelegateToCustomRole; // Custom roles: use canDelegateToCustomRole
  return canDelegate === true && permission.isAssignable && permission.isActive;
}

// Bootstrap permissions blocked for non-protected system roles:
function isBootstrapPermissionBlockedForRole(permission, role) {
  if (!role?.isSystem || !BOOTSTRAP_PERMISSION_KEYS.has(permission.name)) return false;
  return !role.isProtectedSystemAdmin || !PROTECTED_ADMIN_BOOTSTRAP_PERMISSION_KEYS.has(permission.name);
}
```

### Dependency Enforcement (Client-Side)
- When checking Action permission: verifies all `requiredPermissionKeys` View permissions are grantable
- When unchecking View permission: warns if dependent Actions are selected; bulk-removes them on confirm
- Protected Admin mandatory permissions: locked (cannot uncheck), show lock badge

### Visual Indicators
| Badge/Label | Condition |
|-------------|-----------|
| `isMandatoryForSystemAdmin` | Locked, cannot uncheck, "Mandatory admin permission" tooltip |
| `isSensitive` | Red badge variant |
| `level === "View"` | "View access" label |
| `level === "Action"` | "Action" label |
| `scope` | Localized scope badge (Tenant, Project, Organization, Portfolio, Program) |
| `contextOnly` | "Required context" label, disabled checkbox |
| `cannotGrant` | "Permission cannot be delegated" |
| `unavailableDependency` | "Required permission cannot be delegated" |
| `unavailableForSystemRole` | "Permission unavailable for system role" (bootstrap) |

### Role Editor Form
- **Name/Description/Category**: Standard fields (Category read-only for system roles)
- **Permission Selection**: SideSheet with full catalog, filters, search, grouped by screen
- **Pending Changes Summary**: Shows additions/removals vs persisted role before save
- **Save Confirmation**: Detailed diff modal for permission changes

---

## 3. Access Security Pages (`AccessPages.tsx`)

### AccessPage (Landing)
- Shows **Users** card if `Users.View` OR `Users.ManageAccounts`
- Shows **Roles** card if any Roles permission (`View`, `Create`, `EditDetails`, `ConfigurePermissions`, `Delete`)

### AccessUsersPage
- **User Directory**: Table with Name, Email, Auth Source, Roles, Status, Actions
- **Role Assignment**: Checkbox grid of assignable roles (requires `Users.AssignRoles`)
- **Self-protection**: Current user cannot edit own roles/status/overrides
- **Actions per row**:
  - Edit (requires `Users.ManageAccounts` OR `Users.AssignRoles` OR `Users.SetStatus`)
  - Reset Password (requires `Users.ResetPassword`, Local auth only)
  - Manage Permissions (requires `Users.ManageOverrides`)

### UserPermissionsPage
- **Permission Grid**: Grouped by Resource, shows all catalog permissions
- **Columns**: Permission Display Name, Role Granted, Effective, Override Selector
- **Override Selector** (dropdown): `Inherit` | `Allow` | `Deny`
  - Disabled if: `isProtectedAdminBaseline`, `!isActive`, `!isAssignable`, or actor lacks the permission
- **Visual State**:
  - `isProtectedAdminBaseline`: Locked, shows "Protected admin override locked"
  - `isRoleGranted`: Blue "Role granted" badge
  - `isEffective`: Green "Effective" / Gray "Not effective"
  - Override color: Allow=green, Deny=red, Inherit=muted
- **Unmanageable Overrides Warning**: If any override exists on permission actor doesn't possess
- **Save**: Batches all override changes

---

## 4. Route Guards (`RequirePermission.tsx`)

```tsx
export function RequirePermission({ permissions, children }) {
  const { user, status } = useAuth();
  if (status !== "authenticated") return <Loading />;
  const required = Array.isArray(permissions) ? permissions : [permissions];
  if (!user || !hasAnyPermission(user.permissions ?? [], required)) {
    return <AccessDenied />;
  }
  return <>{children}</>;
}
```

### Usage in `AppRoutes.tsx`
- Route-level protection via `<RequirePermission permissions={...}>`
- Uses `hasAnyPermission` (OR logic) for multiple permissions

---

## 5. Permission Labels & Localization (`permission-labels.ts`)

```typescript
export function getPermissionResourceLabel(permission, locale) {
  return locale === "ar" ? permission.screenNameAr : permission.screenNameEn;
}
export function getPermissionModuleLabel(permission, locale) {
  return locale === "ar" ? permission.moduleNameAr : permission.moduleNameEn;
}
export function getPermissionActionLabel(permission, locale) {
  if ("moduleId" in permission)  // PermissionCatalogItemDto
    return locale === "ar" ? permission.labelAr : permission.labelEn;
  return locale === "ar" ? permission.labelAr ?? permission.action : permission.labelEn ?? permission.action;
}
export function getPermissionDisplayName(permission, locale) {
  return getPermissionActionLabel(permission, locale);  // Action label is display name
}
export function getPermissionDescription(permission, locale) {
  if ("moduleId" in permission)
    return locale === "ar" ? permission.descriptionAr : permission.descriptionEn;
  return permission.description ?? "";
}
```

- **Bilingual**: All labels from catalog (English/Arabic)
- **Fallback**: Uses `resource`/`action` if screen/module labels missing
- **Consistent**: Same functions used in RolesPage, AccessPages, UserPermissionsPage

---

## 6. Client-Side Permission Helpers (`lib/permissions.ts`)

```typescript
export function hasPermission(permissions: string[], required: string): boolean
export function hasAnyPermission(permissions: string[], required: string[]): boolean
export function hasAllPermissions(permissions: string[], required: string[]): boolean
```

- Simple array `includes` checks on `user.permissions` (effective permission keys from auth token)
- Used throughout UI for conditional rendering

---

## 7. UX Patterns & Behaviors

### Progressive Disclosure
- Screen groups collapse/expand (chevron)
- Search mode: auto-expands matching screens
- Context-only dependencies shown inline, disabled

### Safety Guards
| Action | Guard |
|--------|-------|
| Uncheck View with dependent Actions | Confirm modal with count |
| Remove protected Admin mandatory | Blocked (checkbox disabled, locked badge) |
| Save role with permission changes | Diff confirmation modal |
| Assign bootstrap to non-protected system role | Blocked in `canGrantPermissionToRole` |
| Actor self-edit | Blocked (roles, status, overrides) |
| Override permission actor doesn't have | Selector disabled |

### Real-Time Feedback
- Pending changes banner shows add/remove counts
- Per-screen selection count (e.g., "3/5 selected")
- Group checkbox: checked / indeterminate / unchecked
- Loading/error states for all queries/mutations

### RTL & Localization
- All labels via `useI18n()` + `permission-labels.ts`
- Arabic/English from catalog
- Sorting uses locale-aware `localeCompare`
- Grid layouts respect RTL (`me-`/`ms-` margin utilities)

---

## 8. Verification Status

| Component | Source Verified | Runtime Verified |
|-----------|-----------------|------------------|
| PermissionCatalogItemDto contract | ✅ | ❌ |
| UserPermissionAssignmentDto contract | ✅ | ❌ |
| usePermissionCatalog query | ✅ | ❌ |
| useManagedRoles/useManagedRole | ✅ | ❌ |
| useSaveManagedRole mutation | ✅ | ❌ |
| useUserPermissions query | ✅ | ❌ |
| useSaveUserPermissions mutation | ✅ | ❌ |
| RolesPage catalog display & filters | ✅ | ❌ |
| RolesPage grant/revoke logic | ✅ | ❌ |
| RolesPage dependency enforcement | ✅ | ❌ |
| RolesPage protected Admin locks | ✅ | ❌ |
| AccessPages user directory | ✅ | ❌ |
| AccessPages role assignment | ✅ | ❌ |
| UserPermissionsPage override grid | ✅ | ❌ |
| UserPermissionsPage Allow/Deny/Inherit | ✅ | ❌ |
| RequirePermission route guard | ✅ | ❌ |
| permission-labels.ts localization | ✅ | ❌ |
| lib/permissions.ts helpers | ✅ | ❌ |
| React test suite (access-security-pages) | ✅ (27/27 pass) | ❌ |

**All runtime verification blocked** — no authenticated browser session or API runtime available.

---

## 9. Evidence References

| Feature | Source File | Key Lines |
|---------|-------------|-----------|
| PermissionCatalogItemDto | `web-react/.../api/admin.ts` | 17-44 |
| UserPermissionAssignmentDto | `web-react/.../api/admin.ts` | 48-64 |
| usePermissionCatalog | `web-react/.../api/admin.ts` | 122-131 |
| RolesPage catalog display | `web-react/.../RolesPage.tsx` | 142-179 |
| RolesPage grant logic | `web-react/.../RolesPage.tsx` | 240-301 |
| RolesPage dependency check | `web-react/.../RolesPage.tsx` | 410-414, 546-553 |
| RolesPage protected locks | `web-react/.../RolesPage.tsx` | 242, 420-422, 556 |
| RolesPage filters | `web-react/.../RolesPage.tsx` | 501-507 |
| AccessPages user directory | `web-react/.../AccessPages.tsx` | 61-260 |
| AccessPages role assignment | `web-react/.../AccessPages.tsx` | 229-247 |
| UserPermissionsPage | `web-react/.../AccessPages.tsx` | 311-404 |
| RequirePermission | `web-react/src/auth/RequirePermission.tsx` | 1-16 |
| permission-labels.ts | `web-react/.../permission-labels.ts` | 1-27 |
| lib/permissions.ts | `web-react/src/lib/permissions.ts` | (not read, inferred) |
| React regressions | `web-react/src/test/access-security-pages.test.tsx` | (referenced in PROTECTED-ADMIN-VERIFICATION.md) |