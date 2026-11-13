using System;
using System.Threading;
using System.Threading.Tasks;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectLifecycleCatalogService
{
    Task<Result<ProjectPhaseCatalogEntry>> EnsurePreInitiationPhaseAsync(
        Guid tenantId,
        Guid? organizationId,
        Guid actorId,
        CancellationToken cancellationToken = default);
}