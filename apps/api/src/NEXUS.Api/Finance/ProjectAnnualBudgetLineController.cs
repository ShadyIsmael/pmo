using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Api.Finance;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

[ApiController]
[Route("api/projects/{projectId:guid}/annual-budgets/{budgetId:guid}/lines")]
[Authorize]
public sealed class ProjectAnnualBudgetLineController(
    ProjectAnnualBudgetLineWorkflow workflow,
    IManagementContextAccessor management) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "ProjectAnnualBudget.View")]
    public async Task<IActionResult> Get(Guid projectId, Guid budgetId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetPageAsync(tenantId, projectId, budgetId, actorId, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "ProjectAnnualBudget.EditOrManageLines")]
    public async Task<IActionResult> Create(Guid projectId, Guid budgetId,
        [FromBody] CreateProjectAnnualBudgetLineDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.CreateAsync(tenantId, projectId, budgetId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPut("{lineId:guid}")]
    [Authorize(Policy = "ProjectAnnualBudget.EditOrManageLines")]
    public async Task<IActionResult> Update(Guid projectId, Guid budgetId, Guid lineId,
        [FromBody] UpdateProjectAnnualBudgetLineDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.UpdateAsync(tenantId, projectId, budgetId, lineId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpDelete("{lineId:guid}")]
    [Authorize(Policy = "ProjectAnnualBudget.EditOrManageLines")]
    public async Task<IActionResult> Delete(Guid projectId, Guid budgetId, Guid lineId,
        [FromBody] DeleteProjectAnnualBudgetLineDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        var result = await workflow.DeleteAsync(tenantId, projectId, budgetId, lineId, actorId, ActorName(), dto, cancellationToken);
        return result.IsFailure ? ErrorResult(result.Error) : NoContent();
    }

    private bool TryGetScope(Guid projectId, out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var current = management.Current;
        return Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId) && tenantId != Guid.Empty
            && Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId) && actorId != Guid.Empty
            && current is not null && current.TenantId == tenantId && current.UserId == actorId
            && current.Scope.ProjectIds.Contains(projectId);
    }

    private IActionResult ScopeFailure() => Guid.TryParse(User.FindFirst("tenantId")?.Value, out _)
        ? NotFound(new { error = "Project not found." }) : Unauthorized();
    private string? ActorName() => User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;
    private IActionResult ToActionResult<T>(Result<T> result) => result.IsFailure ? ErrorResult(result.Error) : Ok(result.Value);
    private IActionResult ErrorResult(Error? error) => error?.StatusCode switch
    {
        401 => Unauthorized(new { error = error.Message }),
        403 => Forbid(),
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message ?? "Request failed" })
    };
}

