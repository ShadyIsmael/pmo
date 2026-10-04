using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
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

namespace NEXUS.Modules.ProjectManagement.Application;

public class ProjectService : IProjectService, IProjectReferenceLookup
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor? _managementContext;
    private readonly ProjectManagerMembershipSynchronizer _membershipSynchronizer;

    public ProjectService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor, ProjectManagerMembershipSynchronizer membershipSynchronizer, IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _membershipSynchronizer = membershipSynchronizer;
        _managementContext = managementContext;
    }

    public async Task<ProjectReference?> FindAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await InScope(_context.Set<Project>())
            .Where(project => project.Id == projectId)
            .Select(project => new ProjectReference(project.Id, project.Name, project.Currency, project.Budget))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectReference>> GetFinancialReferencesAsync(CancellationToken cancellationToken = default)
        => await InScope(_context.Set<Project>().AsNoTracking())
            .Select(p => new ProjectReference(p.Id, p.Name, p.Currency, p.Budget)).ToListAsync(cancellationToken);

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

    public async Task<PagedResult<ProjectSummaryDto>> GetPagedAsync(int page, int pageSize, string? status = null, string? priority = null, Guid? portfolioId = null, CancellationToken cancellationToken = default)
    {
        var query = InScope(_context.Set<Project>().AsNoTracking());
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(p => p.Priority == priority);
        if (portfolioId.HasValue) query = query.Where(p => p.PortfolioId == portfolioId.Value);
        var total = await query.CountAsync(cancellationToken);
        var summaries = await query.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProjectSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                Status = p.Status,
                Health = p.Health,
                RagStatus = p.RagStatus,
                Progress = p.Progress,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Budget = p.Budget,
                ActualCost = p.ActualCost,
                Currency = p.Currency,
                ProjectManagerId = p.ProjectManagerId,
                ProjectManagerName = _context.Set<User>()
                    .Where(user => user.Id == p.ProjectManagerId)
                    .Select(user => user.FullName)
                    .FirstOrDefault(),
                WorkingCalendarId = p.WorkingCalendarId
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ProjectSummaryDto>(summaries, page, pageSize, total);
    }

    public async Task<Result<ProjectDto>> CreateAsync(CreateProjectDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (!dto.ProjectManagerId.HasValue)
        {
            return Result<ProjectDto>.Failure(Error.Validation("A project manager is required"));
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
        var project = dto.ToEntity(userId);
        project.Currency = hierarchy.Currency!;
        project.PortfolioId = hierarchy.PortfolioId;
        project.Code = finalCode;
        project.TenantId = GetCurrentTenantId();
        project.ProjectManagerId = dto.ProjectManagerId.Value;

        // Set default WorkingCalendarId if not provided
        if (!project.WorkingCalendarId.HasValue)
        {
            var defaultCalendar = await _context.Set<WorkingCalendar>()
                .Where(c => c.TenantId == project.TenantId && c.IsDefault && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            project.WorkingCalendarId = defaultCalendar?.Id;
        }

        _context.Set<Project>().Add(project);

        _context.Set<ProjectManagerAssignment>().Add(new ProjectManagerAssignment
        {
            TenantId = project.TenantId,
            ProjectId = project.Id,
            ProjectManagerId = dto.ProjectManagerId.Value,
            StartDate = project.StartDate ?? DateTime.UtcNow.Date,
            IsCurrent = true,
            CreatedBy = userId
        });

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Project",
            EntityId = project.Id,
            Action = "Create",
            UserId = TryParseGuid(userId),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        var resultDto = project.ToDto();
        await ResolveProjectNamesAsync(resultDto, cancellationToken);
        return Result<ProjectDto>.Success(resultDto);
    }

    private Guid GetCurrentTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var parsed))
        {
            return parsed;
        }

        return TenantConstants.DemoTenantId;
    }

    private IQueryable<Project> InScope(IQueryable<Project> query)
    {
        var scope = _managementContext?.Current?.Scope;
        return scope is null ? query : query.Where(project => scope.ProjectIds.Contains(project.Id));
    }

    private static Guid? TryParseGuid(string value)
    {
        return GuidHelper.TryParse(value);
    }

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

        // Creation is the existing setup/approval boundary; Status is not a draft approval gate.
        if (dto.Budget.HasValue && dto.Budget != project.Budget)
            return Result<ProjectDto>.Failure(Error.Validation("Project budget changes require an approved Project Change Request."));
        var hierarchy = await ResolveHierarchyAsync(dto.ProgramId, dto.PortfolioId, dto.Currency, cancellationToken, project);
        if (hierarchy.Error is not null) return Result<ProjectDto>.Failure(hierarchy.Error);
        if (dto.Currency != null && !await IsValidCurrencyAsync(dto.Currency.Trim(), cancellationToken))
            return Result<ProjectDto>.Failure(Error.Validation("Currency must be an active configured currency"));

        var oldValues = System.Text.Json.JsonSerializer.Serialize(project.ToDto());

        var managerChanged = dto.ProjectManagerId.HasValue && dto.ProjectManagerId.Value != project.ProjectManagerId;

        if (!managerChanged)
        {
            dto.UpdateEntity(project, userId);
            if (hierarchy.PortfolioId.HasValue) project.PortfolioId = hierarchy.PortfolioId;
            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(new AuditEntry
            {
                EntityType = "Project",
                EntityId = project.Id,
                Action = "Update",
                UserId = TryParseGuid(userId),
                OriginalValues = oldValues,
                NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
                Timestamp = DateTime.UtcNow,
                CreatedBy = userId
            }, cancellationToken);

            var resultDto = project.ToDto();
            await ResolveProjectNamesAsync(resultDto, cancellationToken);
            return Result<ProjectDto>.Success(resultDto);
        }

        var newManagerId = dto.ProjectManagerId!.Value;
        var previousManagerId = project.ProjectManagerId;
        if (!previousManagerId.HasValue)
        {
            return Result<ProjectDto>.Failure(Error.Validation("Cannot hand over the project manager because the project does not have a current project manager"));
        }

        var newManager = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.Id == newManagerId && !user.IsDeleted, cancellationToken);
        if (newManager is null)
            return Result<ProjectDto>.Failure(Error.Validation("The new project manager could not be found"));

        var previousManager = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.Id == previousManagerId.Value && !user.IsDeleted, cancellationToken);
        if (previousManager is null)
            return Result<ProjectDto>.Failure(Error.Validation("The current project manager could not be found"));

        var currentAssignments = await _context.Set<ProjectManagerAssignment>()
            .Where(a => a.ProjectId == id && a.IsCurrent && !a.IsDeleted)
            .OrderByDescending(a => a.StartDate)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
        var currentAssignment = currentAssignments.FirstOrDefault();

        var handover = dto.Handover ?? new ProjectHandoverDto();
        var handoverDate = handover.HandoverDate?.Date;
        var handoverReason = handover.HandoverReason?.Trim();

        if (!handoverDate.HasValue)
        {
            return Result<ProjectDto>.Failure(Error.Validation("Handover date is required when changing the project manager"));
        }
        if (string.IsNullOrWhiteSpace(handoverReason))
        {
            return Result<ProjectDto>.Failure(Error.Validation("Handover reason is required when changing the project manager"));
        }

        var currentStartDate = (currentAssignment?.StartDate ?? project.StartDate ?? project.CreatedAt).Date;
        if (handoverDate.Value < currentStartDate)
        {
            return Result<ProjectDto>.Failure(Error.Validation("Handover date cannot be before the current project manager's start date"));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        dto.UpdateEntity(project, userId);
        if (hierarchy.PortfolioId.HasValue) project.PortfolioId = hierarchy.PortfolioId;

        foreach (var assignment in currentAssignments)
        {
            assignment.EndDate = handoverDate;
            assignment.IsCurrent = false;
            assignment.UpdatedBy = userId;
        }

        _context.Set<ProjectManagerAssignment>().Add(new ProjectManagerAssignment
        {
            TenantId = project.TenantId,
            ProjectId = project.Id,
            ProjectManagerId = newManagerId,
            StartDate = handoverDate.Value,
            IsCurrent = true,
            HandoverFromProjectManagerId = previousManagerId,
            HandoverDate = handoverDate,
            HandoverReason = handoverReason,
            HandoverNotes = handover.HandoverNotes,
            CreatedBy = userId
        });

        await _membershipSynchronizer.SynchronizeAsync(project, previousManagerId.Value, newManagerId, handoverDate.Value, userId, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var managerChange = $"Project Manager: {previousManager.FullName} → {newManager.FullName}";

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Project",
            EntityId = project.Id,
            Action = "ManagerHandover",
            TenantId = project.TenantId,
            UserId = TryParseGuid(userId),
            Changes = managerChange,
            OriginalValues = oldValues,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                dto.ProjectManagerId,
                ProjectManagerName = newManager.FullName,
                PreviousProjectManagerId = previousManagerId,
                PreviousProjectManagerName = previousManager.FullName,
                HandoverDate = handoverDate,
                HandoverReason = handoverReason,
                HandoverNotes = handover.HandoverNotes
            }),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var updatedDto = project.ToDto();
        await ResolveProjectNamesAsync(updatedDto, cancellationToken);
        return Result<ProjectDto>.Success(updatedDto);
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
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        if (dto.ProjectManagerId.HasValue && users.TryGetValue(dto.ProjectManagerId.Value, out var manager))
        {
            dto.ProjectManagerName = manager.FullName;
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
    }

    private async Task<(Error? Error, Guid? PortfolioId, string? Currency)> ResolveHierarchyAsync(Guid? programId, Guid? portfolioId, string? requestedCurrency, CancellationToken ct, Project? existing = null)
    {
        var tenant = GetCurrentTenantId();
        programId ??= existing?.ProgramId;
        portfolioId ??= existing?.PortfolioId;
        Guid? resolvedPortfolio = portfolioId;
        if (programId.HasValue)
        {
            var program = await _context.Set<ProgramEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == programId.Value && !x.IsDeleted, ct);
            if (program is null || program.TenantId != tenant) return (Error.Validation("Program must exist in the current tenant"), null, null);
            if (portfolioId.HasValue && portfolioId.Value != program.PortfolioId) return (Error.Validation("Project Portfolio must match the selected Program Portfolio"), null, null);
            resolvedPortfolio = program.PortfolioId;
            var currency = string.IsNullOrWhiteSpace(requestedCurrency) ? (existing?.Currency ?? program.Currency) : requestedCurrency.Trim().ToUpperInvariant();
            if (!await IsValidCurrencyAsync(currency, ct)) return (Error.Validation("Currency must be an active configured currency"), null, null);
            return (null, resolvedPortfolio, currency);
        }
        if (portfolioId.HasValue)
        {
            var portfolio = await _context.Set<PortfolioEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == portfolioId.Value && !x.IsDeleted, ct);
            if (portfolio is null || portfolio.TenantId != tenant) return (Error.Validation("Portfolio must exist in the current tenant"), null, null);
            var currency = string.IsNullOrWhiteSpace(requestedCurrency) ? (existing?.Currency ?? portfolio.Currency) : requestedCurrency.Trim().ToUpperInvariant();
            if (!await IsValidCurrencyAsync(currency, ct)) return (Error.Validation("Currency must be an active configured currency"), null, null);
            return (null, portfolioId, currency);
        }
        if (existing is not null && string.IsNullOrWhiteSpace(requestedCurrency)) return (null, existing.PortfolioId, existing.Currency);
        var standaloneCurrency = requestedCurrency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(standaloneCurrency) || !await IsValidCurrencyAsync(standaloneCurrency, ct)) return (Error.Validation("Currency is required and must be an active configured currency"), null, null);
        return (null, existing?.PortfolioId, standaloneCurrency);
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
            UserId = TryParseGuid(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ArchiveAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (project is null) return Result.Failure(Error.NotFound("Project not found"));

        if (project.Status == "Completed" || project.Status == "Cancelled" || project.Status == "Closed")
            return Result.Failure(Error.Validation("Cannot archive a project that is already completed, cancelled, or closed"));

        var oldStatus = project.Status;
        project.Status = "OnHold";
        project.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Project",
            EntityId = project.Id,
            Action = "Archive",
            UserId = TryParseGuid(userId),
            OriginalValues = oldStatus,
            NewValues = "OnHold",
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ReactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (project is null) return Result.Failure(Error.NotFound("Project not found"));

        if (project.Status != "OnHold")
            return Result.Failure(Error.Validation("Only archived (OnHold) projects can be reactivated"));

        var oldStatus = project.Status;
        project.Status = "InProgress";
        project.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Project",
            EntityId = project.Id,
            Action = "Reactivate",
            UserId = TryParseGuid(userId),
            OriginalValues = oldStatus,
            NewValues = "InProgress",
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ProjectMemberDto>>> GetTeamAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var members = await _context.Set<ProjectMember>()
            .Include(m => m.Project)
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .OrderBy(m => m.Role)
            .ToListAsync(cancellationToken);

        var memberDtos = new List<ProjectMemberDto>();
        foreach (var member in members)
        {
            var resource = await _context.Set<Resource>().FirstOrDefaultAsync(r => r.Id == member.ResourceId && !r.IsDeleted, cancellationToken);
            memberDtos.Add(new ProjectMemberDto
            {
                Id = member.Id,
                ProjectId = member.ProjectId,
                ResourceId = member.ResourceId,
                ResourceName = resource?.Name ?? "Unknown",
                ResourceJobTitle = resource?.JobTitle,
                Role = member.Role,
                AllocationPercentage = member.AllocationPercentage,
                HoursPerWeek = member.HoursPerWeek,
                StartDate = member.StartDate,
                EndDate = member.EndDate,
                Status = member.Status,
                IsBillable = member.IsBillable,
                Notes = member.Notes,
                CreatedAt = member.CreatedAt,
                UpdatedAt = member.UpdatedAt
            });
        }

        return Result<IReadOnlyList<ProjectMemberDto>>.Success(memberDtos);
    }

    public async Task<Result<ProjectMemberDto>> AddTeamMemberAsync(Guid projectId, CreateProjectMemberDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var project = await _context.Set<Project>().FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);
        if (project is null) return Result<ProjectMemberDto>.Failure(Error.NotFound("Project not found"));

        var resource = await _context.Set<Resource>().FirstOrDefaultAsync(r => r.Id == dto.ResourceId && !r.IsDeleted, cancellationToken);
        if (resource is null) return Result<ProjectMemberDto>.Failure(Error.NotFound("Resource not found"));
        if (!resource.IsActive) return Result<ProjectMemberDto>.Failure(Error.Validation("Resource is not active"));

        var conflicts = await CheckResourceConflictsInternalAsync(dto.ResourceId, dto.StartDate, dto.EndDate, dto.AllocationPercentage, null, cancellationToken);
        if (conflicts.Count > 0)
        {
            var conflictDetails = conflicts.Select(c => $"{c.ProjectName ?? "Unknown"} ({c.AllocationPercentage}%)");
            return Result<ProjectMemberDto>.Failure(Error.Validation(
                $"Resource over-allocation detected: total would exceed 100%. Conflicting allocations: {string.Join(", ", conflictDetails)}"));
        }

        var member = dto.ToEntity(userId);
        _context.Set<ProjectMember>().Add(member);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectMember",
            EntityId = member.Id,
            Action = "Create",
            UserId = TryParseGuid(userId),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        var resultDto = member.ToDto();
        resultDto.ResourceName = resource.Name;
        resultDto.ResourceJobTitle = resource.JobTitle;
        return Result<ProjectMemberDto>.Success(resultDto);
    }

    public async Task<Result<ProjectMemberDto>> UpdateTeamMemberAsync(Guid projectId, Guid memberId, UpdateProjectMemberDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var member = await _context.Set<ProjectMember>()
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == projectId && !m.IsDeleted, cancellationToken);
        if (member is null) return Result<ProjectMemberDto>.Failure(Error.NotFound("Project member not found"));

        var startDate = dto.StartDate ?? member.StartDate;
        var endDate = dto.EndDate ?? member.EndDate;
        var allocationPercentage = dto.AllocationPercentage ?? member.AllocationPercentage;

        var conflicts = await CheckResourceConflictsInternalAsync(member.ResourceId, startDate, endDate, allocationPercentage, memberId, cancellationToken);
        if (conflicts.Count > 0)
        {
            var conflictDetails = conflicts.Select(c => $"{c.ProjectName ?? "Unknown"} ({c.AllocationPercentage}%)");
            return Result<ProjectMemberDto>.Failure(Error.Validation(
                $"Resource over-allocation detected: total would exceed 100%. Conflicting allocations: {string.Join(", ", conflictDetails)}"));
        }

        dto.UpdateEntity(member, userId);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectMember",
            EntityId = member.Id,
            Action = "Update",
            UserId = TryParseGuid(userId),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        var resource = await _context.Set<Resource>().FirstOrDefaultAsync(r => r.Id == member.ResourceId && !r.IsDeleted, cancellationToken);
        var resultDto = member.ToDto();
        resultDto.ResourceName = resource?.Name ?? "Unknown";
        resultDto.ResourceJobTitle = resource?.JobTitle;
        return Result<ProjectMemberDto>.Success(resultDto);
    }

    public async Task<Result> RemoveTeamMemberAsync(Guid projectId, Guid memberId, string userId, CancellationToken cancellationToken = default)
    {
        var member = await _context.Set<ProjectMember>()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == projectId && !m.IsDeleted, cancellationToken);
        if (member is null) return Result.Failure(Error.NotFound("Project member not found"));

        member.IsDeleted = true;
        member.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectMember",
            EntityId = memberId,
            Action = "Delete",
            UserId = TryParseGuid(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<List<ProjectTeamMemberAllocationDto>>> CheckResourceConflictsAsync(Guid resourceId, DateTime startDate, DateTime endDate, decimal allocationPercentage, Guid? excludeMemberId, CancellationToken cancellationToken = default)
    {
        var conflicts = await CheckResourceConflictsInternalAsync(resourceId, startDate, endDate, allocationPercentage, excludeMemberId, cancellationToken);
        return Result<List<ProjectTeamMemberAllocationDto>>.Success(conflicts);
    }

    private async Task<List<ProjectTeamMemberAllocationDto>> CheckResourceConflictsInternalAsync(Guid resourceId, DateTime startDate, DateTime endDate, decimal allocationPercentage, Guid? excludeMemberId, CancellationToken cancellationToken)
    {
        var query = _context.Set<ProjectMember>()
            .Include(m => m.Project)
            .Where(m => m.ResourceId == resourceId && m.Status == "Active" && !m.IsDeleted);

        if (excludeMemberId.HasValue)
            query = query.Where(m => m.Id != excludeMemberId.Value);

        var overlapping = await query
            .Where(m => m.StartDate < endDate && m.EndDate > startDate)
            .ToListAsync(cancellationToken);

        var conflicts = new List<ProjectTeamMemberAllocationDto>();
        var totalPercentage = overlapping.Sum(m => m.AllocationPercentage) + allocationPercentage;

        if (totalPercentage > 100)
        {
            foreach (var m in overlapping)
            {
                conflicts.Add(new ProjectTeamMemberAllocationDto
                {
                    AllocationId = m.Id,
                    ResourceId = m.ResourceId,
                    ResourceName = m.Project?.Name ?? "Unknown",
                    ProjectName = m.Project?.Name ?? "Unknown",
                    Role = m.Role,
                    AllocationPercentage = m.AllocationPercentage,
                    HoursPerWeek = m.HoursPerWeek,
                    StartDate = m.StartDate,
                    EndDate = m.EndDate,
                    Status = m.Status,
                    IsBillable = m.IsBillable,
                    Notes = m.Notes
                });
            }
        }

        return conflicts;
    }
}
