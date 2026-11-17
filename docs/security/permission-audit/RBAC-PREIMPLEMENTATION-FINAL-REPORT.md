# FOPX PM RBAC Preimplementation Validation - Final Report

> **Supersession notice — 2026-11-14:** The no-implementation recommendation below applied to the earlier pre-implementation phase. The current task authorizes implementation of expressly approved rules. `RBAC-IMPLEMENTATION-FINAL-REPORT.md` is the current consolidated result and supersedes this document's readiness status; no unresolved policy is approved by implication.

**Date:** 2026-11-14  
**Review status:** Source scenario baseline reviewed and reconciled for Product Owner review; completeness and implementation behavior are not certified.  
**Implementation readiness:** **NOT READY - STOP; no implementation approval.**

## Chairman's consolidated recommendation

Do not begin RBAC implementation, role reclassification, migration, seed changes, permission changes, runtime fix, or UAT. The source attachment is now available in Sources as Pasted markdown(9).md, titled “FOPX PM — Roles & Permissions: Final Goal, Governance, Business Journeys & Scenario Coverage.” It contains 83 original scenarios (39 Direct / 44 Corner), but explicitly says the inventory is enumerated, not completeness-certified, and not approved for implementation.

All original scenario IDs and W/S values are preserved in the validated matrix. The prior 29 RBC rows are retained and crosswalked as supplemental analysis, not counted as source scenarios or proven-new requirements. Sixteen unique additional candidate scenarios are proposed for Product Owner review. There are unresolved policy conflicts, including Organization Admin protection/scope, Administration permission delegation, and the optional-versus-mandatory Tenant Admin permission boundary.

## Scenario baseline counts

| Measure | Result |
|---|---:|
| Original source scenarios reviewed | 83 |
| Original source Direct scenarios | 39 |
| Original source Corner scenarios | 44 |
| Original source completeness certification | Not established by the attachment |
| Supplemental reconstructed RBC candidates | 29, crosswalked and not additive |
| Newly proposed unique scenario candidates | 16, RBAC-ADD-01..16; not approved |
| Source scenario rows approved for implementation | 0 |

The source's 83 rows are fully enumerated by family: RM 12, UR 14, DP 15, PA 9, EP 11, UX 10, CM 12. The inventory count is correct. It is not a claim that every actual API operation, workflow variant, or business transition is covered.

For the supplied source, the reviewed/validated row counts are **39 Direct and 44 Corner**: all 83 original IDs, declared types, family totals, and W/S values were reconciled and crosswalked. “Validated” here means source-row accounting and review, not completeness certification, PO approval, or implementation success.

## P0 journey: Protected Tenant Admin creates User and assigns Project Manager

The required journey maps primarily to UR-02/03, DP-01/02, and CM-01:

1. Authenticated protected Tenant Admin in the same Tenant creates a distinct user.
2. The ordinary, eligible, assignable Project Manager role is available in the role picker.
3. The Admin assigns it without holding Project Manager operational permissions.
4. Backend independently validates delegation authority, every role grant, reserved/Core policy, Tenant and resource scope, and self-escalation.
5. User and role membership commit atomically with appropriate audit.
6. The Admin does not gain operational permissions; separate project-assignment/handover governs ProjectManager assignment and approval eligibility.

The attachment reports a POST /api/users 403 with trace 0HNQ2H5VM306U:00000001. The exact rejected permission, rejection branch, and deployed assembly are explicitly unverified. Current static source supports a plausible classification-related cause: Project Manager is seeded as System, and CanAssignRolesAsync rejects an unheld System-role permission before the protected Tenant Admin ordinary-role delegation branch. That is not confirmation of the historical runtime root cause. No successful current runtime journey was evidenced.

A separate workflow gap remains: the user page enables role assignment through Users.AssignRoles but fetches roles from /api/roles/manage behind Roles.View; the catalog does not make Roles.View a dependency of Users.AssignRoles. An assignment-authorized actor may therefore be unable to load role choices. Mutation-side eligibility checks must remain authoritative.

## Principal current implementation gaps and evidence

1. **System-role inventory conflict:** Seed source defines eight System roles: Tenant Admin, Executive, Portfolio Manager, Project Manager, Team Member, PMO Admin, Finance Controller, and Resource Manager. Seven are outside the approved pair. Organization Admin was not found in the inspected seed. This is source evidence, not proof of current persisted database rows (SeedDataProvider.cs:2358-2377, 2492-2521).
2. **Role identity/schema coupling:** Role.AssignSystemRoleKey requires IsSystem=true; SQL constraints require a non-null SystemRoleKey to accompany IsSystem; seed reconciliation expects the existing keys. A flag-only conversion is not safe (Role.cs:21-35; RoleConfiguration.cs:11-26; SeedDataProvider.cs:2559-2573).
3. **System status conflates policies:** Backend and React use IsSystem for mutation, grant configuration, assignment, editability and deletion. Protected Tenant Admin is identified by system key and Tenant; that does not establish Organization Admin protection. There is no evidenced role-level Assignable/Editable policy, and Role has no IsActive field (RoleAdministrationService.cs:246-269, 276-314, 407-412, 675-703; RolesPage.tsx:386, 450-508, 562-598).
4. **Delegation/UI inconsistency:** Backend checks role grants, scope, assignability, restriction, identity and self-escalation. Protected Tenant Admin may delegate eligible ordinary unheld permissions only on the ordinary path, but the seeded Project Manager is System. Direct-override React UI also requires personal possession in some cases even where backend permits eligible delegation (UserAdministrationService.cs:470-595, 739-765; AccessPages.tsx:326-328, 389-413).
5. **Organization Admin semantics are absent:** Inspected role/user membership is Tenant-scoped; no Organization Admin seed or Organization role membership boundary was established. Its key, per-Organization cardinality, appointment/removal/recovery, protected status, assignability, editability, and delegated scope need explicit decisions (Role.cs:5-35; UserRole.cs:5-11).
6. **Core security logic is source-supported, not runtime-verified:** Effective resolver applies Deny to ordinary grants and reapplies mandatory protected Tenant Admin Core. Tenant filters, reserved-scope checks, self-escalation checks and last-admin guard exist in source; no runtime, database, test, concurrency or browser evidence was obtained (EffectivePermissionResolver.cs:8-29; ProtectedTenantAdminPolicy.cs:76-125; TenantAdminMutationGuard.cs:22-68).
7. **Cross-module workflow integrity:** Project Manager Role membership is separate from ProjectManager assignment and finance approver eligibility. Exact consequences of PM role revocation while assigned, request-type approver eligibility, and role/permission changes during pending approvals remain open.

## Missing or conflicting business rules

| Topic | Finding | Required treatment |
|---|---|---|
| RM-09 / DP-14 | Source treats both Tenant Admin and Organization Admin as Protected/immutable, although it leaves Organization Admin's protection and appointment open and requires System/Protected distinction. | Preserve IDs; define Tenant Admin protection separately and leave Organization Admin policy to PO. |
| DP-04 / ADR-RBAC-05 | Source proposes delegating ordinary Administration permissions; existing Admin delegation rules treat Administration-module and baseline permissions as restricted. | PO decision required; retain Protected Core and reserved-scope restrictions. |
| PA-04 | Optional Tenant Admin permission revocation is not separated from the mandatory tenant Administration baseline, including future additions. | Define optional versus mandatory grant rules; no Core weakening. |
| UR-09 | Source says Users.Create only; current API contract uses Users.ManageAccounts and separately Users.AssignRoles. | Reconcile canonical permission fixture while preserving UR-09. |
| EP-03 | Negative/security Deny case is classified Direct though the source rubric places negative/security cases under Corner. | Proposed type adjustment to Corner; keep ID and W5/S0. |
| EP-07 | A Project-scoped permission is treated as available only to a user's ProjectManager assignment. | Decide per permission and authoritative resource/team scope; do not infer from role label. |
| UR-14 / CM-08 | Rollback wording may include audit events, while security scenarios require evidence of failed/denied attempts. | Separate committed business-change audit from security-attempt telemetry, retention and redaction. |
| Organization Admin | Appointment, removal, recovery, per-Organization identity/membership, last-Org-Admin protection, and privilege boundary remain open. | Approve an Organization-specific governance matrix. |
| Session/workflow freshness | EP-09 and CM-05/06/12 leave refresh, re-evaluation, pending approval, reassignment and recovery policies open. | Define behavior per permission/request type before implementation. |

The source's GAP-01..12 are candidate gaps, not automatically closed by partial overlaps. The matrix proposes 16 unique additions RBAC-ADD-01..16, including account lifecycle, retries/idempotency, service identities, bulk assignment, seed recovery, audit provenance, catalog version skew, direct override lifecycle, PM-role revocation effects, explicit Portfolio/Program scope, and minimal role lookup.

## Weight, Severity and classification review

- Preserve all original 83 W/S values pending PO review. The source defines Weight as business importance and Severity as impact if mishandled; Severity is not release priority.
- Preserve UR-03 at W5/S1 as supplied. The earlier synthetic RBC-D02 S0 rating is not a change to source severity.
- Propose reclassifying EP-03 from Direct to Corner because it is a negative/security boundary case under the source rubric. Keep EP-03 ID and W5/S0; if approved, original-set accounting becomes 38 Direct / 45 Corner.
- No other original W/S value is silently changed. The 16 additions carry proposed ratings only and are not approved.

## Decisions requiring Product Owner approval

1. Confirm the authoritative source version (latest located Sources item: Pasted markdown(9).md) and review all 83 source rows.
2. Decide whether to adopt, amend, or reject EP-03's proposed Direct-to-Corner reclassification and each of 16 additional candidates.
3. Define Organization Admin identity, scope, membership cardinality, protection, appointment/removal/recovery, assignability and editability.
4. Resolve DP-04 versus the existing Administration/baseline restriction and PA-04 versus the mandatory Tenant Admin baseline.
5. Clarify EP-07 resource assignment policy, UR-09 permission names, UR-14/CM-08 audit semantics, and pending-approval behavior on changing roles/permissions.
6. Record any GAP-01..12 candidate as a scenario or Not Applicable with supporting evidence.
7. Explicitly approve the final version of the scenario matrix and implementation scope before work begins.

See RBAC-PRODUCT-OWNER-DECISION-REGISTER.md for the full register.

## Proposed implementation workstreams and sequencing

These are planning proposals only, not authorization:

1. **Approval and evidence gate:** resolve decisions, review candidate additions, approve the final scenario version; obtain a verified read-only Development role/permission/membership inventory.
2. **Role identity and migration design:** separate stable built-in identity from System classification; plan preservation of Role IDs, UserRole membership, role grants, direct overrides and audit history; prepare a reviewed rollback plan. No SQL or migration was generated/applied.
3. **Authorization policy separation:** define System, Protected, Category, Assignable and Editable independently; preserve Protected Core, Active Deny, Tenant isolation, resource scope, anti-escalation, and last-admin protection.
4. **API and React journey alignment:** make assignment lookup follow assignment authority; reconcile direct override UI and backend delegation; preserve accurate bilingual visibility and safe errors.
5. **Evidence after authorization:** add per-scenario backend/frontend/integration/persistence tests, then authenticated runtime verification. Product Owner browser UAT remains a later explicit phase.

## Multi-agent review and limits

Requirements, workflow integrity, IAM/security architecture, and independent critical review were performed against the located source and current working-tree evidence. Reviewers agree that 83 original rows were enumerated and crosswalked, but reject a completeness claim. They flagged source conflicts, rating/classification issues, and additional transitions. Current working tree was extensively dirty before this review; findings describe that working copy, not a deployed assembly or verified database.

No source code, database, seed, migrations or permissions were changed. No tests, builds, database queries, runtime requests, or browser UAT were run. Live role-row count, the exact 403 rejection branch, and successful end-to-end behavior remain unverified.

## Deliverables

- RBAC-VALIDATED-SCENARIO-MATRIX.md
- RBAC-REQUIREMENTS-TRACEABILITY.md
- RBAC-END-TO-END-WORKFLOW-REVIEW.md
- RBAC-SYSTEM-ROLE-IMPACT-ASSESSMENT.md
- RBAC-SECURITY-AND-GAP-ANALYSIS.md
- RBAC-PRODUCT-OWNER-DECISION-REGISTER.md
- RBAC-INDEPENDENT-CRITICAL-REVIEW.md

**Final recommendation: NOT READY FOR IMPLEMENTATION. Stop at this analysis and decision register. Do not proceed until Product Owner explicitly approves the final scenario matrix and implementation scope.**
