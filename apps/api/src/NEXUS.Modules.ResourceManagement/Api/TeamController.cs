using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;

namespace NEXUS.Modules.ResourceManagement.Api;

[Authorize]
[ApiController]
[Route("api/teams")]
public class TeamController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Team.View")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] bool includeMembers = true, CancellationToken cancellationToken = default)
    {
        var result = await _teamService.GetByIdAsync(id, includeMembers, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet]
    [Authorize(Policy = "Team.View")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _teamService.GetPagedAsync(page, pageSize, search, departmentId, isActive, cancellationToken);
        return Ok(result);
    }

    /// <summary>Selector-friendly active team list for the Resource form and My Work filters.</summary>
    [HttpGet("lookup")]
    [Authorize(Policy = "Team.View")]
    public async Task<IActionResult> GetLookup([FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
        => Ok(await _teamService.GetLookupAsync(activeOnly, cancellationToken));

    [HttpPost]
    [Authorize(Policy = "Team.Create")]
    public async Task<IActionResult> Create([FromBody] CreateTeamDto dto, CancellationToken cancellationToken)
    {
        var result = await _teamService.CreateAsync(dto, ActorId(), cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Team.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeamDto dto, CancellationToken cancellationToken)
    {
        var result = await _teamService.UpdateAsync(id, dto, ActorId(), cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/deactivate")]
    [Authorize(Policy = "Team.Delete")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _teamService.DeactivateAsync(id, ActorId(), cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/reactivate")]
    [Authorize(Policy = "Team.Delete")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _teamService.ReactivateAsync(id, ActorId(), cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Team.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _teamService.DeleteAsync(id, ActorId(), cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    [Authorize(Policy = "Team.View")]
    public async Task<IActionResult> GetMembers(Guid id, [FromQuery] bool currentOnly = true, CancellationToken cancellationToken = default)
    {
        var result = await _teamService.GetMembersAsync(id, currentOnly, cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = "Team.Edit")]
    public async Task<IActionResult> AddMembers(Guid id, [FromBody] AddTeamMembersDto dto, CancellationToken cancellationToken)
    {
        var result = await _teamService.AddMembersAsync(id, dto, ActorId(), cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/members")]
    [Authorize(Policy = "Team.Edit")]
    public async Task<IActionResult> SetMembers(Guid id, [FromBody] SetTeamMembersDto dto, CancellationToken cancellationToken)
    {
        var result = await _teamService.SetMembersAsync(id, dto, ActorId(), cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}/members/{resourceId:guid}")]
    [Authorize(Policy = "Team.Edit")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid resourceId, [FromBody] RemoveTeamMemberDto? dto, CancellationToken cancellationToken)
    {
        var result = await _teamService.RemoveMemberAsync(id, resourceId, dto ?? new RemoveTeamMemberDto(), ActorId(), cancellationToken);
        if (result.IsFailure) return Failure(result.Error);
        return Ok(result.Value);
    }

    private string ActorId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "System";

    private IActionResult Failure(NEXUS.SharedKernel.Error? error) => error?.StatusCode switch
    {
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        503 => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message })
    };
}
