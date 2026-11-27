namespace NEXUS.Api.Documents;

public sealed record CreateTemplateDeletionRequest(Guid TemplateId, string Reason);

public sealed record DecideTemplateDeletionRequest(string Comments);

public sealed record TemplateDeletionRequestDto(
    Guid Id,
    Guid TemplateId,
    string TemplateName,
    Guid? OwnerOrganizationId,
    string OwnerScope,
    Guid RequesterId,
    string RequesterName,
    string Reason,
    string Status,
    DateTime RequestedAt,
    Guid? ApproverId,
    string? ApproverName,
    DateTime? DecidedAt,
    string? DecisionComments,
    string? DeletionType);

public sealed record TemplateDeletionPagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
