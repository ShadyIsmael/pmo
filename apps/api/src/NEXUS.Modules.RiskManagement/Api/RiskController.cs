using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NEXUS.Modules.RiskManagement.Application;
using NEXUS.Modules.RiskManagement.Application.DTOs;
using NEXUS.Modules.RiskManagement.Domain;

namespace NEXUS.Modules.RiskManagement.Api;

[ApiController]
[Route("api/risks")]
[Authorize]
public sealed class RiskController(IRiskService riskService) : ControllerBase
{
    [Authorize(Policy = "Risk.Create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRiskDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await riskService.CreateAsync(dto, actorId, cancellationToken), nameof(GetById));
    }

    [Authorize(Policy = "Risk.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) => ToActionResult(await riskService.GetByIdAsync(id, cancellationToken));

    [Authorize(Policy = "Risk.Edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRiskDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await riskService.UpdateAsync(id, dto, actorId, cancellationToken));
    }

    [Authorize(Policy = "Risk.Mitigate")]
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateRiskStatusDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await riskService.UpdateStatusAsync(id, dto, actorId, cancellationToken));
    }

    [Authorize(Policy = "Risk.View")]
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, [FromQuery] RiskStatus? status = null, [FromQuery] Guid? ownerResourceId = null, CancellationToken cancellationToken = default) => Ok(await riskService.GetPagedAsync(page, pageSize, projectId, status, ownerResourceId, cancellationToken));

    [Authorize(Policy = "Risk.View")]
    [HttpGet("{riskId:guid}/impacts")]
    public async Task<IActionResult> GetImpacts(Guid riskId, CancellationToken cancellationToken) => ToActionResult(await riskService.GetImpactsAsync(riskId, cancellationToken));

    [Authorize(Policy = "Risk.Edit")]
    [HttpPost("{riskId:guid}/impacts")]
    public async Task<IActionResult> CreateImpact(Guid riskId, [FromBody] CreateRiskImpactAssessmentDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await riskService.CreateImpactAsync(riskId, dto, actorId, cancellationToken));
    }

    [Authorize(Policy = "Risk.Edit")]
    [HttpPut("{riskId:guid}/impacts/{impactId:guid}")]
    public async Task<IActionResult> UpdateImpact(Guid riskId, Guid impactId, [FromBody] UpdateRiskImpactAssessmentDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await riskService.UpdateImpactAsync(riskId, impactId, dto, actorId, cancellationToken));
    }

    [Authorize(Policy = "Risk.Delete")]
    [HttpDelete("{riskId:guid}/impacts/{impactId:guid}")]
    public async Task<IActionResult> DeleteImpact(Guid riskId, Guid impactId, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await riskService.DeleteImpactAsync(riskId, impactId, actorId, cancellationToken));
    }

    [Authorize(Policy = "Risk.View")]
    [HttpGet("/api/projects/{projectId:guid}/risks")]
    public async Task<IActionResult> GetByProject(Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, [FromQuery] Guid? ownerResourceId = null, CancellationToken cancellationToken = default) => Ok(await riskService.GetPagedAsync(page, pageSize, projectId, ownerResourceId: ownerResourceId, cancellationToken: cancellationToken));

    [Authorize(Policy = "Risk.View")]
    [HttpGet("{riskId:guid}/actions")]
    public async Task<IActionResult> GetActions(Guid riskId, CancellationToken cancellationToken) => ToActionResult(await riskService.GetActionsAsync(riskId, cancellationToken));

    [Authorize(Policy = "Risk.View")]
    [HttpGet("/api/risk-action-follow-ups/{actionPublicId:guid}/target")]
    public async Task<IActionResult> GetActionFollowUpTarget(Guid actionPublicId, CancellationToken cancellationToken) =>
        ToActionResult(await riskService.GetActionFollowUpTargetAsync(actionPublicId, cancellationToken));

    [Authorize(Policy = "Risk.Edit")]
    [HttpPost("{riskId:guid}/actions")]
    public async Task<IActionResult> CreateAction(Guid riskId, [FromBody] CreateRiskActionDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await riskService.CreateActionAsync(riskId, dto, actorId, cancellationToken));
    }

    [Authorize(Policy = "Risk.Edit")]
    [HttpPut("{riskId:guid}/actions/{actionId:guid}")]
    public async Task<IActionResult> UpdateAction(Guid riskId, Guid actionId, [FromBody] UpdateRiskActionDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await riskService.UpdateActionAsync(riskId, actionId, dto, actorId, cancellationToken));
    }

    [Authorize(Policy = "Risk.Delete")]
    [HttpDelete("{riskId:guid}/actions/{actionId:guid}")]
    public async Task<IActionResult> DeleteAction(Guid riskId, Guid actionId, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await riskService.DeleteActionAsync(riskId, actionId, actorId, cancellationToken));
    }

    [Authorize(Policy = "Risk.View")]
    [HttpGet("{riskId:guid}/actions/{actionId:guid}/updates")]
    public async Task<IActionResult> GetActionUpdates(Guid riskId, Guid actionId, CancellationToken cancellationToken) => ToActionResult(await riskService.GetActionUpdatesAsync(riskId, actionId, cancellationToken));

    [Authorize(Policy = "Risk.Edit")]
    [HttpPost("{riskId:guid}/actions/{actionId:guid}/updates")]
    public async Task<IActionResult> AddActionUpdate(Guid riskId, Guid actionId, [FromBody] CreateRiskActionUpdateDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        return ToActionResult(await riskService.AddActionUpdateAsync(riskId, actionId, dto, actorId, cancellationToken));
    }

    private bool TryGetActorId(out string actorId)
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) && parsed != Guid.Empty)
        {
            actorId = parsed.ToString();
            return true;
        }

        actorId = string.Empty;
        return false;
    }
    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result, string? createdAction = null) => result.IsFailure ? ErrorResult(result.Error) : createdAction is null ? Ok(result.Value) : CreatedAtAction(createdAction, new { id = result.Value is RiskDto risk ? risk.Id : Guid.Empty }, result.Value);
    private IActionResult ToActionResult(NEXUS.SharedKernel.Result result) => result.IsFailure ? ErrorResult(result.Error) : NoContent();
    private IActionResult ErrorResult(NEXUS.SharedKernel.Error? error) => error?.StatusCode switch { 404 => NotFound(new { error = error.Message }), 409 => Conflict(new { error = error.Message }), _ => BadRequest(new { error = error?.Message ?? "Request failed" }) };
}
