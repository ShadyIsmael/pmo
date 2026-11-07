using NEXUS.Modules.ProjectManagement.Application.DTOs;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IPaymentObligationProjectLookupService
{
    Task<IReadOnlyList<PaymentObligationProjectLookupDto>> GetProjectsAsync(CancellationToken cancellationToken = default);
}
