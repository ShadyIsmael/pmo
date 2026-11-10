using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

[ApiController]
[Route("api/projects/{projectId:guid}/annual-budgets/{budgetId:guid}/approval-configuration")]
[Authorize]
[Authorize(Policy = "ProjectAnnualBudget.View")]
public sealed class ProjectAnnualBudgetDraftApprovalConfigurationController(
    ProjectAnnualBudgetDraftApprovalConfigurationService configuration,
    IManagementContextAccessor management) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, Guid budgetId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await configuration.GetAsync(tenantId, projectId, budgetId, actorId, cancellationToken));
    }

    [HttpPut]
    [Authorize(Policy = "ProjectAnnualBudget.EditDraft")]
    [Authorize(Policy = "ProjectAnnualBudget.ConfigureDraftApproval")]
    public async Task<IActionResult> Save(
        Guid projectId,
        Guid budgetId,
        [FromBody] SaveProjectAnnualBudgetDraftApprovalConfigurationDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await configuration.SaveAsync(
            tenantId, projectId, budgetId, actorId, ActorName(), dto, cancellationToken));
    }

    private bool TryGetScope(Guid projectId, out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var current = management.Current;
        return current is not null
            && Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId)
            && tenantId != Guid.Empty
            && Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId)
            && actorId != Guid.Empty
            && current.TenantId == tenantId
            && current.UserId == actorId
            && current.Scope.ProjectIds.Contains(projectId);
    }

    private IActionResult ScopeFailure() => Guid.TryParse(User.FindFirst("tenantId")?.Value, out _)
        ? NotFound(new { error = "Project not found." })
        : Unauthorized();

    private string? ActorName() => User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;

    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result) => result.IsFailure
        ? StatusCode(result.Error?.StatusCode ?? StatusCodes.Status400BadRequest,
            new { error = result.Error?.Message ?? "Request failed." })
        : Ok(result.Value);
}
