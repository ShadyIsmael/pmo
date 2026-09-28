using NEXUS.SharedKernel;
using Microsoft.AspNetCore.Http;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.Modules.WorkItems.Application.DTOs;

namespace NEXUS.Modules.WorkItems.Application;

public interface IWorkItemService
{
    Task<Result<WorkItemDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<WorkItemAssigneeDto>>> GetAssigneesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<PagedResult<WorkItemDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, string? status = null, string? type = null, string? priority = null, string? search = null, Guid? assigneeId = null, Guid? reporterId = null, DateTime? dueDateFrom = null, DateTime? dueDateTo = null, bool? overdue = null, bool? unassigned = null, bool? highPriority = null, string? sortBy = null, CancellationToken cancellationToken = default);
    Task<WorkItemInsightsDto> GetInsightsAsync(Guid? projectId = null, string? status = null, string? type = null, string? priority = null, string? search = null, Guid? assigneeId = null, Guid? reporterId = null, DateTime? dueDateFrom = null, DateTime? dueDateTo = null, bool? overdue = null, bool? unassigned = null, bool? highPriority = null, CancellationToken cancellationToken = default);
    Task<ProjectWorkItemOverviewDto> GetProjectOverviewAsync(Guid projectId, int previewLimit = 3, CancellationToken cancellationToken = default);
    Task<Result<WorkItemDto>> CreateAsync(CreateWorkItemDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<WorkItemDto>> UpdateAsync(Guid id, UpdateWorkItemDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<WorkItemActivityDto>>> GetActivityAsync(Guid id, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<WorkItemCommentDto>>> GetCommentsAsync(Guid id, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<Result<WorkItemCommentDto>> AddCommentAsync(Guid id, CreateWorkItemCommentDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<WorkItemAttachmentDto>>> GetAttachmentsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<WorkItemAttachmentDto>>> AddAttachmentsAsync(Guid id, IReadOnlyList<IFormFile> files, string userId, CancellationToken cancellationToken = default);
    Task<Result<WorkItemAttachmentContent>> GetAttachmentContentAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAttachmentAsync(Guid id, Guid attachmentId, string userId, CancellationToken cancellationToken = default);
}
