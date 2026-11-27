using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

[ApiController]
[Route("api/projects/{projectId:guid}/planned-payments")]
[Authorize]
public sealed class PlannedPaymentController(
    IPlannedPaymentService service,
    IManagementContextAccessor management) : ControllerBase
{
    [HttpGet("budget")]
    [Authorize(Policy = "PaymentObligation.View")]
    public async Task<IActionResult> GetBudget(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out _)) return ScopeFailure();
        return ToActionResult(await service.GetBudgetAsync(tenantId, projectId, cancellationToken));
    }

    [HttpGet]
    [Authorize(Policy = "PaymentObligation.View")]
    public async Task<IActionResult> GetPage(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetScope(projectId, out var tenantId, out _)) return ScopeFailure();
        return ToActionResult(await service.GetPageAsync(tenantId, projectId, page, pageSize, cancellationToken));
    }

    [HttpGet("{plannedPaymentId:guid}")]
    [Authorize(Policy = "PaymentObligation.View")]
    public async Task<IActionResult> GetById(Guid projectId, Guid plannedPaymentId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out _)) return ScopeFailure();
        return ToActionResult(await service.GetByIdAsync(tenantId, projectId, plannedPaymentId, cancellationToken));
    }

    [HttpGet("{plannedPaymentId:guid}/replacement-preview")]
    [Authorize(Policy = "PaymentObligation.Edit")]
    public async Task<IActionResult> GetReplacementPreview(
        Guid projectId,
        Guid plannedPaymentId,
        [FromQuery] decimal? newRemainingCommitment,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await service.GetReplacementPreviewAsync(
            tenantId, projectId, plannedPaymentId, newRemainingCommitment, actorId, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "PaymentObligation.Create")]
    public async Task<IActionResult> Register(
        Guid projectId, [FromBody] CreatePlannedPaymentDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        var result = await service.RegisterAsync(tenantId, projectId, actorId, ActorName(), dto, cancellationToken);
        if (result.IsFailure) return ErrorResult(result.Error);
        return CreatedAtAction(nameof(GetById), new { projectId, plannedPaymentId = result.Value!.Id }, result.Value);
    }

    [HttpPut("{plannedPaymentId:guid}")]
    [Authorize(Policy = "PaymentObligation.Edit")]
    public async Task<IActionResult> Update(
        Guid projectId, Guid plannedPaymentId, [FromBody] UpdatePlannedPaymentDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await service.UpdateAsync(
            tenantId, projectId, plannedPaymentId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{plannedPaymentId:guid}/replace")]
    [Authorize(Policy = "PaymentObligation.Edit")]
    public async Task<IActionResult> Replace(
        Guid projectId, Guid plannedPaymentId, [FromBody] ReplaceRemainingCommitmentDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await service.ReplaceRemainingCommitmentAsync(
            tenantId, projectId, plannedPaymentId, actorId, ActorName(), dto, cancellationToken));
    }

    private bool TryGetScope(Guid projectId, out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        return Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId)
            && tenantId != Guid.Empty
            && Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId)
            && actorId != Guid.Empty
            && management.Current?.TenantId == tenantId
            && management.Current.Scope.ProjectIds.Contains(projectId);
    }

    private IActionResult ScopeFailure() => Guid.TryParse(User.FindFirst("tenantId")?.Value, out _)
        ? NotFound(new { error = "Project not found." })
        : Unauthorized();

    private string? ActorName() => User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;

    private IActionResult ToActionResult<T>(Result<T> result) => result.IsFailure
        ? ErrorResult(result.Error)
        : Ok(result.Value);

    private IActionResult ErrorResult(Error? error) => error?.StatusCode switch
    {
        401 => Unauthorized(new { error = error.Message }),
        403 => Forbid(),
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message ?? "Request failed" })
    };
}
