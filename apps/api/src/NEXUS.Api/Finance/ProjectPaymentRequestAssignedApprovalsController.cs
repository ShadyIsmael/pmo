using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Finance;

[ApiController]
[Route("api/my-work/payment-request-approvals")]
[Authorize]
public sealed class ProjectPaymentRequestAssignedApprovalsController(
    ProjectPaymentRequestWorkflow workflow) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "ProjectPaymentRequest.Approve")]
    public async Task<IActionResult> GetMyActionableApprovals(CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var tenantId, out var actorId)) return Unauthorized();
        var result = await workflow.GetMyPendingApprovalsAsync(tenantId, actorId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetIdentity(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        return Guid.TryParse(User.FindFirstValue("tenantId"), out tenantId) && tenantId != Guid.Empty
            && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId) && actorId != Guid.Empty;
    }

    private IActionResult ToActionResult<T>(Result<T> result) => result.IsFailure
        ? result.Error?.StatusCode switch
        {
            401 => Unauthorized(new { error = result.Error.Message }),
            403 => Forbid(),
            404 => NotFound(new { error = result.Error.Message }),
            409 => Conflict(new { error = result.Error.Message }),
            _ => BadRequest(new { error = result.Error?.Message ?? "Request failed." })
        }
        : Ok(result.Value is System.Collections.Generic.IReadOnlyList<NEXUS.Modules.CostManagement.Application.DTOs.PendingPaymentRequestApprovalDto> items
            ? new { items }
            : result.Value);
}
