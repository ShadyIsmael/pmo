using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Application.Mapping;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;
using System.Security.Claims;
using System.Text.Json;

namespace NEXUS.Modules.ProjectManagement.Application;

public class ProjectService : IProjectService, IProjectReferenceLookup, IProjectPublicIdLookup
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor? _managementContext;
    private readonly ProjectManagerMembershipSynchronizer _membershipSynchronizer;
    private readonly ProjectSponsorMembershipSynchronizer _sponsorMembershipSynchronizer;
    private readonly IProjectPlanProgressLookup? _planProgressLookup;
    private readonly IUserEffectivePermissionService? _effectivePermissions;
    private readonly ITenantFiscalYearService? _tenantFiscalYear;
    private readonly IProjectOrganizationLookup? _projectOrganizationLookup;
    private readonly IProjectLifecycleCatalogService? _lifecycleCatalog;

    public ProjectService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor, ProjectManagerMembershipSynchronizer membershipSynchronizer, IManagementContextAccessor? managementContext = null, IProjectPlanProgressLookup? planProgressLookup = null, IUserEffectivePermissionService? effectivePermissions = null, ITenantFiscalYearService? tenantFiscalYear = null, IProjectOrganizationLookup? projectOrganizationLookup = null, IProjectLifecycleCatalogService? lifecycleCatalog = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _membershipSynchronizer = membershipSynchronizer;
        _sponsorMembershipSynchronizer = new ProjectSponsorMembershipSynchronizer(context);
        _managementContext = managementContext;
        _planProgressLookup = planProgressLookup;
        _effectivePermissions = effectivePermissions;
        _tenantFiscalYear = tenantFiscalYear;
        _projectOrganizationLookup = projectOrganizationLookup;
        _lifecycleCatalog = lifecycleCatalog;
    }

    public async Task<ProjectReference?> FindAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await InScope(_context.Set<Project>())
            .Where(project => project.Id == projectId)
            .Select(project => new ProjectReference(project.Id, project.Name, project.Currency, project.Budget, project.PublicId, project.Code))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> FindPublicIdAsync(Guid projectId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var publicId = await _context.Set<Project>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(project => project.Id == projectId && project.TenantId == tenantId && !project.IsDeleted)
            .Select(project => (Guid?)project.PublicId)
            .FirstOrDefaultAsync(cancellationToken);
        return publicId == Guid.Empty ? null : publicId;
    }

    public async Task<IReadOnlyList<ProjectReference>> GetFinancialReferencesAsync(CancellationToken cancellationToken = default)
        => await InScope(_context.Set<Project>().AsNoTracking())
            .Select(p => new ProjectReference(p.Id, p.Name, p.Currency, p.Budget, p.PublicId, p.Code)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, ProjectReference>> GetReferencesAsync(IEnumerable<Guid> projectIds, CancellationToken cancellationToken = default)
    {
        var ids = projectIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, ProjectReference>();
        return await InScope(_context.Set<Project>().AsNoTracking())
            .Where(project => ids.Contains(project.Id))
            .Select(project => new ProjectReference(project.Id, project.Name, project.Currency, project.Budget, project.PublicId, project.Code))
            .ToDictionaryAsync(project => project.Id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> projectIds, CancellationToken cancellationToken = default)
    {
        var ids = projectIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        return await InScope(_context.Set<Project>())
            .Where(project => ids.Contains(project.Id))
            .ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken);
    }

    public async Task<Result<ProjectDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await InScope(_context.Set<Project>())
            .Include(p => p.Members)
            .Include(p => p.Phases)
            .Include(p => p.Milestones)
            .Include(p => p.Deliverables)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null) return Result<ProjectDto>.Failure(Error.NotFound());

        var dto = project.ToDto();
        await ResolveProjectNamesAsync(dto, cancellationToken);
        return Result<ProjectDto>.Success(dto);
    }

    public async Task<Result<ProjectDto>> GetShellAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // The project shell is used by the detail route and overview tabs. Keep it
        // scalar-only: tab data and team collections have their own endpoints.
        var project = await InScope(_context.Set<Project>().AsNoTracking())
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null) return Result<ProjectDto>.Failure(Error.NotFound());

        var dto = project.ToDto();
        await ResolveProjectNamesAsync(dto, cancellationToken);
        return Result<ProjectDto>.Success(dto);
    }

    public async Task<PagedResult<ProjectSummaryDto>> GetPagedAsync(int page, int pageSize, string? status = null, string? priority = null, Guid? portfolioId = null, CancellationToken cancellationToken = default, Guid? statusId = null)
    {
        var query = InScope(_context.Set<Project>().AsNoTracking());
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);
        if (statusId.HasValue) query = query.Where(p => p.ProjectStatusId == statusId.Value);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(p => p.Priority == priority);
        if (portfolioId.HasValue) query = query.Where(p => p.PortfolioId == portfolioId.Value);
        var total = await query.CountAsync(cancellationToken);
        var summaries = await query.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProjectSummaryDto
            {
                Id = p.Id,
                PublicId = p.PublicId,
                Name = p.Name,
                Code = p.Code,
                Status = p.Status,
                StatusId = p.ProjectStatusId,
                StatusNameEn = _context.Set<ProjectStatusCatalogEntry>()
                    .Where(item => item.Id == p.ProjectStatusId && item.TenantId == p.TenantId && !item.IsDeleted)
                    .Select(item => item.NameEn).FirstOrDefault(),
                StatusNameAr = _context.Set<ProjectStatusCatalogEntry>()
                    .Where(item => item.Id == p.ProjectStatusId && item.TenantId == p.TenantId && !item.IsDeleted)
                    .Select(item => item.NameAr).FirstOrDefault(),
                Phase = p.Phase,
                PhaseId = p.ProjectPhaseId,
                PhaseNameEn = _context.Set<ProjectPhaseCatalogEntry>()
                    .Where(item => item.Id == p.ProjectPhaseId && item.TenantId == p.TenantId && !item.IsDeleted)
                    .Select(item => item.NameEn).FirstOrDefault(),
                PhaseNameAr = _context.Set<ProjectPhaseCatalogEntry>()
                    .Where(item => item.Id == p.ProjectPhaseId && item.TenantId == p.TenantId && !item.IsDeleted)
                    .Select(item => item.NameAr).FirstOrDefault(),
                Health = p.Health,
                RagStatus = p.RagStatus,
                Progress = p.Progress,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Budget = p.Budget,
                ActualCost = p.ActualCost,
                Currency = p.Currency,
                ProjectManagerId = _context.Set<User>().Any(user =>
                    user.Id == p.ProjectManagerId && user.TenantId == p.TenantId && user.IsActive && !user.IsDeleted)
                        ? p.ProjectManagerId
                        : null,
                ProjectManagerName = _context.Set<User>()
                    .Where(user => user.Id == p.ProjectManagerId
                        && user.TenantId == p.TenantId
                        && user.IsActive
                        && !user.IsDeleted)
                    .Select(user => user.FullName)
                    .FirstOrDefault(),
                WorkingCalendarId = p.WorkingCalendarId
            })
            .ToListAsync(cancellationToken);

        await PopulateLifecycleSummaryDataAsync(summaries, cancellationToken);

        if (_planProgressLookup is not null && summaries.Count > 0)
        {
            var progressByProject = await _planProgressLookup.GetManyAsync(
                summaries.Select(summary => summary.Id), cancellationToken);
            foreach (var summary in summaries)
            {
                if (progressByProject.TryGetValue(summary.Id, out var planProgress))
                {
                    summary.PlanProgress = planProgress;
                }
            }
        }

        return new PagedResult<ProjectSummaryDto>(summaries, page, pageSize, total);
    }

    public async Task<Result<ProjectDto>> CreateAsync(CreateProjectDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == Guid.Empty || !HasManagementScope(tenantId))
            return Result<ProjectDto>.Failure(Error.Forbidden("A valid tenant management context is required."));

        if (dto.InitialAnnualBudgetAmount.HasValue
            && (dto.InitialAnnualBudgetAmount.Value < 0m
                || dto.InitialAnnualBudgetAmount.Value > 999999999999999.9999m
                || decimal.Round(dto.InitialAnnualBudgetAmount.Value, 4) != dto.InitialAnnualBudgetAmount.Value))
            return Result<ProjectDto>.Failure(Error.Validation("Initial Annual Budget must be non-negative and use at most four decimal places."));
        var initialProjectManagerId = dto.ProjectManagerId;
        if (initialProjectManagerId.HasValue
            && !await IsEligibleProjectManagerAsync(initialProjectManagerId.Value, tenantId, cancellationToken))
        {
            return Result<ProjectDto>.Failure(Error.Validation("The Project Manager must be an active same-tenant user with an active Management-category role and effective Handover.Approve permission."));
        }

        var normalizedCode = dto.Code?.Trim();
        var generatedCode = await GenerateUniqueProjectCodeAsync(cancellationToken);
        var finalCode = string.IsNullOrWhiteSpace(normalizedCode) ? generatedCode : normalizedCode;

        if (!string.IsNullOrWhiteSpace(normalizedCode) &&
            await _context.Set<Project>().AnyAsync(p => p.Code.Trim().ToUpper() == normalizedCode.ToUpper() && !p.IsDeleted, cancellationToken))
        {
            return Result<ProjectDto>.Failure(Error.Conflict("A project with this code already exists"));
        }

        var hierarchy = await ResolveHierarchyAsync(dto.ProgramId, dto.PortfolioId, dto.Currency, cancellationToken);
        if (hierarchy.Error is not null) return Result<ProjectDto>.Failure(hierarchy.Error);
        var lifecycleOrganizationId = await ResolveLifecycleOrganizationIdAsync(hierarchy.PortfolioId, dto.DepartmentId, cancellationToken);
        if (!lifecycleOrganizationId.HasValue)
            return Result<ProjectDto>.Failure(Error.Validation(
                "Project ownership must resolve to an active Organization in the current tenant through its Program, Portfolio, or Department."));
        if (_lifecycleCatalog is not null)
        {
            var lifecycleActorId = _managementContext?.Current?.UserId ?? TryParseGuid(userId) ?? Guid.Empty;
            if (lifecycleActorId == Guid.Empty)
                return Result<ProjectDto>.Failure(Error.Forbidden("A valid actor is required to provision the protected Project phase."));
            var preInitiation = await _lifecycleCatalog.EnsurePreInitiationPhaseAsync(
                tenantId, lifecycleOrganizationId, lifecycleActorId, cancellationToken);
            if (preInitiation.IsFailure)
                return Result<ProjectDto>.Failure(preInitiation.Error!);
        }
        var lifecycleSelection = await ResolveLifecycleSelectionAsync(
            tenantId, lifecycleOrganizationId, dto.PhaseId, dto.StatusId, dto.Status, cancellationToken);
        if (lifecycleSelection.Error is not null) return Result<ProjectDto>.Failure(lifecycleSelection.Error);
        var project = dto.ToEntity(userId);
        project.ProjectPhaseId = lifecycleSelection.Phase!.Id;
        project.ProjectStatusId = lifecycleSelection.Status!.Id;
        // Retain the stable catalog codes for older read integrations. Lifecycle behavior uses the IDs.
        project.Phase = lifecycleSelection.Phase.Code;
        project.Status = lifecycleSelection.Status.Code;
        project.Currency = hierarchy.Currency!;
        project.PortfolioId = hierarchy.PortfolioId;
        project.Code = finalCode;
        project.TenantId = tenantId;
        project.ProjectManagerId = initialProjectManagerId;

        ProjectAnnualBudget? initialAnnualBudget = null;
        if (dto.InitialAnnualBudgetAmount.HasValue)
        {
            if (!project.Budget.HasValue)
                return Result<ProjectDto>.Failure(Error.Validation("Set the Project lifetime financial ceiling before preparing an Initial Annual Budget."));
            if (dto.InitialAnnualBudgetAmount.Value > project.Budget.Value)
                return Result<ProjectDto>.Failure(Error.Validation("The Initial Annual Budget cannot exceed the Project lifetime financial ceiling."));
            if (_effectivePermissions is null || _tenantFiscalYear is null || !Guid.TryParse(userId, out var financeActorId))
                return Result<ProjectDto>.Failure(Error.Forbidden("ProjectAnnualBudget.Create and the tenant Fiscal Year configuration are required to prepare an Initial Annual Budget."));
            var financePermissions = await _effectivePermissions.GetEffectivePermissionsAsync(financeActorId, project.TenantId, cancellationToken);
            if (!financePermissions.Contains("ProjectAnnualBudget.Create"))
                return Result<ProjectDto>.Failure(Error.Forbidden("ProjectAnnualBudget.Create is required to prepare an Initial Annual Budget during Project creation."));
            var fiscalYear = await _tenantFiscalYear.ResolveForTimestampAsync(project.TenantId, DateTimeOffset.UtcNow, cancellationToken);
            var now = DateTime.UtcNow;
            initialAnnualBudget = new ProjectAnnualBudget
            {
                TenantId = project.TenantId,
                ProjectId = project.Id,
                FiscalYear = fiscalYear.FiscalYear,
                FiscalYearStartDate = fiscalYear.StartDate,
                FiscalYearEndDate = fiscalYear.EndDate,
                FinancialReference = MakeInitialAnnualBudgetReference(project.Code, fiscalYear.FiscalYear),
                Currency = project.Currency.Trim().ToUpperInvariant(),
                NewApprovedFunding = dto.InitialAnnualBudgetAmount.Value,
                ApprovedCarryForward = 0m,
                ApprovedAdjustments = 0m,
                Status = ProjectAnnualBudgetStatuses.Draft,
                CreatedAt = now,
                CreatedBy = userId
            };
        }

        var sponsorError = await _sponsorMembershipSynchronizer.ValidateAssignmentAsync(project.SponsorId, project.TenantId, cancellationToken);
        if (sponsorError is not null) return Result<ProjectDto>.Failure(sponsorError);

        if (project.WorkingCalendarId.HasValue)
        {
            var selectedCalendar = await _context.Set<WorkingCalendar>()
                .Include(calendar => calendar.DayRules)
                .Where(calendar => calendar.TenantId == project.TenantId
                    && calendar.Id == project.WorkingCalendarId.Value && !calendar.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (selectedCalendar?.HasValidSchedule != true)
                return Result<ProjectDto>.Failure(Error.Validation("The selected project calendar is unavailable or has no valid working schedule."));
        }
        else
        {
            var defaultCalendar = await _context.Set<WorkingCalendar>()
                .Include(calendar => calendar.DayRules)
                .Where(calendar => calendar.TenantId == project.TenantId && calendar.IsDefault && !calendar.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            project.WorkingCalendarId = defaultCalendar?.HasValidSchedule == true ? defaultCalendar.Id : null;
        }

        await using var createTransaction = initialAnnualBudget is null
            ? null
            : await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.Set<Project>().Add(project);
        await _sponsorMembershipSynchronizer.SynchronizeAsync(project, null, project.SponsorId, userId, cancellationToken);

        if (initialProjectManagerId.HasValue)
        {
            var managerStartDate = project.StartDate ?? DateTime.UtcNow.Date;
            _context.Set<ProjectManagerAssignment>().Add(new ProjectManagerAssignment
            {
                TenantId = project.TenantId,
                ProjectId = project.Id,
                ProjectManagerId = initialProjectManagerId.Value,
                StartDate = managerStartDate,
                IsCurrent = true,
                CreatedBy = userId
            });
            await _membershipSynchronizer.SynchronizeAsync(
                project, null, initialProjectManagerId.Value, managerStartDate, userId, cancellationToken);
        }

        if (project.Budget.HasValue)
        {
            var budgetRecordedAt = DateTime.UtcNow;
            await _auditService.AppendAsync(new AuditEntry
            {
                ProjectId = project.Id,
                TenantId = project.TenantId,
                EntityType = "ProjectBudget",
                EntityId = project.Id,
                Action = "LifetimeCeilingRecorded",
                UserId = TryParseGuid(userId),
                UserName = CurrentUserName,
                Timestamp = budgetRecordedAt,
                Description = $"Initial approved Project lifetime financial ceiling recorded for {project.Code}",
                Changes = JsonSerializer.Serialize(new
                {
                    financialReference = new { from = (string?)null, to = project.Code },
                    amount = new { from = (decimal?)null, to = project.Budget.Value },
                    currency = new { from = (string?)null, to = project.Currency }
                }),
                OriginalValues = JsonSerializer.Serialize(new { financialReference = (string?)null, amount = (decimal?)null, currency = (string?)null }),
                NewValues = JsonSerializer.Serialize(new { financialReference = project.Code, amount = project.Budget.Value, currency = project.Currency }),
                CreatedBy = userId
            }, cancellationToken);
        }

        if (initialAnnualBudget is not null)
        {
            _context.Set<ProjectAnnualBudget>().Add(initialAnnualBudget);
            await _auditService.AppendAsync(new AuditEntry
            {
                ProjectId = project.Id,
                TenantId = project.TenantId,
                EntityType = "ProjectAnnualBudget",
                EntityId = initialAnnualBudget.Id,
                Action = "InitialDraftCreated",
                UserId = TryParseGuid(userId),
                UserName = CurrentUserName,
                Timestamp = DateTime.UtcNow,
                Description = $"Initial Draft Annual Budget prepared for FY {initialAnnualBudget.FiscalYear} for {project.Code}; it is not approved for spending.",
                Changes = JsonSerializer.Serialize(new
                {
                    fiscalYear = new { from = (int?)null, to = initialAnnualBudget.FiscalYear },
                    newApprovedFunding = new { from = (decimal?)null, to = initialAnnualBudget.NewApprovedFunding },
                    currency = new { from = (string?)null, to = initialAnnualBudget.Currency },
                    status = new { from = (string?)null, to = ProjectAnnualBudgetStatuses.Draft }
                }),
                NewValues = JsonSerializer.Serialize(new
                {
                    financialReference = initialAnnualBudget.FinancialReference,
                    fiscalYear = initialAnnualBudget.FiscalYear,
                    newApprovedFunding = initialAnnualBudget.NewApprovedFunding,
                    currency = initialAnnualBudget.Currency,
                    status = ProjectAnnualBudgetStatuses.Draft
                }),
                CreatedBy = userId
            }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Project",
            EntityId = project.Id,
            Action = "Create",
            TenantId = project.TenantId,
            UserId = TryParseGuid(userId),
            UserName = CurrentUserName,
            Changes = JsonSerializer.Serialize(new { name = project.Name, code = project.Code, status = project.Status }),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        if (createTransaction is not null)
            await createTransaction.CommitAsync(cancellationToken);

        var resultDto = project.ToDto();
        await ResolveProjectNamesAsync(resultDto, cancellationToken);
        return Result<ProjectDto>.Success(resultDto);
    }

    private Guid GetCurrentTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            if (httpContext.User.Identity?.IsAuthenticated != true) return Guid.Empty;
            var tenantId = httpContext.User.FindFirst("tenantId")?.Value;
            return Guid.TryParse(tenantId, out var parsed) && parsed != Guid.Empty ? parsed : Guid.Empty;
        }

        return TrustedTenantExecutionContext.CurrentTenantId ?? Guid.Empty;
    }

    private bool HasManagementScope(Guid tenantId)
    {
        if (_httpContextAccessor.HttpContext is null)
            return TrustedTenantExecutionContext.CurrentTenantId == tenantId;

        var context = _managementContext?.Current;
        return context is not null && context.TenantId == tenantId;
    }

    private static string MakeInitialAnnualBudgetReference(string projectCode, int fiscalYear)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var maximumCodeLength = Math.Max(1, 99 - suffix.Length - fiscalYear.ToString().Length - 10);
        var code = projectCode.Length > maximumCodeLength ? projectCode[..maximumCodeLength] : projectCode;
        return $"{code}-FY{fiscalYear}-AB-{suffix}";
    }

    private IQueryable<Project> InScope(IQueryable<Project> query)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == Guid.Empty) return query.Where(_ => false);

        query = query.Where(project => project.TenantId == tenantId);
        if (_httpContextAccessor.HttpContext is null)
            return TrustedTenantExecutionContext.CurrentTenantId == tenantId ? query : query.Where(_ => false);

        var context = _managementContext?.Current;
        if (context is null || context.TenantId != tenantId) return query.Where(_ => false);
        return query.Where(project => context.Scope.ProjectIds.Contains(project.Id));
    }

    private static Guid? TryParseGuid(string value)
    {
        return GuidHelper.TryParse(value);
    }

    private string? CurrentUserName => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value
        ?? _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value;

    private static Dictionary<string, object?> Snapshot(Project project) => new()
    {
        ["name"] = project.Name,
        ["code"] = project.Code,
        ["description"] = project.Description,
        ["status"] = project.Status,
        ["priority"] = project.Priority,
        ["category"] = project.Category,
        ["sponsorId"] = project.SponsorId,
        ["startDate"] = project.StartDate,
        ["endDate"] = project.EndDate,
        ["currency"] = project.Currency,
        ["budget"] = project.Budget,
        ["actualCost"] = project.ActualCost,
        ["plannedValue"] = project.PlannedValue,
        ["earnedValue"] = project.EarnedValue,
        ["confidenceLevel"] = project.ConfidenceLevel,
        ["businessCase"] = project.BusinessCase,
        ["notes"] = project.Notes,
        ["workingCalendarId"] = project.WorkingCalendarId
    };

    private static Dictionary<string, object?> Snapshot(ProjectMember member) => new()
    {
        ["role"] = member.Role,
        ["allocationPercentage"] = member.AllocationPercentage,
        ["hoursPerWeek"] = member.HoursPerWeek,
        ["startDate"] = member.StartDate,
        ["endDate"] = member.EndDate,
        ["status"] = member.Status,
        ["isBillable"] = member.IsBillable,
        ["notes"] = ProjectSponsorMembershipSynchronizer.IsManagedMembership(member) ? null : member.Notes
    };

    private static Dictionary<string, object?> BuildChanges(Dictionary<string, object?> before, Dictionary<string, object?> after)
        => after
            .Where(pair => !Equals(before.GetValueOrDefault(pair.Key), pair.Value))
            .ToDictionary(pair => pair.Key, pair => (object?)new { from = before.GetValueOrDefault(pair.Key), to = pair.Value });

    private static string? SerializeChanges(Dictionary<string, object?> changes) => changes.Count == 0 ? null : JsonSerializer.Serialize(changes);

    private async Task<string> GenerateUniqueProjectCodeAsync(CancellationToken cancellationToken)
    {
        const string prefix = "PRJ-";
        var existingCodes = await _context.Set<Project>()
            .Where(p => !p.IsDeleted && p.Code.ToUpper().StartsWith(prefix))
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);

        var nextNumber = 1001;
        foreach (var code in existingCodes)
        {
            if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(code[prefix.Length..], out var number) && number >= nextNumber)
            {
                nextNumber = number + 1;
            }
        }

        return $"{prefix}{nextNumber}";
    }

    public async Task<Result<ProjectDto>> UpdateAsync(Guid id, UpdateProjectDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (project is null) return Result<ProjectDto>.Failure(Error.NotFound("Project not found"));

        if (dto.ProjectManagerIdWasProvided && dto.ProjectManagerId != project.ProjectManagerId)
        {
            var assignmentWorkflow = project.ProjectManagerId.HasValue ? "Handover" : "Initial Assignment";
            return Result<ProjectDto>.Failure(Error.Conflict($"Project Manager changes must use the authorized {assignmentWorkflow} workflow."));
        }
        if (dto.Handover is not null)
            return Result<ProjectDto>.Failure(Error.Conflict("Project Update does not accept Handover data. Use the authorized Handover workflow."));
        if (dto.Status is not null)
            return Result<ProjectDto>.Failure(Error.Conflict("Project Status changes must use the authorized Project Status action."));

        // Creation is the existing setup/approval boundary; Status is not a draft approval gate.
        if (dto.Budget.HasValue && dto.Budget != project.Budget)
            return Result<ProjectDto>.Failure(Error.Validation("Project budget changes require an approved Project Change Request."));
        if (dto.Currency is not null
            && !string.Equals(dto.Currency.Trim(), project.Currency.Trim(), StringComparison.OrdinalIgnoreCase)
            && await HasFinancialHistoryAsync(project, cancellationToken))
            return Result<ProjectDto>.Failure(Error.Conflict("Project currency cannot be changed after a financial ceiling or financial records exist. No historical currency conversion is inferred."));
        var hierarchy = await ResolveHierarchyAsync(dto.ProgramId, dto.PortfolioId, dto.Currency, cancellationToken, project);
        if (hierarchy.Error is not null) return Result<ProjectDto>.Failure(hierarchy.Error);
        if (dto.Currency != null && !await IsValidCurrencyAsync(dto.Currency.Trim(), cancellationToken))
            return Result<ProjectDto>.Failure(Error.Validation("Currency must be an active configured currency"));

        var candidateProgramId = dto.ProgramId ?? project.ProgramId;
        var candidateDepartmentId = dto.DepartmentId ?? project.DepartmentId;
        var ownershipReassigned = candidateProgramId != project.ProgramId
            || hierarchy.PortfolioId != project.PortfolioId
            || candidateDepartmentId != project.DepartmentId;
        if (ownershipReassigned)
        {
            var reassignedOrganizationId = await ResolveLifecycleOrganizationIdAsync(
                hierarchy.PortfolioId, candidateDepartmentId, cancellationToken);
            if (!reassignedOrganizationId.HasValue)
                return Result<ProjectDto>.Failure(Error.Validation(
                    "Project ownership reassignment must resolve to an active Organization in the current tenant through its Program, Portfolio, or Department."));
        }

        var previousSponsorId = project.SponsorId;
        var nextSponsorId = dto.ClearSponsor ? null : dto.SponsorId ?? project.SponsorId;
        var sponsorChanged = nextSponsorId != project.SponsorId;
        if (sponsorChanged)
        {
            var sponsorError = await _sponsorMembershipSynchronizer.ValidateAssignmentAsync(nextSponsorId, project.TenantId, cancellationToken);
            if (sponsorError is not null) return Result<ProjectDto>.Failure(sponsorError);
        }

        var oldValues = System.Text.Json.JsonSerializer.Serialize(project.ToDto());
        var beforeValues = Snapshot(project);

        if (dto.WorkingCalendarId.HasValue)
        {
            var selectedCalendar = await _context.Set<WorkingCalendar>()
                .Include(calendar => calendar.DayRules)
                .Where(calendar => calendar.TenantId == project.TenantId
                    && calendar.Id == dto.WorkingCalendarId.Value && !calendar.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (selectedCalendar?.HasValidSchedule != true)
                return Result<ProjectDto>.Failure(Error.Validation("The selected project calendar is unavailable or has no valid working schedule."));
        }

        dto.UpdateEntity(project, userId);
        if (hierarchy.PortfolioId.HasValue) project.PortfolioId = hierarchy.PortfolioId;
        if (sponsorChanged)
            await _sponsorMembershipSynchronizer.SynchronizeAsync(project, previousSponsorId, project.SponsorId, userId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Project",
            EntityId = project.Id,
            Action = "Update",
            TenantId = project.TenantId,
            UserId = TryParseGuid(userId),
            UserName = CurrentUserName,
            Changes = SerializeChanges(BuildChanges(beforeValues, Snapshot(project))),
            OriginalValues = oldValues,
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        var resultDto = project.ToDto();
        await ResolveProjectNamesAsync(resultDto, cancellationToken);
        return Result<ProjectDto>.Success(resultDto);
    }

    public async Task<Result<ProjectDto>> ChangeStatusAsync(
        Guid id,
        ChangeProjectStatusDto dto,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var project = await InScope(_context.Set<Project>())
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (project is null) return Result<ProjectDto>.Failure(Error.NotFound("Project not found."));
        if (!Guid.TryParse(userId, out var actorId) || actorId == Guid.Empty)
            return Result<ProjectDto>.Failure(Error.Unauthorized("A valid user identity is required."));
        if (_effectivePermissions is null)
            return Result<ProjectDto>.Failure(Error.Forbidden("Project Status authorization is unavailable."));
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorId, project.TenantId, cancellationToken);
        if (!permissions.Contains("ProjectStatus.Change"))
            return Result<ProjectDto>.Failure(Error.Forbidden("ProjectStatus.Change is required to change Project Status."));
        if (dto.TargetStatusId == Guid.Empty)
            return Result<ProjectDto>.Failure(Error.Validation("A target Project Status is required.", "targetStatusId"));
        if (!project.ProjectStatusId.HasValue)
            return Result<ProjectDto>.Failure(Error.Conflict("This project has no linked lifecycle Status. Resolve its legacy Status mapping before changing it."));

        var source = await _context.Set<ProjectStatusCatalogEntry>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == project.ProjectStatusId.Value
                && item.TenantId == project.TenantId && !item.IsDeleted, cancellationToken);
        var target = await _context.Set<ProjectStatusCatalogEntry>()
            .FirstOrDefaultAsync(item => item.Id == dto.TargetStatusId && item.TenantId == project.TenantId
                && item.IsActive && !item.IsDeleted, cancellationToken);
        if (source is null || target is null)
            return Result<ProjectDto>.Failure(Error.Validation("The current or selected Project Status is unavailable."));
        if (source.Id == target.Id)
            return Result<ProjectDto>.Failure(Error.Validation("The project already has the selected Status."));

        var organizationId = _projectOrganizationLookup is null
            ? null
            : await _projectOrganizationLookup.ResolveOrganizationIdAsync(project.TenantId, project.Id, cancellationToken);
        if (source.OrganizationId.HasValue && source.OrganizationId != organizationId)
            return Result<ProjectDto>.Failure(Error.Conflict("The project's current Status belongs to a different Organization scope."));
        if (target.OrganizationId.HasValue && target.OrganizationId != organizationId)
            return Result<ProjectDto>.Failure(Error.Validation("The target Status belongs to a different Organization scope."));
        var reason = dto.Reason?.Trim();
        if (reason?.Length > 2000)
            return Result<ProjectDto>.Failure(Error.Validation("The Status change reason cannot exceed 2,000 characters.", "reason"));

        var previousStatusId = source.Id;
        var previousStatusCode = source.Code;
        var previousLegacyStatus = project.Status;
        project.ProjectStatusId = target.Id;
        project.Status = target.Code;
        project.UpdatedBy = userId;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync(new AuditEntry
            {
                EntityType = "Project",
                EntityId = project.Id,
                Action = "StatusChanged",
                TenantId = project.TenantId,
                UserId = actorId,
                UserName = CurrentUserName,
                Changes = JsonSerializer.Serialize(new
                {
                    status = new
                    {
                        fromId = previousStatusId, from = previousStatusCode,
                        toId = target.Id, to = target.Code, legacyFrom = previousLegacyStatus
                    },
                    reason
                }),
                OriginalValues = JsonSerializer.Serialize(new { id = previousStatusId, code = previousStatusCode, legacyCode = previousLegacyStatus }),
                NewValues = JsonSerializer.Serialize(new { id = target.Id, code = target.Code, reason }),
                Timestamp = DateTime.UtcNow,
                CreatedBy = userId
            }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<ProjectDto>.Failure(Error.Conflict("Project Status changed concurrently. Reload the project and try again."));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        var resultDto = project.ToDto();
        await ResolveProjectNamesAsync(resultDto, cancellationToken);
        return Result<ProjectDto>.Success(resultDto);
    }

    private async Task<bool> IsEligibleProjectManagerAsync(Guid candidateId, Guid tenantId, CancellationToken cancellationToken)
    {
        var isActiveTenantUser = await _context.Set<User>().AsNoTracking()
            .AnyAsync(user => user.Id == candidateId && user.TenantId == tenantId && user.IsActive && !user.IsDeleted, cancellationToken);
        if (!isActiveTenantUser || _effectivePermissions is null)
            return false;

        var hasProjectManagerRole = await _context.Set<UserRole>().AsNoTracking()
            .AnyAsync(userRole => userRole.UserId == candidateId && userRole.TenantId == tenantId && !userRole.IsDeleted
                && _context.Set<Role>().Any(role => role.Id == userRole.RoleId && role.TenantId == tenantId
                    && !role.IsDeleted && role.Category == "Management"), cancellationToken);
        if (!hasProjectManagerRole)
            return false;

        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(candidateId, tenantId, cancellationToken);
        return permissions.Contains("Handover.Approve");
    }

    public async Task<Result<IReadOnlyList<ProjectManagerHistoryDto>>> GetManagerHistoryAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var assignments = await _context.Set<ProjectManagerAssignment>()
            .Where(a => a.ProjectId == projectId && !a.IsDeleted)
            .OrderBy(a => a.StartDate)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var userIds = assignments
            .SelectMany(a => new[] { a.ProjectManagerId }.Concat(a.HandoverFromProjectManagerId.HasValue ? new[] { a.HandoverFromProjectManagerId.Value } : Array.Empty<Guid>()))
            .Distinct()
            .ToList();

        var users = new Dictionary<Guid, User>();
        if (userIds.Count > 0)
        {
            users = await _context.Set<User>()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        var history = assignments.Select(a =>
        {
            var dto = a.ToHistoryDto();
            if (users.TryGetValue(a.ProjectManagerId, out var manager))
            {
                dto.ProjectManagerName = manager.FullName;
                dto.ProjectManagerEmail = manager.Email;
            }
            if (a.HandoverFromProjectManagerId.HasValue && users.TryGetValue(a.HandoverFromProjectManagerId.Value, out var previous))
            {
                dto.HandoverFromProjectManagerName = previous.FullName;
            }
            return dto;
        }).ToList();

        return Result<IReadOnlyList<ProjectManagerHistoryDto>>.Success(history);
    }

    private async Task ResolveProjectNamesAsync(ProjectDto dto, CancellationToken cancellationToken)
    {
        var userIds = new List<Guid>();
        if (dto.ProjectManagerId.HasValue) userIds.Add(dto.ProjectManagerId.Value);
        if (dto.SponsorId.HasValue) userIds.Add(dto.SponsorId.Value);
        userIds = userIds.Distinct().ToList();

        var users = new Dictionary<Guid, User>();
        if (userIds.Count > 0)
        {
            users = await _context.Set<User>()
                .Where(u => userIds.Contains(u.Id) && u.TenantId == dto.TenantId && u.IsActive && !u.IsDeleted)
                .ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        if (dto.ProjectManagerId.HasValue && users.TryGetValue(dto.ProjectManagerId.Value, out var manager))
        {
            dto.ProjectManagerName = manager.FullName;
        }
        else
        {
            dto.ProjectManagerId = null;
            dto.ProjectManagerName = null;
        }

        if (dto.SponsorId.HasValue && users.TryGetValue(dto.SponsorId.Value, out var sponsor))
        {
            dto.SponsorName = sponsor.FullName;
        }

        if (dto.PortfolioId.HasValue)
        {
            var portfolio = await _context.Set<PortfolioEntity>().FirstOrDefaultAsync(p => p.Id == dto.PortfolioId.Value, cancellationToken);
            dto.PortfolioName = portfolio?.Name;
        }

        if (dto.DepartmentId.HasValue)
        {
            var department = await _context.Set<Department>().FirstOrDefaultAsync(d => d.Id == dto.DepartmentId.Value, cancellationToken);
            dto.DepartmentName = department?.Name;
        }

        Guid? lifecycleOrganizationId = null;
        if (_projectOrganizationLookup is not null)
            lifecycleOrganizationId = await _projectOrganizationLookup.ResolveOrganizationIdAsync(dto.TenantId, dto.Id, cancellationToken);
        else
            lifecycleOrganizationId = await ResolveLifecycleOrganizationIdAsync(dto.PortfolioId, dto.DepartmentId, cancellationToken);

        if (dto.StatusId.HasValue)
        {
            var status = await _context.Set<ProjectStatusCatalogEntry>().AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == dto.StatusId.Value && item.TenantId == dto.TenantId
                    && !item.IsDeleted && (item.OrganizationId == null || item.OrganizationId == lifecycleOrganizationId), cancellationToken);
            dto.StatusNameEn = status?.NameEn;
            dto.StatusNameAr = status?.NameAr;
            dto.ReportingCategory = status?.ReportingCategory;
            dto.AttentionLevel = status?.AttentionLevel;
        }

        if (dto.PhaseId.HasValue)
        {
            var phase = await _context.Set<ProjectPhaseCatalogEntry>().AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == dto.PhaseId.Value && item.TenantId == dto.TenantId
                    && !item.IsDeleted && (item.OrganizationId == null || item.OrganizationId == lifecycleOrganizationId), cancellationToken);
            dto.PhaseNameEn = phase?.NameEn;
            dto.PhaseNameAr = phase?.NameAr;
        }
    }

    private async Task PopulateLifecycleSummaryDataAsync(
        IReadOnlyCollection<ProjectSummaryDto> summaries,
        CancellationToken cancellationToken)
    {
        if (summaries.Count == 0) return;
        var statusIds = summaries.Where(item => item.StatusId.HasValue).Select(item => item.StatusId!.Value).Distinct().ToArray();
        var phaseIds = summaries.Where(item => item.PhaseId.HasValue).Select(item => item.PhaseId!.Value).Distinct().ToArray();
        var tenantId = GetCurrentTenantId();
        var statuses = statusIds.Length == 0 ? [] : await _context.Set<ProjectStatusCatalogEntry>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && statusIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var phases = phaseIds.Length == 0 ? [] : await _context.Set<ProjectPhaseCatalogEntry>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && phaseIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var scope = _managementContext?.Current?.Scope;
        foreach (var summary in summaries)
        {
            Guid? organizationId = null;
            if (scope is not null && scope.ProjectOrganizationIds.TryGetValue(summary.Id, out var scopedOrganizationId)
                && scopedOrganizationId != Guid.Empty)
                organizationId = scopedOrganizationId;
            else if (_projectOrganizationLookup is not null)
                organizationId = await _projectOrganizationLookup.ResolveOrganizationIdAsync(tenantId, summary.Id, cancellationToken);

            var status = summary.StatusId.HasValue
                ? statuses.FirstOrDefault(item => item.Id == summary.StatusId.Value
                    && (item.OrganizationId is null || item.OrganizationId == organizationId))
                : null;
            summary.StatusNameEn = status?.NameEn;
            summary.StatusNameAr = status?.NameAr;
            summary.ReportingCategory = status?.ReportingCategory;
            summary.AttentionLevel = status?.AttentionLevel;

            var phase = summary.PhaseId.HasValue
                ? phases.FirstOrDefault(item => item.Id == summary.PhaseId.Value
                    && (item.OrganizationId is null || item.OrganizationId == organizationId))
                : null;
            summary.PhaseNameEn = phase?.NameEn;
            summary.PhaseNameAr = phase?.NameAr;
        }
    }

    private async Task<Guid?> ResolveLifecycleOrganizationIdAsync(Guid? portfolioId, Guid? departmentId, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        Guid? organizationId = null;
        if (portfolioId.HasValue)
        {
            organizationId = await _context.Set<PortfolioEntity>().AsNoTracking()
                .Where(portfolio => portfolio.Id == portfolioId.Value && portfolio.TenantId == tenantId && !portfolio.IsDeleted)
                .Select(portfolio => portfolio.OrganizationId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!organizationId.HasValue && departmentId.HasValue)
        {
            organizationId = await (
                from department in _context.Set<Department>().AsNoTracking()
                join businessUnit in _context.Set<BusinessUnit>().AsNoTracking()
                    on new { department.TenantId, department.BusinessUnitId }
                    equals new { businessUnit.TenantId, BusinessUnitId = businessUnit.Id }
                where department.Id == departmentId.Value && department.TenantId == tenantId
                    && department.IsActive && !department.IsDeleted && businessUnit.IsActive && !businessUnit.IsDeleted
                select (Guid?)businessUnit.OrganizationId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!organizationId.HasValue) return null;
        return await _context.Set<OrganizationEntity>().AsNoTracking()
            .Where(organization => organization.Id == organizationId.Value && organization.TenantId == tenantId
                && organization.IsActive && !organization.IsDeleted)
            .Select(organization => (Guid?)organization.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<(Error? Error, ProjectPhaseCatalogEntry? Phase, ProjectStatusCatalogEntry? Status)> ResolveLifecycleSelectionAsync(
        Guid tenantId,
        Guid? organizationId,
        Guid? requestedPhaseId,
        Guid? requestedStatusId,
        string? legacyStatus,
        CancellationToken cancellationToken)
    {
        var phaseRows = await _context.Set<ProjectPhaseCatalogEntry>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive
                && (item.OrganizationId == null || item.OrganizationId == organizationId))
            .ToListAsync(cancellationToken);
        var statusRows = await _context.Set<ProjectStatusCatalogEntry>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive
                && (item.OrganizationId == null || item.OrganizationId == organizationId))
            .ToListAsync(cancellationToken);

        var phases = phaseRows.GroupBy(item => item.NormalizedCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => organizationId.HasValue && item.OrganizationId == organizationId).First())
            .ToArray();
        var statuses = statusRows.GroupBy(item => item.NormalizedCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => organizationId.HasValue && item.OrganizationId == organizationId).First())
            .ToArray();

        var defaultPhases = phases.Where(item => item.IsDefault).ToArray();
        var defaultStatuses = statuses.Where(item => item.IsDefault).ToArray();
        if (defaultPhases.Length != 1 || defaultStatuses.Length != 1)
            return (Error.Validation("One active default phase and one active default status must be configured for the project scope."), null, null);

        var phase = requestedPhaseId.HasValue
            ? phases.SingleOrDefault(item => item.Id == requestedPhaseId.Value)
            : defaultPhases[0];
        if (phase is null)
            return (Error.Validation("The selected project phase is unavailable in the authorized project scope.", "phaseId"), null, null);

        if (requestedStatusId.HasValue && !string.IsNullOrWhiteSpace(legacyStatus))
            return (Error.Validation("Provide a Project Status ID or a legacy status code, not both.", "statusId"), null, null);
        var normalizedLegacy = legacyStatus?.Trim();
        ProjectStatusCatalogEntry? status;
        if (requestedStatusId.HasValue)
        {
            status = statuses.SingleOrDefault(item => item.Id == requestedStatusId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(normalizedLegacy))
        {
            var legacyMatches = statuses.Where(item =>
                string.Equals(item.Code, normalizedLegacy, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.NameEn, normalizedLegacy, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.NameAr, normalizedLegacy, StringComparison.OrdinalIgnoreCase)).ToArray();
            status = legacyMatches.Length == 1 ? legacyMatches[0] : null;
            if (legacyMatches.Length > 1)
                return (Error.Validation("The legacy Project Status is ambiguous; select a catalog Status ID.", "statusId"), null, null);
        }
        else
        {
            status = defaultStatuses[0];
        }

        if (status is null)
            return (Error.Validation("The selected Project Status is unavailable in the authorized project scope.", "statusId"), null, null);
        return (null, phase, status);
    }

    private async Task<(Error? Error, Guid? PortfolioId, string? Currency)> ResolveHierarchyAsync(Guid? programId, Guid? portfolioId, string? requestedCurrency, CancellationToken ct, Project? existing = null)
    {
        var tenant = GetCurrentTenantId();
        if (tenant == Guid.Empty) return (Error.Unauthorized("A valid tenant context is required."), null, null);
        programId ??= existing?.ProgramId;
        portfolioId ??= existing?.PortfolioId;
        Guid? resolvedPortfolio = portfolioId;
        if (programId.HasValue)
        {
            var program = await _context.Set<ProgramEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == programId.Value && !x.IsDeleted, ct);
            if (program is null || program.TenantId != tenant) return (Error.Validation("Program must exist in the current tenant"), null, null);
            if (portfolioId.HasValue && portfolioId.Value != program.PortfolioId) return (Error.Validation("Project Portfolio must match the selected Program Portfolio"), null, null);
            var programPortfolioExists = await _context.Set<PortfolioEntity>().AsNoTracking()
                .AnyAsync(x => x.Id == program.PortfolioId && x.TenantId == tenant && !x.IsDeleted, ct);
            if (!programPortfolioExists) return (Error.Validation("Program Portfolio must exist in the current tenant"), null, null);
            var hierarchyChanged = existing is null || existing.ProgramId != program.Id || existing.PortfolioId != program.PortfolioId;
            if (hierarchyChanged && !HasHierarchyAssignmentScope(program.Id, program.PortfolioId))
                return (Error.NotFound(), null, null);
            resolvedPortfolio = program.PortfolioId;
            var currency = string.IsNullOrWhiteSpace(requestedCurrency) ? (existing?.Currency ?? program.Currency) : requestedCurrency.Trim().ToUpperInvariant();
            if (!await IsValidCurrencyAsync(currency, ct)) return (Error.Validation("Currency must be an active configured currency"), null, null);
            return (null, resolvedPortfolio, currency);
        }
        if (portfolioId.HasValue)
        {
            var portfolio = await _context.Set<PortfolioEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == portfolioId.Value && !x.IsDeleted, ct);
            if (portfolio is null || portfolio.TenantId != tenant) return (Error.Validation("Portfolio must exist in the current tenant"), null, null);
            var hierarchyChanged = existing is null || existing.ProgramId.HasValue || existing.PortfolioId != portfolio.Id;
            if (hierarchyChanged && !HasHierarchyAssignmentScope(null, portfolio.Id))
                return (Error.NotFound(), null, null);
            var currency = string.IsNullOrWhiteSpace(requestedCurrency) ? (existing?.Currency ?? portfolio.Currency) : requestedCurrency.Trim().ToUpperInvariant();
            if (!await IsValidCurrencyAsync(currency, ct)) return (Error.Validation("Currency must be an active configured currency"), null, null);
            return (null, portfolioId, currency);
        }
        if (existing is not null && string.IsNullOrWhiteSpace(requestedCurrency)) return (null, existing.PortfolioId, existing.Currency);
        var currentManagementContext = _managementContext?.Current;
        if (existing is null && _httpContextAccessor.HttpContext is not null && currentManagementContext is null)
            return (Error.NotFound(), null, null);
        if (existing is null && currentManagementContext is not null
            && currentManagementContext.SelectedPerspective is (ManagementPerspective.PortfolioManager or ManagementPerspective.ProgramManager)
            && !programId.HasValue && !portfolioId.HasValue)
            return (Error.Validation("A Project created in Portfolio or Program scope must be assigned to an in-scope Program or Portfolio"), null, null);
        if (existing is null && _managementContext?.Current?.SelectedProjectId.HasValue == true)
            return (Error.NotFound(), null, null);
        var standaloneCurrency = requestedCurrency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(standaloneCurrency) || !await IsValidCurrencyAsync(standaloneCurrency, ct)) return (Error.Validation("Currency is required and must be an active configured currency"), null, null);
        return (null, existing?.PortfolioId, standaloneCurrency);
    }

    private bool HasHierarchyAssignmentScope(Guid? programId, Guid? portfolioId)
    {
        var context = _managementContext?.Current;
        if (context is null) return _httpContextAccessor.HttpContext is null;
        if (programId.HasValue)
        {
            return context.SelectedPerspective is (ManagementPerspective.ProgramManager or ManagementPerspective.PortfolioManager)
                && context.Scope.ProgramIds.Contains(programId.Value);
        }
        if (portfolioId.HasValue)
        {
            return context.SelectedPerspective == ManagementPerspective.PortfolioManager
                && context.Scope.PortfolioIds.Contains(portfolioId.Value);
        }
        return true;
    }

    private async Task<bool> HasFinancialHistoryAsync(Project project, CancellationToken cancellationToken)
    {
        if (project.Budget.HasValue)
            return true;

        var tenantId = project.TenantId;
        var projectId = project.Id;
        if (await _context.Set<AuditEntry>().IgnoreQueryFilters().AnyAsync(entry =>
                entry.TenantId == tenantId && !entry.IsDeleted && entry.EntityType == "ProjectBudget"
                && (entry.ProjectId == projectId || entry.EntityId == projectId), cancellationToken))
            return true;

        return await _context.Set<ProjectAnnualBudget>().IgnoreQueryFilters().AnyAsync(item =>
                   item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted, cancellationToken)
            || await _context.Set<Budget>().IgnoreQueryFilters().AnyAsync(item =>
                   item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted, cancellationToken)
            || await _context.Set<CostEntry>().IgnoreQueryFilters().AnyAsync(item =>
                   item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted, cancellationToken)
            || await _context.Set<PurchaseCommitment>().IgnoreQueryFilters().AnyAsync(item =>
                   item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted, cancellationToken)
            || await _context.Set<PaymentObligation>().IgnoreQueryFilters().AnyAsync(item =>
                   item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted, cancellationToken)
            || await _context.Set<PaymentSettlement>().IgnoreQueryFilters().AnyAsync(item =>
                   item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted, cancellationToken)
            || await _context.Set<ProjectPaymentRequestDetail>().IgnoreQueryFilters().AnyAsync(item =>
                   item.TenantId == tenantId && item.ProjectId == projectId, cancellationToken);
    }

    private async Task<bool> IsValidCurrencyAsync(string code, CancellationToken ct) => await _context.Set<Currency>().AnyAsync(x => x.Code == code.ToUpper() && !x.IsDeleted, ct);

    public async Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (project is null) return Result.Failure(Error.NotFound("Project not found"));

        project.IsDeleted = true;
        project.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Project",
            EntityId = id,
            Action = "Delete",
            TenantId = project.TenantId,
            UserId = TryParseGuid(userId),
            UserName = CurrentUserName,
            Changes = JsonSerializer.Serialize(new { name = project.Name, code = project.Code }),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    public Task<Result> ArchiveAsync(Guid id, string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure(Error.Conflict("Fixed Archive status mutation is retired. Use the authorized Project Status action.")));

    public Task<Result> ReactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure(Error.Conflict("Fixed Reactivate status mutation is retired. Use the authorized Project Status action.")));

    public async Task<Result<IReadOnlyList<ProjectMemberDto>>> GetTeamAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var members = await _context.Set<ProjectMember>()
            .Include(m => m.Project)
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .OrderBy(m => m.Role)
            .ToListAsync(cancellationToken);

        var resourceIds = members.Where(member => member.ResourceId.HasValue).Select(member => member.ResourceId!.Value).Distinct().ToArray();
        var teamIds = members.Where(member => member.TeamId.HasValue).Select(member => member.TeamId!.Value).Distinct().ToArray();
        var resources = await _context.Set<Resource>().AsNoTracking()
            .Where(resource => resourceIds.Contains(resource.Id) && !resource.IsDeleted)
            .ToDictionaryAsync(resource => resource.Id, cancellationToken);
        var teams = await _context.Set<Team>().AsNoTracking()
            .Where(team => teamIds.Contains(team.Id) && !team.IsDeleted)
            .ToDictionaryAsync(team => team.Id, cancellationToken);
        var memberDtos = members.Select(member =>
        {
            var dto = member.ToDto();
            if (member.ResourceId is Guid resourceId && resources.TryGetValue(resourceId, out var resource))
            {
                dto.ResourceName = resource.Name;
                dto.ResourceJobTitle = resource.JobTitle;
            }
            if (member.TeamId is Guid teamId && teams.TryGetValue(teamId, out var team))
            {
                dto.TeamName = team.Name;
                dto.ResourceName = team.Name;
            }
            if (ProjectSponsorMembershipSynchronizer.IsManagedMembership(member)) dto.Notes = null;
            return dto;
        }).ToList();

        return Result<IReadOnlyList<ProjectMemberDto>>.Success(memberDtos);
    }

    public Task<bool> IsTeamGroupAssignmentAsync(Guid projectId, Guid memberId, CancellationToken cancellationToken = default) =>
        _context.Set<ProjectMember>().AsNoTracking().AnyAsync(member => member.Id == memberId
            && member.ProjectId == projectId && member.TeamId.HasValue && !member.IsDeleted, cancellationToken);

    public async Task<Result<ProjectMemberDto>> AddTeamMemberAsync(Guid projectId, CreateProjectMemberDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (ProjectSponsorMembershipSynchronizer.IsReservedManagedMembershipNote(dto.Notes))
            return Result<ProjectMemberDto>.Failure(Error.Validation("This Project Team note is reserved for the system-managed Sponsor relationship."));
        if (dto.AllocationPercentage is < 0m or > 100m)
            return Result<ProjectMemberDto>.Failure(Error.Validation("Project allocation must be between 0 and 100 percent."));
        if (dto.EndDate.Date < dto.StartDate.Date)
            return Result<ProjectMemberDto>.Failure(Error.Validation("Project team end date cannot be earlier than its start date."));
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);
        if (project is null) return Result<ProjectMemberDto>.Failure(Error.NotFound("Project not found"));

        Resource? resource = null;
        Team? team = null;
        if (dto.ResourceId.HasValue)
        {
            resource = await _context.Set<Resource>().FirstOrDefaultAsync(r => r.Id == dto.ResourceId.Value && !r.IsDeleted, cancellationToken);
            if (resource is null) return Result<ProjectMemberDto>.Failure(Error.NotFound("Resource not found"));
            if (!resource.IsActive) return Result<ProjectMemberDto>.Failure(Error.Validation("Resource is not active"));
            var conflicts = await CheckResourceConflictsInternalAsync(resource.Id, dto.StartDate, dto.EndDate, dto.AllocationPercentage, null, cancellationToken, projectId);
            if (conflicts.Count > 0) return ResourceAllocationConflict(conflicts);
        }
        else if (dto.TeamId.HasValue)
        {
            team = await _context.Set<Team>().FirstOrDefaultAsync(item => item.Id == dto.TeamId.Value && !item.IsDeleted, cancellationToken);
            if (team is null) return Result<ProjectMemberDto>.Failure(Error.NotFound("Team not found"));
            if (!team.IsActive || team.TenantId != project.TenantId)
                return Result<ProjectMemberDto>.Failure(Error.Validation("The Team must be active in the same tenant as the project"));
            var conflicts = await CheckTeamGroupConflictsInternalAsync(team.Id, projectId, dto.StartDate, dto.EndDate, dto.AllocationPercentage, null, cancellationToken);
            if (conflicts.Count > 0) return ResourceAllocationConflict(conflicts);
        }
        else return Result<ProjectMemberDto>.Failure(Error.Validation("Choose exactly one Resource or Team"));

        var member = dto.ToEntity(userId);
        // Membership rows must carry the owning project's tenant: the global tenant
        // query filter hides rows with an empty TenantId, which previously made the
        // new member invisible to team reads, scope resolution, and conflict checks.
        member.TenantId = project.TenantId;
        // Project membership records dedication only. Weekly resource capacity is
        // derived from the resource calendar, never supplied as task work here.
        member.HoursPerWeek = null;
        _context.Set<ProjectMember>().Add(member);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectMember",
            EntityId = member.Id,
            Action = "Create",
            TenantId = member.TenantId,
            UserId = TryParseGuid(userId),
            UserName = CurrentUserName,
            Changes = JsonSerializer.Serialize(new { resource = resource?.Name, team = team?.Name, role = member.Role, allocationPercentage = member.AllocationPercentage }),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        var resultDto = member.ToDto();
        resultDto.ResourceName = resource?.Name ?? team?.Name ?? string.Empty;
        resultDto.ResourceJobTitle = resource?.JobTitle;
        resultDto.TeamName = team?.Name;
        return Result<ProjectMemberDto>.Success(resultDto);
    }

    public async Task<Result<ProjectMemberDto>> UpdateTeamMemberAsync(Guid projectId, Guid memberId, UpdateProjectMemberDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var member = await _context.Set<ProjectMember>()
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == projectId && !m.IsDeleted, cancellationToken);
        if (member is null) return Result<ProjectMemberDto>.Failure(Error.NotFound("Project member not found"));
        if (ProjectSponsorMembershipSynchronizer.IsManagedMembership(member)
            || await IsCurrentSponsorMemberAsync(member, cancellationToken))
            return Result<ProjectMemberDto>.Failure(Error.Conflict("This Project Team relationship is governed by Project Sponsor settings. Change the Sponsor through Project Governance."));
        if (ProjectSponsorMembershipSynchronizer.IsReservedManagedMembershipNote(dto.Notes))
            return Result<ProjectMemberDto>.Failure(Error.Validation("This Project Team note is reserved for the system-managed Sponsor relationship."));

        var startDate = dto.StartDate ?? member.StartDate;
        var endDate = dto.EndDate ?? member.EndDate;
        var allocationPercentage = dto.AllocationPercentage ?? member.AllocationPercentage;
        if (allocationPercentage is < 0m or > 100m)
            return Result<ProjectMemberDto>.Failure(Error.Validation("Project allocation must be between 0 and 100 percent."));
        if (endDate.Date < startDate.Date)
            return Result<ProjectMemberDto>.Failure(Error.Validation("Project team end date cannot be earlier than its start date."));

        var conflicts = member.ResourceId.HasValue
            ? await CheckResourceConflictsInternalAsync(member.ResourceId.Value, startDate, endDate, allocationPercentage, memberId, cancellationToken, projectId)
            : await CheckTeamGroupConflictsInternalAsync(member.TeamId!.Value, projectId, startDate, endDate, allocationPercentage, memberId, cancellationToken);
        if (conflicts.Count > 0) return ResourceAllocationConflict(conflicts);

        var beforeMemberValues = Snapshot(member);
        dto.UpdateEntity(member, userId);
        member.HoursPerWeek = null;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectMember",
            EntityId = member.Id,
            Action = "Update",
            TenantId = member.TenantId,
            UserId = TryParseGuid(userId),
            UserName = CurrentUserName,
            Changes = SerializeChanges(BuildChanges(beforeMemberValues, Snapshot(member))),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        var resource = member.ResourceId.HasValue
            ? await _context.Set<Resource>().FirstOrDefaultAsync(r => r.Id == member.ResourceId.Value && !r.IsDeleted, cancellationToken)
            : null;
        var team = member.TeamId.HasValue
            ? await _context.Set<Team>().FirstOrDefaultAsync(item => item.Id == member.TeamId.Value && !item.IsDeleted, cancellationToken)
            : null;
        var resultDto = member.ToDto();
        resultDto.ResourceName = resource?.Name ?? team?.Name ?? string.Empty;
        resultDto.ResourceJobTitle = resource?.JobTitle;
        resultDto.TeamName = team?.Name;
        return Result<ProjectMemberDto>.Success(resultDto);
    }

    public async Task<Result> RemoveTeamMemberAsync(Guid projectId, Guid memberId, string userId, CancellationToken cancellationToken = default)
    {
        var member = await _context.Set<ProjectMember>()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == projectId && !m.IsDeleted, cancellationToken);
        if (member is null) return Result.Failure(Error.NotFound("Project member not found"));
        if (ProjectSponsorMembershipSynchronizer.IsManagedMembership(member)
            || await IsCurrentSponsorMemberAsync(member, cancellationToken))
            return Result.Failure(Error.Conflict("This Project Team relationship is governed by Project Sponsor settings. Change the Sponsor through Project Governance."));

        member.IsDeleted = true;
        member.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectMember",
            EntityId = memberId,
            Action = "Delete",
            TenantId = member.TenantId,
            UserId = TryParseGuid(userId),
            UserName = CurrentUserName,
            Changes = JsonSerializer.Serialize(new { role = member.Role, allocationPercentage = member.AllocationPercentage }),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    private async Task<bool> IsCurrentSponsorMemberAsync(ProjectMember member, CancellationToken cancellationToken)
    {
        var projectSponsorId = await _context.Set<Project>().AsNoTracking()
            .Where(project => project.Id == member.ProjectId && project.TenantId == member.TenantId && !project.IsDeleted)
            .Select(project => project.SponsorId)
            .FirstOrDefaultAsync(cancellationToken);
        return member.ResourceId.HasValue && projectSponsorId.HasValue && await _context.Set<Resource>().AsNoTracking()
            .AnyAsync(resource => resource.Id == member.ResourceId.Value && resource.TenantId == member.TenantId
                && resource.UserId == projectSponsorId.Value && !resource.IsDeleted, cancellationToken);
    }

    public async Task<Result<List<ProjectTeamMemberAllocationDto>>> CheckResourceConflictsAsync(Guid resourceId, DateTime startDate, DateTime endDate, decimal allocationPercentage, Guid? excludeMemberId, CancellationToken cancellationToken = default, Guid? projectId = null)
    {
        var conflicts = await CheckResourceConflictsInternalAsync(resourceId, startDate, endDate, allocationPercentage, excludeMemberId, cancellationToken, projectId);
        return Result<List<ProjectTeamMemberAllocationDto>>.Success(conflicts);
    }

    private async Task<List<ProjectTeamMemberAllocationDto>> CheckResourceConflictsInternalAsync(Guid resourceId, DateTime startDate, DateTime endDate, decimal allocationPercentage, Guid? excludeMemberId, CancellationToken cancellationToken, Guid? projectId = null)
    {
        var requestedStart = startDate.Date;
        var requestedEnd = endDate.Date;
        var resource = await _context.Set<Resource>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == resourceId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (resource is null) return new List<ProjectTeamMemberAllocationDto>();

        var activeTeamMemberships = await _context.Set<TeamMember>().AsNoTracking()
            .Where(member => member.ResourceId == resourceId && member.IsActive && !member.IsDeleted
                && member.EffectiveFrom.Date <= requestedEnd
                && (!member.EffectiveTo.HasValue || member.EffectiveTo.Value.Date >= requestedStart))
            .Select(member => new { member.TeamId, member.EffectiveFrom, member.EffectiveTo })
            .ToListAsync(cancellationToken);
        var candidateTeamIds = activeTeamMemberships.Select(member => member.TeamId).Distinct().ToArray();
        var activeTeamIds = await _context.Set<Team>().AsNoTracking()
            .Where(team => candidateTeamIds.Contains(team.Id) && team.IsActive && !team.IsDeleted)
            .Select(team => team.Id)
            .ToArrayAsync(cancellationToken);
        var activeTeamIdSet = activeTeamIds.ToHashSet();
        activeTeamMemberships = activeTeamMemberships.Where(member => activeTeamIdSet.Contains(member.TeamId)).ToList();

        var query = _context.Set<ProjectMember>().Include(member => member.Project)
            .Where(member => member.Status == "Active" && !member.IsDeleted
                && member.StartDate.Date <= requestedEnd && member.EndDate.Date >= requestedStart
                && (member.ResourceId == resourceId || (member.TeamId.HasValue && activeTeamIds.Contains(member.TeamId.Value))));
        if (excludeMemberId.HasValue) query = query.Where(member => member.Id != excludeMemberId.Value);
        var rows = await query.ToListAsync(cancellationToken);

        var commitments = new List<(ProjectMember Member, DateTime Start, DateTime End)>();
        foreach (var row in rows)
        {
            if (row.ResourceId == resourceId)
                commitments.Add((row, row.StartDate.Date, row.EndDate.Date));
            else if (row.TeamId is Guid teamId)
            {
                foreach (var teamMembership in activeTeamMemberships.Where(item => item.TeamId == teamId))
                {
                    var from = teamMembership.EffectiveFrom.Date > row.StartDate.Date ? teamMembership.EffectiveFrom.Date : row.StartDate.Date;
                    var to = teamMembership.EffectiveTo.HasValue && teamMembership.EffectiveTo.Value.Date < row.EndDate.Date
                        ? teamMembership.EffectiveTo.Value.Date : row.EndDate.Date;
                    if (from <= to) commitments.Add((row, from, to));
                }
            }
        }

        var evaluationDates = commitments.Select(item => item.Start).Where(date => date >= requestedStart && date <= requestedEnd)
            .Append(requestedStart).Distinct().ToArray();
        var conflictIds = new HashSet<Guid>();
        foreach (var date in evaluationDates)
        {
            var activeAtDate = commitments.Where(item => item.Start <= date && item.End >= date).ToList();
            var projectTotals = activeAtDate.GroupBy(item => item.Member.ProjectId)
                .ToDictionary(group => group.Key, group => group.Max(item => item.Member.AllocationPercentage));
            if (projectId.HasValue)
                projectTotals[projectId.Value] = Math.Max(projectTotals.GetValueOrDefault(projectId.Value), allocationPercentage);
            else
                projectTotals[Guid.Empty] = allocationPercentage;
            if (projectTotals.Values.Sum() > 100m)
                foreach (var item in activeAtDate) conflictIds.Add(item.Member.Id);
        }

        return commitments.Select(item => item.Member).DistinctBy(member => member.Id)
            .Where(member => conflictIds.Contains(member.Id))
            .Select(member => new ProjectTeamMemberAllocationDto
            {
                AllocationId = member.Id,
                ResourceId = resourceId,
                ResourceName = resource.Name,
                ProjectName = member.Project?.Name ?? "Unknown",
                Role = member.Role,
                AllocationPercentage = member.AllocationPercentage,
                HoursPerWeek = member.HoursPerWeek,
                StartDate = member.StartDate,
                EndDate = member.EndDate,
                Status = member.Status,
                IsBillable = member.IsBillable,
                Notes = ProjectSponsorMembershipSynchronizer.IsManagedMembership(member) ? null : member.Notes
            }).ToList();
    }

    private async Task<List<ProjectTeamMemberAllocationDto>> CheckTeamGroupConflictsInternalAsync(
        Guid teamId, Guid projectId, DateTime startDate, DateTime endDate, decimal allocationPercentage,
        Guid? excludeMemberId, CancellationToken cancellationToken)
    {
        var resourceIds = await _context.Set<TeamMember>().AsNoTracking()
            .Where(member => member.TeamId == teamId && member.IsActive && !member.IsDeleted
                && member.EffectiveFrom.Date <= endDate.Date
                && (!member.EffectiveTo.HasValue || member.EffectiveTo.Value.Date >= startDate.Date))
            .Join(_context.Set<Resource>().AsNoTracking().Where(resource => resource.IsActive && !resource.IsDeleted),
                membership => membership.ResourceId, resource => resource.Id, (membership, resource) => resource.Id)
            .Distinct().ToListAsync(cancellationToken);
        var conflicts = new List<ProjectTeamMemberAllocationDto>();
        foreach (var resourceId in resourceIds)
            conflicts.AddRange(await CheckResourceConflictsInternalAsync(resourceId, startDate, endDate, allocationPercentage, excludeMemberId, cancellationToken, projectId));
        return conflicts.DistinctBy(conflict => new { conflict.ResourceId, conflict.AllocationId }).ToList();
    }

    private static Result<ProjectMemberDto> ResourceAllocationConflict(IReadOnlyCollection<ProjectTeamMemberAllocationDto> conflicts)
    {
        var detail = conflicts.Select(conflict => $"{conflict.ResourceName ?? "Unknown"}: {conflict.ProjectName ?? "Unknown"} ({conflict.AllocationPercentage}%)");
        return Result<ProjectMemberDto>.Failure(Error.Validation(
            $"Resource over-allocation detected: total would exceed 100%. Conflicting allocations: {string.Join(", ", detail)}"));
    }
}
