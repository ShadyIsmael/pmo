using NEXUS.SharedKernel;

namespace NEXUS.Modules.Integrations.Domain;

public static class DirectoryProviderTypes
{
    public const string Entra = "Entra";
    public const string Ldap = "Ldap";
}

public static class DirectoryProvisioningActions
{
    public const string Ignore = "Ignore";
    public const string Review = "Review";
    public const string AutoCreate = "AutoCreate";
    public const string AutoUpdate = "AutoUpdate";
    public const string AutoDeactivate = "AutoDeactivate";
    public const string DoNothing = "DoNothing";
}

public static class DirectoryUpdatePolicies
{
    public const string Ignore = "Ignore";
    public const string Review = "Review";
    public const string AutoUpdate = "AutoUpdate";
}

public static class DirectoryScopeTypes
{
    public const string SelectedGroups = "SelectedGroups";
    public const string SelectedUsers = "SelectedUsers";
    public const string OrganizationalUnits = "OrganizationalUnits";
    public const string AllUsers = "AllUsers";
}

public class DirectoryConnection : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = DirectoryProviderTypes.Entra;
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }

    // Entra configuration. CredentialReference is an alias into server-side secret
    // configuration; the credential value is never accepted from or returned to the UI.
    public string? DirectoryTenantId { get; set; }
    public string? ClientId { get; set; }
    public string? CredentialReference { get; set; }

    // LDAP configuration. BindCredentialReference follows the same server-only rule.
    public string? Host { get; set; }
    public int? Port { get; set; }
    public bool UseSsl { get; set; } = true;
    public string? BaseDn { get; set; }
    public string? BindUser { get; set; }
    public string? BindCredentialReference { get; set; }
    public string? Domain { get; set; }
}

public class DirectorySyncScope : BaseEntity
{
    public Guid DirectoryConnectionId { get; set; }
    public string ScopeType { get; set; } = DirectoryScopeTypes.SelectedGroups;
    public string SelectedGroupIdsJson { get; set; } = "[]";
    public string SelectedUserIdsJson { get; set; } = "[]";
    public string OrganizationalUnitsJson { get; set; } = "[]";
    public string? UserFilter { get; set; }
    public string? GroupFilter { get; set; }
    public int MaxItems { get; set; } = 500;
}

public class DirectoryFieldMapping : BaseEntity
{
    public Guid DirectoryConnectionId { get; set; }
    public string TargetField { get; set; } = string.Empty;
    public string SourceAttribute { get; set; } = string.Empty;
    public string UpdatePolicy { get; set; } = DirectoryUpdatePolicies.Review;
    public string? Transform { get; set; }
}

public class DirectoryGroup : BaseEntity
{
    public Guid DirectoryConnectionId { get; set; }
    public string ExternalDirectoryId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }
}

public class DirectoryGroupMapping : BaseEntity
{
    public Guid DirectoryConnectionId { get; set; }
    public string ExternalGroupId { get; set; } = string.Empty;
    public string ExternalGroupName { get; set; } = string.Empty;
    public Guid? RoleId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DirectoryOrganizationMapping : BaseEntity
{
    public Guid DirectoryConnectionId { get; set; }
    public string DirectoryValue { get; set; } = string.Empty;
    public Guid OrganizationalUnitId { get; set; }
}

public class DirectoryProvisioningPolicy : BaseEntity
{
    public Guid DirectoryConnectionId { get; set; }
    public string MissingUserAction { get; set; } = DirectoryProvisioningActions.Review;
    public string MissingGroupAction { get; set; } = DirectoryProvisioningActions.Review;
    public string DisabledUserAction { get; set; } = DirectoryProvisioningActions.Review;
    public string OutOfScopeUserAction { get; set; } = DirectoryProvisioningActions.Review;
    public string ChangedGroupAction { get; set; } = DirectoryProvisioningActions.Review;
    public string MissingGroupReferenceAction { get; set; } = DirectoryProvisioningActions.Review;
    public bool CreateResource { get; set; }
    public Guid? DefaultOrganizationalUnitId { get; set; }
    public Guid? DefaultBaseCalendarId { get; set; }
    public string? DefaultEmploymentType { get; set; }
    public string? DefaultSeniority { get; set; }
}

public class DirectoryUserLink : BaseEntity
{
    public Guid DirectoryConnectionId { get; set; }
    public string ExternalDirectoryId { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public string? EmployeeId { get; set; }
    public string? ManagerExternalId { get; set; }
    public string? DistinguishedName { get; set; }
    public Guid? ManagedOrganizationalMembershipId { get; set; }
    public Guid? ManagedReportingLineId { get; set; }
    public bool IsDirectoryEnabled { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }
    public string? LastImportedValuesJson { get; set; }
}

public class DirectoryRoleGrant : BaseEntity
{
    public Guid DirectoryConnectionId { get; set; }
    public string ExternalGroupId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid? UserRoleId { get; set; }
}

public class DirectorySyncRun : BaseEntity
{
    public Guid DirectoryConnectionId { get; set; }
    public Guid TriggeredByUserId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Mode { get; set; } = "Manual";
    public string Scope { get; set; } = string.Empty;
    public string Status { get; set; } = "Running";
    public int UsersFetched { get; set; }
    public int UsersCreated { get; set; }
    public int UsersUpdated { get; set; }
    public int UsersDeactivated { get; set; }
    public int UsersSkipped { get; set; }
    public int GroupsCreated { get; set; }
    public int GroupsUpdated { get; set; }
    public int GroupsDeactivated { get; set; }
    public int Conflicts { get; set; }
    public int Failures { get; set; }
}

public class DirectorySyncItem : BaseEntity
{
    public Guid DirectorySyncRunId { get; set; }
    public string ItemType { get; set; } = "User";
    public string ExternalDirectoryId { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string ProposedAction { get; set; } = string.Empty;
    public Guid? MatchedUserId { get; set; }
    public string? DisplayName { get; set; }
    public string? DetailsJson { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}
