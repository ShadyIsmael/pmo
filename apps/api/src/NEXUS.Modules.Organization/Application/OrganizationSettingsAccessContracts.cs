namespace NEXUS.Modules.Organization.Application;

public sealed record OrganizationSettingsGrantTargetUserDto(Guid Id, string FullName, string Email);

public sealed record OrganizationSettingsGrantDto(
    Guid Id,
    Guid OrganizationId,
    Guid UserId,
    string UserName,
    string UserEmail,
    bool CanRead,
    bool CanManage,
    string Version);

public sealed class UpdateOrganizationSettingsGrantRequest
{
    public Guid UserId { get; init; }
    public bool CanRead { get; init; }
    public bool CanManage { get; init; }
    public string? ExpectedVersion { get; init; }
}

public sealed class RevokeOrganizationSettingsGrantRequest
{
    public string? ExpectedVersion { get; init; }
}

public enum OrganizationSettingsGrantOperationCode
{
    Saved,
    Revoked,
    NotFound,
    TenantContextMissing,
    ConcurrencyConflict,
    ValidationFailed,
    PersistenceUnavailable
}

public sealed record OrganizationSettingsGrantOperationResult(
    OrganizationSettingsGrantOperationCode Code,
    OrganizationSettingsGrantDto? Grant = null,
    string? Error = null);

public interface IOrganizationSettingsAccessService
{
    Task<IReadOnlyList<Guid>> GetReadableOrganizationIdsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetManageableOrganizationIdsAsync(CancellationToken cancellationToken = default);
    Task<bool> HasAccessAsync(Guid organizationId, bool manage, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationSettingsGrantDto>> GetGrantsAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationSettingsGrantTargetUserDto>> GetGrantTargetsAsync(CancellationToken cancellationToken = default);
    Task<OrganizationSettingsGrantOperationResult> SaveGrantAsync(
        Guid organizationId, UpdateOrganizationSettingsGrantRequest request, CancellationToken cancellationToken = default);
    Task<OrganizationSettingsGrantOperationResult> RevokeGrantAsync(
        Guid organizationId, Guid userId, RevokeOrganizationSettingsGrantRequest request, CancellationToken cancellationToken = default);
}
