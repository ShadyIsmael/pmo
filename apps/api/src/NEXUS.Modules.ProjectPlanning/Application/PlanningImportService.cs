using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Application.Import;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ProjectPlanning.Application;

public sealed class PlanningImportService : IPlanningImportService
{
    private readonly IReadOnlyList<IPlanningImportProvider> _providers;
    private readonly DbContext _context;
    private readonly IResourceService _resourceService;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SummaryTaskRollupService _summaryRollup;

    public PlanningImportService(
        IEnumerable<IPlanningImportProvider> providers,
        DbContext context,
        IResourceService resourceService,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        SummaryTaskRollupService? summaryRollup = null)
    {
        _providers = providers.ToList();
        _context = context;
        _resourceService = resourceService;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _summaryRollup = summaryRollup ?? new SummaryTaskRollupService(context);
    }

    public async Task<Result<PlanImportSessionDto>> CreateSessionAsync(
        Stream file,
        string fileName,
        Guid projectId,
        Guid? targetPlanId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
            return Result<PlanImportSessionDto>.Failure(Error.Validation("A project is required"));

        if (targetPlanId.HasValue && !await _context.Set<Plan>().AnyAsync(plan => plan.Id == targetPlanId && plan.ProjectId == projectId, cancellationToken))
            return Result<PlanImportSessionDto>.Failure(Error.NotFound("The selected Plan was not found in this project"));

        var parse = await ParseAsync(file, fileName, cancellationToken);
        if (parse.IsFailure) return Result<PlanImportSessionDto>.Failure(parse.Error!);
        var document = parse.Value!;
        if (document.Tasks.Count == 0)
            return Result<PlanImportSessionDto>.Failure(Error.Validation("The MPP file does not contain any importable tasks"));

        var candidates = await LoadResourceCandidatesAsync(document.Resources, cancellationToken);
        var state = CreateInitialResolutionState(document, candidates);
        var session = new PlanImportSession
        {
            TenantId = GetCurrentTenantId(),
            ProjectId = projectId,
            FileName = fileName,
            Format = document.Format,
            SuggestedPlanName = FirstNonBlank(document.ProjectName, Path.GetFileNameWithoutExtension(fileName)) ?? "Imported Plan",
            Status = PlanImportSessionStatus.Parsed,
            ParsedDocumentJson = JsonSerializer.Serialize(document),
            ResolutionJson = JsonSerializer.Serialize(state),
            ParsedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        AddDocumentIssues(session, document);
        _context.Set<PlanImportSession>().Add(session);
        var review = BuildSessionReview(session, document, candidates, state);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<PlanImportSessionDto>.Success(review);
    }

    public async Task<Result<PlanImportSessionDto>> GetSessionAsync(Guid projectId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(projectId, sessionId, cancellationToken);
        if (session is null) return Result<PlanImportSessionDto>.Failure(Error.NotFound("The import review session was not found"));
        var document = DeserializeDocument(session.ParsedDocumentJson);
        var state = DeserializeResolutionState(session.ResolutionJson);
        var candidates = await LoadResourceCandidatesAsync(document.Resources, cancellationToken);
        var review = BuildSessionReview(session, document, candidates, state);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<PlanImportSessionDto>.Success(review);
    }

    public async Task<Result<PlanImportSessionDto>> ResolveResourceAsync(Guid projectId, Guid sessionId, string sourceResourceId, PlanImportResourceResolutionDto resolution, string userId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(projectId, sessionId, cancellationToken);
        if (session is null) return Result<PlanImportSessionDto>.Failure(Error.NotFound("The import review session was not found"));
        var document = DeserializeDocument(session.ParsedDocumentJson);
        var source = document.Resources.FirstOrDefault(item => item.SourceId == sourceResourceId);
        if (source is null) return Result<PlanImportSessionDto>.Failure(Error.NotFound("The imported resource was not found"));

        var normalized = resolution.Resolution.Trim();
        if (!string.Equals(normalized, "MapExisting", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalized, "CreateNew", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalized, "Ignore", StringComparison.OrdinalIgnoreCase))
            return Result<PlanImportSessionDto>.Failure(Error.Validation("Resource resolution must be MapExisting, CreateNew, or Ignore"));

        if (string.Equals(normalized, "MapExisting", StringComparison.OrdinalIgnoreCase))
        {
            if (!resolution.ResourceId.HasValue) return Result<PlanImportSessionDto>.Failure(Error.Validation("Choose an active NEXUS Resource to map"));
            var mapped = await _resourceService.GetByIdAsync(resolution.ResourceId.Value, cancellationToken);
            if (mapped.IsFailure || mapped.Value is null) return Result<PlanImportSessionDto>.Failure(Error.NotFound("The selected Resource was not found"));
            if (!mapped.Value.IsActive || string.Equals(mapped.Value.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
                return Result<PlanImportSessionDto>.Failure(Error.Validation("Inactive Resources cannot be mapped to an import"));
        }

        if (string.Equals(normalized, "CreateNew", StringComparison.OrdinalIgnoreCase))
        {
            if (resolution.CreateResource is null || string.IsNullOrWhiteSpace(resolution.CreateResource.Name))
                return Result<PlanImportSessionDto>.Failure(Error.Validation("Provide the Resource details before creating a new Resource"));
            if (!Enum.IsDefined(resolution.CreateResource.Type))
            {
                var supportedTypes = string.Join(", ", Enum.GetNames<ResourceType>());
                return Result<PlanImportSessionDto>.Failure(Error.Validation(
                    $"Resource type must be one of the supported values: {supportedTypes}."));
            }
            if (resolution.CreateResource.Type == ResourceType.Vendor && !resolution.CreateResource.VendorId.HasValue)
                return Result<PlanImportSessionDto>.Failure(Error.Validation("Select a Vendor for this resource."));
        }

        var state = DeserializeResolutionState(session.ResolutionJson);
        state.Resources[sourceResourceId] = new ImportResourceResolution
        {
            Resolution = normalized,
            ResourceId = resolution.ResourceId,
            CreateResource = resolution.CreateResource
        };
        session.ResolutionJson = JsonSerializer.Serialize(state);
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = userId;
        return await SaveAndReviewAsync(session, document, state, cancellationToken);
    }

    public async Task<Result<PlanImportSessionDto>> ResolveAssignmentAsync(Guid projectId, Guid sessionId, string sourceAssignmentId, PlanImportAssignmentResolutionDto resolution, string userId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(projectId, sessionId, cancellationToken);
        if (session is null) return Result<PlanImportSessionDto>.Failure(Error.NotFound("The import review session was not found"));
        var document = DeserializeDocument(session.ParsedDocumentJson);
        var assignment = document.Assignments.FirstOrDefault(item => AssignmentSourceId(item) == sourceAssignmentId);
        if (assignment is null) return Result<PlanImportSessionDto>.Failure(Error.NotFound("The imported assignment was not found"));
        var normalized = resolution.Resolution.Trim();
        if (!string.Equals(normalized, "Ready", StringComparison.OrdinalIgnoreCase) && !string.Equals(normalized, "Ignore", StringComparison.OrdinalIgnoreCase))
            return Result<PlanImportSessionDto>.Failure(Error.Validation("Assignment resolution must be Ready or Ignore"));
        var state = DeserializeResolutionState(session.ResolutionJson);
        state.Assignments[sourceAssignmentId] = normalized;
        session.ResolutionJson = JsonSerializer.Serialize(state);
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = userId;
        return await SaveAndReviewAsync(session, document, state, cancellationToken);
    }

    public async Task<Result<PlanImportSessionDto>> ResolveCalendarAsync(Guid projectId, Guid sessionId, string sourceCalendarId, PlanImportCalendarResolutionDto resolution, string userId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(projectId, sessionId, cancellationToken);
        if (session is null) return Result<PlanImportSessionDto>.Failure(Error.NotFound("The import review session was not found"));
        var document = DeserializeDocument(session.ParsedDocumentJson);
        if (document.Calendars.All(item => CalendarSourceId(item) != sourceCalendarId))
            return Result<PlanImportSessionDto>.Failure(Error.NotFound("The imported calendar was not found"));
        var normalized = resolution.Resolution.Trim();
        if (normalized is not ("Import" or "Simplify" or "Ignore"))
            return Result<PlanImportSessionDto>.Failure(Error.Validation("Calendar resolution must be Import, Simplify, or Ignore"));
        var state = DeserializeResolutionState(session.ResolutionJson);
        state.Calendars[sourceCalendarId] = normalized;
        session.ResolutionJson = JsonSerializer.Serialize(state);
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = userId;
        return await SaveAndReviewAsync(session, document, state, cancellationToken);
    }

    public async Task<Result<PlanImportResultDto>> CommitSessionAsync(Guid projectId, Guid sessionId, CommitPlanImportDto request, string userId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(projectId, sessionId, cancellationToken);
        if (session is null) return Result<PlanImportResultDto>.Failure(Error.NotFound("The import review session was not found"));
        if (session.Status == PlanImportSessionStatus.Committed && !string.IsNullOrWhiteSpace(session.CommitResultJson))
            return Result<PlanImportResultDto>.Success(JsonSerializer.Deserialize<PlanImportResultDto>(session.CommitResultJson)!);
        if (session.Status == PlanImportSessionStatus.Cancelled)
            return Result<PlanImportResultDto>.Failure(Error.Conflict("The import review session was cancelled"));

        var document = DeserializeDocument(session.ParsedDocumentJson);
        var state = DeserializeResolutionState(session.ResolutionJson);
        var candidates = await LoadResourceCandidatesAsync(document.Resources, cancellationToken);
        var review = BuildSessionReview(session, document, candidates, state);
        if (review.BlockingIssueCount > 0)
            return Result<PlanImportResultDto>.Failure(Error.Conflict("Resolve all blocking import issues before committing"));
        if (request.ProjectId == Guid.Empty) request.ProjectId = projectId;
        if (request.ProjectId != projectId) return Result<PlanImportResultDto>.Failure(Error.Validation("The import project does not match the review session"));
        if (!request.TargetPlanId.HasValue && request.NewPlanType is null)
            return Result<PlanImportResultDto>.Failure(Error.Validation("A Plan type is required when creating a new Plan"));

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var matches = new Dictionary<string, ResourceDto>(StringComparer.Ordinal);
            foreach (var source in document.Resources)
            {
                var selected = state.Resources.GetValueOrDefault(source.SourceId);
                if (selected is null || string.Equals(selected.Resolution, "Ignore", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(selected.Resolution, "MapExisting", StringComparison.OrdinalIgnoreCase))
                {
                    if (!selected.ResourceId.HasValue) throw new InvalidOperationException("A mapped Resource is missing its id");
                    var mapped = await _resourceService.GetByIdAsync(selected.ResourceId.Value, cancellationToken);
                    if (mapped.IsFailure || mapped.Value is null || !mapped.Value.IsActive) throw new InvalidOperationException("A mapped Resource is no longer active");
                    matches[source.SourceId] = mapped.Value;
                }
                else if (string.Equals(selected.Resolution, "CreateNew", StringComparison.OrdinalIgnoreCase))
                {
                    if (selected.CreateResource is null) throw new InvalidOperationException("A new Resource definition is missing");
                    var created = await _resourceService.CreateAsync(selected.CreateResource, userId, cancellationToken);
                    if (created.IsFailure || created.Value is null) throw new InvalidOperationException(created.Error?.Message ?? "The new Resource could not be created");
                    matches[source.SourceId] = created.Value;
                }
            }

            var ignoredAssignments = document.Assignments
                .Where(item => string.Equals(state.Assignments.GetValueOrDefault(AssignmentSourceId(item)), "Ignore", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(state.Resources.GetValueOrDefault(item.ResourceSourceId)?.Resolution, "Ignore", StringComparison.OrdinalIgnoreCase))
                .Select(AssignmentSourceId)
                .ToHashSet(StringComparer.Ordinal);
            var ignoredCalendars = document.Calendars
                .Where(item => string.Equals(state.Calendars.GetValueOrDefault(CalendarSourceId(item)), "Ignore", StringComparison.OrdinalIgnoreCase))
                .Select(CalendarSourceId)
                .ToHashSet(StringComparer.Ordinal);
            var commitResult = await CommitParsedDocumentAsync(document, session.FileName, request, userId, matches, ignoredAssignments, ignoredCalendars, cancellationToken);
            if (commitResult.IsFailure)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return commitResult;
            }

            session.Status = PlanImportSessionStatus.Committed;
            session.CommitResultJson = JsonSerializer.Serialize(commitResult.Value);
            session.CommittedAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;
            session.UpdatedBy = userId;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return commitResult;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PlanImportResultDto>.Failure(Error.Conflict("The Plan import could not be persisted atomically. No imported Plan data was saved."));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result> CancelSessionAsync(Guid projectId, Guid sessionId, string userId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(projectId, sessionId, cancellationToken);
        if (session is null) return Result.Failure(Error.NotFound("The import review session was not found"));
        if (session.Status == PlanImportSessionStatus.Committed) return Result.Failure(Error.Conflict("A committed import cannot be cancelled"));
        session.Status = PlanImportSessionStatus.Cancelled;
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<PlanImportResultDto>> CommitParsedDocumentAsync(
        PlanningImportDocument document,
        string fileName,
        CommitPlanImportDto request,
        string userId,
        IReadOnlyDictionary<string, ResourceDto> matches,
        IReadOnlySet<string> ignoredAssignments,
        IReadOnlySet<string>? ignoredCalendars = null,
        CancellationToken cancellationToken = default)
    {
        if (request.ProjectId == Guid.Empty)
            return Result<PlanImportResultDto>.Failure(Error.Validation("A project is required"));
        var tenantId = GetCurrentTenantId();
        Plan? plan;
        var createdPlan = !request.TargetPlanId.HasValue;
        if (request.TargetPlanId.HasValue)
        {
            plan = await _context.Set<Plan>().FirstOrDefaultAsync(candidate => candidate.Id == request.TargetPlanId && candidate.ProjectId == request.ProjectId, cancellationToken);
            if (plan is null) return Result<PlanImportResultDto>.Failure(Error.NotFound("The selected Plan was not found in this project"));
        }
        else
        {
            var planName = FirstNonBlank(request.NewPlanName, document.ProjectName, Path.GetFileNameWithoutExtension(fileName));
            if (string.IsNullOrWhiteSpace(planName)) return Result<PlanImportResultDto>.Failure(Error.Validation("A Plan name is required"));
            var normalizedPlanName = Truncate(planName.Trim(), 250);
            if (await _context.Set<Plan>().AnyAsync(candidate => candidate.ProjectId == request.ProjectId && candidate.PlanType == request.NewPlanType && candidate.Name == normalizedPlanName, cancellationToken))
                return Result<PlanImportResultDto>.Failure(Error.Conflict("A Plan with this name and type already exists for the project. Import into that Plan or choose another name."));
            plan = new Plan
            {
                TenantId = tenantId,
                ProjectId = request.ProjectId,
                PlanType = request.NewPlanType!.Value,
                Name = normalizedPlanName,
                Description = $"Imported from {fileName}",
                Status = PlanStatus.Draft,
                CreatedBy = userId
            };
            _context.Set<Plan>().Add(plan);
        }

        PlanVersion? previousWorking = null;
        if (!createdPlan)
        {
            previousWorking = await _context.Set<PlanVersion>().FirstOrDefaultAsync(version => version.PlanId == plan.Id && version.Type == PlanVersionType.Working && version.Status == PlanVersionStatus.Active, cancellationToken);
            if (previousWorking is not null)
            {
                previousWorking.IsDeleted = true;
                previousWorking.UpdatedAt = DateTime.UtcNow;
                previousWorking.UpdatedBy = userId;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        var previousMaxVersion = createdPlan
            ? 0
            : await _context.Set<PlanVersion>().IgnoreQueryFilters().Where(version => version.TenantId == tenantId && version.PlanId == plan.Id && version.Type == PlanVersionType.Working).Select(version => (int?)version.VersionNumber).MaxAsync(cancellationToken) ?? 0;
        var versionNumber = previousMaxVersion + 1;
        var currentBaselineId = createdPlan
            ? null
            : await _context.Set<PlanVersion>().Where(version => version.PlanId == plan.Id && version.Type == PlanVersionType.Baseline && version.Status == PlanVersionStatus.Approved).Select(version => (Guid?)version.Id).FirstOrDefaultAsync(cancellationToken);
        var version = new PlanVersion
        {
            TenantId = tenantId,
            PlanId = plan.Id,
            VersionNumber = versionNumber,
            Type = PlanVersionType.Working,
            Status = PlanVersionStatus.Active,
            Name = $"Imported Working v{versionNumber}",
            SourceBaselineId = currentBaselineId,
            CreatedBy = userId
        };
        _context.Set<PlanVersion>().Add(version);
        var warnings = document.Warnings.Select(ToDto).ToList();
        AddImportedModel(document, matches, plan, version, tenantId, userId, warnings, ignoredAssignments, ignoredCalendars);
        await _summaryRollup.RecalculateAsync(version.Id, userId, cancellationToken: cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        var result = new PlanImportResultDto
        {
            PlanId = plan.Id,
            PlanVersionId = version.Id,
            VersionNumber = version.VersionNumber,
            CreatedPlan = createdPlan,
            Statistics = Statistics(document),
            Warnings = warnings
        };
        var auditResult = await _auditService.LogAsync(new AuditEntry
        {
            TenantId = tenantId,
            EntityType = "PlanVersion",
            EntityId = version.Id,
            Action = "ImportMPP",
            UserId = TryParseGuid(userId),
            NewValues = JsonSerializer.Serialize(new { FileName = fileName, Format = document.Format, result.Statistics, ImportSession = true, ReplacedWorkingVersionId = previousWorking?.Id }),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);
        if (auditResult.IsFailure) throw new InvalidOperationException(auditResult.Error?.Message ?? "The import audit entry could not be saved");
        return Result<PlanImportResultDto>.Success(result);
    }

    private async Task<PlanImportSession?> LoadSessionAsync(Guid projectId, Guid sessionId, CancellationToken cancellationToken) =>
        await _context.Set<PlanImportSession>().Include(item => item.Issues).FirstOrDefaultAsync(item => item.Id == sessionId && item.ProjectId == projectId, cancellationToken);

    private async Task<Dictionary<string, List<ResourceDto>>> LoadResourceCandidatesAsync(IEnumerable<PlanningImportResource> imported, CancellationToken cancellationToken)
    {
        var resources = await _resourceService.GetPagedAsync(new ResourceFilterDto { Page = 1, PageSize = 5000, IsActive = null, SortBy = "name" }, cancellationToken);
        var result = new Dictionary<string, List<ResourceDto>>(StringComparer.Ordinal);
        foreach (var source in imported)
        {
            var byEmail = string.IsNullOrWhiteSpace(source.Email)
                ? []
                : resources.Items.Where(resource => string.Equals(resource.Email?.Trim(), source.Email.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            var byName = resources.Items.Where(resource => string.Equals(Normalize(source.Name), Normalize(resource.Name), StringComparison.OrdinalIgnoreCase)).ToList();
            result[source.SourceId] = byEmail.Count > 0 ? byEmail : byName;
        }
        return result;
    }

    private static ImportResolutionState CreateInitialResolutionState(PlanningImportDocument document, IReadOnlyDictionary<string, List<ResourceDto>> candidates)
    {
        var state = new ImportResolutionState();
        foreach (var source in document.Resources)
        {
            var sourceCandidates = candidates.GetValueOrDefault(source.SourceId) ?? [];
            var active = sourceCandidates.Where(item => item.IsActive && !string.Equals(item.Status, "Inactive", StringComparison.OrdinalIgnoreCase)).ToList();
            state.Resources[source.SourceId] = new ImportResourceResolution
            {
                Resolution = active.Count == 1 ? "MapExisting" : "Unresolved",
                ResourceId = active.Count == 1 ? active[0].Id : null
            };
        }
        var requiresCalendarReview = document.UnsupportedFields.Any(item => item.Code == "calendar-features" && item.Count > 0);
        foreach (var calendar in document.Calendars)
            state.Calendars[CalendarSourceId(calendar)] = requiresCalendarReview ? "Pending" : "Import";
        return state;
    }

    private static void AddDocumentIssues(PlanImportSession session, PlanningImportDocument document)
    {
        foreach (var notice in document.Warnings)
            session.Issues.Add(new PlanImportIssue { TenantId = session.TenantId, Severity = "Warning", Category = "Warning", Code = notice.Code, Message = notice.Message, Count = notice.Count, CreatedBy = session.CreatedBy });
        foreach (var notice in document.UnsupportedFields)
            session.Issues.Add(new PlanImportIssue { TenantId = session.TenantId, Severity = "Info", Category = "Unsupported", Code = notice.Code, Message = notice.Message, Count = notice.Count, CreatedBy = session.CreatedBy });
    }

    private async Task<Result<PlanImportSessionDto>> SaveAndReviewAsync(PlanImportSession session, PlanningImportDocument document, ImportResolutionState state, CancellationToken cancellationToken)
    {
        var candidates = await LoadResourceCandidatesAsync(document.Resources, cancellationToken);
        var review = BuildSessionReview(session, document, candidates, state);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<PlanImportSessionDto>.Success(review);
    }

    private static PlanImportSessionDto BuildSessionReview(PlanImportSession session, PlanningImportDocument document, IReadOnlyDictionary<string, List<ResourceDto>> candidates, ImportResolutionState state)
    {
        var preview = BuildPreview(document, session.FileName, new Dictionary<string, ResourceDto>(StringComparer.Ordinal));
        var issues = session.Issues.Select(issue => new PlanImportIssueDto
        {
            Id = issue.Id, Severity = issue.Severity, Category = issue.Category, EntityType = issue.EntityType, SourceId = issue.SourceId,
            Code = issue.Code, Message = issue.Message, Count = issue.Count, IsBlocking = issue.IsBlocking, Resolution = "Informational"
        }).ToList();
        var unresolvedResources = 0;
        foreach (var source in document.Resources)
        {
            var selected = state.Resources.GetValueOrDefault(source.SourceId);
            var resolution = selected?.Resolution ?? "Unresolved";
            if (resolution is "Unresolved" or "") unresolvedResources++;
            if (resolution is "Unresolved" or "") issues.Add(new PlanImportIssueDto { Id = Guid.NewGuid(), Severity = "Error", Category = "Resource", EntityType = "Resource", SourceId = source.SourceId, Code = "resource-resolution-required", Message = $"Resource '{source.Name}' must be mapped, created, or ignored before commit.", IsBlocking = true, Resolution = resolution });
            else if (resolution == "MapExisting" && selected?.ResourceId is null) issues.Add(new PlanImportIssueDto { Id = Guid.NewGuid(), Severity = "Error", Category = "Resource", EntityType = "Resource", SourceId = source.SourceId, Code = "resource-mapping-invalid", Message = $"Resource '{source.Name}' has an incomplete mapping.", IsBlocking = true, Resolution = resolution });
        }

        var requiresCalendarReview = document.UnsupportedFields.Any(item => item.Code == "calendar-features" && item.Count > 0);
        var assignmentReviews = document.Assignments.Select(item =>
        {
            var sourceId = AssignmentSourceId(item);
            var explicitResolution = state.Assignments.GetValueOrDefault(sourceId);
            var resourceResolution = state.Resources.GetValueOrDefault(item.ResourceSourceId)?.Resolution;
            var status = explicitResolution ?? (resourceResolution == "Ignore" ? "IgnoredByResource" : "Ready");
            if (item.WorkConversionStatus == "NeedsReview" && status != "Ignore")
            {
                issues.Add(new PlanImportIssueDto { Id = Guid.NewGuid(), Severity = "Error", Category = "Assignment", EntityType = "Assignment", SourceId = sourceId, Code = "assignment-work-review-required", Message = "Assignment Work could not be converted safely to total Planned Hours. Ignore this assignment or resolve its source value before commit.", IsBlocking = true, Resolution = status });
                status = "NeedsReview";
            }
            else if (resourceResolution is "Unresolved" or null && status != "Ignore")
            {
                issues.Add(new PlanImportIssueDto { Id = Guid.NewGuid(), Severity = "Error", Category = "Assignment", EntityType = "Assignment", SourceId = sourceId, Code = "assignment-resource-unresolved", Message = "This assignment waits for its imported Resource to be resolved.", IsBlocking = true, Resolution = status });
                status = "NeedsResource";
            }
            return new PlanImportAssignmentPreviewDto { SourceId = sourceId, TaskSourceId = item.TaskSourceId, ResourceSourceId = item.ResourceSourceId, PlannedHours = item.PlannedHours, WorkConversionStatus = item.WorkConversionStatus, AllocationPercent = item.AllocationPercent, Start = item.Start, Finish = item.Finish, Status = status };
        }).ToList();

        var calendars = document.Calendars.Select(calendar =>
        {
            var sourceId = CalendarSourceId(calendar);
            var resolution = state.Calendars.GetValueOrDefault(sourceId) ?? (requiresCalendarReview ? "Pending" : "Import");
            if (requiresCalendarReview && resolution == "Pending")
                issues.Add(new PlanImportIssueDto { Id = Guid.NewGuid(), Severity = "Error", Category = "Calendar", EntityType = "Calendar", SourceId = sourceId, Code = "calendar-review-required", Message = $"Calendar '{calendar.Name}' contains unsupported or complex rules and must be explicitly imported, simplified, or ignored.", IsBlocking = true, Resolution = resolution });
            return new PlanImportCalendarPreviewDto { SourceId = sourceId, Name = calendar.Name, IsDefault = calendar.IsDefault, WorkingDays = calendar.Days.Count(item => item.IsWorkingDay), Holidays = calendar.Holidays.Count, Resolution = resolution, RequiresReview = requiresCalendarReview };
        }).ToList();
        var blocking = issues.Count(item => item.IsBlocking && item.Resolution is not "Ignore");
        session.Status = blocking > 0 ? PlanImportSessionStatus.NeedsReview : PlanImportSessionStatus.ReadyToCommit;
        return new PlanImportSessionDto
        {
            Id = session.Id, ProjectId = session.ProjectId, FileName = session.FileName, Format = session.Format, SuggestedPlanName = session.SuggestedPlanName,
            Status = session.Status.ToString(), Statistics = preview.Statistics, Tasks = preview.Tasks, Dependencies = preview.Dependencies,
            Resources = document.Resources.Select(source => new PlanImportResourcePreviewDto
            {
                SourceId = source.SourceId, Name = source.Name, Email = source.Email, AssignmentCount = document.Assignments.Count(item => item.ResourceSourceId == source.SourceId),
                Resolution = state.Resources.GetValueOrDefault(source.SourceId)?.Resolution ?? "Unresolved",
                Candidates = (candidates.GetValueOrDefault(source.SourceId) ?? []).Select(candidate => new PlanImportResourceCandidateDto { Id = candidate.Id, Name = candidate.Name, Email = candidate.Email, Type = candidate.Type, IsActive = candidate.IsActive }).ToList(),
                NexusResourceId = state.Resources.GetValueOrDefault(source.SourceId)?.ResourceId,
                MatchStatus = state.Resources.GetValueOrDefault(source.SourceId)?.Resolution == "MapExisting" ? "Matched" : "Unmatched"
            }).ToList(),
            Assignments = assignmentReviews,
            Calendars = calendars,
            Warnings = preview.Warnings,
            UnsupportedFields = preview.UnsupportedFields,
            Issues = issues,
            BlockingIssueCount = blocking,
            UnresolvedResourceCount = unresolvedResources,
            IgnoredAssignmentCount = assignmentReviews.Count(item => item.Status.StartsWith("Ignored", StringComparison.OrdinalIgnoreCase) || item.Status == "Ignore")
        };
    }

    private static PlanningImportDocument DeserializeDocument(string json) => JsonSerializer.Deserialize<PlanningImportDocument>(json) ?? new PlanningImportDocument();
    private static ImportResolutionState DeserializeResolutionState(string json) => JsonSerializer.Deserialize<ImportResolutionState>(json) ?? new ImportResolutionState();
    private static string AssignmentSourceId(PlanningImportAssignment assignment) => string.IsNullOrWhiteSpace(assignment.SourceId) ? $"{assignment.TaskSourceId}|{assignment.ResourceSourceId}" : assignment.SourceId;
    private static string CalendarSourceId(PlanningImportCalendar calendar) => string.IsNullOrWhiteSpace(calendar.SourceId) ? calendar.Name : calendar.SourceId;
    private static string Normalize(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed class ImportResolutionState
    {
        public Dictionary<string, ImportResourceResolution> Resources { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Assignments { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Calendars { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class ImportResourceResolution
    {
        public string Resolution { get; set; } = "Unresolved";
        public Guid? ResourceId { get; set; }
        public CreateResourceDto? CreateResource { get; set; }
    }

    public async Task<Result<PlanImportPreviewDto>> PreviewAsync(
        Stream file,
        string fileName,
        Guid projectId,
        Guid? targetPlanId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
            return Result<PlanImportPreviewDto>.Failure(Error.Validation("A project is required"));

        if (targetPlanId.HasValue)
        {
            var target = await _context.Set<Plan>()
                .FirstOrDefaultAsync(plan => plan.Id == targetPlanId && plan.ProjectId == projectId, cancellationToken);
            if (target is null)
                return Result<PlanImportPreviewDto>.Failure(Error.NotFound("The selected Plan was not found in this project"));
        }

        var parse = await ParseAsync(file, fileName, cancellationToken);
        if (parse.IsFailure)
            return Result<PlanImportPreviewDto>.Failure(parse.Error!);

        var document = parse.Value!;
        var matches = await MatchResourcesAsync(document.Resources, cancellationToken);
        var preview = BuildPreview(document, fileName, matches);
        if (targetPlanId.HasValue && await _context.Set<PlanVersion>().AnyAsync(
                version => version.PlanId == targetPlanId && version.Type == PlanVersionType.Working && version.Status == PlanVersionStatus.Active,
                cancellationToken))
        {
            preview.Warnings = preview.Warnings.Append(new PlanImportNoticeDto
            {
                Code = "working-version-replaced",
                Message = "The existing active Working version will be retained in history as deleted and replaced by a new imported Working version. Approved Baselines are never changed.",
                Count = 1
            }).ToList();
        }

        return Result<PlanImportPreviewDto>.Success(preview);
    }

    public async Task<Result<PlanImportResultDto>> ImportAsync(
        Stream file,
        string fileName,
        CommitPlanImportDto request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (request.ProjectId == Guid.Empty)
            return Result<PlanImportResultDto>.Failure(Error.Validation("A project is required"));
        if (!request.TargetPlanId.HasValue && request.NewPlanType is null)
            return Result<PlanImportResultDto>.Failure(Error.Validation("A Plan type is required when creating a new Plan"));

        var parse = await ParseAsync(file, fileName, cancellationToken);
        if (parse.IsFailure)
            return Result<PlanImportResultDto>.Failure(parse.Error!);

        var document = parse.Value!;
        if (document.Tasks.Count == 0)
            return Result<PlanImportResultDto>.Failure(Error.Validation("The MPP file does not contain any importable tasks"));

        var tenantId = GetCurrentTenantId();
        Plan? plan;
        var createdPlan = !request.TargetPlanId.HasValue;
        if (request.TargetPlanId.HasValue)
        {
            plan = await _context.Set<Plan>()
                .FirstOrDefaultAsync(candidate => candidate.Id == request.TargetPlanId && candidate.ProjectId == request.ProjectId, cancellationToken);
            if (plan is null)
                return Result<PlanImportResultDto>.Failure(Error.NotFound("The selected Plan was not found in this project"));
        }
        else
        {
            var planName = FirstNonBlank(request.NewPlanName, document.ProjectName, Path.GetFileNameWithoutExtension(fileName));
            if (string.IsNullOrWhiteSpace(planName))
                return Result<PlanImportResultDto>.Failure(Error.Validation("A Plan name is required"));
            var normalizedPlanName = Truncate(planName.Trim(), 250);
            if (await _context.Set<Plan>().AnyAsync(
                    candidate => candidate.ProjectId == request.ProjectId && candidate.PlanType == request.NewPlanType && candidate.Name == normalizedPlanName,
                    cancellationToken))
            {
                return Result<PlanImportResultDto>.Failure(Error.Conflict("A Plan with this name and type already exists for the project. Import into that Plan or choose another name."));
            }

            plan = new Plan
            {
                TenantId = tenantId,
                ProjectId = request.ProjectId,
                PlanType = request.NewPlanType!.Value,
                Name = normalizedPlanName,
                Description = $"Imported from {fileName}",
                Status = PlanStatus.Draft,
                CreatedBy = userId
            };
        }

        var matches = await MatchResourcesAsync(document.Resources, cancellationToken);
        var importWarnings = document.Warnings.Select(ToDto).ToList();
        var unmatchedAssignments = document.Assignments.Count(assignment => !matches.ContainsKey(assignment.ResourceSourceId));
        if (unmatchedAssignments > 0)
        {
            importWarnings.Add(new PlanImportNoticeDto
            {
                Code = "unmatched-resource-assignments",
                Message = "Assignments for resources that could not be matched uniquely by email or name were not imported.",
                Count = unmatchedAssignments
            });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (createdPlan)
            {
                _context.Set<Plan>().Add(plan);
            }

            PlanVersion? previousWorking = null;
            if (!createdPlan)
            {
                previousWorking = await _context.Set<PlanVersion>()
                    .FirstOrDefaultAsync(
                        version => version.PlanId == plan.Id && version.Type == PlanVersionType.Working && version.Status == PlanVersionStatus.Active,
                        cancellationToken);
                if (previousWorking is not null)
                {
                    previousWorking.IsDeleted = true;
                    previousWorking.UpdatedAt = DateTime.UtcNow;
                    previousWorking.UpdatedBy = userId;
                    // Persist inside this transaction before inserting the next
                    // active Working row so the filtered unique index is satisfied.
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            var previousMaxVersion = createdPlan
                ? 0
                : await _context.Set<PlanVersion>().IgnoreQueryFilters()
                    .Where(version => version.TenantId == tenantId && version.PlanId == plan.Id && version.Type == PlanVersionType.Working)
                    .Select(version => (int?)version.VersionNumber)
                    .MaxAsync(cancellationToken) ?? 0;
            var versionNumber = previousMaxVersion + 1;
            var currentBaselineId = createdPlan
                ? null
                : await _context.Set<PlanVersion>()
                    .Where(version => version.PlanId == plan.Id && version.Type == PlanVersionType.Baseline && version.Status == PlanVersionStatus.Approved)
                    .Select(version => (Guid?)version.Id)
                    .FirstOrDefaultAsync(cancellationToken);

            var version = new PlanVersion
            {
                TenantId = tenantId,
                PlanId = plan.Id,
                VersionNumber = versionNumber,
                Type = PlanVersionType.Working,
                Status = PlanVersionStatus.Active,
                Name = $"Imported Working v{versionNumber}",
                SourceBaselineId = currentBaselineId,
                CreatedBy = userId
            };
            _context.Set<PlanVersion>().Add(version);
            AddImportedModel(document, matches, plan, version, tenantId, userId, importWarnings);
            await _summaryRollup.RecalculateAsync(version.Id, userId, cancellationToken: cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var result = new PlanImportResultDto
            {
                PlanId = plan.Id,
                PlanVersionId = version.Id,
                VersionNumber = version.VersionNumber,
                CreatedPlan = createdPlan,
                Statistics = Statistics(document),
                Warnings = importWarnings
            };

            var auditResult = await _auditService.LogAsync(new AuditEntry
            {
                TenantId = tenantId,
                EntityType = "PlanVersion",
                EntityId = version.Id,
                Action = "ImportMPP",
                UserId = TryParseGuid(userId),
                NewValues = JsonSerializer.Serialize(new
                {
                    FileName = fileName,
                    Format = document.Format,
                    result.Statistics,
                    ReplacedWorkingVersionId = previousWorking?.Id
                }),
                Timestamp = DateTime.UtcNow,
                CreatedBy = userId
            }, cancellationToken);
            if (auditResult.IsFailure)
                throw new InvalidOperationException(auditResult.Error?.Message ?? "The import audit entry could not be saved");

            await transaction.CommitAsync(cancellationToken);
            return Result<PlanImportResultDto>.Success(result);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PlanImportResultDto>.Failure(Error.Conflict("The selected Plan changed during import. Reload and try again."));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PlanImportResultDto>.Failure(Error.Conflict("The Plan import could not be persisted atomically. No imported Plan data was saved."));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Result<PlanningImportDocument>> ParseAsync(Stream file, string fileName, CancellationToken cancellationToken)
    {
        var provider = _providers.FirstOrDefault(candidate => candidate.CanRead(fileName));
        if (provider is null)
        {
            var extensions = string.Join(", ", _providers.SelectMany(candidate => candidate.SupportedExtensions).Distinct(StringComparer.OrdinalIgnoreCase));
            return Result<PlanningImportDocument>.Failure(Error.Validation($"Unsupported plan file type. Supported types: {extensions}"));
        }

        try
        {
            return Result<PlanningImportDocument>.Success(await provider.ParseAsync(file, fileName, cancellationToken));
        }
        catch (PlanningImportParseException exception)
        {
            return Result<PlanningImportDocument>.Failure(Error.Validation(exception.Message));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result<PlanningImportDocument>.Failure(Error.Validation("The uploaded plan could not be parsed"));
        }
    }

    private async Task<Dictionary<string, ResourceDto>> MatchResourcesAsync(
        IReadOnlyCollection<PlanningImportResource> imported,
        CancellationToken cancellationToken)
    {
        if (imported.Count == 0) return new Dictionary<string, ResourceDto>(StringComparer.Ordinal);
        var resources = await _resourceService.GetPagedAsync(new ResourceFilterDto
        {
            Page = 1,
            PageSize = 5000,
            IsActive = true,
            SortBy = "name"
        }, cancellationToken);
        var result = new Dictionary<string, ResourceDto>(StringComparer.Ordinal);
        foreach (var source in imported)
        {
            List<ResourceDto> candidates;
            if (!string.IsNullOrWhiteSpace(source.Email))
            {
                candidates = resources.Items.Where(resource =>
                    string.Equals(resource.Email?.Trim(), source.Email.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                candidates = resources.Items.Where(resource =>
                    string.Equals(resource.Name.Trim(), source.Name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (candidates.Count == 1) result[source.SourceId] = candidates[0];
        }
        return result;
    }

    private static void AddImportedModel(
        PlanningImportDocument document,
        IReadOnlyDictionary<string, ResourceDto> resourceMatches,
        Plan plan,
        PlanVersion version,
        Guid tenantId,
        string userId,
        List<PlanImportNoticeDto> warnings,
        IReadOnlySet<string>? ignoredAssignments = null,
        IReadOnlySet<string>? ignoredCalendars = null)
    {
        var nodeIds = document.Tasks.ToDictionary(task => task.SourceId, _ => Guid.NewGuid(), StringComparer.Ordinal);
        var activityIds = document.Tasks.ToDictionary(task => task.SourceId, _ => Guid.NewGuid(), StringComparer.Ordinal);
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var task in document.Tasks.OrderBy(task => task.SortOrder))
        {
            var code = UniqueCode(task.Code, task.SourceId, usedCodes);
            var nodeId = nodeIds[task.SourceId];
            var activityId = activityIds[task.SourceId];
            var actualStart = task.ActualStart;
            var actualFinish = task.ActualStart.HasValue ? task.ActualFinish : null;
            if (task.ActualFinish.HasValue && !task.ActualStart.HasValue)
                warnings.Add(new PlanImportNoticeDto { Code = "actual-finish-without-start", Message = $"Actual Finish for task '{task.Name}' was ignored because Actual Start was not present.", Count = 1 });
            Guid? parentId = null;
            if (!string.IsNullOrWhiteSpace(task.ParentSourceId))
            {
                if (nodeIds.TryGetValue(task.ParentSourceId, out var mappedParent)) parentId = mappedParent;
                else warnings.Add(new PlanImportNoticeDto { Code = "missing-parent", Message = $"The parent of task '{task.Name}' was missing; the task was imported at the root level.", Count = 1 });
            }

            var node = new PlanWbsNode
            {
                Id = nodeId,
                TenantId = tenantId,
                PlanVersionId = version.Id,
                ParentWbsId = parentId,
                SortOrder = task.SortOrder,
                Code = code,
                Name = Truncate(task.Name, 250),
                ActivityId = activityId,
                TextFormat = task.TextFormat?.Clone(),
                CreatedBy = userId
            };
            var activity = new PlanActivity
            {
                Id = activityId,
                TenantId = tenantId,
                PlanVersionId = version.Id,
                Code = code,
                Name = Truncate(task.Name, 250),
                Description = task.Description,
                Type = task.IsMilestone ? PlanActivityType.Milestone : PlanActivityType.Task,
                SchedulingMode = task.SchedulingMode,
                ConstraintType = task.ConstraintType,
                ConstraintDate = task.ConstraintDate,
                Deadline = task.Deadline,
                StartDate = task.Start,
                EndDate = task.Finish,
                ActualStart = actualStart,
                ActualFinish = actualFinish,
                Duration = task.IsMilestone ? 0 : Math.Max(0, task.DurationDays),
                SortOrder = task.SortOrder,
                Progress = task.PercentComplete,
                WbsId = nodeId,
                TextFormat = task.TextFormat?.Clone(),
                CreatedBy = userId
            };
            version.WbsNodes.Add(node);
            version.Activities.Add(activity);
        }

        foreach (var dependency in document.Dependencies)
        {
            if (!activityIds.TryGetValue(dependency.SuccessorSourceId, out var successorId)
                || !activityIds.TryGetValue(dependency.PredecessorSourceId, out var predecessorId)) continue;
            version.Dependencies.Add(new PlanDependency
            {
                TenantId = tenantId,
                PlanVersionId = version.Id,
                ActivityId = successorId,
                PredecessorActivityId = predecessorId,
                Type = dependency.Type,
                Lag = dependency.LagDays,
                CreatedBy = userId
            });
        }

        foreach (var assignment in document.Assignments)
        {
            if (ignoredAssignments?.Contains(AssignmentSourceId(assignment)) == true) continue;
            if (!activityIds.TryGetValue(assignment.TaskSourceId, out var activityId)
                || !resourceMatches.TryGetValue(assignment.ResourceSourceId, out var resource)) continue;
            version.Assignments.Add(new PlanAssignment
            {
                TenantId = tenantId,
                PlanVersionId = version.Id,
                ActivityId = activityId,
                ResourceId = resource.Id,
                AllocationPct = assignment.AllocationPercent,
                HoursPerWeek = assignment.HoursPerWeek,
                PlannedHours = assignment.PlannedHours,
                StartDate = assignment.Start,
                EndDate = assignment.Finish,
                CreatedBy = userId
            });
        }

        var calendarNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in document.Calendars)
        {
            if (ignoredCalendars?.Contains(CalendarSourceId(source)) == true) continue;
            var name = UniqueName(source.Name, calendarNames);
            var calendar = new PlanCalendar
            {
                TenantId = tenantId,
                ProjectId = plan.ProjectId,
                PlanVersionId = version.Id,
                Name = name,
                Description = source.IsDefault ? "Default calendar imported from Microsoft Project" : "Imported from Microsoft Project",
                IsDefault = source.IsDefault,
                CreatedBy = userId
            };
            foreach (var day in source.Days)
            {
                calendar.DayRules.Add(new CalendarDayRule
                {
                    TenantId = tenantId,
                    PlanningCalendarId = calendar.Id,
                    DayOfWeek = day.DayOfWeek,
                    IsWorkingDay = day.IsWorkingDay,
                    StartTime = day.StartTime,
                    EndTime = day.EndTime,
                    BreakStartTime = day.BreakStartTime,
                    BreakEndTime = day.BreakEndTime,
                    CreatedBy = userId
                });
            }
            foreach (var holiday in source.Holidays.GroupBy(item => item.Date.Date).Select(group => group.First()))
            {
                calendar.Holidays.Add(new CalendarHoliday
                {
                    TenantId = tenantId,
                    PlanningCalendarId = calendar.Id,
                    Name = Truncate(holiday.Name, 250),
                    Date = holiday.Date.Date,
                    IsRecurring = false,
                    CreatedBy = userId
                });
            }
            version.Calendars.Add(calendar);
        }
    }

    private static PlanImportPreviewDto BuildPreview(
        PlanningImportDocument document,
        string fileName,
        IReadOnlyDictionary<string, ResourceDto> resourceMatches)
    {
        var dependenciesByTask = document.Dependencies.GroupBy(item => item.SuccessorSourceId)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var resourceNames = document.Resources.ToDictionary(item => item.SourceId, item => item.Name, StringComparer.Ordinal);
        var assignmentsByTask = document.Assignments.GroupBy(item => item.TaskSourceId)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var warnings = document.Warnings.Select(ToDto).ToList();
        var unmatched = document.Resources.Count(resource => !resourceMatches.ContainsKey(resource.SourceId));
        if (unmatched > 0)
            warnings.Add(new PlanImportNoticeDto { Code = "unmatched-resources", Message = "Resources without one unique active NEXUS match by email or name will be shown but their assignments will not be committed.", Count = unmatched });

        return new PlanImportPreviewDto
        {
            FileName = fileName,
            Format = document.Format,
            SuggestedPlanName = FirstNonBlank(document.ProjectName, Path.GetFileNameWithoutExtension(fileName)) ?? "Imported Plan",
            Statistics = Statistics(document),
            Tasks = document.Tasks.OrderBy(task => task.SortOrder).Select(task => new PlanImportTaskPreviewDto
            {
                SourceId = task.SourceId,
                ParentSourceId = task.ParentSourceId,
                Code = task.Code,
                Name = task.Name,
                OutlineLevel = task.OutlineLevel,
                SortOrder = task.SortOrder,
                IsMilestone = task.IsMilestone,
                SchedulingMode = task.SchedulingMode,
                ConstraintType = task.ConstraintType,
                ConstraintDate = task.ConstraintDate,
                Deadline = task.Deadline,
                DurationDays = task.DurationDays,
                Start = task.Start,
                Finish = task.Finish,
                ActualStart = task.ActualStart,
                ActualFinish = task.ActualFinish,
                PercentComplete = task.PercentComplete,
                Predecessors = dependenciesByTask.GetValueOrDefault(task.SourceId)?.Select(FormatPredecessor).ToList() ?? [],
                Resources = assignmentsByTask.GetValueOrDefault(task.SourceId)?.Select(assignment => resourceNames.GetValueOrDefault(assignment.ResourceSourceId, assignment.ResourceSourceId)).Distinct().ToList() ?? []
            }).ToList(),
            Dependencies = document.Dependencies.Select(item => new PlanImportDependencyPreviewDto
            {
                SuccessorSourceId = item.SuccessorSourceId,
                PredecessorSourceId = item.PredecessorSourceId,
                Type = item.Type,
                LagDays = item.LagDays
            }).ToList(),
            Resources = document.Resources.Select(resource => new PlanImportResourcePreviewDto
            {
                SourceId = resource.SourceId,
                Name = resource.Name,
                Email = resource.Email,
                NexusResourceId = resourceMatches.GetValueOrDefault(resource.SourceId)?.Id,
                MatchStatus = resourceMatches.ContainsKey(resource.SourceId) ? "Matched" : "Unmatched",
                AssignmentCount = document.Assignments.Count(assignment => assignment.ResourceSourceId == resource.SourceId)
            }).ToList(),
            Calendars = document.Calendars.Select(calendar => new PlanImportCalendarPreviewDto
            {
                Name = calendar.Name,
                IsDefault = calendar.IsDefault,
                WorkingDays = calendar.Days.Count(day => day.IsWorkingDay),
                Holidays = calendar.Holidays.Count
            }).ToList(),
            Warnings = warnings,
            UnsupportedFields = document.UnsupportedFields.Select(ToDto).ToList()
        };
    }

    private static PlanImportStatisticsDto Statistics(PlanningImportDocument document) => new()
    {
        Tasks = document.Tasks.Count(task => !task.IsMilestone),
        Milestones = document.Tasks.Count(task => task.IsMilestone),
        WbsLevels = document.Tasks.Count == 0 ? 0 : document.Tasks.Max(task => task.OutlineLevel),
        Dependencies = document.Dependencies.Count,
        Resources = document.Resources.Count,
        Assignments = document.Assignments.Count,
        Calendars = document.Calendars.Count,
        ActualDates = document.Tasks.Sum(task => (task.ActualStart is null ? 0 : 1) + (task.ActualFinish is null ? 0 : 1))
    };

    private static string FormatPredecessor(PlanningImportDependency dependency)
    {
        var type = dependency.Type switch
        {
            PlanDependencyType.StartToStart => "SS",
            PlanDependencyType.FinishToFinish => "FF",
            PlanDependencyType.StartToFinish => "SF",
            _ => "FS"
        };
        var lag = dependency.LagDays is null or 0 ? string.Empty : $"{(dependency.LagDays > 0 ? "+" : string.Empty)}{dependency.LagDays}d";
        return $"{dependency.PredecessorSourceId}{type}{lag}";
    }

    private static string UniqueCode(string sourceCode, string sourceId, ISet<string> used)
    {
        var root = Truncate(FirstNonBlank(sourceCode, sourceId) ?? sourceId, 50);
        if (used.Add(root)) return root;
        var suffix = $"-{sourceId}";
        var candidate = Truncate(root, Math.Max(1, 50 - suffix.Length)) + suffix;
        var counter = 2;
        while (!used.Add(candidate))
        {
            suffix = $"-{sourceId}-{counter++}";
            candidate = Truncate(root, Math.Max(1, 50 - suffix.Length)) + suffix;
        }
        return candidate;
    }

    private static string UniqueName(string sourceName, ISet<string> used)
    {
        var root = Truncate(FirstNonBlank(sourceName, "Imported calendar")!, 250);
        if (used.Add(root)) return root;
        var counter = 2;
        string candidate;
        do
        {
            var suffix = $" ({counter++})";
            candidate = Truncate(root, 250 - suffix.Length) + suffix;
        } while (!used.Add(candidate));
        return candidate;
    }

    private Guid GetCurrentTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        return Guid.TryParse(tenantId, out var parsed) ? parsed : TenantConstants.DemoTenantId;
    }

    private static PlanImportNoticeDto ToDto(PlanningImportNotice notice) => new() { Code = notice.Code, Message = notice.Message, Count = notice.Count };
    private static string? FirstNonBlank(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    private static string Truncate(string value, int maximumLength) => value.Length <= maximumLength ? value : value[..maximumLength];
    private static Guid? TryParseGuid(string value) => GuidHelper.TryParse(value);
}
