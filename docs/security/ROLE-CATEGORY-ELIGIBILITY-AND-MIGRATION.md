# Role Category Eligibility and Migration Notes

**Historical status (2026-11-05):** Source eligibility implementation and static seed inventory. No Development database inventory or data migration was performed.

**Current Product Owner decision (2026-11-09):** Role Category-based eligibility restrictions for ordinary permission grants are temporarily suspended. Role Category remains descriptive metadata; the API compatibility field AllowedCategories lists every selectable custom-role category. Existing role grants are preserved, and no category/grant migration or database write is part of this change. Do not reintroduce grant-category restrictions without explicit Product Owner approval.
**Historical runtime correction, 2026-11-08:** A read-only Development check verified all 11 canonical Project Annual Budget/Closing catalog entries and their stored IDs/names. Applicable Management category eligibility was already represented by `AllowedCategories`; the non-selectable checkboxes were caused by missing first-grant `CanDelegate` entries. The exact Annual Budget/Closing bootstrap key set is now exposed to authorized Tenant role administrators, with a protected tenant Admin recipient exception and all existing category, dependency, Deny, scope, baseline, and last-admin checks preserved. No catalog sync, grant write, migration, or seed occurred. See the [P0 recovery evidence](../finance/P1-PROJECT-ANNUAL-BUDGET-WORKFLOW-RECOVERY.md).

**Historical source addition, 2026-11-09:** `ProjectAnnualBudget.Delete` and `ProjectAnnualBudget.Withdraw` were registered as distinct Project-scoped action permissions. The source addition described Management eligibility at that time; the current contract below supersedes that category restriction. Their Development persistence and runtime access were not verified by the earlier 11-key inventory; no permission grants or database writes were made for those additions.

## Current contract (2026-11-09)

Custom roles retain the selectable descriptive categories Tenant, Organization, Management, Operation, and Business. System is reserved for protected system roles, and system-role categories remain protected through normal role administration.

Category no longer restricts ordinary custom-role permission grants. An administrator authorized through `Roles.ConfigurePermissions` may grant or revoke any active, assignable, scoped non-Administration permission to a same-Tenant user-managed custom role regardless of recipient category or whether the actor holds that permission. This supersedes the earlier actor-possession ceiling for ordinary permissions. Administration-module permissions remain actor-authorized only. Tenant/role boundaries, dependencies, resource scope, Active Deny, platform/global boundaries, protected/system-managed role rules, and no-self-escalation remain enforced.

The historical Project Plan category map is not active. `Planning.View`, `Planning.Edit`, and `ProjectPlan.Create` follow the same category-neutral and actor-possession-neutral rule when granted to a user-managed custom role. For a role assigned to the actor, a change cannot add an effective permission beyond the permissions currently effective for the actor. The separate protected Admin and Administration-module rules still apply.

Portfolio, Program, and Project manager eligibility may continue to require an active Management-category role. Annual Budget approval candidate eligibility may continue to require a System or Management category with effective ProjectAnnualBudget.Approve. These are independent domain eligibility rules, not permission grant filters, and remain unchanged.
## Historical source-only seeded-role inventory (2026-11-05)

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

## Deferred migration notes (inactive during category suspension)
The numbered approach below documents the earlier category migration proposal. It is deferred while category-based permission grant restrictions are suspended. Do not use the historical seed conflict table to remove or remap grants; no category/grant migration is required by this change.


1. Before changing persisted categories or grants, run the documented read-only Development inspection and export active role IDs, system-role keys, category values, role grants, assigned users, direct Allow/Deny overrides, and current manager assignments/history. Compare existing effective permissions and manager candidate sets with the proposed result.
2. Prepare a per-role, per-permission conflict manifest. A role owner must approve any category mapping and each grant decision. Do not infer a category from a role name, copy grants between roles, or remove grants as part of a routine seed.
3. For a custom role, explicitly remove conflicting grants through the role service while keeping its current category; change its category only after every retained grant is allowed. For protected system roles with legacy categories, any remapping requires a separate reviewed, tenant-scoped, audited application migration. Normal role administration cannot change a system-role category.
4. Preserve role IDs, system-role identities, user-role assignments, direct overrides, audit history, manager fields, and assignment-history rows. Any approved Development/UAT database write still requires the repository's exact environment and target identity, migration, and schema checks; a backup is optional and its absence is not a gate. Do not apply direct SQL updates. Production backup and recovery requirements remain unchanged.
5. Recheck effective permission sets, dependencies, Active Deny, tenant/resource scope, role membership, and the Portfolio/Program/Project manager candidate sets. Complete the related [Master UAT scenarios](../quality/MASTER-UAT-CHECKLIST.md) before closing runtime rollout.

No EF schema migration, database write, reseed, reset, direct SQL write, or browser UAT was performed for this source change.

## Verification

- Passed: focused React coverage for role categories, localized conflict removal, manager lookup, and Project creation: 3 test files, 46 tests.
- Passed: `npm run build` from `apps/web-react` (TypeScript build and Vite production bundle). Vite reports the existing Planned Payments static/dynamic import warning and large bundle chunks.
- Passed: `dotnet build apps/api/src/NEXUS.Modules.Identity/NEXUS.Modules.Identity.csproj --no-restore -m:1 -v minimal` (0 warnings, 0 errors).
- Not completed: API host build and focused API tests. Both reached prolonged IKVM class conversion warnings from the existing MPP dependency before C# compilation/test results were produced; the attempts were stopped without treating them as passes.
- Not run: authenticated Development API/UI UAT, persisted-role inventory, migration, or persistence tests. No explicit Development connection was configured for this task, and no database writes were made.

## Source references

- Category policy: `apps/api/src/NEXUS.Modules.Identity/Application/RoleCategories.cs`
- Role enforcement and conflict response: `apps/api/src/NEXUS.Modules.Identity/Infrastructure/RoleAdministrationService.cs`
- Manager-candidate policy: `docs/modules/MANAGER-ASSIGNMENT-SYSTEM-USERS.md`
- Permission and role delegation architecture: `docs/architecture/permission-model.md` and `docs/security/PERMISSIONS-ROLE-DELEGATION.md`
