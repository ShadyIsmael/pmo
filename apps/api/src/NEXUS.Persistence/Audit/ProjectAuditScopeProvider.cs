using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.IssueManagement.Domain;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.Persistence;

namespace NEXUS.Persistence.Audit;

/// <summary>
/// Builds the project audit scope with a fixed number of indexed lookups (one per
/// audited entity family) rather than per audit row, so the audit query never
/// degrades into N+1 reads.
/// <para>
/// Soft-deleted children are intentionally included: their audit events are part of
/// the project's history (for example a deleted work item or a removed member). Only
/// the project itself must be live for its trail to be readable.
/// </para>
/// </summary>
public sealed class ProjectAuditScopeProvider : IProjectAuditScopeProvider
{
    private readonly AppDbContext _context;

    public ProjectAuditScopeProvider(AppDbContext context) => _context = context;

    public async Task<ProjectAuditScope?> ResolveAsync(Guid projectId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new { item.Id, item.PublicId, item.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null) return null;

        var scope = new Dictionary<string, HashSet<Guid>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Project"] = [projectId]
        };

        var memberIds = await _context.Set<ProjectMember>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        Add(scope, "ProjectMember", memberIds);

        var stakeholderIds = await _context.Set<ProjectStakeholder>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        Add(scope, "ProjectStakeholder", stakeholderIds);

        // ProjectPerson events carry the member/stakeholder relationship id rather
        // than a separate person id, so the union of both id sets is the scope.
        Add(scope, "ProjectPerson", memberIds);
        Add(scope, "ProjectPerson", stakeholderIds);

        Add(scope, "ProjectSnapshot", await _context.Set<ProjectSnapshot>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "ProjectChangeRequest", await _context.Set<ProjectChangeRequest>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "ProjectStatusReport", await _context.Set<ProjectStatusReport>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "ProjectReportingConfiguration", await _context.Set<ProjectReportingConfiguration>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "ProjectWorkflowConfiguration", await _context.Set<ProjectWorkflowConfiguration>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "WorkItem", await _context.Set<WorkItem>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "Meeting", await _context.Set<Meeting>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        var riskIds = await _context.Set<Risk>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        Add(scope, "Risk", riskIds);

        if (riskIds.Count > 0)
        {
            Add(scope, "RiskImpactAssessment", await _context.Set<RiskImpactAssessment>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => riskIds.Contains(item.RiskId) && item.TenantId == tenantId)
                .Select(item => item.Id).ToListAsync(cancellationToken));

            var riskActionIds = await _context.Set<RiskAction>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => riskIds.Contains(item.RiskId) && item.TenantId == tenantId)
                .Select(item => item.Id).ToListAsync(cancellationToken);
            Add(scope, "RiskAction", riskActionIds);

            if (riskActionIds.Count > 0)
            {
                Add(scope, "RiskActionUpdate", await _context.Set<RiskActionUpdate>().IgnoreQueryFilters().AsNoTracking()
                    .Where(item => riskActionIds.Contains(item.RiskActionId) && item.TenantId == tenantId)
                    .Select(item => item.Id).ToListAsync(cancellationToken));
            }
        }

        Add(scope, "Issue", await _context.Set<Issue>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "Budget", await _context.Set<Budget>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "BudgetRevision", await _context.Set<BudgetRevision>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "CostEntry", await _context.Set<CostEntry>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        var obligationIds = await _context.Set<PaymentObligation>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        Add(scope, "PaymentObligation", obligationIds);

        if (obligationIds.Count > 0)
        {
            Add(scope, "PaymentSettlement", await _context.Set<PaymentSettlement>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.PaymentObligationId.HasValue
                && obligationIds.Contains(item.PaymentObligationId.Value)
                && item.TenantId == tenantId)
                .Select(item => item.Id).ToListAsync(cancellationToken));
        }

        Add(scope, "Invoice", await _context.Set<Invoice>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "Document", await _context.Set<Document>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "ResourceAllocation", await _context.Set<ResourceAllocation>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        Add(scope, "TimeEntry", await _context.Set<TimeEntry>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        // Planning content is reached through plan versions, which belong to a plan.
        var planIds = await _context.Set<Plan>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        Add(scope, "Plan", planIds);

        // Plan grid undo/redo history stores the plan id as the audited entity id.
        Add(scope, "PlanGridCommand", planIds);

        Add(scope, "PlanCalendar", await _context.Set<PlanCalendar>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId)
            .Select(item => item.Id).ToListAsync(cancellationToken));

        if (planIds.Count > 0)
        {
            var versionIds = await _context.Set<PlanVersion>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => planIds.Contains(item.PlanId) && item.TenantId == tenantId)
                .Select(item => item.Id).ToListAsync(cancellationToken);

            Add(scope, "PlanVersion", versionIds);

            if (versionIds.Count > 0)
            {
                Add(scope, "PlanActivity", await _context.Set<PlanActivity>().IgnoreQueryFilters().AsNoTracking()
                    .Where(item => versionIds.Contains(item.PlanVersionId) && item.TenantId == tenantId)
                    .Select(item => item.Id).ToListAsync(cancellationToken));

                Add(scope, "PlanWbsNode", await _context.Set<PlanWbsNode>().IgnoreQueryFilters().AsNoTracking()
                    .Where(item => versionIds.Contains(item.PlanVersionId) && item.TenantId == tenantId)
                    .Select(item => item.Id).ToListAsync(cancellationToken));

                Add(scope, "PlanDependency", await _context.Set<PlanDependency>().IgnoreQueryFilters().AsNoTracking()
                    .Where(item => versionIds.Contains(item.PlanVersionId) && item.TenantId == tenantId)
                    .Select(item => item.Id).ToListAsync(cancellationToken));

                Add(scope, "PlanAssignment", await _context.Set<PlanAssignment>().IgnoreQueryFilters().AsNoTracking()
                    .Where(item => versionIds.Contains(item.PlanVersionId) && item.TenantId == tenantId)
                    .Select(item => item.Id).ToListAsync(cancellationToken));
            }
        }

        var byType = scope
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyCollection<Guid>)pair.Value, StringComparer.OrdinalIgnoreCase);

        return new ProjectAuditScope(project.Id, project.PublicId, project.Name, byType);
    }

    private static void Add(Dictionary<string, HashSet<Guid>> scope, string entityType, IEnumerable<Guid> ids)
    {
        if (!scope.TryGetValue(entityType, out var set))
        {
            set = [];
            scope[entityType] = set;
        }
        foreach (var id in ids) set.Add(id);
    }
}
