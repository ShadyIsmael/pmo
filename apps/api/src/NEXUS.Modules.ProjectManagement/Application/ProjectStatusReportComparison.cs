using NEXUS.Modules.ProjectManagement.Application.DTOs;

namespace NEXUS.Modules.ProjectManagement.Application;

public static class ProjectStatusReportComparison
{
    public static ProjectStatusReportComparisonDto Build(ProjectStatusReportDto first, ProjectStatusReportDto second)
    {
        var ordered = new[] { first, second }
            .OrderBy(report => report.StatusDate.Date)
            .ThenBy(report => report.ReportDate.Date)
            .ThenBy(report => report.Id)
            .ToArray();
        var earlier = ordered[0];
        var later = ordered[1];
        var earlierPerformance = earlier.PerformanceSnapshot;
        var laterPerformance = later.PerformanceSnapshot;

        return new ProjectStatusReportComparisonDto
        {
            EarlierReportId = earlier.Id,
            LaterReportId = later.Id,
            EarlierStatusDate = earlier.StatusDate,
            LaterStatusDate = later.StatusDate,
            ReportingConfigurationChanged = earlier.ReportingConfigurationVersionSnapshot != later.ReportingConfigurationVersionSnapshot,
            IncludedPlansChanged = !SetEquals(PlanIds(earlierPerformance), PlanIds(laterPerformance)),
            ReportingBaselinesChanged = !DictionaryEquals(Baselines(earlierPerformance), Baselines(laterPerformance)),
            Metrics =
            [
                Metric("OverallProgressPercent", earlierPerformance?.OverallProgressPercent, laterPerformance?.OverallProgressPercent),
                Metric("SchedulePerformanceIndex", earlierPerformance?.SchedulePerformanceIndex, laterPerformance?.SchedulePerformanceIndex),
                Metric("CostPerformanceIndex", earlierPerformance?.CostPerformanceIndex, laterPerformance?.CostPerformanceIndex),
                Metric("ScheduleVariance", earlierPerformance?.ScheduleVariance, laterPerformance?.ScheduleVariance),
                Metric("CostVariance", earlierPerformance?.CostVariance, laterPerformance?.CostVariance)
            ],
            MilestoneChanges = CompareMilestones(earlierPerformance?.Milestones ?? [], laterPerformance?.Milestones ?? []),
            RiskTrend = CompareTrend(earlier.Risks, later.Risks, item => item.RiskStatus, item => item.RiskLevel),
            IssueTrend = CompareTrend(earlier.Issues, later.Issues, item => item.IssueStatus, item => item.Severity)
        };
    }

    private static ProjectStatusReportMetricDeltaDto Metric(string code, decimal? earlier, decimal? later) => new()
    {
        Code = code,
        EarlierValue = earlier,
        LaterValue = later,
        Change = earlier.HasValue && later.HasValue ? later.Value - earlier.Value : null
    };

    private static IReadOnlySet<Guid> PlanIds(ProjectPerformanceDto? performance) =>
        (performance?.Plans ?? []).Where(plan => plan.IncludedInOfficialReporting).Select(plan => plan.PlanId).ToHashSet();

    private static IReadOnlyDictionary<Guid, Guid?> Baselines(ProjectPerformanceDto? performance) =>
        (performance?.Plans ?? []).Where(plan => plan.IncludedInOfficialReporting)
            .ToDictionary(plan => plan.PlanId, plan => plan.BaselineVersionId);

    private static bool SetEquals(IReadOnlySet<Guid> left, IReadOnlySet<Guid> right) => left.SetEquals(right);

    private static bool DictionaryEquals(IReadOnlyDictionary<Guid, Guid?> left, IReadOnlyDictionary<Guid, Guid?> right) =>
        left.Count == right.Count && left.All(item => right.TryGetValue(item.Key, out var value) && value == item.Value);

    private static IReadOnlyList<ProjectStatusReportMilestoneDeltaDto> CompareMilestones(
        IReadOnlyList<ProjectPerformanceMilestoneDto> earlier,
        IReadOnlyList<ProjectPerformanceMilestoneDto> later)
    {
        var earlierByKey = IndexMilestones(earlier);
        var laterByKey = IndexMilestones(later);
        return earlierByKey.Keys.Union(laterByKey.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Where(key => !earlierByKey.TryGetValue(key, out var previous)
                || !laterByKey.TryGetValue(key, out var current)
                || !SameMilestone(previous, current))
            .Select(key =>
            {
                earlierByKey.TryGetValue(key, out var previous);
                laterByKey.TryGetValue(key, out var current);
                var milestone = current ?? previous!;
                return new ProjectStatusReportMilestoneDeltaDto
                {
                    PlanId = milestone.PlanId,
                    PlanName = milestone.PlanName,
                    Name = milestone.Name,
                    EarlierStatus = previous?.Status,
                    LaterStatus = current?.Status,
                    EarlierPlannedDate = previous?.PlannedDate,
                    LaterPlannedDate = current?.PlannedDate,
                    EarlierActualCompletionDate = previous?.ActualCompletionDate,
                    LaterActualCompletionDate = current?.ActualCompletionDate,
                    EarlierVarianceDays = previous?.VarianceDays,
                    LaterVarianceDays = current?.VarianceDays
                };
            }).ToList();
    }

    private static Dictionary<string, ProjectPerformanceMilestoneDto> IndexMilestones(IReadOnlyList<ProjectPerformanceMilestoneDto> milestones) =>
        milestones.GroupBy(item => $"{item.PlanId:N}|{item.Name.Trim()}", StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.OrderBy(item => item.BaselineDate).ThenBy(item => item.PlannedDate)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .Select((item, index) => new KeyValuePair<string, ProjectPerformanceMilestoneDto>($"{group.Key}|{index}", item)))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static bool SameMilestone(ProjectPerformanceMilestoneDto left, ProjectPerformanceMilestoneDto right) =>
        string.Equals(left.Status, right.Status, StringComparison.OrdinalIgnoreCase)
        && left.PlannedDate?.Date == right.PlannedDate?.Date
        && left.ActualCompletionDate?.Date == right.ActualCompletionDate?.Date
        && left.VarianceDays == right.VarianceDays;

    private static ProjectStatusReportTrendDto CompareTrend<T>(
        IReadOnlyList<T> earlier,
        IReadOnlyList<T> later,
        Func<T, string> status,
        Func<T, string> severity)
    {
        static bool IsOpen(string value) => !value.Equals("Closed", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("Resolved", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("Mitigated", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("Completed", StringComparison.OrdinalIgnoreCase);
        static bool IsHighOrCritical(string value) => value.Equals("High", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Critical", StringComparison.OrdinalIgnoreCase);

        return new ProjectStatusReportTrendDto
        {
            EarlierTotal = earlier.Count,
            LaterTotal = later.Count,
            EarlierOpen = earlier.Count(item => IsOpen(status(item))),
            LaterOpen = later.Count(item => IsOpen(status(item))),
            EarlierHighOrCriticalOpen = earlier.Count(item => IsOpen(status(item)) && IsHighOrCritical(severity(item))),
            LaterHighOrCriticalOpen = later.Count(item => IsOpen(status(item)) && IsHighOrCritical(severity(item)))
        };
    }
}
