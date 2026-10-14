namespace NEXUS.Modules.Integrations.Application;

public sealed record DirectoryReferenceOptionDto(Guid Id, string Name, string? Code);

public sealed record DirectoryConnectionDto(
    Guid Id,
    string Name,
    string ProviderType,
    bool IsActive,
    bool IsDefault,
    string? DirectoryTenantId,
    string? ClientId,
    string? CredentialReference,
    bool CredentialConfigured,
    string? Host,
    int? Port,
    bool UseSsl,
    string? BaseDn,
    string? BindUser,
    string? BindCredentialReference,
    bool BindCredentialConfigured,
    string? Domain,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class SaveDirectoryConnectionRequest
{
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public string? DirectoryTenantId { get; set; }
    public string? ClientId { get; set; }
    public string? CredentialReference { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public bool UseSsl { get; set; } = true;
    public string? BaseDn { get; set; }
    public string? BindUser { get; set; }
    public string? BindCredentialReference { get; set; }
    public string? Domain { get; set; }
}

public sealed record DirectorySyncScopeDto(
    string ScopeType,
    IReadOnlyList<string> SelectedGroupIds,
    IReadOnlyList<string> SelectedUserIds,
    IReadOnlyList<string> OrganizationalUnits,
    string? UserFilter,
    string? GroupFilter,
    int MaxItems);

public sealed class SaveDirectorySyncScopeRequest
{
    public string ScopeType { get; set; } = "SelectedGroups";
    public List<string> SelectedGroupIds { get; set; } = [];
    public List<string> SelectedUserIds { get; set; } = [];
    public List<string> OrganizationalUnits { get; set; } = [];
    public string? UserFilter { get; set; }
    public string? GroupFilter { get; set; }
    public int MaxItems { get; set; } = 500;
}

public sealed record DirectoryFieldMappingDto(string TargetField, string SourceAttribute, string UpdatePolicy, string? Transform);
public sealed class SaveDirectoryFieldMappingsRequest { public List<DirectoryFieldMappingDto> Mappings { get; set; } = []; }

public sealed record DirectoryGroupDto(string ExternalDirectoryId, string Name, string? Description, bool IsEnabled);
public sealed record DirectoryGroupMappingDto(string ExternalGroupId, string ExternalGroupName, Guid? RoleId, bool IsActive);
public sealed class SaveDirectoryGroupMappingsRequest { public List<DirectoryGroupMappingDto> Mappings { get; set; } = []; }

public sealed record DirectoryOrganizationMappingDto(string DirectoryValue, Guid OrganizationalUnitId);
public sealed class SaveDirectoryOrganizationMappingsRequest { public List<DirectoryOrganizationMappingDto> Mappings { get; set; } = []; }

public sealed record DirectoryProvisioningPolicyDto(
    string MissingUserAction,
    string MissingGroupAction,
    string DisabledUserAction,
    string OutOfScopeUserAction,
    string ChangedGroupAction,
    string MissingGroupReferenceAction,
    bool CreateResource,
    Guid? DefaultOrganizationalUnitId,
    Guid? DefaultBaseCalendarId,
    string? DefaultEmploymentType,
    string? DefaultSeniority);

public sealed class SaveDirectoryProvisioningPolicyRequest
{
    public string MissingUserAction { get; set; } = "Review";
    public string MissingGroupAction { get; set; } = "Review";
    public string DisabledUserAction { get; set; } = "Review";
    public string OutOfScopeUserAction { get; set; } = "Review";
    public string ChangedGroupAction { get; set; } = "Review";
    public string MissingGroupReferenceAction { get; set; } = "Review";
    public bool CreateResource { get; set; }
    public Guid? DefaultOrganizationalUnitId { get; set; }
    public Guid? DefaultBaseCalendarId { get; set; }
    public string? DefaultEmploymentType { get; set; }
    public string? DefaultSeniority { get; set; }
}

public sealed record DirectoryPreviewItemDto(
    string ItemType,
    string ExternalDirectoryId,
    string DisplayName,
    string Classification,
    string ProposedAction,
    string PolicyReason,
    Guid? MatchedUserId,
    string? MatchedUserName,
    IReadOnlyDictionary<string, string?> Changes,
    bool Selectable);

public sealed record DirectoryPreviewResultDto(
    Guid ConnectionId,
    int Page,
    int PageSize,
    bool HasMore,
    IReadOnlyList<DirectoryPreviewItemDto> Items,
    DirectoryProviderTestResult ProviderStatus);

public sealed record SelectedDirectorySyncItem(string ItemType, string ExternalDirectoryId, string Action);
public sealed class ExecuteDirectorySyncRequest { public List<SelectedDirectorySyncItem> Items { get; set; } = []; }

public sealed record DirectorySyncRunDto(
    Guid Id,
    Guid ConnectionId,
    string ConnectionName,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Mode,
    string Scope,
    string Status,
    int UsersFetched,
    int UsersCreated,
    int UsersUpdated,
    int UsersDeactivated,
    int UsersSkipped,
    int GroupsCreated,
    int GroupsUpdated,
    int GroupsDeactivated,
    int Conflicts,
    int Failures);

public sealed record DirectorySyncItemDto(
    Guid Id,
    string ItemType,
    string ExternalDirectoryId,
    string? DisplayName,
    string Classification,
    string ProposedAction,
    Guid? MatchedUserId,
    string Status,
    string? ErrorCode,
    IReadOnlyDictionary<string, string?> Details);

public sealed record DirectorySyncRunDetailDto(DirectorySyncRunDto Run, IReadOnlyList<DirectorySyncItemDto> Items);
public sealed record DirectorySyncExecutionResultDto(DirectorySyncRunDto Run, IReadOnlyList<DirectorySyncItemDto> Items);
