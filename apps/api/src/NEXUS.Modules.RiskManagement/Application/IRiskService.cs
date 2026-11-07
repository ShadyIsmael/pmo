using NEXUS.SharedKernel;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.RiskManagement.Application.DTOs;

namespace NEXUS.Modules.RiskManagement.Application;

public interface IRiskService
{
    Task<Result<RiskDto>> CreateAsync(CreateRiskDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<RiskDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<RiskDto>> UpdateAsync(Guid id, UpdateRiskDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<RiskDto>> UpdateStatusAsync(Guid id, UpdateRiskStatusDto dto, string userId, CancellationToken cancellationToken = default);
    Task<PagedResult<RiskDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, RiskStatus? status = null, Guid? ownerResourceId = null, CancellationToken cancellationToken = default);
    Task<ProjectRiskOverviewDto> GetProjectOverviewAsync(Guid projectId, int previewLimit = 3, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RiskImpactAssessmentDto>>> GetImpactsAsync(Guid riskId, CancellationToken cancellationToken = default);
    Task<Result<RiskImpactAssessmentDto>> CreateImpactAsync(Guid riskId, CreateRiskImpactAssessmentDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<RiskImpactAssessmentDto>> UpdateImpactAsync(Guid riskId, Guid impactId, UpdateRiskImpactAssessmentDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteImpactAsync(Guid riskId, Guid impactId, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RiskActionDto>>> GetActionsAsync(Guid riskId, CancellationToken cancellationToken = default);
    Task<Result<RiskActionFollowUpTargetDto>> GetActionFollowUpTargetAsync(Guid actionPublicId, CancellationToken cancellationToken = default);
    Task<Result<RiskActionDto>> CreateActionAsync(Guid riskId, CreateRiskActionDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<RiskActionDto>> UpdateActionAsync(Guid riskId, Guid actionId, UpdateRiskActionDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteActionAsync(Guid riskId, Guid actionId, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RiskActionUpdateDto>>> GetActionUpdatesAsync(Guid riskId, Guid actionId, CancellationToken cancellationToken = default);
    Task<Result<RiskActionUpdateDto>> AddActionUpdateAsync(Guid riskId, Guid actionId, CreateRiskActionUpdateDto dto, string userId, CancellationToken cancellationToken = default);
}
