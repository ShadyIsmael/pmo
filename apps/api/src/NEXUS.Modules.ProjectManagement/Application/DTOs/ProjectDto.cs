using NEXUS.Modules.ProjectManagement.Domain;
using System.Text.Json.Serialization;

namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public class ProjectDto
{
    public Guid Id { get; set; }
    public Guid PublicId { get; set; }
    public Guid TenantId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProgramId { get; set; }
    /// <summary>Legacy catalog code projection. Use StatusId and localized names for lifecycle behavior and display.</summary>
    public string Status { get; set; } = string.Empty;
    public Guid? StatusId { get; set; }
    public string? StatusNameEn { get; set; }
    public string? StatusNameAr { get; set; }
    public string? ReportingCategory { get; set; }
    public string? AttentionLevel { get; set; }
    public required string Priority { get; set; }
    public Guid? ProjectManagerId { get; set; }
    public string? ProjectManagerName { get; set; }
    public Guid? SponsorId { get; set; }
    public string? SponsorName { get; set; }
    public Guid? PortfolioId { get; set; }
    public string? PortfolioName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public decimal? Budget { get; set; }
    public string Currency { get; set; } = "GBP";
    public decimal? ActualCost { get; set; }
    public decimal? PlannedValue { get; set; }
    public decimal? EarnedValue { get; set; }
    public decimal Progress { get; set; }
    public string Health { get; set; } = string.Empty;
    public string RagStatus { get; set; } = string.Empty;
    public string ConfidenceLevel { get; set; } = string.Empty;
    public string? Phase { get; set; }
    public Guid? PhaseId { get; set; }
    public string? PhaseNameEn { get; set; }
    public string? PhaseNameAr { get; set; }
    public string? RiskRating { get; set; }
    public string? BusinessCase { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? WorkingCalendarId { get; set; }
    public IReadOnlyList<ProjectMemberDto> Members { get; set; } = Array.Empty<ProjectMemberDto>();
}

public class ProjectMemberDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string MemberType { get; set; } = "Resource";
    public string ResourceName { get; set; } = string.Empty;
    public string? ResourceJobTitle { get; set; }
    public string Role { get; set; } = string.Empty;
    public decimal AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CalendarPolicy { get; set; } = CalendarPolicyConstants.UseResourceCalendar;
    public bool IsBillable { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateProjectDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid? PhaseId { get; set; }
    public Guid? StatusId { get; set; }
    /// <summary>Compatibility input resolved against active catalog codes/names; never used as an unrestricted value.</summary>
    public string? Status { get; set; }
    public string Priority { get; set; } = "Medium";
    public string? Category { get; set; }
    public Guid? ProjectManagerId { get; set; }
    public Guid? SponsorId { get; set; }
    public Guid? PortfolioId { get; set; }
    public Guid? DepartmentId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
    /// <summary>Optional initial Draft allocation for the tenant's current Fiscal Year. It is never approved during Project creation.</summary>
    public decimal? InitialAnnualBudgetAmount { get; set; }
    public string? Currency { get; set; }
    public string? BusinessCase { get; set; }
    public string? Notes { get; set; }
    public string ConfidenceLevel { get; set; } = "Green";
    public Guid? WorkingCalendarId { get; set; }
}

public sealed record ChangeProjectStatusDto(Guid TargetStatusId, string? Reason);

public class UpdateProjectDto
{
    private Guid? _projectManagerId;

    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid? ProgramId { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Category { get; set; }
    public Guid? ProjectManagerId
    {
        get => _projectManagerId;
        set
        {
            _projectManagerId = value;
            ProjectManagerIdWasProvided = true;
        }
    }
    [JsonIgnore]
    public bool ProjectManagerIdWasProvided { get; private set; }
    public Guid? SponsorId { get; set; }
    public bool ClearSponsor { get; set; }
    public Guid? PortfolioId { get; set; }
    public Guid? DepartmentId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
    public string? Currency { get; set; }
    public decimal? PlannedValue { get; set; }
    public decimal? EarnedValue { get; set; }
    public decimal? ActualCost { get; set; }
    public string? BusinessCase { get; set; }
    public string? Notes { get; set; }
    public string? ConfidenceLevel { get; set; }
    public Guid? WorkingCalendarId { get; set; }
    public ProjectHandoverDto? Handover { get; set; }
}

public class ProjectHandoverDto
{
    public DateTime? HandoverDate { get; set; }
    public string? HandoverReason { get; set; }
    public string? HandoverNotes { get; set; }
}

public class ProjectManagerHistoryDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectManagerId { get; set; }
    public string ProjectManagerName { get; set; } = string.Empty;
    public string? ProjectManagerEmail { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public Guid? HandoverFromProjectManagerId { get; set; }
    public string? HandoverFromProjectManagerName { get; set; }
    public DateTime? HandoverDate { get; set; }
    public string? HandoverReason { get; set; }
    public string? HandoverNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateProjectMemberDto
{
    public Guid ProjectId { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string Role { get; set; } = string.Empty;
    public decimal AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsBillable { get; set; } = true;
    public string? Notes { get; set; }
}

public class UpdateProjectMemberDto
{
    public string? Role { get; set; }
    public decimal? AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Status { get; set; }
    public bool? IsBillable { get; set; }
    public string? Notes { get; set; }
}

public class ProjectSummaryDto
{
    public Guid Id { get; set; }
    public Guid PublicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? StatusId { get; set; }
    public string? StatusNameEn { get; set; }
    public string? StatusNameAr { get; set; }
    public string? ReportingCategory { get; set; }
    public string? AttentionLevel { get; set; }
    public string? Phase { get; set; }
    public Guid? PhaseId { get; set; }
    public string? PhaseNameEn { get; set; }
    public string? PhaseNameAr { get; set; }
    public string Health { get; set; } = string.Empty;
    public string RagStatus { get; set; } = string.Empty;
    public decimal Progress { get; set; }
    public decimal? PlanProgress { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
    public decimal? ActualCost { get; set; }
    public string Currency { get; set; } = "GBP";
    public Guid? ProjectManagerId { get; set; }
    public string? ProjectManagerName { get; set; }
    public Guid? WorkingCalendarId { get; set; }
}

public class ProjectTeamMemberAllocationDto
{
    public Guid AllocationId { get; set; }
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string? ResourceJobTitle { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public decimal AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsBillable { get; set; }
    public string? Notes { get; set; }
}
