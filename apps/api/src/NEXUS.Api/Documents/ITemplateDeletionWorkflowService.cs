using NEXUS.Modules.Documents.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Documents;

public interface ITemplateDeletionWorkflowService
{
    Task<Result<TemplateDeletionRequestDto>> RequestAsync(
        CreateTemplateDeletionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>> GetTemplateHistoryAsync(
        Guid templateId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateDeletionPagedResult<TemplateDeletionRequestDto>>> GetInboxAsync(
        string status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateDeletionRequestDto>> ApproveAsync(
        Guid requestId,
        string? comments,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateDeletionRequestDto>> RejectAsync(
        Guid requestId,
        string? comments,
        CancellationToken cancellationToken = default);
}
