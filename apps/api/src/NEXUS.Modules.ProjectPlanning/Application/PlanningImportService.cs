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
            if (await _context.Set<Plan>().AnyAsync(
                    candidate => candidate.ProjectId == request.ProjectId && candidate.PlanType == request.NewPlanType,
                    cancellationToken))
            {
                return Result<PlanImportResultDto>.Failure(Error.Conflict("A Plan of this type already exists for the project. Import into that Plan or choose another type."));
            }

            plan = new Plan
            {
                TenantId = tenantId,
                ProjectId = request.ProjectId,
                PlanType = request.NewPlanType!.Value,
                Name = Truncate(planName, 250),
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
        List<PlanImportNoticeDto> warnings)
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
                StartDate = assignment.Start,
                EndDate = assignment.Finish,
                CreatedBy = userId
            });
        }

        var calendarNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in document.Calendars)
        {
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
