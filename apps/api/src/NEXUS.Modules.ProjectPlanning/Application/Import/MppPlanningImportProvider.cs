using NEXUS.Modules.ProjectPlanning.Domain;
using System.Text;
using net.sf.mpxj;
using net.sf.mpxj.mpp;
using MpxjResource = net.sf.mpxj.Resource;
using MpxjTask = net.sf.mpxj.Task;

namespace NEXUS.Modules.ProjectPlanning.Application.Import;

/// <summary>
/// Native Microsoft Project provider. MPXJ remains entirely behind the
/// format-neutral <see cref="IPlanningImportProvider"/> contract.
/// </summary>
public sealed class MppPlanningImportProvider : IPlanningImportProvider
{
    private static readonly string[] Extensions = [".mpp"];

    static MppPlanningImportProvider()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public string Format => "Microsoft Project (MPP)";
    public IReadOnlyCollection<string> SupportedExtensions => Extensions;

    public bool CanRead(string fileName) =>
        string.Equals(Path.GetExtension(fileName), ".mpp", StringComparison.OrdinalIgnoreCase);

    public async Task<PlanningImportDocument> ParseAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!CanRead(fileName))
            throw new PlanningImportParseException("Only Microsoft Project .mpp files are supported");
        if (!stream.CanRead)
            throw new PlanningImportParseException("The uploaded file cannot be read");

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"nexus-mpp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var tempFile = Path.Combine(tempDirectory, "schedule.mpp");

        try
        {
            await using (var output = new FileStream(
                             tempFile,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await stream.CopyToAsync(output, cancellationToken);
            }

            if (new FileInfo(tempFile).Length == 0)
                throw new PlanningImportParseException("The uploaded MPP file is empty");

            ProjectFile project;
            try
            {
                // Use the format-specific reader deliberately: an uploaded file
                // cannot select another parser merely by disguising its extension.
                project = new MPPReader().Read(new java.io.File(tempFile));
            }
            catch (Exception exception)
            {
                throw new PlanningImportParseException(
                    "The file could not be read as a Microsoft Project MPP file. It may be corrupt, encrypted, or use an unsupported MPP version.",
                    exception);
            }

            return Map(project, cancellationToken);
        }
        finally
        {
            TryDelete(tempFile);
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static PlanningImportDocument Map(ProjectFile project, CancellationToken cancellationToken)
    {
        var document = new PlanningImportDocument
        {
            Format = "mpp",
            ProjectName = FirstNonBlank(project.ProjectProperties.ProjectTitle, project.ProjectProperties.Name)
        };

        var tasksBySourceId = new Dictionary<string, MpxjTask>(StringComparer.Ordinal);
        var sortOrder = 0;
        var fractionalDurations = 0;
        var actualDurationFields = 0;
        var baselineFields = 0;
        var constraintFields = 0;
        var taskCalendarLinks = 0;
        var truncatedNotes = 0;

        foreach (MpxjTask task in project.Tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = task.ID?.intValue() ?? 0;
            var name = task.Name?.Trim();
            if (id == 0 || string.IsNullOrWhiteSpace(name)) continue;

            var sourceId = SourceId(task.UniqueID, task.ID, sortOrder + 1);
            if (!tasksBySourceId.TryAdd(sourceId, task))
            {
                document.Warnings.Add(Notice("duplicate-task-id", $"Task '{name}' has a duplicate Microsoft Project unique ID and was skipped.", 1));
                continue;
            }

            var durationValue = ConvertDuration(task.Duration, TimeUnit.valueOf("DAYS"), project.ProjectProperties);
            var durationDays = ToWholeDays(durationValue, out var wasFractional);
            if (wasFractional) fractionalDurations++;

            var description = task.Notes?.Trim();
            if (description?.Length > 2000)
            {
                description = description[..2000];
                truncatedNotes++;
            }

            var parent = task.ParentTask;
            var parentSourceId = parent is null || (parent.ID?.intValue() ?? 0) == 0
                ? null
                : SourceId(parent.UniqueID, parent.ID, 0);
            var milestone = task.Milestone || (!task.Summary && durationDays == 0);
            var actualStart = ToDateTime(task.ActualStart);
            var actualFinish = ToDateTime(task.ActualFinish);
            var constraintType = MapConstraintType(task.ConstraintType);
            var schedulingMode = ReadManualSchedulingMode(task) ? PlanSchedulingMode.Manual : PlanSchedulingMode.Automatic;

            document.Tasks.Add(new PlanningImportTask
            {
                SourceId = sourceId,
                ParentSourceId = parentSourceId,
                Code = FirstNonBlank(task.WBS, task.ID?.toString(), sourceId)!,
                Name = name,
                Description = description,
                OutlineLevel = Math.Max(1, task.OutlineLevel?.intValue() ?? 1),
                SortOrder = sortOrder++,
                IsMilestone = milestone,
                SchedulingMode = schedulingMode,
                ConstraintType = constraintType,
                ConstraintDate = ToDateTime(task.ConstraintDate),
                Deadline = ToDateTime(task.Deadline),
                DurationDays = milestone ? 0 : Math.Max(0, durationDays),
                Start = ToDateTime(task.Start),
                Finish = ToDateTime(task.Finish),
                ActualStart = actualStart,
                ActualFinish = actualFinish,
                PercentComplete = ToDecimal(task.PercentageComplete, 0, 100),
                TextFormat = null
            });

            if (task.ActualDuration is not null) actualDurationFields++;
            if (task.BaselineStart is not null || task.BaselineFinish is not null || task.BaselineDuration is not null) baselineFields++;
            if (task.ConstraintDate is not null || task.Deadline is not null || !IsAsSoonAsPossible(task.ConstraintType)) constraintFields++;
            if (task.CalendarUniqueID is not null) taskCalendarLinks++;
        }

        MapResourcesAndAssignments(project, document, tasksBySourceId);
        MapDependencies(project, document, tasksBySourceId);
        MapCalendars(project, document);

        if (fractionalDurations > 0)
            document.Warnings.Add(Notice("fractional-duration-rounded", "Fractional task durations were rounded to whole Planning days.", fractionalDurations));
        if (truncatedNotes > 0)
            document.Warnings.Add(Notice("task-notes-truncated", "Task notes longer than 2,000 characters were truncated to the Planning description limit.", truncatedNotes));
        if (actualDurationFields > 0)
            document.UnsupportedFields.Add(Notice("actual-duration", "Microsoft Project actual duration is not imported; Actual Start and Actual Finish are preserved as execution history.", actualDurationFields));
        if (baselineFields > 0)
            document.UnsupportedFields.Add(Notice("baseline-fields", "Microsoft Project baseline fields are not imported; NEXUS Baselines are governed Plan Versions.", baselineFields));
        if (constraintFields > 0)
            document.Warnings.Add(Notice("constraints-imported", "Task constraints and deadlines were mapped to Planning constraint fields.", constraintFields));
        if (taskCalendarLinks > 0)
            document.UnsupportedFields.Add(Notice("task-calendar-links", "Calendar definitions are imported when safe, but task-to-calendar links are not represented by PlanActivity.", taskCalendarLinks));

        document.UnsupportedFields.Add(Notice("task-text-formatting", "View-specific Microsoft Project task font and cell formatting is not imported because it cannot be mapped reliably to Planning text tokens.", 0));
        document.UnsupportedFields.Add(Notice("scheduling-fields", "Critical path, slack, early/late dates, leveling, costs, earned value, custom fields, recurring tasks, and timephased data are not imported.", 0));
        return document;
    }

    private static void MapDependencies(
        ProjectFile project,
        PlanningImportDocument document,
        IReadOnlyDictionary<string, MpxjTask> tasksBySourceId)
    {
        var known = tasksBySourceId.Keys.ToHashSet(StringComparer.Ordinal);
        var fractionalLags = 0;
        var skipped = 0;
        var edges = new HashSet<string>(StringComparer.Ordinal);

        foreach (MpxjTask task in project.Tasks)
        {
            var successorId = SourceId(task.UniqueID, task.ID, 0);
            if (!known.Contains(successorId)) continue;

            foreach (var relation in EnumerateJavaList<Relation>(task.Predecessors))
            {
                var predecessor = relation.PredecessorTask;
                var predecessorId = SourceId(predecessor?.UniqueID, predecessor?.ID, 0);
                if (!known.Contains(predecessorId) || predecessorId == successorId)
                {
                    skipped++;
                    continue;
                }

                var type = MapRelationType(relation.Type);
                var lagValue = ConvertDuration(relation.Lag, TimeUnit.valueOf("DAYS"), project.ProjectProperties);
                var lagDays = ToWholeDays(lagValue, out var fractional);
                if (fractional) fractionalLags++;
                var edgeKey = $"{successorId}|{predecessorId}";
                if (!edges.Add(edgeKey))
                {
                    skipped++;
                    continue;
                }

                document.Dependencies.Add(new PlanningImportDependency
                {
                    SuccessorSourceId = successorId,
                    PredecessorSourceId = predecessorId,
                    Type = type,
                    LagDays = lagDays == 0 ? null : lagDays
                });
            }
        }

        if (fractionalLags > 0)
            document.Warnings.Add(Notice("fractional-lag-rounded", "Dependency lag or lead values were rounded to whole Planning days.", fractionalLags));
        if (skipped > 0)
            document.Warnings.Add(Notice("dependency-skipped", "Invalid, duplicate, or project-summary dependency edges were skipped.", skipped));
    }

    private static void MapResourcesAndAssignments(
        ProjectFile project,
        PlanningImportDocument document,
        IReadOnlyDictionary<string, MpxjTask> tasksBySourceId)
    {
        var resources = new Dictionary<string, PlanningImportResource>(StringComparer.Ordinal);
        foreach (MpxjResource resource in project.Resources)
        {
            var sourceId = SourceId(resource.UniqueID, resource.ID, 0);
            var name = resource.Name?.Trim();
            if (sourceId == "0" || string.IsNullOrWhiteSpace(name)) continue;
            resources[sourceId] = new PlanningImportResource
            {
                SourceId = sourceId,
                Name = name,
                Email = NullIfWhiteSpace(resource.EmailAddress)
            };
        }
        document.Resources.AddRange(resources.Values);

        var knownTasks = tasksBySourceId.Keys.ToHashSet(StringComparer.Ordinal);
        var duplicateAssignments = new HashSet<string>(StringComparer.Ordinal);
        var skippedAssignments = 0;
        var workValues = 0;
        foreach (MpxjTask task in project.Tasks)
        {
            var taskSourceId = SourceId(task.UniqueID, task.ID, 0);
            if (!knownTasks.Contains(taskSourceId)) continue;
            foreach (var assignment in EnumerateJavaList<ResourceAssignment>(task.ResourceAssignments))
            {
                var resource = assignment.Resource;
                var resourceSourceId = SourceId(resource?.UniqueID, assignment.ResourceUniqueID, 0);
                var key = $"{taskSourceId}|{resourceSourceId}";
                if (!resources.ContainsKey(resourceSourceId) || !duplicateAssignments.Add(key))
                {
                    skippedAssignments++;
                    continue;
                }

                if (assignment.Work is not null) workValues++;
                document.Assignments.Add(new PlanningImportAssignment
                {
                    TaskSourceId = taskSourceId,
                    ResourceSourceId = resourceSourceId,
                    AllocationPercent = ToDecimal(assignment.Units, 0, 100),
                    HoursPerWeek = null,
                    Start = ToDateTime(assignment.Start),
                    Finish = ToDateTime(assignment.Finish)
                });
            }
        }

        if (skippedAssignments > 0)
            document.Warnings.Add(Notice("assignment-skipped", "Assignments without a usable task/resource or duplicate assignments were skipped.", skippedAssignments));
        if (workValues > 0)
            document.UnsupportedFields.Add(Notice("assignment-work", "Microsoft Project assignment work is a total/timephased value and is not guessed as Planning hours per week.", workValues));
    }

    private static void MapCalendars(ProjectFile project, PlanningImportDocument document)
    {
        var unsupportedCalendarFeatures = 0;
        foreach (ProjectCalendar calendar in project.Calendars)
        {
            if (calendar.Parent is not null)
            {
                unsupportedCalendarFeatures++;
                continue;
            }

            var dayTypes = calendar.CalendarDayTypes;
            var hours = calendar.CalendarHours;
            var days = new List<PlanningImportCalendarDay>();
            var safe = dayTypes is { Length: 7 } && hours is { Length: 7 };
            if (!safe)
            {
                unsupportedCalendarFeatures++;
                continue;
            }

            for (var index = 0; index < 7; index++)
            {
                var dayHours = hours[index];
                var rangeCount = dayHours?.size() ?? 0;
                var isWorking = IsWorkingDay(dayTypes[index]) || rangeCount > 0;
                if (rangeCount > 2)
                {
                    safe = false;
                    break;
                }

                LocalTimeRange? first = rangeCount > 0 ? dayHours!.get(0) : null;
                LocalTimeRange? last = rangeCount > 0 ? dayHours!.get(rangeCount - 1) : null;
                days.Add(new PlanningImportCalendarDay
                {
                    DayOfWeek = (DayOfWeek)index,
                    IsWorkingDay = isWorking,
                    StartTime = ToTimeSpan(first?.Start),
                    EndTime = ToTimeSpan(last?.End),
                    BreakStartTime = rangeCount == 2 ? ToTimeSpan(first?.End) : null,
                    BreakEndTime = rangeCount == 2 ? ToTimeSpan(last?.Start) : null
                });
            }

            if (!safe)
            {
                unsupportedCalendarFeatures++;
                continue;
            }

            var imported = new PlanningImportCalendar
            {
                Name = FirstNonBlank(calendar.Name, "Imported calendar")!,
                IsDefault = ReferenceEquals(project.DefaultCalendar, calendar),
                Days = days
            };

            foreach (var exception in EnumerateJavaList<ProjectCalendarException>(calendar.CalendarExceptions))
            {
                var from = ToDateTime(exception.FromDate);
                var to = ToDateTime(exception.ToDate);
                if (exception.Working || from is null || to is null || to < from || (to.Value.Date - from.Value.Date).TotalDays > 366)
                {
                    unsupportedCalendarFeatures++;
                    continue;
                }

                for (var date = from.Value.Date; date <= to.Value.Date; date = date.AddDays(1))
                {
                    imported.Holidays.Add(new PlanningImportHoliday
                    {
                        Name = FirstNonBlank(exception.Name, "Microsoft Project calendar exception")!,
                        Date = date
                    });
                }
            }

            if (calendar.WorkWeeks.size() > 0) unsupportedCalendarFeatures += calendar.WorkWeeks.size();
            document.Calendars.Add(imported);
        }

        if (unsupportedCalendarFeatures > 0)
            document.UnsupportedFields.Add(Notice("calendar-features", "Derived calendars, work weeks, working exceptions, or calendars with more than two daily work periods were not imported.", unsupportedCalendarFeatures));
    }

    private static PlanDependencyType MapRelationType(RelationType? type)
    {
        var value = type?.toString()?.Replace("-", "_", StringComparison.Ordinal).Replace(" ", "_", StringComparison.Ordinal).ToUpperInvariant();
        return value switch
        {
            "START_START" or "SS" => PlanDependencyType.StartToStart,
            "FINISH_FINISH" or "FF" => PlanDependencyType.FinishToFinish,
            "START_FINISH" or "SF" => PlanDependencyType.StartToFinish,
            _ => PlanDependencyType.FinishToStart
        };
    }

    private static bool IsWorkingDay(DayType? type) =>
        string.Equals(type?.toString(), "WORKING", StringComparison.OrdinalIgnoreCase);

    private static bool IsAsSoonAsPossible(ConstraintType? type)
    {
        if (type is null) return true;
        var value = type.toString();
        return string.Equals(value, "AS_SOON_AS_POSSIBLE", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "ASAP", StringComparison.OrdinalIgnoreCase);
    }

    private static PlanConstraintType MapConstraintType(ConstraintType? type)
    {
        var value = type?.toString()?.Replace("-", "_", StringComparison.Ordinal).Replace(" ", "_", StringComparison.Ordinal).ToUpperInvariant();
        return value switch
        {
            "AS_LATE_AS_POSSIBLE" or "ALAP" => PlanConstraintType.AsLateAsPossible,
            "MUST_START_ON" or "MSO" => PlanConstraintType.MustStartOn,
            "MUST_FINISH_ON" or "MFO" => PlanConstraintType.MustFinishOn,
            "START_NO_EARLIER_THAN" or "SNET" => PlanConstraintType.StartNoEarlierThan,
            "START_NO_LATER_THAN" or "SNLT" => PlanConstraintType.StartNoLaterThan,
            "FINISH_NO_EARLIER_THAN" or "FNET" => PlanConstraintType.FinishNoEarlierThan,
            "FINISH_NO_LATER_THAN" or "FNLT" => PlanConstraintType.FinishNoLaterThan,
            _ => PlanConstraintType.AsSoonAsPossible
        };
    }

    private static bool ReadManualSchedulingMode(MpxjTask task)
    {
        foreach (var name in new[] { "Manual", "IsManual" })
        {
            var property = task.GetType().GetProperty(name);
            if (property?.GetValue(task) is bool value) return value;
            var method = task.GetType().GetMethod(name, Type.EmptyTypes);
            if (method?.Invoke(task, null) is bool methodValue) return methodValue;
        }
        return false;
    }

    private static double? ConvertDuration(Duration? duration, TimeUnit target, TimeUnitDefaultsContainer defaults)
    {
        if (duration is null) return null;
        try { return duration.ConvertUnits(target, defaults).Duration; }
        catch { return duration.Duration; }
    }

    private static int ToWholeDays(double? value, out bool fractional)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            fractional = false;
            return 0;
        }
        fractional = Math.Abs(value.Value - Math.Round(value.Value)) > 0.0001;
        return checked((int)Math.Round(value.Value, MidpointRounding.AwayFromZero));
    }

    private static decimal? ToDecimal(java.lang.Number? number, decimal min, decimal max)
    {
        if (number is null) return null;
        var value = (decimal)number.doubleValue();
        return Math.Clamp(value, min, max);
    }

    private static DateTime? ToDateTime(java.time.LocalDateTime? value) => value is null
        ? null
        : new DateTime(value.getYear(), value.getMonthValue(), value.getDayOfMonth(), value.getHour(), value.getMinute(), value.getSecond(), DateTimeKind.Unspecified);

    private static DateTime? ToDateTime(java.time.LocalDate? value) => value is null
        ? null
        : new DateTime(value.getYear(), value.getMonthValue(), value.getDayOfMonth(), 0, 0, 0, DateTimeKind.Unspecified);

    private static TimeSpan? ToTimeSpan(java.time.LocalTime? value) => value is null
        ? null
        : new TimeSpan(value.getHour(), value.getMinute(), value.getSecond());

    private static string SourceId(java.lang.Integer? uniqueId, java.lang.Integer? id, int fallback) =>
        (uniqueId ?? id)?.toString() ?? fallback.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string? FirstNonBlank(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static PlanningImportNotice Notice(string code, string message, int count) => new() { Code = code, Message = message, Count = count };

    private static IEnumerable<T> EnumerateJavaList<T>(java.util.List? values)
    {
        if (values is null) yield break;
        for (var index = 0; index < values.size(); index++)
        {
            if (values.get(index) is T value) yield return value;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* Best-effort cleanup; never hide the parse result/error. */ }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, false); }
        catch { /* Best-effort cleanup; never hide the parse result/error. */ }
    }
}

public sealed class PlanningImportParseException : Exception
{
    public PlanningImportParseException(string message) : base(message) { }
    public PlanningImportParseException(string message, Exception innerException) : base(message, innerException) { }
}
