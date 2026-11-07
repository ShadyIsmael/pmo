using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Application;

public interface IProgramService
{
    Task<Result<ProgramDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ProgramDto>> GetPagedAsync(int page, int pageSize, string? search, string? status, Guid? portfolioId, CancellationToken cancellationToken = default);
    Task<PagedResult<ProgramLookupDto>> GetProjectLookupAsync(int page, int pageSize, string? search, string? status, Guid? portfolioId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProgramPortfolioLookupDto>>> GetEligiblePortfoliosForCreationAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProgramPortfolioLookupDto>>> GetEligiblePortfoliosForEditingAsync(CancellationToken cancellationToken = default);
    Task<Result<ProgramDto>> CreateAsync(SaveProgramDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProgramDto>> UpdateAsync(Guid id, SaveProgramDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> ArchiveAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result> ReactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
