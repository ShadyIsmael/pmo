# Final Canonical Permission Catalog

**Snapshot:** 2026-11-02  
**Authoritative source:** `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`  
**Matrix:** `ACTION-PERMISSION-MATRIX.csv`

## Catalog totals

| Measure | Source count |
|---|---:|
| Active canonical permissions | 154 |
| View permissions | 39 |
| Action permissions | 115 |
| Duplicate keys | 0 |
| Duplicate seed IDs | 0 |
| Missing frontend constants | 0 |
| Unknown `Program.cs` permission policy keys | 0 |

The previous audit reports recorded 133 active permissions. This update adds 24 financial capabilities, retires the broad `ProjectFinance.Manage` key, and removes two unsupported/overlapping resource keys (`Resource.Allocate` and `Resource.ManageRates`), yielding 154 (net +21). The task brief also cited a 136-active snapshot; no corresponding source snapshot exists in the repository, so the three-key gap between 133 and 136 cannot be reconstructed. The current count was measured from the catalog source.

## Financial capabilities added in this update

| Resource | Canonical keys |
|---|---|
| Budget | `Budget.View`, `Budget.Create`, `Budget.Edit`, `Budget.Delete` |
| Budget revision | `BudgetRevision.Create`, `BudgetRevision.EditDraft`, `BudgetRevision.SubmitForReview`, `BudgetRevision.Cancel`, `BudgetRevision.Delete` |
| Cost | `Cost.View`, `Cost.Create`, `Cost.Edit`, `Cost.Delete` |
| Commitment | `Commitment.View`, `Commitment.Create`, `Commitment.Edit`, `Commitment.Close`, `Commitment.Cancel`, `Commitment.Delete` |
| Payment obligation | `PaymentObligation.View`, `PaymentObligation.Create`, `PaymentObligation.Edit`, `PaymentObligation.Delete` |
| Payment obligation settlement | `PaymentObligationSettlement.Record` |

The matrix carries the full 154-entry active catalog mapping, stable seed ID, level, bilingual labels and descriptions, scope, dependencies, role-seed mapping, and source evidence. New financial actions keep their operation-specific permissions; `ProjectFinance.Manage`, `Resource.Allocate`, and `Resource.ManageRates` are not active keys or fallbacks. Resource allocation uses `Resource.Assign`; rate viewing requires `Resource.ViewCostRate`, and rate editing requires both `Resource.EditCostRate` and its declared `Resource.ViewCostRate` dependency.

## Metadata and policy rules

- The catalog is the authoritative source for active keys, stable IDs, labels, sensitivity, scope, assignability, and action dependencies.
- Protected tenant Admin grants are derived from active Tenant-scoped `administration` module entries, explicit `IsTenantAdminBaseline` catalog metadata, and the separately approved Portfolio/Program/Handover keys; no hard-coded resource allowlist determines the baseline.
- Effective permission resolution filters unknown/inactive keys and removes actions whose declared dependency is absent.
- The permission seed projection includes active catalog entries only; a catalog test asserts seeded keys and stable IDs match that active set.
- Permission policies and React constants use canonical keys. No compatibility alias participates in an allow decision.
- Legacy mapping metadata has been removed from the catalog and role-permission DTO.
- Scope and business eligibility are evaluated separately from the permission identifier.
- Protected tenant Admin identity is the set-once `Role.SystemRoleKey = "tenant.admin"`; runtime policy does not depend on a display name or creator field.
- Runtime/persisted state remains unverified until the final API tests and requested EF database recreation complete.

## 2026-11-05 source and UI addendum

The current source recount is **166 unique active keys: 44 View and 122 Action**, with 166 unique IDs. Six active `ApprovalRequest.*` permissions (`View`, `Submit`, `Approve`, `Reject`, `Delegate`, and `Cancel`) were missed by an earlier 160-key tally; they were already in source, are in active service use, and are outside the protected Admin baseline. This explains why the task brief's 160 and this report's 154 totals are stale. The catalog metadata work did not add or remove permission keys/IDs. The protected baseline is still exactly 47 keys, with the same set before and after the metadata corrections.

The action matrix has 174 rows marked `Catalog Active=true`, representing 159 distinct active Action IDs. Seven current source keys are absent from its mapping: the six `ApprovalRequest.*` keys and `ProjectSettings.View`. This remains a deferred documentation mapping follow-up; the matrix was not rewritten in this UI task. The Roles & Permissions UI receives the complete active role-scoped catalog from the API, including permission scope and actor-specific `canDelegate`; no persisted database grants or runtime permissions were inspected or changed in this task.
