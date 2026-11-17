using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/people")]
[Authorize]
public sealed class ProjectPeopleController : ControllerBase
{
    private readonly IProjectPeopleService _service;
    private readonly IAuthorizationService _authorization;
    private readonly IManagementContextAccessor? _managementContext;

    public ProjectPeopleController(IProjectPeopleService service, IAuthorizationService authorization, IManagementContextAccessor? managementContext = null)
    {
        _service = service;
        _authorization = authorization;
        _managementContext = managementContext;
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(projectId, cancellationToken);
        if (result.IsFailure) return Map(result.Error!);
        var canReadResourceContacts = (await _authorization.AuthorizeAsync(User, "Resources.View")).Succeeded;
        var canReadStakeholderContacts = (await _authorization.AuthorizeAsync(User, "Stakeholders.Read")).Succeeded;
        foreach (var person in result.Value!)
        {
            var canReadEmail = string.Equals(person.Source, "Stakeholder", StringComparison.OrdinalIgnoreCase)
                ? canReadStakeholderContacts
                : canReadResourceContacts;
            if (!canReadEmail) person.Email = null;
        }
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Add(Guid projectId, [FromBody] CreateProjectPersonDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var actorId);
        if (denied is not null) return denied;
        if (!await CanManageAsync(dto.RelationshipType)) return Forbid();
        if (dto.TeamId.HasValue && !(await _authorization.AuthorizeAsync(User, "Team.View")).Succeeded) return Forbid();
        var result = await _service.AddAsync(projectId, dto, actorId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [HttpPut("{personId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid personId, [FromBody] UpdateProjectPersonDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var actorId);
        if (denied is not null) return denied;
        if (!await CanManageAsync(dto.RelationshipType ?? dto.Source)) return Forbid();
        if ((dto.TeamId.HasValue || await _service.IsTeamGroupAssignmentAsync(projectId, personId, cancellationToken))
            && !(await _authorization.AuthorizeAsync(User, "Team.View")).Succeeded) return Forbid();
        var result = await _service.UpdateAsync(projectId, personId, dto, actorId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [HttpPost("{personId:guid}/remove")]
    public async Task<IActionResult> Remove(Guid projectId, Guid personId, [FromBody] RemoveProjectPersonDto dto, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var actorId);
        if (denied is not null) return denied;
        if (!await CanManageAsync(dto.Source)) return Forbid();
        if ((dto.TeamId.HasValue || await _service.IsTeamGroupAssignmentAsync(projectId, personId, cancellationToken))
            && !(await _authorization.AuthorizeAsync(User, "Team.View")).Succeeded) return Forbid();
        byte[]? rowVersion = null;
        if (!string.IsNullOrWhiteSpace(dto.RowVersion))
        {
            try { rowVersion = Convert.FromBase64String(dto.RowVersion); }
            catch (FormatException) { return BadRequest(new { error = "RowVersion must be valid Base64" }); }
        }
        var result = await _service.RevokeOrRemoveAsync(projectId, personId, dto.Source, rowVersion, actorId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : Ok(result.Value);
    }

    [HttpDelete("sponsor")]
    public async Task<IActionResult> ClearSponsor(Guid projectId, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var actorId);
        if (denied is not null) return denied;
        if (!await CanManageAsync(ProjectRelationshipType.Sponsor)) return Forbid();
        var result = await _service.ClearSponsorAsync(projectId, actorId, cancellationToken);
        return result.IsFailure ? Map(result.Error!) : NoContent();
    }

    /// <summary>
    /// Team membership is a resourcing action (Resource.Assign). Sponsors and other
    /// governance stakeholders are not: they change project accountability, so they require
    /// the dedicated stakeholder permission instead of the allocation permission.
    /// </summary>
    private async Task<bool> CanManageAsync(string? relationship)
    {
        if (string.Equals(relationship, ProjectRelationshipType.TeamMember, StringComparison.OrdinalIgnoreCase))
            return (await _authorization.AuthorizeAsync(User, "Resource.Assign")).Succeeded;

        if (string.Equals(relationship, ProjectRelationshipType.Sponsor, StringComparison.OrdinalIgnoreCase)
            || string.Equals(relationship, ProjectRelationshipType.Stakeholder, StringComparison.OrdinalIgnoreCase)
            || string.Equals(relationship, ProjectRelationshipType.Other, StringComparison.OrdinalIgnoreCase))
            return (await _authorization.AuthorizeAsync(User, "Stakeholders.Manage")).Succeeded;

        return false;
    }

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
    public Guid? TeamId { get; set; }
    public string? RowVersion { get; set; }
}
