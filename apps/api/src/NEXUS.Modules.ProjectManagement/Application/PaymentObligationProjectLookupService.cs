using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class PaymentObligationProjectLookupService(IProjectReferenceLookup projectLookup)
    : IPaymentObligationProjectLookupService
{
    public async Task<IReadOnlyList<PaymentObligationProjectLookupDto>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = await projectLookup.GetFinancialReferencesAsync(cancellationToken);
        return projects
            .Select(project => new PaymentObligationProjectLookupDto(
                project.Id,
                project.PublicId,
                project.Name,
                project.Code,
                project.Currency))
            .ToArray();
    }
}
