using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/projects/workflow-lookups")]
[Authorize(Policy = PolicyName)]
public sealed class PaymentObligationProjectLookupController(IPaymentObligationProjectLookupService service) : ControllerBase
{
    public const string PolicyName = "WorkflowLookup.PaymentObligation.Projects";

    /// <summary>Lists scoped projects that can be selected for a payment obligation.</summary>
    [HttpGet("payment-obligations")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentObligationProjectLookupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentObligationProjectLookupDto>>> GetPaymentObligationProjects(
        CancellationToken cancellationToken)
    {
        var projects = await service.GetProjectsAsync(cancellationToken);
        return Ok(projects);
    }
}
