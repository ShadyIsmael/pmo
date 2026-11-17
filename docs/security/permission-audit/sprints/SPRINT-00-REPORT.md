# Sprint S0 — Evidence Baseline & Decision-Ready Architecture

**Date:** 2026-11-13  
**Result:** **COMPLETE — EVIDENCE BASELINE ONLY**  
**Repository:** `C:\Projects\pmo`  
**Observed branch/commit:** `Document-Center` / `cdd1bddf46b851eef38fc915137d4eefe32c1bc4`  
**Execution type:** Read-only source audit plus this report; no policy implementation.

## 1. Work performed

Five focused, independent source reviews covered IAM/catalog and protected Admin behavior, API/resource authorization, approval workflows, frontend permission UX, and cross-cutting security risks. The provided plan and five supplied audit inputs were read. The repository's existing permission audit inventory, action matrix, and referenced source files were inspected.

## 2. Canonical permission catalog

The current `PermissionCatalog.cs` matches the supplied `PERMISSION-CATALOG-INVENTORY.csv`:

| Measure | Count |
| --- | ---: |
| Active, unique canonical keys | 188 |
| View / Action | 49 / 139 |
| Modules | 8 |
| Tenant / Project / Portfolio / Program / Organization scopes | 141 / 44 / 1 / 1 / 1 |
| `IsTenantAdminBaseline=true` | 18 |
| Duplicate keys / SeedIds | 0 / 0 |

All catalog label and description fields are populated. This does not establish translation quality; the supplied catalog export contains malformed Arabic module/screen text for `ProjectStatus.Change` and a Persian-script module label for `ProjectPhase.Change`.

### Protected Admin baseline reconciliation

- `ProtectedTenantAdminPolicy.MandatoryPermissionKeys` is derived from **all 141 active Tenant-scoped keys** plus 14 named Portfolio/Program/Handover keys. Those 14 are already Tenant-scoped in this catalog, so set de-duplication leaves **141 distinct mandatory keys**, not 155.
- The legacy Administration/flag subset is 49 distinct keys: 39 Administration-module Tenant keys plus 10 additional Tenant keys carrying the baseline flag. Adding the 14 explicitly named Portfolio/Program/Handover keys yields a 63-key comparison set. This is not an approved replacement for the current 141-key policy and does not fully represent the prior request for dedicated manager assignment operations.
- The catalog has no dedicated Portfolio/Program manager assign, reassign, remove, or general assignment-management keys. The Portfolio update route uses `Portfolio.Edit`; manager handover has separate `Handover.*` permissions. Whether this satisfies the prior operation-level permission requirement is unresolved. The catalog also represents retirement as Archive/Reactivate rather than a distinct Delete key.
- Current seed source has two separate Project-scoped supplemental Admin grants: `ProjectSettings.View` and `ProjectSettings.Edit`. The attached database report's claim that the seed adds 14 Annual Budget permissions does not match the current seed list; the 14 Annual Budget keys appear in bootstrap delegation material, which is a different concept.
- `AdminRoleSeedPermissionKeys` is another seed-time grant list and must be kept distinct from mandatory Core, supplemental grants, persisted role grants, and effective permissions.
- Every dependency of the current 141-key Tenant baseline is also in that baseline. The 63-key candidate likewise has no currently missing View dependencies in the supplied catalog. This does not define the rule for future dependencies.

### Source/test inconsistency

`PermissionCatalogArchitectureTests.cs` still expects 185 catalog entries (49 View, 136 Action) and a mandatory set of 59. Current source and catalog contain 188 entries and 141 mandatory Tenant keys. This is a source-level test-baseline mismatch; tests were **not run**, so no test outcome is claimed.

## 3. API authorization and endpoint/action coverage

- Current source contains 94 controller files and 676 HTTP route attributes. The generated `API-ENDPOINT-S0-CENSUS.csv` contains all 676 attributes with method names and source lines; 13 route declarations have the full path on the method attribute and no class-level route prefix. The source review found authorization attributes on 671 routes; five routes are explicit anonymous authentication/version endpoints. `Program.cs` also configures an authenticated fallback policy.
- The repository's `API-ENDPOINT-INVENTORY.csv` has 604 imported records: 603 endpoint rows, one row missing its method name, and one empty trailing row. It was last modified before the current source baseline. Its usable rows are 73 short of the current 676 route attributes. The attached backend report's 605 count is also stale.
- The existing `ACTION-PERMISSION-MATRIX.csv` has 219 rows, but most coverage statements are source-only and runtime-unverified; retired-key rows still await persisted/runtime exclusion evidence. The new route census marks all 676 canonical permission mappings as `NOT MAPPED` with an explicit evidence-gap status until the per-route business action, policy, service scope, and frontend consumer are joined and independently reviewed.
- A literal scan found 167 distinct `[Authorize(Policy=...)]` names. Static string comparison alone is inconclusive because policies are registered through constants and dynamic registration paths. No missing policy was confirmed by this pass.
- There are 147 class-auth-only candidate routes in the existing source scan. This is a review queue for service-level checks, not a confirmed vulnerability count.
- Role and user administration have both controller- and service-level checks in reviewed paths. Tenant filters, role/permission tenant predicates, protected-key guards, and the per-tenant SQL Server application lock were observed. Full route-by-route scope validation remains open.

## 4. Effective permissions, delegation, and lockout protection

- Multiple user roles are combined as a union of tenant-local permission keys. Direct user Allow adds a key; direct user Deny removes ordinary grants; protected mandatory keys are re-added after Deny.
- Dependency enforcement runs after protected mandatory keys are re-added. No current mandatory key loses a dependency in the supplied catalog, but a future cross-scope dependency could make a protected action ineffective unless the invariant is tested.
- Permission claims are rebuilt by middleware on authenticated requests rather than trusted as long-lived JWT snapshots. No stale-permission cache was found in that path.
- The effective resolver itself returns permission keys, not resource-specific grants. Project, Organization, Portfolio, Program, and resource scope therefore depend on service/controller checks and still need complete mapping.
- Role, user, and direct-override mutations use serializable transactions and a per-tenant SQL Server application lock. A design risk remains: the acting user's permissions are acquired before that lock and then reused. Revocation racing an in-flight mutation has no identified test.
- Existing review found no role-to-role inheritance or role-level Deny. Delegation to custom roles is broad for active, assignable non-Administration permissions; assignment paths add separate checks. This deserves adversarial tests but was not established as exploitable in the source-only review.

## 5. Approval workflow inventory

| Workflow | Observed source behavior | S0 gap |
| --- | --- | --- |
| Change Request | Snapshots approver assignment; validates eligibility; excludes requester; supports reasoned reassignment under serializable/concurrency checks. | Candidate set is based on project sponsor/PM/team/eligible approvers; no Program/Portfolio manager path was found. |
| Annual Budget | Uses Project Settings defaults or saved draft override; preserves immutable per-submission plan/audit; excludes submitter; checks row versions and reviewer eligibility. | No reassignment/recovery path was found if an assigned reviewer becomes ineligible. |
| Payment Request | Persists selected eligible project-team approvers and stage data under serializable/row-version control; rechecks assignment and eligibility. | Submission and decision do not show requester exclusion or a request-type self-approval policy. Runtime impact is unverified. No reassignment path was found. |
| Payment Reversal | Persists approval assignments and audit; excludes requester; rejects replayed/duplicate decisions. | No reassignment path was found. |
| Year-End Closing | Requires separate close approval permission; excludes preparer; validates assigned reviewer and stage. | No reassignment path was found. |
| Generic Approval | Requires current assignee and permission; excludes requester; has a separate delegation capability. | Self-approval behavior is hard-coded rather than read from a request-type policy. |

No blanket maker/checker behavior was introduced. Existing Annual Budget configuration and immutable snapshot decisions remain in force.

## 6. Roles & Permissions UX

- The current Roles page groups by `moduleId:screenId`, separates View/Action, and exposes module as a filter; the visible list is not nested as Business Module → Feature → View & Actions.
- The frontend declares 182 unique permission constants. Six `ApprovalRequest.*` keys exist in the catalog but have no frontend constants or source usages; whether those are intentionally backend-only remains unknown.
- Three different module IDs share the English label “Delivery Management”. The supplied export has the malformed Arabic labels noted above.
- User override UI shows Inherit/Allow/Deny and basic role-granted/effective/protected state, but the current DTO cannot explain which role grants the permission or show resource scope.
- Route guards are UI controls only; they do not establish backend authorization.
- No browser UAT or runtime UI verification was performed.

## 7. Evidence limits and checks not performed

- No tests, builds, or runtime API checks were run.
- No Development database connection, migration inspection, or persisted-grant reconciliation was performed in this S0 pass. Database status is therefore **not reverified here**; no database write or migration was attempted.
- No browser UAT was performed, consistent with Product Owner ownership of browser UAT.
- No complete per-action service-scope matrix was verified. The census records an explicit gap for every route, so the source-only S0 evidence baseline is complete; the gaps block any claim of fully verified authorization coverage and remain a prerequisite for S1.

### Test architecture prepared for subsequent implementation

Use unit tests for resolver precedence, dependency closure, and role/delegation decisions; API integration tests for direct invocation, cross-tenant and cross-resource access, minimal authorized lookups, and policy resolution; transaction/concurrency tests for last-admin removal, grant/revoke races, and duplicate approval transitions; and workflow tests for requester self-approval policy, current assignment/eligibility, immutable plan snapshots, reassignment, and terminal/recovery states. Frontend tests should cover the 188-key single-primary-location map, EN/AR/RTL, protected/denied/dependency states, role-grant diffs, and multi-role effective-access explanations. These are planned checks; none were executed in S0.

## Post-baseline source updates

The S0 counts above are historical source snapshots. The current 683-route census maps all 617 Tenant-policy routes, 8 Platform-only routes, and 58 routes without direct `Policy =` metadata to source-level service/resource-scope and React consumer or explicit no-consumer evidence. No source/UI mapping fields remain marked unverified. The current map remains untested at runtime; API/React tests, database reconciliation, concurrency, and Owner browser UAT are open. Subsequent source fixes and regressions are summarized in the current [Decision Register](../DECISION-REGISTER.md) and [Final Handover](../FINAL-HANDOVER-REPORT.md).

## 8. Git and worktree status

The original worktree was clean at the start. The approved integration base was not identified in repository state. The current execution environment did not permit creation of an isolated shared clone (Git failed with an MSYS named-object permission error), and the repository Git metadata is read-only in this execution context. Therefore the requested feature branch was not created; these documentation files are uncommitted on the active `Document-Center` branch. No source files were modified.

## 9. Decisions required before dependent sprints

See `DECISION-REGISTER.md`. S0 evidence collection and explicit route-level gaps are recorded. DG-01, DG-02, DG-04, and part of DG-05 remain open. DG-03 has approved precedence direction but incomplete resource-scope coverage and concurrency semantics. S1–S6 must not be reported complete until their gates, implementation, tests, and applicable runtime/database checks are satisfied.
