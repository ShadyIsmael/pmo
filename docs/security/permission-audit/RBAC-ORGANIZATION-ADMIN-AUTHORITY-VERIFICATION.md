# Organization Admin Authority Verification

**Decision:** `DEC-RBAC-ORG-001`  
**Date:** 2026-11-15  
**Status:** Source scope is implemented for named supported configuration paths; tests/API/DB are not fully verified.

## Approved authority boundary

Organization Admin is the second and only other permitted System Role. Its configuration authority requires all of:

1. Canonical `organization.admin` identity, distinct from role name/category.
2. Active same-Tenant role membership for the actor.
3. Fresh effective `OrganizationConfiguration.View` or `.Manage` permission.
4. Active explicit user-to-Organization settings grant for the requested active Organization.
5. Setting/catalog permission to override at Organization scope and no read-only management context.

The role does not imply Tenant or Platform settings access, unrelated Organization access, protected Tenant Admin mutation, user/role administration, financial approval/payment execution, or general operational project permissions. Tenant settings remain controlled at Tenant scope; inherited and Tenant-enforced values are read-only at Organization scope.

Current known canonical keys are `OrganizationConfiguration.View` and `OrganizationConfiguration.Manage`, declared at Organization scope. Current source seed creates the Organization Admin role with these configuration grants and no seeded user membership. If an existing canonical Organization Admin role contains active grants outside this approved set, seed reconciliation now fails closed without removing grants or changing role identity; a reviewed disposition is required. This is source behavior only; actual DB grants are unknown.

## OA-01 through OA-07 detailed acceptance scenarios

| ID / class / W-S | Actor and preconditions | Permissions and scope | Expected response/persistence/audit | Current source/gap |
|---|---|---|---|---|
| OA-01 — View authorized Organization Configurations / Direct / 5-S1 | Active Organization Admin membership in Tenant T; active explicit grant for Organization O; active catalog permission; no Active Deny. | `OrganizationConfiguration.View`; only O and allowed inherited/read-only values. | Read succeeds (HTTP 200); results contain only authorized Organization data; no writes; read audit behavior follows existing service policy. Sibling/Tenant/Platform data is absent or denied. | General organization profiles/settings and named scoped configuration reads use the fresh scoped authority service. BusinessUnit and some org-owned/reference models remain Tenant-only; their inclusion in “all supported Organization Configuration” requires PO ownership clarification. |
| OA-02 — Edit supported Organization-level Configurations / Direct / 5-S1 | OA-01 plus `OrganizationConfiguration.Manage`, grant `CanManage`, current row/version. | Organization scope only; supported field; `CanOverrideAt(Organization)`. | Eligible update/reset succeeds (typically HTTP 200); only O's configuration row changes; audit captures actor, Organization, before/after, expected version; stale version returns conflict without partial update. | Profile/settings, Organization email profile settings, Organization Phase Gate, and Organization Project Lifecycle catalog paths have scoped source guards. Runtime/persistence and each concrete endpoint remain unverified. |
| OA-03 - Manage Organization Lifecycle and Workflow Configurations / Direct / 5-S1 | OA-02; configuration surface explicitly owned by O; not a project operational action. | `OrganizationConfiguration.View/Manage` plus O grant; configuration-only. Existing `ProjectLifecycle.OptionsLookup`, project phase/status change, and approval permissions are separate. | Configuration reads/mutations succeed only for O; other-Organization/Tenant defaults/operations denied; audit and optimistic-concurrency behavior preserved. | React route/nav/catalog and backend service source expose scoped Project Lifecycle catalog configuration and Phase Gate configuration. The approved OA-03 boundary is Organization-level configuration only; Organization state changes, Project state changes and approval actions remain separately authorized. React build and focused UI tests pass; API/persistence are not runtime-verified. |
| OA-04 — Modify Tenant Configurations / Corner / 5-S0 | Organization Admin only; may hold O grant and OrgConfig Manage. | Tenant Settings authority absent. | Tenant setting update/reset denied (HTTP 403); no row or audit mutation. | Separate Tenant policy/controller/service gates remain. No runtime request executed. |
| OA-05 — Modify another Organization / Corner / 5-S0 | Admin has membership/grant for O1, attempts O2 in same Tenant. | O1 grant only; O2 outside exact scope. | Read/write denied or O2 omitted; no persistence/audit change for O2. Cross-Tenant also denied. | Fresh effective permission + exact active grant + active same-Tenant Organization checks are in service source. No DB/API proof. |
| OA-06 — Exercise Platform or Protected Tenant Admin authority / Corner / 5-S0 | Organization Admin only; attempt Platform config and Tenant Admin Protected Core/admin mutation. | OrgConfig keys only; no Platform, Tenant Core, User/Role Administration or Tenant Admin appointment/mutation capability. | Denied (HTTP 403); no change to Platform/Tenant/Admin role, grants, or audit state. | Scoped service/controller separation and ordinary role policy reject these authorities. No runtime proof. |
| OA-07 — Override Tenant-enforced settings / Corner / 4-S1 | Org Admin Manage + exact O grant; setting resolves to Tenant-enforced/inherit-only, or catalog says it cannot override at Organization. | Org scope only; `CanOverrideAt(Organization)` false. | Update/reset denied (HTTP 403); no override row; inherited Tenant value remains effective; no partial audit mutation. | Both update/reset source paths check catalog scope/inheritance. No concrete currently seeded InheritOnly key was identified in the baseline; keep a reviewed isolated fixture until Product Owner names a production example. |

The exact HTTP status is an acceptance target; no current live endpoint response is claimed. The source endpoints should be checked in the executable test report, with persistence and audit assertions.

## Supported and excluded configuration inventory

**Source-supported candidate surfaces:**

- Organization profile/general settings where the settings catalog permits an Organization override.
- Organization-scoped email notification profile settings (not Tenant, Platform, test, or log controls).
- Organization Phase Gate requirement/configuration.
- Organization-target Project Lifecycle phase/status catalog configuration; reference counts are hidden (`null`/N/A) for Org-only views when project scope is not established.

**Still Tenant-only, unscoped, or unclear:**

- BusinessUnit is stored with `OrganizationId`, but current controller policy remains `Settings.ManageTenantConfiguration`; its by-ID read lacks an Organization grant check. Do not enable it for Organization Admin until a same-Tenant/exact-Organization list and by-ID access rule is designed and tested. Decide whether it is among “supported Organization configuration.”
- Organizational Unit/type/membership/reporting workflows need authoritative ownership classification; they do not become Organization Admin authority merely because UI names are organization-related.
- Workflow tables and some reference catalogs have no Organization ownership field; they remain Tenant-only.
- Organization directory/detail, settings-grant administration, organization lifecycle state change, and role appointment/removal/recovery remain outside the current authority.
- Tenant-enforced and inherited values remain non-editable at Organization scope.

## Appointment and bootstrap

Organization Admin role appointment, removal, protection, recovery, maximum/minimum cardinality, and last-Organization-Admin rules were not approved in DEC-RBAC-ORG-001. The ordinary role assignment policy therefore denies appointment. No Organization Admin membership or scope grant is seeded. A user cannot enter the OA workflow until the Product Owner separately decides who may appoint it and how scoped grants are issued/revoked/recovered.

## Frontend/API alignment

React route, navigation, reference-data entry, and catalog UI accept `OrganizationConfiguration.View/Manage`; the selected Organization is derived from server-authorized profiles, not ManagementScope. OA-only actors receive no Tenant-wide lifecycle option. Mutations require effective OrgConfig Manage and the selected profile's `canManageSettings`; at the row level, OA mutation controls are shown only for rows owned by the selected Organization. Tenant rows remain visible as inherited/read-only values to OA, while actors with separate Tenant lifecycle permissions retain their Tenant controls. `ProjectLifecycle.OptionsLookup` remains under existing operational permission policy. The React production build passed; focused lifecycle UI tests passed 1 file/6 tests after the row guard, and an earlier two-file OA UI selection passed 12 tests (overlapping runs). API/DB/runtime assertions and browser UAT remain unverified; browser UAT is reserved for the Product Owner.

