namespace NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;

public static class ProjectLifecyclePermissions
{
    public const string PhaseView = "ProjectLifecyclePhase.View";
    public const string PhaseCreate = "ProjectLifecyclePhase.Create";
    public const string PhaseEdit = "ProjectLifecyclePhase.Edit";
    public const string PhaseActivateDeactivate = "ProjectLifecyclePhase.ActivateDeactivate";
    public const string StatusView = "ProjectLifecycleStatus.View";
    public const string StatusCreate = "ProjectLifecycleStatus.Create";
    public const string StatusEdit = "ProjectLifecycleStatus.Edit";
    public const string StatusActivateDeactivate = "ProjectLifecycleStatus.ActivateDeactivate";
    public const string OptionsLookup = "ProjectLifecycle.OptionsLookup";
    public const string PhaseChange = "ProjectPhase.Change";
    public const string StatusChange = "ProjectStatus.Change";
}

public sealed record ProjectLifecycleCatalogRequest(
    Guid? OrganizationId,
    string Code,
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    int DisplayOrder,
    bool IsDefault,
    bool IsActive,
    bool IsContinuousActivity = false,
    Guid? DocumentCategoryId = null,
    byte[]? RowVersion = null,
    string? ReportingCategory = null,
    string? AttentionLevel = null);

public sealed record ProjectLifecycleCatalogDto(
    Guid Id,
    Guid TenantId,
    Guid? OrganizationId,
    string Code,
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    int DisplayOrder,
    bool IsDefault,
    bool IsActive,
    bool IsContinuousActivity,
    int ReferencedProjectCount,
    byte[] RowVersion,
    Guid? DocumentCategoryId = null,
    string ReportingCategory = ProjectStatusReportingCategories.InProgress,
    string AttentionLevel = ProjectStatusAttentionLevels.None);

public sealed record ProjectLifecycleOptionDto(
    Guid Id,
    string Code,
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    int DisplayOrder,
    bool IsActive,
    bool IsDefault,
    bool IsContinuousActivity = false,
    Guid? DocumentCategoryId = null,
    string? ReportingCategory = null,
    string AttentionLevel = ProjectStatusAttentionLevels.None);

public sealed record ProjectLifecycleOptionsDto(
    IReadOnlyList<ProjectLifecycleOptionDto> Phases,
    IReadOnlyList<ProjectLifecycleOptionDto> Statuses);

public sealed record ProjectLifecycleActionRequest(byte[] RowVersion);
