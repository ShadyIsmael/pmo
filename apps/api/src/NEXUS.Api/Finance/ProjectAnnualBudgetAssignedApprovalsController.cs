using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Finance;

[ApiController]
[Route("api/my-work/annual-budget-approvals")]
[Authorize]
public sealed class ProjectAnnualBudgetAssignedApprovalsController(
    ProjectAnnualBudgetWorkflow workflow) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "ProjectAnnualBudget.Approve")]
    public async Task<IActionResult> GetMyActionableApprovals(CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var tenantId, out var actorId)) return Unauthorized();

        var result = await workflow.GetMyActionableApprovalsAsync(tenantId, actorId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("history")]
    [Authorize(Policy = "ProjectAnnualBudget.View")]
    public async Task<IActionResult> GetMyAssignedApprovalHistory(CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var tenantId, out var actorId)) return Unauthorized();
        return ToActionResult(await workflow.GetMyAssignedApprovalHistoryAsync(tenantId, actorId, cancellationToken));
    }

    [HttpGet("{requestId:guid}")]
    [Authorize(Policy = "ProjectAnnualBudget.View")]
    public async Task<IActionResult> GetMyAssignedApprovalDetail(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var tenantId, out var actorId)) return Unauthorized();
        return ToActionResult(await workflow.GetMyAssignedApprovalDetailAsync(
            tenantId, actorId, requestId, cancellationToken));
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
        : Ok(result.Value);
}
