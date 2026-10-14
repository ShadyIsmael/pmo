# Domain Entity Reference

All entities inherit from `BaseEntity` which provides: `Id` (Guid), `TenantId`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsDeleted`, `RowVersion`.

**Legend:**
- **MT** = Multi-tenancy support (TenantId field)
- **SD** = Soft delete support (IsDeleted field)
- **AU** = Audit support (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)

---

## Identity Module

### User
| Attribute | Detail |
|-----------|--------|
| **Table** | `Users` |
| **Key Properties** | Email, PasswordHash, FirstName, LastName, JobTitle, Phone, AvatarUrl, IsActive, LastLoginAt |
| **Relationships** | HasMany UserRoles, HasMany Sessions |
| **Domain Events** | UserLoggedIn, UserRoleChanged |
| **MT/SD/AU** | Yes / Yes / Yes |

### Role
| Attribute | Detail |
|-----------|--------|
| **Table** | `Roles` |
| **Key Properties** | Name, Description, Category, IsSystem |
| **Relationships** | HasMany UserRoles, HasMany Permissions |
| **Value Objects** | None |
| **MT/SD/AU** | Yes / Yes / Yes |

### Permission
| Attribute | Detail |
|-----------|--------|
| **Table** | `Permissions` |
| **Key Properties** | Name, Description, Resource, Action |
| **Relationships** | HasMany Roles |
| **MT/SD/AU** | Yes / Yes / Yes |

### UserRole
| Attribute | Detail |
|-----------|--------|
| **Table** | `UserRoles` |
| **Key Properties** | UserId, RoleId |
| **Relationships** | BelongsTo User, BelongsTo Role |
| **MT/SD/AU** | Yes / Yes / Yes |

### UserSession
| Attribute | Detail |
|-----------|--------|
| **Table** | `UserSessions` |
| **Key Properties** | UserId, Token, IpAddress, UserAgent, ExpiresAt, IsRevoked |
| **Relationships** | BelongsTo User |
| **MT/SD/AU** | Yes / Yes / Yes |

---

## Organization Module

### Organization
| Table: `Organizations` |
| Key Properties: Name, Code, Description, Email, Website, LogoUrl, IsActive |

### BusinessUnit
| Table: `BusinessUnits` |
| Key Properties: Name, Code, Description, OrganizationId, HeadId, IsActive |

### Department
| Table: `Departments` |
| Key Properties: Name, Code, BusinessUnitId, ManagerId |

### Team
| Table: `Teams` |
| Key Properties: Name, Code, DepartmentId, LeadId |

### Location
| Table: `Locations` |
| Key Properties: Name, Address, City, Country, Timezone, Type (Office/Remote/Hybrid) |

### WorkingCalendar
| Table: `WorkingCalendars` |
| Key Properties: Name, Description, WorkingDays (JSON), DefaultStartTime, DefaultEndTime, Timezone |

### Holiday
| Table: `Holidays` |
| Key Properties: WorkingCalendarId, Name, Date, IsRecurring |

### Lookup
| Table: `Lookups` |
| Key Properties: Category, Code, DisplayName, DisplayOrder, IsActive |

### Currency
| Table: `Currencies` |
| Key Properties: Code, Name, Symbol, ExchangeRate, IsBase |

### CostCentre
| Table: `CostCentres` |
| Key Properties: Code, Name, Description, DepartmentId, IsActive |

---

## Portfolio Module

### Portfolio
| Table: `Portfolios` |
| Key Properties: Name, Code, Description, Status, OwnerId, OrganizationId, StartDate, EndDate, TotalBudget, ActualSpend, Health, Priority |
| Relationships: HasMany Programs, HasMany HealthHistory, HasMany Budgets |

### Program
| Table: `Programs` |
| Key Properties: Name, Code, Description, PortfolioId, Status, ProgramManagerId, StartDate, EndDate, Budget, ActualSpend, Health, Priority |
| Relationships: BelongsTo Portfolio |

### StrategicObjective
| Table: `StrategicObjectives` |
| Key Properties: Name, Description, PortfolioId, Category, TargetDate, Status, Progress |

### PortfolioHealth
| Table: `PortfolioHealths` |
| Key Properties: PortfolioId, Status (Green/Amber/Red), Date, Notes |

### PortfolioBudget
| Table: `PortfolioBudgets` |
| Key Properties: PortfolioId, FiscalYear, TotalBudget, Allocated, Remaining |

### PortfolioRisk
| Table: `PortfolioRisks` |
| Key Properties: PortfolioId, Title, Impact, Likelihood, RiskLevel, Status |

---

## Project Management Module

### Project
| Table: `Projects` |
| Key Properties: Name, Code, Description, ProgramId, Status, Priority, Category, ProjectManagerId, PortfolioId, StartDate, EndDate, ActualStartDate, ActualEndDate, Budget, ActualCost, PlannedValue, EarnedValue, CostVariance, ScheduleVariance, Progress, Health, Phase, RiskRating, BusinessCase |
| Relationships: HasMany Members, Phases, Stakeholders, Milestones, Deliverables, Baselines, StatusReports |

### ProjectPlanDraft
| Table: `ProjectPlanDrafts` |
| Key Properties: ProjectId, SourceBaselineVersionId, Status (Draft/Submitted/Rejected/Applied), IsActive, SubmittedChangeRequestId, CreatedBy/CreatedAt/UpdatedBy/UpdatedAt, RowVersion |
| Constraints: one active draft per tenant/project (filtered unique index `(TenantId, ProjectId) WHERE IsActive = 1`); RowVersion concurrency token |

### Milestone
| Table: `Milestones` |
| Key Properties: ProjectId, Name, Description, DueDate, CompletedDate, Status, Category, IsCritical |

### ProjectMember
| Table: `ProjectMembers` |
| Key Properties: ProjectId, UserId, Role (on project), AllocationPercentage |

### ProjectPhase
| Table: `ProjectPhases` |
| Key Properties: ProjectId, Name, Description, StartDate, EndDate, Status |

### Deliverable
| Table: `Deliverables` |
| Key Properties: ProjectId, Name, Description, DueDate, Status, OwnerId, QualityCriteria |

### Baseline
| Table: `Baselines` |
| Key Properties: ProjectId, Name, Version, Type (Schedule/Cost/Scope), DateCreated, Data (JSON snapshot) |

### WorkBreakdownStructure
| Table: `WorkBreakdownStructures` |
| Key Properties: ProjectId, Name, Description, ParentId, Level, SequenceOrder |

### ProjectSchedule
| Table: `ProjectSchedules` |
| Key Properties: ProjectId, Name, StartDate, EndDate, IsBaseline |

### ProjectStakeholder
| Table: `ProjectStakeholders` |
| Key Properties: ProjectId, UserId, Role, Influence, Interest, EngagementLevel |

### ProjectStatusReport
| Table: `ProjectStatusReports` |
| Key Properties: ProjectId, ReportDate, Period, Status, Progress, Accomplishments, NextSteps, Issues, Risks |

---

## Work Items Module

### WorkItem
| Table: `WorkItems` |
| Key Properties: Title, Description, Type (Task/Subtask/Bug/Feature/Epic/Story/Ticket/TestCase), Priority, Status, Source (Internal/Jira/ServiceNow/AzureDevOps/Email), ProjectId, AssigneeId, ReporterId, ParentId, StoryPoints, EstimatedHours, ActualHours, StartDate, DueDate, CompletedAt, Tags, Labels, SortOrder |
| Relationships: BelongsTo Project, HasMany Assignments |

### WorkItemAssignment
| Table: `WorkItemAssignments` |
| Key Properties: WorkItemId, UserId, AssignedAt, AssignedBy |

---

## Resource Management Module

### Resource
| Table: `Resources` |
| Key Properties: UserId, Name, JobTitle, Email, Phone, Type (Employee/Outsourced), Status, CostPerHour, BillableRate, Currency, EmploymentType, DateOfJoining, BusinessUnitId, DepartmentId, TeamId, LocationId, Timezone, UtilizationTarget, CurrentUtilization, IsActive |
| Relationships: HasMany Skills, HasMany Allocations |

### ResourceAllocation
| Table: `ResourceAllocations` |
| Key Properties: ResourceId, ProjectId, StartDate, EndDate, AllocationPercentage, Role, Status |

### Skill
| Table: `Skills` |
| Key Properties: Name, Category, Description |

### ResourceSkill
| Table: `ResourceSkills` |
| Key Properties: ResourceId, SkillId, ProficiencyLevel, YearsOfExperience |

### ResourceRequest
| Table: `ResourceRequests` |
| Key Properties: ProjectId, Role, Skills (JSON), StartDate, EndDate, AllocationPercentage, Priority, Status |

### RateCard
| Table: `RateCards` |
| Key Properties: Name, Role, ResourceType, HourlyRate, DailyRate, Currency, EffectiveDate |

### Contract
| Table: `Contracts` |
| Key Properties: ResourceId, VendorId, StartDate, EndDate, Rate, Type, Status |

### ResourceCalendar
| Table: `ResourceCalendars` |
| Key Properties: ResourceId, Date, IsWorkingDay, StartTime, EndTime |

### Vendor
| Table: `Vendors` |
| Key Properties: Name, ContactName, Email, Phone, Address, IsActive |

---

## Cost Management Module

### Budget
| Table: `Budgets` |
| Key Properties: Name, Description, ProjectId, PortfolioId, Category (Capital/Operational/Labour), PlannedAmount, ApprovedAmount, ActualAmount, CommittedAmount, ForecastAmount, Currency, FiscalYear, FiscalQuarter, Status, StartDate, EndDate |
| **Value Objects**: Variance (computed), VariancePercentage (computed) |

### CostEntry
| Table: `CostEntries` |
| Key Properties: ProjectId, Category, Description, Amount, Currency, IncurredDate, Type (Direct/Indirect), Status |

### CostCategory
| Table: `CostCategories` |
| Key Properties: Name, Code, Description, ParentId, IsActive |

### CostApproval
| Table: `CostApprovals` |
| Key Properties: CostEntryId, ApproverId, ApprovedAt, Comments |

### Invoice
| Table: `Invoices` |
| Key Properties: VendorId, ProjectId, InvoiceNumber, Amount, Currency, IssueDate, DueDate, PaidDate, Status |

### PurchaseCommitment
| Table: `PurchaseCommitments` |
| Key Properties: ProjectId, VendorId, Description, Amount, Currency, CommitmentDate, DeliveryDate, Status |

---

## Risk Management Module

### Risk
| Table: `Risks` |
| Key Properties: Title, Description, ProjectId, PortfolioId, CategoryId, Impact (High/Medium/Low), Likelihood (High/Medium/Low), RiskLevel (Critical/High/Medium/Low), Status (Open/Mitigating/Closed/Monitoring), PotentialCost, PotentialDelay, RiskResponse, OwnerId, IdentifiedDate, TargetResolutionDate, ClosedDate |
| Relationships: HasMany Mitigations, HasMany Contingencies, BelongsTo Category |

### RiskCategory
| Table: `RiskCategories` |
| Key Properties: Name, Code, Description, Color |

### RiskMitigation
| Table: `RiskMitigations` |
| Key Properties: RiskId, Description, OwnerId, DueDate, Status, Effectiveness |

### RiskContingency
| Table: `RiskContingencies` |
| Key Properties: RiskId, Description, TriggerCondition, Budget, OwnerId |

---

## Issue Management Module

### Issue
| Table: `Issues` |
| Key Properties: Title, Description, ProjectId, CategoryId, Severity (Critical/Major/Minor), Priority (High/Medium/Low), Status (Open/InProgress/Resolved), AssigneeId, ReporterId, IdentifiedDate, TargetResolutionDate, ResolvedDate, Resolution |
| Relationships: HasMany Actions, BelongsTo Category |

### IssueCategory
| Table: `IssueCategories` |
| Key Properties: Name, Code, Description, Color |

### IssueAction
| Table: `IssueActions` |
| Key Properties: IssueId, Description, AssigneeId, DueDate, Status, CompletedAt |

---

## Meetings Module

### Meeting
| Table: `Meetings` |
| Key Properties: Title, Description, ProjectId, MeetingType (Virtual/InPerson/Hybrid), Location, MeetingLink, StartTime, EndTime, Status, Notes, OrganizerId, Recurrence |
| Relationships: HasMany AgendaItems, Participants, Minutes, Actions |

### MeetingAgenda
| Table: `MeetingAgendas` |
| Key Properties: MeetingId, Title, Description, Duration, SortOrder |

### MeetingParticipant
| Table: `MeetingParticipants` |
| Key Properties: MeetingId, nullable UserId for internal attendees, required Email for external attendees, optional Name snapshot, Status (Accepted/Declined/Pending/Tentative), Attended |
| Invariants: exactly one of the internal-user identity or external-email identity is accepted by the API; active attendees are unique per meeting by UserId and case-insensitive Email |

### MeetingMinutes
| Table: `MeetingMinutes` |
| Key Properties: MeetingId, Content, CreatedBy, CreatedAt |

### MeetingAction
| Table: `MeetingActions` |
| Key Properties: MeetingId, Description, AssigneeId, DueDate, Status, CompletedAt |

---

## Workflow Engine Module

### WorkflowTemplate
| Table: `WorkflowTemplates` |
| Key Properties: Name, Description, EntityType, Version, IsActive |

### WorkflowVersion
| Table: `WorkflowVersions` |
| Key Properties: WorkflowTemplateId, VersionNumber, Definition (JSON), IsPublished, PublishedAt |

### WorkflowInstance
| Table: `WorkflowInstances` |
| Key Properties: WorkflowTemplateId, EntityId, EntityType, CurrentState, Status, StartedAt, CompletedAt |
| Relationships: BelongsTo WorkflowTemplate, HasMany States |

### WorkflowState
| Table: `WorkflowStates` |
| Key Properties: WorkflowInstanceId, StateName, EnteredAt, CompletedAt |

### WorkflowTransition
| Table: `WorkflowTransitions` |
| Key Properties: WorkflowTemplateId, FromState, ToState, Name, TriggerType, Conditions (JSON) |

### WorkflowCondition
| Table: `WorkflowConditions` |
| Key Properties: WorkflowTransitionId, Field, Operator, Value, LogicGroup |

### SLA
| Table: `SLAs` |
| Key Properties: WorkflowTemplateId, Name, Duration, DurationUnit, WarningThreshold, BreachAction |

### Escalation
| Table: `Escalations` |
| Key Properties: WorkflowTemplateId, Name, TriggerCondition, EscalateToRole, EscalateToUser, Timeout |

---

## Approvals Module

### ApprovalRequest
| Table: `ApprovalRequests` |
| Key Properties: Title, Description, EntityType, EntityId, EntityNumber, Type, Status, Priority, Amount, Currency, RequestedBy, RequestedAt, RequiredBy, CurrentApproverId, ApprovalLevel |
| Relationships: HasMany Actions |

### ApprovalAction
| Table: `ApprovalActions` |
| Key Properties: ApprovalRequestId, ApproverId, Action (Approved/Rejected/Delegated), Comments, ActionedAt |

### Delegation
| Table: `Delegations` |
| Key Properties: FromUserId, ToUserId, StartDate, EndDate, EntityType, IsActive |

---

## Notifications Module

### Notification
| Table: `Notifications` |
| Key Properties: TenantId, nullable UserId, Title, Body, Type, ReferenceType, ReferenceId, IsInAppVisible, IsRead, ReadAt, SentAt, ScheduledAt |
| Purpose: Canonical user-facing event record; channel delivery state is stored separately. |

### NotificationDelivery
| Table: `NotificationDeliveries` |
| Key Properties: TenantId, NotificationId, Channel (Push/Email), Status, AttemptCount, MaxAttempts, NextAttemptAt, ProcessingStartedAt, LeaseId, DeliveredAt, FailureCategory, LastError |
| Invariants: one delivery per NotificationId and Channel; Email exists as a future channel only and is not dispatched. |

### NotificationDeliveryAttempt
| Table: `NotificationDeliveryAttempts` |
| Key Properties: TenantId, NotificationDeliveryId, DeviceTokenId, Status, AttemptCount, NextAttemptAt, DeliveredAt, ProviderMessageId, FailureCategory, LastError |
| Purpose: Independent per-device Push result and retry state. |

### NotificationTemplate
| Table: `NotificationTemplates` |
| Key Properties: Name, Type, Channel, SubjectTemplate, BodyTemplate |

### NotificationPreference
| Table: `NotificationPreferences` |
| Key Properties: TenantId, UserId, Type, InAppEnabled, PushEnabled, EmailEnabled, DigestEnabled, DigestFrequency |
| Invariants: supported event type is validated at the API; active preference is unique by TenantId, UserId, and Type. Defaults are In-App on, Push off, Email off. |

### UserDeviceToken
| Table: `UserDeviceTokens` |
| Key Properties: TenantId, UserId, Token, TokenHash, Platform, DeviceName, IsActive, LastSeenAt, DeactivatedAt |
| Invariants: only one active row may own a token fingerprint across tenants/users; inactive previous owners remain for history. |

### NotificationDigest
| Table: `NotificationDigests` |
| Key Properties: UserId, Frequency, LastSentAt, PendingCount |

---

## Documents Module

### Document
| Table: `Documents` |
| Key Properties: Name, Description, ProjectId, CategoryId, FileName, FileSize, ContentType, StoragePath, OwnerId, Status |
| Relationships: HasMany Versions, HasMany Reviews |

### DocumentVersion
| Table: `DocumentVersions` |
| Key Properties: DocumentId, VersionNumber, FileName, FileSize, StoragePath, UploadedBy, UploadedAt, Notes |

### DocumentCategory
| Table: `DocumentCategories` |
| Key Properties: Name, Code, Description, ParentId, IsActive |

### DocumentReview
| Table: `DocumentReviews` |
| Key Properties: DocumentId, ReviewerId, Status, Comments, ReviewDate |

---

## Reporting Module

### ReportDefinition
| Table: `ReportDefinitions` |
| Key Properties: Name, Description, Category, Type, Parameters (JSON), QueryDefinition (JSON), OwnerId, IsPublic |

### ReportSchedule
| Table: `ReportSchedules` |
| Key Properties: ReportDefinitionId, Frequency (Daily/Weekly/Monthly/Quarterly), CronExpression, Recipients (JSON), LastRunAt, NextRunAt, IsActive |

### ReportExport
| Table: `ReportExports` |
| Key Properties: ReportDefinitionId, Format (PDF/Excel/CSV), GeneratedAt, GeneratedBy, FilePath, FileSize, Parameters (JSON) |

---

## Audit Module

### AuditEntry
| Table: `AuditEntries` |
| Key Properties: EntityType, EntityId, Action (Create/Update/Delete), Changes (JSON), PerformedBy, PerformedAt, IpAddress, CorrelationId |

### AuditActivity
| Table: `AuditActivities` |
| Key Properties: UserId, Activity, Description, ResourceType, ResourceId, Timestamp |

---

## Integrations Module

### IntegrationConnection
| Table: `IntegrationConnections` |
| Key Properties: Name, ProviderType (Jira/AzureDevOps/ServiceNow/SAP/Oracle), ConnectionUrl, ApiKey/Token (encrypted), IsActive, LastSyncAt |

### IntegrationSync
| Table: `IntegrationSyncs` |
| Key Properties: IntegrationConnectionId, Direction (Inbound/Outbound/Bidirectional), EntityType, StartedAt, CompletedAt, Status, ItemsProcessed, ItemsFailed, ErrorLog |

### IntegrationWebhook
| Table: `IntegrationWebhooks` |
| Key Properties: IntegrationConnectionId, Url, Events (JSON), Secret, IsActive |

### SyncConflict
| Table: `SyncConflicts` |
| Key Properties: IntegrationSyncId, EntityType, EntityId, SourceData (JSON), TargetData (JSON), Resolution, ResolvedAt, ResolvedBy |

---

## Plugin Management Module

### Plugin
| Table: `Plugins` |
| Key Properties: Name, Description, Version, Provider, IconUrl, Status, Manifest (JSON) |

### TenantPlugin
| Table: `TenantPlugins` |
| Key Properties: PluginId, TenantId, Status, ActivatedAt, Configuration (JSON) |

### PluginDependency
| Table: `PluginDependencies` |
| Key Properties: PluginId, DependencyPluginId, VersionRange |

---

## AI Planning Module

### AIPlanRequest
| Table: `AIPlanRequests` |
| Key Properties: Prompt/Input (JSON), Provider, Model, Status, CreatedById |

### AIPlanResponse
| Table: `AIPlanResponses` |
| Key Properties: AIPlanRequestId, Response (JSON), TokensUsed, ProcessingTime, Confidence |
