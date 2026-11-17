# Independent Critical Review - RBAC Scenario Baseline

> **Supersession notice — 2026-11-14:** This is the pre-implementation critical review. The separate `RBAC-INDEPENDENT-IMPLEMENTATION-REVIEW.md` is the current independent implementation review and controls current blockers/readiness.

**Review date:** 2026-11-14  
**Reviewer role:** Independent Critical Reviewer  
**Disposition:** Original source scenarios reviewed; completeness not certified; not ready for implementation.

## Review scope and source provenance

The first draft did not find the source because it searched the repository/workspace but not Sources. The coordinator later located the latest matching source in conversation Sources as Pasted markdown(9).md, Library item libfile_beac8302215c8191a2b500d789d12a04. It is a 427-line document titled “FOPX PM — Roles & Permissions: Final Goal, Governance, Business Journeys & Scenario Coverage.” This review re-evaluated it against the current working-tree reports and the independent requirements, workflow, and IAM findings. No source code or database was changed or queried.

The source enumerates 83 original scenarios across RM, UR, DP, PA, EP, UX, and CM: 39 Direct and 44 Corner. Its arithmetic correction is valid. The source explicitly says the list is enumerated, not completeness-certified, and its proposed scenarios and W/S values are not approved for implementation. The earlier 29 RBC candidates are not the source matrix and are not 29 proven-new scenarios.

## Critical findings

1. **The source is found, but completeness remains unproven.** Preserve all 83 original IDs and ratings. Keep the source's 12 GAP candidates open until each is added as a scenario or marked Not Applicable with evidence. The review adds 16 unique candidate rows; those also remain proposals.
2. **Two-System-role rule conflicts with current seed and model.** Seed source creates eight System roles; seven are outside the approved pair. Organization Admin is absent from the inspected role seed. Live persisted rows were not queried, so this is not proof that eight rows exist in the current database.
3. **PM onboarding success is not established.** The source reports a POST /api/users 403 and trace 0HNQ2H5VM306U:00000001, but says the rejected permission, branch, and deployed assembly are unverified. Static source shows Project Manager is seeded System and an unheld System-role grant is rejected before the protected Tenant Admin ordinary-delegation branch. This is a plausible/static-supported conflict, not a trace-confirmed historical root cause or successful fix.
4. **System status is coupled to unrelated governance decisions.** Role identity, schema, seed, role administration, assignment, and React UI use IsSystem to imply identity, protection, editability, grant eligibility, and deletion behavior. Reclassifying seven roles is not a data-only flag change.
5. **RM-09 and DP-14 conflate System with Protected.** The source says Tenant Admin is Protected but leaves Organization Admin's exact protection and mutation rules open. Yet those rows group both as protected/immutable. Preserve the IDs, but split or qualify behavior through a PO amendment; do not infer Org Admin protection from System status.
6. **Organization Admin identity and scope are missing.** The inspected model is Tenant-scoped and provides no Organization-specific role membership link; no Org Admin seed or policy was found. Appointment, removal, cardinality, scope, Protected status, last-Org-Admin recovery, assignability, and editability need decisions. A System Role must not receive Tenant-wide authority by name.
7. **DP-04 conflicts with established delegation restrictions.** The source proposes permitting ordinary Administration permissions when not Protected Core/reserved; current delegation policy treats the Administration module and baseline keys as restricted. Keep the conflict visible and preserve all Core/reserved protections pending PO direction.
8. **PA-04 needs an exact Core/optional boundary.** Established protected Tenant Admin policy requires all mandatory tenant Administration permissions, including future additions. “Optional operational permission” must not include mandatory baseline grants.
9. **UR-09 uses a non-canonical permission label.** Source precondition says Users.Create only; current API/service uses Users.ManageAccounts and separately Users.AssignRoles. Preserve UR-09 and amend its fixture/key explicitly.
10. **EP-07 overstates PM-only scope.** A Project-scoped permission does not necessarily imply that only a user's ProjectManager assignment authorizes it. Bind the test to the exact permission, team/assignment policy, and resource resolver.
11. **Severity must not encode release priority.** Source rubric defines S0 as critical compromise/lockout/cross-Tenant impact and S1 as a high core-operation/access failure. The PM journey is source-rated W5/S1 (UR-02/03). The earlier RBC-D02 S0 was a proposal under a different rationale, not an adjustment to source rating.
12. **EP-03 Type conflicts with the source rubric.** Deny precedence is a negative/security boundary case but EP-03 is classified Direct. Recommend Corner classification while preserving the original ID and W5/S0. If approved, the source-set count changes to 38 Direct / 45 Corner.
13. **Several distinct scenarios overlap but should not be deleted.** RM-09/DP-14; UR-02/03/DP-01/02; UR-14/CM-08; and RM-12/DP-12/EP-10/CM-11 overlap in topic but test different operations or race invariants. Crosswalk them and share fixtures where useful; retain each original ID.
14. **Transaction rollback must not erase security evidence.** UR-14 and CM-08 require rollback, while the source also requires traceable denial/failure reasons. Decide business audit behavior separately from security attempt telemetry.
15. **Workflow gaps remain outside the 83 rows.** Current role picker may require Roles.View where Users.AssignRoles alone should authorize minimal choices. Backend and direct-override UI disagree on possession-neutral delegation. Additional gaps include complete override lifecycle, account states, role restore, PM revocation while assigned, Portfolio/Program scope, permission refresh, pending approvals/SoD, and service/bulk operations. See RBAC-ADD-01..16.

## Static code evidence challenged

- Seed: eight System role keys are expected and all eight seeded definitions have IsSystem=true; seven must be reclassified if present in persisted data. Organization Admin is absent (SeedDataProvider.cs:2358-2377, 2492-2521).
- Domain/schema: SystemRoleKey assignment requires IsSystem=true; schema check couples the key to System classification; seed reuses those keys (Role.cs:21-35; RoleConfiguration.cs:11-26; SeedDataProvider.cs:2559-2573).
- Assignment: unheld permissions on System roles are rejected before protected Tenant Admin's ordinary-role possession-neutral branch (UserAdministrationService.cs:551-573). The earlier and current reports do not provide a trace-correlated runtime reproduction.
- Role editing: backend and React derive several edit/deletion/delegation choices from IsSystem (RoleAdministrationService.cs:246-269, 276-314, 407-703; RolesPage.tsx:386, 450-508, 562-598).
- Scope and effective permissions: resolver applies Active Deny then restores mandatory Tenant Admin Core; scope checks remain necessary in consumers (EffectivePermissionResolver.cs:8-29; ProtectedTenantAdminPolicy.cs:76-125).
- Assignment lookup: React calls /api/roles/manage under Roles.View; Users.AssignRoles depends on Users.View, creating an assignment-only lookup gap (AccessPages.tsx:68-75; RolesController.cs:37-43; PermissionCatalog.cs:915-919).
- Direct overrides: backend has eligible unheld delegation checks, but some UI paths require actor possession (UserAdministrationService.cs:739-765; AccessPages.tsx:326-328, 389-413).

These are current working-tree source observations, not deployed-code or runtime verification. Tests, database inspection, API calls, builds and browser UAT were not performed.

## Rating and classification disposition

- Preserve the source's 83 original W/S values. Do not change them without an explicit review entry and PO approval.
- Propose only EP-03 Direct-to-Corner reclassification; keep its W/S and ID unchanged.
- Preserve UR-03 W5/S1. Do not carry forward RBC-D02's S0 rating as if it were a source rating.
- The 16 RBAC-ADD candidate ratings are proposals under the source rubric, not a source baseline adjustment.
- Keep source scenario approval separate from implementation evidence. A Confirmed business constraint does not mean the corresponding code passes.

## Independent recommendation

**NOT READY FOR IMPLEMENTATION.** The source is now read and every original ID has a validation crosswalk, but the source itself disclaims completeness and multiple expected outcomes remain open or conflicting. Resolve PO decisions; decide the source GAPs and candidate additions; obtain a verified read-only Development role inventory through the approved host workflow; then obtain explicit Product Owner approval of the final scenario matrix and implementation scope.

No source, database, seed, migration, grant, or runtime state was modified. No tests or UAT were run.
