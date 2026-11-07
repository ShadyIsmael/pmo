using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

public interface IPaymentObligationService
{
    Task<Result<PaymentObligationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PaymentObligationDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, Guid? milestoneId = null, bool? dueOnly = null, CancellationToken cancellationToken = default);
    Task<Result<PaymentObligationDto>> CreateAsync(CreatePaymentObligationDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<PaymentObligationDto>> UpdateAsync(Guid id, UpdatePaymentObligationDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);
    Task<Result<PaymentSettlementDto>> RegisterSettlementAsync(Guid paymentObligationId, RecordPaymentSettlementDto dto, string userId, CancellationToken cancellationToken = default);
    Task<PagedResult<PaymentSettlementDto>> GetSettlementsAsync(Guid paymentObligationId, int page, int pageSize, CancellationToken cancellationToken = default);
}
