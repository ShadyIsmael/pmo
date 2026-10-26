namespace NEXUS.Modules.ResourceManagement.Domain;

/// <summary>
/// String limits shared by Resource API validation and SQL Server mapping.
/// </summary>
public static class ResourceFieldLengths
{
    public const int Name = 200;
    public const int EmployeeId = 50;
    public const int JobTitle = 200;
    public const int Email = 256;
    public const int Phone = 50;
    public const int AvatarUrl = 500;
    public const int Type = 50;
    public const int Status = 50;
    public const int EmploymentType = 50;
    public const int Seniority = 50;
    public const int Timezone = 50;
    public const int Notes = 2000;
}
