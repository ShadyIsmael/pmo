# Financial Management: Current-State Audit

**Status:** Analysis only; no implementation or database changes authorized by this document.  
**Review date:** 2026-10-29  
**Repository:** `C:\Projects\pmo`  
**Scope:** Active API and domain code, `apps/web-react`, finance persistence mappings and migration source, and relevant module documentation. The retired `apps/web` application is excluded under ADR-009 and `AGENTS.md`.

**Related proposal:** [Target Business Model](FINANCIAL-TARGET-BUSINESS-MODEL.md) and [Redesign Roadmap](FINANCIAL-REDESIGN-ROADMAP.md).

## Executive summary

The checkout contains meaningful financial functionality: project budget allocation lines, approval-based budget revisions, manually entered cost records, purchase commitments, planned payment obligations with multiple partial settlements, payment due notifications, project financial summaries, and immutable published status-report snapshots. The planned-payment lifecycle is the most explicit and internally constrained part of the current design.

The module is not yet a consistent financial system of record. Several independently implemented project, program, portfolio, EVM, dashboard, and snapshot calculations give different meanings to labels such as **Actual Cost**, **Committed Cost**, **Remaining Budget**, and **Forecast**. In particular, costs and commitments are omitted from some program/portfolio rollups when budget lines exist, while other paths sum them. Budget line actual and commitment fields are read by reports but are not maintained by the observed finance services. This makes some figures incomplete or stale.

The payment obligation/settlement model correctly remains separate from cost recognition and budget deduction. The main blockers to a reliable MVP are consistent source records and formulas, controlled budget edits, exposure reconciliation between commitments and recognized costs, report snapshot completeness, and project-scope authorization on several finance paths. Existing finance records and in-flight work must be preserved while those issues are addressed.

## Review method and limits

Reviewed the repository instructions and finance, project, portfolio, resource, identity, audit, notification, architecture, and EF delivery documentation before source inspection. Also reviewed the repository's EF database delivery, frontend data integrity, security audit, and code-quality guidance. No finance-domain-specific skill package was available in the installed skill catalogs.

The checkout had extensive pre-existing uncommitted work, including finance API/domain/persistence/UI files, migration sources, tests, and the untracked planned-payments documentation. All such work was treated as concurrent and left untouched. This review describes the checked-out source, including uncommitted source where present; it does not establish that the current branch is a clean or deployable release.

This was a source and documentation review. No tests, builds, scanners, database queries, migration generation/application, service restarts, or production access were performed. Therefore, this document does not verify a live Development database, applied migration history, backup, or runtime behavior. Migration source filenames and prior documentation are not evidence of current database state.

## Current implementation map

| Area | Observed implementation |
|---|---|
| API | `apps/api/src/NEXUS.Modules.CostManagement/Api/`: budget, budget revision, cost, commitment, and payment obligation controllers. Finance policies expose read/write operations. |
| Application services | `BudgetService`, `BudgetRevisionService`, `CostService`, `CommitmentService`, `PaymentObligationService`, `PaymentDueNotificationService`, `GlobalFinancialSummaryService`, `ProjectFinancialLookup`, `ProjectEvmDirectCostLookup`, and `BudgetAuthority`. |
| Persistence/domain | `Budget`, `BudgetRevision`, `CostEntry`, `CostCategory`, `CostApproval`, `Invoice`, `PurchaseCommitment`, `PaymentObligation`, `PaymentSettlement`, and `PaymentNotificationDispatch`, registered through `AppDbContext`. |
| Active frontend | `apps/web-react`: cost/finance overview, budget allocations, actual costs, forecasts, planned payments, project finance tab, and budget revision UI. `apps/web` is retired and out of scope. |
| Other financial read models | Project overview/EVM, project status report snapshot, Program management, Portfolio management, and legacy Program/Portfolio services each produce financial views using separate calculations. |
| Notifications | Hourly planned-payment due notification processing through the existing notification system; current documented recipient behavior is creator-only. |

### Direct source file index

These are the primary implementation files traced for the findings below; they are not an exhaustive list of every DTO, interface, or test fixture.

| Concern | Primary source files |
|---|---|
| Budget, revisions, costs, commitments, and payment APIs | `apps/api/src/NEXUS.Modules.CostManagement/Api/BudgetController.cs`, `BudgetRevisionController.cs`, `CostController.cs`, `CommitmentController.cs`, `PaymentObligationController.cs` |
| Finance services, workflow and calculation paths | `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetService.cs`, `BudgetRevisionService.cs`, `BudgetAuthority.cs`, `CostService.cs`, `CommitmentService.cs`, `PaymentObligationService.cs`, `ProjectFinancialLookup.cs`, `GlobalFinancialSummaryService.cs`, `ProjectEvmDirectCostLookup.cs`, `FinanceWorkflowIntegration.cs`, `PaymentDueNotificationService.cs`, `PaymentDueNotificationScheduler.cs`, `ModuleRegistration/CostManagementModuleRegistration.cs` |
| Finance entities | `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs`, `BudgetRevision.cs`, `CostEntry.cs`, `CostCategory.cs`, `CostApproval.cs`, `PurchaseCommitment.cs`, `Invoice.cs`, `PaymentObligation.cs`, `PaymentSettlement.cs`, `PaymentNotificationDispatch.cs` |
| EF model and explicit payment/snapshot mappings | `apps/api/src/NEXUS.Persistence/AppDbContext.cs`, `apps/api/src/NEXUS.Persistence/Configurations/PaymentObligationConfiguration.cs`, `PaymentSettlementConfiguration.cs`, `PaymentNotificationDispatchConfiguration.cs`, `ProjectStatusReportConfiguration.cs` |
| Finance and report migration sources | `apps/api/src/NEXUS.Persistence/Migrations/20261026160000_AddPlannedPaymentLifecycle.cs`, `20261026170000_AddPaymentTenantScopedRelationships.cs`, `20261027202734_AddStatusReportPaymentSnapshotFields.cs` |
| Active finance UI and API client | `apps/web-react/src/features/finance/CostsOverviewPage.tsx`, `CostsPage.tsx`, `CostsForecastsPage.tsx`, `PlannedPaymentsPage.tsx`, `apps/web-react/src/features/finance/api/finance.ts`, `apps/web-react/src/features/projects/ProjectFinanceTab.tsx` |
| Independent EVM, resource-cost, report, and management rollups | `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ActualWorkCostLookup.cs`, `ProjectEvmCalculator.cs`, `ProjectStatusReportService.cs`, `apps/api/src/NEXUS.Modules.ResourceManagement/Application/ProjectEvmLaborCostLookup.cs`, `apps/api/src/NEXUS.Api/ProgramManagement/ProgramManagementService.cs`, `apps/api/src/NEXUS.Api/PortfolioManagement/PortfolioManagementService.cs`, `apps/api/src/NEXUS.Modules.Portfolio/Application/ProgramService.cs`, `PortfolioService.cs` |

## Capability classification

Classifications describe behavior evidenced in the source, not feature names or intended design. More than one classification can apply where a capability has a working slice and a material defect.

| Capability | Classification | Observed behavior and evidence |
|---|---|---|
| Project approved budget ceiling | **Partial / Requires redesign** | `Project.Budget` is documented in `Project.cs` as the current approved project ceiling, changed by an approved Project Change Request. This is distinct from finance allocation lines. The distinction is not consistently surfaced by all read models. |
| Budget allocation lines | **Implemented / Defective** | `Budget` stores planned, approved, actual, committed, forecast, currency, period, category, and optional project/portfolio ownership. `BudgetService` enforces allocation totals against the project ceiling, but allows direct changes to `ApprovedAmount`, bypassing the revision approval path. Project/portfolio ownership validation is incomplete for unowned or portfolio-only records. |
| Budget baseline and original amount | **Partial / Requires redesign** | Budget revision records retain previous/proposed amounts and review details. Some summaries infer original approved amount from the earliest revision. There is no single explicit immutable approved baseline independent of later revisions, and project-ceiling history is in the separate change-request flow. |
| Budget revisions | **Implemented / Defective** | `BudgetRevisionService` supports draft, submit, approve, reject, and cancel; approval checks the amount has not changed and performs the accepted line update within a serializable transaction. Direct budget updates still circumvent it. The approve/reject permission is the same coarse finance-write permission; row-version checks are optional when the token is omitted; duplicate pending revisions can race because creation is not protected by a transaction/unique constraint. |
| Cost categories/classification | **Partial / Missing** | `CostCategory` is persisted but no operational category API/UI path was found. `CostEntry` uses free-text category, subcategory, type, and status. Standard reporting categories and validation are not enforced. |
| Actual cost entry | **Implemented / Defective** | `CostService` supports create/read/update/soft-delete, project/budget validation in common cases, and audit events. It accepts unprojected entries, permits zero amount, does not enforce an accounting lifecycle, and has inconsistent project/budget scope validation when only `BudgetId` is supplied. Free-text `Type`/`Status` are accepted. |
| Cost approval | **Missing operational workflow** | `CostApproval` is in persistence, but no active service/controller or UI workflow was found. Cost CRUD is not gated by this entity. |
| Cost recognition versus cash paid | **Partial / Requires redesign** | Current EVM has a defined direct-cost rule and separate time-entry labor calculation. Project finance summaries and global summary instead sum all non-deleted cost entries regardless of type/status and omit the same labor calculation. Settlements do not create costs, correctly preserving the agreed boundary. |
| Cost estimates and forecasts | **Partial / Defective** | Budget allocation lines carry `ForecastAmount`. The forecast page calculates line variance from the current page of budget rows and is not a full project EAC forecast. Summary services use the sum of line forecasts when present, otherwise return null or a different fallback. Forecast method, confidence, remaining-work estimate, and as-of date are not governed centrally. |
| Purchase commitments | **Implemented / Partial** | `CommitmentService` creates and updates project commitments, tracks open/active/closed/cancelled status, and enforces project currency and budget-project pairing. There is no amendment history, line-level lifecycle, activation route, invoice match, settlement link, or release/invoiced reconciliation. |
| Commitment exposure and remaining amount | **Defective / Requires redesign** | Current reports use gross active commitment amount. `InvoicedAmount` is persisted but has no observed writer and is not incorporated in exposure. `BudgetAuthority` sums posted costs plus gross commitments, so a cost recognized against a commitment can be counted again. Close/cancel does not guard invoiced or paid activity. |
| Invoice processing | **Missing operational capability** | `Invoice` and `VendorId` fields exist in the model, but no invoice service/controller/UI or operational commitment-to-invoice relationship was found. Invoice data is not a source for the observed financial summaries. |
| Planned payment obligations | **Implemented** | `PaymentObligationService` owns project-scoped obligations, a project-name snapshot, optional informational milestone, due date, currency, and purpose/reference. It has CRUD with optimistic concurrency and prevents deleting an obligation after settlements exist. See `FINANCIAL-MANAGEMENT-PLANNED-PAYMENTS.md`. |
| Multiple/partial settlements | **Implemented** | One obligation can have many `PaymentSettlement` records. Settlement amounts must be positive and no greater than the current outstanding amount; amount precision is limited to four decimal places; duplicate idempotency keys are handled; the parent row version is updated with the settlement. No settlement update/delete API was found. |
| Outstanding and due/paid statuses | **Implemented / Partial** | Outstanding is obligation amount minus settlements, floored at zero. Due status (`Upcoming`, `Due`, `Overdue`, `Settled`) is calculated separately from settlement amount. Date boundaries use UTC day, not a tenant/project timezone. There is no adjustment/reversal path for settlement errors. |
| Payment evidence and correction | **Missing / Requires redesign** | Settlement fields do not include evidence/document reference, and no correction/reversal command exists. Immutable original settlement rows are a sound start, but a controlled compensating record is required to correct mistakes without rewriting history. |
| Payment notifications | **Partial** | Hourly due processing deduplicates due/overdue dispatch by obligation/date and uses the notification system. Current behavior targets the creator only if still active and authorized. Finance-owner, project-manager, escalation, tenant-local due date, and delivery outcome policies are not configurable. |
| Financial approvals and permissions | **Partial / Defective** | Finance read/write permissions exist and budget revisions support a different-person reviewer. There are no separate approval actions/limits/roles for budgets, commitments, or cost recognition; maker-checker is not consistently enforced. Empty/missing row versions are accepted by several legacy finance services. |
| Project financial dashboard | **Partial / Defective** | `ProjectFinanceTab` shows approved budget, actual cost, committed cost, and forecast from `ProjectFinancialLookup`, alongside allocations, costs, commitments, and revisions. It does not include planned payments/settlements in the project finance view. Its summary actual-cost formula differs from EVM and global summary. |
| Global finance dashboard | **Partial / Defective** | `/api/costs/summary` groups tenant data by project/currency and returns budget, actual, commitment, forecast, and available values. It does not include payment obligations/settlements and has no management-scope filter, unlike the scoped lists. |
| Forecast UI | **Partial** | The active React forecast page uses line planned/approved/forecast values and per-page totals. This is useful as a line review but does not represent an authoritative full-project EAC or explain the calculation method. |
| Published status report financial snapshot | **Implemented / Defective** | Publication captures budget, actual, commitment, forecast, payment, settlement, and outstanding figures into a report snapshot; published reports are immutable. When budget lines exist, snapshot actual and commitment can be taken from line `ActualAmount`/`CommittedAmount`, fields not maintained by observed finance services, rather than from cost/commitment records. Historical payment values are period-filtered, but there is no full effective-dated ledger for reconstructing arbitrary prior source state. |
| Program financial aggregation | **Partial / Defective** | Program management groups by currency and uses budget lines where present; for a project/currency with any budget row, separately loaded cost/commitment rows are not used. Legacy Program service separately uses `Project.Budget`, `Project.ActualCost`, and line forecasts. Neither includes payment settlements in the observed rollups. |
| Portfolio financial aggregation | **Partial / Defective** | Portfolio management groups by currency, but budget rows suppress separate cost/commitment sources for that project/currency. A separate legacy Portfolio service uses project legacy totals and budget forecasts. Parent portfolio/program budget/spend fields and `PortfolioBudget` create parallel persisted financial values whose authority is unclear. |
| Currency and precision | **Partial / Defective** | Most money columns are `decimal(19,4)` and planned payment input explicitly rejects precision beyond four places. Cost and commitment input do not consistently validate scale. Currency is often project currency, but several summaries normalize missing currency to GBP while other management paths default to USD. No FX conversion is performed in program/portfolio summaries, which group currency separately. |
| Audit/history | **Partial / Requires redesign** | Finance services write audit entries for common create/update/delete actions, usually after the business `SaveChanges`, so audit failure can occur after the business change has committed. Soft deletion and mutable cost/commitment rows are not an append-only adjustment ledger. Settlement rows are immutable through current API but lack reversal. Audit trail docs note settlements/notifications are not fully represented in project audit views. |

## Calculations currently in use

These are distinct implementations, not one shared contract:

| Read model | Current calculation behavior | Material consequence |
|---|---|---|
| `ProjectFinancialLookup` | Project ceiling from `Project.CurrentApprovedProjectBudget`; allocated from approved budget lines; actual from all matching non-deleted `CostEntry.Amount`; committed from active commitment gross amount; forecast from line forecasts where any are positive; remaining = ceiling − actual − commitments. Missing currency is normalized to GBP. | Does not filter cost type/status, does not include priced time-entry labor, and does not include payment exposure. |
| `GlobalFinancialSummaryService` | Similar project/currency grouping; actual sums non-deleted costs; committed sums gross active commitments; available = project ceiling − actual − commitments; line forecast; no payments. | Tenant-wide summary is not scoped to the current management project set. |
| Project EVM lookup | Direct `CostEntry` only for `Type=Actual` and `Status=Posted` or `Verified`, through the status date, excluding resource-linked/labor-like costs; adds priced time-entry labor; rejects mixed currency. | Its `Actual Cost` may disagree with project finance tab and global finance summary. |
| Program/Portfolio management summaries | Budget lines are used when present; cost/commitment rows are only used for projects without matching budget rows. Values are grouped by currency; no FX conversion. | Common projects with both budget lines and costs/commitments can appear to have zero/low actual and committed cost. |
| Status report capture | When budget lines exist, actual/committed snapshots use `Budget.ActualAmount`/`Budget.CommittedAmount`; without lines it falls back to project legacy values or cost entries. Forecast sums line `ForecastAmount`. | Stale denormalized line values can override current source records. |
| Forecast page | Aggregates budget line values from the currently loaded page; forecast variance is line approved less forecast (and displayed percentage uses forecast relative to approved). | Pagination can make page totals incomplete; line forecast is not EAC. |

`Budget.Variance` is approved allocation minus line actual, while some summary forecast variance is project ceiling minus forecast. Labels and sign conventions therefore also differ.

## Security and consistency findings

1. **Project scope is not consistently enforced.** The `GET /api/budget-revisions/by-project/{projectId}` route is not extracted as a project resource by `ProjectResourceScopeMiddleware`, and its service applies tenant filtering but not management scope. The revision-create body identifies only a budget; its service helper allows an empty project scope. Cost or budget records submitted with a `BudgetId` but no `ProjectId` also do not give the middleware a project owner to check. These paths need owner resolution through the related budget and fail-closed scope checks.
2. **Global summary scope differs from finance list scope.** `/api/costs/summary` requires finance read permission but does not apply the management project filter used by budget, cost, and commitment list services. Decide whether Finance.Read is intentionally tenant-wide; enforce and document that boundary consistently.
3. **Direct budget writes bypass approval.** A user with finance write can update an approved allocation directly. Route all approved amount changes through the revision workflow, or explicitly define a permitted non-approval action and its limits.
4. **Concurrency controls are uneven.** Planned payment writes require a row version; several budget, revision, cost, and commitment paths allow an absent token. Revision creation can race to create duplicate pending revisions.
5. **Audit is not atomic with finance writes.** In many services, audit is called after `SaveChanges`; this can leave a committed business mutation with missing audit evidence if audit persistence fails.
6. **Parent ownership validation is uneven.** A budget may be portfolio-scoped or project-scoped without a clearly enforced exclusive owner invariant; portfolio validity and tenant/project ownership need consistent validation.

These are source-level findings. They were not exercised against a running API or database during this documentation-only review.

## Frontend findings

The active React frontend has finance navigation for overview, budget allocations, actual costs, planned payments, and forecasts. Project finance has separate budget, cost, commitment, and revision interactions. Planned payments are not integrated into the project finance tab. There is no operational invoice or cost approval UI.

Some global create forms do not satisfy the backend's required fields: the global cost form submits project, description, and amount without category/currency, and the global budget form does not let a user supply a currency. The project-scoped forms have richer context and can provide project currency. Form fields also expose free-text financial statuses/types, and the forecast page describes line variance rather than a forecast methodology. These are functionality and data-quality gaps, not evidence that every frontend workflow is broken.

## Data, migration, and preservation notes

- The checked-out model includes payment obligation, settlement, notification-dispatch entities and payment migration source files. The finance documentation present in this checkout says payment migrations had not yet been applied to Development and that snapshot reconciliation was still pending when that document was written. That statement is historical and was not revalidated against a live database.
- The current Development database identity, migration history, backup, pending migration list, and physical schema were not queried or verified. No migration was generated or applied.
- The active working tree already contains concurrent financial migration, snapshot, API, UI, and test work. This audit did not modify any of it. Do not infer that any changed or untracked file is approved or deployable.
- Existing financial figures may be duplicated among project ceiling fields, budget line snapshots, parent portfolio/program totals, costs, commitments, and report snapshots. Any redesign migration needs an inventory and reconciliation plan; it must retain source IDs, currency, effective dates, published snapshots, and audit history.
- No destructive cleanup, data rewrite, or feature removal is part of this proposal.

## Component recommendations

| Component | Recommendation | Reason |
|---|---|---|
| Project approved ceiling / change request | **Keep and clarify** | Existing approved-change boundary is useful; expose its baseline and effective history as the canonical ceiling source. |
| Budget allocation lines | **Refactor** | Keep category/period allocations, but prevent direct approved-amount bypass and distinguish planned, approved, actual, and forecast semantics. |
| Budget revision workflow | **Enhance** | Preserve history and workflow; require concurrency tokens, unique pending revision protection, explicit approval permission, and one mutation route. |
| Cost entries | **Refactor** | Keep operational data and IDs; standardize categories, recognition states, project/budget ownership, evidence, corrections, and the relationship to labor costing. |
| Cost category / cost approval persistence | **Enhance or retire later after inventory** | Category model is not operational; cost approval model has no active workflow. Do not drop either until usage and data are reconciled. |
| Purchase commitments | **Enhance** | Preserve commitments but add controlled amendments, amount release, and traceable recognition/payment linkage to prevent double count. |
| Invoice model | **Retire later or operationalize by PO decision** | It is persisted without workflow or service; determine whether a minimum invoice record is required for MVP before retaining it as an implied capability. |
| Planned payments and settlements | **Keep and enhance** | Preserve the distinct obligation/settlement model and its partial-payment/idempotency behavior; add correction/reversal, evidence, timezone, and broader notification policy. |
| Global/project finance summary calculations | **Replace with shared calculation/read-model service** | Current sources and formulas conflict; a single contract should feed project, dashboard, program, portfolio, and status-report capture. |
| Program/Portfolio read models | **Refactor** | Keep currency-separated aggregation and ownership scoping; aggregate unique project-level facts exactly once and make parent-level budget ownership explicit. |
| Published status snapshots | **Keep and enhance** | Immutability is valuable; fix capture sources and preserve formula version, cutoff/as-of timestamp, currency, and source provenance. |
| Finance frontend | **Enhance / Refactor** | Keep active React screens where useful; repair incomplete global forms and make distinctions and source timing visible. Add invoices only if PO confirms MVP need. |

## Approval boundary

This audit recommends preserving all current financial records and approved behavior while Product Owner decisions in [the target model](FINANCIAL-TARGET-BUSINESS-MODEL.md) and [roadmap](FINANCIAL-REDESIGN-ROADMAP.md) are resolved. It does not authorize rebuilding screens, removing tables/features, changing schema, generating migrations, or applying database changes.

## Source update - Finance Legacy Cleanup - 2026-10-31

This audit records the earlier Finance implementation and its risks. The global legacy Finance React navigation and overview/budget/actual/forecast pages have since been removed, along with their unconsumed React client code. The unscoped legacy `GET /api/costs/summary` endpoint and its duplicate calculation service/DTOs have also been retired. Project Finance now presents the approved six-workspace architecture; the Planned Payments route remains as a compatibility deep link and embedded Project workflow.

Legacy persistence and CRUD APIs remain because Project Overview/EVM, status-report capture, BudgetRevision workflow integration, and potential deployed API clients still depend on these sources. No legacy financial data, entity, mapping, migration, or database object was removed. See [Finance Legacy Cleanup](FINANCIAL-LEGACY-CLEANUP.md) for the removal inventory, verification results, and later database recommendations.
