# Role Category Eligibility and Migration Notes

**Status:** Source implementation and static seed inventory, 2026-11-05. No Development database inventory or data migration was performed.

## Contract

Custom roles use one controlled category: `Tenant`, `Organization`, `Management`, `Operation`, or `Business`. `System` is reserved for code-defined system roles and cannot be selected for a custom role. Existing system-role categories remain protected through normal role administration.

Each permission has a code-backed, explicit multi-category `AllowedCategories` list in `PermissionRoleCategoryPolicy`. The API returns this metadata with the permission catalog. A role's category limits which permissions can be newly assigned to it; the category itself does not grant runtime capability, tenant/organization/project scope, or workflow assignment. Existing actor delegation limits, permission dependencies, Active Deny, and scope checks remain authoritative.

Role creation validates category and every submitted permission on the server. For existing roles, unchanged legacy categories and their grants are preserved so an administrator can explicitly remove incompatible grants. A category change is rejected with the conflicting permission keys until the grants have been reviewed and removed; the API does not silently revoke them. Protected Tenant Admin remains in the reserved System category, and its mandatory Administration baseline is still non-removable.

Portfolio, Program, and Project manager eligibility is based on an active same-tenant user with an active assigned Management-category role, plus the existing entity-specific effective permissions and `Handover.Approve`. Lookups, create/assignment services, and incoming-manager acceptance recheck this rule. Resource profiles remain relevant only to Project Team membership and Resource Allocation.

## Source-only seeded-role inventory

The `SeedDataProvider` currently defines eight system roles. The protected Admin (`admin`, `System`) is excluded from category-conflict counts because its protected System policy is handled separately. The other seven roles contain 253 distinct role-to-permission grants in the seed source; 162 grants do not match the role's current category policy.

| Seed role | Current category | Distinct seeded grants | Grants outside `AllowedCategories` |
|---|---|---:|---:|
| `portfolio.manager` | Management | 34 | 0 |
| `project.manager` | Management | 42 | 0 |
| `resource.manager` | Management | 15 | 0 |
| `team.member` | Operations | 14 | 14 |
| `executive` | Leadership | 46 | 46 |
| `pmo.admin` | PMO | 69 | 69 |
| `finance.controller` | Finance | 33 | 33 |
| **Total, excluding protected Admin** |  | **253** | **162** |

The three manager roles have no source conflicts after `TimeTracking.ViewOwn` and `TimeTracking.ManageOwn` were explicitly allowed for both Operation and Management. The legacy `Operations`, `Leadership`, `PMO`, and `Finance` categories are outside the controlled list. The Admin baseline has no ordinary category-conflict count because its reserved System identity and mandatory grant policy are separate.

These figures describe checked-in seed definitions only. They do not establish active Development roles, current grant rows, assigned users, direct Allow/Deny overrides, or effective permissions. No explicit Development EF connection was configured for a read-only inventory during this task, so persisted conflict counts remain unknown.

## Safe migration approach

1. Before changing persisted categories or grants, run the documented read-only Development inspection and export active role IDs, system-role keys, category values, role grants, assigned users, direct Allow/Deny overrides, and current manager assignments/history. Compare existing effective permissions and manager candidate sets with the proposed result.
2. Prepare a per-role, per-permission conflict manifest. A role owner must approve any category mapping and each grant decision. Do not infer a category from a role name, copy grants between roles, or remove grants as part of a routine seed.
3. For a custom role, explicitly remove conflicting grants through the role service while keeping its current category; change its category only after every retained grant is allowed. For protected system roles with legacy categories, any remapping requires a separate reviewed, tenant-scoped, audited application migration. Normal role administration cannot change a system-role category.
4. Preserve role IDs, system-role identities, user-role assignments, direct overrides, audit history, manager fields, and assignment-history rows. Any approved database write still requires the repository's exact Development target and verified-backup gate. Do not apply direct SQL updates.
5. Recheck effective permission sets, dependencies, Active Deny, tenant/resource scope, role membership, and the Portfolio/Program/Project manager candidate sets. Complete the related [Master UAT scenarios](../quality/MASTER-UAT-CHECKLIST.md) before closing runtime rollout.

No EF schema migration, database write, reseed, reset, direct SQL write, or browser UAT was performed for this source change.

## Verification

- Passed: focused React coverage for role categories, localized conflict removal, manager lookup, and Project creation: 3 test files, 46 tests.
- Passed: `npm run build` from `apps/web-react` (TypeScript build and Vite production bundle). Vite reports the existing Planned Payments static/dynamic import warning and large bundle chunks.
- Passed: `dotnet build apps/api/src/NEXUS.Modules.Identity/NEXUS.Modules.Identity.csproj --no-restore -m:1 -v minimal` (0 warnings, 0 errors).
- Not completed: API host build and focused API tests. Both reached prolonged IKVM class conversion warnings from the existing MPP dependency before C# compilation/test results were produced; the attempts were stopped without treating them as passes.
- Not run: authenticated Development API/UI UAT, persisted-role inventory, migration, or persistence tests. No explicit Development connection was configured for this task, and no database writes were made.

## Source references

- Category policy: `apps/api/src/NEXUS.Modules.Identity/Application/RoleCategoryPolicy.cs`
- Role enforcement and conflict response: `apps/api/src/NEXUS.Modules.Identity/Infrastructure/RoleAdministrationService.cs`
- Manager-candidate policy: `docs/modules/MANAGER-ASSIGNMENT-SYSTEM-USERS.md`
- Permission and role delegation architecture: `docs/architecture/permission-model.md` and `docs/security/PERMISSIONS-ROLE-DELEGATION.md`
