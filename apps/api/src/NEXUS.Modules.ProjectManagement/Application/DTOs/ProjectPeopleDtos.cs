using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public sealed class ProjectPersonDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ResourceId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? JobTitle { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public bool HasSystemUser => UserId.HasValue;
    public string RelationshipType { get; set; } = ProjectRelationshipType.TeamMember;
    public string Status { get; set; } = string.Empty;
    public decimal? AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsBillable { get; set; }
    public string? Role { get; set; }
    public string? Interest { get; set; }
    public string? Influence { get; set; }
    public bool IsLegacy { get; set; }
    public string CalendarPolicy { get; set; } = CalendarPolicyConstants.UseResourceCalendar;
    public List<ProjectRoleRefDto> FunctionalRoles { get; set; } = new();
    public IReadOnlyList<string> AdditionalRelationshipTypes { get; set; } = Array.Empty<string>();
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ProjectRoleRefDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public sealed class CreateProjectPersonDto
{
    public Guid ResourceId { get; set; }
    public string RelationshipType { get; set; } = ProjectRelationshipType.TeamMember;
    public string? Role { get; set; }
    public decimal? AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsBillable { get; set; }
    public string? Interest { get; set; }
    public string? Influence { get; set; }
    public List<Guid>? ProjectRoleIds { get; set; }
    public string? CalendarPolicy { get; set; }
}

public sealed class UpdateProjectPersonDto
{
    public string Source { get; set; } = string.Empty;
    public string? RelationshipType { get; set; }
    public string? Role { get; set; }
    public decimal? AllocationPercentage { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsBillable { get; set; }
    public string? Interest { get; set; }
    public string? Influence { get; set; }
    public byte[]? RowVersion { get; set; }
    public List<Guid>? ProjectRoleIds { get; set; }
    public string? CalendarPolicy { get; set; }
}

public static class ProjectRelationshipType
{
    public const string ProjectManager = "ProjectManager";
    public const string TeamMember = "TeamMember";
    public const string Sponsor = "Sponsor";
    public const string Stakeholder = "Stakeholder";
    public const string Other = "Other";

    public static bool IsSupported(string? value) => value is ProjectManager or TeamMember or Sponsor or Stakeholder or Other;
}
