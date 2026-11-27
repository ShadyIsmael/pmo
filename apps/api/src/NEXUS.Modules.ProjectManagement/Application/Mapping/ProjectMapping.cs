using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Modules.ProjectManagement.Application.Mapping;

public static class ProjectMapping
{
    public static ProjectDto ToDto(this Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            PublicId = project.PublicId,
            TenantId = project.TenantId,
            RowVersion = project.RowVersion,
            Name = project.Name,
            Code = project.Code,
            Description = project.Description,
            ProgramId = project.ProgramId,
            Status = project.Status,
            StatusId = project.ProjectStatusId,
            Priority = project.Priority,
            ProjectManagerId = project.ProjectManagerId,
            SponsorId = project.SponsorId,
            PortfolioId = project.PortfolioId,
            DepartmentId = project.DepartmentId,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            ActualStartDate = project.ActualStartDate,
            ActualEndDate = project.ActualEndDate,
            Budget = project.Budget,
            OriginalProjectBudget = project.OriginalProjectBudget,
            Currency = project.Currency,
            ActualCost = project.ActualCost,
            PlannedValue = project.PlannedValue,
            EarnedValue = project.EarnedValue,
            Progress = project.Progress,
            Health = project.Health,
            RagStatus = project.RagStatus,
            ConfidenceLevel = project.ConfidenceLevel,
            WorkingCalendarId = project.WorkingCalendarId,
            Phase = project.Phase,
            PhaseId = project.ProjectPhaseId,
            RiskRating = project.RiskRating,
            BusinessCase = project.BusinessCase,
            Notes = project.Notes,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            CreatedBy = project.CreatedBy,
            UpdatedBy = project.UpdatedBy,
            IsDeleted = project.IsDeleted,
            Members = project.Members?.Select(m => m.ToDto()).ToList() ?? []
        };
    }

    public static ProjectSummaryDto ToSummaryDto(this Project project)
    {
        return new ProjectSummaryDto
        {
            Id = project.Id,
            PublicId = project.PublicId,
            Name = project.Name,
            Code = project.Code,
            Status = project.Status,
            StatusId = project.ProjectStatusId,
            Health = project.Health,
            RagStatus = project.RagStatus,
            Phase = project.Phase,
            PhaseId = project.ProjectPhaseId,
            Progress = project.Progress,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            Budget = project.Budget,
            ActualCost = project.ActualCost,
            Currency = project.Currency,
            WorkingCalendarId = project.WorkingCalendarId,
            ProjectManagerId = project.ProjectManagerId,
        };
    }

    public static ProjectMemberDto ToDto(this ProjectMember member)
    {
        return new ProjectMemberDto
        {
            Id = member.Id,
            ProjectId = member.ProjectId,
            ResourceId = member.ResourceId,
            TeamId = member.TeamId,
            MemberType = member.TeamId.HasValue ? "Team" : "Resource",
            Role = member.Role,
            AllocationPercentage = member.AllocationPercentage,
            HoursPerWeek = member.HoursPerWeek,
            StartDate = member.StartDate,
            EndDate = member.EndDate,
            Status = member.Status,
            CalendarPolicy = member.CalendarPolicy,
            IsBillable = member.IsBillable,
            Notes = ProjectSponsorMembershipSynchronizer.IsManagedMembership(member) ? null : member.Notes,
            CreatedAt = member.CreatedAt,
            UpdatedAt = member.UpdatedAt
        };
    }

    public static Project ToEntity(this CreateProjectDto dto, string userId)
    {
        return new Project
        {
            Name = dto.Name,
            Code = dto.Code,
            Description = dto.Description,
            ProgramId = dto.ProgramId,
            ProjectStatusId = dto.StatusId,
            Status = dto.Status,
            Priority = dto.Priority,
            Category = dto.Category,
            ProjectManagerId = dto.ProjectManagerId,
            SponsorId = dto.SponsorId,
            PortfolioId = dto.PortfolioId,
            DepartmentId = dto.DepartmentId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Budget = dto.Budget,
            // The original lifetime ceiling is the value approved at creation and never changes afterwards.
            OriginalProjectBudget = dto.Budget,
            Currency = dto.Currency ?? string.Empty,
            BusinessCase = dto.BusinessCase,
            Notes = dto.Notes,
            ConfidenceLevel = string.IsNullOrWhiteSpace(dto.ConfidenceLevel) ? "Green" : dto.ConfidenceLevel,
            WorkingCalendarId = dto.WorkingCalendarId,
            Health = "Unknown",
            RagStatus = "Unknown",
            Progress = 0,
            CreatedBy = userId
        };
    }

    public static void UpdateEntity(this UpdateProjectDto dto, Project project, string userId)
    {
        if (dto.Name != null) project.Name = dto.Name;
        if (dto.Code != null) project.Code = dto.Code;
        if (dto.Description != null) project.Description = dto.Description;
        if (dto.ProgramId.HasValue) project.ProgramId = dto.ProgramId;
        // Status is governed by the canonical Reference Data identity. The legacy
        // Status string is never accepted as an update source; callers must send
        // StatusId so catalog scope, activation, and transition rules can be enforced.
        if (dto.StatusId.HasValue) project.ProjectStatusId = dto.StatusId;
        if (dto.Priority != null) project.Priority = dto.Priority;
        if (dto.Category != null) project.Category = dto.Category;
        // Project manager ownership is assigned at creation or changed only through the
        // authorized Handover / Initial Assignment workflows, never through Project update.
        if (dto.ClearSponsor) project.SponsorId = null;
        else if (dto.SponsorId.HasValue) project.SponsorId = dto.SponsorId;
        if (dto.PortfolioId.HasValue) project.PortfolioId = dto.PortfolioId;
        if (dto.DepartmentId.HasValue) project.DepartmentId = dto.DepartmentId;
        if (dto.StartDate.HasValue) project.StartDate = dto.StartDate;
        if (dto.EndDate.HasValue) project.EndDate = dto.EndDate;
        // Project.Budget is governed by creation and approved Change Requests only.
        if (dto.Currency != null) project.Currency = dto.Currency;
        if (dto.PlannedValue.HasValue) project.PlannedValue = dto.PlannedValue;
        if (dto.EarnedValue.HasValue) project.EarnedValue = dto.EarnedValue;
        if (dto.ActualCost.HasValue) project.ActualCost = dto.ActualCost;
        if (dto.BusinessCase != null) project.BusinessCase = dto.BusinessCase;
        if (dto.Notes != null) project.Notes = dto.Notes;
        if (dto.ConfidenceLevel != null) project.ConfidenceLevel = dto.ConfidenceLevel;
        if (dto.WorkingCalendarId.HasValue) project.WorkingCalendarId = dto.WorkingCalendarId;
        project.UpdatedBy = userId;
    }

    public static ProjectManagerHistoryDto ToHistoryDto(this ProjectManagerAssignment assignment)
    {
        return new ProjectManagerHistoryDto
        {
            Id = assignment.Id,
            ProjectId = assignment.ProjectId,
            ProjectManagerId = assignment.ProjectManagerId,
            StartDate = assignment.StartDate,
            EndDate = assignment.EndDate,
            IsCurrent = assignment.IsCurrent,
            HandoverFromProjectManagerId = assignment.HandoverFromProjectManagerId,
            HandoverDate = assignment.HandoverDate,
            HandoverReason = assignment.HandoverReason,
            HandoverNotes = assignment.HandoverNotes,
            CreatedAt = assignment.CreatedAt,
            CreatedBy = assignment.CreatedBy
        };
    }

    public static ProjectMember ToEntity(this CreateProjectMemberDto dto, string userId)
    {
        return new ProjectMember
        {
            ProjectId = dto.ProjectId,
            ResourceId = dto.ResourceId,
            TeamId = dto.TeamId,
            Role = dto.Role,
            AllocationPercentage = dto.AllocationPercentage,
            HoursPerWeek = dto.HoursPerWeek,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = dto.Status,
            IsBillable = dto.IsBillable,
            Notes = dto.Notes,
            CreatedBy = userId
        };
    }

    public static void UpdateEntity(this UpdateProjectMemberDto dto, ProjectMember member, string userId)
    {
        if (dto.Role != null) member.Role = dto.Role;
        if (dto.AllocationPercentage.HasValue) member.AllocationPercentage = dto.AllocationPercentage.Value;
        if (dto.HoursPerWeek.HasValue) member.HoursPerWeek = dto.HoursPerWeek;
        if (dto.StartDate.HasValue) member.StartDate = dto.StartDate.Value;
        if (dto.EndDate.HasValue) member.EndDate = dto.EndDate.Value;
        if (dto.Status != null) member.Status = dto.Status;
        if (dto.IsBillable.HasValue) member.IsBillable = dto.IsBillable.Value;
        if (dto.Notes != null) member.Notes = dto.Notes;
        member.UpdatedBy = userId;
    }

    public static ProjectChangeRequestDto ToDto(this ProjectChangeRequest request, string projectName, string requestedByName, string? reviewedByName = null, Guid? projectPublicId = null)
    {
        return new ProjectChangeRequestDto
        {
            Id = request.Id,
            ProjectId = request.ProjectId,
            ProjectPublicId = projectPublicId,
            ProjectName = projectName,
            Number = request.Number,
            Type = request.Type,
            Reason = request.Reason,
            Justification = request.Justification,
            Impact = request.Impact,
            BudgetBeforeChange = request.BudgetBeforeChange,
            BudgetChangeAmount = request.BudgetChangeAmount,
            ProposedProjectBudget = request.BudgetBeforeChange + request.BudgetChangeAmount,
            Attachments = request.Attachments,
            Status = request.Status,
            RequestedBy = request.RequestedBy,
            RequestedByName = requestedByName,
            RequestedAt = request.RequestedAt,
            ReviewedBy = request.ReviewedBy,
            ReviewedByName = reviewedByName,
            ReviewedAt = request.ReviewedAt,
            DecisionNotes = request.DecisionNotes,
            CreatedAt = request.CreatedAt,
            CreatedBy = request.CreatedBy,
            UpdatedAt = request.UpdatedAt,
            UpdatedBy = request.UpdatedBy,
            ApprovalStrategy = request.ApprovalStrategy,
            SubmittedAt = request.SubmittedAt,
            WorkflowInstanceId = request.WorkflowInstanceId,
            RowVersion = request.RowVersion
        };
    }

    public static ProjectChangeRequest ToEntity(this CreateProjectChangeRequestDto dto, string userId)
    {
        return new ProjectChangeRequest
        {
            ProjectId = dto.ProjectId,
            Type = dto.Type,
            Reason = dto.Reason,
            Justification = dto.Justification,
            Impact = dto.Impact,
            Attachments = dto.Attachments,
            ApprovalStrategy = dto.ApprovalStrategy,
            SelectedApproverUserIdsJson = System.Text.Json.JsonSerializer.Serialize(dto.ApproverUserIds.Distinct().ToList()),
            RequestedBy = Guid.Parse(userId), // Assuming userId is a string representation of Guid
            RequestedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
    }
}
