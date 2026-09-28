using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanVersionService
{
    Task<Result<PlanVersionDto>> GetActiveWorkingVersionAsync(Guid planId, CancellationToken cancellationToken = default);

    Task<Result<PlanVersionDto>> CreateWorkingFromCurrentBaselineAsync(
        Guid planId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<Result> DiscardWorkingVersionAsync(
        Guid planId,
        byte[] rowVersion,
        string userId,
        CancellationToken cancellationToken = default);

    Task<Result<PlanVersionDto>> CreateInitialBaselineAsync(
        Guid planId,
        byte[] rowVersion,
        string userId,
        CancellationToken cancellationToken = default);

    Task<Result<PlanVersionDto>> CaptureBaselineAsync(
        Guid planId,
        byte[] rowVersion,
        string userId,
        CancellationToken cancellationToken = default);

    Task<Result<PlanVersionDto>> PromoteWorkingToBaselineAsync(
        Guid planId,
        byte[] rowVersion,
        string userId,
        CancellationToken cancellationToken = default);
}
