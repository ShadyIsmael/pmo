using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Api.Account;

public sealed record AccountProfileDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    AuthenticationSource AuthenticationSource,
    DateTime? PasswordChangedAt,
    DateTime? LastLoginAt);

public sealed record AccountProfessionalProfileDto(
    string Name,
    string? EmployeeId,
    string? JobTitle,
    string? Email,
    string? Phone,
    string? AvatarUrl,
    string Type,
    string Status,
    string? EmploymentType,
    string? Seniority,
    DateTime? DateOfJoining,
    string? Location);

public sealed record AccountTenantMembershipDto(Guid TenantId, string Code, string Name, string Status);
public sealed record AccountOrganizationAccessDto(Guid OrganizationId, string Name, string? Code, bool CanReadSettings, bool CanManageSettings);
public sealed record AccountOrganizationalUnitMembershipDto(Guid OrganizationalUnitId, string Name, string Type, bool IsPrimary);
public sealed record AccountTeamMembershipDto(Guid TeamId, string Name, string? Code, DateTime EffectiveFrom);
public sealed record AccountProjectRoleDto(Guid Id, string Code, string NameEn, string NameAr, bool IsPrimary);
public sealed record AccountProjectMembershipDto(
    Guid ProjectId,
    string ProjectName,
    string ProjectCode,
    string ProjectStatus,
    string AssignmentSource,
    string Role,
    decimal AllocationPercentage,
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyList<AccountProjectRoleDto> FunctionalRoles);
public sealed record AccountSecuritySummaryDto(
    bool CanChangePassword,
    bool MustChangePassword,
    int ActiveSessionCount,
    Guid CurrentSessionId,
    DateTime? PasswordChangedAt,
    DateTime? LastLoginAt);
public sealed record AccountSessionDto(
    Guid Id,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string? IpAddress,
    string? UserAgent,
    bool IsCurrent);

public sealed record AccountResponseDto(
    AccountProfileDto Profile,
    AccountProfessionalProfileDto? ProfessionalProfile,
    AccountTenantMembershipDto? Tenant,
    IReadOnlyList<AccountOrganizationAccessDto> OrganizationAccess,
    IReadOnlyList<AccountOrganizationalUnitMembershipDto> OrganizationalUnits,
    IReadOnlyList<AccountTeamMembershipDto> Teams,
    IReadOnlyList<AccountProjectMembershipDto> Projects,
    AccountSecuritySummaryDto Security,
    IReadOnlyList<AccountSessionDto> Sessions);

public sealed record UpdateAccountProfileRequest(string FirstName, string LastName);
public sealed record ChangeAccountPasswordRequest(string CurrentPassword, string NewPassword);
public sealed record RevokeOtherSessionsResponse(int RevokedCount);
public sealed record ChangeAccountPasswordResponse(bool ReauthenticationRequired);
