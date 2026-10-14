# Permission Model

NEXUS PMO implements a robust, multi-layered authorization model combining Role-Based Access Control (RBAC) with context-based roles and data-level scoping.

---

## RBAC Foundation

The authorization model is built on four core entities:

```
┌──────────┐     ┌──────────┐     ┌──────────────┐     ┌──────────────┐
│   User   │────▶│ UserRole │────▶│    Role      │────▶│  Permission   │
└──────────┘     └──────────┘     └──────────────┘     └──────────────┘
                                       │                     │
                                       │ Name, Category      │ Resource, Action
                                       │ IsSystem            │
```

- **User** -- Person using the system
- **Role** -- Named collection of permissions (e.g., "Project Manager", "Executive")
- **Permission** -- Granular `Resource.Action` pair (e.g., "projects.read", "finances.write")
- **UserRole** -- Many-to-many assignment linking users to roles

---

## Permission Categories

Permissions are organized by resource domain. The full set of permission categories:

| Category | Example Permissions |
|----------|-------------------|
| `Project` | View, Create, Edit, Delete, Approve, Close, ViewFinancials, EditFinancials |
| `Portfolio` | View, Create, Edit, Delete, Manage |
| `Program` | View, Create, Edit, Delete, Manage |
| `Task` | View, Create, Edit, Delete, Assign, Complete, Reassign |
| `Resource` | View, Allocate, Deallocate, ManageRates, ViewCapacity |
| `Risk` | View, Create, Edit, Delete, Mitigate, Escalate |
| `Issue` | View, Create, Edit, Delete, Resolve, Escalate |
| `Meeting` | View, Create, Edit, Delete, ManageActions |
| `Workflow` | View, Create, Edit, Delete, Execute |
| `Report` | View, Create, Schedule, Export |
| `Document` | View, Upload, Edit, Delete, Share |
| `Notification` | View, Manage |
| `Costs` | View, Manage, Approve |
| `Integration` | View, Configure, Manage |
| `Administration` | View, ManageUsers, ManageRoles, ManageSettings, ManagePlugins |
| `SecurityAndAudit` | View, ViewAuditLog, ManagePolicies |
| `AICopilot` | Access, GeneratePlan, Analyze |
| `Ticket` | View, Create, Edit, Resolve |
| `TestManagement` | View, Create, Edit, Execute |

---

## Permission Granularity

Permissions follow the pattern `{Resource}.{Action}` providing fine-grained control:

- **View permissions** control read access to data
- **Create/Edit/Delete** control CRUD operations
- **Specialized actions** like `Approve`, `Allocate`, `Mitigate`, `ManageRoles` control specific business operations
- **Financial permissions** (`ViewFinancials`, `EditFinancials`, `Costs.Approve`) are isolated from other project data for compliance

---

## Data Scope Authorization

In addition to permission checks, data access is scoped by:

1. **Tenant Scope** -- Global query filters ensure users only see their tenant's data
2. **Context Scope** -- Users may have different roles in different contexts:
   - **Project-level**: User is "Project Manager" on Project A but "Team Member" on Project B
   - **Portfolio-level**: User is "Portfolio Manager" for Portfolio X
3. **Organization Scope** -- Data visibility limited to user's organization hierarchy

---

## Context-Based Roles

Users can hold multiple roles simultaneously and switch between contexts:

```
User: Emily Project
├── Global Role: "Project Manager" (assigned via UserRole)
├── Project A Role: "Project Manager"
├── Project B Role: "Team Member"
└── Portfolio Y Role: "Program Manager"
```

The `switch-role` endpoint allows users to change their active role context, which affects which permissions are evaluated.

---

## Authorization Evaluation Order

When a user makes a request, authorization is evaluated in this order:

```
1. Authentication Check ──── Is JWT valid? Is user active?
        │
2. Tenant Isolation ──────── Does user belong to this tenant?
        │
3. Permission Check ──────── Does user's active role have required permission?
        │
4. Data Scope Check ──────── Does user have access to this specific entity?
        │
5. Context Validation ────── Is the context (project, portfolio) valid for this role?
        │
            ALLOW or DENY
```

---

## Permission Constants Reference

```typescript
// apps/web/src/lib/permissions.ts

export const Permissions = {
  Project: {
    View: "Project.View",
    Create: "Project.Create",
    Edit: "Project.Edit",
    Delete: "Project.Delete",
    Approve: "Project.Approve",
    Close: "Project.Close",
    ViewFinancials: "Project.ViewFinancials",
    EditFinancials: "Project.EditFinancials",
  },
  Portfolio: {
    View: "Portfolio.View",
    Create: "Portfolio.Create",
    Edit: "Portfolio.Edit",
    Delete: "Portfolio.Delete",
    Manage: "Portfolio.Manage",
  },
  Resource: {
    View: "Resource.View",
    Allocate: "Resource.Allocate",
    Deallocate: "Resource.Deallocate",
    ManageRates: "Resource.ManageRates",
    ViewCapacity: "Resource.ViewCapacity",
  },
  Costs: {
    View: "Costs.View",
    Manage: "Costs.Manage",
    Approve: "Costs.Approve",
  },
  Administration: {
    View: "Administration.View",
    ManageUsers: "Administration.ManageUsers",
    ManageRoles: "Administration.ManageRoles",
    ManageSettings: "Administration.ManageSettings",
    ManagePlugins: "Administration.ManagePlugins",
  },
  // ... see full file for all categories
};
```

---

## Default Role-Permission Mappings

| Role | Permissions Granted |
|------|--------------------|
| **Executive** | All except `Administration.*`, `SecurityAndAudit.ManagePolicies` |
| **Portfolio Manager** | `Portfolio.*`, `Program.*`, `Project.View/Edit`, `Report.*` |
| **Project Manager** | `Project.*`, `Task.*`, `Resource.View/Allocate`, `Report.View` |
| **Team Member** | `Task.View/Edit (own)`, `Meeting.View`, `Notification.View` |
| **PMO Administrator** | All permissions |
| **Finance Controller** | `Costs.*`, `Project.ViewFinancials`, `Report.View/Export`, `Invoice.View` |
| **Resource Manager** | `Resource.*`, `Project.View`, `Report.View` |
| **System Administrator** | All permissions (full system access) |

## Role Administration and Effective Permissions

The Identity module exposes role administration through the existing `users.write` capability. Role-management reads and writes are tenant-scoped from the authenticated tenant claim; callers do not supply a tenant ID in the request body. The server only returns permission entries the acting user currently has and rejects a role permission set that exceeds the actor's own effective permissions.

- Effective access is the union of permissions from the user's assigned roles, then per-user overrides are applied: `Allow` adds a permission and `Deny` removes it. `Inherit` is represented by having no override row.
- Role permission changes and user-role assignments are audited. An actor cannot change their own role assignments or remove their final effective `users.write` grant through a role update.
- System roles may have their permission sets adjusted by an authorized actor, but cannot be renamed or deleted.
- A custom role cannot be deleted while any non-deleted user assignment points to it. This avoids silently dropping user access assignments.
- Role and permission records are selected with explicit tenant checks in addition to EF query filters, including lookup paths that intentionally bypass ambient filters.

---

## Multi-Tenant Permission Isolation

- Each tenant has its own set of roles and permissions
- Global query filters on `TenantId` prevent cross-tenant data access at the database level
- Tenant-specific role mappings ensure one tenant's administrators cannot access another tenant's data
- The `GetCurrentTenantId()` method on `AppDbContext` automatically scopes all queries
