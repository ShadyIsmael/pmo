using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/project-lifecycle")]
[Authorize]
public sealed class ProjectLifecycleCatalogController : ControllerBase
{
    private readonly ProjectLifecycleCatalogService _catalog;

    public ProjectLifecycleCatalogController(ProjectLifecycleCatalogService catalog) => _catalog = catalog;

    [HttpGet("phases")]
    public async Task<IActionResult> GetPhases(
        [FromQuery] Guid? organizationId,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _catalog.GetPhasesAsync(organizationId, includeInactive, search, cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpGet("statuses")]
    public async Task<IActionResult> GetStatuses(
        [FromQuery] Guid? organizationId,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _catalog.GetStatusesAsync(organizationId, includeInactive, search, cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("phases")]
    public async Task<IActionResult> CreatePhase(
        [FromBody] ProjectLifecycleCatalogRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalog.CreatePhaseAsync(request, CurrentUserId(), cancellationToken);
        return result.IsFailure
            ? ToError(result.Error!)
            : CreatedAtAction(nameof(GetPhases), new { organizationId = result.Value!.OrganizationId }, result.Value);
    }

    [HttpPut("phases/{id:guid}")]
    public async Task<IActionResult> UpdatePhase(
        Guid id, [FromBody] ProjectLifecycleCatalogRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalog.UpdatePhaseAsync(id, request, CurrentUserId(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("phases/{id:guid}/activate")]
    public async Task<IActionResult> ActivatePhase(Guid id, [FromBody] ProjectLifecycleActionRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalog.SetPhaseActiveAsync(id, true, request.RowVersion, CurrentUserId(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("phases/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivatePhase(Guid id, [FromBody] ProjectLifecycleActionRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalog.SetPhaseActiveAsync(id, false, request.RowVersion, CurrentUserId(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("phases/{id:guid}/default")]
    public async Task<IActionResult> SetDefaultPhase(Guid id, [FromBody] ProjectLifecycleActionRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalog.SetPhaseDefaultAsync(id, request.RowVersion, CurrentUserId(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("statuses")]
    public async Task<IActionResult> CreateStatus(
        [FromBody] ProjectLifecycleCatalogRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalog.CreateStatusAsync(request, CurrentUserId(), cancellationToken);
        return result.IsFailure
            ? ToError(result.Error!)
            : CreatedAtAction(nameof(GetStatuses), new { organizationId = result.Value!.OrganizationId }, result.Value);
    }

    [HttpPut("statuses/{id:guid}")]
    public async Task<IActionResult> UpdateStatus(
        Guid id, [FromBody] ProjectLifecycleCatalogRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalog.UpdateStatusAsync(id, request, CurrentUserId(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("statuses/{id:guid}/activate")]
    public async Task<IActionResult> ActivateStatus(Guid id, [FromBody] ProjectLifecycleActionRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalog.SetStatusActiveAsync(id, true, request.RowVersion, CurrentUserId(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("statuses/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateStatus(Guid id, [FromBody] ProjectLifecycleActionRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalog.SetStatusActiveAsync(id, false, request.RowVersion, CurrentUserId(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("statuses/{id:guid}/default")]
    public async Task<IActionResult> SetDefaultStatus(Guid id, [FromBody] ProjectLifecycleActionRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalog.SetStatusDefaultAsync(id, request.RowVersion, CurrentUserId(), cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    [HttpGet("options")]
    [Authorize(Policy = ProjectLifecyclePermissions.OptionsLookup)]
    public async Task<IActionResult> GetOptions(
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? projectId,
        [FromQuery] string? purpose,
        [FromQuery] Guid? portfolioId,
        [FromQuery] Guid? programId,
        CancellationToken cancellationToken)
    {
        var isCreatePurpose = string.Equals(purpose, "create", StringComparison.OrdinalIgnoreCase);
        var result = await _catalog.GetOptionsAsync(
            organizationId, projectId, portfolioId, programId, isCreatePurpose, cancellationToken);
        return result.IsFailure ? ToError(result.Error!) : Ok(result.Value);
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    private IActionResult ToError(NEXUS.SharedKernel.Error error) =>
        StatusCode(error.StatusCode, error);
}
