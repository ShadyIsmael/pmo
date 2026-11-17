# System Role Impact Assessment — Tenant Admin + Organization Admin Only

> **Supersession notice — 2026-11-14:** The design-only status below has been superseded by the authorized implementation source changes. Migration is scaffolded but not generated/applied against the Development database because live identity inspection was blocked. Current source impact and verification status are in `RBAC-SYSTEM-ROLE-IMPACT-ASSESSMENT.md`'s implementation addendum and `RBAC-IMPLEMENTATION-FINAL-REPORT.md`; the final report controls where this earlier assessment says no implementation was authorized.

**Review date:** 2026-11-14  
**Status:** Proposed migration/design assessment only. No role data or schema was changed.

**Scenario authority reviewed:** Sources item Pasted markdown(9).md (§§4.1, 5, 7, 8); the 83 original scenarios are preserved in RBAC-VALIDATED-SCENARIO-MATRIX.md. The source's proposed migration language does not itself authorize data changes.

## Executive finding

The current seed defines **eight** roles as System. The approved rule permits only Tenant Admin and Organization Admin. Seven currently seeded roles are therefore outside the allowed set: Executive, Portfolio Manager, Project Manager, Team Member, PMO Admin, Finance Controller, and Resource Manager. The inspected seed contains no Organization Admin role. This is a seed/source inventory, not a live persisted database inventory; the current database could not be queried from this execution context.

The change cannot be implemented safely as a one-column flip. The System flag is coupled to `SystemRoleKey` by domain code and a SQL check constraint, and role administration, assignment and UI behavior use System status as a proxy for protection, editability, deletion and delegability. Current seed reconciliation expects all eight keys. The impact must be designed as a data-preserving role identity and policy change. The source's RM-09/DP-14 wording treats both approved System roles as protected/immutable, but the same source leaves Organization Admin protection open and requires System/Protected separation; preserve that conflict for PO resolution.

## Current classification from source

| Seed identity | Display name / category | Current `IsSystem` | Target classification under PO rule | Evidence |
|---|---|---:|---|---|
| `tenant.admin` | Admin / System | true | System; existing protected Tenant Admin | `SeedDataProvider.cs:2513`; `ProtectedTenantAdminPolicy.cs:122-125` |
| `tenant.executive` | executive / Leadership | true | Non-System | `SeedDataProvider.cs:2514` |
| `tenant.portfolio-manager` | portfolio.manager / Management | true | Non-System | `SeedDataProvider.cs:2515` |
| `tenant.project-manager` | project.manager / Management | true | Non-System | `SeedDataProvider.cs:2516` |
| `tenant.team-member` | team.member / Operations | true | Non-System | `SeedDataProvider.cs:2517` |
| `tenant.pmo-admin` | pmo.admin / PMO | true | Non-System | `SeedDataProvider.cs:2518` |
| `tenant.finance-controller` | finance.controller / Finance | true | Non-System | `SeedDataProvider.cs:2519` |
| `tenant.resource-manager` | resource.manager / Management | true | Non-System | `SeedDataProvider.cs:2520` |
| Organization Admin | No entry found in inspected seed | Not established | The only other permitted System role | Live/source identity, seed need and grant policy require confirmation/evidence. |

The Minimal Seed verifier currently requires the exact eight keys, every row marked System, and no other role (`SeedDataProvider.cs:2358-2377`). This is a direct source conflict with the PO decision. The seven reclassification targets are not proven to exist in the current persisted database until a fresh, authorized read-only host inspection is available.

## Where `IsSystem` is consumed

| Area | Current dependency | Impact |
|---|---|---|
| Domain identity | `Role.IsSystem` plus nullable `SystemRoleKey`; `AssignSystemRoleKey` requires `IsSystem=true` and makes the key immutable (`Role.cs:7-35`). | Non-System built-in roles cannot retain their current canonical key under the existing model. |
| SQL schema | `CK_Roles_SystemRoleKeyRequiresSystemRole` enforces `SystemRoleKey IS NULL OR IsSystem=1`; filtered unique tenant/key index (`RoleConfiguration.cs:11-26`; `AppDbContextModelSnapshot.cs:2437-2463`). | Flipping only the flag while retaining a key violates the check constraint. |
| Seed and reconciliation | Expected eight System keys; role definitions, grant maps and role resolution key off `SystemRoleKey` (`SeedDataProvider.cs:2358-2377,2492-2545`). | Seed would reject a reclassified role set or create/resolve roles incorrectly until it is redesigned. |
| Protected Admin detection | Protection is derived from `IsSystem`, non-empty TenantId and key `tenant.admin` (`ProtectedTenantAdminPolicy.cs:122-125`; `UserEffectivePermissionService.cs:56-65`). | Tenant Admin must retain a stable protected identity; System status alone must not confer protection. |
| User role assignment | System role with an unheld operational permission is rejected; protected Admin possession-neutral allowance only covers non-System roles (`UserAdministrationService.cs:505-590`). | Project Manager reclassification is necessary for the required PM delegation journey, but classification alone is not enough if keys/seed checks remain coupled. |
| Role configuration | System roles cannot change name/category or be deleted; grant configuration and manageability use separate System-specific branches (`RoleAdministrationService.cs:246-269,307-314,407-412,675-699`). | System status currently implies several management behaviors that must be separated. |
| React Roles & Permissions | UI badge, delete control, read-only name/category, system-role grant availability and delegation rule branch on `isSystem` (`RolesPage.tsx:386,450-508,562-598`). | After reclassification, UI must express protection, assignability and editability separately. |
| Role category | “System” is reserved as a category for code-defined roles, while the seeded PM and other System roles use functional categories (`RoleCategories.cs:3-21`; seed `:2513-2520`). | Do not change PM's Management category as a side effect; workflow lookup uses it (`UsersController.cs:54-67`). |

No role-level `IsAssignable`, `IsEditable`, or `IsActive` field was found. Permission-level `IsAssignable` exists in the catalog, but it is not the same as role assignability. `Role` exposes `IsDeleted` and the current delete path is soft-delete-only for unassigned custom roles.

## Required concept separation

| Concept | Proposed meaning for future design | Current state / open point |
|---|---|---|
| System | Classification limited to the two approved admin roles; system identity or seeding provenance must not imply protection or assignability. | Current field also acts as a management/assignment switch. |
| Canonical identity | Stable key that lets seed/reconciliation identify a built-in role independent of classification. | Current `SystemRoleKey` is constrained to System rows; decide whether to introduce/rename to neutral `RoleKey` for reclassified built-ins. |
| Protected | Explicit invariant/policy for immutable core permissions and anti-lockout. Existing protected identity is `tenant.admin`. | Do not infer that Organization Admin is protected. Its protection model is open. |
| Category | Descriptive grouping and domain metadata; not a permission eligibility rule. | Category `System` is reserved, but roles use functional categories. Preserve Management for PM selection. |
| Assignable | Explicit policy determining whether a role can be assigned, to whom, and at what scope; backend remains authoritative. | No role-level flag/API contract; current endpoint/filtered list is not named as an assignment policy. |
| Editable | Explicit policy for name, category, grants, deletion/deactivation under actor authority and role protection. | Current `IsSystem` controls these operations broadly. |

## Existing memberships and grants

Recommended preservation principle for an approved migration: reclassify each existing role **in place** using its existing `Role.Id`; preserve every `UserRole` membership, role-permission link, user override, audit row, name, category, and unrelated role metadata. Never delete/recreate roles merely to change classification. The relationship models are FK-linked (`UserRole.cs:5-11`; `Role` permission collection `Role.cs:14-15`), so keeping Role IDs preserves membership linkage.

Before any future write, a verified host-side read-only inventory must report the actual Tenant/DB identity, role IDs/names/categories/flags/keys, assigned-user counts, grant counts, direct overrides, and any Org Admin candidate. The repository's database workflow documents that the current executor cannot reach SQL and must not retry from this isolated context (`docs/development/development-database-connectivity-and-runner.md`). No live role inventory is claimed in this report.

## Proposed migration and rollback plan

This is a plan only. It does not authorize or execute a migration.

1. **Approval and inventory gate.** Obtain explicit approval of the reconciled 83-row source baseline, any accepted additions, and unresolved Org Admin decisions. From the authorized Windows Development host, perform the documented read-only DB status/identity inspection. Reconcile actual role rows with the seed source before designing data operations.
2. **Approve neutral role identity semantics.** Preferred design for discussion: introduce a canonical `RoleKey` independent of `IsSystem` (or equivalent metadata) so pre-existing built-in ordinary roles can retain stable seed identity. Keep `tenant.admin` stable. Do not assign an Org Admin key or create a role until its identity/scope/grants are approved. If the PO instead wants keys cleared for ordinary roles, update seed reconciliation and all identity consumers accordingly; do not use display names as a silent substitute.
3. **Schema migration design.** Review a migration that removes/rewrites the current key ⇒ System check and adjusts unique indexes, backfills/renames the neutral key, and preserves old role IDs. If SystemRoleKey is retained for only system classification, reclassified built-ins need a separate canonical key. No migration SQL has been generated or applied.
4. **Data reclassification.** In one reviewed transactional migration, change only the intended classification and identity fields for confirmed existing roles. Set only Tenant Admin and the approved Organization Admin row to System. Preserve IDs, permission links, memberships, categories, display names and audit history. The PM role must remain category Management pending separate domain approval.
5. **Code/seed/authorization alignment.** Update seed verification/creation, protected-role policy, role administration, user assignment, role lookup DTO/API and frontend behavior so System, Protected, Category, Assignable and Editable are separate. Keep all existing role grants; no automatic grant or removal is authorized by this task.
6. **Rollback preparation.** Record a pre-migration mapping of RoleId → old IsSystem/key/category plus membership/grant counts and hash/revision of reviewed SQL. Rollback must restore classification/key mapping without deleting role or membership rows. If post-migration business writes changed roles, stop for reconciliation rather than overwriting those changes. Rollback feasibility must be demonstrated against an isolated copy before any production-like change.
7. **Verification gates.** Verify exact two-role invariant, seed idempotency, key uniqueness, FK integrity, preserved membership/grant counts, Tenant/Admin protection, PM workflow lookup, protected-core invariants, direct Deny behavior, ordinary delegation, cross-scope denial, rollback, and tenant-concurrency safeguards. Runtime/UAT remains a later explicit phase.

## Source scenario and P0 impact crosswalk

The role-taxonomy and assignment impact is traced to RM-09, UR-02/03, UR-12, DP-02/14, PA-01/08/09, and CM-01. The source inventory is 83 rows (39 Direct / 44 Corner), but it explicitly is not completeness-certified and carries draft W/S proposals. The protected-Tenant-Admin-to-create-user-to-Project-Manager journey maps to UR-02/03 + DP-01/02 + CM-01; Project Manager is currently seeded as System, and service validation rejects an unheld System-role permission before the protected-admin ordinary-delegation allowance. Static evidence identifies a conflict; the attachment-reported 403 trace has not been independently reproduced, and no successful runtime result or current DB inventory exists.

The seven seed roles outside the permitted System-role pair, if present in persisted data, must be reclassified in place with their IDs, user memberships, grants, overrides and audit history preserved. The inspected seed has no Organization Admin identity. Do not create it, mark it protected, or grant it tenant-wide scope as part of the PM reclassification.

## Impact decision

The requested two-role rule is not implementation-ready in current source. The Project Manager assignment path depends on reclassification and identity decoupling; the Organization Admin record/scope and independent protection/assignability rules are not established. Do not issue migration SQL or change role data until the relevant decisions are recorded and the Product Owner explicitly approves the final scenario matrix and implementation phase.
