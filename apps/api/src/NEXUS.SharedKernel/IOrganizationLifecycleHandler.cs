using System;
using System.Threading;
using System.Threading.Tasks;

namespace NEXUS.SharedKernel;

public interface IOrganizationLifecycleHandler
{
    Task<Result> HandleOrganizationCreatedAsync(
        Guid tenantId,
        Guid organizationId,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
