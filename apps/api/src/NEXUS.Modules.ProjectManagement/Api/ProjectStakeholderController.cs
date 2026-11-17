using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/stakeholders")]
[Authorize]
public sealed class ProjectStakeholderController : ControllerBase
{
    private readonly IProjectStakeholderService _service;
    private readonly IManagementContextAccessor? _managementContext;

    public ProjectStakeholderController(IProjectStakeholderService service, IManagementContextAccessor? managementContext = null)
    {
        _service = service;
        _managementContext = managementContext;
    }

    [Authorize(Policy = "Stakeholders.Read")]
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByProjectAsync(projectId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [Authorize(Policy = "Stakeholders.Manage")]
    [HttpPost("internal")]
    public async Task<IActionResult> AddInternal(Guid projectId, [FromBody] AddInternalProjectStakeholderDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var actorId);
        if (denied is not null) return denied;
        var result = await _service.AddInternalAsync(projectId, dto, actorId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : CreatedAtAction(nameof(Get), new { projectId }, result.Value);
    }

    [Authorize(Policy = "Stakeholders.Manage")]
    [HttpPost("contact")]
    public async Task<IActionResult> AddContact(Guid projectId, [FromBody] AddContactProjectStakeholderDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var actorId);
        if (denied is not null) return denied;
        var result = await _service.AddContactAsync(projectId, dto, actorId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : CreatedAtAction(nameof(Get), new { projectId }, result.Value);
    }

    [Authorize(Policy = "Stakeholders.Manage")]
    [HttpPut("{stakeholderId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid stakeholderId, [FromBody] UpdateProjectStakeholderDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var actorId);
        if (denied is not null) return denied;
        var result = await _service.UpdateAsync(projectId, stakeholderId, dto, actorId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [Authorize(Policy = "Stakeholders.Manage")]
    [HttpPost("{stakeholderId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid projectId, Guid stakeholderId, [FromBody] RevokeProjectStakeholderDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var actorId);
        if (denied is not null) return denied;
        var result = await _service.RevokeAsync(projectId, stakeholderId, dto, actorId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [Authorize(Policy = "Stakeholders.Manage")]
    [HttpDelete("{stakeholderId:guid}")]
    public async Task<IActionResult> Remove(Guid projectId, Guid stakeholderId, [FromQuery] string? rowVersion, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var actorId);
        if (denied is not null) return denied;
        byte[]? version;
        try
        {
            version = string.IsNullOrWhiteSpace(rowVersion) ? null : Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            return BadRequest(new { error = "RowVersion must be valid Base64" });
        }
        var result = await _service.RemoveAsync(projectId, stakeholderId, version, actorId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : NoContent();
    }

    private static IActionResult Map(NEXUS.SharedKernel.Error error) => error.StatusCode switch
    {
        404 => new NotFoundObjectResult(new { error = error.Message }),
        409 => new ConflictObjectResult(new { error = error.Message }),
        403 => new ObjectResult(new { error = error.Message }) { StatusCode = 403 },
        _ => new BadRequestObjectResult(new { error = error.Message })
    };
}
