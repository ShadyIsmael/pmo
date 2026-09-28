using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;

namespace NEXUS.Modules.ProjectPlanning.Application.Mapping;

public static class PlanActivityMapping
{
    public static PlanActivity ToEntity(this CreatePlanActivityDto dto, Guid tenantId, Guid planVersionId, string userId)
    {
        return new PlanActivity
        {
            TenantId = tenantId,
            PlanVersionId = planVersionId,
            Code = dto.Code?.Trim() ?? string.Empty,
            Name = dto.Name?.Trim() ?? string.Empty,
            Description = dto.Description?.Trim(),
            Type = dto.Type,
            SchedulingMode = dto.SchedulingMode,
            ConstraintType = dto.ConstraintType,
            ConstraintDate = dto.ConstraintDate,
            Deadline = dto.Deadline,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ActualStart = dto.ActualStart,
            ActualFinish = dto.ActualFinish,
            CalendarId = dto.CalendarId,
            Duration = dto.Duration,
            EstimateHours = dto.EstimateHours,
            Progress = dto.Progress,
            WbsId = dto.WbsId,
            TextFormat = dto.TextFormat.ToEntity(),
            CreatedBy = userId
        };
    }

    public static PlanActivityDto ToDto(this PlanActivity activity, CriticalPathTaskMetrics? metrics = null, bool isSummaryTask = false)
    {
        return new PlanActivityDto
        {
            Id = activity.Id,
            PlanVersionId = activity.PlanVersionId,
            SourceActivityId = activity.SourceActivityId,
            Code = activity.Code,
            Name = activity.Name,
            Description = activity.Description,
            Type = activity.Type,
            IsSummaryTask = isSummaryTask,
            SchedulingMode = activity.SchedulingMode,
            ConstraintType = activity.ConstraintType,
            ConstraintDate = activity.ConstraintDate,
            Deadline = activity.Deadline,
            DeadlineExceeded = activity.Deadline.HasValue && activity.EndDate.HasValue && activity.EndDate.Value.Date > activity.Deadline.Value.Date,
            ConstraintApplied = activity.ConstraintType is not PlanConstraintType.AsSoonAsPossible and not PlanConstraintType.AsLateAsPossible && activity.ConstraintDate.HasValue,
            CalendarOverride = activity.CalendarId.HasValue,
            SchedulingConflict = HasConstraintConflict(activity),
            StartDate = activity.StartDate,
            EndDate = activity.EndDate,
            ActualStart = activity.ActualStart,
            ActualFinish = activity.ActualFinish,
            CalendarId = activity.CalendarId,
            Duration = activity.Duration,
            SortOrder = activity.SortOrder,
            EstimateHours = activity.EstimateHours,
            Progress = activity.Progress,
            WbsId = activity.WbsId,
            TextFormat = activity.TextFormat.ToDto(),
            TotalSlack = metrics?.TotalSlack,
            FreeSlack = metrics?.FreeSlack,
            Critical = metrics?.Critical ?? false,
            RowVersion = activity.RowVersion.ToArray()
        };
    }

    private static bool HasConstraintConflict(PlanActivity activity)
    {
        if (!activity.ConstraintDate.HasValue) return false;
        var date = activity.ConstraintDate.Value.Date;
        return activity.ConstraintType switch
        {
            PlanConstraintType.MustStartOn => !activity.StartDate.HasValue || activity.StartDate.Value.Date != date,
            PlanConstraintType.MustFinishOn => !activity.EndDate.HasValue || activity.EndDate.Value.Date != date,
            PlanConstraintType.StartNoEarlierThan => activity.StartDate.HasValue && activity.StartDate.Value < activity.ConstraintDate.Value,
            PlanConstraintType.StartNoLaterThan => activity.StartDate.HasValue && activity.StartDate.Value > activity.ConstraintDate.Value,
            PlanConstraintType.FinishNoEarlierThan => activity.EndDate.HasValue && activity.EndDate.Value < activity.ConstraintDate.Value,
            PlanConstraintType.FinishNoLaterThan => activity.EndDate.HasValue && activity.EndDate.Value > activity.ConstraintDate.Value,
            _ => false
        };
    }
}
