using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Api.PortfolioManagement;
using NEXUS.Api.ProgramManagement;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Api.Governance;

public sealed class AssignmentRecoveryService
{
    private const string Pending = "Pending";
    private const string Accepted = "Accepted";
    private const string Rejected = "Rejected";
    private readonly AppDbContext _db;
    private readonly IProjectHandoverRequestService _projectHandovers;
    private readonly IManagerHandoverService _managerHandovers;
    private readonly ProjectSponsorMembershipSynchronizer _sponsorSync;
    private readonly ProjectManagerMembershipSynchronizer _projectManagerSync;
    private readonly IUserEffectivePermissionService _effectivePermissions;

    public AssignmentRecoveryService(
        AppDbContext db,
        IProjectHandoverRequestService projectHandovers,
        IManagerHandoverService managerHandovers,
        ProjectSponsorMembershipSynchronizer sponsorSync,
        ProjectManagerMembershipSynchronizer projectManagerSync,
        IUserEffectivePermissionService effectivePermissions)
    {
        _db = db;
        _projectHandovers = projectHandovers;
        _managerHandovers = managerHandovers;
        _sponsorSync = sponsorSync;
        _projectManagerSync = projectManagerSync;
        _effectivePermissions = effectivePermissions;
    }

    public async Task<AssignmentRecoveryPageDto> GetPageAsync(
        Guid tenantId, Guid actorId, string entityType, string? search, string? filter,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        await EnsureAnyPermissionAsync(actorId, tenantId, cancellationToken,
            AssignmentRecoveryPermissions.View, AssignmentRecoveryPermissions.Initiate, AssignmentRecoveryPermissions.Manage);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        filter = NormalizeFilter(filter);
        var term = search?.Trim();
        var (items, total) = entityType.ToLowerInvariant() switch
        {
            "project" or "projects" => await GetProjectPageAsync(tenantId, term, filter, page, pageSize, cancellationToken),
            "program" or "programs" => await GetProgramPageAsync(tenantId, term, filter, page, pageSize, cancellationToken),
            "portfolio" or "portfolios" => await GetPortfolioPageAsync(tenantId, term, filter, page, pageSize, cancellationToken),
            _ => throw new AssignmentRecoveryException(400, "Select Projects, Programs, or Portfolios.")
        };

        return new AssignmentRecoveryPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            Summary = await GetSummaryAsync(tenantId, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<AssignmentRecoveryCandidateDto>> GetManagerCandidatesAsync(
        Guid tenantId, Guid actorId, string entityType, Guid publicId, CancellationToken cancellationToken)
    {
        await EnsureAnyPermissionAsync(actorId, tenantId, cancellationToken,
            AssignmentRecoveryPermissions.Initiate, AssignmentRecoveryPermissions.Manage);
        switch (entityType.ToLowerInvariant())
        {
            case "project":
            {
                var entity = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken)
                    ?? throw NotFound();
                var result = await _projectHandovers.GetEligibleManagersAsync(entity.Id, cancellationToken);
                if (result.IsFailure) throw FromError(result.Error);
                return result.Value!.Select(x => new AssignmentRecoveryCandidateDto { UserId = x.Id, FullName = x.FullName, Email = x.Email, JobTitle = x.JobTitle, ManagerTypes = x.ManagerTypes }).ToArray();
            }
            case "program":
            {
                var entity = await _db.Programs.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken)
                    ?? throw NotFound();
                var result = await _managerHandovers.GetEligibleProgramManagersAsync(entity.Id, actorId, cancellationToken);
                if (result.IsFailure) throw FromError(result.Error);
                return result.Value!.Select(x => new AssignmentRecoveryCandidateDto { UserId = x.Id, FullName = x.FullName, Email = x.Email, JobTitle = x.JobTitle, ManagerTypes = x.ManagerTypes }).ToArray();
            }
            case "portfolio":
            {
                var entity = await _db.Portfolios.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken)
                    ?? throw NotFound();
                var result = await _managerHandovers.GetEligiblePortfolioManagersAsync(entity.Id, actorId, cancellationToken);
                if (result.IsFailure) throw FromError(result.Error);
                return result.Value!.Select(x => new AssignmentRecoveryCandidateDto { UserId = x.Id, FullName = x.FullName, Email = x.Email, JobTitle = x.JobTitle, ManagerTypes = x.ManagerTypes }).ToArray();
            }
            default:
                throw new AssignmentRecoveryException(400, "Unsupported entity type.");
        }
    }

    public async Task<IReadOnlyList<AssignmentRecoveryCandidateDto>> GetSponsorCandidatesAsync(
        Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(actorId, tenantId, AssignmentRecoveryPermissions.Manage, cancellationToken);
        return await _db.Users.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName)
            .Select(x => new AssignmentRecoveryCandidateDto { UserId = x.Id, FullName = x.FirstName + " " + x.LastName, Email = x.Email, JobTitle = x.JobTitle })
            .ToListAsync(cancellationToken);
    }

    public async Task<RecoveryAssignmentCreatedDto> InitiateInitialAssignmentAsync(
        Guid tenantId, Guid actorId, string entityType, Guid publicId, CreateRecoveryAssignmentDto input,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(actorId, tenantId, AssignmentRecoveryPermissions.Initiate, cancellationToken);
        await EnsurePermissionAsync(actorId, tenantId, "Handover.AdminInitiate", cancellationToken);
        if (input.ManagerId == Guid.Empty) throw new AssignmentRecoveryException(400, "Select an incoming manager.");
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
            throw new AssignmentRecoveryException(400, "Enter an assignment reason of 1–500 characters.");

        switch (entityType.ToLowerInvariant())
        {
            case "project":
            {
                var entity = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                if (entity.ProjectManagerId.HasValue) throw new AssignmentRecoveryException(409, "This Project already has a manager. Use administrative handover instead.");
                if (await _db.ProjectManagerAssignments.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.ProjectId == entity.Id && x.IsCurrent, cancellationToken))
                    throw new AssignmentRecoveryException(409, "The Project has an active manager-history record that needs reconciliation first.");
                var result = await _projectHandovers.CreateInitialAssignmentAsync(entity.Id, new CreateProjectHandoverRequestDto
                {
                    NewProjectManagerId = input.ManagerId, EffectiveHandoverDate = input.EffectiveDate, Reason = input.Reason, Notes = input.Notes
                }, actorId.ToString(), cancellationToken);
                if (result.IsFailure) throw FromError(result.Error);
                return new RecoveryAssignmentCreatedDto { RequestId = result.Value!.Id, EntityType = "Project", EntityPublicId = entity.PublicId, EntityName = entity.Name, Status = result.Value.Status, InitiationType = result.Value.InitiationType, ProposedManagerName = result.Value.NewProjectManagerName, InitiatedByName = result.Value.RequestedByName, RequestedAt = result.Value.RequestedAt };
            }
            case "program":
            {
                var entity = await _db.Programs.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                if (entity.ProgramManagerId.HasValue) throw new AssignmentRecoveryException(409, "This Program already has a manager. Use administrative handover instead.");
                if (await _db.ProgramManagerAssignments.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.ProgramId == entity.Id && x.IsCurrent, cancellationToken))
                    throw new AssignmentRecoveryException(409, "The Program has an active manager-history record that needs reconciliation first.");
                var result = await _managerHandovers.CreateInitialProgramAssignmentAsync(entity.Id, ToManagerInput(input), actorId, cancellationToken);
                if (result.IsFailure) throw FromError(result.Error);
                return MapCreated("Program", entity.PublicId, entity.Name, result.Value!);
            }
            case "portfolio":
            {
                var entity = await _db.Portfolios.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                if (entity.OwnerId.HasValue) throw new AssignmentRecoveryException(409, "This Portfolio already has a manager. Use administrative handover instead.");
                if (await _db.PortfolioManagerAssignments.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.PortfolioId == entity.Id && x.IsCurrent, cancellationToken))
                    throw new AssignmentRecoveryException(409, "The Portfolio has an active manager-history record that needs reconciliation first.");
                var result = await _managerHandovers.CreateInitialPortfolioAssignmentAsync(entity.Id, ToManagerInput(input), actorId, cancellationToken);
                if (result.IsFailure) throw FromError(result.Error);
                return MapCreated("Portfolio", entity.PublicId, entity.Name, result.Value!);
            }
            default:
                throw new AssignmentRecoveryException(400, "Unsupported entity type.");
        }
    }

    public async Task<RecoveryAssignmentCreatedDto> InitiateAdministrativeHandoverAsync(
        Guid tenantId, Guid actorId, string entityType, Guid publicId, CreateRecoveryAssignmentDto input,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(actorId, tenantId, AssignmentRecoveryPermissions.Manage, cancellationToken);
        await EnsurePermissionAsync(actorId, tenantId, "Handover.AdminInitiate", cancellationToken);
        if (input.ManagerId == Guid.Empty) throw new AssignmentRecoveryException(400, "Select an incoming manager.");
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
            throw new AssignmentRecoveryException(400, "Enter a handover reason of 1–500 characters.");

        switch (entityType.ToLowerInvariant())
        {
            case "project":
            {
                var entity = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                if (!entity.ProjectManagerId.HasValue) throw new AssignmentRecoveryException(409, "This Project has no manager. Use initial assignment instead.");
                if (await HasConflictingProjectAssignmentAsync(tenantId, entity.Id, entity.ProjectManagerId.Value, cancellationToken)) throw ReconcileFirst("Project");
                var result = await _projectHandovers.CreateAdminInitiatedAsync(entity.Id, new CreateProjectHandoverRequestDto
                {
                    NewProjectManagerId = input.ManagerId, EffectiveHandoverDate = input.EffectiveDate, Reason = input.Reason, Notes = input.Notes
                }, actorId.ToString(), cancellationToken);
                if (result.IsFailure) throw FromError(result.Error);
                return new RecoveryAssignmentCreatedDto { RequestId = result.Value!.Id, EntityType = "Project", EntityPublicId = entity.PublicId, EntityName = entity.Name, Status = result.Value.Status, InitiationType = result.Value.InitiationType, ProposedManagerName = result.Value.NewProjectManagerName, InitiatedByName = result.Value.RequestedByName, RequestedAt = result.Value.RequestedAt };
            }
            case "program":
            {
                var entity = await _db.Programs.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                if (!entity.ProgramManagerId.HasValue) throw new AssignmentRecoveryException(409, "This Program has no manager. Use initial assignment instead.");
                if (await HasConflictingProgramAssignmentAsync(tenantId, entity.Id, entity.ProgramManagerId.Value, cancellationToken)) throw ReconcileFirst("Program");
                var result = await _managerHandovers.CreateAdminProgramAsync(entity.Id, ToManagerInput(input), actorId, cancellationToken);
                if (result.IsFailure) throw FromError(result.Error);
                return MapCreated("Program", entity.PublicId, entity.Name, result.Value!);
            }
            case "portfolio":
            {
                var entity = await _db.Portfolios.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                if (!entity.OwnerId.HasValue) throw new AssignmentRecoveryException(409, "This Portfolio has no manager. Use initial assignment instead.");
                if (await HasConflictingPortfolioAssignmentAsync(tenantId, entity.Id, entity.OwnerId.Value, cancellationToken)) throw ReconcileFirst("Portfolio");
                var result = await _managerHandovers.CreateAdminPortfolioAsync(entity.Id, ToManagerInput(input), actorId, cancellationToken);
                if (result.IsFailure) throw FromError(result.Error);
                return MapCreated("Portfolio", entity.PublicId, entity.Name, result.Value!);
            }
            default:
                throw new AssignmentRecoveryException(400, "Unsupported entity type.");
        }
    }

    public async Task<AssignmentRecoveryItemDto> AssignProjectSponsorAsync(
        Guid tenantId, Guid actorId, Guid publicId, Guid sponsorUserId, CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(actorId, tenantId, AssignmentRecoveryPermissions.Manage, cancellationToken);
        if (sponsorUserId == Guid.Empty) throw new AssignmentRecoveryException(400, "Select a Sponsor.");
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await _db.Projects.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
        if (project.SponsorId.HasValue) throw new AssignmentRecoveryException(409, "This Project already has a Sponsor. Sponsor replacement is not part of recovery.");
        var sponsorError = await _sponsorSync.ValidateAssignmentAsync(sponsorUserId, tenantId, cancellationToken);
        if (sponsorError is not null) throw FromError(sponsorError);
        var actor = await GetActiveUserAsync(actorId, tenantId, cancellationToken);
        project.SponsorId = sponsorUserId;
        project.UpdatedBy = actorId.ToString();
        await _sponsorSync.SynchronizeAsync(project, null, sponsorUserId, actorId.ToString(), cancellationToken);
        var sponsor = await _db.Users.AsNoTracking().FirstAsync(x => x.Id == sponsorUserId && x.TenantId == tenantId, cancellationToken);
        _db.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = tenantId, EntityType = "Project", EntityId = project.Id, Action = "AssignmentRecoverySponsorAssigned",
            Changes = "Project Sponsor assigned by authorized governance recovery.",
            OriginalValues = JsonSerializer.Serialize(new { sponsorId = (Guid?)null }),
            NewValues = JsonSerializer.Serialize(new { sponsorId = sponsorUserId, sponsorName = sponsor.FullName, projectPublicId = project.PublicId }),
            UserId = actorId, UserName = actor.FullName, CreatedBy = actorId.ToString()
        });
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AssignmentRecoveryException(409, "The Project changed while Sponsor assignment was being saved. Refresh and try again.");
        }
        return new AssignmentRecoveryItemDto
        {
            EntityType = "Project", PublicId = project.PublicId, Name = project.Name, Code = project.Code,
            ManagerId = project.ProjectManagerId, ManagerAssignmentState = "CurrentStateUnchanged",
            SponsorId = sponsor.Id, SponsorName = sponsor.FullName, SponsorIsActive = sponsor.IsActive,
            SponsorState = "Assigned", ParentProgramName = await GetProgramNameAsync(tenantId, project.ProgramId, cancellationToken),
            ParentPortfolioName = await GetPortfolioNameAsync(tenantId, project.PortfolioId, cancellationToken),
            MissingParentHierarchy = !project.ProgramId.HasValue && !project.PortfolioId.HasValue,
            CreatedAt = project.CreatedAt, UpdatedAt = project.UpdatedAt
        };
    }

    public async Task<AssignmentRecoveryReconciledDto> ReconcileAsync(
        Guid tenantId, Guid actorId, string entityType, Guid publicId, CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(actorId, tenantId, AssignmentRecoveryPermissions.Manage, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var actor = await GetActiveUserAsync(actorId, tenantId, cancellationToken);
        var today = DateTime.UtcNow.Date;
        AssignmentRecoveryReconciledDto result;
        switch (entityType.ToLowerInvariant())
        {
            case "project":
            {
                var entity = await _db.Projects.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                var current = await _db.ProjectManagerAssignments.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProjectId == entity.Id && x.IsCurrent, cancellationToken);
                var directManagerIsActive = entity.ProjectManagerId.HasValue
                    && await IsActiveTenantUserAsync(entity.ProjectManagerId.Value, tenantId, cancellationToken);
                var currentManagerIsActive = current is not null
                    && await IsActiveTenantUserAsync(current.ProjectManagerId, tenantId, cancellationToken);
                var resolvedManagerId = directManagerIsActive
                    ? entity.ProjectManagerId
                    : currentManagerIsActive ? current!.ProjectManagerId : null;
                if (entity.ProjectManagerId == resolvedManagerId && current?.ProjectManagerId == resolvedManagerId
                    && (current is null || !current.IsDeleted))
                    throw new AssignmentRecoveryException(409, "No inconsistent Project Manager reference needs reconciliation.");

                var priorManagerId = current?.ProjectManagerId ?? Guid.Empty;
                var managersToRemove = new[] { entity.ProjectManagerId, current?.ProjectManagerId }
                    .Where(managerId => managerId.HasValue && managerId != resolvedManagerId)
                    .Select(managerId => managerId!.Value).Distinct().ToArray();
                if (current is not null)
                {
                    current.IsCurrent = false; current.EndDate = today; current.UpdatedBy = actorId.ToString();
                }
                entity.ProjectManagerId = resolvedManagerId;
                entity.UpdatedBy = actorId.ToString();
                await _projectManagerSync.RemoveManagerMembershipsAsync(entity, managersToRemove, today, actorId.ToString(), cancellationToken);
                if (resolvedManagerId.HasValue)
                {
                    _db.ProjectManagerAssignments.Add(new ProjectManagerAssignment
                    {
                        TenantId = tenantId, ProjectId = entity.Id, ProjectManagerId = resolvedManagerId.Value,
                        StartDate = today, IsCurrent = true, HandoverFromProjectManagerId = priorManagerId == Guid.Empty ? null : priorManagerId,
                        HandoverDate = today, HandoverReason = "Administrative assignment recovery reconciliation",
                        CreatedBy = actorId.ToString()
                    });
                    await _projectManagerSync.SynchronizeAsync(entity, Guid.Empty, resolvedManagerId.Value, today, actorId.ToString(), cancellationToken);
                }
                AddRecoveryAudit(tenantId, entity.Id, "Project", entity.PublicId, actor, actorId, priorManagerId == Guid.Empty ? null : priorManagerId, resolvedManagerId, "Manager governance reference reconciled.");
                result = new AssignmentRecoveryReconciledDto { EntityType = "Project", EntityPublicId = entity.PublicId, ManagerAssignmentState = resolvedManagerId.HasValue ? "Active" : "Unassigned", Message = "Project manager references and team projection were reconciled." };
                break;
            }
            case "program":
            {
                var entity = await _db.Programs.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                var current = await _db.ProgramManagerAssignments.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProgramId == entity.Id && x.IsCurrent, cancellationToken);
                var directManagerIsActive = entity.ProgramManagerId.HasValue && await IsActiveTenantUserAsync(entity.ProgramManagerId.Value, tenantId, cancellationToken);
                var currentManagerIsActive = current is not null && await IsActiveTenantUserAsync(current.ProgramManagerId, tenantId, cancellationToken);
                var resolved = directManagerIsActive ? entity.ProgramManagerId : currentManagerIsActive ? current!.ProgramManagerId : null;
                if (entity.ProgramManagerId == resolved && current?.ProgramManagerId == resolved
                    && (current is null || !current.IsDeleted))
                    throw new AssignmentRecoveryException(409, "No inconsistent Program Manager reference needs reconciliation.");
                if (current is not null) { current.IsCurrent = false; current.EndDate = today; current.UpdatedBy = actorId.ToString(); }
                entity.ProgramManagerId = resolved;
                entity.UpdatedBy = actorId.ToString();
                if (resolved.HasValue) _db.ProgramManagerAssignments.Add(new ProgramManagerAssignment { TenantId = tenantId, ProgramId = entity.Id, ProgramManagerId = resolved.Value, StartDate = today, IsCurrent = true, HandoverFromProgramManagerId = current?.ProgramManagerId, HandoverDate = today, HandoverReason = "Administrative assignment recovery reconciliation", CreatedBy = actorId.ToString() });
                AddRecoveryAudit(tenantId, entity.Id, "Program", entity.PublicId, actor, actorId, current?.ProgramManagerId, resolved, "Manager governance reference reconciled.");
                result = new AssignmentRecoveryReconciledDto { EntityType = "Program", EntityPublicId = entity.PublicId, ManagerAssignmentState = resolved.HasValue ? "Active" : "Unassigned", Message = "Program manager references were reconciled." };
                break;
            }
            case "portfolio":
            {
                var entity = await _db.Portfolios.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                var current = await _db.PortfolioManagerAssignments.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PortfolioId == entity.Id && x.IsCurrent, cancellationToken);
                var directManagerIsActive = entity.OwnerId.HasValue && await IsActiveTenantUserAsync(entity.OwnerId.Value, tenantId, cancellationToken);
                var currentManagerIsActive = current is not null && await IsActiveTenantUserAsync(current.PortfolioManagerId, tenantId, cancellationToken);
                var resolved = directManagerIsActive ? entity.OwnerId : currentManagerIsActive ? current!.PortfolioManagerId : null;
                if (entity.OwnerId == resolved && current?.PortfolioManagerId == resolved
                    && (current is null || !current.IsDeleted))
                    throw new AssignmentRecoveryException(409, "No inconsistent Portfolio Manager reference needs reconciliation.");
                if (current is not null) { current.IsCurrent = false; current.EndDate = today; current.UpdatedBy = actorId.ToString(); }
                entity.OwnerId = resolved;
                entity.UpdatedBy = actorId.ToString();
                if (resolved.HasValue) _db.PortfolioManagerAssignments.Add(new PortfolioManagerAssignment { TenantId = tenantId, PortfolioId = entity.Id, PortfolioManagerId = resolved.Value, StartDate = today, IsCurrent = true, HandoverFromPortfolioManagerId = current?.PortfolioManagerId, HandoverDate = today, HandoverReason = "Administrative assignment recovery reconciliation", CreatedBy = actorId.ToString() });
                AddRecoveryAudit(tenantId, entity.Id, "Portfolio", entity.PublicId, actor, actorId, current?.PortfolioManagerId, resolved, "Manager governance reference reconciled.");
                result = new AssignmentRecoveryReconciledDto { EntityType = "Portfolio", EntityPublicId = entity.PublicId, ManagerAssignmentState = resolved.HasValue ? "Active" : "Unassigned", Message = "Portfolio manager references were reconciled." };
                break;
            }
            default:
                throw new AssignmentRecoveryException(400, "Unsupported entity type.");
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AssignmentRecoveryException(409, "The entity changed while reconciliation was in progress. Refresh and try again.");
        }
        return result;
    }

    public async Task<AssignmentRecoveryHistoryDto> GetHistoryAsync(
        Guid tenantId, Guid actorId, string entityType, Guid publicId, CancellationToken cancellationToken)
    {
        await EnsureAnyPermissionAsync(actorId, tenantId, cancellationToken,
            AssignmentRecoveryPermissions.View, AssignmentRecoveryPermissions.Initiate, AssignmentRecoveryPermissions.Manage);
        var items = new List<AssignmentRecoveryHistoryItemDto>();
        switch (entityType.ToLowerInvariant())
        {
            case "project":
            {
                var entity = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                var assignments = await _db.ProjectManagerAssignments.AsNoTracking().Where(x => x.TenantId == tenantId && x.ProjectId == entity.Id && !x.IsDeleted).OrderByDescending(x => x.StartDate).ToListAsync(cancellationToken);
                var requests = await _db.ProjectHandoverRequests.AsNoTracking().Where(x => x.TenantId == tenantId && x.ProjectId == entity.Id && !x.IsDeleted).OrderByDescending(x => x.RequestedAt).ToListAsync(cancellationToken);
                items.AddRange(await MapProjectHistoryAsync(tenantId, assignments, requests, cancellationToken));
                break;
            }
            case "program":
            {
                var entity = await _db.Programs.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                var assignments = await _db.ProgramManagerAssignments.AsNoTracking().Where(x => x.TenantId == tenantId && x.ProgramId == entity.Id && !x.IsDeleted).OrderByDescending(x => x.StartDate).ToListAsync(cancellationToken);
                var requests = await _db.ProgramHandoverRequests.AsNoTracking().Where(x => x.TenantId == tenantId && x.ProgramId == entity.Id && !x.IsDeleted).OrderByDescending(x => x.RequestedAt).ToListAsync(cancellationToken);
                items.AddRange(await MapManagerHistoryAsync(tenantId, assignments.Select(x => new AssignmentHistoryRow(x.Id, x.ProgramManagerId, x.HandoverFromProgramManagerId, x.StartDate, x.EndDate, x.IsCurrent, x.HandoverReason, x.HandoverNotes, x.CreatedBy)).ToList(), requests.Select(x => new RequestHistoryRow(x.Status, x.CurrentProgramManagerId, x.NewProgramManagerId, x.RequestedBy, x.RequestedAt, x.EffectiveHandoverDate, x.RespondedAt, x.Reason, x.Notes, x.RejectionReason, x.InitiationType)).ToList(), cancellationToken));
                break;
            }
            case "portfolio":
            {
                var entity = await _db.Portfolios.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PublicId == publicId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
                var assignments = await _db.PortfolioManagerAssignments.AsNoTracking().Where(x => x.TenantId == tenantId && x.PortfolioId == entity.Id && !x.IsDeleted).OrderByDescending(x => x.StartDate).ToListAsync(cancellationToken);
                var requests = await _db.PortfolioHandoverRequests.AsNoTracking().Where(x => x.TenantId == tenantId && x.PortfolioId == entity.Id && !x.IsDeleted).OrderByDescending(x => x.RequestedAt).ToListAsync(cancellationToken);
                items.AddRange(await MapManagerHistoryAsync(tenantId, assignments.Select(x => new AssignmentHistoryRow(x.Id, x.PortfolioManagerId, x.HandoverFromPortfolioManagerId, x.StartDate, x.EndDate, x.IsCurrent, x.HandoverReason, x.HandoverNotes, x.CreatedBy)).ToList(), requests.Select(x => new RequestHistoryRow(x.Status, x.CurrentPortfolioManagerId, x.NewPortfolioManagerId, x.RequestedBy, x.RequestedAt, x.EffectiveHandoverDate, x.RespondedAt, x.Reason, x.Notes, x.RejectionReason, x.InitiationType)).ToList(), cancellationToken));
                break;
            }
            default:
                throw new AssignmentRecoveryException(400, "Unsupported entity type.");
        }
        return new AssignmentRecoveryHistoryDto { Items = items.OrderByDescending(x => x.RequestedAt ?? x.EffectiveDate).ToArray() };
    }

    private async Task<(IReadOnlyList<AssignmentRecoveryItemDto>, int)> GetProjectPageAsync(Guid tenantId, string? search, string filter, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Projects.AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.Code.Contains(search));
        query = filter switch
        {
            "NoManager" => query.Where(x => !x.ProjectManagerId.HasValue),
            "NoSponsor" => query.Where(x => !x.SponsorId.HasValue),
            "MissingParentHierarchy" => query.Where(x => !x.ProgramId.HasValue && !x.PortfolioId.HasValue),
            "MissingGovernanceAssignment" => query.Where(x => !x.ProjectManagerId.HasValue
                || !_db.Users.Any(user => user.Id == x.ProjectManagerId && user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
                || !_db.ProjectManagerAssignments.Any(a => a.TenantId == tenantId && a.ProjectId == x.Id && a.ProjectManagerId == x.ProjectManagerId && a.IsCurrent && !a.IsDeleted)),
            "Pending" => query.Where(x => _db.ProjectHandoverRequests.Any(r => r.TenantId == tenantId && r.ProjectId == x.Id && r.Status == Pending && !r.IsDeleted)),
            "PendingInitialAssignment" => query.Where(x => _db.ProjectHandoverRequests.Any(r => r.TenantId == tenantId && r.ProjectId == x.Id && r.Status == Pending && r.InitiationType == ManagerHandoverInitiationType.InitialAssignment && !r.IsDeleted)),
            "Rejected" => query.Where(x => _db.ProjectHandoverRequests.Where(r => r.TenantId == tenantId && r.ProjectId == x.Id && !r.IsDeleted).OrderByDescending(r => r.RequestedAt).Select(r => r.Status).FirstOrDefault() == Rejected),
            "Assigned" => query.Where(x => x.ProjectManagerId.HasValue),
            _ => query
        };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new RecoverySource
            {
                Id = x.Id, PublicId = x.PublicId, Type = "Project", Name = x.Name, Code = x.Code,
                ManagerId = x.ProjectManagerId, SponsorId = x.SponsorId, ParentProgramId = x.ProgramId, ParentPortfolioId = x.PortfolioId,
                ParentProgramName = _db.Programs.Where(p => p.Id == x.ProgramId && p.TenantId == tenantId && !p.IsDeleted).Select(p => p.Name).FirstOrDefault(),
                ParentPortfolioName = _db.Portfolios.Where(p => p.Id == x.PortfolioId && p.TenantId == tenantId && !p.IsDeleted).Select(p => p.Name).FirstOrDefault(),
                CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
            }).ToListAsync(ct);
        return (await MaterializeRowsAsync(tenantId, rows, "Project", ct), total);
    }

    private async Task<(IReadOnlyList<AssignmentRecoveryItemDto>, int)> GetProgramPageAsync(Guid tenantId, string? search, string filter, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Programs.AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || (x.Code != null && x.Code.Contains(search)));
        query = filter switch
        {
            "NoManager" => query.Where(x => !x.ProgramManagerId.HasValue),
            "MissingParentHierarchy" => query.Where(x => !_db.Portfolios.Any(p => p.Id == x.PortfolioId && p.TenantId == tenantId && !p.IsDeleted)),
            "MissingGovernanceAssignment" => query.Where(x => !x.ProgramManagerId.HasValue
                || !_db.Users.Any(user => user.Id == x.ProgramManagerId && user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
                || !_db.ProgramManagerAssignments.Any(a => a.TenantId == tenantId && a.ProgramId == x.Id && a.ProgramManagerId == x.ProgramManagerId && a.IsCurrent && !a.IsDeleted)),
            "Pending" => query.Where(x => _db.ProgramHandoverRequests.Any(r => r.TenantId == tenantId && r.ProgramId == x.Id && r.Status == Pending && !r.IsDeleted)),
            "PendingInitialAssignment" => query.Where(x => _db.ProgramHandoverRequests.Any(r => r.TenantId == tenantId && r.ProgramId == x.Id && r.Status == Pending && r.InitiationType == ManagerHandoverInitiationType.InitialAssignment && !r.IsDeleted)),
            "Rejected" => query.Where(x => _db.ProgramHandoverRequests.Where(r => r.TenantId == tenantId && r.ProgramId == x.Id && !r.IsDeleted).OrderByDescending(r => r.RequestedAt).Select(r => r.Status).FirstOrDefault() == Rejected),
            "Assigned" => query.Where(x => x.ProgramManagerId.HasValue),
            _ => query
        };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new RecoverySource
            {
                Id = x.Id, PublicId = x.PublicId, Type = "Program", Name = x.Name, Code = x.Code,
                ManagerId = x.ProgramManagerId, ParentPortfolioId = x.PortfolioId,
                ParentPortfolioName = _db.Portfolios.Where(p => p.Id == x.PortfolioId && p.TenantId == tenantId && !p.IsDeleted).Select(p => p.Name).FirstOrDefault(),
                CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
            }).ToListAsync(ct);
        return (await MaterializeRowsAsync(tenantId, rows, "Program", ct), total);
    }

    private async Task<(IReadOnlyList<AssignmentRecoveryItemDto>, int)> GetPortfolioPageAsync(Guid tenantId, string? search, string filter, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Portfolios.AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || (x.Code != null && x.Code.Contains(search)));
        query = filter switch
        {
            "NoManager" => query.Where(x => !x.OwnerId.HasValue),
            "MissingGovernanceAssignment" => query.Where(x => !x.OwnerId.HasValue
                || !_db.Users.Any(user => user.Id == x.OwnerId && user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
                || !_db.PortfolioManagerAssignments.Any(a => a.TenantId == tenantId && a.PortfolioId == x.Id && a.PortfolioManagerId == x.OwnerId && a.IsCurrent && !a.IsDeleted)),
            "Pending" => query.Where(x => _db.PortfolioHandoverRequests.Any(r => r.TenantId == tenantId && r.PortfolioId == x.Id && r.Status == Pending && !r.IsDeleted)),
            "PendingInitialAssignment" => query.Where(x => _db.PortfolioHandoverRequests.Any(r => r.TenantId == tenantId && r.PortfolioId == x.Id && r.Status == Pending && r.InitiationType == ManagerHandoverInitiationType.InitialAssignment && !r.IsDeleted)),
            "Rejected" => query.Where(x => _db.PortfolioHandoverRequests.Where(r => r.TenantId == tenantId && r.PortfolioId == x.Id && !r.IsDeleted).OrderByDescending(r => r.RequestedAt).Select(r => r.Status).FirstOrDefault() == Rejected),
            "Assigned" => query.Where(x => x.OwnerId.HasValue),
            _ => query
        };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new RecoverySource
            {
                Id = x.Id, PublicId = x.PublicId, Type = "Portfolio", Name = x.Name, Code = x.Code,
                ManagerId = x.OwnerId, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
            }).ToListAsync(ct);
        return (await MaterializeRowsAsync(tenantId, rows, "Portfolio", ct), total);
    }

    private async Task<IReadOnlyList<AssignmentRecoveryItemDto>> MaterializeRowsAsync(Guid tenantId, List<RecoverySource> rows, string type, CancellationToken ct)
    {
        if (rows.Count == 0) return Array.Empty<AssignmentRecoveryItemDto>();
        var entityIds = rows.Select(x => x.Id).ToArray();
        var requestRows = type switch
        {
            "Project" => (await _db.ProjectHandoverRequests.AsNoTracking().Where(x => x.TenantId == tenantId && entityIds.Contains(x.ProjectId) && !x.IsDeleted).OrderByDescending(x => x.RequestedAt).ToListAsync(ct))
                .GroupBy(x => x.ProjectId).Select(g => new RecoveryRequestSource(g.Key, g.First().Status, g.First().InitiationType, g.First().CurrentProjectManagerId, g.First().NewProjectManagerId, g.First().RequestedBy, g.First().RequestedAt, g.First().RespondedAt, g.First().RejectionReason)).ToDictionary(x => x.EntityId),
            "Program" => (await _db.ProgramHandoverRequests.AsNoTracking().Where(x => x.TenantId == tenantId && entityIds.Contains(x.ProgramId) && !x.IsDeleted).OrderByDescending(x => x.RequestedAt).ToListAsync(ct))
                .GroupBy(x => x.ProgramId).Select(g => new RecoveryRequestSource(g.Key, g.First().Status, g.First().InitiationType, g.First().CurrentProgramManagerId, g.First().NewProgramManagerId, g.First().RequestedBy, g.First().RequestedAt, g.First().RespondedAt, g.First().RejectionReason)).ToDictionary(x => x.EntityId),
            _ => (await _db.PortfolioHandoverRequests.AsNoTracking().Where(x => x.TenantId == tenantId && entityIds.Contains(x.PortfolioId) && !x.IsDeleted).OrderByDescending(x => x.RequestedAt).ToListAsync(ct))
                .GroupBy(x => x.PortfolioId).Select(g => new RecoveryRequestSource(g.Key, g.First().Status, g.First().InitiationType, g.First().CurrentPortfolioManagerId, g.First().NewPortfolioManagerId, g.First().RequestedBy, g.First().RequestedAt, g.First().RespondedAt, g.First().RejectionReason)).ToDictionary(x => x.EntityId)
        };
        var assignmentRows = type switch
        {
            "Project" => (await _db.ProjectManagerAssignments.AsNoTracking().Where(x => x.TenantId == tenantId && entityIds.Contains(x.ProjectId) && x.IsCurrent && !x.IsDeleted).Select(x => new { x.ProjectId, x.ProjectManagerId }).ToListAsync(ct)).ToDictionary(x => x.ProjectId, x => (Guid?)x.ProjectManagerId),
            "Program" => (await _db.ProgramManagerAssignments.AsNoTracking().Where(x => x.TenantId == tenantId && entityIds.Contains(x.ProgramId) && x.IsCurrent && !x.IsDeleted).Select(x => new { x.ProgramId, x.ProgramManagerId }).ToListAsync(ct)).ToDictionary(x => x.ProgramId, x => (Guid?)x.ProgramManagerId),
            _ => (await _db.PortfolioManagerAssignments.AsNoTracking().Where(x => x.TenantId == tenantId && entityIds.Contains(x.PortfolioId) && x.IsCurrent && !x.IsDeleted).Select(x => new { x.PortfolioId, x.PortfolioManagerId }).ToListAsync(ct)).ToDictionary(x => x.PortfolioId, x => (Guid?)x.PortfolioManagerId)
        };
        var userIds = rows.SelectMany(x => new Guid?[] { x.ManagerId, x.SponsorId })
            .Concat(requestRows.Values.SelectMany(x => new Guid?[] { x.ProposedManagerId, x.InitiatedBy }))
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var users = await _db.Users.AsNoTracking().Where(x => x.TenantId == tenantId && userIds.Contains(x.Id) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id, ct);
        return rows.Select(row =>
        {
            users.TryGetValue(row.ManagerId ?? Guid.Empty, out var manager);
            users.TryGetValue(row.SponsorId ?? Guid.Empty, out var sponsor);
            requestRows.TryGetValue(row.Id, out var request);
            assignmentRows.TryGetValue(row.Id, out var assignedManager);
            var managerState = GetManagerState(row.ManagerId, manager?.IsActive, assignedManager);
            return new AssignmentRecoveryItemDto
            {
                EntityType = type, PublicId = row.PublicId, Name = row.Name, Code = row.Code,
                ManagerId = row.ManagerId, ManagerName = manager?.FullName, ManagerIsActive = manager?.IsActive,
                ManagerAssignmentState = managerState, SponsorId = row.SponsorId, SponsorName = sponsor?.FullName,
                SponsorIsActive = sponsor?.IsActive, SponsorState = type != "Project" ? "NotApplicable" : row.SponsorId.HasValue ? sponsor is null ? "Invalid" : sponsor.IsActive ? "Assigned" : "Inactive" : "Unassigned",
                ParentProgramName = row.ParentProgramName, ParentPortfolioName = row.ParentPortfolioName,
                MissingParentHierarchy = type == "Project" && !row.ParentProgramId.HasValue && !row.ParentPortfolioId.HasValue,
                AssignmentRequestStatus = request?.Status, AssignmentInitiationType = request?.InitiationType,
                ProposedManagerId = request?.ProposedManagerId, ProposedManagerName = request is not null && users.TryGetValue(request.ProposedManagerId, out var proposed) ? proposed.FullName : null,
                InitiatedByName = request is not null && users.TryGetValue(request.InitiatedBy, out var initiator) ? initiator.FullName : null,
                AssignmentRequestedAt = request?.RequestedAt, AssignmentRespondedAt = request?.RespondedAt, RejectionReason = request?.RejectionReason,
                CreatedAt = row.CreatedAt, UpdatedAt = row.UpdatedAt
            };
        }).ToArray();
    }

    private async Task<AssignmentRecoverySummaryDto> GetSummaryAsync(Guid tenantId, CancellationToken ct)
    {
        var projects = _db.Projects.AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted);
        var programs = _db.Programs.AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted);
        var portfolios = _db.Portfolios.AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted);
        var totalProjects = await projects.CountAsync(ct);
        var totalPrograms = await programs.CountAsync(ct);
        var totalPortfolios = await portfolios.CountAsync(ct);
        var unassignedManagers = await projects.CountAsync(x => !x.ProjectManagerId.HasValue, ct)
            + await programs.CountAsync(x => !x.ProgramManagerId.HasValue, ct)
            + await portfolios.CountAsync(x => !x.OwnerId.HasValue, ct);
        var inconsistentProjectAssignments = await projects.CountAsync(x => x.ProjectManagerId.HasValue
            ? !_db.Users.Any(user => user.Id == x.ProjectManagerId && user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
                || !_db.ProjectManagerAssignments.Any(a => a.TenantId == tenantId && a.ProjectId == x.Id && a.ProjectManagerId == x.ProjectManagerId && a.IsCurrent && !a.IsDeleted)
            : _db.ProjectManagerAssignments.Any(a => a.TenantId == tenantId && a.ProjectId == x.Id && a.IsCurrent && !a.IsDeleted), ct);
        var inconsistentProgramAssignments = await programs.CountAsync(x => x.ProgramManagerId.HasValue
            ? !_db.Users.Any(user => user.Id == x.ProgramManagerId && user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
                || !_db.ProgramManagerAssignments.Any(a => a.TenantId == tenantId && a.ProgramId == x.Id && a.ProgramManagerId == x.ProgramManagerId && a.IsCurrent && !a.IsDeleted)
            : _db.ProgramManagerAssignments.Any(a => a.TenantId == tenantId && a.ProgramId == x.Id && a.IsCurrent && !a.IsDeleted), ct);
        var inconsistentPortfolioAssignments = await portfolios.CountAsync(x => x.OwnerId.HasValue
            ? !_db.Users.Any(user => user.Id == x.OwnerId && user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
                || !_db.PortfolioManagerAssignments.Any(a => a.TenantId == tenantId && a.PortfolioId == x.Id && a.PortfolioManagerId == x.OwnerId && a.IsCurrent && !a.IsDeleted)
            : _db.PortfolioManagerAssignments.Any(a => a.TenantId == tenantId && a.PortfolioId == x.Id && a.IsCurrent && !a.IsDeleted), ct);
        var pending = await _db.ProjectHandoverRequests.CountAsync(x => x.TenantId == tenantId && x.Status == Pending && !x.IsDeleted, ct)
            + await _db.ProgramHandoverRequests.CountAsync(x => x.TenantId == tenantId && x.Status == Pending && !x.IsDeleted, ct)
            + await _db.PortfolioHandoverRequests.CountAsync(x => x.TenantId == tenantId && x.Status == Pending && !x.IsDeleted, ct);
        var rejected = await _db.ProjectHandoverRequests.CountAsync(x => x.TenantId == tenantId && x.Status == Rejected && !x.IsDeleted, ct)
            + await _db.ProgramHandoverRequests.CountAsync(x => x.TenantId == tenantId && x.Status == Rejected && !x.IsDeleted, ct)
            + await _db.PortfolioHandoverRequests.CountAsync(x => x.TenantId == tenantId && x.Status == Rejected && !x.IsDeleted, ct);
        var missingSponsors = await projects.CountAsync(x => !x.SponsorId.HasValue, ct);
        return new AssignmentRecoverySummaryDto
        {
            TotalProjects = totalProjects, TotalPrograms = totalPrograms, TotalPortfolios = totalPortfolios,
            UnassignedManagers = unassignedManagers,
            ProjectsWithoutSponsor = missingSponsors, PendingAssignments = pending, RejectedAssignments = rejected,
            InconsistentAssignments = inconsistentProjectAssignments + inconsistentProgramAssignments + inconsistentPortfolioAssignments
        };
    }

    private async Task<IReadOnlyList<AssignmentRecoveryHistoryItemDto>> MapProjectHistoryAsync(Guid tenantId, IReadOnlyList<ProjectManagerAssignment> assignments, IReadOnlyList<ProjectHandoverRequest> requests, CancellationToken ct)
    {
        var assignmentRows = assignments.Select(x => new AssignmentHistoryRow(x.Id, x.ProjectManagerId, x.HandoverFromProjectManagerId, x.StartDate, x.EndDate, x.IsCurrent, x.HandoverReason, x.HandoverNotes, x.CreatedBy)).ToList();
        var requestRows = requests.Select(x => new RequestHistoryRow(x.Status, x.CurrentProjectManagerId, x.NewProjectManagerId, x.RequestedBy, x.RequestedAt, x.EffectiveHandoverDate, x.RespondedAt, x.Reason, x.Notes, x.RejectionReason, x.InitiationType)).ToList();
        return await MapManagerHistoryAsync(tenantId, assignmentRows, requestRows, ct);
    }

    private async Task<IReadOnlyList<AssignmentRecoveryHistoryItemDto>> MapManagerHistoryAsync(Guid tenantId, List<AssignmentHistoryRow> assignments, List<RequestHistoryRow> requests, CancellationToken ct)
    {
        var ids = assignments.SelectMany(x => new Guid?[]
            { x.ManagerId, x.PreviousManagerId, Guid.TryParse(x.CreatedBy, out var createdBy) ? createdBy : null })
            .Concat(requests.SelectMany(x => new Guid?[] { x.ManagerId, x.PreviousManagerId, x.RequestedBy }))
            .Where(x => x.HasValue && x.Value != Guid.Empty).Select(x => x!.Value).Distinct().ToArray();
        var users = await _db.Users.AsNoTracking().Where(x => x.TenantId == tenantId && ids.Contains(x.Id) && !x.IsDeleted).ToDictionaryAsync(x => x.Id, ct);
        var result = new List<AssignmentRecoveryHistoryItemDto>();
        foreach (var row in assignments)
        {
            Guid.TryParse(row.CreatedBy, out var createdBy);
            result.Add(new AssignmentRecoveryHistoryItemDto
            {
                RecordType = "ManagerAssignment", Status = row.IsCurrent ? "Active" : "Historical",
                PreviousManagerName = row.PreviousManagerId.HasValue && users.TryGetValue(row.PreviousManagerId.Value, out var previous) ? previous.FullName : null,
                ManagerName = users.TryGetValue(row.ManagerId, out var manager) ? manager.FullName : "Unavailable user",
                InitiatedByName = users.TryGetValue(createdBy, out var initiator) ? initiator.FullName : null,
                EffectiveDate = row.StartDate, Reason = row.Reason, Notes = row.Notes
            });
        }
        foreach (var row in requests)
        {
            result.Add(new AssignmentRecoveryHistoryItemDto
            {
                RecordType = "HandoverRequest", Status = row.Status,
                PreviousManagerName = row.PreviousManagerId.HasValue && users.TryGetValue(row.PreviousManagerId.Value, out var previous) ? previous.FullName : null,
                ManagerName = users.TryGetValue(row.ManagerId, out var manager) ? manager.FullName : "Unavailable user",
                InitiatedByName = users.TryGetValue(row.RequestedBy, out var initiator) ? initiator.FullName : null,
                RequestedAt = row.RequestedAt, EffectiveDate = row.EffectiveDate, RespondedAt = row.RespondedAt,
                Reason = row.Reason, Notes = row.Notes, RejectionReason = row.RejectionReason
            });
        }
        return result;
    }

    private async Task<bool> HasConflictingProjectAssignmentAsync(Guid tenantId, Guid entityId, Guid managerId, CancellationToken ct)
    {
        var current = await _db.ProjectManagerAssignments.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProjectId == entityId && x.IsCurrent && !x.IsDeleted, ct);
        return current is not null && current.ProjectManagerId != managerId;
    }
    private async Task<bool> HasConflictingProgramAssignmentAsync(Guid tenantId, Guid entityId, Guid managerId, CancellationToken ct)
    {
        var current = await _db.ProgramManagerAssignments.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProgramId == entityId && x.IsCurrent && !x.IsDeleted, ct);
        return current is not null && current.ProgramManagerId != managerId;
    }
    private async Task<bool> HasConflictingPortfolioAssignmentAsync(Guid tenantId, Guid entityId, Guid managerId, CancellationToken ct)
    {
        var current = await _db.PortfolioManagerAssignments.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PortfolioId == entityId && x.IsCurrent && !x.IsDeleted, ct);
        return current is not null && current.PortfolioManagerId != managerId;
    }

    private async Task EnsurePermissionAsync(Guid userId, Guid tenantId, string permission, CancellationToken ct)
    {
        await GetActiveUserAsync(userId, tenantId, ct);
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, ct);
        if (!permissions.Contains(permission)) throw new AssignmentRecoveryException(403, "You do not have the required entity assignment recovery permission.");
    }

    private async Task EnsureAnyPermissionAsync(Guid userId, Guid tenantId, CancellationToken ct, params string[] required)
    {
        await GetActiveUserAsync(userId, tenantId, ct);
        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, ct);
        if (!required.Any(permissions.Contains)) throw new AssignmentRecoveryException(403, "You do not have permission to manage entity assignments.");
    }

    private async Task<User> GetActiveUserAsync(Guid userId, Guid tenantId, CancellationToken ct) =>
        await _db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId && x.IsActive && !x.IsDeleted, ct)
        ?? throw new AssignmentRecoveryException(403, "The authenticated user is not active in this tenant.");

    private Task<bool> IsActiveTenantUserAsync(Guid userId, Guid tenantId, CancellationToken ct) =>
        _db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.TenantId == tenantId && x.IsActive && !x.IsDeleted, ct);

    private async Task<string?> GetProgramNameAsync(Guid tenantId, Guid? id, CancellationToken ct) => id.HasValue
        ? await _db.Programs.AsNoTracking().Where(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
    private async Task<string?> GetPortfolioNameAsync(Guid tenantId, Guid? id, CancellationToken ct) => id.HasValue
        ? await _db.Portfolios.AsNoTracking().Where(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;

    private void AddRecoveryAudit(Guid tenantId, Guid entityId, string entityType, Guid publicId, User actor, Guid actorId, Guid? previous, Guid? next, string note) =>
        _db.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = tenantId, EntityType = entityType, EntityId = entityId, Action = "AssignmentRecoveryReconciled",
            Changes = note, OriginalValues = JsonSerializer.Serialize(new { managerId = previous, publicId }),
            NewValues = JsonSerializer.Serialize(new { managerId = next, publicId }), UserId = actorId,
            UserName = actor.FullName, CreatedBy = actorId.ToString()
        });

    private static string GetManagerState(Guid? managerId, bool? managerActive, Guid? assignmentManagerId)
    {
        if (!managerId.HasValue) return assignmentManagerId.HasValue ? "Inconsistent" : "Unassigned";
        if (managerActive == false || managerActive is null) return "ManagerInactive";
        if (!assignmentManagerId.HasValue) return "MissingAssignment";
        return assignmentManagerId == managerId ? "Active" : "Inconsistent";
    }

    private static string NormalizeFilter(string? value)
    {
        var allowed = new[] { "All", "NoManager", "NoSponsor", "MissingGovernanceAssignment", "PendingInitialAssignment", "Pending", "Rejected", "Assigned", "MissingParentHierarchy" };
        var normalized = string.IsNullOrWhiteSpace(value) ? "All" : value.Trim();
        return allowed.FirstOrDefault(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase))
            ?? throw new AssignmentRecoveryException(400, "Unsupported recovery filter.");
    }

    private static AssignmentRecoveryException NotFound() => new(404, "The requested entity is not available in the authenticated tenant.");
    private static AssignmentRecoveryException ReconcileFirst(string type) => new(409, $"The {type} manager reference and active assignment history do not match. Reconcile the governance record before initiating a handover.");
    private static AssignmentRecoveryException FromError(Error? error) => new(error?.StatusCode ?? 400, error?.Message ?? "The manager assignment could not be created.");
    private static CreateManagerHandoverRequestDto ToManagerInput(CreateRecoveryAssignmentDto input) => new() { NewManagerId = input.ManagerId, EffectiveHandoverDate = input.EffectiveDate, Reason = input.Reason, Notes = input.Notes };
    private static RecoveryAssignmentCreatedDto MapCreated(string entityType, Guid publicId, string entityName, ManagerHandoverRequestDto request) => new()
    {
        RequestId = request.Id, EntityType = entityType, EntityPublicId = publicId, EntityName = entityName,
        Status = request.Status, InitiationType = request.InitiationType, ProposedManagerName = request.NewManagerName,
        InitiatedByName = request.RequestedByName, RequestedAt = request.RequestedAt
    };

    private sealed class RecoverySource
    {
        public Guid Id { get; init; }
        public Guid PublicId { get; init; }
        public string Type { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Code { get; init; }
        public Guid? ManagerId { get; init; }
        public Guid? SponsorId { get; init; }
        public Guid? ParentProgramId { get; init; }
        public Guid? ParentPortfolioId { get; init; }
        public string? ParentProgramName { get; init; }
        public string? ParentPortfolioName { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
    private sealed record RecoveryRequestSource(Guid EntityId, string Status, string InitiationType, Guid? PreviousManagerId, Guid ProposedManagerId, Guid InitiatedBy, DateTime RequestedAt, DateTime? RespondedAt, string? RejectionReason);
    private sealed record AssignmentHistoryRow(Guid Id, Guid ManagerId, Guid? PreviousManagerId, DateTime StartDate, DateTime? EndDate, bool IsCurrent, string? Reason, string? Notes, string CreatedBy);
    private sealed record RequestHistoryRow(string Status, Guid? PreviousManagerId, Guid ManagerId, Guid RequestedBy, DateTime RequestedAt, DateTime EffectiveDate, DateTime? RespondedAt, string Reason, string? Notes, string? RejectionReason, string InitiationType);
}

public sealed class AssignmentRecoveryException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
