# Project Manager Assignment and Handover Rules

## Business rules

- A Project may be created with an eligible initial Project Manager or with no manager. Leaving the field blank preserves the unassigned creation path.
- The Create Project selector lists only active same-tenant System Users with an active `Management`-category role and effective `Handover.Approve` permission. The create service revalidates the submitted user; UI lookup results are not authority.
- When a manager is selected during Project creation, the Project field and one current `ProjectManagerAssignment` are persisted together. The existing membership synchronizer creates or aligns a Project Manager team relationship when that User has a linked same-tenant Resource. It does not create a User or Resource to fill a missing profile.
- When no manager is selected during creation, `Project.ProjectManagerId` stays null and the create path does not add a current manager-assignment or Project Manager membership row. The Project remains eligible for the existing Initial Assignment workflow.
- Project Edit displays the current manager as read-only information. It does not provide a manager selector. Its contextual assignment action uses the existing Handover capability result: assigned Projects use Handover; unassigned Projects use Initial Assignment. The action is shown only when the actor can initiate the matching workflow.
- `PUT /api/projects/{id}` cannot assign, replace, or clear a Project Manager, and it rejects the legacy embedded `Handover` field. A crafted manager-change request returns HTTP 409 without applying other fields from that request. Handover and Initial Assignment remain available only through their existing request and acceptance routes.
- Post-creation Initial Assignment and Handover keep the current Project Manager unchanged while a request is pending. An accepted immediate request, or a scheduled request once its effective date is processed, updates the Project field, assignment history, request state, audit, and membership through the existing transactional workflow.
- Governance, Project Details, Project Team/People, My Projects, and Project access must resolve from the same authoritative accepted manager assignment. Where no Resource profile is linked, Project People uses its existing User-backed governance projection and no fabricated membership is written.

## Existing permission and scope matrix

No permission key or role grant is introduced by these rules.

| Operation | Existing permission | Required scope and validation |
|---|---|---|
| Create Project | `Project.Create` | Authenticated tenant and management context; hierarchy and resource identifiers remain in scope. A selected manager must be active, belong to the same tenant, hold an active role in the `Management` category, and have effective `Handover.Approve`. |
| Load eligible creation candidates | `WorkflowLookup.Project.Users` policy (`Project.View`, `Project.Create`, or `Project.Edit`) | Authenticated tenant. `GET /api/users/workflow-lookups/projects/managers` returns active same-tenant candidates with an active Management-category role and effective `Handover.Approve`; candidate eligibility is rechecked during create and incoming acceptance. |
| Edit other Project fields | `Project.Edit` | Existing tenant and management scope. `ProjectManagerId` is immutable through normal update, including null/foreign/ineligible manager replacement attempts. |
| Initiate a current-manager transfer | `Handover.Manage` | Actor is the current manager, the Project is in scope, and the incoming manager is an active same-tenant eligible approver. |
| Administrative handover or post-creation Initial Assignment | `Handover.AdminInitiate` | Project is in scope and its assigned/unassigned state matches the requested workflow; incoming manager is an active same-tenant eligible approver. |
| Accept or reject an incoming assignment | `Handover.Approve` | Actor is the addressed active same-tenant incoming manager; request and Project are available and in the authenticated tenant. |
| Read Project and team projections | Existing `Project.View` and Project People policies | Tenant and resource-level scope continue to apply. Membership does not grant unrelated permissions. |

Effective permissions, including Active Deny, remain authoritative. The candidate selector is informational and does not replace backend checks. Role category is checked from an active, same-tenant user-role assignment and active role row; no hard-coded role name grants manager eligibility, Project access, or Handover authority. Manager assignment and acceptance do not require a Resource profile. Project Team membership and Resource Allocation remain Resource-based.

## Regression coverage and runtime status

Focused service coverage is in `apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/ProjectServiceTests.cs`; HTTP 409 mapping is covered by `apps/api/tests/NEXUS.Api.Tests/Unit/ProjectManagerUpdateControllerTests.cs`; the manager candidate filter is covered by `apps/api/tests/NEXUS.Api.Tests/Unit/ProjectManagerWorkflowLookupTests.cs`. The existing handover lifecycle persistence suite covers Initial Assignment and handover acceptance, manager history, membership synchronization, and the no-Resource governance projection.

React coverage is recorded in `apps/web-react/src/test/projects-list.test.tsx` and `apps/web-react/src/test/project-detail.test.tsx`. These tests are automated source checks only. Authenticated Development API verification and browser UAT for the complete five-view consistency path remain unrun; see the corresponding cases in the [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md).

## Cross-entity manager selection

See [Manager Appointment Independence](MANAGER-ASSIGNMENT-SYSTEM-USERS.md) for the root cause, no-Resource behavior, and candidate-scope rules.
