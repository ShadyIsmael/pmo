using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

public interface IPlannedPaymentService
{
    Task<Result<PlannedPaymentPageDto>> GetPageAsync(
        Guid tenantId, Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<PlannedPaymentDto>> GetByIdAsync(
        Guid tenantId, Guid projectId, Guid plannedPaymentId, CancellationToken cancellationToken = default);

    Task<Result<PlannedPaymentBudgetDto>> GetBudgetAsync(
        Guid tenantId, Guid projectId, CancellationToken cancellationToken = default);

    Task<Result<PlannedPaymentDto>> RegisterAsync(
        Guid tenantId,
        Guid projectId,
        Guid actorId,
        string? actorName,
        CreatePlannedPaymentDto dto,
        CancellationToken cancellationToken = default);

    Task<Result<PlannedPaymentDto>> UpdateAsync(
        Guid tenantId,
        Guid projectId,
        Guid plannedPaymentId,
        Guid actorId,
        string? actorName,
        UpdatePlannedPaymentDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lock hook used by the first linked Payment Request. It is additive and
    /// must be called inside the caller's existing transaction.
    /// </summary>
    Task<Result> ApplyPaymentRequestAsync(
        Guid tenantId,
        Guid projectId,
        Guid installmentId,
        Guid requestId,
        decimal requestAmount,
        Guid actorId,
        string? actorName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the unpaid remaining commitment of a financially Locked Planned Payment.
    /// Historical settlements are preserved; the old plan transitions to Replaced and its
    /// unpaid reservation is released. A new Planned Payment is created for the remaining
    /// amount with installments mirroring the old plan's outstanding balances. No Lifetime
    /// Budget is double-consumed.
    /// </summary>
    Task<Result<PlannedPaymentDto>> ReplaceRemainingCommitmentAsync(
        Guid tenantId,
        Guid projectId,
        Guid plannedPaymentId,
        Guid actorId,
        string? actorName,
        ReplaceRemainingCommitmentDto dto,
        CancellationToken cancellationToken = default);

    Task<Result<PlannedPaymentReplacementPreviewDto>> GetReplacementPreviewAsync(
        Guid tenantId,
        Guid projectId,
        Guid plannedPaymentId,
        decimal? newRemainingCommitment,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
