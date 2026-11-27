namespace NEXUS.Api.Documents;

public interface ITemplateDeletionApproverResolver
{
    Task<IReadOnlyCollection<Guid>> GetEligibleApproverIdsAsync(
        Guid tenantId,
        Guid? ownerOrganizationId,
        Guid excludeUserId,
        CancellationToken cancellationToken);

    Task<bool> IsAuthorizedAdministratorAsync(
        Guid userId,
        Guid tenantId,
        Guid? ownerOrganizationId,
        string permission,
        CancellationToken cancellationToken);
}
