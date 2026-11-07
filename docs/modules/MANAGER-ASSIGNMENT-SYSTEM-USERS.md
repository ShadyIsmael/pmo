# P0: Separate Manager Appointments from Resource Membership

**Date:** 2026-11-05

## Root cause

The code review found that manager appointments and Resource-based Project Team membership are separate relationships. Portfolio, Program, and Project manager fields and assignment-history rows refer to System Users. Resource records are used only where a Project Team membership or Resource Allocation is required. Project manager membership synchronization already returns without creating a Project Member when the assigned User has no Resource, and Project People already has a User-backed Governance projection.

The concrete selector failure was in Program creation: its lookup required a fixed `program.manager` role, but `SeedDataProvider` provisions no such system role. The standard seeded roles include `portfolio.manager` and `project.manager`, not `program.manager`, so a User could have the required Program access and approval permissions yet still be omitted from the Program manager selector. Portfolio and Program creation services also needed to revalidate candidate permissions server-side instead of relying on the lookup response alone. The category redesign removes role-name eligibility checks: manager lookups and assignment services now require an active same-tenant System User with an active assigned role whose category is `Management`.

The identity model has no general User-to-Organization membership relationship. `OrganizationSettingsGrant` represents settings access, and Resource organizational-unit membership is Resource-specific; neither is a valid way to determine whether a System User can receive a management appointment.

## Manager candidate rules

| Entity | Actor authorization | Candidate eligibility |
|---|---|---|
| Portfolio | Existing Portfolio operation permission and management context; `Handover.AdminInitiate` is required to include an initial manager. | Active same-tenant System User with an active `Management`-category role and effective `Handover.Approve` plus `Portfolio.View`. |
| Program | Existing Program operation permission and eligible Portfolio scope; `Handover.AdminInitiate` is required to include an initial manager. | Active same-tenant System User with an active `Management`-category role and effective `Program.View` plus `Handover.Approve`. |
| Project | Existing `Project.Create` permission and management context. | Active same-tenant System User with an active `Management`-category role and effective `Handover.Approve`. |

Candidate lookup authorization and target-user eligibility are enforced separately. The create and handover services recheck the candidate, including the active Management category when the incoming manager accepts; UI lookup results are not authority. Effective permissions include Active Deny. A candidate's category role is evaluated independently of Resource profiles and prior manager appointments.

The actor's operation and management scope constrain the target Portfolio, Program, or Project. Candidate lists are tenant-scoped because the current model has no general User-to-Organization membership source. A narrower candidate Organization-affiliation rule would require an approved identity model and policy. The implementation does not infer it from Resources or settings grants.

## Role-category conflict inventory and safe rollout

The source-defined role inventory in `SeedDataProvider` currently uses these categories: Admin=`System`; Executive=`Leadership`; Portfolio Manager, Project Manager, and Resource Manager=`Management`; Team Member=`Operations`; PMO Admin=`PMO`; Finance Controller=`Finance`. The manager System roles already use the required `Management` category. `Leadership`, `Operations`, `PMO`, and `Finance` are outside the controlled categories (`Tenant`, `Organization`, `Management`, `Operation`, `Business`) and reserved `System` category. Their destination categories require an explicit product mapping; do not infer mappings from display names or grant sets.

No live Development role, assignment, permission-grant, or current-manager inventory was run for this change. Before any category data correction, perform a read-only inventory of active role IDs, names, system keys, categories, assigned users, grants, and current manager fields/history; compare the old and new candidate sets. Approve a precise category-only mapping for each conflicting role, preserving every grant and user-role link. The `Role.Category` column already exists, so this does not require a schema migration; applying category data is still a database write and requires the repository's verified-backup and Development-target gate. Do not add grants, remove grants, reassign users, or write directly to SQL as part of this rollout.

## Assignment and Resource behavior

When a manager is selected during creation, the entity's direct manager field and current manager-assignment history are persisted through the existing service transaction. The selected identity does not need a Resource profile, Project Team membership, or an existing management assignment.

Project Team selection and Resource Allocation remain Resource-based. If the Project Manager has a linked same-tenant Resource, the existing synchronizer maintains the Project Manager team relationship. Without one, it writes no Project Member; Project details and Governance continue to resolve the manager from the User-backed assignment.

Project Manager changes after creation remain governed by Handover. An existing unassigned Project retains its Initial Assignment workflow.

## Changed implementation

- `apps/api/src/NEXUS.Modules.Identity/Api/UsersController.cs` and `Infrastructure/UserRepository.cs` — purpose-specific Project/Portfolio/Program candidate lookups from active Management-category role assignments.
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs` and `ProjectHandoverRequestService.cs`, and `apps/api/src/NEXUS.Modules.Portfolio/Application/PortfolioService.cs`, `ProgramService.cs`, and `ManagerHandoverService.cs` — server-side category and effective-permission checks for create, handover, and incoming acceptance while preserving governance and assignment-history writes.
- `apps/web-react/src/features/projects/components/CreateProjectSheet.tsx` and `ProjectAdvancedSearch.tsx` — Project manager candidate selectors use the purpose-specific lookup route instead of a role-name filter.
- `apps/web-react/src/features/delivery/PortfolioSheet.tsx`, `ProgramSheet.tsx`, and `apps/web-react/src/features/projects/components/CreateProjectSheet.tsx` — specific localized empty and authorization messages; Project creation explains that no Resource or team membership is needed.
- `apps/web-react/src/i18n/messages/en.json` and `ar.json` — matching English and Arabic copy.
- `docs/security/WORKFLOW-LOOKUP-AUTHORIZATION.md` and `docs/modules/PORTFOLIO-MANAGEMENT-CORE.md` — candidate rules and scope boundaries.

## Verification

Verification performed:

- **React manager creation tests:** passed, 23/23 (`npm test -- src/test/portfolio-sheet.test.tsx src/test/program-sheet.test.tsx src/test/projects-list.test.tsx --reporter=dot`).
- **Project create assignment service test:** passed, 1/1 in the in-memory provider. The selected active eligible User had no Resource; the test verified the direct manager field, one current assignment row, and no Project Member or Resource row.
- **React TypeScript typecheck:** passed (`npm --prefix apps/web-react run typecheck`).
- **React production build:** passed (`npm --prefix apps/web-react run build`). Vite reported existing large-chunk warnings.
- **API build:** passed with zero warnings and zero errors (`dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore --verbosity quiet`).
- **API lookup/repository tests:** authored but not run. The API test project is blocked by unrelated compile errors in `WorkflowExecutionPhase2Tests.cs` and `WorkflowDefinitionValidatorTests.cs` (missing `Mock` imports) and `Phase2AuthorizationNegativeTests.cs` (invalid `.Items` access on `Result<PagedResult<ApprovalRequest>>`).

No database reset, seed, migration, direct SQL write, or browser UAT was performed.

