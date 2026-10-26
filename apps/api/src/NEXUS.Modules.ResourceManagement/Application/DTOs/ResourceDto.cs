using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Application.DTOs;

public class ResourceDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? EmployeeId { get; set; }
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? EmploymentType { get; set; }
    public string? Seniority { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public string? BusinessUnitName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? PrimaryOrganizationalUnitId { get; set; }
    public string? PrimaryOrganizationalUnitName { get; set; }
    public string? PrimaryOrganizationalUnitCode { get; set; }
    public Guid? PrimaryManagerResourceId { get; set; }
    public string? PrimaryManagerResourceName { get; set; }
    public string? TeamName { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }
    public Guid? BaseCalendarId { get; set; }
    public string? BaseCalendarName { get; set; }
    public string? Timezone { get; set; }
    public decimal? UtilizationTarget { get; set; }
    public decimal? CurrentUtilization { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ResourceSkillDto> Skills { get; set; } = new();
    public List<ResourceAllocationDto> Allocations { get; set; } = new();
    public List<ResourceCostProfileDto> CostProfiles { get; set; } = new();
}

public class ResourceBaseCalendarDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public sealed class ResourceReferenceDataDto
{
    public IReadOnlyList<ResourceTypeOptionDto> ResourceTypes { get; init; } = Array.Empty<ResourceTypeOptionDto>();
    public IReadOnlyList<ResourceSeniorityOptionDto> SeniorityLevels { get; init; } = Array.Empty<ResourceSeniorityOptionDto>();
    public IReadOnlyList<string> CompensationTypes { get; init; } = Array.Empty<string>();
}

public sealed record ResourceTypeOptionDto(
    string Code,
    string NameEn,
    string NameAr,
    int DisplayOrder,
    bool IsDefault,
    bool RequiresVendor);

public sealed record ResourceSeniorityOptionDto(
    string Code,
    string NameEn,
    string NameAr,
    int DisplayOrder);

public class ResourceSkillDto
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string? SkillCategory { get; set; }
    public string Proficiency { get; set; } = string.Empty;
    public int YearsOfExperience { get; set; }
}

public class ResourceAllocationDto
{
    public Guid Id { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public decimal AllocationPercentage { get; set; }
    public decimal? PlannedHours { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Status { get; set; }
}

public class CreateResourceDto
{
    public Guid? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? EmployeeId { get; set; }
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public ResourceType Type { get; set; } = ResourceType.Staff;
    public string Status { get; set; } = "Active";
    public string? EmploymentType { get; set; }
    public string? Seniority { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? PrimaryOrganizationalUnitId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? VendorId { get; set; }
    public Guid? BaseCalendarId { get; set; }
    public string? Timezone { get; set; }
    public decimal? UtilizationTarget { get; set; }
    public string? Notes { get; set; }
    public List<CreateResourceSkillDto> Skills { get; set; } = new();
}

public class UpdateResourceDto
{
    public Guid? UserId { get; set; }
    public bool? RemoveUserAssociation { get; set; }
    public string? Name { get; set; }
    public string? EmployeeId { get; set; }
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public ResourceType? Type { get; set; }
    public string? Status { get; set; }
    public string? EmploymentType { get; set; }
    public string? Seniority { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? PrimaryOrganizationalUnitId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? VendorId { get; set; }
    public Guid? BaseCalendarId { get; set; }
    public string? Timezone { get; set; }
    public decimal? UtilizationTarget { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
    public List<CreateResourceSkillDto>? Skills { get; set; }
}

public class CreateResourceSkillDto
{
    public Guid SkillId { get; set; }
    public string Proficiency { get; set; } = string.Empty;
    public int YearsOfExperience { get; set; }
}

public class ResourceFilterDto
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public Guid? UserId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public Guid? OrganizationalUnitId { get; set; }
    public Guid? VendorId { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}
