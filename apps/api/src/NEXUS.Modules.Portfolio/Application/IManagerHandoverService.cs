using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Application;

public interface IManagerHandoverService
{
    Task<Result<ManagerHandoverRequestDto>> CreateProgramAsync(Guid programId, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> CreatePortfolioAsync(Guid portfolioId, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetProgramRequestsAsync(Guid programId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPortfolioRequestsAsync(Guid portfolioId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPendingForMeAsync(Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> AcceptProgramAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> AcceptPortfolioAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> RejectProgramAsync(Guid requestId, RejectManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> RejectPortfolioAsync(Guid requestId, RejectManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> CancelProgramAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> CancelPortfolioAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerAssignmentDto>>> GetProgramHistoryAsync(Guid programId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerAssignmentDto>>> GetPortfolioHistoryAsync(Guid portfolioId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerSnapshotDto>>> GetProgramSnapshotsAsync(Guid programId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerSnapshotDto>>> GetPortfolioSnapshotsAsync(Guid portfolioId, CancellationToken ct = default);
}
