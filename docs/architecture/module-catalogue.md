# Module Catalogue

NEXUS PMO consists of 19 backend modules, each organized in Clean Architecture layers (Domain, Application, API, Infrastructure). Below is the complete catalogue with verified evidence from the codebase.

---

## 1. Identity

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.Identity` |
| **Business responsibility** | Authentication, authorization, user and role management, JWT issuance, context-based role switching |
| **Maturity** | Implemented |
| **Evidence** | `AppsDbContext.cs:29-33` (5 DbSets), `SeedDataProvider.cs:37-156` (8 roles, 15 permissions, 8 users seeded), `AuthController.cs`, `DemoAuthService.cs:1-1` (in `Infrastructure/`), `Program.cs:99` (`AddIdentityModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| User | `Users` | `AppDbContext.cs:29` |
| Role | `Roles` | `AppDbContext.cs:30` |
| Permission | `Permissions` | `AppDbContext.cs:31` |
| UserRole | `UserRoles` | `AppDbContext.cs:32` |
| UserSession | `UserSessions` | `AppDbContext.cs:33` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IAuthenticationService` | `Application/IAuthenticationService.cs` | Yes |
| `IUserRepository` | `Application/IUserRepository.cs` | Yes |
| `IAuthorizationService` | `Application/IAuthorizationService.cs` | Yes |
| `DemoAuthService` | `Infrastructure/DemoAuthService.cs` | Yes |
| `UserRepository` | `Infrastructure/UserRepository.cs` | Yes |

### API Endpoints
| Endpoint | File | Verified |
|----------|------|----------|
| `POST /api/auth/login` | `Api/AuthController.cs` | Yes |
| `GET /api/auth/me` | `Api/AuthController.cs` | Yes |
| `POST /api/auth/logout` | `Api/AuthController.cs` | Yes |
| `POST /api/auth/switch-role` | `Api/AuthController.cs` | Compatibility route; returns current-user state and does not switch roles |

### Events
- `UserLoggedIn`, `UserRoleChanged` (documented in module-catalogue.md, not verified as code implementations)
- **No MediatR `INotificationHandler` implementations found**

### Database Ownership
- `Users`, `Roles`, `Permissions`, `UserRoles`, `UserSessions` tables

### Dependencies
- `NEXUS.SharedKernel`

---

## 2. Organization

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.Organization` |
| **Business responsibility** | Company structure: business units, departments, teams, locations, working calendars, cost centres |
| **Maturity** | Implemented |
| **Evidence** | `AppDbContext.cs:35-44` (10 DbSets), `SeedDataProvider.cs:188-199` (1 organization seeded), `OrganizationController.cs`, `BusinessUnitController.cs`, `Program.cs:100` (`AddOrganizationModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| Organization | `Organizations` | `AppDbContext.cs:35` |
| BusinessUnit | `BusinessUnits` | `AppDbContext.cs:36` |
| Department | `Departments` | `AppDbContext.cs:37` |
| Team | `Teams` | `AppDbContext.cs:38` |
| Location | `Locations` | `AppDbContext.cs:39` |
| WorkingCalendar | `WorkingCalendars` | `AppDbContext.cs:40` |
| Holiday | `Holidays` | `AppDbContext.cs:41` |
| Lookup | `Lookups` | `AppDbContext.cs:42` |
| Currency | `Currencies` | `AppDbContext.cs:43` |
| CostCentre | `CostCentres` | `AppDbContext.cs:44` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IOrganizationService` | `Application/IOrganizationService.cs` | Yes |
| `OrganizationService` | `Application/OrganizationService.cs` | Yes |
| `IBusinessUnitService` | `Application/IBusinessUnitService.cs` | Yes |
| `BusinessUnitService` | `Application/BusinessUnitService.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET /api/organization` | `OrganizationController.cs` | Yes |
| `GET /api/business-units` | `BusinessUnitController.cs` | Yes |

### Dependencies
- `NEXUS.SharedKernel`

---

## 3. Portfolio

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.Portfolio` |
| **Business responsibility** | Portfolio and program management with strategic alignment, health tracking, budget oversight |
| **Maturity** | Implemented |
| **Evidence** | `AppDbContext.cs:46-51` (6 DbSets), `SeedDataProvider.cs:202-221` (4 portfolios, 7 programs seeded), `PortfolioController.cs`, `ProgramController.cs`, `Program.cs:101` (`AddPortfolioModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| Portfolio | `Portfolios` | `AppDbContext.cs:46` |
| Program | `Programs` | `AppDbContext.cs:47` |
| StrategicObjective | `StrategicObjectives` | `AppDbContext.cs:48` |
| PortfolioHealth | `PortfolioHealths` | `AppDbContext.cs:49` |
| PortfolioBudget | `PortfolioBudgets` | `AppDbContext.cs:50` |
| PortfolioRisk | `PortfolioRisks` | `AppDbContext.cs:51` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IPortfolioService` | `Application/IPortfolioService.cs` | Yes |
| `PortfolioService` | `Application/PortfolioService.cs` | Yes |
| `IProgramService` | `Application/IProgramService.cs` | Yes |
| `ProgramService` | `Application/ProgramService.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST /api/portfolios` | `PortfolioController.cs` | Yes |
| `GET/PUT/DELETE /api/portfolios/{id}` | `PortfolioController.cs` | Yes |
| `GET/POST /api/programs` | `ProgramController.cs` | Yes |
| `GET /api/programs/{id}` | `ProgramController.cs` | Yes |

### Dependencies
- Identity (user ownership)
- Organization (org reference)

---

## 4. Project Management

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.ProjectManagement` |
| **Business responsibility** | Full project lifecycle: milestones, baselines, deliverables, phases, WBS, EVM, status reporting |
| **Maturity** | Implemented |
| **Evidence** | `AppDbContext.cs:53-62` (10 DbSets), `SeedDataProvider.cs:223-328` (24 projects seeded with EVM data), `ProjectController.cs`, `MilestoneController.cs`, `BaselineController.cs`, `Program.cs:102` (`AddProjectManagementModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| Project | `Projects` | `AppDbContext.cs:53` |
| ProjectMember | `ProjectMembers` | `AppDbContext.cs:54` |
| ProjectPhase | `ProjectPhases` | `AppDbContext.cs:55` |
| ProjectStakeholder | `ProjectStakeholders` | `AppDbContext.cs:56` |
| WorkBreakdownStructure | `WorkBreakdownStructures` | `AppDbContext.cs:57` |
| ProjectSchedule | `ProjectSchedules` | `AppDbContext.cs:58` |
| Milestone | `Milestones` | `AppDbContext.cs:59` |
| Baseline | `Baselines` | `AppDbContext.cs:60` |
| Deliverable | `Deliverables` | `AppDbContext.cs:61` |
| ProjectStatusReport | `ProjectStatusReports` | `AppDbContext.cs:62` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IProjectService` | `Application/IProjectService.cs` | Yes |
| `ProjectService` | `Application/ProjectService.cs` | Yes |
| `IMilestoneService` | `Application/IMilestoneService.cs` | Yes |
| `MilestoneService` | `Application/MilestoneService.cs` | Yes |
| `IBaselineService` | `Application/IBaselineService.cs` | Yes |
| `BaselineService` | `Application/BaselineService.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST /api/projects` | `ProjectController.cs` | Yes |
| `GET/PUT/DELETE /api/projects/{id}` | `ProjectController.cs` | Yes |
| `GET /api/projects/{id}/milestones` | `MilestoneController.cs` | Yes |
| `POST /api/projects/{id}/milestones` | `MilestoneController.cs` | Yes |
| `GET /api/projects/{id}/baselines` | `BaselineController.cs` | Yes |

### Dependencies
- Identity (project manager)
- Portfolio (portfolio/program membership)

---

## 5. Work Items

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.WorkItems` |
| **Business responsibility** | Agile work item management: tasks, bugs, stories, epics; supports internal/external source tracking |
| **Maturity** | Implemented |
| **Evidence** | `AppDbContext.cs:64-65` (2 DbSets), `SeedDataProvider.cs:400-422` (320 work items seeded with Type/Priority/Status/Source enums), `WorkItemController.cs`, `Program.cs:103` (`AddWorkItemsModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| WorkItem | `WorkItems` | `AppDbContext.cs:64` |
| WorkItemAssignment | `WorkItemAssignments` | `AppDbContext.cs:65` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IWorkItemService` | `Application/IWorkItemService.cs` | Yes |
| `WorkItemService` | `Application/WorkItemService.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST/DELETE /api/work-items` | `WorkItemController.cs` | Yes |

### Dependencies
- Project Management (project reference)

---

## 6. Resource Management

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.ResourceManagement` |
| **Business responsibility** | Resource planning, allocations, capacity, skills, rate cards, contracts, vendors |
| **Maturity** | Implemented |
| **Evidence** | `AppDbContext.cs:67-75` (9 DbSets), `SeedDataProvider.cs:377-398` (120 resources seeded), `ResourceController.cs`, `AllocationController.cs`, `Program.cs:104` (`AddResourceManagementModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| Resource | `Resources` | `AppDbContext.cs:67` |
| ResourceSkill | `ResourceSkills` | `AppDbContext.cs:68` |
| Skill | `Skills` | `AppDbContext.cs:69` |
| ResourceAllocation | `ResourceAllocations` | `AppDbContext.cs:70` |
| ResourceRequest | `ResourceRequests` | `AppDbContext.cs:71` |
| RateCard | `RateCards` | `AppDbContext.cs:72` |
| Contract | `Contracts` | `AppDbContext.cs:73` |
| ResourceCalendar | `ResourceCalendars` | `AppDbContext.cs:74` |
| Vendor | `Vendors` | `AppDbContext.cs:75` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IResourceService` | `Application/IResourceService.cs` | Yes |
| `ResourceService` | `Application/ResourceService.cs` | Yes |
| `IAllocationService` | `Application/IAllocationService.cs` | Yes |
| `AllocationService` | `Application/AllocationService.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST /api/resources` | `ResourceController.cs` | Yes |
| `GET/PUT /api/resources/{id}` | `ResourceController.cs` | Yes |
| `POST /api/allocations` | `AllocationController.cs` | Yes |

### Dependencies
- Identity (user link)
- Organization (BU, department)

---

## 7. Cost Management

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.CostManagement` |
| **Business responsibility** | Financial management: budgets, cost entries, invoices, purchase commitments, cost categories, cost approvals |
| **Maturity** | Implemented |
| **Evidence** | `AppDbContext.cs:77-82` (6 DbSets), `SeedDataProvider.cs:545-587` (24 budgets, 50 cost entries seeded), `BudgetController.cs`, `CostController.cs`, `Program.cs:105` (`AddCostManagementModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| Budget | `Budgets` | `AppDbContext.cs:77` |
| CostEntry | `CostEntries` | `AppDbContext.cs:78` |
| CostCategory | `CostCategories` | `AppDbContext.cs:79` |
| CostApproval | `CostApprovals` | `AppDbContext.cs:80` |
| Invoice | `Invoices` | `AppDbContext.cs:81` |
| PurchaseCommitment | `PurchaseCommitments` | `AppDbContext.cs:82` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IBudgetService` | `Application/IBudgetService.cs` | Yes |
| `BudgetService` | `Application/BudgetService.cs` | Yes |
| `ICostService` | `Application/ICostService.cs` | Yes |
| `CostService` | `Application/CostService.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST /api/budgets` | `BudgetController.cs` | Yes |
| `GET /api/costs` | `CostController.cs` | Yes |

### Dependencies
- Project Management (project reference)
- Identity (approver)

---

## 8. Risk Management

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.RiskManagement` |
| **Business responsibility** | Tenant-scoped project risk workflow, action ownership, follow-up planning, and progress history |
| **Maturity** | Implemented |
| **Evidence** | `AppDbContext.cs:84-87,114-116` (Risk and workflow DbSets), `RiskController.cs`, `Program.cs:106` (`AddRiskManagementModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| Risk | `Risks` | `AppDbContext.cs:84` |
| RiskCategory | `RiskCategories` | `AppDbContext.cs:85` |
| RiskAction | `RiskActions` | `AppDbContext.cs:114` |
| RiskFollowUp | `RiskFollowUps` | `AppDbContext.cs:115` |
| RiskFollowUpUpdate | `RiskFollowUpUpdates` | `AppDbContext.cs:116` |
| RiskMitigation / RiskContingency | legacy tables | compatibility only |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IRiskService` | `Application/IRiskService.cs` | Yes |
| `RiskService` | `Application/RiskService.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST/PUT /api/risks`, status, actions, follow-up, updates | `RiskController.cs` | Yes |

### Dependencies
- Project Management (project reference)
- Identity (risk owner)

---

## 9. Issue Management

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.IssueManagement` |
| **Business responsibility** | Issue tracking with severity/priority classification, resolution, action items |
| **Maturity** | Implemented |
| **Evidence** | `AppDbContext.cs:89-91` (3 DbSets), `SeedDataProvider.cs:464-483` (17 issues seeded), `IssueController.cs`, `Program.cs:107` (`AddIssueManagementModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| Issue | `Issues` | `AppDbContext.cs:89` |
| IssueCategory | `IssueCategories` | `AppDbContext.cs:90` |
| IssueAction | `IssueActions` | `AppDbContext.cs:91` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IIssueService` | `Application/IIssueService.cs` | Yes |
| `IssueService` | `Application/IssueService.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST/PUT/DELETE /api/issues` | `IssueController.cs` | Yes |

### Dependencies
- Project Management (project reference)
- Identity (assignee, reporter)

---

## 10. Meetings

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.Meetings` |
| **Business responsibility** | Meeting scheduling, agendas, participants, minutes, action items; virtual/in-person/hybrid |
| **Maturity** | Partial: core lifecycle, workspace, scoped action-to-Work-Item links, and shared email reminder integration are implemented; migration `20261028084103_AddMeetingActionWorkItemLink` is not applied, SMTP configuration and owner verification remain pending, and external-recipient authorization is not granted |
| **Evidence** | `AppDbContext.cs:154-163` (10 DbSets), `SeedDataProvider.cs` (meeting seed data), `MeetingController.cs`, `MeetingReminderScheduler.cs`, `Program.cs` (`AddMeetingsModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| Meeting | `Meetings` | `AppDbContext.cs:154` |
| MeetingAgenda | `MeetingAgendas` | `AppDbContext.cs:155` |
| MeetingParticipant | `MeetingParticipants` | `AppDbContext.cs:156` |
| MeetingMinutes | `MeetingMinutes` | `AppDbContext.cs:157` |
| MeetingMinutesComment | `MeetingMinutesComments` | `AppDbContext.cs:158` |
| MeetingMinutesChecklistItem | `MeetingMinutesChecklistItems` | `AppDbContext.cs:159` |
| MeetingMinutesAttachment | `MeetingMinutesAttachments` | `AppDbContext.cs:160` |
| MeetingReminderDispatch | `MeetingReminderDispatches` | `AppDbContext.cs:161` |
| MeetingMinutesWorkItemLink | `MeetingMinutesWorkItemLinks` | `AppDbContext.cs:162` |
| MeetingAction | `MeetingActions` | `AppDbContext.cs:163` |

`MeetingAction.WorkItemId` is an optional same-tenant, same-project reference to an existing Task or Issue. Work Item creation and workflow remain owned by the Work Items module. The nullable column, index, and `SET NULL` foreign key are represented by migration `20261028084103_AddMeetingActionWorkItemLink`; the snapshot is surgically updated, and the migration has not been applied or validated against a database.

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IMeetingService` | `Application/IMeetingService.cs` | Implemented; current changes untested |
| `MeetingService` | `Application/MeetingService.cs` | Implemented; current changes untested |
| `IMeetingWorkspaceService` | `Application/IMeetingWorkspaceService.cs` | Implemented; untested |
| `MeetingWorkspaceService` | `Application/MeetingWorkspaceService.cs` | Implemented; untested |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST/PUT/DELETE /api/meetings` | `MeetingController.cs` | Implemented; current changes untested |
| `GET/POST/PUT/DELETE /api/meetings/{id}/agenda` | `MeetingController.cs` | Implemented; untested |
| `GET /api/meetings/{id}/participants` and attendance updates | `MeetingController.cs` | Implemented; untested |
| `GET/POST/PUT/DELETE /api/meetings/{id}/actions` | `MeetingController.cs` | Implemented; untested |
| `POST/DELETE /api/meetings/{id}/actions/{itemId}/work-item` | `MeetingController.cs` | Implemented; untested (link requires `Meeting.Manage` and `WorkItem.View`; unlink requires `Meeting.Manage`) |
| Minutes, comments, checklist, attachments, and Work Item link endpoints | `MeetingController.cs` | Yes |

### Dependencies
- Project Management (project reference)
- Identity (organizer, participants)
- Work Items (scoped reference lookup and creation through the existing Work Items API)

Meeting participants support either an active internal user (`UserId`) or an external email (`Email`); attendee resolution, email normalization, duplicate checks, and soft-delete synchronization are enforced by `MeetingService`. Internal meeting reminders use recipient email preferences through the shared notification channel. External attendee reminder email is not enabled pending recipient-policy authorization. See [`docs/modules/MEETINGS.md`](../modules/MEETINGS.md) for delivery and verification limits.

---

## 11. Workflow Engine

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.WorkflowEngine` |
| **Business responsibility** | Configurable workflow state machines, transitions, conditions, SLA, escalation |
| **Maturity** | Partial |
| **Evidence** | `AppDbContext.cs:99-106` (8 DbSets), `WorkflowController.cs`, `Program.cs:109` (`AddWorkflowEngineModule()`). **Workflow execution logic not verified** -- no state machine engine, condition evaluator, SLA timer found |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| WorkflowTemplate | `WorkflowTemplates` | `AppDbContext.cs:99` |
| WorkflowVersion | `WorkflowVersions` | `AppDbContext.cs:100` |
| WorkflowInstance | `WorkflowInstances` | `AppDbContext.cs:101` |
| WorkflowState | `WorkflowStates` | `AppDbContext.cs:102` |
| WorkflowTransition | `WorkflowTransitions` | `AppDbContext.cs:103` |
| WorkflowCondition | `WorkflowConditions` | `AppDbContext.cs:104` |
| SLA | `SLAs` | `AppDbContext.cs:105` |
| Escalation | `Escalations` | `AppDbContext.cs:106` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IWorkflowService` | `Application/IWorkflowService.cs` | Yes |
| `WorkflowService` | `Application/WorkflowService.cs` | Yes |
| `IWorkflowDesignerService` | `Application/IWorkflowDesignerService.cs` | Yes |
| `WorkflowDesignerService` | `Application/WorkflowDesignerService.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST/PUT /api/workflows` | `WorkflowController.cs` | Yes |

### Events
- Workflow state transition events (documented, not verified as code)

### Dependencies
- Identity (actor reference)

---

## 12. Approvals

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.Approvals` |
| **Business responsibility** | Multi-level approval requests, delegation; entities: budgets, contracts, scope changes |
| **Maturity** | Implemented |
| **Evidence** | `AppDbContext.cs:108-110` (3 DbSets), `SeedDataProvider.cs:505-523` (18 approval requests seeded), `ApprovalController.cs`, `Program.cs:110` (`AddApprovalsModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| ApprovalRequest | `ApprovalRequests` | `AppDbContext.cs:108` |
| ApprovalAction | `ApprovalActions` | `AppDbContext.cs:109` |
| Delegation | `Delegations` | `AppDbContext.cs:110` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IApprovalService` | `Application/IApprovalService.cs` | Yes |
| `ApprovalService` | `Application/ApprovalService.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST /api/approvals` | `ApprovalController.cs` | Yes |
| `POST /api/approvals/{id}/approve` | `ApprovalController.cs` | Yes |
| `POST /api/approvals/{id}/reject` | `ApprovalController.cs` | Yes |

### Dependencies
- Identity (requester, approver)
- Workflow Engine (approval workflows)

---

## 13. Notifications

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.Notifications` |
| **Business responsibility** | Canonical in-app notifications, user channel preferences, durable asynchronous Push and Email delivery, templates, and digests |
| **Maturity** | In-App, Push, and shared SMTP Email delivery implemented; Firebase/SMTP runtime delivery requires deployment configuration, and current Meetings changes remain unverified |
| **Evidence** | `AppDbContext.cs` notification DbSets, `NotificationController.cs`, `NotificationDeliveryProcessor.cs`, `NotificationDeliveryWorker.cs`, `FirebaseNotificationChannel.cs`, `SmtpEmailNotificationChannel.cs`, `NotificationsModuleRegistration.cs` |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| Notification | `Notifications` | `AppDbContext.cs:112` |
| NotificationDelivery | `NotificationDeliveries` | `AppDbContext.cs` |
| NotificationDeliveryAttempt | `NotificationDeliveryAttempts` | `AppDbContext.cs` |
| NotificationTemplate | `NotificationTemplates` | `AppDbContext.cs:113` |
| NotificationPreference | `NotificationPreferences` | `AppDbContext.cs:114` |
| UserDeviceToken | `UserDeviceTokens` | `AppDbContext.cs` |
| NotificationDigest | `NotificationDigests` | `AppDbContext.cs:115` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `INotificationService` | `Application/INotificationService.cs` | Implemented; email changes untested |
| `NotificationService` | `Application/NotificationService.cs` | Implemented; email changes untested |
| `IPushNotificationSender` | `Application/PushNotificationSender.cs` | Yes (Firebase provider boundary) |
| `IEmailNotificationSender` | `Application/EmailNotificationSender.cs` | Implemented; untested (shared SMTP provider boundary) |
| `NotificationDeliveryProcessor` | `Infrastructure/NotificationDeliveryProcessor.cs` | Push path present; email changes untested |
| `NotificationDeliveryWorker` | `Infrastructure/NotificationDeliveryWorker.cs` | Yes (bounded background polling) |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET /api/notifications` | `NotificationController.cs` | Yes (paged, current-user only) |
| `GET /api/notifications/unread-count` | `NotificationController.cs` | Yes |
| `POST /api/notifications/{id}/mark-read`, `PUT /api/notifications/read-all` | `NotificationController.cs` | Yes |
| `GET/PUT /api/notifications/preferences` | `NotificationController.cs` | Yes (authenticated user only) |
| `POST/DELETE /api/notifications/devices` | `NotificationController.cs` | Yes (authenticated tenant/user ownership) |

Supported event types include Project/Program/Portfolio handover, Project Change Request, payment due/overdue, and Meeting Reminder. Preferences default to In-App on, Push off, and Email off. Email is delivered only when the shared SMTP provider is configured and the recipient has enabled Email for that event. See [`docs/integrations/email-notifications.md`](../integrations/email-notifications.md).

### Dependencies
- Identity (user reference)

---

## 14. Documents

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.Documents` |
| **Business responsibility** | Shared document metadata/versioning, organization-owned approved templates, categories, reviews, and file storage |
| **Maturity** | Source includes versioned organization-owned templates and a private local Development provider; migration `20261110170000_AddOrganizationDocumentTemplateStorageMetadata` is not applied and authenticated database/browser verification remains pending |
| **Evidence** | `AppDbContext.cs:117-120` (4 DbSets), `DocumentController.cs`, `IStorageProvider.cs`, `LocalStorageProvider.cs`, `DocumentRepository.cs`, `Program.cs:112` (`AddDocumentsModule()`), additive metadata migration |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| Document | `Documents` | `AppDbContext.cs:117` |
| DocumentVersion | `DocumentVersions` | `AppDbContext.cs:118` |
| DocumentCategory | `DocumentCategories` | `AppDbContext.cs:119` |
| DocumentReview | `DocumentReviews` | `AppDbContext.cs:120` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IDocumentService` | `Application/IDocumentService.cs` | Yes |
| `DocumentService` | `Application/DocumentService.cs` | Yes |
| `IStorageProvider` | `Application/IStorageProvider.cs` | Stream storage contract, keyed registration by provider ID |
| `LocalStorageProvider` | `Infrastructure/LocalStorageProvider.cs` | Private local implementation for Development; external File Server deferred |
| `DocumentRepository` | `Infrastructure/DocumentRepository.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Source contract |
|----------|----------------|-----------------|
| `GET /api/documents`, `/categories`, `/organizations`, `/{id}` | `DocumentController.cs` | Scoped document/template lists, category and permitted organization selectors, and details |
| `POST /api/documents`, `/{id}/versions`, `/{id}/archive`, `/{id}/activate` | `DocumentController.cs` | Permission-gated uploads, immutable version creation, non-destructive archive, and template activation |
| `GET /api/documents/{id}/download`, `/{id}/preview` | `DocumentController.cs` | API-mediated authorized file access |

### Dependencies
- Project Management (project reference)
- Identity (owner)

---

## 15. Reporting

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.Reporting` |
| **Business responsibility** | Report definitions, scheduling, export (PDF/Excel/CSV) |
| **Maturity** | Implemented (entities verified) |
| **Evidence** | `AppDbContext.cs:122-124` (3 DbSets), `ReportController.cs`, `Program.cs:113` (`AddReportingModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| ReportDefinition | `ReportDefinitions` | `AppDbContext.cs:122` |
| ReportSchedule | `ReportSchedules` | `AppDbContext.cs:123` |
| ReportExport | `ReportExports` | `AppDbContext.cs:124` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IReportService` | `Application/IReportService.cs` | Yes |
| `ReportService` | `Application/ReportService.cs` | Yes |
| `IReportDataProvider` | `Application/IReportDataProvider.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST /api/reports` | `ReportController.cs` | Yes |

### Dependencies
- All modules (data sources)

---

## 16. Audit

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.Audit` |
| **Business responsibility** | Immutable audit trail, entity change tracking, project-scoped audit workspace, activity log |
| **Maturity** | Implemented |
| **Evidence** | `AppDbContext.cs:196-197` (2 DbSets), `AuditController.cs`, `ProjectAuditController.cs`, `Program.cs` (`AddAuditModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| AuditEntry | `AuditEntries` | `AppDbContext.cs:196` |
| AuditActivity | `AuditActivities` | `AppDbContext.cs:197` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IAuditService` | `Application/IAuditService.cs` | Yes |
| `AuditService` | `Application/AuditService.cs` | Yes |
| `IProjectAuditScopeProvider` | `Application/IProjectAuditScopeProvider.cs` (implementation `NEXUS.Persistence/Audit/ProjectAuditScopeProvider.cs`) | Yes |
| `AuditFieldRedactor` | `Application/AuditFieldRedactor.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET /api/audit` | `AuditController.cs` | Yes |
| `GET /api/projects/{projectId}/audit` | `ProjectAuditController.cs` | Yes |
| `GET /api/projects/{projectId}/audit/facets` | `ProjectAuditController.cs` | Yes |

### Dependencies
- SharedKernel (BaseEntity, IAuditable interface)
- Persistence (scope provider, append-only guard in `SaveChangesAsync`)
- Documentation: `docs/modules/AUDIT-TRAIL.md`

---

## 17. Integrations

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.Integrations` |
| **Business responsibility** | External system connection management, sync jobs, webhooks, conflict resolution |
| **Maturity** | Partial |
| **Evidence** | `AppDbContext.cs:129-132` (4 DbSets), `IntegrationController.cs`, `ISyncProvider.cs`, `IntegrationService.cs`, `Program.cs:115` (`AddIntegrationsModule()`). **No real sync adapters implemented** |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| IntegrationConnection | `IntegrationConnections` | `AppDbContext.cs:129` |
| IntegrationSync | `IntegrationSyncs` | `AppDbContext.cs:130` |
| IntegrationWebhook | `IntegrationWebhooks` | `AppDbContext.cs:131` |
| SyncConflict | `SyncConflicts` | `AppDbContext.cs:132` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IIntegrationService` | `Application/IIntegrationService.cs` | Yes |
| `IntegrationService` | `Application/IntegrationService.cs` | Yes |
| `ISyncProvider` | `Application/ISyncProvider.cs` | Yes (interface only; no implementation) |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST/PUT/DELETE /api/integrations` | `IntegrationController.cs` | Yes |
| `POST /api/integrations/{id}/sync` | `IntegrationController.cs` | Yes |

### Dependencies
- Identity (connection owner)

---

## 18. Plugin Management

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.PluginManagement` |
| **Business responsibility** | Plugin registry, lifecycle management, tenant activation, dependency resolution |
| **Maturity** | Implemented (entities/controllers exist; no runtime plugin loading verified) |
| **Evidence** | `AppDbContext.cs:134-136` (3 DbSets), `PluginController.cs`, `IPluginRegistry.cs`, `IPluginManager.cs`, `PluginRegistryService.cs`, `Program.cs:116` (`AddPluginManagementModule()`) |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| Plugin | `Plugins` | `AppDbContext.cs:134` |
| TenantPlugin | `TenantPlugins` | `AppDbContext.cs:135` |
| PluginDependency | `PluginDependencies` | `AppDbContext.cs:136` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IPluginRegistry` | `Application/IPluginRegistry.cs` | Yes |
| `IPluginManager` | `Application/IPluginManager.cs` | Yes |
| `PluginRegistryService` | `Application/PluginRegistryService.cs` | Yes |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `GET/POST/DELETE /api/plugins` | `PluginController.cs` | Yes |

### Dependencies
- Identity (admin authorization)

---

## 19. AI Planning

| Attribute | Detail |
|-----------|--------|
| **Namespace** | `NEXUS.Modules.AIPlanning` |
| **Business responsibility** | AI-powered plan generation, analysis, risk prediction (mock/provider-abstracted) |
| **Maturity** | Partial |
| **Evidence** | `AppDbContext.cs:138-139` (2 DbSets), `AIPlanningController.cs`, `IAIProvider.cs`, `MockAIProvider.cs`, `IAIPlanningService.cs`, `Program.cs:117` (`AddAIPlanningModule()`). **AI provider adapter is mock** |

### Domain Entities
| Entity | DbSet | Verified |
|--------|-------|----------|
| AIPlanRequest | `AIPlanRequests` | `AppDbContext.cs:138` |
| AIPlanResponse | `AIPlanResponses` | `AppDbContext.cs:139` |

### Application Services
| Service | File | Verified |
|---------|------|----------|
| `IAIPlanningService` | `Application/IAIPlanningService.cs` | Yes |
| `AIPlanningService` | `Application/AIPlanningService.cs` | Yes |
| `IAIProvider` | `Application/IAIProvider.cs` | Yes (interface) |
| `MockAIProvider` | `Infrastructure/MockAIProvider.cs` | Yes (mock implementation) |

### API Endpoints
| Endpoint | Controller File | Verified |
|----------|----------------|----------|
| `POST /api/ai/generate-plan` | `AIPlanningController.cs` | Yes |
| `GET /api/ai/requests` | `AIPlanningController.cs` | Yes |

### Dependencies
- Project Management (plan context)
- Resource Management (resource suggestions)

---

## Module Summary

| # | Module | Entities | Controllers | Services | Maturity |
|---|--------|----------|-------------|----------|----------|
| 1 | Identity | 5 | 1 (AuthController) | 5 | Implemented |
| 2 | Organization | 10 | 2 (Organization, BusinessUnit) | 4 | Implemented |
| 3 | Portfolio | 6 | 2 (Portfolio, Program) | 4 | Implemented |
| 4 | Project Management | 10 | 3 (Project, Milestone, Baseline) | 6 | Implemented |
| 5 | Work Items | 2 | 1 (WorkItem) | 2 | Implemented |
| 6 | Resource Management | 9 | 2 (Resource, Allocation) | 4 | Implemented |
| 7 | Cost Management | 6 | 2 (Budget, Cost) | 4 | Implemented |
| 8 | Risk Management | 7 (3 legacy) | 1 (Risk) | 2 | Implemented |
| 9 | Issue Management | 3 | 1 (Issue) | 2 | Implemented |
| 10 | Meetings | 10 | 1 (Meeting) | 2 | Partial |
| 11 | Workflow Engine | 8 | 1 (Workflow) | 4 | Partial |
| 12 | Approvals | 3 | 1 (Approval) | 2 | Implemented |
| 13 | Notifications | 4 | 1 (Notification) | 3 | Implemented |
| 14 | Documents | 4 | 1 (Document) | 4 | Implemented |
| 15 | Reporting | 3 | 1 (Report) | 3 | Implemented |
| 16 | Audit | 2 | 1 (Audit) | 2 | Implemented |
| 17 | Integrations | 4 | 1 (Integration) | 3 | Partial |
| 18 | Plugin Management | 3 | 1 (Plugin) | 3 | Implemented |
| 19 | AI Planning | 2 | 1 (AIPlanning) | 3 | Partial |
| **Total** | | **93** | **25** | **56** | |
