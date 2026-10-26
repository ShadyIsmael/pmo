using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Application;

public interface IManagerHandoverService
{
    Task<Result<IReadOnlyList<ManagerHandoverManagerDto>>> GetEligibleProgramManagersAsync(Guid programId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerHandoverManagerDto>>> GetEligiblePortfolioManagersAsync(Guid portfolioId, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverCapabilitiesDto>> GetProgramCapabilitiesAsync(Guid programId, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverCapabilitiesDto>> GetPortfolioCapabilitiesAsync(Guid portfolioId, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> CreateProgramAsync(Guid programId, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> CreatePortfolioAsync(Guid portfolioId, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> CreateAdminProgramAsync(Guid programId, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> CreateAdminPortfolioAsync(Guid portfolioId, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> CreateInitialProgramAssignmentAsync(Guid programId, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> CreateInitialPortfolioAssignmentAsync(Guid portfolioId, CreateManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetProgramRequestsAsync(Guid programId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPortfolioRequestsAsync(Guid portfolioId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPendingForMeAsync(Guid userId, CancellationToken ct = default);
    Task<Result<ApprovalReviewDto>> GetProgramApprovalReviewAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<Result<ApprovalReviewDto>> GetPortfolioApprovalReviewAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPendingProgramForMeAsync(Guid programId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerHandoverRequestDto>>> GetPendingPortfolioForMeAsync(Guid portfolioId, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> AcceptProgramAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> AcceptPortfolioAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> RejectProgramAsync(Guid requestId, RejectManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> RejectPortfolioAsync(Guid requestId, RejectManagerHandoverRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> CancelProgramAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<Result<ManagerHandoverRequestDto>> CancelPortfolioAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerAssignmentDto>>> GetProgramHistoryAsync(Guid programId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerAssignmentDto>>> GetPortfolioHistoryAsync(Guid portfolioId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerSnapshotDto>>> GetProgramSnapshotsAsync(Guid programId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ManagerSnapshotDto>>> GetPortfolioSnapshotsAsync(Guid portfolioId, Guid userId, CancellationToken ct = default);
}
