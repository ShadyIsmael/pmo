using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

/// <summary>
/// Persists an approval-plan override only on a Draft Annual Budget. Project Settings
/// remain the defaults and are never modified by this workflow.
/// </summary>
public sealed class ProjectAnnualBudgetDraftApprovalConfigurationService(
    AppDbContext db,
    IUserEffectivePermissionService permissions,
    IManagementContextAccessor management,
    IAuditService audit)
{
    private const string ViewPermission = "ProjectAnnualBudget.View";
    private const string EditDraftPermission = "ProjectAnnualBudget.EditDraft";
    private const string ConfigurePermission = "ProjectAnnualBudget.ConfigureDraftApproval";
    private const string ApprovePermission = "ProjectAnnualBudget.Approve";
    private const string ApprovalEntityType = "ProjectAnnualBudget";
    private const string ReviewApprovalType = "ProjectAnnualBudgetReview";
    private const string RevisionApprovalType = "ProjectAnnualBudgetRevision";
    private const string ApprovalModeSequential = "Sequential";
    private const string ApprovalModeParallel = "Parallel";

    public async Task<Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>> GetAsync(
        Guid tenantId, Guid projectId, Guid budgetId, Guid actorId, CancellationToken cancellationToken)
    {
        var accessError = await ValidateAccessAsync(tenantId, projectId, actorId, saving: false, cancellationToken);
        if (accessError is not null)
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(accessError);

        var project = await FindProject(tenantId, projectId).AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(Error.NotFound("Project not found."));
        var budget = await FindBudget(tenantId, projectId, budgetId).AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (budget is null)
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(Error.NotFound("Annual Budget not found."));
        if (budget.Status != ProjectAnnualBudgetStatuses.Draft)
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Conflict("Only an Annual Budget Draft can be configured. Withdraw a pending submission before editing its plan."));
        if (await HasPendingReviewAsync(tenantId, budgetId, cancellationToken))
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Conflict("Withdraw the pending submission before editing this Draft's approval configuration."));

        var defaults = await LoadProjectDefaultsAsync(tenantId, projectId, cancellationToken);
        var candidates = await LoadCandidatesAsync(tenantId, projectId, actorId, cancellationToken);
        if (!ProjectAnnualBudgetDraftApprovalConfigurationSnapshot.TryRead(
                budget.DraftApprovalConfigurationJson, out var saved))
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Conflict("The Draft approval configuration is invalid. Contact an administrator before submitting this budget."));

        var mode = saved?.ApprovalMode ?? defaults.Configuration?.ApprovalMode;
        var selections = saved?.Approvers ?? ToSelections(defaults.Approvers);
        var issues = ValidatePlan(project.FinanceApprovalMode, mode, selections, candidates, actorId,
            saved is not null || defaults.Configuration is not null, enforceSubmitterSeparation: false).ToList();
        if (saved is not null && !SameVersion(saved.ProjectSettingsRowVersion, project.RowVersion))
            issues.Add("project_settings_changed_since_draft_configuration");
        if (saved is not null && !SameVersion(saved.ProjectSettingsConfigurationRowVersion, defaults.Configuration?.RowVersion))
            issues.Add("project_approval_defaults_changed_since_draft_configuration");

        var approverNames = await LoadNamesAsync(tenantId, selections.Select(item => item.UserId), cancellationToken);
        var configured = selections.OrderBy(item => item.Position ?? int.MaxValue)
            .ThenBy(item => item.UserId)
            .Select(item =>
            {
                var candidate = candidates.GetValueOrDefault(item.UserId);
                var issueCode = candidate?.SelectionBlockReason
                    ?? (candidate is null ? "configured_approver_unavailable"
                        : item.UserId == actorId ? "submitter_separation_of_duties" : null);
                return new ProjectAnnualBudgetConfiguredApproverDto(
                    item.UserId,
                    approverNames.GetValueOrDefault(item.UserId),
                    item.Position,
                    candidate is { SelectionBlockReason: null },
                    issueCode);
            })
            .ToArray();
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        var canSave = !management.Current!.EffectiveReadOnly
            && effective.Contains(EditDraftPermission)
            && effective.Contains(ConfigurePermission);

        return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Success(
            new ProjectAnnualBudgetDraftApprovalConfigurationDto(
                projectId,
                budgetId,
                project.Name,
                saved is null
                    ? ProjectAnnualBudgetDraftApprovalConfigurationSources.ProjectSettings
                    : ProjectAnnualBudgetDraftApprovalConfigurationSources.DraftOverride,
                project.FinanceApprovalMode,
                mode,
                GetAllowedModes(project.FinanceApprovalMode),
                configured,
                candidates.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray(),
                budget.RowVersion,
                defaults.Configuration?.RowVersion,
                project.RowVersion,
                issues.Count == 0,
                canSave,
                issues.Distinct(StringComparer.Ordinal).ToArray(),
                saved?.OverrideReason,
                saved?.UpdatedAtUtc ?? budget.UpdatedAt));
    }

    public async Task<Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>> SaveAsync(
        Guid tenantId,
        Guid projectId,
        Guid budgetId,
        Guid actorId,
        string? actorName,
        SaveProjectAnnualBudgetDraftApprovalConfigurationDto dto,
        CancellationToken cancellationToken)
    {
        var accessError = await ValidateAccessAsync(tenantId, projectId, actorId, saving: true, cancellationToken);
        if (accessError is not null)
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(accessError);
        if (dto.BudgetRowVersion is not { Length: > 0 }
            || dto.ProjectSettingsRowVersion is not { Length: > 0 })
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Validation("Refresh the Draft and Project approval settings before saving."));
        if (string.IsNullOrWhiteSpace(dto.OverrideReason) || dto.OverrideReason.Trim().Length > 1000)
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Validation("Enter a reason for this Draft-specific approval configuration (maximum 1000 characters)."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProject(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(Error.NotFound("Project not found."));
        var budget = await FindBudget(tenantId, projectId, budgetId).FirstOrDefaultAsync(cancellationToken);
        if (budget is null)
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(Error.NotFound("Annual Budget not found."));
        if (budget.Status != ProjectAnnualBudgetStatuses.Draft)
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Conflict("Only an Annual Budget Draft can be configured. Withdraw a pending submission before editing its plan."));
        if (await HasPendingReviewAsync(tenantId, budgetId, cancellationToken))
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Conflict("Withdraw the pending submission before editing this Draft's approval configuration."));
        if (!SameVersion(budget.RowVersion, dto.BudgetRowVersion))
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Conflict("The Draft changed. Refresh before saving its approval configuration."));
        if (!SameVersion(project.RowVersion, dto.ProjectSettingsRowVersion))
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Conflict("Project approval settings changed. Refresh before saving."));

        var defaults = await LoadProjectDefaultsAsync(tenantId, projectId, cancellationToken);
        if (!SameVersion(defaults.Configuration?.RowVersion, dto.ProjectSettingsConfigurationRowVersion))
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Conflict("Project approval defaults changed. Refresh before saving."));

        var candidates = await LoadCandidatesAsync(tenantId, projectId, actorId, cancellationToken);
        var ids = dto.ApproverUserIds?.ToArray() ?? [];
        if (ids.Length == 0 || ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Validation("Select one or more distinct eligible approvers."));
        var approvalMode = dto.ApprovalMode?.Trim();
        var selections = ids.Select((id, index) => new PaymentRequestApproverSelectionDto(
            id,
            string.Equals(approvalMode, ApprovalModeSequential, StringComparison.OrdinalIgnoreCase) ? index + 1 : null,
            PaymentRequestApproverRequirements.Mandatory)).ToArray();
        var issues = ValidatePlan(project.FinanceApprovalMode, approvalMode, selections, candidates, actorId,
            hasConfiguration: true, enforceSubmitterSeparation: false);
        if (issues.Count > 0)
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Validation(string.Join(",", issues)));

        if (!ProjectAnnualBudgetDraftApprovalConfigurationSnapshot.TryRead(
                budget.DraftApprovalConfigurationJson, out var previous))
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Conflict("The existing Draft approval configuration is invalid. Contact an administrator before changing it."));

        var timestamp = DateTime.UtcNow;
        var snapshot = new ProjectAnnualBudgetDraftApprovalConfigurationSnapshot(
            ProjectAnnualBudgetDraftApprovalConfigurationSnapshot.CurrentVersion,
            approvalMode!,
            selections,
            dto.OverrideReason.Trim(),
            project.RowVersion.ToArray(),
            defaults.Configuration?.RowVersion?.ToArray(),
            actorId,
            timestamp);
        object? before = previous is null ? new
        {
            approvalMode = defaults.Configuration?.ApprovalMode,
            approvers = ToSelections(defaults.Approvers)
        } : new
        {
            approvalMode = (string?)previous.ApprovalMode,
            approvers = previous.Approvers
        };
        budget.DraftApprovalConfigurationJson = JsonSerializer.Serialize(snapshot);
        budget.UpdatedAt = timestamp;
        budget.UpdatedBy = actorId.ToString("D");

        await audit.AppendAsync(new AuditEntry
        {
            TenantId = tenantId,
            ProjectId = projectId,
            EntityType = ApprovalEntityType,
            EntityId = budget.Id,
            Action = "DraftApprovalConfigurationSaved",
            UserId = actorId,
            UserName = actorName,
            Timestamp = timestamp,
            Description = $"Draft-specific Annual Budget approval configuration saved for {budget.FinancialReference}.",
            Changes = JsonSerializer.Serialize(new { from = before, to = snapshot }),
            OriginalValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = JsonSerializer.Serialize(snapshot),
            CreatedBy = actorId.ToString("D")
        }, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectAnnualBudgetDraftApprovalConfigurationDto>.Failure(
                Error.Conflict("The Draft or Project approval settings changed. Refresh before saving."));
        }

        return await GetAsync(tenantId, projectId, budgetId, actorId, cancellationToken);
    }

    public static bool TryReadStoredConfiguration(
        string? json, out ProjectAnnualBudgetDraftApprovalConfigurationSnapshot? snapshot) =>
        ProjectAnnualBudgetDraftApprovalConfigurationSnapshot.TryRead(json, out snapshot);

    private async Task<Dictionary<Guid, ProjectAnnualBudgetDraftApproverCandidateDto>> LoadCandidatesAsync(
        Guid tenantId, Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        var participants = await ProjectTeamApproverEligibility.GetActiveLinkedUserIdsAsync(
            db, tenantId, projectId, cancellationToken);
        var assignments = await FindProject(tenantId, projectId).AsNoTracking()
            .Select(item => new { item.ProjectManagerId, item.SponsorId })
            .FirstOrDefaultAsync(cancellationToken);
        if (assignments?.ProjectManagerId is Guid managerId) participants.Add(managerId);
        if (assignments?.SponsorId is Guid sponsorId) participants.Add(sponsorId);
        if (participants.Count == 0) return [];

        var users = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == tenantId && participants.Contains(user.Id)
                && user.IsActive && !user.IsDeleted)
            .Select(user => new { user.Id, user.FullName })
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
        if (users.Count == 0) return [];
        var candidateIds = users.Keys.ToArray();

        var roleAssignments = await db.UserRoles.IgnoreQueryFilters().AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && !assignment.IsDeleted
                && candidateIds.Contains(assignment.UserId)
                && assignment.User.TenantId == tenantId && assignment.User.IsActive && !assignment.User.IsDeleted
                && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted)
            .Select(assignment => new { assignment.UserId, RoleCategory = assignment.Role.Category! })
            .ToListAsync(cancellationToken);
        var categories = roleAssignments.GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.RoleCategory)
                .Where(category => category == RoleCategories.System || category == RoleCategories.Management)
                .Distinct(StringComparer.Ordinal).OrderBy(category => category, StringComparer.Ordinal).ToArray());

        var result = new Dictionary<Guid, ProjectAnnualBudgetDraftApproverCandidateDto>();
        foreach (var (userId, displayName) in users)
        {
            var roleCategories = categories.GetValueOrDefault(userId) ?? [];
            string? reason = roleCategories.Length == 0 ? "system_or_management_role_required" : null;
            if (reason is null)
            {
                var effective = await permissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);
                if (!effective.Contains(ApprovePermission)) reason = "approval_permission_not_effective";
            }
            result[userId] = new ProjectAnnualBudgetDraftApproverCandidateDto(
                userId, displayName, roleCategories, reason);
        }

        return result;
    }

    private async Task<(ProjectAnnualBudgetApprovalConfiguration? Configuration,
        IReadOnlyList<ProjectAnnualBudgetApprovalApprover> Approvers)> LoadProjectDefaultsAsync(
        Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var configuration = await db.Set<ProjectAnnualBudgetApprovalConfiguration>().IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Approvers.Where(approver => approver.TenantId == tenantId
                && approver.ProjectId == projectId && !approver.IsDeleted))
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted,
                cancellationToken);
        return (configuration, (IReadOnlyList<ProjectAnnualBudgetApprovalApprover>)(configuration?.Approvers
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Position ?? int.MaxValue)
            .ThenBy(item => item.ApproverUserId)
            .ToArray() ?? []));
    }

    private async Task<Dictionary<Guid, string?>> LoadNamesAsync(
        Guid tenantId, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var values = ids.Distinct().ToArray();
        if (values.Length == 0) return [];
        return await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == tenantId && values.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToDictionaryAsync(user => user.Id, user => (string?)user.FullName, cancellationToken);
    }

    private async Task<Error?> ValidateAccessAsync(
        Guid tenantId, Guid projectId, Guid actorId, bool saving, CancellationToken cancellationToken)
    {
        var current = management.Current;
        if (tenantId == Guid.Empty || actorId == Guid.Empty || current is null
            || current.TenantId != tenantId || current.UserId != actorId
            || !current.Scope.ProjectIds.Contains(projectId))
            return Error.NotFound("Project not found.");

        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!effective.Contains(ViewPermission))
            return Error.Forbidden("ProjectAnnualBudget.View is required.");
        if (saving && current.EffectiveReadOnly)
            return Error.Forbidden("The current management context is read-only.");
        if (saving && !effective.Contains(EditDraftPermission))
            return Error.Forbidden("ProjectAnnualBudget.EditDraft is required.");
        if (saving && !effective.Contains(ConfigurePermission))
            return Error.Forbidden("ProjectAnnualBudget.ConfigureDraftApproval is required.");
        return null;
    }

    private async Task<bool> HasPendingReviewAsync(
        Guid tenantId, Guid budgetId, CancellationToken cancellationToken) =>
        await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(request => request.TenantId == tenantId && !request.IsDeleted
                && request.EntityType == ApprovalEntityType && request.EntityId == budgetId
                && (request.Type == ReviewApprovalType || request.Type == RevisionApprovalType)
                && request.Status == PaymentRequestStatuses.Pending, cancellationToken);

    private IQueryable<Project> FindProject(Guid tenantId, Guid projectId) => db.Projects.IgnoreQueryFilters()
        .Where(project => project.TenantId == tenantId && project.Id == projectId && !project.IsDeleted);

    private IQueryable<ProjectAnnualBudget> FindBudget(Guid tenantId, Guid projectId, Guid budgetId) =>
        db.ProjectAnnualBudgets.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == budgetId && !item.IsDeleted);

    private static IReadOnlyList<string> ValidatePlan(
        string? projectMode,
        string? approvalMode,
        IReadOnlyCollection<PaymentRequestApproverSelectionDto> selections,
        IReadOnlyDictionary<Guid, ProjectAnnualBudgetDraftApproverCandidateDto> candidates,
        Guid actorId,
        bool hasConfiguration,
        bool enforceSubmitterSeparation)
    {
        var issues = new List<string>();
        if (!hasConfiguration) issues.Add("annual_budget_approval_configuration_missing");
        issues.AddRange(PaymentRequestWorkflowPolicy.ValidateApprovalSetup(projectMode, approvalMode, selections));
        if (selections.Any(item => !string.Equals(item.Requirement, PaymentRequestApproverRequirements.Mandatory,
                StringComparison.OrdinalIgnoreCase)))
            issues.Add("annual_budget_approvers_must_be_mandatory");
        if (selections.Any(item => !candidates.TryGetValue(item.UserId, out var candidate)
                || candidate.SelectionBlockReason is not null))
            issues.Add("configured_approver_unavailable");
        if (enforceSubmitterSeparation && selections.Any(item => item.UserId == actorId))
            issues.Add("annual_budget_submitter_is_configured_approver");
        if (string.Equals(approvalMode, ApprovalModeSequential, StringComparison.OrdinalIgnoreCase))
        {
            var positions = selections.Select(item => item.Position).OrderBy(position => position).ToArray();
            if (positions.Any(position => !position.HasValue)
                || !positions.Select(position => position!.Value).SequenceEqual(Enumerable.Range(1, selections.Count)))
                issues.Add("sequential_approver_positions_invalid");
        }
        else if (string.Equals(approvalMode, ApprovalModeParallel, StringComparison.OrdinalIgnoreCase)
            && selections.Any(item => item.Position.HasValue))
        {
            issues.Add("parallel_approver_positions_invalid");
        }
        return issues.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<PaymentRequestApproverSelectionDto> ToSelections(
        IReadOnlyCollection<ProjectAnnualBudgetApprovalApprover> approvers) => approvers
        .OrderBy(item => item.Position ?? int.MaxValue)
        .ThenBy(item => item.ApproverUserId)
        .Select(item => new PaymentRequestApproverSelectionDto(
            item.ApproverUserId, item.Position, PaymentRequestApproverRequirements.Mandatory))
        .ToArray();

    private static IReadOnlyList<string> GetAllowedModes(string? projectMode) => projectMode switch
    {
        FinanceApprovalModes.Sequential => [ApprovalModeSequential],
        FinanceApprovalModes.Parallel => [ApprovalModeParallel],
        FinanceApprovalModes.Both => [ApprovalModeSequential, ApprovalModeParallel],
        _ => []
    };

    private static bool SameVersion(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
}
