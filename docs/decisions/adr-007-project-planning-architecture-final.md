# ADR-007: Finalized Project Planning Architecture (Governing)

**Status:** Accepted
**Date:** 2026-08-06
**Author:** Project Planning / Platform architecture

---

## Context

ADR-006 (First-Class Project Planning Module with Versioned Plan Model) resolved the
core planning domain and was Accepted. Before implementation phases begin, the
architecture must be **finalized** so it can act as the single governing reference
for all future Project Planning work. This ADR consolidates, refines, and extends
ADR-006, and is the authority that supersedes any earlier draft/working-draft or
baseline scaffolding decisions.

Several requirements that were only implied in ADR-006 are made explicit here:

- a project may contain **multiple plans**,
- the versioning rules apply **per Plan**,
- version comparison is a **first-class** capability,
- item deletion across versions follows documented semantics,
- importers target the Working version only,
- the module must support both **On-Prem (single tenant)** and **SaaS
  (multi-tenant)** deployments with a single architecture.

This revision additionally makes explicit:

- **Change Requests belong to the Project**, not to a Plan;
- a **first-class `PlanType`** with defined initial values;
- **governing business rules** per PlanType;
- an **architecture diagram** showing Project → Plans and Project → Change
  Requests;
- that **future Planning capabilities** (Monte Carlo, What-if, Primavera/MS
  Project import, AI Scheduling, Resource Optimization, Critical Path, …)
  extend **Plans and PlanVersions only**, never the Project Management domain.

## Decision

Finalize the Project Planning architecture as follows and adopt this ADR as the
governing specification for all future implementation. All statements are `Accepted`.

### 1. Module ownership boundary

- **Project Management owns Projects only.** It is responsible for the project
  lifecycle, project master data, and high-level project state. It must not own
  planning content.
- **Project Planning owns all planning data.** Planning entities live in
  `NEXUS.Modules.ProjectPlanning` and are versioned. Cross-module access to Project,
  Resource Management, and Organization data is via **service interfaces only** (no
  direct cross-module `DbContext`).

### 2. One Project, many Plans

A single project can contain **multiple independent plans**, each identified by a
`PlanType` (see §4). Plans are versioned independently and may be started and
baselined on their own schedule. The current project tables are **not** the plan of
record.

### 3. Change Requests belong to the Project, not to a Plan

A Change Request (CR) is owned by the **Project**. Plans are **impacted** by a CR;
they do not own it. This keeps CRs aligned with the Project Management domain while
allowing planning to react to them.

- A CR may affect:
  - **one Plan**,
  - **multiple Plans**,
  - **Budget only**,
  - **Scope only**,
  - or **any future project artifact** (not limited to plans).

- A CR that affects planning is resolved against the plan(s) it impacts through the
  versioning flow (§10): an approved CR promotes the governing Working version
  contents of each impacted Plan into its next Baseline version. A CR with no plan
  impact (e.g. budget-only) still belongs to the Project and does not force a
  planning version.
- The Project-side CR record is the single source of truth for the request;
  planning stores only the *impact link* (which PlanVersion(s) a CR created or
  modified), never the CR itself.

### 4. First-class PlanType

`Plan` carries a first-class `PlanType` that drives behaviour, rules, and UI
templates. Initial values:

- **Execution** — the primary working plan for the live project.
- **Recovery** — a corrective/re-planning track.
- **Tender** — a bid/estimation plan.
- **Construction** — a build/execution-focused plan.
- **Shutdown** — a maintenance/outage plan.
- **Scenario** — an exploratory alternative that never affects the live project
  until explicitly promoted.
- **Custom** — an extensible type governed by configurable business rules.

PlanType is not a free-text label; it is a typed, extensible enum-like value.

### 5. PlanType governing business rules

Each PlanType is subject to governing rules that constrain the lifecycle and the
valid transitions of its versions:

1. **Execution** is the **only** PlanType that can execute "Start Project"
   (create the initial Baseline v1). Other PlanTypes cannot trigger Start Project.
2. **Recovery** **requires an existing approved baseline** (on the Execution plan
   or on the project) before it can be created/baselined; it cannot be the first
   baseline of a project.
3. **Scenario** **never affects the live project** until explicitly promoted; its
   Working/Baseline versions are isolated from the current plan view and from
   downstream planning until promotion is performed.
4. **Custom** follows **configurable business rules** (defined per
   tenant/project/configuration); its lifecycle is not hardcoded.

These rules are enforced at the Application layer and validated at write time.

### 6. Every Plan owns its own PlanVersions

Versioning is scoped per `Plan`. Each `Plan` has its own sequence of `PlanVersion`
rows; `VersionNumber` is 1-based and unique per `(TenantId, PlanId)`.

### 7. Every planning entity belongs to exactly one PlanVersion

All plan-content rows (`PlanActivity`, `PlanDeliverable`, `PlanWbsNode`,
`PlanDependency`, `PlanAssignment`, `PlanCalendar`) are scoped by a
**non-null `PlanVersionId`**. There are **no unversioned plan-content rows** and
**no JSON payloads** for plan content. Versioning is expressed solely by
`PlanVersionId` — there are no competing nullable `BaselineId` / `PlanDraftId`
columns.

### 8. Working Version is editable; Baselines are immutable

`PlanVersion.Type` is `Working` or `Baseline`.

- A **Working version** is the live, editable working draft.
- A **Baseline** in `Approved` status is **frozen/immutable**; writes are rejected
  and routed to a Working version.

### 9. Start Project creates Baseline v1 from the Working version

The initial baseline action snapshots the current **Working version** into an
immutable **Baseline v1** (`SourceVersionId` records the Working version it came
from). Per §5, **only an Execution plan** can execute Start Project; each eligible
plan is started and baselined independently.

### 10. Every approved CR creates the next Baseline version

A Change Request that is approved promotes the governing **Working version**
contents of each impacted Plan into a **new Baseline version** (`VersionNumber`
increments 1-based per `(TenantId, PlanId)`) and opens the next Working version for
that Plan. Unapproved/rejected CRs do not create a Baseline. This yields a
monotonic, auditable history of approved plan changes. Per §3, the CR itself lives
on the Project; planning records only the plan impact.

### 11. One active Working version per Plan

At most **one Working version is `Active`** per **Plan** (per `(TenantId,
PlanId)`). Opening a
new Working version supersedes the prior active one; a plan can never have two
editable working drafts.

### 12. One Current Approved Baseline per Plan

At most **one Baseline is `Current` and `Approved`** per **Plan** (per
`(TenantId, PlanId)`). A new approved Baseline demotes the previous `Current`
Baseline to `Superseded`
(following a unique filtered constraint). This invariant is enforced per plan,
not per project.

### 13. Resources are referenced from Resource Management only

Planning never stores the resource master. `PlanAssignment.ResourceId` references a
Resource Management resource (via service interface). Planning stores **planning
attributes only**:

- assignment
- allocation
- planned effort / hours
- planned cost snapshots
- scheduling attributes (start/end date, working days)

actual resource data and cost-rate cards remain owned by Resource Management.

### 14. Calendars belong to Project Planning

Project Planning owns calendars. Calendars use **tenant templates with optional
project-level overrides** (a project may override a tenant template). A project
override may reference a template and adjust `WorkingDays` / `Holidays` / effective
dates.

### 15. Activities include Tasks and zero-duration Milestones; Deliverables are separate

`PlanActivity.Type` = `Task` | `Milestone`. A **Milestone is a zero-duration
Activity**. `PlanDeliverable` is a **separate entity**, optionally linked to a
`PlanWbsNode` and/or a `PlanActivity`.

### 16. Deletion semantics across versions

After the first baseline, removing a planning item must never erase it from history.
An item deleted in a later version becomes **`Removed`** in that newer version while
remaining intact (unchanged) in the version(s) where it existed. Before the first
baseline (single Working version), deletion may physically remove the item since no
history exists yet.

### 17. Version comparison is a first-class capability

Two `PlanVersion`s can always be compared (Working↔Baseline, Baseline↔Baseline).
Comparison is a built-in, first-class capability exposed at the application/API
level, not an ad-hoc report. Version comparison is owned by each Plan (see diagram
below).

### 18. Importers always import into a Working version

Importers (Primavera, Microsoft Project, etc.) always import into a **Working
version** and may open a new one if needed. They **never** write directly into a
Baseline. Promotion to Baseline happens only through the approved-CR/Start-Project
flow.

### 19. Deployment target

The single architecture supports both:

- **On-Prem (single tenant)** — one tenant, one logical database.
- **SaaS (multi-tenant subscriptions)** — tenant isolation via `TenantId` + global
  query filters.

No architecture fan or re-design is required between the two; multi-tenancy is the
default modeling and single-tenant is the degenerate case.

### 20. Future Planning capabilities extend Plans and PlanVersions only

Future Planning capabilities — e.g. **Monte Carlo simulation**, **What-if
analysis**, **Primavera/MS Project import**, **AI Scheduling**, **Resource
Optimization**, **Critical Path** analysis, and similar — extend **`Plan` and
`PlanVersion`** and their plan-content rows. They are implemented within the
Project Planning module (plus service interfaces to other modules) and **never
change the Project Management domain**. The Project Management module continues to
own Projects (and CRs, per §3) and does not need to evolve when a planning
capability is added.

---

## Architecture diagram

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
 └── Change Requests
```

Each Plan owns:

- **Working Version**
- **Baseline Versions**
- **Version Comparison**

Change Requests belong to the Project and **impact** one or more Plans (or Budget
/ Scope / other project artifacts); they are not owned by any Plan.

```
Plan (ProjectId, PlanType, Name, Description, Status)   <- one per planning concern
 │ 1
 ▼
PlanVersion (Type: Working | Baseline)                  <- one cohesive version of a plan
 │ 1
 ▼
plan-content rows (all scoped by PlanVersionId)
  activity/deliverable/wbs/dependency/assignment/calendar
```

---

## Decision details (mapping to entities)

- `Plan` → `(ProjectId, PlanType, PlanName, Description, Status, PlanCode)`;
  versioned per-plan.
- `PlanVersion` → `(PlanId, VersionNumber, Type: Working|Baseline, Status,
  Name, SourceVersionId, SourceBaselineId, IsCurrent)`.
- `PlanType` → first-class enum-like value (see §4) with governing rules (§5).
- Change Requests → owned by Project Management (Project domain); planning stores
  only the impact link (the `PlanVersion`(s) a CR created/modified).
- Every storage type inherits `BaseEntity` (`Id`, `TenantId`, `CreatedAt`,
  `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsDeleted`, `RowVersion`) and is scoped
  by the global tenant + soft-delete query filters.
- WBS remains an adjacency list (`ParentWbsId`) scoped to a `PlanVersion`.

> Note: ADR-006 references "one approved baseline per project". This ADR refines
> that invariant to **per Plan** (`(TenantId, PlanId)`), aligned with the
> multiple-plans-per-project model — the governing rule moving forward.

## Consequences

- **Positive:**
  - Owning boundary is unambiguous: Project Management = Projects (+ Change
    Requests); Project Planning = all planning data.
  - CRs stay in the Project domain while plans react to them — a CR can affect one
    plan, many plans, or no plan (budget/scope-only) without forcing a planning
    version.
  - `PlanType` drives behaviour (Execution-only Start Project, Recovery baseline
    dependency, Scenario isolation, Custom configurability) instead of ad-hoc
    flags.
  - Multiple plans per project cover Execution/Recovery/Tender/Shutdown without
    duplicated or unversioned tables.
  - Baselines are permanent/immutable; Working versions are the only editable
    surface; histories can be compared and audit never loses deleted items.
  - Resource/calendar ownership and the On-Prem/SaaS target are explicit and
    consistent with one architecture.
  - Future Planning capabilities (Monte Carlo, What-if, importers, AI scheduling,
    resource optimization, critical path) extend Plans/PlanVersions only, keeping
    the Project Management domain stable.
- **Negative / Cost:**
  - A new module and schema; the legacy Project Management plan tables must be
    decoupled as the plan of record.
  - Baseline promotion and version copying add machinery that must be built and
    tested.
  - PlanType rules (Execution-only Start Project, Recovery baseline dependency,
    Scenario isolation, Custom configurability) add validation logic that must be
    enforced at write time.
- **Risk:** Schema is large and irreversible. It is **gated** — no migration or
  code is emitted until the rollout phase (module skeleton + gated migration) is
  intentionally triggered. The existing draft/baseline scaffolding is reconciled
  with the `PlanVersion` model when the module is built.

## Supersedes

- Supersedes the resolution of ADR-006 in areas it extends/refines (per-plan
  versioning, multiple plans per project, per-plan working/current invariants,
  version comparison, deletion semantics, import-to-working, deployment target,
  CR ownership, PlanType rules, future-capability boundary).
  ADR-006 remains the basis; ADR-007 is the governing specification.

## References

- Full domain model: `docs/architecture/PROJECT-PLANNING-MODEL.md`.
- Prior decision: `docs/decisions/adr-006-project-planning-module.md`.

## Implementation status

Design finalized and Accepted. **No application code or migrations produced.**
Implementation is deferred until the gated rollout phase (module skeleton +
migration) is triggered.
