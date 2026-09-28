using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans/{planId:guid}/scheduling-analysis")]
[Authorize]
public sealed class PlanSchedulingController : ControllerBase
{
    private readonly IPlanSchedulingAnalysisService _analysisService;

    public PlanSchedulingController(IPlanSchedulingAnalysisService analysisService) => _analysisService = analysisService;

    [Authorize(Policy = "Planning.View")]
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        var version = await _analysisService.GetActiveWorkingVersionIdAsync(planId, cancellationToken);
        if (version.IsFailure)
            return version.Error?.StatusCode == 404 ? NotFound(new { error = version.Error.Message }) : BadRequest(new { error = version.Error?.Message });

        var result = await _analysisService.AnalyzeAsync(version.Value, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode == 404 ? NotFound(new { error = result.Error.Message }) : BadRequest(new { error = result.Error?.Message });

        return Ok(new PlanSchedulingAnalysisDto
        {
            PlanVersionId = version.Value,
            HasCycle = result.Value!.HasCycle,
            CriticalPathActivityIds = result.Value.CriticalPathActivityIds,
            Activities = result.Value.ActivityMetrics.Values.Select(metric => new PlanActivitySchedulingMetricsDto
            {
                ActivityId = metric.Id,
                TotalSlack = metric.TotalSlack,
                FreeSlack = metric.FreeSlack,
                Critical = metric.Critical
            }).ToList()
        });
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet("activities/{activityId:guid}/explanation")]
    public async Task<IActionResult> Explain(Guid projectId, Guid planId, Guid activityId, CancellationToken cancellationToken)
    {
        var version = await _analysisService.GetActiveWorkingVersionIdAsync(planId, cancellationToken);
        if (version.IsFailure)
            return version.Error?.StatusCode == 404 ? NotFound(new { error = version.Error.Message }) : BadRequest(new { error = version.Error?.Message });

        var result = await _analysisService.ExplainAsync(version.Value, activityId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode == 404 ? NotFound(new { error = result.Error.Message }) : BadRequest(new { error = result.Error?.Message });

        var explanation = result.Value!;
        return Ok(new PlanSchedulingExplanationDto
        {
            ActivityId = explanation.ActivityId,
            SchedulingMode = explanation.SchedulingMode,
            ConstraintType = explanation.ConstraintType,
            ConstraintDate = explanation.ConstraintDate,
            Deadline = explanation.Deadline,
            EffectiveCalendarId = explanation.EffectiveCalendarId,
            CalendarOverride = explanation.CalendarOverride,
            Start = explanation.Start,
            Finish = explanation.Finish,
            Duration = explanation.Duration,
            TotalSlack = explanation.TotalSlack,
            FreeSlack = explanation.FreeSlack,
            Critical = explanation.Critical,
            DeadlineExceeded = explanation.DeadlineExceeded,
            HasPredecessors = explanation.HasPredecessors,
            HasSuccessors = explanation.HasSuccessors,
            Reasons = explanation.Reasons.Select(reason => new PlanSchedulingReasonDto
            {
                Code = reason.Code.ToString(),
                RelatedActivityId = reason.RelatedActivityId,
                DependencyType = reason.DependencyType,
                LagDays = reason.LagDays,
                WorkingDays = reason.WorkingDays,
                ConstraintType = reason.ConstraintType
            }).ToList(),
            Issues = explanation.Issues.Select(issue => new PlanSchedulingIssueDto
            {
                Code = issue.Code,
                Message = issue.Message,
                ActivityId = issue.ActivityId,
                RelatedActivityId = issue.RelatedActivityId
            }).ToList()
        });
    }
}
