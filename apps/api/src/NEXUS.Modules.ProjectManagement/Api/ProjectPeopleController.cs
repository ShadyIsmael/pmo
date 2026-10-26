using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/people")]
[Authorize]
public sealed class ProjectPeopleController : ControllerBase
{
    private readonly IProjectPeopleService _service;
    private readonly IAuthorizationService _authorization;

    public ProjectPeopleController(IProjectPeopleService service, IAuthorizationService authorization)
    {
        _service = service;
        _authorization = authorization;
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(projectId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Add(Guid projectId, [FromBody] CreateProjectPersonDto dto, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(dto.RelationshipType)) return Forbid();
        var result = await _service.AddAsync(projectId, dto, ActorId(), cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [HttpPut("{personId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid personId, [FromBody] UpdateProjectPersonDto dto, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(dto.RelationshipType ?? dto.Source)) return Forbid();
        var result = await _service.UpdateAsync(projectId, personId, dto, ActorId(), cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [HttpPost("{personId:guid}/remove")]
    public async Task<IActionResult> Remove(Guid projectId, Guid personId, [FromBody] RemoveProjectPersonDto dto, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(dto.Source)) return Forbid();
        byte[]? rowVersion = null;
        if (!string.IsNullOrWhiteSpace(dto.RowVersion))
        {
            try { rowVersion = Convert.FromBase64String(dto.RowVersion); }
            catch (FormatException) { return BadRequest(new { error = "RowVersion must be valid Base64" }); }
        }
        var result = await _service.RevokeOrRemoveAsync(projectId, personId, dto.Source, rowVersion, ActorId(), cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [HttpDelete("sponsor")]
    public async Task<IActionResult> ClearSponsor(Guid projectId, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(ProjectRelationshipType.Sponsor)) return Forbid();
        var result = await _service.ClearSponsorAsync(projectId, ActorId(), cancellationToken);
        return result.IsFailure ? Map(result.Error!) : NoContent();
    }

    private async Task<bool> CanManageAsync(string? relationship)
    {
        var resource = await _authorization.AuthorizeAsync(User, "Resource.Assign");
        var stakeholders = await _authorization.AuthorizeAsync(User, "Stakeholders.Manage");
        return string.Equals(relationship, ProjectRelationshipType.TeamMember, StringComparison.OrdinalIgnoreCase)
            ? resource.Succeeded
            : resource.Succeeded || stakeholders.Succeeded;
    }

    private string ActorId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";

    private static IActionResult Map(NEXUS.SharedKernel.Error error) => error.StatusCode switch
    {
        404 => new NotFoundObjectResult(new { error = error.Message }),
        409 => new ConflictObjectResult(new { error = error.Message }),
        403 => new ObjectResult(new { error = error.Message }) { StatusCode = 403 },
        _ => new BadRequestObjectResult(new { error = error.Message })
    };
}

public sealed class RemoveProjectPersonDto
{
    public string Source { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}
