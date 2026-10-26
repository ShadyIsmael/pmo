namespace NEXUS.Modules.Identity.Application;

public interface IUserSessionValidator
{
    Task<bool> IsActiveAsync(Guid sessionId, Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}
