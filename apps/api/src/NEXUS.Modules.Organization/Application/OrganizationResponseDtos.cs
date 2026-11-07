using NEXUS.Modules.Organization.Domain;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Modules.Organization.Application;

/// <summary>
/// Finite API response contracts for Organization, Business Unit, Department and
/// Team reads. These types intentionally contain scalar foreign keys instead of
/// EF navigation objects.
/// </summary>
public sealed class OrganizationResponseDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public string? LogoUrl { get; init; }
    public string? Website { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string? Postcode { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<BusinessUnitResponseDto> BusinessUnits { get; init; } = Array.Empty<BusinessUnitResponseDto>();
}

public sealed class BusinessUnitResponseDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
    public Guid OrganizationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public Guid? HeadOfUnitId { get; init; }
    public decimal? Budget { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<DepartmentResponseDto> Departments { get; init; } = Array.Empty<DepartmentResponseDto>();
}

public sealed class DepartmentResponseDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
    public Guid BusinessUnitId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public Guid? HeadOfDepartmentId { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<DepartmentTeamResponseDto> Teams { get; init; } = Array.Empty<DepartmentTeamResponseDto>();
}

public sealed class DepartmentTeamResponseDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
    public Guid? DepartmentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public Guid? TeamLeadId { get; init; }
    public bool IsActive { get; init; }
    public string? ExternalDirectoryId { get; init; }
    public string? EmailAlias { get; init; }
    // Team membership is not loaded by these organization hierarchy endpoints.
    // Keep the existing empty collection shape without triggering extra queries.
    public IReadOnlyList<DepartmentTeamMemberResponseDto> Members { get; init; } = Array.Empty<DepartmentTeamMemberResponseDto>();
}

public sealed class DepartmentTeamMemberResponseDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
    public Guid TeamId { get; init; }
    public Guid ResourceId { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
    public bool IsCurrent { get; init; }
}

internal static class OrganizationResponseMapper
{
    public static OrganizationResponseDto ToResponse(OrganizationEntity organization, bool includeDepartments) => new()
    {
        Id = organization.Id,
        TenantId = organization.TenantId,
        CreatedAt = organization.CreatedAt,
        UpdatedAt = organization.UpdatedAt,
        CreatedBy = organization.CreatedBy,
        UpdatedBy = organization.UpdatedBy,
        IsDeleted = organization.IsDeleted,
        RowVersion = organization.RowVersion,
        Name = organization.Name,
        Code = organization.Code,
        Description = organization.Description,
        LogoUrl = organization.LogoUrl,
        Website = organization.Website,
        Email = organization.Email,
        Phone = organization.Phone,
        AddressLine1 = organization.AddressLine1,
        AddressLine2 = organization.AddressLine2,
        City = organization.City,
        Country = organization.Country,
        Postcode = organization.Postcode,
        IsActive = organization.IsActive,
        BusinessUnits = organization.BusinessUnits
            .OrderBy(unit => unit.Name).ThenBy(unit => unit.Id)
            .Select(unit => ToResponse(unit, includeDepartments))
            .ToArray()
    };

    public static BusinessUnitResponseDto ToResponse(BusinessUnit unit, bool includeTeams) => new()
    {
        Id = unit.Id,
        TenantId = unit.TenantId,
        CreatedAt = unit.CreatedAt,
        UpdatedAt = unit.UpdatedAt,
        CreatedBy = unit.CreatedBy,
        UpdatedBy = unit.UpdatedBy,
        IsDeleted = unit.IsDeleted,
        RowVersion = unit.RowVersion,
        OrganizationId = unit.OrganizationId,
        Name = unit.Name,
        Code = unit.Code,
        Description = unit.Description,
        HeadOfUnitId = unit.HeadOfUnitId,
        Budget = unit.Budget,
        IsActive = unit.IsActive,
        Departments = unit.Departments
            .OrderBy(department => department.Name).ThenBy(department => department.Id)
            .Select(department => ToResponse(department, includeTeams))
            .ToArray()
    };

    public static DepartmentResponseDto ToResponse(Department department, bool includeTeams) => new()
    {
        Id = department.Id,
        TenantId = department.TenantId,
        CreatedAt = department.CreatedAt,
        UpdatedAt = department.UpdatedAt,
        CreatedBy = department.CreatedBy,
        UpdatedBy = department.UpdatedBy,
        IsDeleted = department.IsDeleted,
        RowVersion = department.RowVersion,
        BusinessUnitId = department.BusinessUnitId,
        Name = department.Name,
        Code = department.Code,
        Description = department.Description,
        HeadOfDepartmentId = department.HeadOfDepartmentId,
        IsActive = department.IsActive,
        Teams = includeTeams
            ? department.Teams.OrderBy(team => team.Name).ThenBy(team => team.Id).Select(ToResponse).ToArray()
            : Array.Empty<DepartmentTeamResponseDto>()
    };

    private static DepartmentTeamResponseDto ToResponse(Team team) => new()
    {
        Id = team.Id,
        TenantId = team.TenantId,
        CreatedAt = team.CreatedAt,
        UpdatedAt = team.UpdatedAt,
        CreatedBy = team.CreatedBy,
        UpdatedBy = team.UpdatedBy,
        IsDeleted = team.IsDeleted,
        RowVersion = team.RowVersion,
        DepartmentId = team.DepartmentId,
        Name = team.Name,
        Code = team.Code,
        Description = team.Description,
        TeamLeadId = team.TeamLeadId,
        IsActive = team.IsActive,
        ExternalDirectoryId = team.ExternalDirectoryId,
        EmailAlias = team.EmailAlias
    };

    public static DepartmentResponseDto ToDepartmentResponse(Department department) => ToResponse(department, includeTeams: true);
}
