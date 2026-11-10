# Audit Trail

Status: implemented (2026-10-28). Read-only project audit workspace backed by the
existing `AuditEntries` table. Migration created but **not applied** — see
[Deployment prerequisites](#deployment-prerequisites).

---

## 1. Principles

1. **One audit store.** The trail reuses `NEXUS.Modules.Audit` (`AuditEntry`,
   `AuditActivity`, `IAuditService`) and the tenant-wide `GET /api/audit` surface.
   No second logging system, no frontend-derived feed.
2. **Backend capture only.** Events are written by the owning module inside (or
   immediately after) its own unit of work. There is no EF change interceptor, so
   nothing is audited implicitly.
3. **Only committed work is recorded.** Producers log after their own validation and
   persistence path succeeds; failures do not produce events.
4. **Append-only.** Audit rows cannot be updated or deleted through application code
   (see [Immutability](#7-immutability)).
5. **No fabrication.** A previous value, actor, or timestamp is only shown when it was
   actually persisted. Legacy rows keep `Source = Unknown` instead of guessing.
6. **No read logging.** Opening a project, navigating tabs, or reading the audit trail
   itself never creates an event.

## 2. Event shape

`AuditEntry` (table `AuditEntries`) carries:

| Field | Meaning |
|-------|---------|
| `Id` | Audit event identifier (exposed to authorised readers) |
| `TenantId` | Tenant scope |
| `ProjectId` | Project the event belongs to (nullable for non-project rows) |
| `EntityType`, `EntityId` | Audited record type and identifier |
| `Action` | Business action code (for example `Create`, `StatusChanged`) |
| `Changes` | JSON `{ field: { from, to } }` or `{ field: value }` |
| `OriginalValues`, `NewValues` | Optional recorded before/after payload |
| `ChangedFields` | Comma-separated field names derived from `Changes` |
| `Description` | Optional human-readable description |
| `Source` | `User`, `System`, `Integration`, `BackgroundJob`, `Unknown` |
| `CorrelationId` | `X-Correlation-Id`, else request trace id, else activity id |
| `UserId`, `UserName` | Actor identity when known |
| `Timestamp` | UTC event time |

New rows get `TenantId`, `Source`, `CorrelationId` and `ChangedFields` filled
centrally in `AuditService.AppendAsync`/`LogAsync` (`Enrich`), so producers do not have
to supply metadata they do not own.

## 3. Project scoping

`IProjectAuditScopeProvider` (`NEXUS.Persistence/Audit/ProjectAuditScopeProvider.cs`)
resolves, for one project, the identifiers of the entity families that can be audited
in that project: project, members, stakeholders, snapshots, change requests, status
reports, work items, meetings, risks, issues, budgets, budget revisions, cost entries,
commitments, payment obligations, invoices, documents, allocations, time entries, and
plan/version/activity/WBS/dependency rows.

The trail query includes an event when **either**

- `AuditEntry.ProjectId` equals the project (backfilled `Project` rows and the producers
  that already know the project id, for example work items, time entries, people,
  snapshots, baselines and workflow configuration), or
- the event's `EntityType`/`EntityId` matches an identifier inside the resolved scope.

Most producers predate the `ProjectId` column and still write rows without it. Those
events are matched through the resolved scope rather than by inferring a project from the
payload. A consequence is that an entity moved to another project takes its earlier
events with it, and an entity that was hard-deleted without a stored `ProjectId` drops
out of the trail.

Consequences:

- Resolution costs a fixed number of indexed queries per request (one per entity
  family), not one per audit row. There is no N+1 pattern.
- Soft-deleted children are intentionally included so removals stay visible.
- Scope resolution is the dominant per-request cost (roughly two dozen indexed lookups)
  and grows with the number of entity families, not with the number of audit rows.

### Historical backfill

The migration backfills `ProjectId` **only** where `EntityType = 'Project'` (a project
record's entity id *is* its project id) and sets `Source = 'Unknown'` on all existing
rows. No other historical attribution is invented.

## 4. Coverage by module

Producers were verified by inspecting `IAuditService` usage in each module. "Audited"
means the module writes an `AuditEntry` for the listed operations today.

| Module | Status | Producers / notes |
|--------|--------|-------------------|
| Project management | Audited | `ProjectService` (create, update, delete, archive, reactivate, manager handover, team member add/update/remove), `ProjectPeopleService`, `ProjectStakeholderService`, `ProjectSnapshotService`, `ProjectRoleService` |
| Baseline | Audited | `ProjectBaselineService` (`InitialBaselineCreated` on `ProjectBaselineVersion`) |
| Planning | Audited | `PlanService`, `PlanVersionService`, `PlanActivityService`, `PlanWbsService`, `PlanDependencyService`, `PlanAssignmentService`, `PlanGridCommandHistoryService`, `PlanningCalendarService`, `PlanningImportService` |
| Work items | Audited | `WorkItemService`; workflow transitions via `WorkItemWorkflowIntegration` |
| Meetings | Audited | `MeetingService` (create/update/start/complete/cancel), `MeetingWorkspaceService` (agenda, attendance, action items, minutes), `MeetingSharingService` |
| Risks | Audited | `RiskService` |
| Issues | **Not audited** | The Issue Management module has no `IAuditService` usage. Issue changes are not in the trail yet |
| Finance | Partially audited | `ProjectAnnualBudgetWorkflow` records Annual Budget draft, configuration, submission, revision, decision, and return events; other producers and remaining gaps are listed below. Payment Request persistence now reuses `ApprovalRequest`, `ApprovalAction`, and `AuditEntries`; DTOs and transition rules still do not write events, so request/approval/reservation producers await API command persistence. Invoices, payment settlements and payment notifications have no producer |
| Documents | Audited | `DocumentService` |
| Resources | Audited | `ResourceService`, `AllocationService`, `ResourceCostProfileService`, `TeamService`, `SkillService`, `VendorService`, `VendorCategoryService` |
| Time tracking | Audited | `TimeEntryService`, `TimerService` |
| Governance | Audited | `ProjectChangeRequestService` and its workflow, `ProjectStatusReportService`, `ProjectReportingService`, `ManagerHandoverService`, `AssignmentRecoveryService` |
| Workflow | Audited | `WorkflowManagementService`, `WorkflowExecutionService`, `WorkflowInstanceService` |
| Identity / administration | Audited | `UserAdministrationService`, `RoleAdministrationService`, `DirectoryIntegrationService` |
| Notifications / reporting exports / integration syncs | Not audited | Out of scope for the project trail |

### Field detail quality

- Modules that record `{ field: { from, to } }` (for example `ProjectService` via
  `BuildChanges`) render a true before/after diff.
- Modules that record only a flat object (`{ name, code, status }`) render the recorded
  value in the **After** column and leave **Before** empty. The UI never fills the gap
  with an inferred value.
- Annual Budget Project-default changes use `EntityType = ProjectAnnualBudgetApprovalConfiguration`
  and record before/after method and mandatory approver selections. Each submitted review
  or revision records the Project defaults, effective method/approvers, and—when overridden—
  the actor, UTC timestamp, and reason in `NewValues`. The same transaction persists the
  Approval Request assignments; changing Project Settings later does not rewrite that
  event or the in-progress assignments. `ProjectApproval.Override` is a sensitive permission
  and is not automatically granted to any role.
- Modules that record nothing but an action (for example `ProjectPeopleService`) show
  the action, actor and timestamp with the explicit "no field-level detail" message.

## 5. API

| Method | Endpoint | Permission |
|--------|----------|------------|
| GET | `/api/projects/{projectId}/audit` | `Audit.View` + management scope on the project |
| GET | `/api/projects/{projectId}/audit/facets` | `Audit.View` + management scope |
| GET | `/api/audit` | `Audit.View` (tenant-wide, unchanged) |

Query parameters: `page`, `pageSize` (1–100), `from`, `to` (UTC), `module`, `action`,
`actorId`, `entityType`, `search`, `sort` (`newest` default, `oldest`).

Ordering is deterministic: `Timestamp` then `Id` (both descending by default), so
paging is stable when several events share a timestamp.

`{projectId}` is the project **public** identifier; the routing middleware rewrites it
to the internal id and the management middleware rejects projects outside the caller's
scope with 404. Internal database identifiers are never used in browser URLs.

## 6. Security and redaction

- **Tenant and project scope.** Reads bypass the global query filter deliberately (to
  include legacy rows written without a tenant id) but apply explicit predicates:
  `TenantId = current tenant OR TenantId = Guid.Empty`, `IsDeleted = false`, and the
  resolved project scope.
- **Dedicated permission.** `Audit.View` is required. It is seeded and granted to
  `pmo.admin` only; the UI shows an explicit no-access state instead of widening access.
- **Financial detail.** Keys matching monetary terms (amount, budget, cost, rate,
  payment, invoice, currency, …) are replaced with `[restricted]` unless the caller has
  `finances.read`. Masking is per field, so non-monetary fields on the same event stay
  readable; the response-level `includeFinancialDetail` flag tells the UI that masking
  occurred.
- **Governance detail.** For governance entity types, before/after payloads are
  withheld unless the caller has `ChangeRequest.View`, `StatusReport.View`,
  `Stakeholders.Read` or `Project.Edit`. The event itself (actor, action, time) stays
  visible.
- **Secrets.** Passwords, tokens, keys, connection strings, card data and similar keys
  are replaced with `[redacted]` **before persistence** (`AuditFieldRedactor.Sanitize`),
  so they can never be returned later.
- **Bounded key columns.** `EntityType`, `Action`, `Source`, `CorrelationId`,
  `Description`, `ChangedFields`, `UserName`, `IpAddress` and `UserAgent` are clamped
  centrally in `Enrich` to the indexed column widths, so an over-long display name or a
  client-supplied `X-Correlation-Id` can never fail the business transaction it
  describes.
- **No write surface.** There is no endpoint that creates, edits or deletes an audit
  event.

## 7. Immutability

`AppDbContext.SaveChangesAsync` throws
`InvalidOperationException("Audit records are immutable and cannot be updated or deleted.")`
whenever an `AuditEntry` or `AuditActivity` is tracked as `Modified` or `Deleted`. This
is a central guard, so a future producer cannot silently rewrite history.

## 8. Retention and archival

- No automatic deletion, truncation, or TTL exists. Retention is an operational policy
  decision, not code.
- Archival/export of old events is **not implemented**. It must preserve ordering,
  correlation ids and redaction rules if added.
- `CoverageStartUtc` from the facets endpoint is the first captured event in the
  project's current scope. It is a fact about stored data, not a completeness guarantee.

## 9. Frontend

`/projects/{publicId}/history` (Project → History) is the audit workspace:

- chronological list with actor, module, action, timestamp, record type and source;
- expandable detail with a metadata strip (actor, source, recorded time, record type),
  changed-field chips, a before/after diff table, collapsed raw recorded payloads, and
  audit/correlation ids in a footer;
- compact toolbar: always-visible search, a `Filters` toggle with the active filter count
  (for example `3 Active`), and `Clear all` whenever at least one filter is applied;
- the advanced filter panel (module, action, actor, record type, date range, order) is
  collapsed by default and expands/collapses in place without discarding applied values or
  in-progress drafts;
- applied filters are summarised as removable chips below the toolbar, so the active
  criteria stay visible while the panel is collapsed;
- server-side filtering, sorting and paging through the shared `Pagination` component;
- permission-aware entity links that use the project public id and only appear when the
  caller can open the target module;
- explicit loading, empty, filtered-empty, error and no-access states;
- EN/AR resources with RTL support. Action, module and record-type labels fall back to a
  humanised code when a new producer value has no translation yet.

The collapsed panel is removed from the tab order and the accessibility tree, the toggle
exposes `aria-expanded`/`aria-controls`, and every chip has an explicit accessible name.
Layout is responsive from mobile to wide desktop, uses logical (start/end) spacing so RTL
mirrors correctly, and relies on theme tokens for Light/Dark. The expand/collapse
animation is disabled under `prefers-reduced-motion`.

The legacy Project Manager History card was removed from this tab. Manager handover
history remains available from the delivery/handover surface, and the audit trail records
manager handover events as governance entries.

## 10. Deployment prerequisites

Migration `apps/api/src/NEXUS.Persistence/Migrations/20261028170000_AddProjectAuditTrail.cs`:

- adds `ProjectId`, `Source`, `CorrelationId`, `Description`, `ChangedFields`;
- bounds `EntityType` (128), `Action` (96), `UserName` (256), `IpAddress` (64),
  `UserAgent` (512) so the new indexes are valid on SQL Server;
- backfills `ProjectId` for `Project` rows and `Source = 'Unknown'` for existing rows;
- adds indexes on `(TenantId, ProjectId, Timestamp)`, `(TenantId, Timestamp)`,
  `(TenantId, EntityType, EntityId)`, `(TenantId, Action)`, `(TenantId, UserId)`.

Before applying:

1. Identify the real target database (never infer it from `localhost` or a config file name).
2. A backup is optional for Development/UAT. Create one only when explicitly requested; independent storage and `RESTORE VERIFYONLY` are not prerequisites. Production backup and recovery requirements remain unchanged.
3. Review the generated SQL script (`dotnet ef migrations script`).
4. Apply only after explicit authorisation, then verify migration history and physical schema.
5. Expect the trail to be empty for historical activity except backfilled `Project`
   rows; new events start accumulating from deployment.

### Permission provisioning outside Development

The seeder creates the `Audit` permission (`Resource = Audit`, `Action = View`) and grants
`Audit|View` to `pmo.admin`, but `SeedDataProvider.SeedAsync` returns immediately when
`ASPNETCORE_ENVIRONMENT` is `Production`. In any environment where seeding does not run,
the permission row and its role grants must be provisioned explicitly (administration UI or
a reviewed script). Without them every audit read returns 403 and the workspace shows the
no-access state.

Audit access additionally requires the caller to be inside the project's management scope,
exactly like the other project tabs; `Audit.View` does not widen object-level scope.

## 11. Known limitations

- Issues and invoices produce no events yet; the trail cannot show them.
- Legacy rows have `Source = Unknown` and often no `ProjectId`; they appear only when
  their entity still resolves inside the project.
- Rows whose entity was hard-deleted without a stored `ProjectId` are not reattributed.
- Modules that log a flat payload have no recorded previous value.
- Payment settlements, notifications, report exports and integration sync details are
  not part of the project trail.
- No archival, legal hold, or retention automation.
