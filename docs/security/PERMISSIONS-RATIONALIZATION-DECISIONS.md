# Permission Rationalization Decisions — Phase 02

**Status: Proposed for Product Owner review.** No key, grant, seed, policy, or UI change is authorized by this report. Persisted tenant role grants and effective user permissions were not queried.

## Decision summary

- Exact duplicate keys: **0** across the 103 current seeded Resource/Action pairs.
- Duplicate English display labels: **1 pair** — projects.delete and Project.Delete both display the Projects/Delete wording. This is not an exact key duplicate.
- Semantic overlap clusters: **4**. Each is resolved below.
- Orphan candidates: **9**; none is recommended for immediate deletion. One, reports.read, is recommended for enforcement rather than retirement.
- Broad permission candidates from Phase 01: **10**. Program.Manage and Portfolio.Manage are also reviewed because the Product Owner explicitly asked about them.
- Every one of the 103 current keys has a row and proposed disposition in PERMISSIONS-TARGET-MATRIX.csv. Seeded-role columns show source seed associations only; runtime grants remain NOT VERIFIED.

## Four semantic overlap clusters

| Cluster | Classification | Keys | Proposed treatment | Compatibility risk |
|---|---|---|---|---|
| Legacy lowercase keys vs canonical keys | Compatibility aliases and screen/action ambiguity | projects.read/write/delete vs Project.View/Create/Edit/Delete; portfolios.read/write vs Portfolio.View/Manage; resources.read/write vs Resource.View/Manage | Keep canonical PascalCase keys. Merge the three legacy read keys into canonical View keys only after persisted grants/client usage review. Map the legacy project delete key to Project.Delete only after review. Deprecate broad legacy write keys after manual role mapping; never fan one broad grant out to several narrow keys automatically. | A mapped grant may create access where the old key did not pass an active backend check; do not bulk-copy. |
| manager.handover vs Handover family | Ambiguous legacy alias; meaning is not proven equivalent | manager.handover; Handover.View, Handover.Manage, Handover.Approve, Handover.AdminInitiate | Keep Handover keys with explicit labels for view, operational management, approval, and administrative initiation. Retain manager.handover until dynamic, API-client, persisted-grant, and historical uses are checked; then deprecate without automatic mapping. | Mapping manager.handover to Handover.Approve could create approval authority. |
| Umbrella Manage/Write vs narrower actions | Mixed: true broad aliases for some keys; domain-specific lifecycle actions for others | Resource.Manage, Team.Manage, Directory.Manage, Planning.Manage, Meeting.Manage, users.write, settings.write, finances.write, Governance.AssignmentRecovery.Manage, ChangeRequest.Manage | Split user, settings, financial workflow, Program, and Portfolio operations as listed below. Deprecate Resource/Team/Directory/Planning umbrella keys after manual migration. Keep Meeting.Manage as a domain-specific post-meeting capability; relabel it. Keep Governance.AssignmentRecovery.Manage and ChangeRequest.Manage with descriptions that limit them to their audited workflow/lifecycle. | A broad key may have been relied on through policy aliases even where a specific key exists. Inventory all policy aliases and active consumers before removal. |
| Projects/Delete duplicate label | Same intended capability at display level; two different keys | projects.delete and Project.Delete | Label projects.delete as legacy and canonicalize new assignments to Project.Delete only after grant review. Do not automatically grant Project.Delete from the legacy key. | The old key has no active check in Phase 01 static source review, so conversion could newly authorize deletion. |

## Broad permission decisions

| Current key | Source-backed purpose / breadth | Proposed treatment | Sensitive capabilities explicitly excluded | PO status |
|---|---|---|---|---|
| finances.write | Budget preparation, Payment Request operations, and other financial writes; current policies also require it alongside some specific sensitive keys. | SPLIT known Annual Budget and Payment Request lifecycle actions into dedicated keys. Retain finances.write temporarily for residual financial write consumers until each is mapped. Remove it as an additional prerequisite for approval/settlement/reversal/closing/carry-forward only after approval and review. | Approve/return budget, approve/reject Payment Request, settlement, reversal, fiscal closing, carry-forward, route override. | Pending |
| Program.Manage | Currently guards Program create, edit, archive, and reactivate endpoints. | SPLIT into Program.Create, Program.Edit, Program.Archive, Program.Reactivate. Require Program.Create for both normal and cross-Portfolio creation. | Cross-Portfolio scope expansion and every workflow approval. | Pending |
| Portfolio.Manage | Currently guards Portfolio create, edit, archive, and reactivate endpoints. | SPLIT into Portfolio.Create, Portfolio.Edit, Portfolio.Archive, Portfolio.Reactivate. Keep tenant, organization, and resource checks. | System/tenant-admin bypass and Program creation rights. | Pending |
| users.write | User account, role, role assignment, and direct permission override administration share one broad gate. | SPLIT into Users.ManageAccounts, Roles.Manage, Users.AssignRoles, UserPermissions.Manage. Keep users.reset-password separate. Preserve the current delegation rule: an editor may assign only permissions the actor holds. | No automatic grants; no self-assignment or role-name bypass. | Pending |
| settings.write | Tenant/organization settings and reference-data changes share a broad gate. | SPLIT into Settings.ManageTenantConfiguration and ReferenceData.Manage; keep platform settings under platform-only policies. | Platform settings, tenant registry, or cross-tenant writes. | Pending |
| Resource.Manage | Umbrella overlaps specific resource create/edit/delete, assignment/allocation, and rate permissions. | DEPRECATE after every policy alias and persisted grant is reviewed; manually map each role to needed existing action keys. | Do not copy grant to every Resource action. | Pending |
| Team.Manage | Umbrella overlaps team creation, editing, and deletion. | DEPRECATE after review; manually map to Team.Create/Edit/Delete. | No automatic fan-out. | Pending |
| Directory.Manage | Umbrella overlaps connection testing, sync preview/execute, mappings, and provisioning policy. | DEPRECATE after review; manually map to each intended existing action. | Sync execution and provisioning changes require explicit review. | Pending |
| Planning.Manage | Broad planning capability and AI/planning route/API alias. | DEPRECATE only after planning and AI consumers are mapped to the specific existing keys; preserve Planning.View and WorkItem actions. | Does not grant unrelated project or AI administration. | Pending |
| Meeting.Manage | Current description and policy use it for meeting minutes, comments, and work-item links. It is not proven to be a CRUD alias. | KEEP and RELABEL with the specific post-meeting purpose. Keep Meeting.Create/Edit/Delete independent. | No automatic merge into Meeting.Delete or general meeting CRUD. | Pending |
| Governance.AssignmentRecovery.Manage | Audited assignment-recovery operation, separate from initiation. | KEEP and clarify that it is limited to assignment recovery. Confirm which recovery sub-actions it currently authorizes before any split. | No global admin, handover approval, or general user assignment bypass. | Pending |
| ChangeRequest.Manage | Current description covers create, update, and cancel; ChangeRequest.Approve is separate. | KEEP and RELABEL to “Create, update, and cancel change requests.” Split further only if business owners require distinct roles. | Approval remains separate. | Pending |

## Orphan candidate disposition

“Orphan candidate” means no direct active permission-check reference was found in the Phase 01 static source inventory; it does not prove no dynamic, external, historical, or persisted-grant use exists.

| Key | Phase 01 static finding | Proposed disposition / replacement | Migration rule |
|---|---|---|---|
| projects.write | No active feature check found. | DEPRECATE after compatibility review; possible intended domain is Project.Create/Edit, but the exact grant intent is unknown. | Do not copy to both actions. |
| projects.delete | No active check; exact duplicate English label with Project.Delete. | MERGE display/catalog intent into Project.Delete after review. | No automatic conversion; Project.Delete is actively enforced and conversion could expand access. |
| portfolios.read | No active check. | MERGE into Portfolio.View after grant and client review. | Preserve old row until validated; manual or exact approved mapping only. |
| portfolios.write | No active check. | DEPRECATE; the intended subset of new Portfolio actions is unknown. | Never map to all create/edit/archive/reactivate actions automatically. |
| resources.read | No active check. | MERGE into Resource.View after grant and client review. | Preserve old row until validated. |
| resources.write | No active check. | DEPRECATE; map only to specifically approved existing Resource actions after role review. | Never copy to all Resource actions. |
| reports.read | Seeded and role-mapped, but not used by Reports route/API in Phase 01. | KEEP as the Level 1 Reports View capability and enforce it on the route and read APIs after query-scope review. | No key migration; current grants should become effective only after approved enforcement. |
| reports.export | No matching active export endpoint found in ReportController. | DEPRECATE only after searching dynamic routes, clients, and historical grants. | Do not add an export endpoint or grant based only on the key. |
| manager.handover | No direct use outside seed/constants/localization found; meaning is ambiguous. | DEPRECATE after consumer/grant review. | Never map automatically to Handover.Approve. |

## Translation and grouping corrections

Phase 01 found a damaged replacement-character separator in rendered permission labels, four missing resource translations and ten missing action translations in both EN and AR catalogues, and localized descriptions for only 11 of 103 keys. The Roles & Permissions page groups by raw Resource, causing lowercase legacy and canonical resources to appear in separate groups. The proposed matrix supplies a human-readable EN/AR label and description for every existing key and proposed key. Use specific terms for View, Create, Edit, Submit, Review, Approve, Reject, Return for Changes, Override, Archive, Close, and Reactivate; do not use a generic “Manage” label where the operation is known.

## Runtime and source-seed limits

Seeded role associations are reported exactly from source seed mappings. They do not establish which persisted tenant roles or direct user overrides exist now. Before merging or retiring keys, obtain read-only exports of RolePermissions and direct permission overrides per tenant, check deleted/disabled roles and historical audit references, and check external API consumers. No such persisted data was accessed for Phase 02.

## Project lifetime financial ceiling through Change Requests

Source review confirms that the current Project lifetime ceiling is not directly editable through Project.Edit. ProjectService rejects a direct Budget change and states that it requires an approved Project Change Request (ProjectService.cs:454-455); the Project domain comment confirms creation-time setting and later CR approval only (Project.cs:24). ProjectChangeRequestService records a proposed BudgetChangeAmount and applies it when the request is approved (ProjectChangeRequestService.cs:52-67, 262-302); the workflow service also applies the approved ceiling and records audit data (ProjectChangeRequestWorkflowService.cs:503-519).

Therefore, no ProjectFinancialCeiling.RequestChange key is proposed: ChangeRequest.Manage creates/edits/cancels the CR but does not change the ceiling. Add only ProjectFinancialCeiling.ApproveChange as an extra qualification for approving a CR that contains a BudgetChangeAmount. The target requires ChangeRequest.Approve plus ProjectFinancialCeiling.ApproveChange, current workflow assignment, project scope, separation of duties, and audit. Project.Edit or finances.write alone never changes the ceiling. This addition is a proposal requiring PO approval; it is not a current key or grant.
