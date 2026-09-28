using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public sealed class PlanVersionComparisonService : IPlanVersionComparisonService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DbContext _context;

    public PlanVersionComparisonService(DbContext context) => _context = context;

    public async Task<Result<PlanVersionComparisonDto>> CompareAsync(
        Guid planId,
        Guid fromVersionId,
        Guid toVersionId,
        CancellationToken cancellationToken = default)
    {
        var versions = await VersionModelQuery()
            .Where(v => v.Id == fromVersionId || v.Id == toVersionId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var from = versions.SingleOrDefault(v => v.Id == fromVersionId);
        var to = versions.SingleOrDefault(v => v.Id == toVersionId);
        if (from is null || to is null)
            return Result<PlanVersionComparisonDto>.Failure(Error.NotFound("One or both plan versions were not found"));

        if (from.PlanId != planId || to.PlanId != planId)
            return Result<PlanVersionComparisonDto>.Failure(Error.Validation("Both versions must belong to the requested plan"));
        if (from.PlanId != to.PlanId)
            return Result<PlanVersionComparisonDto>.Failure(Error.Validation("Both versions must belong to the same plan"));

        var fromActivityCodes = from.Activities.ToDictionary(a => a.Id, a => a.Code);
        var toActivityCodes = to.Activities.ToDictionary(a => a.Id, a => a.Code);
        var fromWbsKeys = BuildWbsKeys(from.WbsNodes);
        var toWbsKeys = BuildWbsKeys(to.WbsNodes);

        var wbs = Compare(
            from.WbsNodes,
            to.WbsNodes,
            n => fromWbsKeys[n.Id],
            n => toWbsKeys[n.Id],
            n => WbsSnapshot(n, fromWbsKeys, fromActivityCodes, from.Deliverables),
            n => WbsSnapshot(n, toWbsKeys, toActivityCodes, to.Deliverables));
        var activities = Compare(
            from.Activities,
            to.Activities,
            a => LogicalKey("activity", a.Code),
            a => LogicalKey("activity", a.Code),
            a => ActivitySnapshot(a, fromWbsKeys),
            a => ActivitySnapshot(a, toWbsKeys));
        var deliverables = Compare(
            from.Deliverables,
            to.Deliverables,
            d => LogicalKey("deliverable", d.Name),
            d => LogicalKey("deliverable", d.Name),
            d => DeliverableSnapshot(d, fromWbsKeys, fromActivityCodes),
            d => DeliverableSnapshot(d, toWbsKeys, toActivityCodes));
        var dependencies = Compare(
            from.Dependencies,
            to.Dependencies,
            d => LogicalKey("dependency", DependencyKey(d, fromActivityCodes)),
            d => LogicalKey("dependency", DependencyKey(d, toActivityCodes)),
            d => DependencySnapshot(d, fromActivityCodes),
            d => DependencySnapshot(d, toActivityCodes));
        var assignments = Compare(
            from.Assignments,
            to.Assignments,
            a => LogicalKey("assignment", $"{ActivityCode(a.ActivityId, fromActivityCodes)}|{a.ResourceId:D}"),
            a => LogicalKey("assignment", $"{ActivityCode(a.ActivityId, toActivityCodes)}|{a.ResourceId:D}"),
            a => AssignmentSnapshot(a, fromActivityCodes),
            a => AssignmentSnapshot(a, toActivityCodes));
        var calendars = Compare(
            from.Calendars,
            to.Calendars,
            c => LogicalKey("calendar", c.Name),
            c => LogicalKey("calendar", c.Name),
            CalendarSnapshot,
            CalendarSnapshot);

        var differences = new Dictionary<string, IReadOnlyList<VersionEntityDifferenceDto>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Wbs"] = wbs,
            ["Activities"] = activities,
            ["Dependencies"] = dependencies,
            ["Deliverables"] = deliverables,
            ["Assignments"] = assignments,
            ["Calendars"] = calendars
        };

        return Result<PlanVersionComparisonDto>.Success(new PlanVersionComparisonDto
        {
            PlanId = planId,
            FromVersionId = from.Id,
            FromVersionNumber = from.VersionNumber,
            FromVersionType = from.Type.ToString(),
            FromVersionStatus = from.Status.ToString(),
            ToVersionId = to.Id,
            ToVersionNumber = to.VersionNumber,
            ToVersionType = to.Type.ToString(),
            ToVersionStatus = to.Status.ToString(),
            Wbs = wbs,
            Activities = activities,
            Dependencies = dependencies,
            Deliverables = deliverables,
            Assignments = assignments,
            Calendars = calendars,
            Summary = differences.ToDictionary(pair => pair.Key, pair => Summarize(pair.Value), StringComparer.OrdinalIgnoreCase)
        });
    }

    private IQueryable<PlanVersion> VersionModelQuery() =>
        _context.Set<PlanVersion>()
            .Include(v => v.WbsNodes)
            .Include(v => v.Activities)
            .Include(v => v.Deliverables)
            .Include(v => v.Dependencies)
            .Include(v => v.Assignments)
            .Include(v => v.Calendars).ThenInclude(c => c.DayRules)
            .Include(v => v.Calendars).ThenInclude(c => c.Holidays);

    private static IReadOnlyList<VersionEntityDifferenceDto> Compare<T>(
        IEnumerable<T> from,
        IEnumerable<T> to,
        Func<T, string> fromKey,
        Func<T, string> toKey,
        Func<T, IReadOnlyDictionary<string, object?>> fromSnapshot,
        Func<T, IReadOnlyDictionary<string, object?>> toSnapshot)
    {
        var before = from.ToDictionary(fromKey, value => (value, fromSnapshot(value)), StringComparer.Ordinal);
        var after = to.ToDictionary(toKey, value => (value, toSnapshot(value)), StringComparer.Ordinal);
        var results = new List<VersionEntityDifferenceDto>();

        foreach (var logicalKey in before.Keys.Union(after.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            var hasBefore = before.TryGetValue(logicalKey, out var beforeValue);
            var hasAfter = after.TryGetValue(logicalKey, out var afterValue);
            if (!hasBefore)
            {
                results.Add(new VersionEntityDifferenceDto { Key = logicalKey, ChangeType = "Added", After = afterValue.Item2 });
                continue;
            }
            if (!hasAfter)
            {
                results.Add(new VersionEntityDifferenceDto { Key = logicalKey, ChangeType = "Removed", Before = beforeValue.Item2 });
                continue;
            }

            var fields = beforeValue.Item2.Keys.Union(afterValue.Item2.Keys, StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal)
                .Where(field => !ValuesEqual(beforeValue.Item2.GetValueOrDefault(field), afterValue.Item2.GetValueOrDefault(field)))
                .ToDictionary(
                    field => field,
                    field => new ComparisonFieldChangeDto
                    {
                        Before = beforeValue.Item2.GetValueOrDefault(field),
                        After = afterValue.Item2.GetValueOrDefault(field)
                    },
                    StringComparer.Ordinal);

            results.Add(new VersionEntityDifferenceDto
            {
                Key = logicalKey,
                ChangeType = fields.Count == 0 ? "Unchanged" : "Modified",
                Before = beforeValue.Item2,
                After = afterValue.Item2,
                Fields = fields
            });
        }

        return results;
    }

    private static VersionComparisonSummaryDto Summarize(IEnumerable<VersionEntityDifferenceDto> differences)
    {
        var list = differences.ToList();
        return new VersionComparisonSummaryDto
        {
            Total = list.Count,
            Added = list.Count(d => d.ChangeType == "Added"),
            Removed = list.Count(d => d.ChangeType == "Removed"),
            Modified = list.Count(d => d.ChangeType == "Modified"),
            Unchanged = list.Count(d => d.ChangeType == "Unchanged")
        };
    }

    private static Dictionary<Guid, string> BuildWbsKeys(IEnumerable<PlanWbsNode> nodes)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var group in nodes.GroupBy(n => n.Code, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(n => n.Name, StringComparer.Ordinal).ThenBy(n => n.SortOrder).ThenBy(n => n.Id).ToList();
            for (var index = 0; index < ordered.Count; index++)
                result[ordered[index].Id] = ordered.Count == 1 ? group.Key : $"{group.Key}#{index + 1}";
        }
        return result;
    }

    private static string LogicalKey(string entity, string value) => $"{entity}:{value.Trim()}";
    private static string ActivityCode(Guid id, IReadOnlyDictionary<Guid, string> codes) => codes.TryGetValue(id, out var code) ? code : $"missing:{id:D}";
    private static string DependencyKey(PlanDependency dependency, IReadOnlyDictionary<Guid, string> codes) =>
        $"{ActivityCode(dependency.PredecessorActivityId, codes)}->{ActivityCode(dependency.ActivityId, codes)}";

    private static IReadOnlyDictionary<string, object?> WbsSnapshot(
        PlanWbsNode node,
        IReadOnlyDictionary<Guid, string> keys,
        IReadOnlyDictionary<Guid, string> activityCodes,
        IEnumerable<PlanDeliverable> deliverables) => Snapshot(
        ("Parent", node.ParentWbsId.HasValue && keys.TryGetValue(node.ParentWbsId.Value, out var parent) ? parent : null),
        ("Code", node.Code),
        ("Name", node.Name),
        ("SortOrder", node.SortOrder),
        ("Activity", node.ActivityId.HasValue ? ActivityCode(node.ActivityId.Value, activityCodes) : null),
        ("Deliverable", node.DeliverableId.HasValue ? deliverables.FirstOrDefault(d => d.Id == node.DeliverableId.Value)?.Name : null),
        ("TextFormat", TextFormatSnapshot(node.TextFormat)));

    private static IReadOnlyDictionary<string, object?> ActivitySnapshot(PlanActivity activity, IReadOnlyDictionary<Guid, string> wbsKeys) => Snapshot(
        ("Code", activity.Code), ("Name", activity.Name), ("Description", activity.Description), ("Type", activity.Type.ToString()), ("SchedulingMode", activity.SchedulingMode.ToString()), ("ConstraintType", activity.ConstraintType.ToString()), ("ConstraintDate", activity.ConstraintDate), ("Deadline", activity.Deadline),
        ("StartDate", activity.StartDate), ("EndDate", activity.EndDate), ("ActualStart", activity.ActualStart), ("ActualFinish", activity.ActualFinish), ("Duration", activity.Duration), ("SortOrder", activity.SortOrder),
        ("EstimateHours", activity.EstimateHours), ("Progress", activity.Progress),
        ("Wbs", activity.WbsId.HasValue && wbsKeys.TryGetValue(activity.WbsId.Value, out var wbs) ? wbs : null),
        ("TextFormat", TextFormatSnapshot(activity.TextFormat)));

    private static object? TextFormatSnapshot(PlanTextFormat? format) => format is null ? null : new
    {
        format.IsBold,
        format.IsItalic,
        format.IsUnderline,
        TextColorToken = format.TextColorToken?.ToString(),
        HighlightColorToken = format.HighlightColorToken?.ToString(),
        FontSizeToken = format.FontSizeToken?.ToString()
    };

    private static IReadOnlyDictionary<string, object?> DeliverableSnapshot(
        PlanDeliverable deliverable,
        IReadOnlyDictionary<Guid, string> wbsKeys,
        IReadOnlyDictionary<Guid, string> activityCodes) => Snapshot(
        ("Name", deliverable.Name), ("Description", deliverable.Description), ("AcceptanceCriteria", deliverable.AcceptanceCriteria),
        ("DueDate", deliverable.DueDate), ("Wbs", deliverable.WbsId.HasValue && wbsKeys.TryGetValue(deliverable.WbsId.Value, out var wbs) ? wbs : null),
        ("Activity", deliverable.ActivityId.HasValue ? ActivityCode(deliverable.ActivityId.Value, activityCodes) : null));

    private static IReadOnlyDictionary<string, object?> DependencySnapshot(PlanDependency dependency, IReadOnlyDictionary<Guid, string> codes) => Snapshot(
        ("Activity", ActivityCode(dependency.ActivityId, codes)), ("Predecessor", ActivityCode(dependency.PredecessorActivityId, codes)),
        ("Type", dependency.Type.ToString()), ("Lag", dependency.Lag));

    private static IReadOnlyDictionary<string, object?> AssignmentSnapshot(PlanAssignment assignment, IReadOnlyDictionary<Guid, string> codes) => Snapshot(
        ("Activity", ActivityCode(assignment.ActivityId, codes)), ("ResourceId", assignment.ResourceId), ("AllocationPct", assignment.AllocationPct),
        ("HoursPerWeek", assignment.HoursPerWeek), ("StartDate", assignment.StartDate), ("EndDate", assignment.EndDate));

    private static IReadOnlyDictionary<string, object?> CalendarSnapshot(PlanCalendar calendar) => Snapshot(
        ("ProjectId", calendar.ProjectId), ("Name", calendar.Name), ("Description", calendar.Description), ("TemplateId", calendar.TemplateId),
        ("EffectiveFrom", calendar.EffectiveFrom), ("EffectiveTo", calendar.EffectiveTo),
        ("DayRules", calendar.DayRules.Where(r => !r.IsDeleted).OrderBy(r => r.DayOfWeek).Select(r => new
        {
            DayOfWeek = r.DayOfWeek.ToString(), r.IsWorkingDay, r.StartTime, r.EndTime, r.BreakStartTime, r.BreakEndTime
        }).ToArray()),
        ("Holidays", calendar.Holidays.Where(h => !h.IsDeleted).OrderBy(h => h.Date).ThenBy(h => h.Name).Select(h => new
        {
            h.Name, h.Description, h.Date, h.EndDate, h.IsRecurring
        }).ToArray()));

    private static IReadOnlyDictionary<string, object?> Snapshot(params (string Name, object? Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

    private static bool ValuesEqual(object? before, object? after) =>
        JsonSerializer.Serialize(before, JsonOptions) == JsonSerializer.Serialize(after, JsonOptions);
}
