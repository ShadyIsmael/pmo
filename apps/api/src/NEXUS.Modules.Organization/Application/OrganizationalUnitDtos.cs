namespace NEXUS.Modules.Organization.Application;

public sealed class OrganizationalUnitDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TypeCode { get; set; } = string.Empty;
    public string? TypeNameEn { get; set; }
    public string? TypeNameAr { get; set; }
    public string? Description { get; set; }
    public string? HierarchyPath { get; set; }
    public Guid? ParentOrganizationalUnitId { get; set; }
    public string? ParentName { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int ChildCount { get; set; }
    public int CurrentMemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class OrganizationalUnitLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class OrganizationalUnitTypeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SaveOrganizationalUnitDto
{
    public string Name { get; set; } = string.Empty;
    public string TypeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentOrganizationalUnitId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SaveOrganizationalUnitTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ResourceOrganizationalUnitDto
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public Guid OrganizationalUnitId { get; set; }
    public string OrganizationalUnitName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SaveResourceOrganizationalUnitDto
{
    public Guid ResourceId { get; set; }
    public Guid OrganizationalUnitId { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
}

public sealed class ResourceReportingLineDto
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public Guid ManagerResourceId { get; set; }
    public string ManagerResourceName { get; set; } = string.Empty;
    public string ReportingType { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SaveResourceReportingLineDto
{
    public Guid ResourceId { get; set; }
    public Guid ManagerResourceId { get; set; }
    public string ReportingType { get; set; } = "LineManager";
    public bool IsPrimary { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
}
