using NEXUS.SharedKernel;
using NEXUS.Modules.Approvals.Domain;

namespace NEXUS.Modules.Approvals.Application;

public interface IApprovalService
{
    /// <summary>
    /// Loads one approval request for a caller holding <c>ApprovalRequest.View</c>, and only
    /// when the caller is its requester or a current/recorded approver. The record must belong
    /// to the caller's authenticated tenant.
    /// </summary>
    Task<Result<ApprovalRequest>> GetByIdAsync(Guid id, Guid callerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists approval requests visible to the caller (requester or approver), gated by the
    /// <c>ApprovalRequest.View</c> permission. An optional <paramref name="userId"/> filter
    /// is intersected with the caller's own visibility.
    /// </summary>
    Task<Result<PagedResult<ApprovalRequest>>> GetPagedAsync(int page, int pageSize, Guid callerId, string? status = null, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a draft approval request to its workflow. Caller must be the requester.
    /// </summary>
    Task<Result<ApprovalRequest>> SubmitAsync(Guid id, Guid callerId, string? comments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a submitted approval request. Caller must be the current assignee.
    /// </summary>
    Task<Result<ApprovalRequest>> ApproveAsync(Guid id, Guid callerId, string? comments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a submitted approval request. Caller must be the current assignee. A comment is required.
    /// </summary>
    Task<Result<ApprovalRequest>> RejectAsync(Guid id, Guid callerId, string comments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delegates a pending approval request to another user. Caller must be the current assignee or requester.
    /// </summary>
    Task<Result<ApprovalRequest>> DelegateAsync(Guid id, Guid callerId, Guid toUserId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a draft or submitted approval request. Caller must be the requester.
    /// </summary>
    Task<Result<ApprovalRequest>> CancelAsync(Guid id, Guid callerId, CancellationToken cancellationToken = default);
}
