# Project Planning Domain — Finalized Model

**Status:** Accepted design (governing decision record: ADR-007; basis: ADR-006)
**Owner:** Project Planning architecture
**Implementation:** **Deferred** — no schema, migration, or application code yet.

>> All Open Questions are resolved (see ADR-006 and ADR-007). This document is the
> single source of truth for the Project Planning domain model. ADR-007 is the
> governing architecture for all future Project Planning implementation — it adds
> multiple plans per project, per-plan versioning invariants, version comparison,
> cross-version deletion semantics, import-to-Working-only, and the On-Prem/SaaS
> deployment target, plus Project-owned Change Requests, a first-class PlanType,
> PlanType governing rules, and the boundary that future capabilities extend only
> Plans/PlanVersions. No migration or code should be generated until the gated
> rollout in §8 is triggered.

---

## 1. Core principle

Project Planning is a **first-class module** (`NEXUS.Modules.ProjectPlanning`)
that owns the plans and versions them. A **Baseline** and a **Working version**
(the working draft) both version a plan. A project can contain **multiple plans**,
each identified by a first-class `PlanType` (Execution, Recovery, Tender,
Construction, Shutdown, Scenario, Custom); each plan is versioned independently.
There are **no unversioned plan-content rows** and **no JSON payloads**. Every
plan-content row belongs to exactly **one `PlanVersion`**.

**Change Requests belong to the Project**, not to a Plan. Plans are only impacted
by a CR; they do not own it. A CR may affect one plan, multiple plans, budget only,
scope only, or any future project artifact.

The current Project Management project tables (`Milestones`, `Deliverables`,
`ProjectSchedules`, `ProjectPhases`, `WorkBreakdownStructures`, …) are **not**
the plan of record once this module lands. Baseline and Working versions version
**this planning model**, not those tables.

---

## 2. Module layout

```
NEXUS.Modules.ProjectPlanning/
  Domain/             Plan, PlanVersion, PlanActivity, PlanDeliverable,
                      PlanWbsNode, PlanDependency, PlanAssignment,
                      PlanCalendar, CalendarTemplate, value objects, enums
  Application/        IPlanService, IPlanVersionService, draft & baseline flows,
                      DTOs, mapping
  Api/                PlanController, PlanDraftController, PlanBaselineController
  ModuleRegistration/ AddProjectPlanningModule()
```

- Depends on `NEXUS.SharedKernel`, plus Project, Resource Management, and
  Organization **only via service interfaces** (never direct cross-module
  DbContext access).
- Registered in `Program.cs` with `AddProjectPlanningModule()`.

---

## 3. The `Plan` aggregate and `PlanVersion`

`Plan` is the shared header and anchor for versioning. A project has many plans,
each with a first-class `PlanType`.

```
Project
 ├── Plans
 │     ├── Execution
 │     ├── Recovery
 │     ├── Scenario
 │     ├── Tender
 │     ├── Construction
 │     ├── Shutdown
 │     └── Custom / …
 │
 └── Change Requests          (Project-owned; impact one or more Plans,
                               or Budget / Scope / other project artifacts)

Plan (ProjectId, PlanType, Name, Description, Status)   <- one per planning concern
 │ 1
 ▼
PlanVersion (Type: Working | Baseline)      <- one cohesive version of a plan
 │ 1
 ▼
plan-content rows (all scoped by PlanVersionId)
  activity/deliverable/wbs/dependency/assignment/calendar
```

Each Plan owns:

- **Working Version**
- **Baseline Versions**
- **Version Comparison**

### PlanType

`Plan` carries a first-class `PlanType` (Execution, Recovery, Tender, Construction,
Shutdown, Scenario, Custom) that drives behaviour, rules, and UI templates.
Governing rules:

- **Execution** is the only PlanType that can execute "Start Project".
- **Recovery** requires an existing approved baseline.
- **Scenario** never affects the live project until explicitly promoted.
- **Custom** follows configurable business rules.

### PlanVersion

**PlanVersion** (the version container; replaces the old working-draft and
baseline headers as the single version concept):

| Column | Semantics |
|--------|-----------|
| Id | Guid |
| PlanId | FK → Plan |
| VersionNumber | 1-based, unique per `(TenantId, PlanId)` |
| Type | `Working` \| `Baseline` |
| Status | Working: `Active` (editable); Baseline: `Approved` (immutable), `Superseded`; plus `PendingApproval`/`Rejected` for the deferred approval path |
| SourceVersionId | the Working version a Baseline was snapshotted from |
| SourceBaselineId | the approved Baseline a Working draft was derived from (draft source) |

---

## 4. Resolved design decisions

All earlier Open Questions are answered as follows.

1. **PlanActivity** represents tasks **and zero-duration milestones** (`Type` =
   `Task` \| `Milestone`).
2. **PlanDeliverable** is a **separate entity**, optionally linked to a
   `PlanWbsNode` and/or a `PlanActivity`.
3. **Every planning row belongs to one `PlanVersionId`** — no unversioned rows.
4. **No competing nullable `BaselineId` / `PlanDraftId` columns** — versioning is
   expressed solely by `PlanVersionId`.
5. **`PlanVersion` has types `Working` and `Baseline`** with lifecycle statuses.
6. **WBS is an adjacency list** (`PlanWbsNode.ParentWbsNodeId`), scoped to a
   `PlanVersion`.
7. **ProjectPlanning owns** `Plan`, `PlanVersion`, WBS, activities,
   deliverables, dependencies, calendars, and assignments.
8. **Before the initial baseline**, the project edits a **Working** version.
9. **Start Project (initial baseline, Execution PlanType only)** snapshots the
   current Working version
   into an **immutable `Baseline` v1**.
10. **After baseline**, edits occur only in a **Working** version; approved
    baselines remain immutable.
11. **No unversioned "live plan" rows** — the "current" view is the latest
    Working version (or the approved Baseline if the working draft is empty).
12. **Calendars** use **tenant templates with optional project overrides**.
13. **Resources** are referenced from Resource Management; **assignments store
    planning attributes only** (allocation, hours, dates) — never the resource
    master.
14. **Change Requests belong to the Project**, not to a Plan; plans are only
    impacted by a CR. A CR may affect one plan, multiple plans, budget only,
    scope only, or any future artifact.
15. **`PlanType` is a first-class enum-like value** (Execution, Recovery, Tender,
    Construction, Shutdown, Scenario, Custom) driving behavioural rules:
    Execution-only Start Project; Recovery requires an existing approved
    baseline; Scenario is isolated until promoted; Custom is configurable.
16. **Future capabilities extend Plans/PlanVersions only** — Monte Carlo,
    What-if, Primavera/MS Project import, AI Scheduling, Resource Optimization,
    Critical Path, etc. never change the Project Management domain.

---

## 5. Entities and relationships (final)

Legend: `(n)` = FK column; `[P]` = value/plan scoping column.

```
Plan (1) ──► PlanVersion (n)
PlanVersion (1) ──► PlanActivity (n)         scoped by PlanVersionId
PlanVersion (1) ──► PlanWbsNode (n)          adjacency tree (ParentWbsId)
PlanVersion (1) ──► PlanDeliverable (n)
PlanVersion (1) ──► PlanDependency (n)
PlanVersion (1) ──► PlanAssignment (n)
PlanVersion (1) ──► PlanCalendar (n)

PlanActivity (1) ──► PlanAssignment (n)      (ActivityId)
PlanActivity (1) ──► PlanDependency (n)      (activity edges)
PlanActivity (1) ─  PlanWbsNode (n)          (optional ActivityId)
PlanDeliverable (optional: WbsId?, ActivityId?)
PlanWbsNode (self) ParentWbsId?

CalendarTemplate (1) ──► PlanCalendar (n)    (project override of a tenant template)
PlanAssignment .ResourceId ──► Resource Management (interface)
Plan.PlanType ──► PlanType enum (Execution/Recovery/Tender/Construction/Shutdown/Scenario/Custom)
Plan ──(impact)── Project ChangeRequest (Project-owned; planning stores impact link only)
```

### Entity field summaries

| Entity | Distinct fields |
|--------|-----------------|
| `Plan` | ProjectId, PlanType, Name, Description, Status |
| `PlanVersion` | VersionNumber, Type (Working/Baseline), Status, Name, SourceBaselineId, SourceVersionId |
| `PlanActivity` | Code, Name, Description, Type (Task/Milestone), StartDate, EndDate, Duration, EstimateHours, Progress, WbsId?, TextFormat? |
| `PlanWbsNode` | ParentWbsId?, SortOrder, Code, Name, ActivityId?, DeliverableId?, TextFormat? |
| `PlanDeliverable` | Name, Description, AcceptanceCriteria, DueDate, WbsId?, ActivityId? |
| `PlanDependency` | ActivityId, PredecessorActivityId, Type (FS/SS/FF/SF), Lag |
| `PlanAssignment` | ActivityId, ResourceId (RM), AllocationPct?, HoursPerWeek, StartDate, EndDate |
| `PlanCalendar` | TemplateId?, WorkingDays, Holidays, effective dates |
| `CalendarTemplate` | TenantId, WorkingDays, Holidays |

All rows inherit `BaseEntity` (Id, TenantId, Created/Updated by/at, IsDeleted,
RowVersion) and are scoped by a `PlanVersionId`.

`PlanActivity.TextFormat` and `PlanWbsNode.TextFormat` are optional structured
display metadata. The shared `PlanTextFormat` value object supports only the
approved bold, italic, underline, semantic text/highlight color tokens, and
limited font-size tokens. It stores no HTML or arbitrary CSS and is copied with
the rest of the versioned planning model.

---

## 6. Invariants

1. **Version scoping** — every plan-content row has a non-null `PlanVersionId`.
2. **Baseline immutability** — a `Baseline` version in `Approved` status is
   frozen; writes are rejected and routed to a `Working` version.
3. **Edit only in Working** — post-baseline edits are allowed solely in a
   `Working` version.
4. **One Current Approved baseline per plan** — at most one `Current`+`Approved`
   Baseline per `(TenantId, PlanId)` (filtered unique index); the next approved
   baseline demotes the prior to `Superseded`.
5. **One active Working version per plan** — at most one `Active` Working version
   per `(TenantId, PlanId)`.
6. **WBS adjacency / uniqueness** — tree via `ParentWbsId`, sibling `Code` unique.
7. **No dependency cycles** — validate at write (topological).
8. **Concurrency** — `RowVersion` per row; conflict → `409`.
9. **Tenant + soft-delete** filters; interfaces-only across modules; audited.
10. **Assignments carry planning attrs only** — resource master lives in Resource
    Management.
11. **Version comparison** — any two `PlanVersion`s are comparable as a
    first-class capability (Working↔Baseline, Baseline↔Baseline).
12. **Import-to-Working only** — importers (Primavera/MS Project/…) write into a
    Working version, never a Baseline.
13. **Deletion across versions** — post-baseline, a removed item becomes
    `Removed` in the newer version and remains intact in the version(s) where it
    existed; pre-baseline deletion may physically remove the item.
14. **Deployment target** — single architecture for On-Prem (single tenant) and
    SaaS (multi-tenant); multi-tenant is the default modeling.
15. **CR ownership** — Change Requests belong to the Project; plans are impacted,
    not owners. A CR may affect one/multiple plans, budget only, scope only, or
    any future artifact. Approved plan-affecting CRs promote the impacted
    Plan(s); budget/scope-only CRs create no planning version.
16. **PlanType rules** — Execution is the only type that can Start Project;
    Recovery requires an existing approved baseline; Scenario is isolated until
    explicitly promoted; Custom follows configurable rules.
17. **Future-capability boundary** — future Planning capabilities (Monte Carlo,
    What-if, importers, AI Scheduling, Resource Optimization, Critical Path, …)
    extend `Plan`/`PlanVersion`/plan-content only and never change the Project
    Management domain.

---

## 7. Version lifecycle

Applies per `Plan` (each plan is versioned independently).

| Event | Working version | Baseline version |
|---|---|---|
| Create plan | create v1 (Active, editable) | — |
| Start Project / initial baseline (Execution only) | snapshot → create baseline v1 | Baseline v1 `Approved` (immutable) |
| First post-baseline edit | open new Working (draft) v2 + copy baseline | Baseline v1 unchanged |
| Save/update draft | write Working rows | unchanged |
| Discard draft | revert/delete Working rows | unchanged |
| Approved CR (impacts plan) | Working rows promote | new Baseline vN (demotes prior Current to Superseded) + new Working |
| Rejected CR | unchanged | unchanged |
| Scenario promotion (explicit) | Scenario Working promotes | Scenario Baseline reflects promoted state |

The **Working version = the working draft**. All editing after a baseline flows
to it; approved baselines are permanent. Only one `Active` Working version and one
`Current`+`Approved` Baseline may exist per `(TenantId, PlanId)`. Change Requests
belong to the Project; they impact plans (promoting them to a new Baseline when
approved) but are not owned by any plan.

---

## 8. Rollout / implementation sequence (to run after this design is greenlit)

> This section is a plan. No part of it has been run yet.

- **Phase 1 — Module skeleton:** create `NEXUS.Modules.ProjectPlanning` with
  Domain / Application / Api / ModuleRegistration; define entities §5, enums, and
  value objects; add `DbSet`s to `AppDbContext`; wire `AddProjectPlanningModule()`
  in `Program.cs`.
- **Phase 2 — Version/WBS foundation:** `PlanVersionService` (create plan, open a
  Working version, snapshot into an immutable Baseline), `PlanVersion` reads;
  `PlanWbsService` (adjacency tree).
- **Phase 3 — Plan content:** Activity / Deliverable / Dependency / Assignment /
  Calendar CRUD within a `PlanVersion`.
- **Phase 4 — API + gate:** read current plan; plan-draft (GET/PUT/DELETE);
  initial-baseline POST. Maintain the pre-baseline (direct Working edit) and
  post-baseline (draft-only) behavior.
- **Phase 5 — Migration + tests:** first planning migration; tests for
  pre-baseline edits, post-baseline draft creation, repeated edits, discard, and
  baseline immutability.
- **Phase 6 — Docs:** update module catalogue, domain model, and this doc to
  implemented.

Signoff for the above is the acceptance of **this** finalized design (ADR-006)
and the governing architecture (ADR-007).

---

## 9. Out of scope

- CR submission, draft promotion to baseline, approval UI, notifications. CRs are
  Project-owned artifacts (see ADR-007 §3); planning stores only the impact link.
- No JSON payload for plan content.
- No modification of the current Project tables to represent plan content.

---

_This is a design document only. It is not a claim of implementation, and no
application code or migrations were produced for it._
