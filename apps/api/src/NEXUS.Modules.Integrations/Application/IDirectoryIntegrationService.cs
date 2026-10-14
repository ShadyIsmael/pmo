namespace NEXUS.Modules.Integrations.Application;

public interface IDirectoryIntegrationService
{
    Task<IReadOnlyList<DirectoryReferenceOptionDto>> GetOrganizationalUnitsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectoryReferenceOptionDto>> GetCalendarsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectoryConnectionDto>> GetConnectionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DirectoryConnectionDto> SaveConnectionAsync(Guid tenantId, Guid actorId, Guid? connectionId, SaveDirectoryConnectionRequest request, CancellationToken cancellationToken = default);
    Task<DirectoryProviderTestResult> TestConnectionAsync(Guid tenantId, Guid actorId, Guid connectionId, CancellationToken cancellationToken = default);
    Task<DirectorySyncScopeDto> GetScopeAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default);
    Task<DirectorySyncScopeDto> SaveScopeAsync(Guid tenantId, Guid actorId, Guid connectionId, SaveDirectorySyncScopeRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectoryFieldMappingDto>> GetFieldMappingsAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectoryFieldMappingDto>> SaveFieldMappingsAsync(Guid tenantId, Guid actorId, Guid connectionId, SaveDirectoryFieldMappingsRequest request, CancellationToken cancellationToken = default);
    Task<DirectoryProviderPage<DirectoryGroupDto>> GetDirectoryGroupsAsync(Guid tenantId, Guid connectionId, DirectoryProviderQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectoryGroupMappingDto>> GetGroupMappingsAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectoryGroupMappingDto>> SaveGroupMappingsAsync(Guid tenantId, Guid actorId, Guid connectionId, SaveDirectoryGroupMappingsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectoryOrganizationMappingDto>> GetOrganizationMappingsAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectoryOrganizationMappingDto>> SaveOrganizationMappingsAsync(Guid tenantId, Guid actorId, Guid connectionId, SaveDirectoryOrganizationMappingsRequest request, CancellationToken cancellationToken = default);
    Task<DirectoryProvisioningPolicyDto> GetProvisioningPolicyAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default);
    Task<DirectoryProvisioningPolicyDto> SaveProvisioningPolicyAsync(Guid tenantId, Guid actorId, Guid connectionId, SaveDirectoryProvisioningPolicyRequest request, CancellationToken cancellationToken = default);
    Task<DirectoryPreviewResultDto> PreviewAsync(Guid tenantId, Guid connectionId, string itemType, DirectoryProviderQuery query, CancellationToken cancellationToken = default);
    Task<DirectorySyncExecutionResultDto> ExecuteSyncAsync(Guid tenantId, Guid actorId, Guid connectionId, ExecuteDirectorySyncRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectorySyncRunDto>> GetHistoryAsync(Guid tenantId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<DirectorySyncRunDetailDto> GetHistoryDetailAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken = default);
}
