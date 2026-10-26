using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class ProjectStatusReportComparisonTests
{
    [Fact]
    public void Compares_frozen_metrics_plans_baselines_milestones_and_risk_issue_trends()
    {
        var planId = Guid.NewGuid();
        var earlierBaselineId = Guid.NewGuid();
        var laterBaselineId = Guid.NewGuid();
        var earlier = PublishedReport(
            DateTime.UtcNow.Date.AddDays(-14), 36m, 0.72m, 0.9m, -280m, -90m,
            planId, earlierBaselineId, 1,
            [Milestone(planId, "Design sign-off", "Delayed", null, -3m)],
            [Risk("High", "Open"), Risk("Low", "Closed")],
            [Issue("Critical", "Open"), Issue("Medium", "Resolved")]);
        var later = PublishedReport(
            DateTime.UtcNow.Date.AddDays(-7), 44m, 0.8m, 0.95m, -200m, -50m,
            planId, laterBaselineId, 2,
            [Milestone(planId, "Design sign-off", "Completed", DateTime.UtcNow.Date.AddDays(-8), 0m)],
            [Risk("Critical", "Open")],
            [Issue("High", "Open")]);

        var result = ProjectStatusReportComparison.Build(earlier, later);

        Assert.Equal(earlier.Id, result.EarlierReportId);
        Assert.Equal(later.Id, result.LaterReportId);
        Assert.True(result.ReportingConfigurationChanged);
        Assert.False(result.IncludedPlansChanged);
        Assert.True(result.ReportingBaselinesChanged);
        Assert.Equal(8m, result.Metrics.Single(item => item.Code == "OverallProgressPercent").Change);
        Assert.Equal(0.08m, result.Metrics.Single(item => item.Code == "SchedulePerformanceIndex").Change);
        Assert.Single(result.MilestoneChanges);
        Assert.Equal("Completed", result.MilestoneChanges[0].LaterStatus);
        Assert.Equal(1, result.RiskTrend.EarlierOpen);
        Assert.Equal(1, result.RiskTrend.LaterHighOrCriticalOpen);
        Assert.Equal(1, result.IssueTrend.EarlierOpen);
        Assert.Equal(1, result.IssueTrend.LaterHighOrCriticalOpen);
    }

    private static ProjectStatusReportDto PublishedReport(
        DateTime statusDate,
        decimal progress,
        decimal spi,
        decimal cpi,
        decimal sv,
        decimal cv,
        Guid planId,
        Guid baselineId,
        int configVersion,
        IReadOnlyList<ProjectPerformanceMilestoneDto> milestones,
        IReadOnlyList<ProjectStatusReportRiskSnapshotDto> risks,
        IReadOnlyList<ProjectStatusReportIssueSnapshotDto> issues)
    {
        var reportId = Guid.NewGuid();
        return new ProjectStatusReportDto
        {
            Id = reportId,
            ProjectId = Guid.NewGuid(),
            ReportDate = statusDate,
            StatusDate = statusDate,
            PeriodFrom = statusDate.AddDays(-6),
            PeriodTo = statusDate,
            ReportState = ProjectStatusReportState.Published,
            ReportingConfigurationVersionSnapshot = configVersion,
            PerformanceSnapshot = new ProjectPerformanceDto
            {
                ProjectId = Guid.NewGuid(),
                StatusDate = statusDate,
                OverallProgressPercent = progress,
                SchedulePerformanceIndex = spi,
                CostPerformanceIndex = cpi,
                ScheduleVariance = sv,
                CostVariance = cv,
                Plans =
                [
                    new ProjectPlanPerformanceDto
                    {
                        PlanId = planId,
                        PlanName = "Execution",
                        IncludedInOfficialReporting = true,
                        BaselineVersionId = baselineId
                    }
                ],
                Milestones = milestones
            },
            Risks = risks,
            Issues = issues,
            ChangeRequests = []
        };
    }

    private static ProjectPerformanceMilestoneDto Milestone(Guid planId, string name, string status, DateTime? actual, decimal variance) => new()
    {
        PlanId = planId,
        PlanName = "Execution",
        Name = name,
        Status = status,
        PlannedDate = DateTime.UtcNow.Date,
        ActualCompletionDate = actual,
        VarianceDays = variance
    };

    private static ProjectStatusReportRiskSnapshotDto Risk(string level, string status) => new()
    {
        SourceRiskId = Guid.NewGuid(),
        Rank = 1,
        Title = "Risk",
        RiskLevel = level,
        RiskStatus = status
    };

    private static ProjectStatusReportIssueSnapshotDto Issue(string severity, string status) => new()
    {
        SourceIssueId = Guid.NewGuid(),
        Rank = 1,
        Title = "Issue",
        Severity = severity,
        Priority = "High",
        IssueStatus = status
    };
}
