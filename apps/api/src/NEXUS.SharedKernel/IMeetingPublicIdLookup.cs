namespace NEXUS.SharedKernel;

/// <summary>Resolves a Meeting database/public identifier to its safe public identifier within a tenant.</summary>
public interface IMeetingPublicIdLookup
{
    Task<Guid?> FindPublicIdAsync(Guid meetingIdOrPublicId, Guid tenantId, CancellationToken cancellationToken = default);
}
