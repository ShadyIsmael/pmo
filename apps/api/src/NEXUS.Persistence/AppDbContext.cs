using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NEXUS.Modules.AIPlanning.Domain;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Integrations.Domain;
using NEXUS.Modules.IssueManagement.Domain;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.PluginManagement.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.Reporting.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<EntityCodeSequence> EntityCodeSequences => Set<EntityCodeSequence>();
    public DbSet<OrganizationalUnit> OrganizationalUnits => Set<OrganizationalUnit>();
    public DbSet<ResourceOrganizationalUnit> ResourceOrganizationalUnits => Set<ResourceOrganizationalUnit>();
    public DbSet<ResourceReportingLine> ResourceReportingLines => Set<ResourceReportingLine>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<WorkingCalendar> WorkingCalendars => Set<WorkingCalendar>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<WorkingCalendarDayRule> WorkingCalendarDayRules => Set<WorkingCalendarDayRule>();
    public DbSet<Lookup> Lookups => Set<Lookup>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<CostCentre> CostCentres => Set<CostCentre>();

    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<global::NEXUS.Modules.Portfolio.Domain.Program> Programs => Set<global::NEXUS.Modules.Portfolio.Domain.Program>();
    public DbSet<ProgramManagerAssignment> ProgramManagerAssignments => Set<ProgramManagerAssignment>();
    public DbSet<PortfolioManagerAssignment> PortfolioManagerAssignments => Set<PortfolioManagerAssignment>();
    public DbSet<ProgramHandoverRequest> ProgramHandoverRequests => Set<ProgramHandoverRequest>();
    public DbSet<PortfolioHandoverRequest> PortfolioHandoverRequests => Set<PortfolioHandoverRequest>();
    public DbSet<ProgramSnapshot> ProgramSnapshots => Set<ProgramSnapshot>();
    public DbSet<PortfolioSnapshot> PortfolioSnapshots => Set<PortfolioSnapshot>();
    public DbSet<StrategicObjective> StrategicObjectives => Set<StrategicObjective>();
    public DbSet<PortfolioHealth> PortfolioHealths => Set<PortfolioHealth>();
    public DbSet<PortfolioBudget> PortfolioBudgets => Set<PortfolioBudget>();
    public DbSet<PortfolioRisk> PortfolioRisks => Set<PortfolioRisk>();

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectManagerAssignment> ProjectManagerAssignments => Set<ProjectManagerAssignment>();
    public DbSet<ProjectSnapshot> ProjectSnapshots => Set<ProjectSnapshot>();
    public DbSet<ProjectHandoverRequest> ProjectHandoverRequests => Set<ProjectHandoverRequest>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectMemberRole> ProjectMemberRoles => Set<ProjectMemberRole>();
    public DbSet<ProjectPhase> ProjectPhases => Set<ProjectPhase>();
    public DbSet<ProjectStakeholder> ProjectStakeholders => Set<ProjectStakeholder>();
    public DbSet<StakeholderInvitation> StakeholderInvitations => Set<StakeholderInvitation>();
    public DbSet<WorkBreakdownStructure> WorkBreakdownStructures => Set<WorkBreakdownStructure>();
    public DbSet<ProjectSchedule> ProjectSchedules => Set<ProjectSchedule>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Baseline> Baselines => Set<Baseline>();
    public DbSet<global::NEXUS.Modules.ProjectManagement.Domain.Entities.ProjectBaselineVersion> ProjectBaselineVersions => Set<global::NEXUS.Modules.ProjectManagement.Domain.Entities.ProjectBaselineVersion>();
    public DbSet<global::NEXUS.Modules.ProjectManagement.Domain.Entities.ProjectPlanDraft> ProjectPlanDrafts => Set<global::NEXUS.Modules.ProjectManagement.Domain.Entities.ProjectPlanDraft>();
    public DbSet<Deliverable> Deliverables => Set<Deliverable>();
    public DbSet<ProjectStatusReport> ProjectStatusReports => Set<ProjectStatusReport>();
    public DbSet<ProjectStatusReportRiskSnapshot> ProjectStatusReportRiskSnapshots => Set<ProjectStatusReportRiskSnapshot>();
    public DbSet<ProjectStatusReportIssueSnapshot> ProjectStatusReportIssueSnapshots => Set<ProjectStatusReportIssueSnapshot>();
    public DbSet<ProjectStatusReportChangeRequestSnapshot> ProjectStatusReportChangeRequestSnapshots => Set<ProjectStatusReportChangeRequestSnapshot>();
    public DbSet<ProjectReportingConfiguration> ProjectReportingConfigurations => Set<ProjectReportingConfiguration>();
    public DbSet<ProjectReportingPlanSelection> ProjectReportingPlanSelections => Set<ProjectReportingPlanSelection>();
    public DbSet<ProjectChangeRequest> ProjectChangeRequests => Set<ProjectChangeRequest>();
    public DbSet<ProjectChangeRequestApproval> ProjectChangeRequestApprovals => Set<ProjectChangeRequestApproval>();
    public DbSet<ProjectChangeRequestApprovalHistory> ProjectChangeRequestApprovalHistories => Set<ProjectChangeRequestApprovalHistory>();

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
    public DbSet<PlanActivity> PlanActivities => Set<PlanActivity>();
    public DbSet<PlanWbsNode> PlanWbsNodes => Set<PlanWbsNode>();
    public DbSet<PlanDeliverable> PlanDeliverables => Set<PlanDeliverable>();
    public DbSet<PlanDependency> PlanDependencies => Set<PlanDependency>();
    public DbSet<PlanAssignment> PlanAssignments => Set<PlanAssignment>();
    public DbSet<CalendarTemplate> CalendarTemplates => Set<CalendarTemplate>();
    public DbSet<PlanCalendar> PlanCalendars => Set<PlanCalendar>();
    public DbSet<CalendarDayRule> CalendarDayRules => Set<CalendarDayRule>();
    public DbSet<CalendarHoliday> CalendarHolidays => Set<CalendarHoliday>();
    public DbSet<PlanningTablePreference> PlanningTablePreferences => Set<PlanningTablePreference>();
    public DbSet<PlanImportSession> PlanImportSessions => Set<PlanImportSession>();
    public DbSet<PlanImportIssue> PlanImportIssues => Set<PlanImportIssue>();

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemAssignment> WorkItemAssignments => Set<WorkItemAssignment>();
    public DbSet<WorkItemComment> WorkItemComments => Set<WorkItemComment>();
    public DbSet<WorkItemAttachment> WorkItemAttachments => Set<WorkItemAttachment>();

    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<ResourceSkill> ResourceSkills => Set<ResourceSkill>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<ResourceAllocation> ResourceAllocations => Set<ResourceAllocation>();
    public DbSet<ResourceRequest> ResourceRequests => Set<ResourceRequest>();
    public DbSet<RateCard> RateCards => Set<RateCard>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ResourceCalendar> ResourceCalendars => Set<ResourceCalendar>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<ResourceCostProfile> ResourceCostProfiles => Set<ResourceCostProfile>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<TimerSession> TimerSessions => Set<TimerSession>();
    public DbSet<TimerSegment> TimerSegments => Set<TimerSegment>();

    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<ProjectAnnualBudget> ProjectAnnualBudgets => Set<ProjectAnnualBudget>();
    public DbSet<ProjectAnnualBudgetClosing> ProjectAnnualBudgetClosings => Set<ProjectAnnualBudgetClosing>();
    public DbSet<ProjectPaymentRequestFundingTransfer> ProjectPaymentRequestFundingTransfers => Set<ProjectPaymentRequestFundingTransfer>();
    public DbSet<BudgetRevision> BudgetRevisions => Set<BudgetRevision>();
    public DbSet<CostEntry> CostEntries => Set<CostEntry>();
    public DbSet<CostCategory> CostCategories => Set<CostCategory>();
    public DbSet<CostApproval> CostApprovals => Set<CostApproval>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<PurchaseCommitment> PurchaseCommitments => Set<PurchaseCommitment>();
    public DbSet<PaymentObligation> PaymentObligations => Set<PaymentObligation>();
    public DbSet<PaymentSettlement> PaymentSettlements => Set<PaymentSettlement>();
    public DbSet<PaymentNotificationDispatch> PaymentNotificationDispatches => Set<PaymentNotificationDispatch>();
    public DbSet<ProjectPaymentRequestDetail> ProjectPaymentRequestDetails => Set<ProjectPaymentRequestDetail>();
    public DbSet<PaymentRequestApproverAssignment> PaymentRequestApproverAssignments => Set<PaymentRequestApproverAssignment>();

    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<RiskAction> RiskActions => Set<RiskAction>();
    public DbSet<RiskActionUpdate> RiskActionUpdates => Set<RiskActionUpdate>();
    public DbSet<RiskImpactAssessment> RiskImpactAssessments => Set<RiskImpactAssessment>();
    public DbSet<RiskImpactAssessmentType> RiskImpactAssessmentTypes => Set<RiskImpactAssessmentType>();
    public DbSet<RiskCategory> RiskCategories => Set<RiskCategory>();
    public DbSet<RiskMitigation> RiskMitigations => Set<RiskMitigation>();
    public DbSet<RiskContingency> RiskContingencies => Set<RiskContingency>();

    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueCategory> IssueCategories => Set<IssueCategory>();
    public DbSet<IssueAction> IssueActions => Set<IssueAction>();

    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingAgenda> MeetingAgendas => Set<MeetingAgenda>();
    public DbSet<MeetingParticipant> MeetingParticipants => Set<MeetingParticipant>();
    public DbSet<MeetingMinutes> MeetingMinutes => Set<MeetingMinutes>();
    public DbSet<MeetingMinutesComment> MeetingMinutesComments => Set<MeetingMinutesComment>();
    public DbSet<MeetingMinutesChecklistItem> MeetingMinutesChecklistItems => Set<MeetingMinutesChecklistItem>();
    public DbSet<MeetingMinutesAttachment> MeetingMinutesAttachments => Set<MeetingMinutesAttachment>();
    public DbSet<MeetingReminderDispatch> MeetingReminderDispatches => Set<MeetingReminderDispatch>();
    public DbSet<MeetingMinutesWorkItemLink> MeetingMinutesWorkItemLinks => Set<MeetingMinutesWorkItemLink>();
    public DbSet<MeetingAction> MeetingActions => Set<MeetingAction>();

    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowDefinitionVersion> WorkflowDefinitionVersions => Set<WorkflowDefinitionVersion>();
    public DbSet<WorkflowState> WorkflowStates => Set<WorkflowState>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
    public DbSet<WorkflowTransitionGuard> WorkflowTransitionGuards => Set<WorkflowTransitionGuard>();
    public DbSet<WorkflowTransitionAction> WorkflowTransitionActions => Set<WorkflowTransitionAction>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowTransitionHistory> WorkflowTransitionHistories => Set<WorkflowTransitionHistory>();
    public DbSet<ProjectWorkflowConfiguration> ProjectWorkflowConfigurations => Set<ProjectWorkflowConfiguration>();

    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();
    public DbSet<Delegation> Delegations => Set<Delegation>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailNotificationProfileEntity> EmailNotificationProfiles => Set<EmailNotificationProfileEntity>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();
    public DbSet<UserDeviceToken> UserDeviceTokens => Set<UserDeviceToken>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationDigest> NotificationDigests => Set<NotificationDigest>();

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();
    public DbSet<DocumentReview> DocumentReviews => Set<DocumentReview>();

    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();
    public DbSet<ReportExport> ReportExports => Set<ReportExport>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<AuditActivity> AuditActivities => Set<AuditActivity>();

    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();
    public DbSet<IntegrationSync> IntegrationSyncs => Set<IntegrationSync>();
    public DbSet<IntegrationWebhook> IntegrationWebhooks => Set<IntegrationWebhook>();
    public DbSet<SyncConflict> SyncConflicts => Set<SyncConflict>();
    public DbSet<DirectoryConnection> DirectoryConnections => Set<DirectoryConnection>();
    public DbSet<DirectorySyncScope> DirectorySyncScopes => Set<DirectorySyncScope>();
    public DbSet<DirectoryFieldMapping> DirectoryFieldMappings => Set<DirectoryFieldMapping>();
    public DbSet<DirectoryGroup> DirectoryGroups => Set<DirectoryGroup>();
    public DbSet<DirectoryGroupMapping> DirectoryGroupMappings => Set<DirectoryGroupMapping>();
    public DbSet<DirectoryOrganizationMapping> DirectoryOrganizationMappings => Set<DirectoryOrganizationMapping>();
    public DbSet<DirectoryProvisioningPolicy> DirectoryProvisioningPolicies => Set<DirectoryProvisioningPolicy>();
    public DbSet<DirectoryUserLink> DirectoryUserLinks => Set<DirectoryUserLink>();
    public DbSet<DirectoryRoleGrant> DirectoryRoleGrants => Set<DirectoryRoleGrant>();
    public DbSet<DirectorySyncRun> DirectorySyncRuns => Set<DirectorySyncRun>();
    public DbSet<DirectorySyncItem> DirectorySyncItems => Set<DirectorySyncItem>();

    public DbSet<Plugin> Plugins => Set<Plugin>();
    public DbSet<TenantPlugin> TenantPlugins => Set<TenantPlugin>();
    public DbSet<PluginDependency> PluginDependencies => Set<PluginDependency>();

    public DbSet<AIPlanRequest> AIPlanRequests => Set<AIPlanRequest>();
    public DbSet<AIPlanResponse> AIPlanResponses => Set<AIPlanResponse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.Entity<Document>().HasIndex(document => document.CategoryId);
        modelBuilder.Entity<Document>().HasIndex(document => document.SourceTemplateId);

        // SQLite has no rowversion type: a byte[] concurrency token configured with
        // IsRowVersion() is store-generated and SQLite never fills it, so inserts would
        // violate the NOT NULL constraint. Give such columns a default on SQLite only,
        // leaving the SQL Server rowversion behavior untouched.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQLite does not accept SQL Server national string literals (N'...') inside
            // check constraints, so test schemas translate them to plain literals. The
            // SQL Server model, its snapshot, and the applied migrations are unchanged.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            foreach (var constraint in entityType.GetDeclaredCheckConstraints().ToArray())
            {
                if (constraint.Name is { } name
                    && constraint.Sql.Contains("N'", StringComparison.Ordinal))
                {
                    entityType.RemoveCheckConstraint(name);
                    entityType.AddCheckConstraint(
                        name,
                        constraint.Sql.Replace("N'", "'", StringComparison.Ordinal));
                }
            }

            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(byte[])
                                     && p.ValueGenerated == ValueGenerated.OnAddOrUpdate
                                     && p.IsConcurrencyToken))
            {
                property.SetDefaultValueSql("randomblob(8)");
            }
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // EF Core permits a query filter only on the root of an inheritance
            // hierarchy. Filters defined on the root are automatically applied to
            // derived queries, so registering them again on a derived type (for
            // example CalendarTemplate/PlanCalendar) makes model construction fail.
            var isHierarchyRoot = entityType.BaseType is null;
            var isSoftDeletable = isHierarchyRoot && typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType);
            var isTenantEntity = isHierarchyRoot && typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType);

            // Apply tenant isolation and soft-delete as a SINGLE query filter. Calling
            // HasQueryFilter twice would have the second call replace the first, so
            // the combined predicate is registered exactly once per relevant entity.
            if (isTenantEntity && isSoftDeletable)
            {
                var method = typeof(AppDbContext).GetMethod("ApplyTenantAndSoftDeleteFilter", BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, new object[] { modelBuilder });
            }
            else if (isTenantEntity)
            {
                var method = typeof(AppDbContext).GetMethod("ApplyTenantFilter", BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, new object[] { modelBuilder });
            }
            else if (isSoftDeletable)
            {
                var method = typeof(AppDbContext).GetMethod("ApplySoftDeleteFilter", BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(entityType.ClrType);
                method.Invoke(null, new object[] { modelBuilder });
            }

            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    var (precision, scale) = GetDecimalPrecision(property.Name);
                    property.SetPrecision(precision);
                    property.SetScale(scale);
                }
            }
        }
    }

    private static void ApplySoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : class, ISoftDeletable
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
    }

    private void ApplyTenantFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => CurrentTenantId != Guid.Empty && e.TenantId == CurrentTenantId);
    }

    /// <summary>
    /// Registers a single combined query filter enforcing both tenant isolation and
    /// soft-delete. <see cref="CurrentTenantId"/> is referenced as an instance
    /// property so EF parameterizes it and re-evaluates it per query against the one
    /// cached model. Queries using <c>IgnoreQueryFilters()</c> bypass this filter for
    /// admin/recovery scenarios.
    /// </summary>
    private void ApplyTenantAndSoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity, ISoftDeletable
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => CurrentTenantId != Guid.Empty && e.TenantId == CurrentTenantId && !e.IsDeleted);
    }

    public override int SaveChanges() => SaveChanges(acceptAllChangesOnSuccess: true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareTrackedEntitiesForSave();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareTrackedEntitiesForSave();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareTrackedEntitiesForSave()
    {
        ValidateTenantScopedEntries();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            // Audit records are append-only. Any attempt to rewrite or remove an
            // existing entry through the application is a programming error and
            // must fail loudly rather than silently corrupting the trail.
            if (entry.Entity is AuditEntry or AuditActivity && entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Audit records are immutable and cannot be updated or deleted.");
            }

            // ApprovalRequest.RowVersion predates SQL Server rowversion mapping
            // and is retained as a binary token for compatibility with existing
            // generic approval data. Rotate it on every write so EF performs a
            // genuine optimistic concurrency check.
            if (entry.Entity is ApprovalRequest && entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(request => request.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = null;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
    }

    private void ValidateTenantScopedEntries()
    {
        var currentTenantId = GetCurrentTenantId();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is not ITenantEntity tenantEntity
                || entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (currentTenantId == Guid.Empty
                || tenantEntity.TenantId == Guid.Empty
                || tenantEntity.TenantId != currentTenantId)
            {
                throw new InvalidOperationException(
                    "Tenant-scoped entities can only be persisted in the matching tenant context.");
            }

            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if (entry.Property(nameof(ITenantEntity.TenantId)).OriginalValue is not Guid originalTenantId
                    || originalTenantId != currentTenantId
                    || tenantEntity.TenantId != originalTenantId)
                {
                    throw new InvalidOperationException(
                        "Tenant-scoped entities can only be persisted in the matching tenant context.");
                }
            }
        }
    }

    private static IHttpContextAccessor? _httpContextAccessor;

    /// <summary>
    /// Wires tenant resolution to the current HTTP request so the global query
    /// filter filters by the JWT "tenantId" claim. A malformed or missing claim
    /// in a request resolves to no tenant. Trusted out-of-request work must
    /// establish an explicit tenant execution scope.
    /// </summary>
    public static void ConfigureTenantResolution(IHttpContextAccessor accessor)
    {
        _httpContextAccessor = accessor;
    }

    /// <summary>
    /// Current tenant for query filtering, resolved per-execution from the active
    /// HTTP request. Referenced as an instance property from the global query
    /// filters so EF parameterizes it and re-evaluates it for every query instead
    /// of baking a single tenant value into the cached model/compiled query.
    /// </summary>
    internal Guid CurrentTenantId => GetCurrentTenantId();

    internal static Guid GetCurrentTenantId()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext is not null)
        {
            if (httpContext.User.Identity?.IsAuthenticated != true)
                return Guid.Empty;

            var tenantClaim = httpContext.User.FindFirst("tenantId")?.Value;
            return Guid.TryParse(tenantClaim, out var parsed) && parsed != Guid.Empty ? parsed : Guid.Empty;
        }

        return TrustedTenantExecutionContext.CurrentTenantId is { } trustedTenantId
            ? trustedTenantId
            : Guid.Empty;
    }

    private static (int precision, int scale) GetDecimalPrecision(string propertyName)
    {
        if (propertyName.Contains("Allocation", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Percent", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Utilization", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Progress", StringComparison.OrdinalIgnoreCase))
            return (7, 4);

        if (propertyName.Contains("Hour", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Duration", StringComparison.OrdinalIgnoreCase))
            return (10, 2);

        if (propertyName.Contains("ExchangeRate", StringComparison.OrdinalIgnoreCase))
            return (18, 8);

        return (19, 4);
    }
}
