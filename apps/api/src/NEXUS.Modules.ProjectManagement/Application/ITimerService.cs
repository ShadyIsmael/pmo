using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface ITimerService
{
    Task<Result<TimerSessionDto?>> GetCurrentAsync(Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TimerSessionDto>> GetByIdAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TimerSessionDto>> StartAsync(Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TimerSessionDto>> BreakAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TimerSessionDto>> PauseAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TimerSessionDto>> ResumeAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TimerSessionDto>> HeartbeatAsync(Guid id, TimerHeartbeatDto dto, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TimerSessionDto>> FinishAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TimeEntryDto>>> FinalizeAsync(Guid id, FinalizeTimerSessionDto dto, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TimerSessionDto>> CancelAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<EligibleProjectDto>>> GetEligibleProjectsForFinalizeAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
}
