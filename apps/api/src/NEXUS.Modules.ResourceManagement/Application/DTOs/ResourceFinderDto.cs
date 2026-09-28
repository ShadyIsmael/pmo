namespace NEXUS.Modules.ResourceManagement.Application.DTOs;

public class ResourceFinderQueryDto
{
    public Guid? ProjectId { get; set; }
    public string? Role { get; set; }
    public List<Guid>? SkillIds { get; set; }
    public string? Seniority { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? RequiredAllocation { get; set; }
    public string? ResourceType { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? VendorId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ResourceFinderResultDto
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Seniority { get; set; }
    public string? DepartmentName { get; set; }
    public string? LocationName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal MatchScore { get; set; }
    public decimal AvailableCapacityHours { get; set; }
    public decimal CurrentLoadPercent { get; set; }
    public DateTime? EarliestAvailableDate { get; set; }
    public List<ResourceFinderConflictDto> ConflictingAllocations { get; set; } = new();
    public List<ResourceFinderSkillMatchDto> SkillMatches { get; set; } = new();
    public string ResourceType { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class ResourceFinderConflictDto
{
    public Guid AllocationId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public decimal AllocationPercentage { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class ResourceFinderSkillMatchDto
{
    public string SkillName { get; set; } = string.Empty;
    public string Proficiency { get; set; } = string.Empty;
    public bool IsMatch { get; set; }
}
