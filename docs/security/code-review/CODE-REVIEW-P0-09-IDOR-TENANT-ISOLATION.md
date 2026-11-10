# P0-09 IDOR and Tenant Isolation Review

**Review date:** 2026-11-08  
**Finding export:** `Semgrep_Code_Combined_Findings_2026_11_08 (1).csv`  
**Scanned source:** `b434cbfa9891d7cafe6bf7792bf143260e915dd1` (`refs/heads/main`)  
**Coverage:** all 95 exported High/Open findings across Project Management, Planning, Workflows, Resources, Documents, Audit, Reporting, and Identity.

The export contains 95 findings, all marked High and Open; Semgrep autotriage marked 14 “True positive” and left 81 without an autotriage result. Those values are export metadata, not the manual dispositions below.

## Outcome

Manual tracing confirmed seven CSV boundary defects: six Resource Management allocation queries omitted the actor's management Project scope, and MPP import review-session creation required only `Planning.View` even though it persists uploaded content and review data. All seven were corrected in source. Tracing also exposed two adjacent authorization defects that were not accurate as the CSV's stated IDOR: the shared Document query admitted project-linked templates outside the actor's Project scope, and MPP import commit could create Resource master records without `Resource.Create`. Both adjacent defects were corrected.

The three Documents findings' direct cross-tenant claims were not reproduced: tenant query filters and the authenticated project route chain protect those actions. The shared document query did, however, admit a same-tenant project-linked template outside management scope. The predicate was narrowed so linked documents and templates require an allowed Project; unlinked templates remain tenant-shared and unlinked ordinary documents remain owner-scoped.

The other 88 CSV claims were not confirmed as exploitable as stated at the public API boundary; five of those point at shared flows where the adjacent document or Resource.Create gaps above were found and fixed. The relevant module evidence and per-ID dispositions are recorded below. A possible functional/routing concern for bulk planning child-ID endpoints is noted under Planning and is not classified as an authorization bypass.

## Changes made

- `AllocationService.GetConflictsAsync` now applies the same fail-closed management Project scope as the other allocation reads.
- `CapacityService` now applies that scope to the Matrix, availability, demand-vs-capacity, Gantt, and per-Resource availability allocation queries. Project Overview continues to filter to the selected Project and its team members.
- `PlanImportController.CreateSession` now requires `Planning.Edit`; `PlanningImportService` repeats the check before parsing or persisting the session.
- `PlanningImportService.CommitSessionAsync` rechecks the current effective `Resource.Create` claim before creating any new Resource. Mapping or ignoring Resources still requires only `Planning.Edit`; creating a Plan still requires `ProjectPlan.Create` plus `Planning.Edit`.
- The import sheet now gates CreateNew and commit on `Resource.Create` and writable management context, with English and Arabic guidance.
- `DocumentService.AccessibleQuery` now keeps project-linked templates inside the current management Project scope. Tenant-shared access remains limited to unlinked templates.

No permission keys or role grants were added or widened. Backend checks read the current effective permission claims, so refreshed Active Deny and revocation continue to apply. Tenant query filters and management scope remain independent controls.

## Verification status and limits

Focused API and React regression tests were added for capacity query scope, allocation conflicts, project-linked template scope, import-session permission, Resource.Create revocation at commit, and import UI gating. The existing dirty `PlanningImportServiceTests.cs` and UAT checklist were amended surgically; their unrelated changes were preserved.

Builds, tests, Semgrep rescans, database work, and browser UAT were not run for this pass. The new tests are authored but unexecuted. Semgrep Platform findings remain Open; this report records source review dispositions and does not update platform triage. Existing uncommitted work in the shared worktree was preserved.

## Control evidence used for disposition

- **Project Management:** authenticated `ProjectPublicIdRoutingMiddleware`, `ManagementContextMiddleware`, and `ProjectResourceScopeMiddleware` resolve the route's tenant-scoped public Project identifier and reject Projects outside the current management scope before controller actions. Child baseline/milestone/project objects are resolved through their owner Project and tenant-filtered persistence. Existing `ProjectPublicIdRoutingMiddlewareTests` and project authorization tests cover these boundaries. Evidence: `apps/api/src/NEXUS.Api/Management/ProjectPublicIdRoutingMiddleware.cs`, `apps/api/src/NEXUS.Api/Management/ManagementContextMiddleware.cs`, `apps/api/src/NEXUS.Api/Management/ProjectResourceScopeMiddleware.cs`.
- **Planning:** project routes pass the same public-ID and management-scope middleware; the planning service loads Plans, versions, and children through tenant-filtered query sets and validates their Project/Plan/version ownership. The `Planning.View`, `Planning.Edit`, `ProjectPlan.Create`, and Active Deny checks remain explicit. Bulk activity and predecessor operations did not show a cross-scope mutation path; child-ID routing may reject some bulk requests before the action, so that behavior should be checked during functional UAT. Evidence: `apps/api/src/NEXUS.Api/Management/ProjectPublicIdRoutingMiddleware.cs`, `apps/api/src/NEXUS.Api/Management/ProjectResourceScopeMiddleware.cs`, `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanningImportService.cs`, `apps/api/tests/NEXUS.Api.Tests/Integration/PlanningImportServiceTests.cs`.
- **Workflows:** workflow entities inherit the tenant boundary and service queries use the filtered `DbContext` sets without bypassing query filters. Workflow View/Manage/Publish permissions are intentionally tenant-wide capabilities; they do not cross the authenticated tenant boundary. Evidence: `apps/api/src/NEXUS.Modules.WorkflowEngine/Api/WorkflowManagementController.cs`, `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowManagementService.cs`, `apps/api/src/NEXUS.Persistence/AppDbContext.cs`, `apps/api/tests/NEXUS.Api.Tests/Security/WorkflowDefinitionTenantIsolationTests.cs`.
- **Resources:** tenant query filters already constrained records to the current Tenant. The confirmed defect was missing same-tenant management Project scope on allocation rows. The fixes use the existing `AllocationService.ApplyManagementScope` rule: missing context filters all allocation rows; an empty Project set permits only projectless rows; otherwise projectless rows and rows in allowed Projects remain visible. Evidence: `apps/api/src/NEXUS.Modules.ResourceManagement/Application/AllocationService.cs`, `apps/api/src/NEXUS.Modules.ResourceManagement/Application/CapacityService.cs`.
- **Documents:** tenant filters and route authorization protect cross-tenant access. The shared `AccessibleQuery` previously treated every template as visible regardless of Project; this was a same-tenant management-scope defect and is fixed. Evidence: `apps/api/src/NEXUS.Modules.Documents/Application/DocumentService.cs`, `apps/api/tests/NEXUS.Api.Tests/Security/DocumentTenantBoundaryTests.cs`.
- **Audit:** project audit routes resolve the route Project and management scope before the controller; the service uses project-scoped audit queries, while the detail endpoint retains its explicit permission check. Evidence: `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs`, `apps/api/tests/NEXUS.Api.Tests/Security/ProjectAuditDetailAuthorizationTests.cs`.
- **Reporting:** report definitions inherit tenant filtering and normal service queries use filtered sets. Project reporting actions also pass through project route scope. Evidence: `apps/api/src/NEXUS.Modules.Reporting/Application/ReportService.cs`, `apps/api/src/NEXUS.Api/Management/ProjectResourceScopeMiddleware.cs`.
- **Identity:** lookup queries use tenant-scoped user repository methods and retain the `Users.View` permission. Evidence: `apps/api/src/NEXUS.Modules.Identity/Api/UsersController.cs`, `apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserRepository.cs`.

## Finding-by-finding dispositions

“Not confirmed” means the CSV's stated cross-tenant or object-scope exploit was not reachable through the reviewed API boundary. CSV status and automated triage were not changed.

| Finding ID | Module / operation | Disposition |
|---:|---|---|
| 1066839056 | Audit — GetTrail | Not confirmed; project route tenant and management-scope checks apply. |
| 1066839057 | Audit — GetFacets | Not confirmed; project route tenant and management-scope checks apply. |
| 1066839058 | Documents — Archive | Cross-tenant claim not confirmed; adjacent linked-template management-scope defect in shared query fixed. |
| 1066839059 | Documents — Download | Cross-tenant claim not confirmed; adjacent linked-template management-scope defect in shared query fixed. |
| 1066839060 | Documents — Preview | Cross-tenant claim not confirmed; adjacent linked-template management-scope defect in shared query fixed. |
| 1066839061 | Reporting — GetById | Not confirmed; tenant-filtered report definition and project route scope apply. |
| 1066839062 | Reporting — GetPaged | Not confirmed; tenant-filtered report definition queries apply. |
| 1066839063 | Identity — GetLookup | Not confirmed; tenant-scoped user lookup and `Users.View` apply. |
| 1066839064 | Project Management — Baseline GetById | Not confirmed; baseline resolves through the scoped owner Project. |
| 1066839065 | Project Management — Baseline GetByProject | Not confirmed; project route identifier and management scope apply. |
| 1066839066 | Project Management — Milestone GetById | Not confirmed; milestone resolves through the scoped owner Project. |
| 1066839067 | Project Management — Milestone GetByProject | Not confirmed; project route identifier and management scope apply. |
| 1066839068 | Project Management — CreateInitialBaseline | Not confirmed; route Project scope is enforced before creation. |
| 1066839069 | Project Management — Status report list | Not confirmed; project route scope applies. |
| 1066839070 | Project Management — Current progress | Not confirmed; project route scope applies. |
| 1066839071 | Project Management — Status report detail | Not confirmed; report and route Project ownership are checked within scope. |
| 1066839072 | Project Management — Status report export | Not confirmed; export reuses scoped report lookup. |
| 1066839073 | Project Management — Financial summary | Not confirmed; project route scope applies. |
| 1066839074 | Project Management — Compare published reports | Not confirmed; both reports resolve within the scoped Project. |
| 1066839075 | Project Management — Create status report | Not confirmed; route Project scope applies. |
| 1066839076 | Project Management — Update status report | Not confirmed; report resolves through the scoped Project. |
| 1066839077 | Project Management — Publish status report | Not confirmed; report resolves through the scoped Project. |
| 1066839078 | Project Management — Delete status report | Not confirmed; report resolves through the scoped Project. |
| 1066839079 | Project Management — Reporting options | Not confirmed; project route scope applies. |
| 1066839080 | Project Management — Reporting configuration read | Not confirmed; project route scope applies. |
| 1066839081 | Project Management — Save reporting configuration | Not confirmed; project route scope applies. |
| 1066839082 | Project Management — Performance report | Not confirmed; project route scope applies. |
| 1066839083 | Project Management — Project Update | Not confirmed; project route tenant and management-scope checks apply. |
| 1066839084 | Project Management — Project Delete | Not confirmed; project route tenant and management-scope checks apply. |
| 1066839085 | Project Management — Project Archive | Not confirmed; project route tenant and management-scope checks apply. |
| 1066839086 | Project Management — Project Reactivate | Not confirmed; project route tenant and management-scope checks apply. |
| 1066839087 | Project Management — GetTeam | Not confirmed; project route scope applies. |
| 1066839088 | Project Management — GetManagerHistory | Not confirmed; project route scope applies. |
| 1066839089 | Project Management — List snapshots | Not confirmed; project route scope applies. |
| 1066839090 | Project Management — Get snapshot | Not confirmed; snapshot is checked through its scoped Project. |
| 1066839091 | Project Management — Add team member | Not confirmed; project route scope applies. |
| 1066839092 | Project Management — Update team member | Not confirmed; project and member are checked within scope. |
| 1066839093 | Project Management — Remove team member | Not confirmed; project and member are checked within scope. |
| 1066839094 | Project Management — Check resource conflicts | Not confirmed; project route scope applies. |
| 1066839095 | Project Management — ProjectPeople Get | Not confirmed; project route scope applies. |
| 1066839096 | Project Management — ProjectPeople Add | Not confirmed; project route scope and relationship permission apply. |
| 1066839097 | Project Management — ProjectPeople Update | Not confirmed; project and relationship are checked within scope. |
| 1066839098 | Project Management — ProjectPeople RevokeOrRemove | Not confirmed; project and relationship are checked within scope. |
| 1066839099 | Project Management — ClearSponsor | Not confirmed; project route scope applies. |
| 1066839100 | Planning — PlanActivity GetByPlan | Not confirmed; route Project and tenant-filtered Plan/version ownership apply. |
| 1066839101 | Planning — PlanActivity Create | Not confirmed; route Project, Plan ownership, and `Planning.Edit` apply. |
| 1066839102 | Planning — PlanActivity Update | Not confirmed; activity resolves through its owned Plan version and `Planning.Edit`. |
| 1066839103 | Planning — PlanActivity BulkUpdate | Not confirmed as an unauthorized mutation; child-ID routing may fail closed and needs functional UAT. |
| 1066839104 | Planning — PlanActivity Move | Not confirmed; activity and target resolve under the scoped Plan and `Planning.Edit`. |
| 1066839105 | Planning — PlanActivity Delete | Not confirmed; activity resolves through its owned Plan version and `Planning.Edit`. |
| 1066839106 | Planning — PlanAssignment List | Not confirmed; Plan/version ownership and tenant filtering apply. |
| 1066839107 | Planning — PlanAssignment Capacity | Not confirmed; project route and Plan ownership checks apply. |
| 1066839108 | Planning — PlanAssignment Create | Not confirmed; scoped Project membership, Plan version, and `Planning.Edit` apply. |
| 1066839109 | Planning — PlanAssignment BatchCreate | Not confirmed; scoped Project membership, Plan version, and `Planning.Edit` apply. |
| 1066839110 | Planning — PlanAssignment Update | Not confirmed; assignment ownership and scoped Plan version are checked. |
| 1066839111 | Planning — PlanAssignment Delete | Not confirmed; assignment ownership and scoped Plan version are checked. |
| 1066839112 | Planning — PlanCalendar List | Not confirmed; tenant-filtered Plan/version ownership applies. |
| 1066839113 | Planning — PlanCalendar Get | Not confirmed; calendar and Plan version ownership apply. |
| 1066839114 | Planning — PlanCalendar Create | Not confirmed; route Project scope and Plan ownership apply. |
| 1066839115 | Planning — PlanCalendar Update | Not confirmed; calendar and Plan version ownership apply. |
| 1066839116 | Planning — PlanCalendar Delete | Not confirmed; calendar and Plan version ownership apply. |
| 1066839117 | Planning — Plan list by Project | Not confirmed; project route scope applies. |
| 1066839118 | Planning — Bootstrap | Not confirmed; project route scope applies before bootstrapping. |
| 1066839119 | Planning — Plan GetById | Not confirmed; route Project and Plan ownership are validated. |
| 1066839120 | Planning — Get active Working version | Not confirmed; Plan is resolved under the scoped Project. |
| 1066839121 | Planning — Dependency list | Not confirmed; dependency version is tied to the scoped Plan. |
| 1066839122 | Planning — Dependency Create | Not confirmed; scoped Plan version and `Planning.Edit` apply. |
| 1066839123 | Planning — Dependency Update | Not confirmed; dependency and Plan version ownership are checked. |
| 1066839124 | Planning — Dependency Delete | Not confirmed; dependency and Plan version ownership are checked. |
| 1066839125 | Planning — Replace predecessors | Not confirmed; project and Plan ownership are resolved before mutation. |
| 1066839126 | Planning — Bulk replace predecessors | Not confirmed as an unauthorized mutation; child-ID routing may fail closed and needs functional UAT. |
| 1066839127 | Planning — Create import session | Permission gap confirmed and fixed: endpoint and service now require `Planning.Edit` before parsing or persistence. |
| 1066839128 | Planning — Get import session | Not confirmed; the session lookup is constrained by route Project and tenant filters. |
| 1066839129 | Planning — Resolve Resource | IDOR claim not confirmed; adjacent `Resource.Create` commit gap fixed and rechecked before creation. |
| 1066839130 | Planning — Resolve Assignment | Not confirmed; session is constrained by route Project and tenant filters. |
| 1066839131 | Planning — Resolve Calendar | Not confirmed; session is constrained by route Project and tenant filters. |
| 1066839132 | Planning — Commit import | IDOR claim not confirmed; adjacent `Resource.Create` commit gap fixed and rechecked before creation. |
| 1066839133 | Planning — Cancel import session | Not confirmed; session is constrained by route Project and tenant filters. |
| 1066839134 | Resources — Allocation.GetConflictsAsync | Confirmed and fixed: allocation query now applies management Project scope. |
| 1066839135 | Resources — Capacity matrix | Confirmed and fixed: allocation query now applies management Project scope. |
| 1066839136 | Resources — Capacity availability | Confirmed and fixed: allocation query now applies management Project scope. |
| 1066839137 | Resources — Demand versus capacity | Confirmed and fixed: allocation query now applies management Project scope before aggregation. |
| 1066839138 | Resources — Gantt data | Confirmed and fixed: allocation query now applies management Project scope. |
| 1066839139 | Resources — Per-Resource availability | Confirmed and fixed: allocation query now applies management Project scope before calculation. |
| 1066839140 | Workflows — List definitions | Not confirmed; tenant-filtered definition query and tenant-wide `Workflow.View` apply. |
| 1066839141 | Workflows — Get definition | Not confirmed; tenant-filtered definition query applies. |
| 1066839142 | Workflows — Update definition | Not confirmed; tenant-filtered definition query and `Workflow.Manage` apply. |
| 1066839143 | Workflows — Create draft | Not confirmed; tenant-filtered definition query and `Workflow.Manage` apply. |
| 1066839144 | Workflows — Get version | Not confirmed; tenant-filtered version query applies. |
| 1066839145 | Workflows — Delete draft | Not confirmed; tenant-filtered version query and `Workflow.Manage` apply. |
| 1066839146 | Workflows — Validate version | Not confirmed; tenant-filtered version query and `Workflow.Manage` apply. |
| 1066839147 | Workflows — Publish version | Not confirmed; tenant-filtered version query and `Workflow.Publish` apply. |
| 1066839148 | Workflows — Preview version | Not confirmed; tenant-filtered version query applies. |
| 1066839149 | Workflows — Create state | Not confirmed; tenant-filtered version query and `Workflow.Manage` apply. |
| 1066839150 | Workflows — Update state | Not confirmed; state is resolved through a tenant-filtered owning version. |

