using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.IssueManagement.Domain;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Modules.ProjectManagement.Application;

public class ProjectSnapshotService : IProjectSnapshotService
{
    private static readonly JsonSerializerOptions DataJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ProjectManagerMembershipSynchronizer _membershipSynchronizer;
    private readonly TimeProvider _timeProvider;

    public ProjectSnapshotService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor, ProjectManagerMembershipSynchronizer membershipSynchronizer, TimeProvider? timeProvider = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _membershipSynchronizer = membershipSynchronizer;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private string? CurrentUserName => _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
        ?? _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value;

    public async Task<Result<ProjectSnapshotDto?>> CompleteHandoverAsync(Guid projectId, ProjectHandoverRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var capturedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var pendingRequest = await _context.Set<ProjectHandoverRequest>()
            .FirstOrDefaultAsync(item => item.Id == request.Id && item.ProjectId == projectId
                && item.TenantId == request.TenantId
                && (item.Status == ProjectHandoverRequest.Statuses.Pending || item.Status == ProjectHandoverRequest.Statuses.ApprovedScheduled)
                && !item.IsDeleted, cancellationToken);
        if (pendingRequest is null)
        {
            return Result<ProjectSnapshotDto?>.Failure(Error.Conflict("The handover request is no longer pending and cannot be accepted"));
        }
        request = pendingRequest;

        var project = await _context.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == request.TenantId && !p.IsDeleted, cancellationToken);
        if (project is null) return Result<ProjectSnapshotDto?>.Failure(Error.NotFound("Project not found"));

        var previousManagerId = project.ProjectManagerId;
        if (previousManagerId != request.CurrentProjectManagerId)
        {
            return Result<ProjectSnapshotDto?>.Failure(Error.Conflict("The current project manager has changed since the request was submitted"));
        }
        if (previousManagerId.HasValue && previousManagerId.Value == request.NewProjectManagerId)
        {
            return Result<ProjectSnapshotDto?>.Failure(Error.Validation("The new project manager must be different from the current project manager"));
        }
        if (!previousManagerId.HasValue && request.InitiationType != ManagerHandoverInitiationType.InitialAssignment)
            return Result<ProjectSnapshotDto?>.Failure(Error.Conflict("This request requires an assigned current manager"));
        if (previousManagerId.HasValue && request.InitiationType == ManagerHandoverInitiationType.InitialAssignment)
            return Result<ProjectSnapshotDto?>.Failure(Error.Conflict("The Project already has an assigned manager"));

        var newManager = await _context.Set<User>().FirstOrDefaultAsync(u => u.Id == request.NewProjectManagerId && u.TenantId == project.TenantId && u.IsActive && !u.IsDeleted, cancellationToken);
        if (newManager is null)
        {
            return Result<ProjectSnapshotDto?>.Failure(Error.Validation("The new project manager could not be found"));
        }

        // The previous manager is historical context only. A project can retain a
        // manager id after that user is removed, so acceptance must not depend on
        // resolving the old user record.
        var previousManager = previousManagerId.HasValue
            ? await _context.Set<User>().FirstOrDefaultAsync(u => u.Id == previousManagerId.Value && u.TenantId == project.TenantId && !u.IsDeleted, cancellationToken)
            : null;

        var handoverDate = request.EffectiveHandoverDate.Date;
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<ProjectSnapshotDto?>.Failure(Error.Validation("Handover reason is required"));
        }
        if (reason.Length > 500)
        {
            return Result<ProjectSnapshotDto?>.Failure(Error.Validation("Handover reason cannot exceed 500 characters"));
        }
        if (!string.IsNullOrWhiteSpace(request.Notes) && request.Notes!.Length > 2000)
        {
            return Result<ProjectSnapshotDto?>.Failure(Error.Validation("Handover notes cannot exceed 2000 characters"));
        }
        if (project.StartDate.HasValue && handoverDate < project.StartDate.Value.Date)
        {
            return Result<ProjectSnapshotDto?>.Failure(Error.Validation("Handover date cannot be before the project start date"));
        }
        if (project.EndDate.HasValue && handoverDate > project.EndDate.Value.Date)
        {
            return Result<ProjectSnapshotDto?>.Failure(Error.Validation("Handover date cannot be after the project end date"));
        }

        var currentAssignments = await _context.Set<ProjectManagerAssignment>()
            .Where(a => a.ProjectId == projectId && a.TenantId == project.TenantId && a.IsCurrent && !a.IsDeleted)
            .OrderByDescending(a => a.StartDate)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
        var currentAssignment = currentAssignments.FirstOrDefault();
        if (!previousManagerId.HasValue && currentAssignments.Count > 0)
            return Result<ProjectSnapshotDto?>.Failure(Error.Conflict("The Project has an inconsistent active manager assignment. Resolve it before initial assignment."));
        if (previousManagerId.HasValue && currentAssignment is not null && currentAssignment.ProjectManagerId != previousManagerId.Value)
            return Result<ProjectSnapshotDto?>.Failure(Error.Conflict("The active manager assignment has changed since the request was submitted"));
        if (currentAssignment is not null && handoverDate < currentAssignment.StartDate.Date)
        {
            return Result<ProjectSnapshotDto?>.Failure(Error.Validation("Handover date cannot be before the current project manager's start date"));
        }

        ProjectSnapshot? snapshot = null;
        var snapshotNumber = 0;
        if (previousManagerId.HasValue)
        {
            snapshotNumber = (await _context.Set<ProjectSnapshot>().CountAsync(s => s.ProjectId == projectId && s.TenantId == project.TenantId, cancellationToken)) + 1;
            var payload = await BuildSnapshotPayloadAsync(projectId, cancellationToken);
            snapshot = new ProjectSnapshot
            {
                TenantId = project.TenantId,
                ProjectId = projectId,
                SnapshotNumber = snapshotNumber,
                SnapshotName = $"Handover Snapshot #{snapshotNumber}",
                SnapshotType = "ManagerHandover",
                SnapshotDate = capturedAt,
                EffectiveEndDate = handoverDate,
                PreviousProjectManagerId = previousManagerId,
                NewProjectManagerId = request.NewProjectManagerId,
                HandoverReason = reason,
                HandoverNotes = request.Notes,
                Data = JsonSerializer.Serialize(payload, DataJsonOptions),
                CreatedBy = userId
            };
            ApplyProjectFields(snapshot, project);
            _context.Set<ProjectSnapshot>().Add(snapshot);
        }

        project.ProjectManagerId = request.NewProjectManagerId;
        project.UpdatedBy = userId;

        foreach (var assignment in currentAssignments)
        {
            assignment.EndDate = handoverDate;
            assignment.IsCurrent = false;
            assignment.UpdatedBy = userId;
        }

        _context.Set<ProjectManagerAssignment>().Add(new ProjectManagerAssignment
        {
            TenantId = project.TenantId,
            ProjectId = projectId,
            ProjectManagerId = request.NewProjectManagerId,
            StartDate = handoverDate,
            IsCurrent = true,
            HandoverFromProjectManagerId = previousManagerId,
            HandoverDate = handoverDate,
            HandoverReason = reason,
            HandoverNotes = request.Notes,
            CreatedBy = userId
        });

        await _membershipSynchronizer.SynchronizeAsync(project, previousManagerId, request.NewProjectManagerId, handoverDate, userId, cancellationToken);

        var wasApprovedForFutureDate = request.Status == ProjectHandoverRequest.Statuses.ApprovedScheduled;
        request.Status = ProjectHandoverRequest.Statuses.Accepted;
        if (!wasApprovedForFutureDate)
        {
            request.RespondedBy = TryParseGuid(userId);
            request.RespondedAt = capturedAt;
        }
        request.SnapshotId = snapshot?.Id;
        request.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);

        var previousManagerName = previousManager?.FullName ?? "Not Assigned";
        var managerChange = JsonSerializer.Serialize(new { projectManager = new { from = previousManagerName, to = newManager.FullName } });

        if (snapshot is not null)
        {
            await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectSnapshot",
            EntityId = snapshot.Id,
            Action = "Handover",
            TenantId = project.TenantId,
            UserId = TryParseGuid(userId),
            UserName = CurrentUserName,
            Changes = managerChange,
            OriginalValues = JsonSerializer.Serialize(new
            {
                ProjectId = projectId,
                PreviousProjectManagerId = previousManagerId,
                PreviousProjectManagerName = previousManager?.FullName,
                NewProjectManagerId = request.NewProjectManagerId,
                NewProjectManagerName = newManager.FullName,
                HandoverDate = handoverDate,
                HandoverReason = reason,
                HandoverNotes = request.Notes,
                SnapshotNumber = snapshotNumber,
                HandoverRequestId = request.Id
            }, DataJsonOptions),
            Timestamp = capturedAt,
            CreatedBy = userId
            }, cancellationToken);
        }

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Project",
            EntityId = projectId,
            Action = "ManagerHandoverAccepted",
            TenantId = project.TenantId,
            UserId = TryParseGuid(userId),
            UserName = CurrentUserName,
            Changes = managerChange,
            OriginalValues = JsonSerializer.Serialize(new
            {
                RequestedBy = request.RequestedBy,
                PreviousProjectManagerId = previousManagerId,
                PreviousProjectManagerName = previousManager?.FullName,
                NewProjectManagerId = request.NewProjectManagerId,
                NewProjectManagerName = newManager.FullName,
                HandoverDate = handoverDate,
                HandoverReason = reason,
                InitiationType = request.InitiationType,
                HandoverRequestId = request.Id
            }, DataJsonOptions),
            NewValues = JsonSerializer.Serialize(new
            {
                ProjectManagerId = request.NewProjectManagerId,
                ProjectManagerName = newManager.FullName
            }, DataJsonOptions),
            Timestamp = capturedAt,
            CreatedBy = userId
        }, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var resultDto = snapshot is null ? null : await BuildSnapshotDtoAsync(snapshot, cancellationToken);
        return Result<ProjectSnapshotDto?>.Success(resultDto);
    }

    public async Task<Result<ProjectSnapshotSummaryDto>> CreateBaselineSnapshotAsync(Guid projectId, string snapshotType, string snapshotName, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == Guid.Empty)
            return Result<ProjectSnapshotSummaryDto>.Failure(Error.Unauthorized("A valid tenant context is required."));

        var project = await _context.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == tenantId && !p.IsDeleted, cancellationToken);
        if (project is null) return Result<ProjectSnapshotSummaryDto>.Failure(Error.NotFound("Project not found"));

        var snapshotNumber = (await _context.Set<ProjectSnapshot>()
            .CountAsync(s => s.ProjectId == projectId, cancellationToken)) + 1;

        var payload = await BuildSnapshotPayloadAsync(projectId, cancellationToken);

        var snapshot = new ProjectSnapshot
        {
            TenantId = tenantId,
            ProjectId = projectId,
            SnapshotNumber = snapshotNumber,
            SnapshotName = snapshotName,
            SnapshotType = snapshotType,
            SnapshotDate = DateTime.UtcNow,
            EffectiveEndDate = DateTime.UtcNow,
            Data = JsonSerializer.Serialize(payload, DataJsonOptions),
            CreatedBy = userId
        };
        ApplyProjectFields(snapshot, project);

        _context.Set<ProjectSnapshot>().Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ProjectSnapshot",
            EntityId = snapshot.Id,
            Action = "BaselineSnapshotCreated",
            TenantId = project.TenantId,
            UserId = TryParseGuid(userId),
            OriginalValues = JsonSerializer.Serialize(new
            {
                ProjectId = projectId,
                SnapshotNumber = snapshotNumber,
                SnapshotType = snapshotType,
                SnapshotName = snapshotName
            }, DataJsonOptions),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result<ProjectSnapshotSummaryDto>.Success(ToSummaryDto(snapshot));
    }

    public async Task<Result<IReadOnlyList<ProjectSnapshotSummaryDto>>> GetSnapshotsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var snapshots = await _context.Set<ProjectSnapshot>()
            .Where(s => s.ProjectId == projectId && !s.IsDeleted)
            .OrderByDescending(s => s.SnapshotNumber)
            .ToListAsync(cancellationToken);

        var userIds = snapshots
            .SelectMany(s => new Guid?[]
            {
                s.PreviousProjectManagerId,
                s.NewProjectManagerId,
                Guid.TryParse(s.CreatedBy, out var createdById) ? createdById : null
            })
            .Where(id => id.HasValue)
            .Select(id => id.GetValueOrDefault())
            .Distinct()
            .ToList();

        var users = new Dictionary<Guid, User>();
        if (userIds.Count > 0)
        {
            users = await _context.Set<User>()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        var summaries = snapshots.Select(s =>
        {
            var dto = ToSummaryDto(s);
            if (s.PreviousProjectManagerId.HasValue && users.TryGetValue(s.PreviousProjectManagerId.Value, out var previous))
                dto.PreviousProjectManagerName = previous.FullName;
            if (s.NewProjectManagerId.HasValue && users.TryGetValue(s.NewProjectManagerId.Value, out var current))
                dto.NewProjectManagerName = current.FullName;
            if (Guid.TryParse(s.CreatedBy, out var createdById) && users.TryGetValue(createdById, out var creator))
                dto.CreatedByName = creator.FullName;
            return dto;
        }).ToList();

        return Result<IReadOnlyList<ProjectSnapshotSummaryDto>>.Success(summaries);
    }

    public async Task<Result<ProjectSnapshotDto>> GetSnapshotAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken = default)
    {
        var snapshot = await _context.Set<ProjectSnapshot>()
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.ProjectId == projectId && !s.IsDeleted, cancellationToken);
        if (snapshot is null) return Result<ProjectSnapshotDto>.Failure(Error.NotFound("Snapshot not found"));

        var dto = await BuildSnapshotDtoAsync(snapshot, cancellationToken);
        return Result<ProjectSnapshotDto>.Success(dto);
    }

    private async Task<ProjectSnapshotDto> BuildSnapshotDtoAsync(ProjectSnapshot snapshot, CancellationToken cancellationToken)
    {
        var dto = new ProjectSnapshotDto
        {
            Id = snapshot.Id,
            ProjectId = snapshot.ProjectId,
            SnapshotNumber = snapshot.SnapshotNumber,
            SnapshotName = snapshot.SnapshotName,
            SnapshotType = snapshot.SnapshotType,
            SnapshotDate = snapshot.SnapshotDate,
            EffectiveEndDate = snapshot.EffectiveEndDate,
            PreviousProjectManagerId = snapshot.PreviousProjectManagerId,
            NewProjectManagerId = snapshot.NewProjectManagerId,
            HandoverReason = snapshot.HandoverReason,
            HandoverNotes = snapshot.HandoverNotes,
            CreatedAt = snapshot.CreatedAt,
            CreatedBy = snapshot.CreatedBy
        };
        dto.Name = snapshot.Name;
        dto.Code = snapshot.Code;
        dto.Description = snapshot.Description;
        dto.ProgramId = snapshot.ProgramId;
        dto.Status = snapshot.Status;
        dto.Priority = snapshot.Priority;
        dto.Category = snapshot.Category;
        dto.SponsorId = snapshot.SponsorId;
        dto.PortfolioId = snapshot.PortfolioId;
        dto.DepartmentId = snapshot.DepartmentId;
        dto.StartDate = snapshot.StartDate;
        dto.EndDate = snapshot.EndDate;
        dto.ActualStartDate = snapshot.ActualStartDate;
        dto.ActualEndDate = snapshot.ActualEndDate;
        dto.Budget = snapshot.Budget;
        dto.Currency = snapshot.Currency;
        dto.ActualCost = snapshot.ActualCost;
        dto.PlannedValue = snapshot.PlannedValue;
        dto.EarnedValue = snapshot.EarnedValue;
        dto.CostVariance = snapshot.CostVariance;
        dto.ScheduleVariance = snapshot.ScheduleVariance;
        dto.Progress = snapshot.Progress;
        dto.Health = snapshot.Health;
        dto.RagStatus = snapshot.RagStatus;
        dto.ConfidenceLevel = snapshot.ConfidenceLevel;
        dto.Phase = snapshot.Phase;
        dto.RiskRating = snapshot.RiskRating;
        dto.BusinessCase = snapshot.BusinessCase;
        dto.Notes = snapshot.Notes;

        if (!string.IsNullOrWhiteSpace(snapshot.Data))
        {
            try
            {
                dto.Data = JsonSerializer.Deserialize<ProjectSnapshotDataDto>(snapshot.Data, DataJsonOptions);
            }
            catch (JsonException)
            {
                dto.Data = null;
            }
        }

        await ResolveSnapshotNamesAsync(dto, cancellationToken);
        return dto;
    }

    private static ProjectSnapshotSummaryDto ToSummaryDto(ProjectSnapshot snapshot)
    {
        return new ProjectSnapshotSummaryDto
        {
            Id = snapshot.Id,
            ProjectId = snapshot.ProjectId,
            SnapshotNumber = snapshot.SnapshotNumber,
            SnapshotName = snapshot.SnapshotName,
            SnapshotType = snapshot.SnapshotType,
            SnapshotDate = snapshot.SnapshotDate,
            EffectiveEndDate = snapshot.EffectiveEndDate,
            PreviousProjectManagerId = snapshot.PreviousProjectManagerId,
            NewProjectManagerId = snapshot.NewProjectManagerId,
            HandoverReason = snapshot.HandoverReason,
            HandoverNotes = snapshot.HandoverNotes,
            CreatedAt = snapshot.CreatedAt,
            CreatedBy = snapshot.CreatedBy
        };
    }

    private async Task ResolveSnapshotNamesAsync(ProjectSnapshotDto dto, CancellationToken cancellationToken)
    {
        var userIds = new List<Guid>();
        if (dto.PreviousProjectManagerId.HasValue) userIds.Add(dto.PreviousProjectManagerId.Value);
        if (dto.NewProjectManagerId.HasValue) userIds.Add(dto.NewProjectManagerId.Value);
        if (dto.SponsorId.HasValue) userIds.Add(dto.SponsorId.Value);
        if (Guid.TryParse(dto.CreatedBy, out var creatorId)) userIds.Add(creatorId);
        userIds = userIds.Distinct().ToList();

        var users = new Dictionary<Guid, User>();
        if (userIds.Count > 0)
        {
            users = await _context.Set<User>()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        if (dto.PreviousProjectManagerId.HasValue && users.TryGetValue(dto.PreviousProjectManagerId.Value, out var previous))
            dto.PreviousProjectManagerName = previous.FullName;
        if (dto.NewProjectManagerId.HasValue && users.TryGetValue(dto.NewProjectManagerId.Value, out var current))
            dto.NewProjectManagerName = current.FullName;
        if (dto.SponsorId.HasValue && users.TryGetValue(dto.SponsorId.Value, out var sponsor))
            dto.SponsorName = sponsor.FullName;
        if (Guid.TryParse(dto.CreatedBy, out creatorId) && users.TryGetValue(creatorId, out var creator))
            dto.CreatedByName = creator.FullName;

        if (dto.PortfolioId.HasValue)
        {
            var portfolio = await _context.Set<PortfolioEntity>().FirstOrDefaultAsync(p => p.Id == dto.PortfolioId.Value, cancellationToken);
            dto.PortfolioName = portfolio?.Name;
        }
        if (dto.ProgramId.HasValue)
        {
            var program = await _context.Set<ProgramEntity>().FirstOrDefaultAsync(p => p.Id == dto.ProgramId.Value, cancellationToken);
            dto.ProgramName = program?.Name;
        }
        if (dto.DepartmentId.HasValue)
        {
            var department = await _context.Set<Department>().FirstOrDefaultAsync(d => d.Id == dto.DepartmentId.Value, cancellationToken);
            dto.DepartmentName = department?.Name;
        }
    }

    private async Task<ProjectSnapshotDataDto> BuildSnapshotPayloadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var payload = new ProjectSnapshotDataDto
        {
            Members = await BuildMembersAsync(projectId, cancellationToken),
            Allocations = await BuildAllocationsAsync(projectId, cancellationToken),
            Phases = await BuildPhasesAsync(projectId, cancellationToken),
            Milestones = await BuildMilestonesAsync(projectId, cancellationToken),
            Deliverables = await BuildDeliverablesAsync(projectId, cancellationToken),
            Stakeholders = await BuildStakeholdersAsync(projectId, cancellationToken),
            Schedules = await BuildSchedulesAsync(projectId, cancellationToken),
            WbsItems = await BuildWbsAsync(projectId, cancellationToken),
            Baselines = await BuildBaselinesAsync(projectId, cancellationToken),
            StatusReports = await BuildStatusReportsAsync(projectId, cancellationToken),
            WorkItems = await BuildWorkItemsAsync(projectId, cancellationToken),
            Risks = await BuildRisksAsync(projectId, cancellationToken),
            Issues = await BuildIssuesAsync(projectId, cancellationToken),
            Budgets = await BuildBudgetsAsync(projectId, cancellationToken),
            CostEntries = await BuildCostEntriesAsync(projectId, cancellationToken),
            Meetings = await BuildMeetingsAsync(projectId, cancellationToken),
            Documents = await BuildDocumentsAsync(projectId, cancellationToken)
        };

        return payload;
    }

    private async Task<IReadOnlyList<SnapshotMemberDto>> BuildMembersAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var members = await _context.Set<ProjectMember>()
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .OrderBy(m => m.Role)
            .ToListAsync(cancellationToken);

        var resourceIds = members.Where(member => member.ResourceId.HasValue).Select(member => member.ResourceId!.Value).Distinct().ToList();
        var teamIds = members.Where(member => member.TeamId.HasValue).Select(member => member.TeamId!.Value).Distinct().ToList();
        var resources = new Dictionary<Guid, Resource>();
        if (resourceIds.Count > 0)
        {
            resources = await _context.Set<Resource>()
                .Where(r => resourceIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, cancellationToken);
        }
        var teams = teamIds.Count == 0 ? new Dictionary<Guid, Team>() : await _context.Set<Team>()
            .Where(team => teamIds.Contains(team.Id) && !team.IsDeleted)
            .ToDictionaryAsync(team => team.Id, cancellationToken);

        return members.Select(m => new SnapshotMemberDto
        {
            OriginalId = m.Id,
            ResourceId = m.ResourceId,
            TeamId = m.TeamId,
            TeamName = m.TeamId.HasValue && teams.TryGetValue(m.TeamId.Value, out var team) ? team.Name : null,
            MemberType = m.TeamId.HasValue ? "Team" : "Resource",
            ResourceName = m.ResourceId.HasValue && resources.TryGetValue(m.ResourceId.Value, out var resource)
                ? resource.Name
                : m.TeamId.HasValue && teams.TryGetValue(m.TeamId.Value, out var group) ? group.Name : null,
            ResourceJobTitle = m.ResourceId.HasValue && resources.TryGetValue(m.ResourceId.Value, out var r2) ? r2.JobTitle : null,
            Role = m.Role,
            AllocationPercentage = m.AllocationPercentage,
            HoursPerWeek = m.HoursPerWeek,
            StartDate = m.StartDate,
            EndDate = m.EndDate,
            Status = m.Status,
            IsBillable = m.IsBillable,
            Notes = ProjectSponsorMembershipSynchronizer.IsManagedMembership(m) ? null : m.Notes
        }).ToList();
    }

    private async Task<IReadOnlyList<SnapshotAllocationDto>> BuildAllocationsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var allocations = await _context.Set<ResourceAllocation>()
            .Where(a => a.ProjectId == projectId && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        var resourceIds = allocations.Select(a => a.ResourceId).Distinct().ToList();
        var resources = new Dictionary<Guid, Resource>();
        if (resourceIds.Count > 0)
        {
            resources = await _context.Set<Resource>()
                .Where(r => resourceIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, cancellationToken);
        }

        return allocations.Select(a => new SnapshotAllocationDto
        {
            OriginalId = a.Id,
            ResourceId = a.ResourceId,
            ResourceName = resources.TryGetValue(a.ResourceId, out var resource) ? resource.Name : null,
            ProjectRoleId = a.ProjectRoleId,
            AllocationPercentage = a.AllocationPercentage,
            HoursPerWeek = a.HoursPerWeek,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            Status = a.Status,
            IsBillable = a.IsBillable,
            Notes = a.Notes
        }).ToList();
    }

    private async Task<IReadOnlyList<SnapshotPhaseDto>> BuildPhasesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _context.Set<ProjectPhase>()
            .Where(p => p.ProjectId == projectId && !p.IsDeleted)
            .OrderBy(p => p.SortOrder)
            .Select(p => new SnapshotPhaseDto
            {
                OriginalId = p.Id,
                Name = p.Name,
                Description = p.Description,
                SortOrder = p.SortOrder,
                Status = p.Status,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Progress = p.Progress
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SnapshotMilestoneDto>> BuildMilestonesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var milestones = await _context.Set<Milestone>()
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .OrderBy(m => m.DueDate)
            .ToListAsync(cancellationToken);

        var assigneeIds = milestones.Where(m => m.AssigneeId.HasValue).Select(m => m.AssigneeId!.Value).Distinct().ToList();
        var users = new Dictionary<Guid, User>();
        if (assigneeIds.Count > 0)
        {
            users = await _context.Set<User>().Where(u => assigneeIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        return milestones.Select(m => new SnapshotMilestoneDto
        {
            OriginalId = m.Id,
            Name = m.Name,
            Description = m.Description,
            DueDate = m.DueDate,
            CompletedDate = m.CompletedDate,
            Status = m.Status,
            Progress = m.Progress,
            Category = m.Category,
            AssigneeId = m.AssigneeId,
            AssigneeName = m.AssigneeId.HasValue && users.TryGetValue(m.AssigneeId.Value, out var user) ? user.FullName : null
        }).ToList();
    }

    private async Task<IReadOnlyList<SnapshotDeliverableDto>> BuildDeliverablesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var deliverables = await _context.Set<Deliverable>()
            .Where(d => d.ProjectId == projectId && !d.IsDeleted)
            .OrderBy(d => d.DueDate)
            .ToListAsync(cancellationToken);

        var ownerIds = deliverables.Where(d => d.OwnerId.HasValue).Select(d => d.OwnerId!.Value).Distinct().ToList();
        var users = new Dictionary<Guid, User>();
        if (ownerIds.Count > 0)
        {
            users = await _context.Set<User>().Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        return deliverables.Select(d => new SnapshotDeliverableDto
        {
            OriginalId = d.Id,
            Name = d.Name,
            Description = d.Description,
            Status = d.Status,
            DueDate = d.DueDate,
            DeliveredDate = d.DeliveredDate,
            OwnerId = d.OwnerId,
            OwnerName = d.OwnerId.HasValue && users.TryGetValue(d.OwnerId.Value, out var user) ? user.FullName : null,
            QualityCriteria = d.QualityCriteria
        }).ToList();
    }

    private async Task<IReadOnlyList<SnapshotStakeholderDto>> BuildStakeholdersAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _context.Set<ProjectStakeholder>()
            .Where(s => s.ProjectId == projectId && !s.IsDeleted)
            .OrderBy(s => s.Role)
            .Select(s => new SnapshotStakeholderDto
            {
                OriginalId = s.Id,
                UserId = s.UserId,
                Name = s.Name,
                Email = s.Email,
                Role = s.Role,
                Interest = s.Interest,
                Influence = s.Influence
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SnapshotScheduleDto>> BuildSchedulesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _context.Set<ProjectSchedule>()
            .Where(s => s.ProjectId == projectId && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .Select(s => new SnapshotScheduleDto
            {
                OriginalId = s.Id,
                Name = s.Name,
                Description = s.Description,
                BaselineStartDate = s.BaselineStartDate,
                BaselineEndDate = s.BaselineEndDate,
                ForecastStartDate = s.ForecastStartDate,
                ForecastEndDate = s.ForecastEndDate,
                ActualStartDate = s.ActualStartDate,
                ActualEndDate = s.ActualEndDate,
                TotalDays = s.TotalDays,
                RemainingDays = s.RemainingDays
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SnapshotWbsDto>> BuildWbsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _context.Set<WorkBreakdownStructure>()
            .Where(w => w.ProjectId == projectId && !w.IsDeleted)
            .OrderBy(w => w.SortOrder)
            .Select(w => new SnapshotWbsDto
            {
                OriginalId = w.Id,
                ParentId = w.ParentId,
                Name = w.Name,
                Code = w.Code,
                Description = w.Description,
                Deliverable = w.Deliverable,
                PlannedHours = w.PlannedHours,
                ActualHours = w.ActualHours,
                Progress = w.Progress,
                SortOrder = w.SortOrder,
                Status = w.Status
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SnapshotBaselineDto>> BuildBaselinesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _context.Set<Baseline>()
            .Where(b => b.ProjectId == projectId && !b.IsDeleted)
            .OrderByDescending(b => b.Version)
            .Select(b => new SnapshotBaselineDto
            {
                OriginalId = b.Id,
                Name = b.Name,
                Description = b.Description,
                Version = b.Version,
                BaselineDate = b.BaselineDate,
                IsCurrent = b.IsCurrent
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SnapshotStatusReportDto>> BuildStatusReportsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var reports = await _context.Set<ProjectStatusReport>()
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .OrderByDescending(r => r.PeriodTo)
            .ToListAsync(cancellationToken);

        var reporterIds = reports.Where(r => r.ReportedBy.HasValue).Select(r => r.ReportedBy!.Value).Distinct().ToList();
        var users = new Dictionary<Guid, User>();
        if (reporterIds.Count > 0)
        {
            users = await _context.Set<User>().Where(u => reporterIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        return reports.Select(r => new SnapshotStatusReportDto
        {
            OriginalId = r.Id,
            Status = r.Status ?? r.ReportState.ToString(),
            Summary = r.ExecutiveSummary,
            Accomplishments = r.KeyAchievements,
            NextSteps = r.NextPeriodPriorities,
            Blockers = r.ChallengesConcerns,
            ScheduleVariance = r.ScheduleVariance,
            CostVariance = r.CostVariance,
            PeriodStart = r.PeriodFrom,
            PeriodEnd = r.PeriodTo,
            ReportedAt = r.ReportedAt,
            ReportedBy = r.ReportedBy,
            ReportedByName = r.ReportedBy.HasValue && users.TryGetValue(r.ReportedBy.Value, out var user) ? user.FullName : null
        }).ToList();
    }

    private async Task<IReadOnlyList<SnapshotWorkItemDto>> BuildWorkItemsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var workItems = await _context.Set<WorkItem>()
            .Where(w => w.ProjectId == projectId && !w.IsDeleted)
            .OrderBy(w => w.SortOrder)
            .ToListAsync(cancellationToken);

        var resourceIds = workItems.Where(w => w.AssignedResourceId.HasValue).Select(w => w.AssignedResourceId!.Value).Distinct().ToList();
        var resourceNames = resourceIds.Count > 0
            ? await _context.Set<Resource>().Where(resource => resourceIds.Contains(resource.Id)).ToDictionaryAsync(resource => resource.Id, resource => resource.Name, cancellationToken)
            : new Dictionary<Guid, string>();
        var teamIds = workItems.Where(w => w.AssignedTeamId.HasValue).Select(w => w.AssignedTeamId!.Value).Distinct().ToList();
        var teamNames = teamIds.Count > 0
            ? await _context.Set<Team>().Where(team => teamIds.Contains(team.Id)).ToDictionaryAsync(team => team.Id, team => team.Name, cancellationToken)
            : new Dictionary<Guid, string>();

        return workItems.Select(w => new SnapshotWorkItemDto
        {
            OriginalId = w.Id,
            Title = w.Title,
            Description = w.Description,
            Type = w.Type,
            Priority = w.Priority,
            Status = w.Status,
            AssignedResourceId = w.AssignedResourceId,
            AssignedResourceName = w.AssignedResourceId.HasValue ? resourceNames.GetValueOrDefault(w.AssignedResourceId.Value) : null,
            AssignedTeamId = w.AssignedTeamId,
            AssignedTeamName = w.AssignedTeamId.HasValue ? teamNames.GetValueOrDefault(w.AssignedTeamId.Value) : null,
            AssigneeId = w.AssignedResourceId,
            AssigneeName = w.AssignedResourceId.HasValue ? resourceNames.GetValueOrDefault(w.AssignedResourceId.Value) : w.AssignedTeamId.HasValue ? teamNames.GetValueOrDefault(w.AssignedTeamId.Value) : null,
            ParentId = w.ParentId,
            StoryPoints = w.StoryPoints,
            EstimatedHours = w.EstimatedHours,
            ActualHours = w.ActualHours,
            StartDate = w.StartDate,
            DueDate = w.DueDate,
            CompletedAt = w.CompletedAt,
            SortOrder = w.SortOrder
        }).ToList();
    }

    private async Task<IReadOnlyList<SnapshotRiskDto>> BuildRisksAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var risks = await _context.Set<Risk>()
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .OrderBy(r => r.IdentifiedDate)
            .ToListAsync(cancellationToken);

        var ownerIds = risks.Where(r => r.OwnerId.HasValue).Select(r => r.OwnerId!.Value).Distinct().ToList();
        var users = new Dictionary<Guid, User>();
        if (ownerIds.Count > 0)
        {
            users = await _context.Set<User>().Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        return risks.Select(r => new SnapshotRiskDto
        {
            OriginalId = r.Id,
            Title = r.Title,
            Description = r.Description,
            Impact = r.Impact,
            Likelihood = r.Likelihood,
            RiskLevel = r.RiskLevel.ToString(),
            Status = r.Status.ToString(),
            PotentialCost = r.PotentialCost,
            PotentialDelay = r.PotentialDelay,
            RiskResponse = r.RiskResponse,
            OwnerId = r.OwnerId,
            OwnerName = r.OwnerId.HasValue && users.TryGetValue(r.OwnerId.Value, out var user) ? user.FullName : null,
            IdentifiedDate = r.IdentifiedDate,
            TargetResolutionDate = null,
            ClosedDate = r.ClosedDate
        }).ToList();
    }

    private async Task<IReadOnlyList<SnapshotIssueDto>> BuildIssuesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var issues = await _context.Set<Issue>()
            .Where(i => i.ProjectId == projectId && !i.IsDeleted)
            .OrderBy(i => i.IdentifiedDate)
            .ToListAsync(cancellationToken);

        var assigneeIds = issues.Where(i => i.AssigneeId.HasValue).Select(i => i.AssigneeId!.Value).Distinct().ToList();
        var users = new Dictionary<Guid, User>();
        if (assigneeIds.Count > 0)
        {
            users = await _context.Set<User>().Where(u => assigneeIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        return issues.Select(i => new SnapshotIssueDto
        {
            OriginalId = i.Id,
            Title = i.Title,
            Description = i.Description,
            Severity = i.Severity,
            Priority = i.Priority,
            Status = i.Status,
            AssigneeId = i.AssigneeId,
            AssigneeName = i.AssigneeId.HasValue && users.TryGetValue(i.AssigneeId.Value, out var user) ? user.FullName : null,
            IdentifiedDate = i.IdentifiedDate,
            TargetResolutionDate = i.TargetResolutionDate,
            ResolvedDate = i.ResolvedDate,
            Resolution = i.Resolution
        }).ToList();
    }

    private async Task<IReadOnlyList<SnapshotBudgetDto>> BuildBudgetsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _context.Set<Budget>()
            .Where(b => b.ProjectId == projectId && !b.IsDeleted)
            .OrderBy(b => b.Name)
            .Select(b => new SnapshotBudgetDto
            {
                OriginalId = b.Id,
                Name = b.Name,
                Description = b.Description,
                Category = b.Category,
                PlannedAmount = b.PlannedAmount,
                ApprovedAmount = b.ApprovedAmount,
                ActualAmount = b.ActualAmount,
                CommittedAmount = b.CommittedAmount,
                ForecastAmount = b.ForecastAmount,
                Currency = b.Currency,
                FiscalYear = b.FiscalYear,
                FiscalQuarter = b.FiscalQuarter,
                Status = b.Status,
                StartDate = b.StartDate,
                EndDate = b.EndDate
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SnapshotCostEntryDto>> BuildCostEntriesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _context.Set<CostEntry>()
            .Where(c => c.ProjectId == projectId && !c.IsDeleted)
            .OrderByDescending(c => c.IncurredDate)
            .Select(c => new SnapshotCostEntryDto
            {
                OriginalId = c.Id,
                BudgetId = c.BudgetId,
                ResourceId = c.ResourceId,
                Category = c.Category,
                SubCategory = c.SubCategory,
                Description = c.Description,
                Amount = c.Amount,
                Currency = c.Currency,
                TaxAmount = c.TaxAmount,
                IncurredDate = c.IncurredDate,
                Type = c.Type,
                Reference = c.Reference,
                VendorName = c.VendorName,
                Status = c.Status
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SnapshotMeetingDto>> BuildMeetingsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var meetings = await _context.Set<Meeting>()
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .OrderByDescending(m => m.StartTime)
            .ToListAsync(cancellationToken);

        var organizerIds = meetings.Where(m => m.OrganizerId.HasValue).Select(m => m.OrganizerId!.Value).Distinct().ToList();
        var users = new Dictionary<Guid, User>();
        if (organizerIds.Count > 0)
        {
            users = await _context.Set<User>().Where(u => organizerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        return meetings.Select(m => new SnapshotMeetingDto
        {
            OriginalId = m.Id,
            Title = m.Title,
            Description = m.Description,
            MeetingType = m.MeetingType,
            Location = m.Location,
            StartTime = m.StartTime,
            EndTime = m.EndTime,
            Status = m.Status,
            Notes = m.Notes,
            OrganizerId = m.OrganizerId,
            OrganizerName = m.OrganizerId.HasValue && users.TryGetValue(m.OrganizerId.Value, out var user) ? user.FullName : null,
            Recurrence = m.Recurrence
        }).ToList();
    }

    private async Task<IReadOnlyList<SnapshotDocumentDto>> BuildDocumentsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var documents = await _context.Set<Document>()
            .Where(d => d.ProjectId == projectId && !d.IsDeleted)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

        var ownerIds = documents.Where(d => d.OwnerId.HasValue).Select(d => d.OwnerId!.Value).Distinct().ToList();
        var users = new Dictionary<Guid, User>();
        if (ownerIds.Count > 0)
        {
            users = await _context.Set<User>().Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        }

        return documents.Select(d => new SnapshotDocumentDto
        {
            OriginalId = d.Id,
            Name = d.Name,
            Description = d.Description,
            FileName = d.FileName,
            FileType = d.FileType,
            FileSize = d.FileSize,
            Tags = d.Tags,
            Status = d.Status,
            Version = d.Version,
            OwnerId = d.OwnerId,
            OwnerName = d.OwnerId.HasValue && users.TryGetValue(d.OwnerId.Value, out var user) ? user.FullName : null
        }).ToList();
    }

    private static void ApplyProjectFields(ProjectSnapshot snapshot, Project project)
    {
        snapshot.Name = project.Name;
        snapshot.Code = project.Code;
        snapshot.Description = project.Description;
        snapshot.ProgramId = project.ProgramId;
        snapshot.Status = project.Status;
        snapshot.Priority = project.Priority;
        snapshot.Category = project.Category;
        snapshot.SponsorId = project.SponsorId;
        snapshot.PortfolioId = project.PortfolioId;
        snapshot.DepartmentId = project.DepartmentId;
        snapshot.StartDate = project.StartDate;
        snapshot.EndDate = project.EndDate;
        snapshot.ActualStartDate = project.ActualStartDate;
        snapshot.ActualEndDate = project.ActualEndDate;
        snapshot.Budget = project.Budget;
        snapshot.Currency = project.Currency;
        snapshot.ActualCost = project.ActualCost;
        snapshot.PlannedValue = project.PlannedValue;
        snapshot.EarnedValue = project.EarnedValue;
        snapshot.CostVariance = null;
        snapshot.ScheduleVariance = null;
        snapshot.Progress = project.Progress;
        snapshot.Health = project.Health;
        snapshot.RagStatus = project.RagStatus;
        snapshot.ConfidenceLevel = project.ConfidenceLevel;
        snapshot.Phase = project.Phase;
        snapshot.RiskRating = project.RiskRating;
        snapshot.BusinessCase = project.BusinessCase;
        snapshot.Notes = project.Notes;
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

    private static Guid? TryParseGuid(string value)
    {
        return GuidHelper.TryParse(value);
    }
}
