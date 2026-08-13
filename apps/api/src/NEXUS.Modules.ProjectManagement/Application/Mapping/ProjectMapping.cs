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
            TenantId = project.TenantId,
            Name = project.Name,
            Code = project.Code,
            Description = project.Description,
            ProgramId = project.ProgramId,
            Status = project.Status,
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
            Currency = project.Currency,
            ActualCost = project.ActualCost,
            PlannedValue = project.PlannedValue,
            EarnedValue = project.EarnedValue,
            CostVariance = project.CostVariance,
            ScheduleVariance = project.ScheduleVariance,
            Progress = project.Progress,
            Health = project.Health,
            RagStatus = project.RagStatus,
            ConfidenceLevel = project.ConfidenceLevel,
            Phase = project.Phase,
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
            Name = project.Name,
            Code = project.Code,
            Status = project.Status,
            Health = project.Health,
            RagStatus = project.RagStatus,
            Progress = project.Progress,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            Budget = project.Budget,
            ActualCost = project.ActualCost,
            Currency = project.Currency,
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
            Role = member.Role,
            AllocationPercentage = member.AllocationPercentage,
            HoursPerWeek = member.HoursPerWeek,
            StartDate = member.StartDate,
            EndDate = member.EndDate,
            Status = member.Status,
            IsBillable = member.IsBillable,
            Notes = member.Notes,
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
            Currency = dto.Currency,
            BusinessCase = dto.BusinessCase,
            Notes = dto.Notes,
            ConfidenceLevel = string.IsNullOrWhiteSpace(dto.ConfidenceLevel) ? "Green" : dto.ConfidenceLevel,
            Health = "Green",
            RagStatus = "Green",
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
        if (dto.Status != null) project.Status = dto.Status;
        if (dto.Priority != null) project.Priority = dto.Priority;
        if (dto.Category != null) project.Category = dto.Category;
        if (dto.ProjectManagerId.HasValue) project.ProjectManagerId = dto.ProjectManagerId;
        if (dto.SponsorId.HasValue) project.SponsorId = dto.SponsorId;
        if (dto.PortfolioId.HasValue) project.PortfolioId = dto.PortfolioId;
        if (dto.DepartmentId.HasValue) project.DepartmentId = dto.DepartmentId;
        if (dto.StartDate.HasValue) project.StartDate = dto.StartDate;
        if (dto.EndDate.HasValue) project.EndDate = dto.EndDate;
        if (dto.Budget.HasValue) project.Budget = dto.Budget;
        if (dto.Currency != null) project.Currency = dto.Currency;
        if (dto.BusinessCase != null) project.BusinessCase = dto.BusinessCase;
        if (dto.Notes != null) project.Notes = dto.Notes;
        if (dto.ConfidenceLevel != null) project.ConfidenceLevel = dto.ConfidenceLevel;
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

    public static ProjectChangeRequestDto ToDto(this ProjectChangeRequest request, string projectName, string requestedByName, string? reviewedByName = null)
    {
        return new ProjectChangeRequestDto
        {
            Id = request.Id,
            ProjectId = request.ProjectId,
            ProjectName = projectName,
            Number = request.Number,
            Type = request.Type,
            Reason = request.Reason,
            Justification = request.Justification,
            Impact = request.Impact,
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
            UpdatedBy = request.UpdatedBy
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
            RequestedBy = Guid.Parse(userId), // Assuming userId is a string representation of Guid
            RequestedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
    }
}