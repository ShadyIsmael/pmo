# Canonical permission catalog disposition

The current code-defined catalog is the source of truth. It contains 154 active permissions (39 View, 115 Action), unique seed IDs, English/Arabic labels, and no inactive compatibility entries. This is the final source catalog for this rebuild. Compared with the prior audit report's 133 keys, it adds 24 financial capabilities and retires `ProjectFinance.Manage`, redundant `Resource.Allocate`, and broad `Resource.ManageRates` (net +21). A separate 136-key baseline mentioned in the brief is not available as a repository snapshot and cannot be reconciled.

The 24 granular financial capabilities added in this update are: `Budget.View`, `Budget.Create`, `Budget.Edit`, `Budget.Delete`; `BudgetRevision.Create`, `BudgetRevision.EditDraft`, `BudgetRevision.SubmitForReview`, `BudgetRevision.Cancel`, `BudgetRevision.Delete`; `Cost.View`, `Cost.Create`, `Cost.Edit`, `Cost.Delete`; `Commitment.View`, `Commitment.Create`, `Commitment.Edit`, `Commitment.Close`, `Commitment.Cancel`, `Commitment.Delete`; `PaymentObligation.View`, `PaymentObligation.Create`, `PaymentObligation.Edit`, `PaymentObligation.Delete`; and `PaymentObligationSettlement.Record`. All are represented in the action matrix with their code-defined scopes, dependencies, and seed IDs. Resource allocation now uses `Resource.Assign` without a parallel `Resource.Allocate` key. `Resource.ManageRates` is retired; `Resource.EditCostRate` depends on `Resource.ViewCostRate`.

| Surface or operation | Canonical permission/control | Scope | Source disposition |
|---|---|---|---|
| Projects, details, snapshots | `Project.View` | Tenant plus assigned project scope | Implemented in API and React routes/navigation. |
| AI Planning workspace | `Planning.View` | Tenant/project management scope | Implemented in route guard; action operations retain their own keys. |
| Audit data | `Audit.View` | Tenant audit scope | Implemented on audit routes and APIs. |
| Tickets and test management | `WorkItem.View` | Tenant and assigned resource/project scope | Implemented on routes; API policies remain authoritative. |
| Integrations | `Settings.View` and operation-specific `Directory.*` keys | Tenant | Implemented in UI/API; connection writes use `Directory.ManageConnection`. |
| Issue list/detail | `Issue.View` | Tenant and assigned project scope | Implemented with fail-closed service scoping. |
| Plugin registry list/detail | `Plugin.View` | Tenant | Implemented with explicit tenant filtering; platform scope is not implied. |
| Optional initial Annual Budget draft | `ProjectAnnualBudget.Create` | Tenant/project/fiscal year | Implemented independently from `Project.Create`; draft, ceiling, and scope validation remain. |
| Status report export | `StatusReport.Export` | Tenant/project | Implemented at the API; only Published reports can be exported. |

## Catalog rules

- Keep all permission identifiers in sync across catalog, `Program.cs`, the React permission constants, Routes & Permissions labels, and the action matrix.
- Use dedicated operation permissions and dependencies. Do not turn an old broad key into multiple new grants.
- Unknown or retired persisted keys are ineffective. The explicit Development reconciliation archives those rows and their links; it does not add permissions to existing roles or users.
- Preserve current active canonical grants, including `pmo.admin` grants. Any operation that would alter grants beyond removal of retired aliases requires a separate approved manifest.
- Keep tenant, Organization, project/resource scope, and workflow assignment checks independent from a permission key.

## Unverified runtime state

The persisted permission rows and grant population are unknown. The requested Development reset and EF seed have not run because SQL Server identity verification failed with `Cannot generate SSPI context`. Before reset, verify the exact Development target, inspect model/migration readiness, and confirm explicit Product Owner authorization. No backup is required only for the exact Development `Shady/NexusPmo` reset; ordinary writes still require the verified-backup gate. Never reset Production or another target.
