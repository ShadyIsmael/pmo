# ADR-006: First-Class Project Planning Module with Versioned Plan Model

**Status:** Accepted
**Date:** 2026-08-06
**Author:** Project Planning / Platform architecture

---

## Context

The working-draft + baseline flow needs to persist plan edits. The current plan
data lives in **unversioned Project tables** (`Milestones`, `Deliverables`,
`ProjectSchedules`, `ProjectPhases`, `WorkBreakdownStructures`, …) and in a coarse
JSON `ProjectSnapshot`. Earlier proposals — adding a `ProjectPlanDraftId` FK to
the project tables, or storing a JSON payload on the draft — were rejected.

The requirement is that **a `Baseline` and a `Working` version (the draft) version
a first-class planning model**, not the current project tables and not JSON.

## Decision

Adopt a first-class **Project Planning module** (`NEXUS.Modules.ProjectPlanning`)
owning a versioned, relational plan model, and resolve the domain as follows:

1. **PlanActivity** models tasks **and zero-duration milestones** (`Type =
   Task | Milestone`).
2. **PlanDeliverable** is a **separate entity** optionally linked to a
   `PlanWbsNode` and/or a `PlanActivity`.
3. **Every planning row belongs to one `PlanVersionId`.**
4. **No competing nullable `BaselineId` / `PlanDraftId` columns** — versioning is
   `PlanVersionId` alone.
5. **`PlanVersion` types: `Working` and `Baseline`** with lifecycle statuses.
6. **WBS is an adjacency list** (`ParentWbsId` hierarchy) scoped to a
   `PlanVersion`.
7. **ProjectPlanning owns** `Plan`, `PlanVersion`, WBS, activities, deliverables,
   dependencies, calendars, and assignments.
8. **Before the initial baseline**, the project edits a **Working** version.
9. **Start Project** creates an **immutable `Baseline` v1** from that Working
   version.
10. **After baseline**, edits occur only in a **Working** version; approved
    baselines remain immutable.
11. **No unversioned "live plan" rows.**
12. **Calendars** use **tenant templates with optional project overrides**.
13. **Resources** are referenced from Resource Management; **assignments store
    planning attributes only**.

## Consequences

- **Positive:** relational, queryable, versioned plan rows; baselines are
  immutable and diff-able against working drafts; tenant/audit/soft-delete/
  RowVersion preserved; clean module ownership.
- **Negative:** a new module + schema; the legacy Project Management plan tables
  must be decoupled from being the plan of record.
- **Risk:** schema is irreversible — gated so no migration is emitted until the
  gated rollout steps (Phase 1+) begin.

## References

Full model and rollout sequence: `docs/architecture/PROJECT-PLANNING-MODEL.md`.

## Implementation status

Design finalized and Accepted. **No application code or migrations produced.**
The draft↔plan integration and this module are **deferred** until rollout
phase 1 is triggered (module skeleton + gated migration).

The existing `ProjectPlanDraft` foundation and `InitialBaseline` workflow remain
as scaffolding and are expected to be reconciled with the `PlanVersion` concept
when the planning module is built.