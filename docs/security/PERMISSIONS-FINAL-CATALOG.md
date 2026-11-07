# Final Permission Catalog

**Source of truth:** `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`.

The catalog defines permission keys and bilingual module/screen labels, descriptions, scope, sensitivity, and dependencies. Effective access also requires tenant/resource scope and, for review actions, the exact workflow assignment.

## Current source counts

| Measure | Count |
|---|---:|
| Active catalog definitions | 133 |
| Active View keys | 35 |
| Active Action keys | 98 |
| Inactive compatibility definitions | 0 |
| Protected tenant Admin baseline | 48 |

These are code counts. They do not represent persisted Permission rows, role grants, user overrides, or runtime effective permissions. The existing `PERMISSIONS-FINAL-TARGET-MATRIX.csv` and `docs/security/permission-audit/ACTION-PERMISSION-MATRIX.csv` retain source evidence; the latter preserves its existing row IDs and source references.

## Canonicalization

The source catalog contains only active canonical keys. Former aliases such as `projects.read`, `Resource.Manage`, `Team.Manage`, `Roles.Manage`, `ReferenceData.Manage`, `Program.Manage`, and `Portfolio.Manage` do not grant access. The canonical frontend constants and API policies use active catalog keys.

The rebuilt catalog includes dedicated read keys for `Issue.View` and `Plugin.View`, exact directory connection management (`Directory.ManageConnection`), and planning editing (`Planning.Edit`). Report, settings, and user administration use `Report.View`, `Settings.View`, and `Users.View`. Each action is catalogued and localized in English and Arabic.

The catalog's action dependencies are enforced during effective-permission resolution. Missing dependencies remove an Action from the effective set; the resolver does not add grants. Assigned approval capabilities remain narrow exceptions where their APIs expose only an exact assigned record.

## Authorization boundaries

1. **Capability:** the actor has the exact permission key for the operation.
2. **Data scope:** the actor is authorized for the current Tenant, Organization, Portfolio, Program, Project, or resource.
3. **Workflow assignment:** approval-only actions require the actor to be assigned to the exact pending step.

Project finance workspace permissions do not imply approvals, settlement recording, reversal approval, budget closing, carry-forward, approval overrides, or ceiling changes. The Project lifetime financial ceiling changes only through final approval of an eligible assigned Change Request.

## Workflow-scoped people lookups

The canonical catalog does not define a blanket `Users.Lookup`/`User.Lookup` capability. Lookup policies are API authorization policies, not permission keys or role grants: a workflow's exact existing operation permission (or the relevant entity View permission) authorizes only the associated lookup projection. The authenticated request middleware rebuilds effective permission claims from the authoritative resolver on every request, so an empty cache is not an implicit allow/deny and current direct Deny overrides are applied before endpoint authorization.

Workflow lookup policies are API policy compositions, not permission-catalog keys or role grants. Project, Program edit, Portfolio/Program manager, Meeting, Work Item, Risk, Resource, Handover, and Payment Obligation lookups are authorized by their canonical operation or assignment permissions and return minimal purpose-specific projections; existing entity list endpoints retain their View policies. The generic `GET /api/users/lookup` remains `Users.View`-only. Cross-Organization/resource candidate eligibility and remaining Finance lookups remain documented rollout gaps.

The Project lookup DTOs are intentionally narrower than management DTOs: Portfolio and Program options expose only hierarchy/display fields needed to create a Project; Organizational Unit options expose ID/name; Currency options expose ID, code, name, symbol, and base-currency flag (not exchange rate); Project user options expose ID/full name, with active status and tenant filtering from the identity repository. Program creation uses its existing eligible-Portfolio lookup and `Program.Create` policy. Lookup authorization does not change any catalog dependency, role grant, resource-level business validation, or management permission.

This is the initial workflow mapping, not permission to reuse one workflow's policy for another. Meetings, Finance, Resources, Handover, and other remaining picker consumers must be mapped to their exact canonical operation and scope, and must use minimal workflow projections before the architecture can be considered complete across all workflows. In particular, Project user candidates are currently tenant-scoped; any workflow requiring a narrower Organization/Project/resource eligibility set must enforce that server-side before it is considered covered. See [F-11](PERMISSIONS-GAP-ANALYSIS.md) and [Workflow Lookup Authorization](WORKFLOW-LOOKUP-AUTHORIZATION.md).

## Protected tenant Admin

The source-derived baseline contains 48 active tenant-scoped permissions. It includes eligible Administration, role-management, Assignment Recovery, and Workflow capabilities plus explicitly approved Portfolio, Program, and Handover operations. Organization, Platform, CrossTenant, and SystemOwner permissions are excluded. The trusted protected identity requires the seeded `admin` role identity; role names alone do not authorize access.

The baseline is applied after direct Deny overrides, so a conflicting Deny cannot remove a mandatory key from effective permissions. Existing active canonical grants are preserved by the permission catalog reconciliation. No new grants are automatically assigned to ordinary roles, users, or `pmo.admin`.

## Persisted permission reconciliation

An explicit `--rebuild-canonical-permissions` operation is available only in Development. It requires `--expected-sql-server`, `--expected-sql-database`, and `--permission-rebuild-backup-directory`. It verifies `SERVERPROPERTY('ServerName')`, `DB_NAME()`, and `DB_ID()`, refuses model changes and pending migrations, creates a copy-only checksum backup on a different volume/share, and requires `RESTORE VERIFYONLY` to pass before writing.

The reconciliation is limited to the seeded Development tenant. It adds missing active catalog rows, normalizes active names/descriptions, archives unknown/retired Permission rows, removes only links to those retired keys, archives their direct overrides, and writes audit events. It never fans a retired broad grant into multiple active permissions. Existing active canonical role/user grants and memberships are not changed.

The operation was **not run** in this task: the current Development target, physical schema, and independent backup were not verified. No migration or database write was performed. Use `scripts/database/ef-database-gate.ps1` and complete the required before-state and backup reviews before an authorized Development execution.

**Role category overlay (2026-11-05):** Role composition also uses explicit `AllowedCategories` metadata for each permission. The code-backed mapping is in `apps/api/src/NEXUS.Modules.Identity/Application/RoleCategoryPolicy.cs`; it does not change permission keys, runtime scope, or effective authorization. See [Role Category Eligibility and Migration Notes](ROLE-CATEGORY-ELIGIBILITY-AND-MIGRATION.md) for enforcement and the source-only seed conflict inventory.
